using System.Collections.Concurrent;
using System.Threading.Channels;
using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNet;

public delegate Task InstrumentClassesUpdateHandler(List<string> instrumentClasses, QuikDataType dataType);
public delegate Task InstrumentParameterUpdateHandler(InstrumentParametersUpdateEventArgs args);
public delegate Task OrderBookUpdateHandler(OrderBookUpdateEventArgs args);
public delegate Task ServiceMessageHandler(JsonMessage resp, QMessage? registeredMsg);
public delegate Task DataSourceSetHandler(string dataSourceName, QMessage? dataSourceReq);
public delegate Task NewAllTradeHandler(AllTrade trade);
public delegate Task SecurityInfoHandler(SecurityContract contract);

public class QuikBridgeEventAggregator
{
    // Channels
    private readonly Channel<(List<string> data, QuikDataType dataType)> _instrumentClassesUpdateChannel = Channel.CreateUnbounded<(List<string>, QuikDataType)>();
    private readonly Channel<AllTrade> _allTradesChannel = Channel.CreateUnbounded<AllTrade>();
    private readonly Channel<SecurityContract> _contractsChannel = Channel.CreateUnbounded<SecurityContract>();
    private readonly Channel<InstrumentParametersUpdateEventArgs> _instrumentParameterUpdateChannel = Channel.CreateUnbounded<InstrumentParametersUpdateEventArgs>();
    private readonly Channel<OrderBookUpdateEventArgs> _orderBookUpdateChannel = Channel.CreateUnbounded<OrderBookUpdateEventArgs>();
    private readonly Channel<(JsonMessage resp, QMessage? registeredMsg)> _serviceMessagesChannel = Channel.CreateUnbounded<(JsonMessage, QMessage?)>();
    private readonly Channel<(string dataSourceName, QMessage? registeredMsg)> _dataSourceSetChannel = Channel.CreateUnbounded<(string, QMessage?)>();

    // Subscriber pools
    private readonly ConcurrentBag<InstrumentClassesUpdateHandler> _instrumentClassesUpdateHandlers = new();
    private readonly ConcurrentBag<InstrumentParameterUpdateHandler> _instrumentParameterUpdateHandlers = new();
    private readonly ConcurrentBag<OrderBookUpdateHandler> _orderBookUpdateHandlers = new();
    private readonly ConcurrentBag<ServiceMessageHandler> _serviceMessageHandlers = new();
    private readonly ConcurrentBag<DataSourceSetHandler> _dataSourceSetHandlers = new();
    private readonly ConcurrentBag<NewAllTradeHandler> _allTradeHandlers = new();
    private readonly ConcurrentBag<SecurityInfoHandler> _securityInfoHandlers = new();

    // Raise events
    public async Task RaiseServiceMessageArrivedEvent(JsonMessage resp, QMessage? registeredMsg)
    {
        await _serviceMessagesChannel.Writer.WriteAsync((resp, registeredMsg));
    }
    
    public async Task RaiseDataSourceSetEvent(string dataSourceName, QMessage? dataSourceReq)
    {
        await _dataSourceSetChannel.Writer.WriteAsync((dataSourceName, dataSourceReq));
    }
    
    public async Task RaiseNewAllTradeEvent(AllTrade trade)
    {
        await _allTradesChannel.Writer.WriteAsync(trade);
    }
    
    public async Task RaiseSecurityInfoEvent(SecurityContract contract)
    {
        await _contractsChannel.Writer.WriteAsync(contract);
    }

    public async Task RaiseInstrumentClassesUpdateEvent(List<string> instrumentClasses, QuikDataType dataType)
    {
        await _instrumentClassesUpdateChannel.Writer.WriteAsync((instrumentClasses, dataType));
    }

    public async Task RaiseInstrumentParameterUpdateEvent(string? secCode, string? classCode, string? paramName, string? paramValue)
    {
        var args = new InstrumentParametersUpdateEventArgs
        {
            SecCode = secCode,
            ClassCode = classCode,
            ParamName = paramName,
            ParamValue = paramValue
        };
        await _instrumentParameterUpdateChannel.Writer.WriteAsync(args);
    }

    public async Task RaiseOrderBookUpdateEvent(string? secCode, string? classCode, OrderBook? orderBook)
    {
        var args = new OrderBookUpdateEventArgs
        {
            SecCode = secCode,
            ClassCode = classCode,
            OrderBook = orderBook
        };
        await _orderBookUpdateChannel.Writer.WriteAsync(args);
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
    
    public void SubscribeToDataSourceSet(DataSourceSetHandler handler)
    {
        _dataSourceSetHandlers.Add(handler);
        _ = ProcessDataSourceSet();
    }
    
    public void SubscribeToAllTrades(NewAllTradeHandler handler)
    {
        _allTradeHandlers.Add(handler);
        _ = ProcessNewAllTrade();
    }
    public void SubscribeToSecurityInfo(SecurityInfoHandler handler)
    {
        _securityInfoHandlers.Add(handler);
        _ = ProcessSecurityInfo();
    }

    // Internal processing methods
    private async Task ProcessInstrumentClassesUpdate()
    {
        await foreach ((var instrumentClasses, var dataType) in _instrumentClassesUpdateChannel.Reader.ReadAllAsync())
        {
            var handlers = _instrumentClassesUpdateHandlers.ToArray();
            var tasks = handlers.Select(handler => handler(instrumentClasses, dataType)).ToList();
            await Task.WhenAll(tasks);
        }
    }
    
    private async Task ProcessNewAllTrade()
    {
        await foreach (var trade in _allTradesChannel.Reader.ReadAllAsync())
        {
            var handlers = _allTradeHandlers.ToArray();
            var tasks = handlers.Select(handler => handler(trade)).ToList();
            await Task.WhenAll(tasks);
        }
    }
    
    private async Task ProcessSecurityInfo()
    {
        await foreach (var contract in _contractsChannel.Reader.ReadAllAsync())
        {
            var handlers = _securityInfoHandlers.ToArray();
            var tasks = handlers.Select(handler => handler(contract)).ToList();
            await Task.WhenAll(tasks);
        }
    }

    private async Task ProcessInstrumentParameterUpdate()
    {
        await foreach (var args in _instrumentParameterUpdateChannel.Reader.ReadAllAsync())
        {
            var handlers = _instrumentParameterUpdateHandlers.ToArray();
            var tasks = handlers.Select(handler => handler(args)).ToList();
            await Task.WhenAll(tasks);
        }
    }

    private async Task ProcessOrderBookUpdate()
    {
        await foreach (var args in _orderBookUpdateChannel.Reader.ReadAllAsync())
        {
            var handlers = _orderBookUpdateHandlers.ToArray();
            var tasks = handlers.Select(handler => handler(args)).ToList();
            await Task.WhenAll(tasks);
        }
    }

    private async Task ProcessServiceMessages()
    {
        await foreach (var (resp, registeredMsg) in _serviceMessagesChannel.Reader.ReadAllAsync())
        {
            var handlers = _serviceMessageHandlers.ToArray();
            var tasks = handlers.Select(handler => handler(resp, registeredMsg)).ToList();
            await Task.WhenAll(tasks);
        }
    }
    
    private async Task ProcessDataSourceSet()
    {
        await foreach (var (dataSourceName, dataSourceReq) in _dataSourceSetChannel.Reader.ReadAllAsync())
        {
            var handlers = _dataSourceSetHandlers.ToArray();
            var tasks = handlers.Select(handler => handler(dataSourceName, dataSourceReq)).ToList();
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
        _dataSourceSetChannel.Writer.Complete();
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
