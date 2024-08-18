namespace QuikBridgeNet.Entities;

public class QMessage
{
    public int Id { get; set; }
    public MessageType MessageType { get; set; }
    public string Method { get; set; } = "";

    public string Ticker { get; set; } = "";
    public string ClassCode { get; set; } = "";
    public string Interval { get; set; } = "";
    public object? DataSource { get; set; }
    public Func<string, string, int>? Callback { get; set; }
}