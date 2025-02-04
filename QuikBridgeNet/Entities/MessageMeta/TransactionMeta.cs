using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNet.Entities.MessageMeta;

public class TransactionMeta : Subscription
{
    public TransactionBase? Transaction { get; set; }
}