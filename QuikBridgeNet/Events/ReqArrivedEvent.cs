using System.Text.Json;
using QuikBridgeNet.Entities;

namespace QuikBridgeNet.Events;

public class ReqArrivedEvent : IDomainEvent
{
    public JsonMessage Req { get; }
    public DateTime ReceivedAt { get; }

    public ReqArrivedEvent(JsonMessage data)
    {
        Req = data;
        ReceivedAt = DateTime.Now;
    }
}