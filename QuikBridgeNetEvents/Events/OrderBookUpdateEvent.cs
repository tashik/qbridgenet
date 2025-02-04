using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNetEvents.Events;

public class OrderBookUpdateEvent
{
    public string? SecCode { get; set; }
    public string? ClassCode { get; set; }
    public OrderBook? OrderBook { get; set; }
}