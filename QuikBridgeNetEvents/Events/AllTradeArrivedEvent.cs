using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNetEvents.Events;

public class AllTradeArrivedEvent
{
    public AllTrade? Trade { get; set; }
}