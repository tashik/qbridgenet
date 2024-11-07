using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuikBridgeNet;
using QuikBridgeNet.Entities;
using QuikBridgeNet.Helpers;
using Serilog;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        var client = host.Services.GetRequiredService<QuikBridgeService>();
        
        client.IsExtendedLogging = false;
        client.ConnectionStateChanged += OnBridgeConnectionStateChanged;
        var dataSource = "";
        
        client.RegisterDataSourceCallback((msg) =>
        {
            Log.Information("DataSource message {body}", msg.body?.ToString());
        });
        CancellationTokenSource cts = new CancellationTokenSource();
        
        var globalEventAggregator = client.GetGlobalEventAggregator();
        
        // Subscribe to the global events
        globalEventAggregator.SubscribeToInstrumentClassesUpdate( (instrumentClasses, dataType) =>
        {
            Log.Information("{DataType} arrived: {NumClasses}", dataType.GetDescription(), instrumentClasses.Count);
            return Task.CompletedTask;
        });
        
        globalEventAggregator.SubscribeToAllTrades( trade =>
        {
            Log.Information("Trade arrived: {Security} {Qty} x {Price}", trade.sec_code, trade.qty, trade.price);
            return Task.CompletedTask;
        });
        
        globalEventAggregator.SubscribeToSecurityInfo( contract =>
        {
            Log.Information("Security contract arrived: {Security} {ClassCode} with lot size {LotSize}", contract.code, contract.class_code, contract.lot_size);
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
        
        globalEventAggregator.SubscribeToServiceMessages( (resp, registeredReq) =>
        {
            Log.Information("Arrived message with type {MsgType}", registeredReq?.MessageType.GetDescription());
            return Task.CompletedTask;
        });
        
        globalEventAggregator.SubscribeToDataSourceSet( async (dataSourceName, dataSourceReq) =>
        {
            Log.Information("Datasource is set for instrument {Ticker} with time frame {Interval}", dataSourceReq?.Ticker, dataSourceReq?.Interval);
            dataSource = dataSourceName;
            await client.GetBar(dataSourceName, MessageType.Close, 1);
        });
        
        await client.StartAsync( "127.0.0.1", 57777, cts.Token);
        await host.RunAsync(cts.Token);
        
        var testClassCode = "TQBR";
        
        var testTicker = "SBER";

        await client.GetClassesList();
        await client.GetClassSecurities(testClassCode);
        await client.GetSecurityInfo(testClassCode, testTicker);

        await client.SubscribeToQuotesTableParams(testClassCode, testTicker, "LAST");

        await client.SubscribeToOrderBook( testClassCode, testTicker);

        await client.CreateDs(testClassCode, testTicker, "5");

        //await client.SetGlobalCallback(MessageType.OnAllTrade);
        
        Console.WriteLine("Press any key to stop...");
        Console.ReadKey();

        client.Finish();
        await Log.CloseAndFlushAsync();
    }
    
    static void OnBridgeConnectionStateChanged(QuikBridgeConnectionState newState)
    {
        Log.Information($"Изменилось состояние подключения моста на {newState.GetDescription()}");
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                QuikBridgeServiceConfiguration.ConfigureServices(services);
                services.AddHostedService<QuikBridgeService>();
                
                // Configure Serilog
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();
            });
}