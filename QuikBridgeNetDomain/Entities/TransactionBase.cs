namespace QuikBridgeNetDomain.Entities;

public class TransactionBase
{
    public string ACCOUNT { get; set; } = string.Empty;
    public string CLIENT_CODE { get; set; } = string.Empty;
    public string TYPE { get; set; } = string.Empty;
    public long TRANS_ID { get; set; }
    public string CLASSCODE { get; set; } = string.Empty;
    public string SECCODE { get; set; } = string.Empty;
    public string ACTION { get; set; } = string.Empty;
    public string OPERATION { get; set; } = string.Empty;
}