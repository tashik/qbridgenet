using System.Globalization;
using Newtonsoft.Json;
using QuikBridgeNet.Entities;
using QuikBridgeNet.Events;
using QuikBridgeNet.Helpers;
using Serilog;

namespace QuikBridgeNet.EventHandlers;

public class RespArrivedEventHandler : IDomainEventHandler<RespArrivedEvent>
{
    private readonly MessageRegistry _messageRegistry;
    private readonly QuikBridgeGlobalEventAggregator _eventAggregator;
    
    public RespArrivedEventHandler(MessageRegistry messageRegistry, QuikBridgeGlobalEventAggregator globalEventAggregator)
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
                var rslt = msg.body["result"] ?? null;
                var classData = rslt?.ToObject<List<string>>();
                if (classData != null)
                {
                    foreach (var cl in from c in classData where c.Contains(',') select c.Split(',').ToList())
                    {
                        classes.AddRange(cl);
                    }
                }
                _eventAggregator.RaiseInstrumentCLassesUpdateEvent(this, classes);
                break;
            default:
                Log.Information("No need to fire global event for message of type " + newMessage.MessageType.GetDescription());
                break;
        }
        
        /*switch (newMessage.Method)
        {
            case "getQuoteLevel2":
            {
                try
                {
                    msg.body.TryGetProperty("result", out var result);
                    foreach (var res in result.EnumerateArray())
                    {
                        string jsonStr = res.GetRawText();
                        var snapshot = JsonConvert.DeserializeObject<OrderBook>(jsonStr);

                        if (snapshot != null && snapshot.bid_count != "" && snapshot.offer_count != "")
                        {
                            double.TryParse(snapshot.bid_count, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var bidsCount);
                            double.TryParse(snapshot.offer_count, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var offersCount);
                            if (bidsCount > 0 && offersCount > 0)
                            {
                                //OrderBookUpdate?.Invoke(newMessage.Ticker, newMessage.MessageType, snapshot);
                            }
                        }
                        
                    }
                    
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error on Json Convert {0}", e.Message);
                }

                break;
            }
            default:
                return Task.CompletedTask;
        }*/
        
        return Task.CompletedTask;
    }
}