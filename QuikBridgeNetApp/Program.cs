using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuikBridgeNet;
using QuikBridgeNet.Helpers;
using QuikBridgeNetDomain.Entities;
using Serilog;

class Program
{
    static async Task Main(string[] args)
    {
        
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false, true);
        var configuration = builder.Build();

        var serviceCollection = new ServiceCollection();
        QuikBridgeServiceConfiguration.ConfigureServices(serviceCollection, configuration);
        
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Resolve the client
        var client = serviceProvider.GetRequiredService<QuikBridge>();
        client.IsExtendedLogging = false;
        client.ConnectionStateChanged += OnBridgeConnectionStateChanged;
        var dataSource = "";
        
        client.RegisterDataSourceCallback((msg) =>
        {
            Log.Information("DataSource message {body}", msg.body?.ToString());
        });
        
        CancellationTokenSource cts = new CancellationTokenSource();
        
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        
        var globalEventAggregator = serviceProvider.GetRequiredService<QuikBridgeNetEvents.QuikBridgeEventAggregator>();
        
        // Subscribe to the global events
        globalEventAggregator.SubscribeToInstrumentClassesUpdate( (eventObj) =>
        {
            Log.Information("{DataType} arrived: {NumClasses}", eventObj.InstrumentClassType.GetDescription(), eventObj.InstrumentClasses.Count);
            return Task.CompletedTask;
        });
        
        globalEventAggregator.SubscribeToAllTrades( eventObj =>
        {
            if (eventObj.Trade != null)
            {
                Log.Information("Trade arrived: {Security} {Qty} x {Price}", eventObj.Trade.sec_code,
                    eventObj.Trade.qty, eventObj.Trade.price);
            }

            return Task.CompletedTask;
        });
        
        globalEventAggregator.SubscribeToSecurityInfo( eventObj =>
        {
            if (eventObj.Contract != null)
            {
                Log.Information("Security contract arrived: {Security} {ClassCode} with lot size {LotSize}",
                    eventObj.Contract.code, eventObj.Contract.class_code, eventObj.Contract.lot_size);
            }

            return Task.CompletedTask;
        });
        
        globalEventAggregator.SubscribeToInstrumentParameterUpdate( eventArgs =>
        {
            Log.Information("Instrument parameter {Name} current value {Val} ", eventArgs.ParamName, eventArgs.ParamValue);
            return Task.CompletedTask;
        });
        
        globalEventAggregator.SubscribeToOrderBookUpdate( eventArgs =>
        {
            Log.Information("Order book: bids number {Bids}; asks number {Offers} ", eventArgs.OrderBook?.bid_count, eventArgs.OrderBook?.offer_count);
            return Task.CompletedTask;
        });
        
        globalEventAggregator.SubscribeToServiceMessages( eventObj =>
        {
            Log.Information("Arrived message with type {MsgType}", eventObj.BridgeMessage?.MessageType.GetDescription());
            return Task.CompletedTask;
        });
        
        globalEventAggregator.SubscribeToDataSourceSet( async eventObj =>
        {
            if (eventObj.BridgeMessage != null)
            {
                Log.Information("Datasource is set for instrument {Ticker} with time frame {Interval}",
                    eventObj.BridgeMessage.Ticker, eventObj.BridgeMessage.Interval);
            }

            dataSource = eventObj.DataSourceName;
            await client.GetBar(eventObj.DataSourceName, MessageType.Close, 1);
        });
        
        await client.StartAsync(cts.Token);

        //var testClassCode = "TQBR";
        var testClassCode = "SPBFUT";
        
        //var testTicker = "SBER";
        var testTicker = "SiH5";

        //await client.GetClassesList();
        //await client.GetClassSecurities(testClassCode);
        //await client.GetSecurityInfo(testClassCode, testTicker);
        Thread.Sleep(5000);
        var subscriptionToken = await client.SubscribeToQuotesTableParams(testClassCode, testTicker, "LAST");
            
        //await client.SubscribeToOrderBook( testClassCode, testTicker);
        
        //await client.CreateDs(testClassCode, testTicker, "5");

        //await client.SetGlobalCallback(MessageType.OnAllTrade);
        
        Console.WriteLine("Press any key to stop...");
        Console.ReadKey();
        await client.UnsubscribeToQuotesTableParams(testClassCode, testTicker, "LAST", subscriptionToken);
        client.Finish();
        await Log.CloseAndFlushAsync();
    }

    static void OnBridgeConnectionStateChanged(QuikBridgeConnectionState newState)
    {
        Log.Information($"Изменилось состояние подключения моста на {newState.GetDescription()}");
    }
}
