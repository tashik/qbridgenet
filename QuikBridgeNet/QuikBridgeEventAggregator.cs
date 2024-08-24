using System.Collections.Concurrent;
using System.Threading.Channels;
using QuikBridgeNet.Entities;

namespace QuikBridgeNet;

public delegate Task InstrumentClassesUpdateHandler(object sender, List<string> instrumentClasses);
public delegate Task InstrumentParameterUpdateHandler(object sender, InstrumentParametersUpdateEventArgs args);
public delegate Task OrderBookUpdateHandler(object sender, OrderBookUpdateEventArgs args);
public delegate Task ServiceMessageHandler(object sender, JsonMessage resp, QMessage? registeredMsg);

public class QuikBridgeEventAggregator
{
    // Channels
    private readonly Channel<(object sender, List<string> instrumentClasses)> _instrumentClassesUpdateChannel = Channel.CreateUnbounded<(object, List<string>)>();
    private readonly Channel<(object sender, InstrumentParametersUpdateEventArgs args)> _instrumentParameterUpdateChannel = Channel.CreateUnbounded<(object, InstrumentParametersUpdateEventArgs)>();
    private readonly Channel<(object sender, OrderBookUpdateEventArgs args)> _orderBookUpdateChannel = Channel.CreateUnbounded<(object, OrderBookUpdateEventArgs)>();
    private readonly Channel<(object sender, JsonMessage resp, QMessage? registeredMsg)> _serviceMessagesChannel = Channel.CreateUnbounded<(object, JsonMessage, QMessage?)>();

    // Subscriber pools
    private readonly ConcurrentBag<InstrumentClassesUpdateHandler> _instrumentClassesUpdateHandlers = new();
    private readonly ConcurrentBag<InstrumentParameterUpdateHandler> _instrumentParameterUpdateHandlers = new();
    private readonly ConcurrentBag<OrderBookUpdateHandler> _orderBookUpdateHandlers = new();
    private readonly ConcurrentBag<ServiceMessageHandler> _serviceMessageHandlers = new();

    public async Task RaiseServiceMessageArrivedEvent(object sender, JsonMessage resp, QMessage registeredMsg)
    {
        await _serviceMessagesChannel.Writer.WriteAsync((sender, resp, registeredMsg));
    }

    public async Task RaiseInstrumentClassesUpdateEvent(object sender, List<string> instrumentClasses)
    {
        await _instrumentClassesUpdateChannel.Writer.WriteAsync((sender, instrumentClasses));
    }

    public async Task RaiseInstrumentParameterUpdateEvent(object sender, string? secCode, string? classCode, string? paramName, string? paramValue)
    {
        var args = new InstrumentParametersUpdateEventArgs
        {
            SecCode = secCode,
            ClassCode = classCode,
            ParamName = paramName,
            ParamValue = paramValue
        };
        await _instrumentParameterUpdateChannel.Writer.WriteAsync((sender, args));
    }

    public async Task RaiseOrderBookUpdateEvent(object sender, string? secCode, string? classCode, OrderBook? orderBook)
    {
        var args = new OrderBookUpdateEventArgs
        {
            SecCode = secCode,
            ClassCode = classCode,
            OrderBook = orderBook
        };
        await _orderBookUpdateChannel.Writer.WriteAsync((sender, args));
    }

    // Subscribe to events
    public void SubscribeToInstrumentClassesUpdate(InstrumentClassesUpdateHandler handler)
    {
        _instrumentClassesUpdateHandlers.Add(handler);
        _ = ProcessInstrumentClassesUpdate();
    }

    public void SubscribeToInstrumentParameterUpdate(InstrumentParameterUpdateHandler handler)
    {
        _instrumentParameterUpdateHandlers.Add(handler);
        _ = ProcessInstrumentParameterUpdate();
    }

    public void SubscribeToOrderBookUpdate(OrderBookUpdateHandler handler)
    {
        _orderBookUpdateHandlers.Add(handler);
        _ = ProcessOrderBookUpdate();
    }

    public void SubscribeToServiceMessages(ServiceMessageHandler handler)
    {
        _serviceMessageHandlers.Add(handler);
        _ = ProcessServiceMessages();
    }

    // Internal processing
    private async Task ProcessInstrumentClassesUpdate()
    {
        await foreach (var (sender, instrumentClasses) in _instrumentClassesUpdateChannel.Reader.ReadAllAsync())
        {
            var handlers = _instrumentClassesUpdateHandlers.ToArray();
            var tasks = handlers.Select(handler => handler(sender, instrumentClasses)).ToList();
            await Task.WhenAll(tasks);
        }
    }

    private async Task ProcessInstrumentParameterUpdate()
    {
        await foreach (var (sender, args) in _instrumentParameterUpdateChannel.Reader.ReadAllAsync())
        {
            var handlers = _instrumentParameterUpdateHandlers.ToArray();
            var tasks = handlers.Select(handler => handler(sender, args)).ToList();
            await Task.WhenAll(tasks);
        }
    }

    private async Task ProcessOrderBookUpdate()
    {
        await foreach (var (sender, args) in _orderBookUpdateChannel.Reader.ReadAllAsync())
        {
            var handlers = _orderBookUpdateHandlers.ToArray();
            var tasks = handlers.Select(handler => handler(sender, args)).ToList();
            await Task.WhenAll(tasks);
        }
    }

    private async Task ProcessServiceMessages()
    {
        await foreach (var (sender, resp, registeredMsg) in _serviceMessagesChannel.Reader.ReadAllAsync())
        {
            var handlers = _serviceMessageHandlers.ToArray();
            var tasks = handlers.Select(handler => handler(sender, resp, registeredMsg)).ToList();
            await Task.WhenAll(tasks);
        }
    }

    // Close all channels gracefully
    public void Close()
    {
        _instrumentClassesUpdateChannel.Writer.Complete();
        _instrumentParameterUpdateChannel.Writer.Complete();
        _orderBookUpdateChannel.Writer.Complete();
        _serviceMessagesChannel.Writer.Complete();
    }
}

public class InstrumentParametersUpdateEventArgs
{
    public string? SecCode { get; set; }
    public string? ClassCode { get; set; }
    public string? ParamName { get; set; }
    public string? ParamValue { get; set; }
}

public class OrderBookUpdateEventArgs
{
    public string? SecCode { get; set; }
    public string? ClassCode { get; set; }
    public OrderBook? OrderBook { get; set; }
}
