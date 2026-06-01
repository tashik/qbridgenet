namespace QuikBridgeNetDomain.Entities;

public class QMessage
{
    public DateTimeOffset RegisteredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public int Id { get; set; }
    public MessageType MessageType { get; set; }
    public string Method { get; set; } = "";

    public string Ticker { get; set; } = "";
    public string ClassCode { get; set; } = "";
    public string Interval { get; set; } = "";
    public string DataSource { get; set; } = "";
    public string ParamName { get; set; } = "";
    public string FirmId { get; set; } = "";
    public string ClientCode { get; set; } = "";
    public string Account { get; set; } = "";
    public string CurrencyCode { get; set; } = "";
    public string Tag { get; set; } = "";
    public int LimitKind { get; set; }
    public int PositionType { get; set; }
}