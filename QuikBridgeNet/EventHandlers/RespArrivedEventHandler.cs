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

    public async Task HandleAsync(RespArrivedEvent domainEvent)
    {
        var msg = domainEvent.Req;
        if (_isExtendedLogging)
            Log.Debug("resp arrived with message id {0}", msg.id);
        
        if (!messageRegistry.TryGetMetadata(msg.id, out var newMessage)) return;
        if (newMessage == null) return;
        if (_isExtendedLogging) Log.Debug("resp method is {0}", newMessage.Method);
        
        try
        {
            switch (newMessage.MessageType)
            {
                case MessageType.Classes:
                    var resultToken = msg.body?["result"] ?? null;
                    var classData = resultToken?.ToObject<List<string>>();
                    await globalEventAggregator.RaiseEvent(new InstrumentClassesUpdateEvent()
                    {
                        InstrumentClasses = ExpandDelimitedValues(classData),
                        InstrumentClassType = QuikDataType.ClassCode
                    });
                    break;
                case MessageType.Securities:
                    var wrapperToken = msg.body?["result"] ?? null;
                    var tickerData = wrapperToken?.ToObject<List<string>>();
                    await globalEventAggregator.RaiseEvent(new InstrumentClassesUpdateEvent()
                    {
                        InstrumentClasses = ExpandDelimitedValues(tickerData),
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
                                await globalEventAggregator.RaiseEvent(new SecurityContractArrivedEvent() { Contract = t});
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
                        
                        await globalEventAggregator.RaiseEvent(new InstrumentParametersUpdateEvent()
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
                            await globalEventAggregator.RaiseEvent(new OrderBookUpdateEvent() {
                                SecCode = newMessage.Ticker,ClassCode = newMessage.ClassCode, OrderBook = orderBook
                            });
                        }
                    }
                    break;
                case MessageType.AccountPosition:
                    var accountPositionToken = msg.body?["result"] ?? null;
                    if (accountPositionToken is JArray { Count: > 0 } accountPositionArray)
                    {
                        var accountPosition = accountPositionArray[0].ToObject<AccountPosition>();
                        if (accountPosition != null)
                        {
                            await globalEventAggregator.RaiseEvent(new AccountPositionArrivedEvent() { Position = accountPosition });
                        }
                    }
                    break;
                case MessageType.MoneyPosition:
                    var moneyPositionToken = msg.body?["result"] ?? null;
                    if (moneyPositionToken is JArray { Count: > 0 } moneyPositionArray)
                    {
                        var moneyPosition = moneyPositionArray[0].ToObject<MoneyPosition>();
                        if (moneyPosition != null)
                        {
                            await globalEventAggregator.RaiseEvent(new MoneyPositionArrivedEvent() { Position = moneyPosition });
                        }
                    }
                    break;
                case MessageType.FuturesHolding:
                    var futuresHoldingToken = msg.body?["result"] ?? null;
                    if (futuresHoldingToken is JArray { Count: > 0 } futuresHoldingArray)
                    {
                        var futuresHolding = futuresHoldingArray[0].ToObject<FuturesHolding>();
                        if (futuresHolding != null)
                        {
                            await globalEventAggregator.RaiseEvent(new FuturesHoldingArrivedEvent() { Holding = futuresHolding });
                        }
                    }
                    break;
                case MessageType.FuturesLimit:
                    var futuresLimitToken = msg.body?["result"] ?? null;
                    if (futuresLimitToken is JArray { Count: > 0 } futuresLimitArray)
                    {
                        var futuresLimit = futuresLimitArray[0].ToObject<FuturesLimit>();
                        if (futuresLimit != null)
                        {
                            await globalEventAggregator.RaiseEvent(new FuturesLimitArrivedEvent() { Limit = futuresLimit });
                        }
                    }
                    break;
                default:
                    await globalEventAggregator.RaiseEvent(new ServiceMessageArrivedEvent() {Response = msg, BridgeMessage = newMessage});
                    break;
            }
        }
        finally
        {
            messageRegistry.RemoveMessage(msg.id);
        }
    }

    private static List<string> ExpandDelimitedValues(IEnumerable<string>? values)
    {
        if (values == null)
        {
            return [];
        }

        var expanded = new List<string>();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            expanded.AddRange(parts);
        }

        return expanded;
    }
}