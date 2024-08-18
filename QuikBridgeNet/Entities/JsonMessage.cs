using System.Text.Json;

namespace QuikBridgeNet.Entities;

public class JsonMessage
{
    public int id { get; set; }
    public string type { get; set; }
    public JsonElement body { get; set; }
}