namespace QuikBridgeNet;

public class QuikBridgeGlobalEventAggregator
{
    // Use a generic event handler
    public event EventHandler<List<string>>? InstrumentCLassesUpdateEvent;

    public void RaiseInstrumentCLassesUpdateEvent(object sender, List<string> instrumentClasses)
    {
        InstrumentCLassesUpdateEvent?.Invoke(sender, instrumentClasses);
    }
}