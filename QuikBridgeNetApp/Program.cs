﻿using Microsoft.Extensions.DependencyInjection;
using QuikBridgeNet;
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
        
        CancellationTokenSource cts = new CancellationTokenSource();
        
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug() // Adjust as needed
            .WriteTo.Console()
            .CreateLogger();
        
        var globalEventAggregator = client.GetGlobalEventAggregator();

        // Subscribe to the global events
        globalEventAggregator.InstrumentCLassesUpdateEvent += (sender, eventArgs) =>
        {
            Log.Information("Instrument classes number arrived: {NumClasses}", eventArgs.Count);
        };
        
        globalEventAggregator.InstrumentParameterUpdateEvent += (sender, eventArgs) =>
        {
            Log.Information("Instrument parameter {Name} current value {Val} ", eventArgs.ParamName, eventArgs.ParamValue);
        };
        
        globalEventAggregator.OrderbookUpdateEvent += (sender, eventArgs) =>
        {
            Log.Information("Orderbook: bids number {Bids}; asks number {Offers} ", eventArgs.OrderBook?.bid_count, eventArgs.OrderBook?.offer_count);
        };
        
        await client.StartAsync(host, port, cts.Token);

        await client.GetClassesList();

        await client.SubscribeToQuotesTableParams("SPBFUT", "SiU4", "LAST");

        await client.SubscribeToOrderBook( "SPBFUT", "SiU4");
        
        Console.WriteLine("Press any key to stop...");
        Console.ReadKey();

        client.Finish();
        await Log.CloseAndFlushAsync();
    }
}
