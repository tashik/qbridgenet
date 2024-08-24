using QuikBridgeNet.Entities;
using QuikBridgeNet.Events;
using Serilog;

namespace QuikBridgeNet.EventHandlers;

public class ReqArrivedEventHandler: IDomainEventHandler<ReqArrivedEvent>
{
    private readonly MessageRegistry _messageRegistry;
    private readonly QuikBridgeEventAggregator _eventAggregator;
    
    public ReqArrivedEventHandler(MessageRegistry messageRegistry, QuikBridgeEventAggregator globalEventAggregator)
    {
        _messageRegistry = messageRegistry;
        _eventAggregator = globalEventAggregator;
    }
    public Task HandleAsync(ReqArrivedEvent domainEvent)
    {
        var msgId = domainEvent.Req.id;
        Log.Debug("msg arrived with message id " + msgId);

        _messageRegistry.TryGetMetadata(msgId, out var qMessage);

        var methodToken = domainEvent.Req.body?["function"] ?? domainEvent.Req.body?["method"];
        var method = methodToken?.ToString();
        
        var secCode = domainEvent.Req.body?["security"]?.ToString();
        var classCode = domainEvent.Req.body?["class"]?.ToString();

        switch (method)
        {
            case "paramChange":
                var paramName = domainEvent.Req.body?["param"]?.ToString();
                var valueToken = domainEvent.Req.body?["value"];
                var value = valueToken?.ToString();
                

                _ = _eventAggregator.RaiseInstrumentParameterUpdateEvent(this, secCode, classCode, paramName, value);
                break;
            case "quotesChange":
                var quotesToken = domainEvent.Req.body?["quotes"];
                var orderBook = quotesToken?.ToObject<OrderBook>();
                if (orderBook != null)
                {
                    _ = _eventAggregator.RaiseOrderBookUpdateEvent(this, secCode, classCode, orderBook);
                }

                break;
            default:
                _ = _eventAggregator.RaiseServiceMessageArrivedEvent(this, domainEvent.Req, qMessage);
                break;
                /*
            elif data["method"] == "callback" and "OnOrder" == data["name"]:
                order = data["arguments"][0]

                if order["trans_id"] != "0": # не программные ордера будут приходить с 0
                    event_data = {
                        "order": order
                    }
                    event = Event(EVENT_ORDER_UPDATE, event_data)
                    self.fire(event)
            elif data["method"] == "callback" and "OnTransReply" == data["name"]:
                order = data["arguments"][0]
                if order["trans_id"] != "0": # не программные ордера будут приходить с 0
                    event_data = {
                        "order": order
                    }
                    event = Event(EVENT_ORDER_UPDATE, event_data)
                    self.fire(event)
            elif data['method'] == "callback" and "OnTrade" == data["name"]:
                trade = data["arguments"][0]
                event_data = {
                    "trade": trade
                }
                event = Event(EVENT_NEW_TRADE, event_data) */
        }

        return Task.CompletedTask;
    }
}