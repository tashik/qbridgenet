using QuikBridgeNet.Entities;

namespace QuikBridgeNet.Events;

public class RespArrivedEvent : ReqArrivedEvent
{
    public RespArrivedEvent(JsonMessage data) : base(data)
    {
    }
}