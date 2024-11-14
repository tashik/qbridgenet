using System.Collections.Concurrent;
using System.Collections.Specialized;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using QuikBridgeNet.Entities;
using QuikBridgeNet.Entities.CommandData;
using QuikBridgeNet.Entities.MessageMeta;
using QuikBridgeNet.Helpers;
using Serilog;

namespace QuikBridgeNet;

public delegate void DatasourceCallbackReceived(JsonMessage jMsg);

public class QuikBridge(
    QuikBridgeProtocolHandler protocolHandler,
    MessageRegistry msgRegistry,
    QuikBridgeEventAggregator eventAggregator)
{
    #region Fields

    private readonly MessageIndexer _msgIndexer = new();
    private readonly QuikBridgeSubscriptionManager _subscriptionManager = new();

    private readonly Dictionary<string, object> _dataSources = new();

    private QuikBridgeConnectionState _connectionState = QuikBridgeConnectionState.Disconnected;

    private bool _isExtendedLogging = true;
    
    private readonly ConcurrentDictionary<string, int> _paramSubscriptions = new();
    
    #endregion
    
    #region Properties

    public bool IsExtendedLogging
    {
        get => _isExtendedLogging;
        set
        {
            if (value == _isExtendedLogging) return;
            _isExtendedLogging = value;
            protocolHandler.IsExtendedLogging = value;
        }
    }

    public QuikBridgeConnectionState ConnectionState
    {
        get => _connectionState;
        set
        {
            if (value == _connectionState) return;
            if (value == QuikBridgeConnectionState.Error)
            {
                OnConnectionStateChanged(value);
                _connectionState = QuikBridgeConnectionState.Disconnected;
            }
            else
            {
                _connectionState = value;
            }
            
            OnConnectionStateChanged(_connectionState);
        }
    }
    
    #endregion
    
    #region Methods

    public async Task StartAsync(string host, int port, CancellationToken cancellationToken)
    {
        if (ConnectionState != QuikBridgeConnectionState.Disconnected) return;
        
        ConnectionState = QuikBridgeConnectionState.Pending;
        var isConnectionEstablished = await protocolHandler.StartClientAsync(host, port, cancellationToken);
        
        if (isConnectionEstablished)
        {
            ConnectionState = QuikBridgeConnectionState.Connected;
            eventAggregator.SubscribeToServiceMessages(async (resp, registeredReq) =>
            {
                if (registeredReq == null) return;
                if (registeredReq.MessageType == MessageType.Datasource) {
                    var result = resp.body?["result"]?.ToObject<List<int>>();
                    if (result != null)
                    {
                        foreach (var r in result)
                        {
                            var dsName = registeredReq.Ticker + "[" + registeredReq.Interval + "]";
                            _dataSources[dsName] = r;
                            if (IsExtendedLogging)
                                Log.Debug("DataSource with name {dsName} has been created; callback is set up", dsName);
                            await SetDsUpdateCallback(r, dsName);
                            _ = eventAggregator.RaiseDataSourceSetEvent(dsName, registeredReq);
                        }
                    }
                } else if (registeredReq.MessageType == MessageType.OrderBookInit)
                {
                    var result = resp.body?["result"]?.ToObject<List<bool>>();
                    if (result is { Count: > 0 } && result[0])
                    {
                        await GetOrderBookSnapshot(registeredReq.ClassCode, registeredReq.Ticker);
                        await DoSubscribeToOrderBook(registeredReq.ClassCode, registeredReq.Ticker);
                    }
                }
            });
            await SetupCallbacks();
        }
        else
        {
            ConnectionState = QuikBridgeConnectionState.Error;
        }
    }

    private async Task SetupCallbacks()
    {
        MessageType[] callbacks = { MessageType.OnTrade, MessageType.OnTransReply, MessageType.OnOrder};
        foreach (var cb in callbacks)
        {
            await SetGlobalCallback(cb);
        }
    }
    
    public void RegisterDataSourceCallback(DatasourceCallbackReceived callback)
    {
        protocolHandler.RegisterDataSourceCallback(callback);
    }

    private async Task<int> SendRequest(JsonCommandData data, MetaData metaData, bool preprocessArguments = true)
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
        await protocolHandler.SendReqAsync(msg, preprocessArguments);
        if (IsExtendedLogging)
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
    
    public async Task<int> GetClassSecurities(string classCode)
    {
        string[] args = {"\"" + classCode + "\""};
        var data = new JsonReqData()
        {
            method = "invoke",
            function = "getClassSecurities",
            arguments = args
        };
        return await SendRequest(data, new ClassCode() { MessageType = MessageType.Securities, InstrumentClass = classCode }, false);
    }
    
    public async Task<int> GetSecurityInfo(string classCode, string secCode)
    {
        string[] args = {"\"" + classCode + "\",\"" + secCode + "\""};
        var data = new JsonReqData()
        {
            method = "invoke",
            function = "getSecurityInfo",
            arguments = args
        };
        return await SendRequest(data, new Subscription() { MessageType = MessageType.SecurityContract, InstrumentClass = classCode, Ticker = secCode}, false);
    }

    public async Task<int> CreateDs(string classCode, string secCode, string interval)
    {
        string[] args = {$"\"{classCode}\",\"{secCode}\",{interval}"};
        var data = new JsonReqData()
        {
            method = "invoke", 
            function = "CreateDataSource",
            arguments = args
        };
        var metaData = new DataSource()
        {
            MessageType = MessageType.Datasource,
            Ticker = secCode,
            InstrumentClass = classCode,
            Interval = interval
        };
        return await SendRequest(data, metaData, false);
    }

    private async Task<int> SetDsUpdateCallback(object datasource, string dataSourceName)
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
            DataSource = dataSourceName
        };
        return await SendRequest(data, metaData, false);
    }

    public async Task<int> GetBar(string dataSourceName, MessageType barFunc, int barIndex)
    {
        if (!_dataSources.TryGetValue(dataSourceName, out var source)) return 0;
        string[] args = {Convert.ToString(barIndex)};
        var data = new JsonReqData()
        {
            method = "invoke",
            obj = source,
            function = barFunc.GetDescription(),
            arguments = args
        };
        var metaData = new DatasourceCallback()
        {
            MessageType = barFunc,
            DataSource = dataSourceName
        };
        return await SendRequest(data, metaData, false);
    }

    public async Task<int> CloseDs(string dataSourceName)
    {
        if (!_dataSources.TryGetValue(dataSourceName, out var source)) return 0;
        var data = new JsonReqData()
        {
            method = "invoke",
            obj = source,
            function = "Close",
        };
        var metaData = new DatasourceCallback()
        {
            MessageType = MessageType.DatasourceClose,
            DataSource = dataSourceName
        };
        return await SendRequest(data, metaData);
    }
    
    private async Task<int> InitOrderBook(string classCode, string secCode)
    {
        string[] args = {"\"" + classCode + "\",\"" + secCode + "\""};
        var data = new JsonReqData()
        {
            method = "invoke",
            function = "Subscribe_Level_II_Quotes",
            arguments = args
        };
        var metaData = new Subscription()
        {
            MessageType = MessageType.OrderBookInit,
            InstrumentClass = classCode,
            Ticker = secCode
        };
        return await SendRequest(data, metaData, false);
    }
    
    private async Task<int> GetOrderBookSnapshot(string classCode, string secCode)
    {
        string[] args = {"\"" + classCode + "\",\"" + secCode + "\""};
        var data = new JsonReqData()
        {
            method = "invoke",
            function = "getQuoteLevel2",
            arguments = args
        };
        var metaData = new Subscription()
        {
            MessageType = MessageType.OrderBookSnapshot,
            InstrumentClass = classCode,
            Ticker = secCode
        };
        return await SendRequest(data, metaData, false);
    }

    public async Task<Guid> SubscribeToOrderBook(string classCode, string secCode)
    {
        var key = $"{classCode}:{secCode}:orderbook";
        var msgId = 0;
        if (!_subscriptionManager.ContainsKey(key))
        {
            msgId = await InitOrderBook(classCode, secCode);
        }
        var subscriptionEntry = _subscriptionManager.Subscribe(key, msgId);
        return subscriptionEntry.SubscriptionToken;
    }

    private async Task DoSubscribeToOrderBook(string classCode, string secCode)
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
            InstrumentClass = classCode,
            Ticker = secCode
        };
        await SendRequest(data, metaData);
    }

    public async Task<int> UnsubscribeToOrderBook(string classCode, string secCode, Guid subscriptionToken)
    {
        var key = $"{classCode}:{secCode}:orderbook";
        var msgId = 0;

        if (!_subscriptionManager.ContainsKey(key))
        {
            return msgId;
        }
        _subscriptionManager.Unsubscribe(key, subscriptionToken);

        if (!_subscriptionManager.ContainsKey(key))
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
                InstrumentClass = classCode,
                Ticker = secCode
            };
            msgId = await SendRequest(data, metaData);
        }
        if (IsExtendedLogging)
            Log.Information("{Sec} orderbook is unsubscribed", secCode);

        return msgId;
    }

    public async Task<Guid> SubscribeToQuotesTableParams(string classCode, string secCode, string paramName)
    {
        var key = $"{classCode}:{secCode}:{paramName}";
        var msgId = 0;
        if (!_subscriptionManager.ContainsKey(key))
        {
            var data = new JsonCommandDataSubscribeParam()
            {
                method = "subscribeParamChanges",
                cl = classCode,
                security = secCode,
                param = paramName
            };
            var metaData = new ParamSubscription()
            {
                MessageType = MessageType.SubscribeParam,
                InstrumentClass = classCode,
                Ticker = secCode,
                ParamName = paramName
            };
            msgId = await SendRequest(data, metaData);
        }

        var subscriptionEntry = _subscriptionManager.Subscribe(key, msgId);
        return subscriptionEntry.SubscriptionToken;
    }

    public async Task<int> GetQuotesTableParam(string classCode, string secCode, string paramName)
    {
        string[] args = {$"\"{classCode}\",\"{secCode}\",\"{paramName}\""};
        var data = new JsonReqData()
        {
            method = "invoke", 
            function = MessageType.GetParam.GetDescription(),
            arguments = args
        };
        var metaData = new ParamSubscription()
        {
            MessageType = MessageType.GetParam,
            InstrumentClass = classCode,
            Ticker = secCode,
            ParamName = paramName
        };
        return await SendRequest(data, metaData, false);
    }

    public async Task<int> UnsubscribeToQuotesTableParams(string classCode, string secCode, string paramName, Guid subscriptionToken)
    {
        var key = $"{classCode}:{secCode}:{paramName}";
        var msgId = 0;

        if (!_subscriptionManager.ContainsKey(key))
        {
            return 0;
        }
        _subscriptionManager.Unsubscribe(key, subscriptionToken);

        if (!_subscriptionManager.ContainsKey(key))
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
                InstrumentClass = classCode,
                Ticker = secCode
            };
            msgId = await SendRequest(data, metaData);
        }

        if (IsExtendedLogging)
                Log.Information("{Sec} {Param} is unsubscribed", secCode, paramName);
        
        return msgId;
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
            InstrumentClass = transaction.CLASSCODE,
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

    private void RegisterRequest(int id, string methodName, MetaData data)
    {
        if (msgRegistry.TryGetMetadata(id, out var _)) return;

        var qMessage = new QMessage()
        {
            Id = id,
            Method = methodName,
            MessageType = data.MessageType
        };

        switch (data)
        {
            case Subscription subscription:
                if (subscription.Ticker != "")
                {
                    qMessage.Ticker = subscription.Ticker;
                }

                if (subscription.InstrumentClass != "")
                {
                    qMessage.ClassCode = subscription.InstrumentClass;
                }

                if (subscription is DataSource dsInit)
                {
                    qMessage.Interval = dsInit.Interval;
                }

                if (subscription is ParamSubscription paramSubscription)
                {
                    qMessage.ParamName = paramSubscription.ParamName;
                }
                break;
            case DatasourceCallback ds:
                qMessage.DataSource = ds.DataSource;
                break;
        }

        msgRegistry.RegisterMessage(id, qMessage);
    }
    
    private void OnConnectionStateChanged(QuikBridgeConnectionState newState)
    {
        ConnectionStateChanged?.Invoke(newState);
    }

    public void Finish()
    {
        protocolHandler.Finish();
        eventAggregator.Close();
        Thread.Sleep(1000);
        protocolHandler.StopClient();
        ConnectionState = QuikBridgeConnectionState.Disconnected;
    }
    
    #endregion
    
    #region Delegates and events
    
    public delegate void ConnectionStateChangedEventHandler(QuikBridgeConnectionState newConnectionState);
    public event ConnectionStateChangedEventHandler? ConnectionStateChanged;
    
    #endregion
}