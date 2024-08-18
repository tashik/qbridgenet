using System.ComponentModel;

namespace QuikBridgeNet.Entities;

public enum MessageType
{
    [Description("getOrderBook")]
    OrderBook,
    [Description("subscribe_orderbook")]
    SubscribeOrderbook,
    [Description("unsubscribe_orderbook")]
    UnsubscribeOrderbook,
    [Description("req")]
    Req,
    [Description("classes_list")]
    Classes,
    [Description("create_datasource")]
    Datasource,
    [Description("datasource_callback")]
    DatasourceCallback,
    [Description("close_datasource")]
    DatasourceClose,
    [Description("subscribe_quotes_table")]
    SubscribeParam,
    [Description("unsubscribe_quotes_table")]
    UnsubscribeParam,
}