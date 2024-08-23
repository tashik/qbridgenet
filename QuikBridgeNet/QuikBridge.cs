using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using QuikBridgeNet.Entities;
using QuikBridgeNet.Entities.CommandData;
using QuikBridgeNet.Entities.MessageMeta;
using QuikBridgeNet.Helpers;
using Serilog;

namespace QuikBridgeNet;

public class QuikBridge
{
    #region Fields
    
    private readonly QuikBridgeProtocolHandler _pHandler;

    private readonly MessageIndexer _msgIndexer;

    private List<Subscription> Subscriptions { get; set; }

    private readonly MessageRegistry _messageRegistry;

    private readonly IServiceProvider _serviceProvider;
    
    #endregion
    
    #region Constructors

    public QuikBridge(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _pHandler = new QuikBridgeProtocolHandler(serviceProvider.GetRequiredService<QuikBridgeEventDispatcher>());

        Subscriptions = new List<Subscription>();
        _messageRegistry = serviceProvider.GetRequiredService<MessageRegistry>();
        
        _msgIndexer = new MessageIndexer();
        
        //_orderBookTimer = new Timer(Timer_Tick, new AutoResetEvent(false), 0, 2000);
    }
    
    #endregion
    
    #region Methods
    
    public QuikBridgeGlobalEventAggregator GetGlobalEventAggregator()
    {
        return _serviceProvider.GetRequiredService<QuikBridgeGlobalEventAggregator>();
    }

    public async Task StartAsync(string host, int port, CancellationToken cancellationToken)
    {
        await _pHandler.StartClientAsync(host, port, cancellationToken);
    }

    private async Task<int> SendRequest(JsonCommandData data, MetaData metaData)
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
        await _pHandler.SendReqAsync(msg);
        Log.Debug($"New message id: {msgId}");
        return msgId;
    }

    public async Task<int> GetClassesList()
    {
        var data = new JsonReqData()
        {
            method = "invoke",
            function = "getClassesList"
        };
        return await SendRequest(data, new MetaData() { MessageType = MessageType.Classes });
    }

    public async Task<int> CreateDs(string classCode, string secCode, string interval)
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
        return await SendRequest(data, metaData);
    }

    public async Task<int> SetDsUpdateCallback(object datasource, Func<string, string, int>? callback = null)
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
        return await SendRequest(data, metaData);
    }

    public async Task<int> GetBar(object datasource, MessageType barFunc, int barIndex)
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
        return await SendRequest(data, metaData);
    }

    public async Task<int> CloseDs(object? datasource)
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
        return await SendRequest(data, metaData);
    }

    public async Task<int> SubscribeToOrderBook(string classCode, string secCode)
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
        return await SendRequest(data, metaData);
    }

    public async Task<int> UnsubscribeToOrderBook(string classCode, string secCode)
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
        return await SendRequest(data, metaData);
    }

    public async Task<int> SubscribeToQuotesTableParams(string classCode, string secCode, string paramName)
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
        return await SendRequest(data, metaData);
    }

    public async Task<int> UnsubscribeToQuotesTableParams(string classCode, string secCode, string paramName)
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
        return await SendRequest(data, metaData);
    }

    public async Task<int> SendTransaction(TransactionBase transaction)
    {
        var transJson = JsonConvert.SerializeObject(transaction);
        string[] args = { transJson };
        var data = new JsonReqData()
        {
            method = "invoike",
            function = "sendTransaction",
            arguments = args
        };

        var metaData = new TransactionMeta()
        {
            MessageType = MessageType.UnsubscribeParam,
            ClassCode = transaction.CLASSCODE,
            Ticker = transaction.SECCODE,
            Transaction = transaction
        };
        return await SendRequest(data, metaData);
    }

    public async Task<int> SetGlobalCallback(MessageType name)
    {
        var data = new JsonCommandDataCallback()
        {
            method = "register",
            callback = name.GetDescription()
        };

        var metaData = new MetaData()
        {
            MessageType = name
        };
        return await SendRequest(data, metaData);
    }
    

    private Subscription? IsSubscribed(MessageType msgType, string ticker)
    {
        return Subscriptions.FindLast(item => item.Ticker == ticker && item.MessageType == msgType);
    }

    public async Task SubscribeOrderBook(string ticker, string classCode)
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
        Log.Debug("subscription request is sent with message id {0} for ticker {1}", msgId, ticker);
        await _pHandler.SendReqAsync(req);
    }

    private void RegisterRequest(int id, string methodName, MetaData data)
    {
        if (_messageRegistry.TryGetMetadata(id, out var _)) return;

        var qMessage = new QMessage()
        {
            Id = id,
            Method = methodName,
            MessageType = data.MessageType
        };

        switch (data)
        {
            case Subscription subscription:
            {
                if (subscription.Ticker != "")
                {
                    qMessage.Ticker = subscription.Ticker;
                }

                break;
            }
            case DatasourceCallback ds:
                qMessage.DataSource = ds.DataSource;
                break;
        }

        _messageRegistry.RegisterMessage(id, qMessage);
    }

    public async Task Unsubscribe(MessageType msgType, string ticker)
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
        Log.Debug("subscription cancellation is sent with message id {0} for ticker {1}", req.id, ticker);
        await _pHandler.SendReqAsync(req);
        Subscriptions.Remove(subscription);
    }

    public void Finish()
    {
        _pHandler.StopClient();
    }
    
    #endregion
}