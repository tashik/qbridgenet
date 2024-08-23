using QuikBridgeNet.Events;
using Serilog;

namespace QuikBridgeNet.EventHandlers;

public class ReqArrivedEventHandler: IDomainEventHandler<ReqArrivedEvent>
{
    public Task HandleAsync(ReqArrivedEvent domainEvent)
    {
        Log.Debug("msg arrived with message id " + domainEvent.Req.id);
        return Task.CompletedTask;
    }
}