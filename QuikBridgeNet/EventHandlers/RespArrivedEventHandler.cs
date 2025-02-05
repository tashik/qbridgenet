using Newtonsoft.Json.Linq;
using QuikBridgeNet.Events;
using QuikBridgeNetDomain;
using QuikBridgeNetDomain.Entities;
using QuikBridgeNetEvents;
using QuikBridgeNetEvents.Events;
using Serilog;

namespace QuikBridgeNet.EventHandlers;

public class RespArrivedEventHandler(
    MessageRegistry messageRegistry,
    QuikBridgeEventAggregator globalEventAggregator,
    QuikBridgeConfig bridgeConfig)
    : IDomainEventHandler<RespArrivedEvent>
{
    private readonly bool _isExtendedLogging = bridgeConfig.UseExtendedLogging;

    public Task HandleAsync(RespArrivedEvent domainEvent)
    {
        var msg = domainEvent.Req;
        if (_isExtendedLogging)
            Log.Debug("resp arrived with message id {0}", msg.id);
        
        if (!messageRegistry.TryGetMetadata(msg.id, out var newMessage)) return Task.CompletedTask;
        if (newMessage == null) return Task.CompletedTask;
        if (_isExtendedLogging) Log.Debug("resp method is {0}", newMessage.Method);
        
        
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
                _ = globalEventAggregator.RaiseEvent(new InstrumentClassesUpdateEvent() {InstrumentClasses = classes, InstrumentClassType = QuikDataType.ClassCode});
                break;
            case MessageType.Securities:
                List<string> tickers = new();
                var wrapperToken = msg.body?["result"] ?? null;
                var tickerData = wrapperToken?.ToObject<List<string>>();
                if (tickerData != null)
                {
                    foreach (var cl in from c in tickerData where c.Contains(',') select c.Split(',').ToList())
                    {
                        tickers.AddRange(cl);
                    }
                }
                _ = globalEventAggregator.RaiseEvent(new InstrumentClassesUpdateEvent()
                {
                    InstrumentClasses = tickers,
                    InstrumentClassType = QuikDataType.SecCode
                });
                break;
            case MessageType.SecurityContract:
                var contractResultToken = domainEvent.Req.body?["result"] ?? null;
                if (contractResultToken is JArray contractJArray)
                {
                    var contracts = new List<SecurityContract>();
                    foreach (var r in contractJArray)
                    {
                        var oneContract = r.ToObject<SecurityContract>();
                        if (oneContract != null)
                        {
                            contracts.Add(oneContract);
                        }
                    }
                    if (contracts.Count > 0)
                    {
                        foreach (var t in contracts)
                        {
                            _ = globalEventAggregator.RaiseEvent(new SecurityContractArrivedEvent() { Contract = t});
                        }
                    }
                }
                break;
            case MessageType.Close:
            case MessageType.High:
            case MessageType.Low:
            case MessageType.Open:
            case MessageType.Volume:
                
                break;
            case MessageType.GetParam:
                var wrapper = msg.body?["result"] ?? null;
                if (wrapper is JArray jArray)
                {
                    var valueToken = jArray[0]["param_value"];
                    var value = valueToken?.ToString();
                    
                    _ = globalEventAggregator.RaiseEvent(new InstrumentParametersUpdateEvent()
                    {
                        SecCode = newMessage.Ticker, ClassCode = newMessage.ClassCode, ParamName = newMessage.ParamName, ParamValue = value
                    });
                } 
                
                break;
            case MessageType.OrderBookSnapshot:
                var jOrderBook = msg.body?["result"] ?? null;
                if (jOrderBook is JArray { Count: > 0 } orderBookJArray)
                {
                    var orderBookToken = orderBookJArray[0];
                    var orderBook = orderBookToken.ToObject<OrderBook>();
                    if (orderBook != null)
                    {
                        _ = globalEventAggregator.RaiseEvent(new OrderBookUpdateEvent() {
                            SecCode = newMessage.Ticker,ClassCode = newMessage.ClassCode, OrderBook = orderBook
                        });
                    }
                }
                break;
            default:
                _ = globalEventAggregator.RaiseEvent(new ServiceMessageArrivedEvent() {Response = msg, BridgeMessage = newMessage});
                break;
        }
        
        return Task.CompletedTask;
    }
}