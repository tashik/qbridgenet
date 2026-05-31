using Newtonsoft.Json.Linq;
using QuikBridgeNet.Events;
using QuikBridgeNetDomain;
using QuikBridgeNetDomain.Entities;
using QuikBridgeNetEvents;
using QuikBridgeNetEvents.Events;
using Serilog;

namespace QuikBridgeNet.EventHandlers;

public class ReqArrivedEventHandler(MessageRegistry messageRegistry, QuikBridgeEventAggregator globalEventAggregator, QuikBridgeConfig bridgeConfig)
    : IDomainEventHandler<ReqArrivedEvent>
{
    private readonly bool _isExtendedLogging = bridgeConfig.UseExtendedLogging;
    
    public Task HandleAsync(ReqArrivedEvent domainEvent)
    {
        var msgId = domainEvent.Req.id;
        if (_isExtendedLogging) Log.Debug("msg arrived with message id " + msgId);

        messageRegistry.TryGetMetadata(msgId, out var qMessage);

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
                
                _ = globalEventAggregator.RaiseEvent(new InstrumentParametersUpdateEvent() {
                    SecCode = secCode, ClassCode = classCode, ParamName = paramName, ParamValue = value});
                break;
            case "quotesChange":
                var quotesToken = domainEvent.Req.body?["quotes"];
                var orderBook = quotesToken?.ToObject<OrderBook>();
                if (orderBook != null)
                {
                    _ = globalEventAggregator.RaiseEvent(new OrderBookUpdateEvent() {
                        SecCode = secCode,ClassCode = classCode, OrderBook = orderBook
                    });
                }

                break;
            case "callback":
                var funcNameToken = domainEvent.Req.body?["name"];
                if (funcNameToken != null)
                {
                    var funcName = funcNameToken.ToString();
                    var argumentsToken = domainEvent.Req.body?["arguments"];
                    switch (funcName)
                    {
                        case "OnAllTrade":
                            if (argumentsToken is JArray jArray)
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
                                        _ = globalEventAggregator.RaiseEvent(new AllTradeArrivedEvent() { Trade = t});
                                    }
                                }
                            }
                            
                            break;
                        case "OnOrder":
                            if (TryGetTransactionalOrder(argumentsToken, out var order))
                            {
                                _ = globalEventAggregator.RaiseEvent(new OrderArrivedEvent() { Order = order });
                            }
                            break;
                        case "OnTransReply":
                            if (TryGetTransactionalOrder(argumentsToken, out var transReplyOrder))
                            {
                                _ = globalEventAggregator.RaiseEvent(new TransactionReplyArrivedEvent() { Order = transReplyOrder });
                            }
                            break;
                    }
                }
                break;
            default:
                _ = globalEventAggregator.RaiseEvent(new ServiceMessageArrivedEvent() {Response = domainEvent.Req, BridgeMessage = qMessage});
                break;
        }

        return Task.CompletedTask;
    }

    private static bool TryGetTransactionalOrder(JToken? argumentsToken, out Order? order)
    {
        order = null;
        if (argumentsToken is not JArray { Count: > 0 } jArray)
        {
            return false;
        }

        order = jArray[0].ToObject<Order>();
        return order != null && order.trans_id != "0";
    }
}