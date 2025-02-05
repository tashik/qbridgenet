using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuikBridgeNet.EventHandlers;
using QuikBridgeNet.Events;
using QuikBridgeNetDomain;
using Serilog;

namespace QuikBridgeNet;

public static class QuikBridgeServiceConfiguration
{
    public static void ConfigureServices(IServiceCollection serviceCollection, IConfiguration? configuration)
    {
        var quikBridgeConfig = new QuikBridgeConfig();
        if (configuration != null)
        {
            quikBridgeConfig = configuration.GetSection("Bridge").Get<QuikBridgeConfig>() ??
                                   new QuikBridgeConfig();
        }
        
        serviceCollection.AddSingleton(quikBridgeConfig);
        
        serviceCollection.AddSingleton<QuikBridgeNetEvents.QuikBridgeEventAggregator>();
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