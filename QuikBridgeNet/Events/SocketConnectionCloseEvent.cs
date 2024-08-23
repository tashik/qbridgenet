namespace QuikBridgeNet.Events;

public class SocketConnectionCloseEvent: IDomainEvent
{
    public DateTime ReceivedAt { get; } = DateTime.Now;
}