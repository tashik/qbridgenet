using QuikBridgeNet.Entities;
using QuikBridgeNet.Events;
using Serilog;

namespace QuikBridgeNet.EventHandlers;

public class RespArrivedEventHandler : IDomainEventHandler<RespArrivedEvent>
{
    private readonly MessageRegistry _messageRegistry;
    private readonly QuikBridgeEventAggregator _eventAggregator;
    
    public RespArrivedEventHandler(MessageRegistry messageRegistry, QuikBridgeEventAggregator globalEventAggregator)
    {
        _messageRegistry = messageRegistry;
        _eventAggregator = globalEventAggregator;
    }
    public Task HandleAsync(RespArrivedEvent domainEvent)
    {
        var msg = domainEvent.Req;
        Log.Debug("resp arrived with message id {0}", msg.id);
        
        if (!_messageRegistry.TryGetMetadata(msg.id, out var newMessage)) return Task.CompletedTask;
        if (newMessage == null) return Task.CompletedTask;
        Log.Debug("resp method is {0}", newMessage.Method);

        switch (newMessage.MessageType)
        {
            case MessageType.Classes:
                List<string> classes = new();
                var resultToken = msg.body?["result"] ?? null;
                var classData = resultToken?.ToObject<List<string>>();
                if (classData != null)
                {
                    foreach (var cl in from c in classData where c.Contains(',') select c.Split(',').ToList())
                    {
                        classes.AddRange(cl);
                    }
                }
                _ = _eventAggregator.RaiseInstrumentClassesUpdateEvent(this, classes);
                break;
            default:
                _ = _eventAggregator.RaiseServiceMessageArrivedEvent(this, msg, newMessage);
                break;
        }
        
        return Task.CompletedTask;
    }
}