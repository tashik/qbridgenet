using QuikBridgeNet.Events;
using Serilog;

namespace QuikBridgeNet.EventHandlers;

public class SocketConnectionCloseEventHandler: IDomainEventHandler<SocketConnectionCloseEvent>
{
    public Task HandleAsync(SocketConnectionCloseEvent domainEvent)
    {
        Log.Debug("close connection msg arrived");
        return Task.CompletedTask;
    }
}