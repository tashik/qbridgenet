using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNetEvents.Events;

public class DataSourceSetEvent
{
    public string DataSourceName { get; set; } = "";
    public QMessage? BridgeMessage { get; set; }
}