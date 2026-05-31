using QuikBridgeNet.Entities.CommandData;

namespace QuikBridgeNet.Entities.ProtocolData;

public class JsonReqMessage
{
    public int id { get; set; }
    public string type { get; set; } = string.Empty;
    public JsonCommandData data { get; set; } = null!;
}