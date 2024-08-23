using QuikBridgeNet.Events;
using Serilog;

namespace QuikBridgeNet.EventHandlers;

public class ReqArrivedEventHandler: IDomainEventHandler<ReqArrivedEvent>
{
    private readonly MessageRegistry _messageRegistry;
    private readonly QuikBridgeGlobalEventAggregator _eventAggregator;
    
    public ReqArrivedEventHandler(MessageRegistry messageRegistry, QuikBridgeGlobalEventAggregator globalEventAggregator)
    {
        _messageRegistry = messageRegistry;
        _eventAggregator = globalEventAggregator;
    }
    public Task HandleAsync(ReqArrivedEvent domainEvent)
    {
        var msgId = domainEvent.Req.id;
        Log.Debug("msg arrived with message id " + msgId);

        _messageRegistry.TryGetMetadata(msgId, out var quikMessage);

        var methodToken = domainEvent.Req.body?["function"] ?? domainEvent.Req.body?["method"];
        var method = methodToken?.ToString();

        switch (method)
        {
            case "paramChange":
                var paramName = domainEvent.Req.body?["param"]?.ToString();
                var valueToken = domainEvent.Req.body?["value"];
                var value = valueToken?.ToString();
                var secCode = domainEvent.Req.body?["security"]?.ToString();
                var classCode = domainEvent.Req.body?["class"]?.ToString();

                _eventAggregator.RaiseInstrumentParameterUpdateEvent(this, secCode, classCode, paramName, value);
                break;
                /*param_name = data["param"]
                param_value = data["value"]
                event_data = {
                    "sec_code": data["security"],
                    "class_code": data["class"],
                    param_name: param_value
                }
                event = Event(EVENT_QUOTESTABLE_PARAM_UPDATE, event_data)
                self.fire(event)

            elif data["method"] == "quotesChange" and "quotes" in data.keys():
                quotes = data["quotes"]
                event_data = {
                    "sec_code": data["security"],
                    "class_code": data["class"]
                }
                if "bid_count" in quotes.keys() and "offer_count" in quotes.keys() and Decimal(quotes["bid_count"]) > 0 and Decimal(quotes["offer_count"]) > 0:
                    event_data["order_book"] = quotes
                    event = Event(EVENT_ORDERBOOK, event_data)
                    self.fire(event)
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