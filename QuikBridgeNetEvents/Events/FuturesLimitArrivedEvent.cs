using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNetEvents.Events;

public class FuturesLimitArrivedEvent
{
    public FuturesLimit? Limit { get; set; }
}