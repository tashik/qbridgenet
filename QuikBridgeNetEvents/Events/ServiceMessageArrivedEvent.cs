using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNetEvents.Events;

public class ServiceMessageArrivedEvent
{
    public JsonMessage? Response { get; set; }
    
    public QMessage? BridgeMessage { get; set; }
}