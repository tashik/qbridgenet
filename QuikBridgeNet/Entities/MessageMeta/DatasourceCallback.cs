namespace QuikBridgeNet.Entities.MessageMeta;

public class DatasourceCallback : MetaData
{
    public object? DataSource { get; set; }

    public Func<string, string, int>? Callback { get; set; }
}