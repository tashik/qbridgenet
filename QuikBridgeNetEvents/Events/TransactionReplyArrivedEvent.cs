using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNetEvents.Events;

public class TransactionReplyArrivedEvent
{
    public Order? Order { get; set; }
}