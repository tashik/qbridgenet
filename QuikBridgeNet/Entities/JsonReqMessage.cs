using QuikBridgeNet.Entities.CommandData;

namespace QuikBridgeNet.Entities;

public class JsonReqMessage
{
    public int id { get; set; }
    public string type { get; set; }
    public JsonCommandData data { get; set; }
}