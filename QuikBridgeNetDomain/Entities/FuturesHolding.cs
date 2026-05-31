using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QuikBridgeNetDomain.Entities;

public class FuturesHolding
{
    public string firmid { get; set; } = string.Empty;
    public string trdaccid { get; set; } = string.Empty;
    public string sec_code { get; set; } = string.Empty;
    public string type { get; set; } = string.Empty;
    public double startbuy { get; set; }
    public double startsell { get; set; }
    public double startnet { get; set; }
    public double todaybuy { get; set; }
    public double todaysell { get; set; }
    public double totalnet { get; set; }
    public double openbuys { get; set; }
    public double opensells { get; set; }
    public double cbplused { get; set; }
    public double cbplplanned { get; set; }
    public double varmargin { get; set; }
    public double avrposnprice { get; set; }
    public double positionvalue { get; set; }
    public double real_varmargin { get; set; }
    public double total_varmargin { get; set; }
    public int session_status { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalFields { get; set; }
}