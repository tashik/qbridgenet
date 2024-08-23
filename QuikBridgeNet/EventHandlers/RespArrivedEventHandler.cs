using System.Globalization;
using Newtonsoft.Json;
using QuikBridgeNet.Entities;
using QuikBridgeNet.Events;
using Serilog;

namespace QuikBridgeNet.EventHandlers;

public class RespArrivedEventHandler : IDomainEventHandler<RespArrivedEvent>
{
    private readonly MessageRegistry _messageRegistry;
    
    public RespArrivedEventHandler(MessageRegistry messageRegistry)
    {
        _messageRegistry = messageRegistry;
    }
    public Task HandleAsync(RespArrivedEvent domainEvent)
    {
        var msg = domainEvent.Req;
        Log.Debug("resp arrived with message id {0}", msg.id);
        
        if (!_messageRegistry.TryGetMetadata(msg.id, out var newMessage)) return Task.CompletedTask;
        if (newMessage == null) return Task.CompletedTask;
        Console.WriteLine("resp arrived with message id {0} to function {1} for ticker {2}", newMessage.Id, newMessage.Method, newMessage.Ticker);

        switch (newMessage.Method)
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
        }
        
        return Task.CompletedTask;
    }
}