using System.Threading.Channels;
using QuikBridgeNet.Entities;

namespace QuikBridgeNet;

public class QuikBridgeEventAggregator
{
    // Channels
    private readonly Channel<(object sender, List<string> instrumentClasses)> _instrumentClassesUpdateChannel = Channel.CreateUnbounded<(object, List<string>)>();
    private readonly Channel<(object sender, InstrumentParametersUpdateEventArgs args)> _instrumentParameterUpdateChannel = Channel.CreateUnbounded<(object, InstrumentParametersUpdateEventArgs)>();
    private readonly Channel<(object sender, OrderBookUpdateEventArgs args)> _orderBookUpdateChannel = Channel.CreateUnbounded<(object, OrderBookUpdateEventArgs)>();

    private readonly Channel<(object sender, JsonMessage msg)> _serviceMessagesChannel =
        Channel.CreateUnbounded<(object, JsonMessage)>();

    public async Task RaiseServiceMessageArrivedEvent(object sender, JsonMessage msg)
    {
        await _serviceMessagesChannel.Writer.WriteAsync((sender, msg));
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
    
    public async Task SubscribeToInstrumentClassesUpdate(Func<object, List<string>, Task> handler)
    {
        await foreach (var (sender, instrumentClasses) in _instrumentClassesUpdateChannel.Reader.ReadAllAsync())
        {
            await handler(sender, instrumentClasses).ConfigureAwait(false);
        }
    }
    
    public async Task SubscribeToInstrumentParameterUpdate(Func<object, InstrumentParametersUpdateEventArgs, Task> handler)
    {
        await foreach (var (sender, args) in _instrumentParameterUpdateChannel.Reader.ReadAllAsync())
        {
            await handler(sender, args).ConfigureAwait(false);
        }
    }
    
    public async Task SubscribeToOrderBookUpdate(Func<object, OrderBookUpdateEventArgs, Task> handler)
    {
        await foreach (var (sender, args) in _orderBookUpdateChannel.Reader.ReadAllAsync())
        {
            await handler(sender, args).ConfigureAwait(false);
        }
    }
    
    public async Task SubscribeToServiceMessages(Func<object, JsonMessage, Task> handler)
    {
        await foreach (var (sender, args) in _serviceMessagesChannel.Reader.ReadAllAsync())
        {
            await handler(sender, args).ConfigureAwait(false);
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
