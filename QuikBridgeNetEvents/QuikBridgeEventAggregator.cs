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
public delegate Task OrderArrivedHandler(OrderArrivedEvent args);
public delegate Task TransactionReplyArrivedHandler(TransactionReplyArrivedEvent args);
public delegate Task SecurityInfoHandler(SecurityContractArrivedEvent args);
public delegate Task AccountPositionArrivedHandler(AccountPositionArrivedEvent args);
public delegate Task MoneyPositionArrivedHandler(MoneyPositionArrivedEvent args);
public delegate Task FuturesHoldingArrivedHandler(FuturesHoldingArrivedEvent args);
public delegate Task FuturesLimitArrivedHandler(FuturesLimitArrivedEvent args);

public sealed record EventProcessingMetrics(
    Type EventType,
    int PendingEvents,
    int PendingHandlerExecutions,
    int ActiveHandlerExecutions,
    int SubscriberCount);

public class QuikBridgeEventAggregator
{
    private sealed class EventSubscriber<TEvent>
    {
        private readonly Func<TEvent, Task> _handler;
        private readonly bool _isExtendedLogging;
        private readonly Action<Type> _onHandlerStarted;
        private readonly Action<Type> _onHandlerCompleted;
        private readonly object _syncRoot = new();
        private Task _tail = Task.CompletedTask;

        public EventSubscriber(
            Func<TEvent, Task> handler,
            bool isExtendedLogging,
            Action<Type> onHandlerStarted,
            Action<Type> onHandlerCompleted)
        {
            _handler = handler;
            _isExtendedLogging = isExtendedLogging;
            _onHandlerStarted = onHandlerStarted;
            _onHandlerCompleted = onHandlerCompleted;
        }

        public void Enqueue(TEvent args)
        {
            lock (_syncRoot)
            {
                _tail = _tail.ContinueWith(
                        _ => ExecuteAsync(args),
                        CancellationToken.None,
                        TaskContinuationOptions.None,
                        TaskScheduler.Default)
                    .Unwrap();
            }
        }

        private async Task ExecuteAsync(TEvent args)
        {
            _onHandlerStarted(typeof(TEvent));
            try
            {
                await _handler(args);
            }
            catch (Exception ex)
            {
                if (_isExtendedLogging) Console.WriteLine($"[Error] Handler for {typeof(TEvent)} failed: {ex}");
            }
            finally
            {
                _onHandlerCompleted(typeof(TEvent));
            }
        }
    }

    private readonly ConcurrentDictionary<Type, object> _channels = new();
    private readonly ConcurrentDictionary<Type, object> _subscribers = new();
    private readonly ConcurrentDictionary<Type, int> _processingFlags = new();
    private readonly ConcurrentDictionary<Type, int> _pendingEvents = new();
    private readonly ConcurrentDictionary<Type, int> _pendingHandlerExecutions = new();
    private readonly ConcurrentDictionary<Type, int> _activeHandlerExecutions = new();
    
    private readonly bool _isExtendedLogging;
    private readonly int _eventQueueWarningThreshold;
    private readonly int _eventHandlerBacklogWarningThreshold;
    
    public QuikBridgeEventAggregator(QuikBridgeConfig bridgeConfig)
    {
        _isExtendedLogging = bridgeConfig.UseExtendedEventLogging;
        _eventQueueWarningThreshold = bridgeConfig.EventQueueWarningThreshold;
        _eventHandlerBacklogWarningThreshold = bridgeConfig.EventHandlerBacklogWarningThreshold;
        AddEventType<InstrumentParametersUpdateEvent, InstrumentParameterUpdateHandler>();
        AddEventType<OrderBookUpdateEvent, OrderBookUpdateHandler>();
        AddEventType<ServiceMessageArrivedEvent, ServiceMessageHandler>();
        AddEventType<DataSourceSetEvent, DataSourceSetHandler>();
        AddEventType<AllTradeArrivedEvent, AllTradeArrivedHandler>();
        AddEventType<OrderArrivedEvent, OrderArrivedHandler>();
        AddEventType<TransactionReplyArrivedEvent, TransactionReplyArrivedHandler>();
        AddEventType<InstrumentClassesUpdateEvent, InstrumentClassesUpdateHandler>();
        AddEventType<SecurityContractArrivedEvent, SecurityInfoHandler>();
        AddEventType<AccountPositionArrivedEvent, AccountPositionArrivedHandler>();
        AddEventType<MoneyPositionArrivedEvent, MoneyPositionArrivedHandler>();
        AddEventType<FuturesHoldingArrivedEvent, FuturesHoldingArrivedHandler>();
        AddEventType<FuturesLimitArrivedEvent, FuturesLimitArrivedHandler>();
    }
    
