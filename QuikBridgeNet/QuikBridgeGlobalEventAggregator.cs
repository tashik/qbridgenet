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
}

public class InstrumentParametersUpdateEventArgs
{
    public string? SecCode { get; set; }
    public string? ClassCode { get; set; }
    public string? ParamName { get; set; }
    public string? ParamValue { get; set; }
}