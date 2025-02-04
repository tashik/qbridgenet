namespace QuikBridgeNetEvents.Events;

public class InstrumentParametersUpdateEvent
{
    public string? SecCode { get; set; }
    public string? ClassCode { get; set; }
    public string? ParamName { get; set; }
    public string? ParamValue { get; set; }
}