    private void AddEventType<TEvent, THandler>()
    {
        _channels[typeof(TEvent)] = Channel.CreateUnbounded<TEvent>();
        _subscribers[typeof(TEvent)] = new ConcurrentBag<EventSubscriber<TEvent>>();
        _processingFlags[typeof(TEvent)] = 0;
        _pendingEvents[typeof(TEvent)] = 0;
        _pendingHandlerExecutions[typeof(TEvent)] = 0;
        _activeHandlerExecutions[typeof(TEvent)] = 0;
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

    public void SubscribeToOrders(Func<OrderArrivedEvent, Task> handler)
    {
        Subscribe(handler);
    }

    public void SubscribeToTransactionReplies(Func<TransactionReplyArrivedEvent, Task> handler)
    {
        Subscribe(handler);
    }

    public void SubscribeToSecurityInfo(Func<SecurityContractArrivedEvent, Task> handler)
    {
        Subscribe(handler);
    }

    public void SubscribeToAccountPositions(Func<AccountPositionArrivedEvent, Task> handler)
    {
        Subscribe(handler);
    }

    public void SubscribeToMoneyPositions(Func<MoneyPositionArrivedEvent, Task> handler)
    {
        Subscribe(handler);
    }

    public void SubscribeToFuturesHoldings(Func<FuturesHoldingArrivedEvent, Task> handler)
    {
        Subscribe(handler);
    }

    public void SubscribeToFuturesLimits(Func<FuturesLimitArrivedEvent, Task> handler)
    {
        Subscribe(handler);
    }
    
    private void Subscribe<TEvent>(Func<TEvent, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var key = typeof(TEvent);
        _subscribers.TryGetValue(key, out var bagObj);
        var subscriber = new EventSubscriber<TEvent>(handler, _isExtendedLogging, OnHandlerStarted, OnHandlerCompleted);
        
        if (bagObj is ConcurrentBag<EventSubscriber<TEvent>> bag)
        {
            bag.Add(subscriber);
        }
        else
        {
            bag = new ConcurrentBag<EventSubscriber<TEvent>>();
            _subscribers[key] = bag;
            bag.Add(subscriber);
        }
        
        EnsureProcessingIsRunning<TEvent>();
    }

    public async Task RaiseEvent<TEvent>(TEvent eventArgs)
    {
        if (_channels.TryGetValue(typeof(TEvent), out var channelObj) && channelObj is Channel<TEvent> channel)
        {
            var pendingEvents = _pendingEvents.AddOrUpdate(typeof(TEvent), 1, (_, current) => current + 1);
            LogPressureIfNeeded(typeof(TEvent), pendingEvents, _pendingHandlerExecutions.GetValueOrDefault(typeof(TEvent)));
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

            if (subscribersObj is not ConcurrentBag<EventSubscriber<TEvent>> subscribers)
            {
                if (_isExtendedLogging) Console.WriteLine($"[{typeof(TEvent)}] ERROR: Subscribers object is of type {subscribersObj.GetType().FullName}, expected ConcurrentBag<EventSubscriber<TEvent>>.");
                return;
            }

            try
            {
                if (_isExtendedLogging) Console.WriteLine($"[{typeof(TEvent)}] Started event processing...");

                await foreach (var args in channel.Reader.ReadAllAsync())
                {
                    _pendingEvents.AddOrUpdate(typeof(TEvent), 0, (_, current) => Math.Max(0, current - 1));
                    if (_isExtendedLogging) Console.WriteLine($"[{typeof(TEvent)}] Event received: {args}");

                    var handlers = subscribers.ToArray();
                    var pendingHandlers = _pendingHandlerExecutions.AddOrUpdate(typeof(TEvent), handlers.Length, (_, current) => current + handlers.Length);
                    LogPressureIfNeeded(typeof(TEvent), _pendingEvents.GetValueOrDefault(typeof(TEvent)), pendingHandlers);
                    foreach (var handler in handlers)
                    {
                        handler.Enqueue(args);
                    }
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

    public EventProcessingMetrics GetMetricsSnapshot<TEvent>()
    {
        return GetMetricsSnapshot(typeof(TEvent));
    }

    public IReadOnlyCollection<EventProcessingMetrics> GetAllMetricsSnapshots()
    {
        return _channels.Keys.Select(GetMetricsSnapshot).ToArray();
    }

    private EventProcessingMetrics GetMetricsSnapshot(Type eventType)
    {
        return new EventProcessingMetrics(
            eventType,
            _pendingEvents.GetValueOrDefault(eventType),
            _pendingHandlerExecutions.GetValueOrDefault(eventType),
            _activeHandlerExecutions.GetValueOrDefault(eventType),
            GetSubscriberCount(eventType));
    }

    private int GetSubscriberCount(Type eventType)
    {
        if (!_subscribers.TryGetValue(eventType, out var subscribersObj))
        {
            return 0;
        }

        return subscribersObj switch
        {
            ConcurrentBag<EventSubscriber<InstrumentClassesUpdateEvent>> bag => bag.Count,
            ConcurrentBag<EventSubscriber<InstrumentParametersUpdateEvent>> bag => bag.Count,
            ConcurrentBag<EventSubscriber<OrderBookUpdateEvent>> bag => bag.Count,
            ConcurrentBag<EventSubscriber<ServiceMessageArrivedEvent>> bag => bag.Count,
            ConcurrentBag<EventSubscriber<DataSourceSetEvent>> bag => bag.Count,
            ConcurrentBag<EventSubscriber<AllTradeArrivedEvent>> bag => bag.Count,
            ConcurrentBag<EventSubscriber<OrderArrivedEvent>> bag => bag.Count,
            ConcurrentBag<EventSubscriber<TransactionReplyArrivedEvent>> bag => bag.Count,
            ConcurrentBag<EventSubscriber<SecurityContractArrivedEvent>> bag => bag.Count,
            ConcurrentBag<EventSubscriber<AccountPositionArrivedEvent>> bag => bag.Count,
            ConcurrentBag<EventSubscriber<MoneyPositionArrivedEvent>> bag => bag.Count,
            ConcurrentBag<EventSubscriber<FuturesHoldingArrivedEvent>> bag => bag.Count,
            ConcurrentBag<EventSubscriber<FuturesLimitArrivedEvent>> bag => bag.Count,
            _ => 0
        };
    }

    private void OnHandlerStarted(Type eventType)
    {
        _pendingHandlerExecutions.AddOrUpdate(eventType, 0, (_, current) => Math.Max(0, current - 1));
        _activeHandlerExecutions.AddOrUpdate(eventType, 1, (_, current) => current + 1);
    }

    private void OnHandlerCompleted(Type eventType)
    {
        _activeHandlerExecutions.AddOrUpdate(eventType, 0, (_, current) => Math.Max(0, current - 1));
    }

    private void LogPressureIfNeeded(Type eventType, int pendingEvents, int pendingHandlers)
    {
        var queueOverloaded = _eventQueueWarningThreshold > 0 && pendingEvents >= _eventQueueWarningThreshold;
        var handlersOverloaded = _eventHandlerBacklogWarningThreshold > 0 && pendingHandlers >= _eventHandlerBacklogWarningThreshold;

        if (!queueOverloaded && !handlersOverloaded)
        {
            return;
        }

        Console.WriteLine(
            $"[Warning] Event pressure detected for {eventType.Name}: pendingEvents={pendingEvents}, pendingHandlerExecutions={pendingHandlers}, activeHandlerExecutions={_activeHandlerExecutions.GetValueOrDefault(eventType)}, subscribers={GetSubscriberCount(eventType)}");
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