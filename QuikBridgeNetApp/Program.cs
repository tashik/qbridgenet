using Microsoft.Extensions.DependencyInjection;
using QuikBridgeNet;
using QuikBridgeNet.Entities;
using QuikBridgeNet.Helpers;
using Serilog;

class Program
{
    static async Task Main(string[] args)
    {
        string host = "127.0.0.1";
        int port = 57777;
        
        QuikBridgeServiceConfiguration.ConfigureServices();
        
        var serviceProvider = new ServiceCollection()
            .AddSingleton<QuikBridge>(provider =>
            {
                var bridge = new QuikBridge(QuikBridgeServiceConfiguration.ServiceProvider);
                return bridge;
            })
            .BuildServiceProvider();

        // Resolve the client
        var client = serviceProvider.GetRequiredService<QuikBridge>();
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
        
        await client.StartAsync(host, port, cts.Token);

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
}
