using QuikBridgeNet.Events;
using Serilog;

namespace QuikBridgeNet.EventHandlers;

public class SocketConnectionCloseEventHandler: IDomainEventHandler<SocketConnectionCloseEvent>
{
    public Task HandleAsync(SocketConnectionCloseEvent domainEvent)
    {
        Log.Debug("Close connection confirmed by Quik");
        return Task.CompletedTask;
    }
}