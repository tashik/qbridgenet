using Microsoft.Extensions.DependencyInjection;
using QuikBridgeNet.EventHandlers;
using QuikBridgeNet.Events;
using Serilog;

namespace QuikBridgeNet;

public static class QuikBridgeServiceConfiguration
{
    private static IServiceProvider _serviceProvider;

    public static void ConfigureServices()
    {
        var serviceCollection = new ServiceCollection();

        // Register services and event handlers
        serviceCollection.AddSingleton<ILogger>(provider => new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger());
        
        serviceCollection.AddSingleton<MessageRegistry>();
        serviceCollection.AddTransient<IDomainEventHandler<RespArrivedEvent>, RespArrivedEventHandler>();
        serviceCollection.AddTransient<IDomainEventHandler<ReqArrivedEvent>, ReqArrivedEventHandler>();
        serviceCollection.AddTransient<IDomainEventHandler<SocketConnectionCloseEvent>, SocketConnectionCloseEventHandler>();
        serviceCollection.AddSingleton<QuikBridgeEventDispatcher>();

        _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    public static IServiceProvider ServiceProvider => _serviceProvider;
}