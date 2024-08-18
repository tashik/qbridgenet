namespace QuikBridgeNet.Entities.MessageMeta;

public class Subscription : MetaData
{
    public string Ticker { get; set; } = "";
    public string ClassCode { get; set; } = "";
}