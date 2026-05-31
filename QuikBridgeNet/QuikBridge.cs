using System.Collections.Concurrent;
using Newtonsoft.Json;
using QuikBridgeNet.Entities.CommandData;
using QuikBridgeNet.Entities.MessageMeta;
using QuikBridgeNet.Entities.ProtocolData;
using QuikBridgeNet.Helpers;
using QuikBridgeNetDomain;
using QuikBridgeNetDomain.Entities;
using QuikBridgeNetEvents;
using QuikBridgeNetEvents.Events;
using Serilog;

namespace QuikBridgeNet;

public delegate void DatasourceCallbackReceived(JsonMessage jMsg);

/// <summary>
/// Клиент верхнего уровня для работы с процессом QuikQtBridge.
/// </summary>
public class QuikBridge(
    QuikBridgeProtocolHandler protocolHandler,
    MessageRegistry msgRegistry,
    QuikBridgeEventAggregator eventAggregator,
    QuikBridgeConfig bridgeConfig)
{
    #region Fields

    private readonly MessageIndexer _msgIndexer = new();
    private readonly QuikBridgeSubscriptionManager _subscriptionManager = new();
    private readonly QuikBridgeDatasourceManager _datasourceManager = new();
    private readonly QuikBridgeCallbackRegistry<MessageType> _globalCallbackRegistry = new();

    private readonly ConcurrentDictionary<string, object> _dataSources = new();

    private QuikBridgeConnectionState _connectionState = QuikBridgeConnectionState.Disconnected;

    private bool _isExtendedLogging = bridgeConfig.UseExtendedLogging;
    
    #endregion
    
    #region Properties

    /// <summary>
    /// Включает расширенное логирование протокола для диагностики.
    /// </summary>
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

    /// <summary>
    /// Текущее состояние подключения клиента к мосту.
    /// </summary>
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

    /// <summary>
    /// Открывает сокетное соединение с QuikQtBridge и регистрирует встроенные callback-обработчики.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (ConnectionState != QuikBridgeConnectionState.Disconnected) return;
        
        ConnectionState = QuikBridgeConnectionState.Pending;
        var isConnectionEstablished = await protocolHandler.StartClientAsync(bridgeConfig.Host, bridgeConfig.Port, cancellationToken);
        
        if (isConnectionEstablished)
        {
            ConnectionState = QuikBridgeConnectionState.Connected;
            eventAggregator.SubscribeToServiceMessages(async (resp) =>
            {
                var registeredReq = resp.BridgeMessage;
                if (registeredReq == null) return;
                if (registeredReq.MessageType == MessageType.Datasource) {
                    var result = resp.Response?.body?["result"]?.ToObject<List<int>>();
                    if (result != null)
                    {
                        foreach (var r in result)
                        {
                            var dsName = BuildDatasourceName(registeredReq.Ticker, registeredReq.Interval);
                            _dataSources[dsName] = r;
                            if (IsExtendedLogging)
                                Log.Debug("DataSource with name {dsName} has been created; callback is set up", dsName);
                            await SetDsUpdateCallback(r, dsName);

                            var closeMessageId = await _datasourceManager.MarkReadyAsync(dsName, () => CloseDatasourceInternal(dsName, r));
                            if (closeMessageId == 0 && _datasourceManager.HasConsumers(dsName))
                            {
                                _ = eventAggregator.RaiseEvent(new DataSourceSetEvent() {DataSourceName = dsName, BridgeMessage = registeredReq});
                            }
                        }
                    }
                }/* else if (registeredReq.MessageType == MessageType.OrderBookInit)
                {
                    var result = resp.body?["result"]?.ToObject<List<bool>>();
                    if (result is { Count: > 0 } && result[0])
                    {
                        await GetOrderBookSnapshot(registeredReq.ClassCode, registeredReq.Ticker);
                        await DoSubscribeToOrderBook(registeredReq.ClassCode, registeredReq.Ticker);
                    }
                }*/
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
        MessageType[] callbacks = { MessageType.OnAllTrade, MessageType.OnTransReply, MessageType.OnOrder};
        foreach (var cb in callbacks)
        {
            await SetGlobalCallback(cb);
        }
    }
    
    /// <summary>
    /// Регистрирует callback для обновлений datasource, приходящих от моста.
    /// </summary>
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

    /// <summary>
    /// Запрашивает список доступных классов инструментов из QUIK.
    /// </summary>
    public async Task<int> GetClassesList()
    {
        var data = new JsonReqData()
        {
            method = "invoke",
            function = "getClassesList"
        };
        return await SendRequest(data, new MetaData() { MessageType = MessageType.Classes });
    }
    
    /// <summary>
    /// Запрашивает список инструментов для указанного кода класса.
    /// </summary>
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
    
    /// <summary>
    /// Запрашивает данные контракта по инструменту.
    /// </summary>
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

    /// <summary>
    /// Запрашивает текущую позицию по бумаге на торговом счете.
    /// </summary>
    public async Task<int> GetAccountPosition(string firmId, string clientCode, string secCode, string account, int limitKind)
    {
        string[] args = {$"\"{firmId}\",\"{clientCode}\",\"{secCode}\",\"{account}\",{limitKind}"};
        var data = new JsonReqData()
        {
            method = "invoke",
            function = MessageType.AccountPosition.GetDescription(),
            arguments = args
        };
        var metaData = new AccountPositionRequest()
        {
            MessageType = MessageType.AccountPosition,
            FirmId = firmId,
            ClientCode = clientCode,
            SecCode = secCode,
            Account = account,
            LimitKind = limitKind
        };
        return await SendRequest(data, metaData, false);
    }

    /// <summary>
    /// Запрашивает текущее состояние денежной позиции.
    /// </summary>
    public async Task<int> GetMoneyPosition(string firmId, string clientCode, string tag, string currencyCode, int limitKind)
    {
        string[] args = {$"\"{firmId}\",\"{clientCode}\",\"{tag}\",\"{currencyCode}\",{limitKind}"};
        var data = new JsonReqData()
        {
            method = "invoke",
            function = MessageType.MoneyPosition.GetDescription(),
            arguments = args
        };
        var metaData = new MoneyPositionRequest()
        {
            MessageType = MessageType.MoneyPosition,
            FirmId = firmId,
            ClientCode = clientCode,
            Tag = tag,
            CurrencyCode = currencyCode,
            LimitKind = limitKind
        };
        return await SendRequest(data, metaData, false);
    }

    /// <summary>
    /// Запрашивает текущую фьючерсную позицию по торговому счету.
    /// </summary>
    public async Task<int> GetFuturesHolding(string firmId, string account, string secCode, int positionType)
    {
        string[] args = {$"\"{firmId}\",\"{account}\",\"{secCode}\",{positionType}"};
        var data = new JsonReqData()
        {
            method = "invoke",
            function = MessageType.FuturesHolding.GetDescription(),
            arguments = args
        };
        var metaData = new FuturesHoldingRequest()
        {
            MessageType = MessageType.FuturesHolding,
            FirmId = firmId,
            Account = account,
            SecCode = secCode,
            PositionType = positionType
        };
        return await SendRequest(data, metaData, false);
    }

    /// <summary>
    /// Запрашивает состояние фьючерсного лимита по торговому счету.
    /// </summary>
    public async Task<int> GetFuturesLimit(string firmId, string account, int limitType, string currencyCode)
    {
        string[] args = {$"\"{firmId}\",\"{account}\",{limitType},\"{currencyCode}\""};
        var data = new JsonReqData()
        {
            method = "invoke",
            function = MessageType.FuturesLimit.GetDescription(),
            arguments = args
        };
        var metaData = new FuturesLimitRequest()
        {
            MessageType = MessageType.FuturesLimit,
            FirmId = firmId,
            Account = account,
            LimitType = limitType,
            CurrencyCode = currencyCode
        };
        return await SendRequest(data, metaData, false);
    }

    /// <summary>
    /// Создаёт datasource в QUIK для указанного инструмента и интервала.
    /// </summary>
    public async Task<int> CreateDs(string classCode, string secCode, string interval)
    {
        var dataSourceName = BuildDatasourceName(secCode, interval);
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
        var acquireResult = await _datasourceManager.AcquireAsync(dataSourceName, () => SendRequest(data, metaData, false));

        if (!acquireResult.IsFirstReference && _dataSources.ContainsKey(dataSourceName))
        {
            _ = eventAggregator.RaiseEvent(new DataSourceSetEvent()
            {
                DataSourceName = dataSourceName,
                BridgeMessage = new QMessage()
                {
                    Id = acquireResult.MessageId,
                    Method = data.function,
                    MessageType = metaData.MessageType,
                    Ticker = metaData.Ticker,
                    ClassCode = metaData.InstrumentClass,
                    Interval = metaData.Interval
                }
            });
        }

        return acquireResult.MessageId;
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

    /// <summary>
    /// Запрашивает значение бара из ранее созданного datasource.
    /// </summary>
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

    /// <summary>
    /// Закрывает ранее созданный datasource.
    /// </summary>
    public async Task<int> CloseDs(string dataSourceName)
    {
        return await _datasourceManager.ReleaseAsync(dataSourceName, async () =>
        {
            if (!_dataSources.TryRemove(dataSourceName, out var source)) return 0;
            return await CloseDatasourceInternal(dataSourceName, source);
        });
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

    /// <summary>
    /// Подписывается на обновления стакана по инструменту.
    /// </summary>
    public async Task<Guid> SubscribeToOrderBook(string classCode, string secCode)
    {
        var key = $"{classCode}:{secCode}:orderbook";
        var subscriptionEntry = await _subscriptionManager.SubscribeAsync(key, async () =>
        {
            // return await InitOrderBook(classCode, secCode);
            return await DoSubscribeToOrderBook(classCode, secCode);
        });
        return subscriptionEntry.SubscriptionToken;
    }

    private async Task<int> DoSubscribeToOrderBook(string classCode, string secCode)
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
        return await SendRequest(data, metaData);
    }

    /// <summary>
    /// Удаляет один токен подписки на стакан и отписывается от QUIK, когда уходит последний подписчик.
    /// </summary>
    public async Task<int> UnsubscribeToOrderBook(string classCode, string secCode, Guid subscriptionToken)
    {
        var key = $"{classCode}:{secCode}:orderbook";
        var msgId = await _subscriptionManager.UnsubscribeAsync(key, subscriptionToken, async () =>
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
            return await SendRequest(data, metaData);
        });

        if (IsExtendedLogging && msgId != 0)
            Log.Information("{Sec} orderbook is unsubscribed", secCode);

        return msgId;
    }

    /// <summary>
    /// Подписывается на обновления параметра из таблицы котировок.
    /// </summary>
    public async Task<Guid> SubscribeToQuotesTableParams(string classCode, string secCode, string paramName)
    {
        var key = $"{classCode}:{secCode}:{paramName}";
        var subscriptionEntry = await _subscriptionManager.SubscribeAsync(key, async () =>
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
            return await SendRequest(data, metaData);
        });
        return subscriptionEntry.SubscriptionToken;
    }

    /// <summary>
    /// Запрашивает текущее значение параметра из таблицы котировок.
    /// </summary>
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

    /// <summary>
    /// Удаляет один токен подписки на параметр и отписывается от QUIK, когда уходит последний подписчик.
    /// </summary>
    public async Task<int> UnsubscribeToQuotesTableParams(string classCode, string secCode, string paramName, Guid subscriptionToken)
    {
        var key = $"{classCode}:{secCode}:{paramName}";
        var msgId = await _subscriptionManager.UnsubscribeAsync(key, subscriptionToken, async () =>
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
            return await SendRequest(data, metaData);
        });

        if (IsExtendedLogging && msgId != 0)
            Log.Information("{Sec} {Param} is unsubscribed", secCode, paramName);
        
        return msgId;
    }

    /// <summary>
    /// Отправляет транзакцию в QUIK.
    /// </summary>
    public async Task<int> SendTransaction(TransactionBase transaction)
    {
        var transJson = JsonConvert.SerializeObject(transaction);
        string[] args = { transJson };
        var data = new JsonReqData()
        {
            method = "invoke",
            function = "sendTransaction",
            arguments = args
        };

        var metaData = new TransactionMeta()
        {
            MessageType = MessageType.SendTransaction,
            InstrumentClass = transaction.CLASSCODE,
            Ticker = transaction.SECCODE,
            Transaction = transaction
        };
        return await SendRequest(data, metaData);
    }

    /// <summary>
    /// Регистрирует глобальный callback QUIK по описанию типа сообщения.
    /// </summary>
    public async Task<int> SetGlobalCallback(MessageType name)
    {
        return await _globalCallbackRegistry.RegisterAsync(name, async () =>
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
        });
    }

    private static string BuildDatasourceName(string ticker, string interval)
    {
        return ticker + "[" + interval + "]";
    }

    private async Task<int> CloseDatasourceInternal(string dataSourceName, object source)
    {
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
            case AccountPositionRequest accountPositionRequest:
                qMessage.FirmId = accountPositionRequest.FirmId;
                qMessage.ClientCode = accountPositionRequest.ClientCode;
                qMessage.Ticker = accountPositionRequest.SecCode;
                qMessage.Account = accountPositionRequest.Account;
                qMessage.LimitKind = accountPositionRequest.LimitKind;
                break;
            case MoneyPositionRequest moneyPositionRequest:
                qMessage.FirmId = moneyPositionRequest.FirmId;
                qMessage.ClientCode = moneyPositionRequest.ClientCode;
                qMessage.Tag = moneyPositionRequest.Tag;
                qMessage.CurrencyCode = moneyPositionRequest.CurrencyCode;
                qMessage.LimitKind = moneyPositionRequest.LimitKind;
                break;
            case FuturesHoldingRequest futuresHoldingRequest:
                qMessage.FirmId = futuresHoldingRequest.FirmId;
                qMessage.Account = futuresHoldingRequest.Account;
                qMessage.Ticker = futuresHoldingRequest.SecCode;
                qMessage.PositionType = futuresHoldingRequest.PositionType;
                break;
            case FuturesLimitRequest futuresLimitRequest:
                qMessage.FirmId = futuresLimitRequest.FirmId;
                qMessage.Account = futuresLimitRequest.Account;
                qMessage.LimitKind = futuresLimitRequest.LimitType;
                qMessage.CurrencyCode = futuresLimitRequest.CurrencyCode;
                break;
        }

        msgRegistry.RegisterMessage(id, qMessage);
    }
    
    private void OnConnectionStateChanged(QuikBridgeConnectionState newState)
    {
        ConnectionStateChanged?.Invoke(newState);
    }

    /// <summary>
    /// Возвращает снимок внутренних метрик очередей и обработчиков событий.
    /// </summary>
    public IReadOnlyCollection<EventProcessingMetrics> GetEventProcessingMetrics()
    {
        return eventAggregator.GetAllMetricsSnapshots();
    }

    /// <summary>
    /// Возвращает снимок внутренних метрик для конкретного типа события.
    /// </summary>
    public EventProcessingMetrics GetEventProcessingMetrics<TEvent>()
    {
        return eventAggregator.GetMetricsSnapshot<TEvent>();
    }

    /// <summary>
    /// Останавливает обработчик протокола и завершает внутреннюю обработку событий.
    /// </summary>
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
    
    /// <summary>
    /// Вызывается при изменении состояния подключения к мосту.
    /// </summary>
    public delegate void ConnectionStateChangedEventHandler(QuikBridgeConnectionState newConnectionState);
    public event ConnectionStateChangedEventHandler? ConnectionStateChanged;
    
    #endregion
}