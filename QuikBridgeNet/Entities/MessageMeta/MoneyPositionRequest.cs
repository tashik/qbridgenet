namespace QuikBridgeNet.Entities.MessageMeta;

public class MoneyPositionRequest : MetaData
{
    public string FirmId { get; set; } = string.Empty;
    public string ClientCode { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public int LimitKind { get; set; }
}