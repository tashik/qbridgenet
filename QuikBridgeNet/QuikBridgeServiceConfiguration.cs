using Microsoft.Extensions.DependencyInjection;
using QuikBridgeNet.EventHandlers;
using QuikBridgeNet.Events;
using Serilog;

namespace QuikBridgeNet;

public static class QuikBridgeServiceConfiguration
{
    public static void ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<QuikBridgeEventAggregator>();
        serviceCollection.AddSingleton<ILogger>(provider => new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger());
        
        serviceCollection.AddSingleton<MessageRegistry>();
        serviceCollection.AddTransient<IDomainEventHandler<RespArrivedEvent>, RespArrivedEventHandler>();
        serviceCollection.AddTransient<IDomainEventHandler<ReqArrivedEvent>, ReqArrivedEventHandler>();
        serviceCollection.AddTransient<IDomainEventHandler<SocketConnectionCloseEvent>, SocketConnectionCloseEventHandler>();
        serviceCollection.AddSingleton<QuikBridgeEventDispatcher>();
        serviceCollection.AddSingleton<QuikBridgeProtocolHandler>();
        serviceCollection.AddSingleton<QuikBridge>();
    }
}