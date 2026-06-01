namespace QuikBridgeNetEvents.Events;

public class SessionStateRestoreFailedEvent
{
    public string Operation { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}