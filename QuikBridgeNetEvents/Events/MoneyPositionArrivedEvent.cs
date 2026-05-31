using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNetEvents.Events;

public class MoneyPositionArrivedEvent
{
    public MoneyPosition? Position { get; set; }
}