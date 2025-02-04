using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNetEvents.Events;

public class InstrumentClassesUpdateEvent
{
    public List<string> InstrumentClasses { get; set; } = [];
    
    public QuikDataType InstrumentClassType { get; set; }
}