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
        
        client.RegisterDataSourceCallback((msg) =>
        {
            Log.Information("DataSource message {body}", msg.body?.ToString());
        });
        
        CancellationTokenSource cts = new CancellationTokenSource();
        
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();
        
        var globalEventAggregator = client.GetGlobalEventAggregator();
        
        // Subscribe to the global events
        globalEventAggregator.SubscribeToInstrumentClassesUpdate( (sender, instrumentClasses) =>
        {
            Log.Information("Instrument classes number arrived: {NumClasses}", instrumentClasses.Count);
            return Task.CompletedTask;
        });
        
        globalEventAggregator.SubscribeToInstrumentParameterUpdate( (sender, eventArgs) =>
        {
            Log.Information("Instrument parameter {Name} current value {Val} ", eventArgs.ParamName, eventArgs.ParamValue);
            return Task.CompletedTask;
        });
        
        globalEventAggregator.SubscribeToOrderBookUpdate( (sender, eventArgs) =>
        {
            Log.Information("Order book: bids number {Bids}; asks number {Offers} ", eventArgs.OrderBook?.bid_count, eventArgs.OrderBook?.offer_count);
            return Task.CompletedTask;
        });
        
        globalEventAggregator.SubscribeToServiceMessages( (sender, resp, registeredReq) =>
        {
            Log.Information("Arrived message with type {MsgType}", registeredReq?.MessageType.GetDescription());
            return Task.CompletedTask;
        });
        
        await client.StartAsync(host, port, cts.Token);

        await client.GetClassesList();

        await client.SubscribeToQuotesTableParams("SPBFUT", "SiU4", "LAST");

        await client.SubscribeToOrderBook( "SPBFUT", "SiU4");

        await client.CreateDs("SPBFUT", "SiU4", "5");
        
        Console.WriteLine("Press any key to stop...");
        Console.ReadKey();

        client.Finish();
        await Log.CloseAndFlushAsync();
    }
}
