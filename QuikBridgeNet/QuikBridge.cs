using System.Globalization;
using System.Net.Sockets;
using Newtonsoft.Json;
using QuikBridgeNet.Entities;
using QuikBridgeNet.Entities.MessageMeta;
using QuikBridgeNet.Helpers;

namespace QuikBridgeNet;

public class QuikBridge
{
    #region Fields
    
    private readonly JsonProtocolHandler _pHandler;

    private readonly MessageIndexer _msgIndexer;

    private List<Subscription> Subscriptions { get; set; }

    private readonly Dictionary<int, QMessage> _messageRegistry;
    
    #endregion
    
    #region Constructors

    public QuikBridge(string host, int port)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _pHandler = new JsonProtocolHandler(socket);
        _pHandler.Connect(host, port);

        if (!IsConnected())
        {
            _pHandler.Finish();
        }

        _pHandler.RespArrived += OnResp;
        _pHandler.ReqArrived += OnReq;
        _pHandler.ConnectionClose += OnDisconnect;

        Subscriptions = new List<Subscription>();
        _messageRegistry = new Dictionary<int, QMessage>();
        
        _msgIndexer = new MessageIndexer();
        
        //_orderBookTimer = new Timer(Timer_Tick, new AutoResetEvent(false), 0, 2000);
    }
    
    #endregion
    
    #region Methods

    public bool IsConnected()
    {
        return _pHandler.Connected;
    }

    private int SendRequest(JsonCommandData data, MetaData metaData)
    {
        var msgId = _msgIndexer.GetIndex();
        var method = data.method;

        var reqData = data as JsonReqData;
        if (reqData != null && reqData.function != "")
        {
            method = reqData.function;
        }

        RegisterRequest(msgId, method, metaData);
        var msg = new JsonReqMessage()
        {
            id = msgId,
            type = MessageType.Req.GetDescription(),
            data = reqData ?? data
        };
        _pHandler.SendReq(msg);
        return msgId;
    }


    public int GetClassesList()
    {
        var data = new JsonReqData()
        {
            method = "invoke",
            function = "getClassesList"
        };
        return SendRequest(data, new MetaData() { MessageType = MessageType.Classes });
    }

    public int CreateDs(string classCode, string secCode, string interval)
    {
        string[] args = {classCode, secCode, interval};
        var data = new JsonReqData()
        {
            method = "invoke",
            function = "getClassesList",
            arguments = args
        };
        var metaData = new DataSource()
        {
            MessageType = MessageType.Datasource,
            Ticker = secCode,
            ClassCode = classCode,
            Interval = interval
        };
        return SendRequest(data, metaData);
    }

    public int SetDsUpdateCallback(object datasource, Func<string, string, int>? callback = null)
    {
        string jsonArguments = "{\"type\": \"callable\", \"function\": \"on_update\"}";
        string[] args = {jsonArguments};
        var data = new JsonReqData()
        {
            method = "invoke",
            obj = datasource,
            function = "SetUpdateCallback",
            arguments = args
        };
        var metaData = new DatasourceCallback()
        {
            MessageType = MessageType.DatasourceCallback,
            DataSource = datasource,
            Callback = callback
        };
        return SendRequest(data, metaData);
    }

    public int GetBar(object datasource, MessageType barFunc, int barIndex)
    {
        string[] args = {Convert.ToString(barIndex)};
        var data = new JsonReqData()
        {
            method = "invoke",
            obj = datasource,
            function = barFunc.GetDescription(),
            arguments = args
        };
        var metaData = new DatasourceCallback()
        {
            MessageType = barFunc,
            DataSource = datasource
        };
        return SendRequest(data, metaData);
    }

    public int CloseDs(object? datasource)
    {
        if (datasource == null) return 0;
        var data = new JsonReqData()
        {
            method = "invoke",
            obj = datasource,
            function = "Close",
        };
        var metaData = new DatasourceCallback()
        {
            MessageType = MessageType.DatasourceClose,
            DataSource = datasource
        };
        return SendRequest(data, metaData);
    }

    public int SubscribeToOrderBook(string classCode, string secCode)
    {
        var data = new JsonCommandDataSubscribeQuotes()
        {
            method = "subscribeQuotes",
            cl = classCode,
            security = secCode
        };
        var metaData = new Subscription()
        {
            MessageType = MessageType.SubscribeOrderbook,
            ClassCode = classCode,
            Ticker = secCode
        };
        return SendRequest(data, metaData);
    }

    public int UnsubscribeToOrderBook(string classCode, string secCode)
    {
        var data = new JsonCommandDataSubscribeQuotes()
        {
            method = "unsubscribeQuotes",
            cl = classCode,
            security = secCode
        };
        var metaData = new Subscription()
        {
            MessageType = MessageType.UnsubscribeOrderbook,
            ClassCode = classCode,
            Ticker = secCode
        };
        return SendRequest(data, metaData);
    }

    public int SubscribeToQuotesTableParams(string classCode, string secCode, string paramName)
    {
        var data = new JsonCommandDataSubscribeParam()
        {
            method = "subscribeParamChanges",
            cl = classCode,
            security = secCode,
            param = paramName
        };
        var metaData = new Subscription()
        {
            MessageType = MessageType.SubscribeParam,
            ClassCode = classCode,
            Ticker = secCode
        };
        return SendRequest(data, metaData);
    }

    public int UnsubscribeToQuotesTableParams(string classCode, string secCode, string paramName)
    {
        var data = new JsonCommandDataSubscribeParam()
        {
            method = "unsubscribeParamChanges",
            cl = classCode,
            security = secCode,
            param = paramName
        };
        var metaData = new Subscription()
        {
            MessageType = MessageType.UnsubscribeParam,
            ClassCode = classCode,
            Ticker = secCode
        };
        return SendRequest(data, metaData);
    }
    

    private Subscription? IsSubscribed(MessageType msgType, string ticker)
    {
        return Subscriptions.FindLast(item => item.Ticker == ticker && item.MessageType == msgType);
    }

    public void SubscribeOrderBook(string ticker, string classCode)
    {
        if (IsSubscribed(MessageType.OrderBook, ticker) != null)
        {
            return;
        }

        int msgId = _msgIndexer.GetIndex();
        
        string[] args = {classCode, ticker};
        
        var req = new JsonReqMessage()
        {
            id = msgId,
            type = "req",
            data = new JsonReqData()
            {
                method = "invoke",
                function = "Subscribe_Level_II_Quotes",
                arguments = args
            }
        };
        
        var newSubscription = new Subscription()
        {
            MessageType = MessageType.OrderBook,
            Ticker = ticker,
            ClassCode = classCode
        };
        
        Subscriptions.Add(newSubscription);
        RegisterRequest(req.id, "Subscribe_Level_II_Quotes", newSubscription);
        Console.WriteLine("subscription request is sent with message id {0} for ticker {1}", msgId, ticker);
        _pHandler.SendReq(req);
    }

    private void RegisterRequest(int id, string methodName, MetaData data)
    {
        if (_messageRegistry.ContainsKey(id)) return;

        var qMessage = new QMessage()
        {
            Id = id,
            Method = methodName,
            MessageType = data.MessageType
        };

        if (data is Subscription subscription)
        {
            if (subscription.Ticker != "")
            {
                qMessage.Ticker = subscription.Ticker;
            }
        }

        if (data is DatasourceCallback ds)
        {
            qMessage.DataSource = ds.DataSource;
        }
        _messageRegistry.Add(id, qMessage);
    }

    public void Unsubscribe(MessageType msgType, string ticker)
    {
        var subscription = IsSubscribed(msgType, ticker);
        if (subscription == null) return;
        
        string[] args = {subscription.ClassCode, ticker};
        
        var req = new JsonReqMessage()
        {
            id = _msgIndexer.GetIndex(),
            type = "req",
            data = new JsonReqData()
            {
                method = "invoke",
                function = "Unsubscribe_Level_II_Quotes",
                arguments = args
            }
        };
        RegisterRequest(req.id, "Unsubscribe_Level_II_Quotes", subscription);
        Console.WriteLine("subscription cancellation is sent with message id {0} for ticker {1}", req.id, ticker);
        _pHandler.SendReq(req);
        Subscriptions.Remove(subscription);
    }

    private void OnReq(JsonMessage msg)
    {
        Console.WriteLine("msg arrived with message id " + msg.id);
    }

    private void OnResp(JsonMessage msg)
    {
        Console.WriteLine("resp arrived with message id {0}", msg.id);
        if (!_messageRegistry.TryGetValue(msg.id, out var newMessage)) return;

        Console.WriteLine("resp arrived with message id {0} to function {1} for ticker {2}", newMessage.Id, newMessage.Method, newMessage.Ticker);

        switch (newMessage.Method)
        {
            case "getQuoteLevel2":
            {
                try
                {
                    msg.body.TryGetProperty("result", out var result);
                    foreach (var res in result.EnumerateArray())
                    {
                        string jsonStr = res.GetRawText();
                        var snapshot = JsonConvert.DeserializeObject<OrderBook>(jsonStr);

                        if (snapshot != null && snapshot.bid_count != "" && snapshot.offer_count != "")
                        {
                            double.TryParse(snapshot.bid_count, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var bidsCount);
                            double.TryParse(snapshot.offer_count, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var offersCount);
                            if (bidsCount > 0 && offersCount > 0)
                                OrderBookUpdate?.Invoke(newMessage.Ticker, newMessage.MessageType, snapshot);
                        }
                        
                    }
                    
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error on Json Convert {0}", e.Message);
                }

                break;
            }
            default:
                return;
        }
    }

    private void OnDisconnect()
    {
    }
    
    #endregion
    
    #region Delegates and events

    /// <summary> Обработчик события разрыва подключения </summary>
    public delegate void OrderBookUpdateEventHandler(string ticker, MessageType msgType, OrderBook snapshot);

    /// <summary> Событие прихода нового сообщения </summary>
    public event OrderBookUpdateEventHandler? OrderBookUpdate;
    
    #endregion
}