using System.Collections.Concurrent;
using System.Threading;
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
    private readonly ConcurrentDictionary<string, SessionResourceDefinition> _sessionResources = new();
    private QuikBridgeSessionCoordinator? _sessionCoordinator;
    private QuikBridgeRequestExpiryController? _requestExpiryController;
    private int _internalServiceHandlerRegistered;

    private QuikBridgeConnectionState _connectionState = QuikBridgeConnectionState.Disconnected;

    private bool _isExtendedLogging = bridgeConfig.UseExtendedLogging;
    
    #endregion
    
    #region Properties

    private QuikBridgeSessionCoordinator SessionCoordinator => _sessionCoordinator ??= new QuikBridgeSessionCoordinator(
        msgRegistry,
        eventAggregator,
        _subscriptionManager,
        _datasourceManager,
        _globalCallbackRegistry,
        _dataSources,
        _sessionResources);

    private QuikBridgeRequestExpiryController RequestExpiryController => _requestExpiryController ??= new QuikBridgeRequestExpiryController(
        msgRegistry,
        eventAggregator,
        bridgeConfig);

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
        set => SetPublicConnectionState(value);
    }
    
    #endregion
    
    #region Methods

    /// <summary>
    /// Открывает сокетное соединение с QuikQtBridge и регистрирует встроенные callback-обработчики.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (ConnectionState != QuikBridgeConnectionState.Disconnected) return;

        await StopRequestExpiryLoopAsync();
        
        EnterPendingState();
        var isConnectionEstablished = await protocolHandler.StartClientAsync(bridgeConfig.Host, bridgeConfig.Port, cancellationToken);
        
        if (isConnectionEstablished)
        {
            EnterConnectedState();
            EnsureInternalServiceHandlerRegistered();
            StartRequestExpiryLoop();
            await SetupCallbacks();
            await RestoreSessionStateAsync();
        }
        else
        {
            TransitionToErrorState();
        }
    }

    internal async Task HandleConnectionLostAsync()
    {
        await StopRequestExpiryLoopAsync();
        SessionCoordinator.InvalidateRemoteState();
        TransitionToErrorState();
    }

    private void SetPublicConnectionState(QuikBridgeConnectionState newState)
    {
        if (newState == QuikBridgeConnectionState.Error)
        {
            TransitionToErrorState();
            return;
        }

        SetConnectionState(newState);
    }

    private void EnterPendingState()
    {
        SetConnectionState(QuikBridgeConnectionState.Pending);
    }

    private void EnterConnectedState()
    {
        SetConnectionState(QuikBridgeConnectionState.Connected);
    }

    private void EnterDisconnectedState(bool forceNotification = false)
    {
        SetConnectionState(QuikBridgeConnectionState.Disconnected, forceNotification);
    }

    private void TransitionToErrorState()
    {
        OnConnectionStateChanged(QuikBridgeConnectionState.Error);
        EnterDisconnectedState(forceNotification: true);
    }

    private void SetConnectionState(QuikBridgeConnectionState newState, bool forceNotification = false)
    {
        if (!forceNotification && newState == _connectionState)
        {
            return;
        }

        _connectionState = newState;
        OnConnectionStateChanged(_connectionState);
    }

    private void StartRequestExpiryLoop()
    {
        RequestExpiryController.Start();
    }

    private async Task ExpirePendingRequestsAsync()
    {
        await RequestExpiryController.ExpirePendingRequestsAsync();
    }

    private async Task StopRequestExpiryLoopAsync()
    {
        if (_requestExpiryController == null)
        {
            return;
        }

        await _requestExpiryController.StopAsync();
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
    /// Повторно регистрирует удалённое session-scoped состояние после реконнекта.
    /// </summary>
    public async Task RestoreSessionStateAsync()
    {
        if (ConnectionState != QuikBridgeConnectionState.Connected)
        {
            return;
        }

        await SessionCoordinator.RestoreAsync(
            SetGlobalCallback,
            DoSubscribeToOrderBook,
            DoSubscribeToQuotesTableParams,
            CreateDatasourceInternal);
    }

    private void EnsureInternalServiceHandlerRegistered()
    {
        if (Interlocked.Exchange(ref _internalServiceHandlerRegistered, 1) == 1)
        {
            return;
        }

        eventAggregator.SubscribeToServiceMessages(HandleInternalServiceMessageAsync);
    }

    private async Task HandleInternalServiceMessageAsync(ServiceMessageArrivedEvent serviceMessage)
    {
        var registeredReq = serviceMessage.BridgeMessage;
        if (registeredReq == null || registeredReq.MessageType != MessageType.Datasource)
        {
            return;
        }

        var datasourceIds = serviceMessage.Response?.body?["result"]?.ToObject<List<int>>();
        if (datasourceIds == null)
        {
            return;
        }

        foreach (var datasourceId in datasourceIds)
        {
            await RegisterDatasourceHandleAsync(registeredReq, datasourceId);
        }
    }

    private async Task RegisterDatasourceHandleAsync(QMessage registeredReq, int datasourceId)
    {
        var datasourceName = BuildDatasourceName(registeredReq.Ticker, registeredReq.Interval);
        _dataSources[datasourceName] = datasourceId;

        if (IsExtendedLogging)
        {
            Log.Debug("DataSource with name {DatasourceName} has been created; callback is set up", datasourceName);
        }

        await SetDsUpdateCallback(datasourceId, datasourceName);
        await FinalizeDatasourceReadyAsync(datasourceName, datasourceId, registeredReq);
    }

    private async Task FinalizeDatasourceReadyAsync(string datasourceName, int datasourceId, QMessage registeredReq)
    {
        var closeMessageId = await _datasourceManager.MarkReadyAsync(
            datasourceName,
            () => CloseDatasourceInternal(datasourceName, datasourceId));

        if (closeMessageId == 0 && _datasourceManager.HasConsumers(datasourceName))
        {
            await eventAggregator.RaiseEvent(new DataSourceSetEvent
            {
                DataSourceName = datasourceName,
                BridgeMessage = registeredReq
            });
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
        try
        {
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
        catch
        {
            msgRegistry.RemoveMessage(msgId);
            throw;
        }
    }

    /// <summary>
    /// Запрашивает список доступных классов инструментов из QUIK.
    /// </summary>
    public async Task<int> GetClassesList()
    {
        var data = QuikCommandFactory.CreateInvoke("getClassesList");
        return await SendRequest(data, new MetaData() { MessageType = MessageType.Classes });
    }
    
    /// <summary>
    /// Запрашивает список инструментов для указанного кода класса.
    /// </summary>
    public async Task<int> GetClassSecurities(string classCode)
    {
        var data = QuikCommandFactory.CreateInvoke("getClassSecurities", QuikCommandFactory.CreateQuotedArguments(classCode));
        return await SendRequest(data, new ClassCode() { MessageType = MessageType.Securities, InstrumentClass = classCode }, false);
    }
    
    /// <summary>
    /// Запрашивает данные контракта по инструменту.
    /// </summary>
    public async Task<int> GetSecurityInfo(string classCode, string secCode)
    {
        var data = QuikCommandFactory.CreateInvoke("getSecurityInfo", QuikCommandFactory.CreateQuotedArguments(classCode, secCode));
        return await SendRequest(data, new Subscription() { MessageType = MessageType.SecurityContract, InstrumentClass = classCode, Ticker = secCode}, false);
    }

    /// <summary>
    /// Запрашивает текущую позицию по бумаге на торговом счете.
    /// </summary>
    public async Task<int> GetAccountPosition(string firmId, string clientCode, string secCode, string account, int limitKind)
    {
        var data = QuikCommandFactory.CreateInvoke(
            MessageType.AccountPosition.GetDescription(),
            QuikCommandFactory.CreateMixedArguments(firmId, clientCode, secCode, account, limitKind));
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
        var data = QuikCommandFactory.CreateInvoke(
            MessageType.MoneyPosition.GetDescription(),
            QuikCommandFactory.CreateMixedArguments(firmId, clientCode, tag, currencyCode, limitKind));
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
        var data = QuikCommandFactory.CreateInvoke(
            MessageType.FuturesHolding.GetDescription(),
            QuikCommandFactory.CreateMixedArguments(firmId, account, secCode, positionType));
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
        var data = QuikCommandFactory.CreateInvoke(
            MessageType.FuturesLimit.GetDescription(),
            QuikCommandFactory.CreateMixedArguments(firmId, account, limitType, currencyCode));
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
        var acquireResult = await _datasourceManager.AcquireAsync(dataSourceName, () => CreateDatasourceInternal(classCode, secCode, interval));
        _sessionResources[dataSourceName] = new SessionResourceDefinition(
            SessionResourceKind.Datasource,
            classCode,
            secCode,
            dataSourceName,
            Interval: interval);

        var bridgeMessage = new QMessage()
        {
            Id = acquireResult.MessageId,
            Method = "CreateDataSource",
            MessageType = MessageType.Datasource,
            Ticker = secCode,
            ClassCode = classCode,
            Interval = interval
        };

        if (!acquireResult.IsFirstReference && _dataSources.ContainsKey(dataSourceName))
        {
            await eventAggregator.RaiseEvent(new DataSourceSetEvent()
            {
                DataSourceName = dataSourceName,
                BridgeMessage = bridgeMessage
            });
        }

        return acquireResult.MessageId;
    }

    private async Task<int> CreateDatasourceInternal(string classCode, string secCode, string interval)
    {
        var data = QuikCommandFactory.CreateInvoke(
            "CreateDataSource",
            QuikCommandFactory.CreateMixedArguments(classCode, secCode, interval));
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
        var data = QuikCommandFactory.CreateInvoke("SetUpdateCallback", [jsonArguments], datasource);
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
        var data = QuikCommandFactory.CreateInvoke(barFunc.GetDescription(), [Convert.ToString(barIndex)], source);
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
        var messageId = await _datasourceManager.ReleaseAsync(dataSourceName, async () =>
        {
            if (!_dataSources.TryRemove(dataSourceName, out var source)) return 0;
            return await CloseDatasourceInternal(dataSourceName, source);
        });

        if (!_datasourceManager.HasConsumers(dataSourceName))
        {
            _sessionResources.TryRemove(dataSourceName, out _);
        }

        return messageId;
    }
    
    private async Task<int> InitOrderBook(string classCode, string secCode)
    {
        var data = QuikCommandFactory.CreateInvoke("Subscribe_Level_II_Quotes", QuikCommandFactory.CreateQuotedArguments(classCode, secCode));
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
        var data = QuikCommandFactory.CreateInvoke("getQuoteLevel2", QuikCommandFactory.CreateQuotedArguments(classCode, secCode));
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
        _sessionResources[key] = new SessionResourceDefinition(
            SessionResourceKind.OrderBookSubscription,
            classCode,
            secCode,
            key);
        return subscriptionEntry.SubscriptionToken;
    }

    private async Task<int> DoSubscribeToOrderBook(string classCode, string secCode)
    {
        var data = QuikCommandFactory.CreateQuotesSubscription("subscribeQuotes", classCode, secCode);
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
            var data = QuikCommandFactory.CreateQuotesSubscription("unsubscribeQuotes", classCode, secCode);
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

        if (!_subscriptionManager.HasSubscribers(key))
        {
            _sessionResources.TryRemove(key, out _);
        }

        return msgId;
    }

    /// <summary>
    /// Подписывается на обновления параметра из таблицы котировок.
    /// </summary>
    public async Task<Guid> SubscribeToQuotesTableParams(string classCode, string secCode, string paramName)
    {
        var key = $"{classCode}:{secCode}:{paramName}";
        var subscriptionEntry = await _subscriptionManager.SubscribeAsync(key, () => DoSubscribeToQuotesTableParams(classCode, secCode, paramName));
        _sessionResources[key] = new SessionResourceDefinition(
            SessionResourceKind.QuoteParameterSubscription,
            classCode,
            secCode,
            key,
            ParamName: paramName);
        return subscriptionEntry.SubscriptionToken;
    }

    private async Task<int> DoSubscribeToQuotesTableParams(string classCode, string secCode, string paramName)
    {
        var data = QuikCommandFactory.CreateParamSubscription("subscribeParamChanges", classCode, secCode, paramName);
        var metaData = new ParamSubscription()
        {
            MessageType = MessageType.SubscribeParam,
            InstrumentClass = classCode,
            Ticker = secCode,
            ParamName = paramName
        };
        return await SendRequest(data, metaData);
    }

    /// <summary>
    /// Запрашивает текущее значение параметра из таблицы котировок.
    /// </summary>
    public async Task<int> GetQuotesTableParam(string classCode, string secCode, string paramName)
    {
        var data = QuikCommandFactory.CreateInvoke(
            MessageType.GetParam.GetDescription(),
            QuikCommandFactory.CreateQuotedArguments(classCode, secCode, paramName));
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
            var data = QuikCommandFactory.CreateParamSubscription("unsubscribeParamChanges", classCode, secCode, paramName);
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

        if (!_subscriptionManager.HasSubscribers(key))
        {
            _sessionResources.TryRemove(key, out _);
        }
        
        return msgId;
    }

    /// <summary>
    /// Отправляет транзакцию в QUIK.
    /// </summary>
    public async Task<int> SendTransaction(TransactionBase transaction)
    {
        var transJson = JsonConvert.SerializeObject(transaction);
        var data = QuikCommandFactory.CreateInvoke("sendTransaction", [transJson]);

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
            var data = QuikCommandFactory.CreateCallbackRegistration(name.GetDescription());

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
        var data = QuikCommandFactory.CreateInvoke("Close", obj: source);
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

        msgRegistry.RegisterMessage(id, QuikMessageMapper.CreateQMessage(id, methodName, data));
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
        FinishAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Асинхронно останавливает обработчик протокола и завершает внутреннюю обработку событий.
    /// </summary>
    public async Task FinishAsync()
    {
        protocolHandler.Finish();
        await protocolHandler.StopClientAsync();
        await StopRequestExpiryLoopAsync();
        SessionCoordinator.InvalidateRemoteState();
        EnterDisconnectedState();
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