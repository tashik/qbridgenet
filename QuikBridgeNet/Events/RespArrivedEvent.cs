using QuikBridgeNet.Entities;
using QuikBridgeNet.Entities.ProtocolData;
using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNet.Events;

public class RespArrivedEvent : ReqArrivedEvent
{
    public RespArrivedEvent(JsonMessage data) : base(data)
    {
    }
}