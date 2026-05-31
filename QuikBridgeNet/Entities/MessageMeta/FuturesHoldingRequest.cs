namespace QuikBridgeNet.Entities.MessageMeta;

public class FuturesHoldingRequest : MetaData
{
    public string FirmId { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public string SecCode { get; set; } = string.Empty;
    public int PositionType { get; set; }
}