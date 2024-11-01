using System.ComponentModel;

namespace QuikBridgeNet.Entities;

public enum MessageType
{
    [Description("init_orderbook")]
    OrderBookInit,
    [Description("get_quotes_snapshot")]
    OrderBookSnapshot,
    [Description("subscribe_orderbook")]
    SubscribeOrderbook,
    [Description("unsubscribe_orderbook")]
    UnsubscribeOrderbook,
    [Description("req")]
    Req,
    [Description("classes_list")]
    Classes,
    [Description("getClassSecurities")]
    Securities,
    [Description("getSecurityInfo")]
    SecurityContract,
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
    [Description("send_transaction")]
    SendTransaction,
    [Description("C")]
    Close,
    [Description("H")]
    High,
    [Description("L")]
    Low,
    [Description("O")]
    Open,
    [Description("V")]
    Volume,
    [Description("T")]
    BarTime,
    [Description("OnTrade")]
    OnTrade,
    [Description("OnOrder")]
    OnOrder,
    [Description("OnTransReply")]
    OnTransReply,
    [Description("OnAllTrade")]
    OnAllTrade,
    [Description("getParamEx2")]
    GetParam,
    
}

public enum QuikDataType
{
    [Description("Код класса")]
    ClassCode,
    [Description("Код инструмента")]
    SecCode
}

public enum QuikBridgeConnectionState
{
    [Description("Подключено")]
    Connected,
    [Description("Отключено")]
    Disconnected,
    [Description("Ожидание подключения")]
    Pending,
    [Description("Ошибка")]
    Error
}