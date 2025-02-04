using System.Collections.Concurrent;
using System.Threading.Channels;
using QuikBridgeNetEvents.Events;

namespace QuikBridgeNetEvents;

public delegate Task InstrumentClassesUpdateHandler(InstrumentClassesUpdateEvent args);
public delegate Task InstrumentParameterUpdateHandler(InstrumentParametersUpdateEvent args);
public delegate Task OrderBookUpdateHandler(OrderBookUpdateEvent args);
public delegate Task ServiceMessageHandler(ServiceMessageArrivedEvent args);
public delegate Task DataSourceSetHandler(DataSourceSetEvent args);
public delegate Task AllTradeArrivedHandler(AllTradeArrivedEvent args);
public delegate Task SecurityInfoHandler(SecurityContractArrivedEvent args);

public class QuikBridgeEventAggregator
{
    private readonly ConcurrentDictionary<Type, object> _channels = new();
    private readonly ConcurrentDictionary<Type, object> _subscribers = new();
    private readonly ConcurrentDictionary<Type, int> _processingFlags = new();
    
    public QuikBridgeEventAggregator()
    {
        AddEventType<InstrumentParametersUpdateEvent, InstrumentParameterUpdateHandler>();
        AddEventType<OrderBookUpdateEvent, OrderBookUpdateHandler>();
        AddEventType<ServiceMessageArrivedEvent, ServiceMessageHandler>();
        AddEventType<DataSourceSetEvent, DataSourceSetHandler>();
        AddEventType<AllTradeArrivedEvent, AllTradeArrivedHandler>();
        AddEventType<InstrumentClassesUpdateEvent, InstrumentClassesUpdateHandler>();
        AddEventType<SecurityContractArrivedEvent, SecurityInfoHandler>();
    }
    
    private void AddEventType<TEvent, THandler>()
    {
        _channels[typeof(TEvent)] = Channel.CreateUnbounded<TEvent>();
        _subscribers[typeof(TEvent)] = new ConcurrentBag<THandler>();
        _processingFlags[typeof(TEvent)] = 0;
    }
    
    public void SubscribeToInstrumentClassesUpdate(Func<InstrumentClassesUpdateEvent, Task> handler)
    {
        Subscribe<InstrumentClassesUpdateEvent>(handler);
    }

    public void SubscribeToInstrumentParameterUpdate(Func<InstrumentParametersUpdateEvent, Task> handler)
    {
       Subscribe<InstrumentParametersUpdateEvent>(handler);
    }

    public void SubscribeToOrderBookUpdate(Func<OrderBookUpdateEvent, Task> handler)
    {
        Subscribe<OrderBookUpdateEvent>(handler);
    }

    public void SubscribeToServiceMessages(Func<ServiceMessageArrivedEvent, Task> handler)
    {
        Subscribe<ServiceMessageArrivedEvent>(handler);
    }
    
    public void SubscribeToDataSourceSet(Func<DataSourceSetEvent, Task> handler)
    {
        Subscribe<DataSourceSetEvent>(handler);
    }
    
    public void SubscribeToAllTrades(Func<AllTradeArrivedEvent, Task> handler)
    {
        Subscribe<AllTradeArrivedEvent>(handler);
    }
    public void SubscribeToSecurityInfo(Func<SecurityContractArrivedEvent, Task> handler)
    {
        Subscribe<SecurityContractArrivedEvent>(handler);
    }
    
    private void Subscribe<TEvent>(Func<TEvent, Task> handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        var key = typeof(TEvent);
        _subscribers.TryGetValue(key, out var bagObj);
        
        if (bagObj is ConcurrentBag<Delegate> bag)
        {
            bag.Add(handler);
        }
        else
        {
            bag = new ConcurrentBag<Delegate>();
            _subscribers[key] = bag;
            bag.Add(handler);
        }

        // Ensure processing starts
        EnsureProcessingIsRunning<TEvent>();
    }

    public async Task RaiseEvent<TEvent>(TEvent eventArgs)
    {
        if (_channels.TryGetValue(typeof(TEvent), out var channelObj) && channelObj is Channel<TEvent> channel)
        {
            Console.WriteLine($"[{typeof(TEvent)}] Raising event...");
            await channel.Writer.WriteAsync(eventArgs);
        }
        else
        {
            Console.WriteLine($"[{typeof(TEvent)}] No channel found for event.");
        }
    }
    
    private void EnsureProcessingIsRunning<TEvent>()
    {
        _processingFlags.TryAdd(typeof(TEvent), 0); // Ensure key exists

        int oldValue;
        do
        {
            oldValue = _processingFlags[typeof(TEvent)];
            if (oldValue == 1)  {
                Console.WriteLine($"[{typeof(TEvent)}] Processing already running.");
                return; // Already running
            }
        }
        while (!_processingFlags.TryUpdate(typeof(TEvent), 1, oldValue)); // Atomically set to 1
        
        Console.WriteLine($"[{typeof(TEvent)}] Starting event processing...");
        _ = Task.Run(ProcessEvents<TEvent>);
    }
    
    private async Task ProcessEvents<TEvent>()
    {
        if (_channels.TryGetValue(typeof(TEvent), out var channelObj) && channelObj is Channel<TEvent> channel)
        {
            if (!_subscribers.TryGetValue(typeof(TEvent), out var subscribersObj))
            {
                Console.WriteLine($"[{typeof(TEvent)}] No subscribers found in dictionary.");
                return;
            }

            if (subscribersObj is not ConcurrentBag<Delegate> subscribers)
            {
                Console.WriteLine($"[{typeof(TEvent)}] ERROR: Subscribers object is of type {subscribersObj.GetType().FullName}, expected ConcurrentBag<Delegate>.");
                return;
            }

            try
            {
                Console.WriteLine($"[{typeof(TEvent)}] Started event processing...");

                await foreach (var args in channel.Reader.ReadAllAsync())
                {
                    Console.WriteLine($"[{typeof(TEvent)}] Event received: {args}");

                    var handlers = subscribers.ToArray();
                    var tasks = handlers.Select(handler => ((Func<TEvent, Task>)handler).Invoke(args)).ToList();
                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Event Processing Failed for {typeof(TEvent)}: {ex}");
            }
            finally
            {
                _processingFlags.TryAdd(typeof(TEvent), 1);

                int oldValue;
                do
                {
                    oldValue = _processingFlags[typeof(TEvent)];
                }
                while (!_processingFlags.TryUpdate(typeof(TEvent), 0, oldValue));

                Console.WriteLine($"[{typeof(TEvent)}] Processing finished.");
            }
        }
        else
        {
            Console.WriteLine($"[{typeof(TEvent)}] No channel found.");
        }
    }
}