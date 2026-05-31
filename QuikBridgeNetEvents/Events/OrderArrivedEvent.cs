using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNetEvents.Events;

public class OrderArrivedEvent
{
    public Order? Order { get; set; }
}