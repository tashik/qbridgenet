using Newtonsoft.Json.Linq;
using QuikBridgeNet.Entities;
using QuikBridgeNet.Events;
using QuikBridgeNetDomain.Entities;
using QuikBridgeNetEvents.Events;
using Serilog;

namespace QuikBridgeNet.EventHandlers;

public class ReqArrivedEventHandler: IDomainEventHandler<ReqArrivedEvent>
{
    private readonly MessageRegistry _messageRegistry;
    private readonly QuikBridgeEventAggregator _eventAggregator;
    private readonly QuikBridgeNetEvents.QuikBridgeEventAggregator _newEventAggregator;
    
    public ReqArrivedEventHandler(MessageRegistry messageRegistry, QuikBridgeEventAggregator globalEventAggregator, QuikBridgeNetEvents.QuikBridgeEventAggregator newEventAggregator)
    {
        _messageRegistry = messageRegistry;
        _eventAggregator = globalEventAggregator;
        _newEventAggregator = newEventAggregator;
    }
    public Task HandleAsync(ReqArrivedEvent domainEvent)
    {
        var msgId = domainEvent.Req.id;
        //Log.Debug("msg arrived with message id " + msgId);

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
                

                _ = _eventAggregator.RaiseInstrumentParameterUpdateEvent(secCode, classCode, paramName, value);
                _ = _newEventAggregator.RaiseEvent(new InstrumentParametersUpdateEvent() {
                    SecCode = secCode, ClassCode = classCode, ParamName = paramName, ParamValue = value});
                break;
            case "quotesChange":
                var quotesToken = domainEvent.Req.body?["quotes"];
                var orderBook = quotesToken?.ToObject<OrderBook>();
                if (orderBook != null)
                {
                    _ = _eventAggregator.RaiseOrderBookUpdateEvent(secCode, classCode, orderBook);
                    _ = _newEventAggregator.RaiseEvent(new OrderBookUpdateEvent() {
                        SecCode = secCode,ClassCode = classCode, OrderBook = orderBook
                    });
                }

                break;
            case "callback":
                var funcNameToken = domainEvent.Req.body?["name"];
                if (funcNameToken != null)
                {
                    var funcName = funcNameToken.ToString();
                    switch (funcName)
                    {
                        case "OnAllTrade":
                            var resultToken = domainEvent.Req.body?["arguments"] ?? null;
                            if (resultToken is JArray jArray)
                            {
                                var trades = new List<AllTrade>();
                                foreach (var r in jArray)
                                {
                                    var oneTrade = r.ToObject<AllTrade>();
                                    if (oneTrade != null)
                                    {
                                        trades.Add(oneTrade);
                                    }
                                }
                                if (trades.Count > 0)
                                {
                                    foreach (var t in trades)
                                    {
                                        _ = _eventAggregator.RaiseNewAllTradeEvent(t);
                                        _ = _newEventAggregator.RaiseEvent(new AllTradeArrivedEvent() { Trade = t});
                                    }
                                }
                            }
                            
                            break;
                    }
                }
                break;
            default:
                _ = _eventAggregator.RaiseServiceMessageArrivedEvent(domainEvent.Req, qMessage);
                _ = _newEventAggregator.RaiseEvent(new ServiceMessageArrivedEvent() {Response = domainEvent.Req, BridgeMessage = qMessage});
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