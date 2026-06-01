using QuikBridgeNet.Events;
using QuikBridgeNetDomain.Entities;
using Serilog;

namespace QuikBridgeNet.EventHandlers;

public class SocketConnectionCloseEventHandler(QuikBridge quikBridge): IDomainEventHandler<SocketConnectionCloseEvent>
{
    public async Task HandleAsync(SocketConnectionCloseEvent domainEvent)
    {
        Log.Warning("Соединение с QuikQtBridge закрыто.");
        await quikBridge.HandleConnectionLostAsync();
    }
}