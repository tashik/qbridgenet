using Newtonsoft.Json.Linq;

namespace QuikBridgeNetDomain.Entities;

public class JsonMessage
{
    public int id { get; set; }
    public string type { get; set; } = string.Empty;    
    public JToken? body { get; set; }
}