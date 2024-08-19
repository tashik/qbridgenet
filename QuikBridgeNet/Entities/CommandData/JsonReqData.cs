namespace QuikBridgeNet.Entities.CommandData;

public class JsonReqData: JsonCommandData
{
    public object? obj { get; set; }
    public string function { get; set; }
    public string[]? arguments { get; set; }
}