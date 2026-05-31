using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QuikBridgeNetDomain.Entities;

public class AccountPosition
{
    public string firmid { get; set; } = string.Empty;
    public string client_code { get; set; } = string.Empty;
    public string sec_code { get; set; } = string.Empty;
    public string trdaccid { get; set; } = string.Empty;
    public string account { get; set; } = string.Empty;
    public int limit_kind { get; set; }
    public double currentbal { get; set; }
    public double openbal { get; set; }
    public double currentlimit { get; set; }
    public double openlimit { get; set; }
    public double locked_buy { get; set; }
    public double locked_sell { get; set; }
    public double wa_position_price { get; set; }
    public double current_price { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalFields { get; set; }
}