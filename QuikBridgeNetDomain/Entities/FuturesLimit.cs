using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QuikBridgeNetDomain.Entities;

public class FuturesLimit
{
    public string firmid { get; set; } = string.Empty;
    public string trdaccid { get; set; } = string.Empty;
    public int limit_type { get; set; }
    public double liquidity_coef { get; set; }
    public double cbp_prev_limit { get; set; }
    public double cbplimit { get; set; }
    public double cbplused { get; set; }
    public double cbplplanned { get; set; }
    public double varmargin { get; set; }
    public double accruedint { get; set; }
    public double cbplused_for_orders { get; set; }
    public double cbplused_for_positions { get; set; }
    public double options_premium { get; set; }
    public double ts_comission { get; set; }
    public double kgo { get; set; }
    public string currcode { get; set; } = string.Empty;
    public double real_varmargin { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalFields { get; set; }
}