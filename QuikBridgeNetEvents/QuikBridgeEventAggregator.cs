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
public delegate Task RequestExpiredHandler(RequestExpiredEvent args);
public delegate Task SessionStateRestoreFailedHandler(SessionStateRestoreFailedEvent args);

public sealed record EventProcessingMetrics(
    Type EventType,
    int PendingEvents,
    int PendingHandlerExecutions,
    int ActiveHandlerExecutions,
    int SubscriberCount);

public class QuikBridgeEventAggregator
{
    private sealed class EventSubscription(Action unsubscribe) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            unsubscribe();
        }
    }

    private interface IEventSlot
    {
        int SubscriberCount { get; }
        void Complete(Exception error);
    }

    private sealed class EventSubscriber<TEvent>
    {
        private readonly Func<TEvent, Task> _handler;
        private readonly bool _isExtendedLogging;
        private readonly Action<Type> _onHandlerStarted;
        private readonly Action<Type> _onHandlerCompleted;
        private readonly SemaphoreSlim? _capacityGate;
        private readonly object _syncRoot = new();
        private Task _tail = Task.CompletedTask;

        public EventSubscriber(
            Func<TEvent, Task> handler,
            bool isExtendedLogging,
            int queueCapacity,
            Action<Type> onHandlerStarted,
            Action<Type> onHandlerCompleted)
        {
            _handler = handler;
            _isExtendedLogging = isExtendedLogging;
            _capacityGate = queueCapacity > 0 ? new SemaphoreSlim(queueCapacity, queueCapacity) : null;
            _onHandlerStarted = onHandlerStarted;
            _onHandlerCompleted = onHandlerCompleted;
        }

        public async Task EnqueueAsync(TEvent args)
        {
            if (_capacityGate != null)
            {
                await _capacityGate.WaitAsync();
            }

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
                _capacityGate?.Release();
            }
        }
    }

    private sealed class EventSlot<TEvent> : IEventSlot
    {
        public EventSlot(Channel<TEvent> channel)
        {
            Channel = channel;
        }

        public Channel<TEvent> Channel { get; }
        public ConcurrentDictionary<long, EventSubscriber<TEvent>> Subscribers { get; } = new();
        public int SubscriberCount => Subscribers.Count;

        public long AddSubscriber(EventSubscriber<TEvent> subscriber)
        {
            var subscriberId = Interlocked.Increment(ref _nextSubscriberId);
            Subscribers[subscriberId] = subscriber;
            return subscriberId;
        }

        public void RemoveSubscriber(long subscriberId)
        {
            Subscribers.TryRemove(subscriberId, out _);
        }

        public void Complete(Exception error)
        {
            Channel.Writer.TryComplete(error);
        }

        private long _nextSubscriberId;
    }

    private readonly ConcurrentDictionary<Type, IEventSlot> _eventSlots = new();
    private readonly ConcurrentDictionary<Type, int> _processingFlags = new();
    private readonly ConcurrentDictionary<Type, int> _pendingEvents = new();
    private readonly ConcurrentDictionary<Type, int> _pendingHandlerExecutions = new();
    private readonly ConcurrentDictionary<Type, int> _activeHandlerExecutions = new();
    
    private readonly bool _isExtendedLogging;
    private readonly int _eventQueueCapacity;
    private readonly int _eventHandlerQueueCapacity;
    private readonly int _eventQueueWarningThreshold;
    private readonly int _eventHandlerBacklogWarningThreshold;
    
    public QuikBridgeEventAggregator(QuikBridgeConfig bridgeConfig)
    {
        _isExtendedLogging = bridgeConfig.UseExtendedEventLogging;
        _eventQueueCapacity = bridgeConfig.EventQueueCapacity;
        _eventHandlerQueueCapacity = bridgeConfig.EventHandlerQueueCapacity;
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
        AddEventType<RequestExpiredEvent, RequestExpiredHandler>();
        AddEventType<SessionStateRestoreFailedEvent, SessionStateRestoreFailedHandler>();
    }
    
    private void AddEventType<TEvent, THandler>()
    {
        var channel = _eventQueueCapacity > 0
            ? Channel.CreateBounded<TEvent>(new BoundedChannelOptions(_eventQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            })
            : Channel.CreateUnbounded<TEvent>();

        _eventSlots[typeof(TEvent)] = new EventSlot<TEvent>(channel);
        _processingFlags[typeof(TEvent)] = 0;
        _pendingEvents[typeof(TEvent)] = 0;
        _pendingHandlerExecutions[typeof(TEvent)] = 0;
        _activeHandlerExecutions[typeof(TEvent)] = 0;
    }
    
    public void SubscribeToInstrumentClassesUpdate(Func<InstrumentClassesUpdateEvent, Task> handler)
    {
        SubscribeCore(handler);
    }

    public void SubscribeToInstrumentParameterUpdate(Func<InstrumentParametersUpdateEvent, Task> handler)
    {
         SubscribeCore(handler);
    }

    public void SubscribeToOrderBookUpdate(Func<OrderBookUpdateEvent, Task> handler)
    {
        SubscribeCore(handler);
    }

    public void SubscribeToServiceMessages(Func<ServiceMessageArrivedEvent, Task> handler)
    {
        SubscribeCore(handler);
    }
    
    public void SubscribeToDataSourceSet(Func<DataSourceSetEvent, Task> handler)
    {
        SubscribeCore(handler);
    }
    
    public void SubscribeToAllTrades(Func<AllTradeArrivedEvent, Task> handler)
    {
        SubscribeCore(handler);
    }

    public void SubscribeToOrders(Func<OrderArrivedEvent, Task> handler)
    {
        SubscribeCore(handler);
    }

    public void SubscribeToTransactionReplies(Func<TransactionReplyArrivedEvent, Task> handler)
    {
        SubscribeCore(handler);
    }

    public void SubscribeToSecurityInfo(Func<SecurityContractArrivedEvent, Task> handler)
    {
        SubscribeCore(handler);
    }

    public void SubscribeToAccountPositions(Func<AccountPositionArrivedEvent, Task> handler)
    {
        SubscribeCore(handler);
    }

    public void SubscribeToMoneyPositions(Func<MoneyPositionArrivedEvent, Task> handler)
    {
        SubscribeCore(handler);
    }

    public void SubscribeToFuturesHoldings(Func<FuturesHoldingArrivedEvent, Task> handler)
    {
        SubscribeCore(handler);
    }

    public void SubscribeToFuturesLimits(Func<FuturesLimitArrivedEvent, Task> handler)
    {
        SubscribeCore(handler);
    }

    public void SubscribeToRequestExpired(Func<RequestExpiredEvent, Task> handler)
    {
        SubscribeCore(handler);
    }

    public void SubscribeToSessionStateRestoreFailed(Func<SessionStateRestoreFailedEvent, Task> handler)
    {
        SubscribeCore(handler);
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler)
    {
        return SubscribeCore(handler);
    }
    
    private IDisposable SubscribeCore<TEvent>(Func<TEvent, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var key = typeof(TEvent);
        var subscriber = new EventSubscriber<TEvent>(handler, _isExtendedLogging, _eventHandlerQueueCapacity, OnHandlerStarted, OnHandlerCompleted);

        if (_eventSlots.TryGetValue(key, out var slotObj) && slotObj is EventSlot<TEvent> slot)
        {
            var subscriberId = slot.AddSubscriber(subscriber);
            EnsureProcessingIsRunning<TEvent>();
            return new EventSubscription(() => slot.RemoveSubscriber(subscriberId));
        }

        EnsureProcessingIsRunning<TEvent>();
        return new EventSubscription(() => { });
    }

    public async Task RaiseEvent<TEvent>(TEvent eventArgs)
    {
        if (_eventSlots.TryGetValue(typeof(TEvent), out var slotObj) && slotObj is EventSlot<TEvent> slot)
        {
            var pendingEvents = _pendingEvents.AddOrUpdate(typeof(TEvent), 1, (_, current) => current + 1);
            LogPressureIfNeeded(typeof(TEvent), pendingEvents, _pendingHandlerExecutions.GetValueOrDefault(typeof(TEvent)));
            if (_isExtendedLogging) Console.WriteLine($"[{typeof(TEvent)}] Raising event...");
            await slot.Channel.Writer.WriteAsync(eventArgs);
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
        if (_eventSlots.TryGetValue(typeof(TEvent), out var slotObj) && slotObj is EventSlot<TEvent> slot)
        {
            try
            {
                if (_isExtendedLogging) Console.WriteLine($"[{typeof(TEvent)}] Started event processing...");

                await foreach (var args in slot.Channel.Reader.ReadAllAsync())
                {
                    _pendingEvents.AddOrUpdate(typeof(TEvent), 0, (_, current) => Math.Max(0, current - 1));
                    if (_isExtendedLogging) Console.WriteLine($"[{typeof(TEvent)}] Event received: {args}");

                    var handlers = slot.Subscribers.Values.ToArray();
                    var pendingHandlers = _pendingHandlerExecutions.AddOrUpdate(typeof(TEvent), handlers.Length, (_, current) => current + handlers.Length);
                    LogPressureIfNeeded(typeof(TEvent), _pendingEvents.GetValueOrDefault(typeof(TEvent)), pendingHandlers);
                    foreach (var handler in handlers)
                    {
                        await handler.EnqueueAsync(args);
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
        return _eventSlots.Keys.Select(GetMetricsSnapshot).ToArray();
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
        if (!_eventSlots.TryGetValue(eventType, out var slot))
        {
            return 0;
        }

        return slot.SubscriberCount;
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
        foreach (var eventSlot in _eventSlots.Values)
        {
            eventSlot.Complete(new Exception("Channel closed."));
        }
    }

    
}