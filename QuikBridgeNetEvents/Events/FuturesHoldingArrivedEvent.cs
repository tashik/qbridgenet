using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNetEvents.Events;

public class FuturesHoldingArrivedEvent
{
    public FuturesHolding? Holding { get; set; }
}