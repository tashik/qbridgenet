namespace QuikBridgeNetDomain.Entities;

public class TransactionBase
{
    public string ACCOUNT { get; set; }
    public string CLIENT_CODE { get; set; }
    public string TYPE { get; set; }
    public long TRANS_ID { get; set; }
    public string CLASSCODE { get; set; }
    public string SECCODE { get; set; }
    public string ACTION { get; set; }
    public string OPERATION { get; set; }
}