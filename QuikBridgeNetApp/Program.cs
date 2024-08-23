using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using QuikBridgeNet;
using QuikBridgeNet.EventHandlers;
using QuikBridgeNet.Events;
using Serilog;

class Program
{
    static async Task Main(string[] args)
    {
        string host = "127.0.0.1";
        int port = 57777;
        
        QuikBridgeServiceConfiguration.ConfigureServices();
        
        var serviceProvider = new ServiceCollection()
            .AddSingleton<Socket>(provider => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            .AddSingleton<MessageRegistry>()
            .AddSingleton<QuikBridgeEventDispatcher>()
            .AddTransient<IDomainEventHandler<RespArrivedEvent>, RespArrivedEventHandler>()
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
        
        await client.StartAsync(host, port, cts.Token);

        await client.GetClassesList();
        Console.WriteLine("Press any key to stop...");
        Console.ReadKey();

        client.Finish();
        await Log.CloseAndFlushAsync();
    }
}
