namespace QuikBridgeNetApp;

public sealed class LiveOptionBoardLoadTestSettings
{
    public bool Enabled { get; set; }
    public string OptionClassCode { get; set; } = string.Empty;
    public string BaseAssetClassCode { get; set; } = string.Empty;
    public string BaseAssetSecCode { get; set; } = string.Empty;
    public int ExpirationDate { get; set; }
    public string[] ParamNames { get; set; } = ["LAST"];
    public int StartupDelayMs { get; set; } = 2000;
    public int DiscoveryTimeoutSeconds { get; set; } = 60;
    public int StatusIntervalSeconds { get; set; } = 5;
    public int SubscriptionParallelism { get; set; } = 32;
    public int UnsubscribeParallelism { get; set; } = 32;
    public int MaxInstruments { get; set; }
}