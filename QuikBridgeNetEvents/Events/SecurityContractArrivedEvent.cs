using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNetEvents.Events;

public class SecurityContractArrivedEvent
{
    public SecurityContract? Contract { get; set; }
}