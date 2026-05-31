using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNetEvents.Events;

public class AccountPositionArrivedEvent
{
    public AccountPosition? Position { get; set; }
}