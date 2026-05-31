namespace QuikBridgeNet.Entities.CommandData;

public class JsonReqData: JsonCommandData
{
    public object? obj { get; set; }
    public string function { get; set; } = string.Empty;
    public string[]? arguments { get; set; }
}