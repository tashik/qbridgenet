namespace QuikBridgeNet.Entities.ProtocolData;

public class JsonVersionRequest
{
    public int id { get; set; }
    public string type { get; set; } = string.Empty;
    public string version { get; set; } = string.Empty;
}