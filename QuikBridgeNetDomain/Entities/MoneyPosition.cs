using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QuikBridgeNetDomain.Entities;

public class MoneyPosition
{
    public string firmid { get; set; } = string.Empty;
    public string client_code { get; set; } = string.Empty;
    public string tag { get; set; } = string.Empty;
    public string currcode { get; set; } = string.Empty;
    public int limit_kind { get; set; }
    public double currentbal { get; set; }
    public double openbal { get; set; }
    public double currentlimit { get; set; }
    public double openlimit { get; set; }
    public double locked { get; set; }
    public double locked_value_coef { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalFields { get; set; }
}