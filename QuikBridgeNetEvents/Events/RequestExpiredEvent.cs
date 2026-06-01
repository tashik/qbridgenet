using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNetEvents.Events;

public class RequestExpiredEvent
{
    public QMessage? BridgeMessage { get; set; }
    public int TimeoutMs { get; set; }
}