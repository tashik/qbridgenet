namespace QuikBridgeNetDomain.Entities;

public class SecurityContract
{
    public int accruedint { get; set; }
    public string base_active_classcode { get; set; } = string.Empty;
    public string base_active_seccode { get; set; } = string.Empty;
    public string bsid { get; set; } = string.Empty;
    public int buybackdate { get; set; }
    public double buybackprice { get; set; }
    public string cfi_code { get; set; } = string.Empty;
    public string class_code { get; set; } = string.Empty;
    public string class_name { get; set; } = string.Empty;
    public string code { get; set; } = string.Empty;
    public string couponperiod { get; set; } = string.Empty;
    public double couponvalue { get; set; }
    public string cusip_code { get; set; } = string.Empty;
    public int exp_date { get; set; }
    public string face_unit { get; set; } = string.Empty;
    public double face_value { get; set; }
    public int first_curr_qty_scale { get; set; }
    public string first_currcode { get; set; } = string.Empty;
    public string isin_code { get; set; } = string.Empty;
    public int list_level { get; set; }
    public int lot_size { get; set; }
    public int mat_date { get; set; }
    public double min_price_step { get; set; }
    public string name { get; set; } = string.Empty;
    public double nextcoupon { get; set; }
    public double option_strike { get; set; }
    public int qty_multiplier { get; set; }
    public int qty_scale { get; set; }
    public string regnumber { get; set; } = string.Empty;
    public string ric_cod { get; set; } = string.Empty;
    public int scale { get; set; }
    public string sec_code { get; set; } = string.Empty;
    public int second_curr_qty_scale { get; set; }
    public string second_currcode { get; set; } = string.Empty;
    public string sedol_code { get; set; } = string.Empty;
    public int settle_date { get; set; }
    public string settlecode { get; set; } = string.Empty;
    public string short_name { get; set; } = string.Empty;
    public string step_price_currency { get; set; } = string.Empty;
    public string stock_code { get; set; } = string.Empty;
    public string stock_name { get; set; } = string.Empty;
    public string trade_currency { get; set; } = string.Empty;
    public double yieldatprevwaprice { get; set; }
}
