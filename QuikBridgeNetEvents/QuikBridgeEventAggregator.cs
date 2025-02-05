using System.Collections.Concurrent;
using System.Threading.Channels;
using QuikBridgeNetDomain;
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
    
    private readonly bool _isExtendedLogging;
    
    public QuikBridgeEventAggregator(QuikBridgeConfig bridgeConfig)
    {
        _isExtendedLogging = bridgeConfig.UseExtendedEventLogging;
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
        Subscribe(handler);
    }

    public void SubscribeToInstrumentParameterUpdate(Func<InstrumentParametersUpdateEvent, Task> handler)
    {
       Subscribe(handler);
    }

    public void SubscribeToOrderBookUpdate(Func<OrderBookUpdateEvent, Task> handler)
    {
        Subscribe(handler);
    }

    public void SubscribeToServiceMessages(Func<ServiceMessageArrivedEvent, Task> handler)
    {
        Subscribe(handler);
    }
    
    public void SubscribeToDataSourceSet(Func<DataSourceSetEvent, Task> handler)
    {
        Subscribe(handler);
    }
    
    public void SubscribeToAllTrades(Func<AllTradeArrivedEvent, Task> handler)
    {
        Subscribe(handler);
    }
    public void SubscribeToSecurityInfo(Func<SecurityContractArrivedEvent, Task> handler)
    {
        Subscribe(handler);
    }
    
    private void Subscribe<TEvent>(Func<TEvent, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

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
        
        EnsureProcessingIsRunning<TEvent>();
    }

    public async Task RaiseEvent<TEvent>(TEvent eventArgs)
    {
        if (_channels.TryGetValue(typeof(TEvent), out var channelObj) && channelObj is Channel<TEvent> channel)
        {
            if (_isExtendedLogging) Console.WriteLine($"[{typeof(TEvent)}] Raising event...");
            await channel.Writer.WriteAsync(eventArgs);
            EnsureProcessingIsRunning<TEvent>();
        }
        else
        {
            if (_isExtendedLogging) Console.WriteLine($"[{typeof(TEvent)}] No channel found for event.");
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
                if (_isExtendedLogging) Console.WriteLine($"[{typeof(TEvent)}] Processing already running.");
                return; // Already running
            }
        }
        while (!_processingFlags.TryUpdate(typeof(TEvent), 1, oldValue)); // set to 1
        
        if (_isExtendedLogging) Console.WriteLine($"[{typeof(TEvent)}] Starting event processing...");
        _ = Task.Run(ProcessEvents<TEvent>);
    }
    
    private async Task ProcessEvents<TEvent>()
    {
        if (_channels.TryGetValue(typeof(TEvent), out var channelObj) && channelObj is Channel<TEvent> channel)
        {
            if (!_subscribers.TryGetValue(typeof(TEvent), out var subscribersObj))
            {
                if (_isExtendedLogging) Console.WriteLine($"[{typeof(TEvent)}] No subscribers found in dictionary.");
                return;
            }

            if (subscribersObj is not ConcurrentBag<Delegate> subscribers)
            {
                if (_isExtendedLogging) Console.WriteLine($"[{typeof(TEvent)}] ERROR: Subscribers object is of type {subscribersObj.GetType().FullName}, expected ConcurrentBag<Delegate>.");
                return;
            }

            try
            {
                if (_isExtendedLogging) Console.WriteLine($"[{typeof(TEvent)}] Started event processing...");

                await foreach (var args in channel.Reader.ReadAllAsync())
                {
                    if (_isExtendedLogging) Console.WriteLine($"[{typeof(TEvent)}] Event received: {args}");

                    var handlers = subscribers.ToArray();
                    var tasks = handlers.Select(async handler =>
                    {
                        try
                        {
                            await ((Func<TEvent, Task>)handler)(args);
                        }
                        catch (Exception ex)
                        {
                            if (_isExtendedLogging) Console.WriteLine($"[Error] Handler for {typeof(TEvent)} failed: {ex}");
                        }
                    }).ToList();
                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception ex)
            {
                if (_isExtendedLogging) Console.WriteLine($"[Error] Event Processing Failed for {typeof(TEvent)}: {ex}");
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

                if (_isExtendedLogging) Console.WriteLine($"[{typeof(TEvent)}] Processing finished.");
            }
        }
        else
        {
            if (_isExtendedLogging) Console.WriteLine($"[{typeof(TEvent)}] No channel found.");
        }
    }
    public void Close()
    {
        foreach (var channelPair in _channels)
        {
            var channelObj = channelPair.Value;
            
            var writerProperty = channelObj.GetType().GetProperty("Writer");
            var writerInstance = writerProperty?.GetValue(channelObj);
            var completeMethod = writerInstance?.GetType().GetMethod("Complete");

            if (completeMethod != null)
            {
                var parameters = completeMethod!.GetParameters();
                if (_isExtendedLogging) Console.WriteLine($"[Debug] Complete method found: {completeMethod}");
                if (_isExtendedLogging) Console.WriteLine($"[Debug] Complete method has {parameters.Length} parameters.");

                foreach (var param in parameters)
                {
                    if (_isExtendedLogging) Console.WriteLine($"[Debug] Parameter: {param.Name}, Type: {param.ParameterType}");
                }
                completeMethod.Invoke(writerInstance, [new Exception("Channel closed.")]);
            }
        }
    }

    
}