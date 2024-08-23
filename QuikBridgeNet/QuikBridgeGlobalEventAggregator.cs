using QuikBridgeNet.Entities;

namespace QuikBridgeNet;

public class QuikBridgeGlobalEventAggregator
{
    public event EventHandler<List<string>>? InstrumentCLassesUpdateEvent;

    public void RaiseInstrumentCLassesUpdateEvent(object sender, List<string> instrumentClasses)
    {
        InstrumentCLassesUpdateEvent?.Invoke(sender, instrumentClasses);
    } 
    public event EventHandler<InstrumentParametersUpdateEventArgs>? InstrumentParameterUpdateEvent;

    public void RaiseInstrumentParameterUpdateEvent(object sender, string? secCode, string? classCode, string? paramName, string? paramValue)
    {
        InstrumentParameterUpdateEvent?.Invoke(sender, new InstrumentParametersUpdateEventArgs { SecCode = secCode, ClassCode = classCode, ParamName = paramName, ParamValue = paramValue });
    }
    public event EventHandler<OrderbookUpdateEventArgs>? OrderbookUpdateEvent;

    public void RaiseOrderbookUpdateEvent(object sender, string? secCode, string? classCode, OrderBook? orderBook)
    {
        OrderbookUpdateEvent?.Invoke(sender, new OrderbookUpdateEventArgs { SecCode = secCode, ClassCode = classCode, OrderBook = orderBook});
    }
}

public class InstrumentParametersUpdateEventArgs
{
    public string? SecCode { get; set; }
    public string? ClassCode { get; set; }
    public string? ParamName { get; set; }
    public string? ParamValue { get; set; }
}

public class OrderbookUpdateEventArgs
{
    public string? SecCode { get; set; }
    public string? ClassCode { get; set; }
    public OrderBook? OrderBook { get; set; }
}