namespace QuikOptionDeskApp;

public class BaseAsset
{
    public string AssetClassCode { get; set; } = "";
    public string NameHead { get; set; } = "";
    
    public bool IsSpotBased { get; set; }
    public string AssetSecCode { get; set; } = "";
    public int NumStrikes { get; set; } = 5;
    public decimal StrikeStep { get; set; }
    
    public int Digits { get; set; }
    public List<DateTime> OptionSeries { get; set; } = new();
}