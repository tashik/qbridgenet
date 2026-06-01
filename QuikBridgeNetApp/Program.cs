using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuikBridgeNet;
using QuikBridgeNet.Helpers;
using QuikBridgeNetApp;
using QuikBridgeNetDomain;
using QuikBridgeNetDomain.Entities;
using QuikBridgeNetEvents;
using QuikBridgeNetEvents.Events;
using Serilog;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false, true);
        var configuration = builder.Build();
        var liveLoadTestSettings = configuration.GetSection("LiveOptionBoardLoadTest").Get<LiveOptionBoardLoadTestSettings>() ?? new LiveOptionBoardLoadTestSettings();

        var serviceCollection = new ServiceCollection();
        QuikBridgeServiceConfiguration.ConfigureServices(serviceCollection, configuration);
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        var client = serviceProvider.GetRequiredService<QuikBridge>();
        client.IsExtendedLogging = false;
        client.ConnectionStateChanged += OnBridgeConnectionStateChanged;
        using var cts = new CancellationTokenSource();

        const string classCode = "SPBFUT";
        const string secCode = "SiH5";
        const string paramName = "LAST";
        const string firmId = "YOUR_FIRM_ID";
        const string clientCode = "YOUR_CLIENT_CODE";
        const string moneyTag = "YOUR_MONEY_TAG";
        const string accountPositionAccount = "YOUR_ACCOUNT_ID";
        const string futuresAccount = "YOUR_FUTURES_ACCOUNT";
        const string moneyCurrencyCode = "SUR";
        
        client.RegisterDataSourceCallback((msg) =>
        {
            Log.Information("DataSource message {body}", msg.body?.ToString());
        });

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        
        var globalEventAggregator = serviceProvider.GetRequiredService<QuikBridgeNetEvents.QuikBridgeEventAggregator>();

        RegisterMarketDataHandlers(globalEventAggregator, client, enableVerboseParameterLogging: !liveLoadTestSettings.Enabled);
        RegisterAccountStateHandlers(globalEventAggregator);

        await client.StartAsync(cts.Token);

        if (liveLoadTestSettings.Enabled)
        {
            await RunLiveOptionBoardLoadTest(client, globalEventAggregator, liveLoadTestSettings, cts.Token);
        }
        else
        {
            var subscriptionToken = await RunMarketDataExample(client, classCode, secCode, paramName);
            await RunAccountStateExample(client, firmId, clientCode, moneyTag, accountPositionAccount, futuresAccount, secCode, moneyCurrencyCode);

            Console.WriteLine("Press any key to stop...");
            Console.ReadKey();
            await client.UnsubscribeToQuotesTableParams(classCode, secCode, paramName, subscriptionToken);
        }

        client.Finish();
        await Log.CloseAndFlushAsync();
    }

    private static void RegisterMarketDataHandlers(QuikBridgeNetEvents.QuikBridgeEventAggregator globalEventAggregator, QuikBridge client, bool enableVerboseParameterLogging)
    {
        globalEventAggregator.SubscribeToInstrumentClassesUpdate( (eventObj) =>
        {
            Log.Information("{DataType} arrived: {NumClasses}", eventObj.InstrumentClassType.GetDescription(), eventObj.InstrumentClasses.Count);
            return Task.CompletedTask;
        });
        
        globalEventAggregator.SubscribeToAllTrades( eventObj =>
        {
            if (eventObj.Trade != null)
            {
                Log.Information("Trade arrived: {Security} {Qty} x {Price}", eventObj.Trade.sec_code,
                    eventObj.Trade.qty, eventObj.Trade.price);
            }

            return Task.CompletedTask;
        });
        
        globalEventAggregator.SubscribeToSecurityInfo( eventObj =>
        {
            if (eventObj.Contract != null)
            {
                Log.Information("Security contract arrived: {Security} {ClassCode} with lot size {LotSize}",
                    eventObj.Contract.code, eventObj.Contract.class_code, eventObj.Contract.lot_size);
            }

            return Task.CompletedTask;
        });

        globalEventAggregator.SubscribeToInstrumentParameterUpdate( eventArgs =>
        {
            if (enableVerboseParameterLogging)
            {
                Log.Information("Instrument parameter {Name} current value {Val} ", eventArgs.ParamName, eventArgs.ParamValue);
            }

            return Task.CompletedTask;
        });

        globalEventAggregator.SubscribeToOrderBookUpdate( eventArgs =>
        {
            Log.Information("Order book: bids number {Bids}; asks number {Offers} ", eventArgs.OrderBook?.bid_count, eventArgs.OrderBook?.offer_count);
            return Task.CompletedTask;
        });

        globalEventAggregator.SubscribeToServiceMessages( eventObj =>
        {
            Log.Information("Arrived message with type {MsgType}", eventObj.BridgeMessage?.MessageType.GetDescription());
            return Task.CompletedTask;
        });

        globalEventAggregator.SubscribeToDataSourceSet( async eventObj =>
        {
            if (eventObj.BridgeMessage != null)
            {
                Log.Information("Datasource is set for instrument {Ticker} with time frame {Interval}",
                    eventObj.BridgeMessage.Ticker, eventObj.BridgeMessage.Interval);
            }

            await client.GetBar(eventObj.DataSourceName, MessageType.Close, 1);
        });
    }

    private static void RegisterAccountStateHandlers(QuikBridgeNetEvents.QuikBridgeEventAggregator globalEventAggregator)
    {
        globalEventAggregator.SubscribeToAccountPositions(eventArgs =>
        {
            if (eventArgs.Position != null)
            {
                Log.Information("Бумажная позиция: {Security} остаток {Balance} средняя цена {AvgPrice}",
                    eventArgs.Position.sec_code,
                    eventArgs.Position.currentbal,
                    eventArgs.Position.wa_position_price);
            }

            return Task.CompletedTask;
        });

        globalEventAggregator.SubscribeToMoneyPositions(eventArgs =>
        {
            if (eventArgs.Position != null)
            {
                Log.Information("Денежная позиция: {Currency} остаток {Balance} заблокировано {Locked}",
                    eventArgs.Position.currcode,
                    eventArgs.Position.currentbal,
                    eventArgs.Position.locked);
            }

            return Task.CompletedTask;
        });

        globalEventAggregator.SubscribeToFuturesHoldings(eventArgs =>
        {
            if (eventArgs.Holding != null)
            {
                Log.Information("Срочная позиция: {Security} net {Net} VM {VarMargin}",
                    eventArgs.Holding.sec_code,
                    eventArgs.Holding.totalnet,
                    eventArgs.Holding.varmargin);
            }

            return Task.CompletedTask;
        });

        globalEventAggregator.SubscribeToFuturesLimits(eventArgs =>
        {
            if (eventArgs.Limit != null)
            {
                Log.Information("Срочный лимит: {Currency} limit {Limit} used {Used}",
                    eventArgs.Limit.currcode,
                    eventArgs.Limit.cbplimit,
                    eventArgs.Limit.cbplused);
            }

            return Task.CompletedTask;
        });
    }

    private static async Task<Guid> RunMarketDataExample(QuikBridge client, string classCode, string secCode, string paramName)
    {
        // Минимальный пример: подписка на один параметр таблицы котировок с выводом обновлений в лог.
        Thread.Sleep(5000);
        return await client.SubscribeToQuotesTableParams(classCode, secCode, paramName);
    }

    private static async Task RunAccountStateExample(
        QuikBridge client,
        string firmId,
        string clientCode,
        string moneyTag,
        string accountPositionAccount,
        string futuresAccount,
        string secCode,
        string moneyCurrencyCode)
    {
        // Примеры разовых запросов состояния счёта.
        // Для реального запуска замените значения констант YOUR_* на идентификаторы из вашего QUIK.
        if (!firmId.StartsWith("YOUR_") && !clientCode.StartsWith("YOUR_") && !accountPositionAccount.StartsWith("YOUR_"))
        {
            await client.GetAccountPosition(firmId, clientCode, secCode, accountPositionAccount, 0);
        }

        if (!firmId.StartsWith("YOUR_") && !clientCode.StartsWith("YOUR_") && !moneyTag.StartsWith("YOUR_"))
        {
            await client.GetMoneyPosition(firmId, clientCode, moneyTag, moneyCurrencyCode, 0);
        }

        if (!firmId.StartsWith("YOUR_") && !futuresAccount.StartsWith("YOUR_"))
        {
            await client.GetFuturesHolding(firmId, futuresAccount, secCode, 0);
            await client.GetFuturesLimit(firmId, futuresAccount, 0, moneyCurrencyCode);
        }
    }

    private static async Task RunLiveOptionBoardLoadTest(
        QuikBridge client,
        QuikBridgeEventAggregator eventAggregator,
        LiveOptionBoardLoadTestSettings settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.OptionClassCode) || string.IsNullOrWhiteSpace(settings.BaseAssetSecCode))
        {
            Log.Warning("LiveOptionBoardLoadTest включён, но не заданы OptionClassCode/BaseAssetSecCode. Сценарий пропущен.");
            return;
        }

        var paramNames = settings.ParamNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (paramNames.Length == 0)
        {
            Log.Warning("LiveOptionBoardLoadTest включён без ParamNames. Сценарий пропущен.");
            return;
        }

        if (settings.StartupDelayMs > 0)
        {
            await Task.Delay(settings.StartupDelayMs, cancellationToken);
        }

        var optionSecCodes = await LoadClassSecuritiesAsync(client, eventAggregator, settings, cancellationToken);
        if (optionSecCodes.Count == 0)
        {
            Log.Warning("Не удалось получить список инструментов для класса {OptionClassCode}", settings.OptionClassCode);
            return;
        }

        var contracts = await LoadSecurityContractsAsync(client, eventAggregator, settings, optionSecCodes, cancellationToken);
        var filteredContracts = FilterOptionContracts(contracts, settings);
        if (filteredContracts.Count == 0)
        {
            Log.Warning(
                "Не найдено опционов для базового актива {BaseAssetSecCode} и серии {ExpirationDate} в классе {OptionClassCode}",
                settings.BaseAssetSecCode,
                settings.ExpirationDate,
                settings.OptionClassCode);
            return;
        }

        Log.Information(
            "Старт нагрузки: найдено {ContractsCount} инструментов, параметров на инструмент {ParamCount}, итоговых подписок {SubscriptionCount}",
            filteredContracts.Count,
            paramNames.Length,
            filteredContracts.Count * paramNames.Length);

        var selectedSecCodes = filteredContracts
            .Select(contract => contract.sec_code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var totalUpdates = 0L;
        var updatesByParam = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var updatedSecurities = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        using var eventSubscription = eventAggregator.Subscribe<InstrumentParametersUpdateEvent>(args =>
        {
            if (string.IsNullOrWhiteSpace(args.SecCode) || string.IsNullOrWhiteSpace(args.ParamName))
            {
                return Task.CompletedTask;
            }

            if (!selectedSecCodes.Contains(args.SecCode) || !paramNames.Contains(args.ParamName, StringComparer.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            Interlocked.Increment(ref totalUpdates);
            updatesByParam.AddOrUpdate(args.ParamName, 1, (_, current) => current + 1);
            updatedSecurities.TryAdd(args.SecCode, 0);
            return Task.CompletedTask;
        });

        using var monitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var monitorTask = Task.Run(
            () => MonitorLiveLoadAsync(settings, totalUpdates: () => Interlocked.Read(ref totalUpdates), updatesByParam, updatedSecurities, selectedSecCodes.Count, monitorCancellation.Token),
            monitorCancellation.Token);

        var subscriptions = await SubscribeToBoardParamsAsync(client, settings, filteredContracts, paramNames, cancellationToken);

        Console.WriteLine("Live option-board load test is running. Press any key to stop...");
        Console.ReadKey();

        monitorCancellation.Cancel();
        try
        {
            await monitorTask;
        }
        catch (OperationCanceledException)
        {
        }

        await UnsubscribeBoardParamsAsync(client, settings, subscriptions, cancellationToken);

        Log.Information(
            "Live load test finished: totalUpdates={TotalUpdates}, updatedSecurities={UpdatedSecurities}/{TotalSecurities}",
            Interlocked.Read(ref totalUpdates),
            updatedSecurities.Count,
            selectedSecCodes.Count);
    }

    private static async Task<IReadOnlyCollection<string>> LoadClassSecuritiesAsync(
        QuikBridge client,
        QuikBridgeEventAggregator eventAggregator,
        LiveOptionBoardLoadTestSettings settings,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<IReadOnlyCollection<string>>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = eventAggregator.Subscribe<InstrumentClassesUpdateEvent>(args =>
        {
            if (args.InstrumentClassType == QuikDataType.SecCode)
            {
                completion.TrySetResult(args.InstrumentClasses.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            }

            return Task.CompletedTask;
        });

        await client.GetClassSecurities(settings.OptionClassCode);

        try
        {
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(settings.DiscoveryTimeoutSeconds), cancellationToken);
        }
        catch (TimeoutException)
        {
            Log.Warning("Не дождались списка инструментов для класса {OptionClassCode} за {TimeoutSeconds} сек.", settings.OptionClassCode, settings.DiscoveryTimeoutSeconds);
            return [];
        }
    }

    private static async Task<IReadOnlyCollection<SecurityContract>> LoadSecurityContractsAsync(
        QuikBridge client,
        QuikBridgeEventAggregator eventAggregator,
        LiveOptionBoardLoadTestSettings settings,
        IReadOnlyCollection<string> securityCodes,
        CancellationToken cancellationToken)
    {
        var expectedCodes = securityCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var contracts = new ConcurrentDictionary<string, SecurityContract>(StringComparer.OrdinalIgnoreCase);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = eventAggregator.Subscribe<SecurityContractArrivedEvent>(args =>
        {
            var contract = args.Contract;
            if (contract == null || !expectedCodes.Contains(contract.sec_code))
            {
                return Task.CompletedTask;
            }

            contracts.TryAdd(contract.sec_code, contract);
            if (contracts.Count >= expectedCodes.Count)
            {
                completion.TrySetResult();
            }

            return Task.CompletedTask;
        });

        foreach (var securityCode in securityCodes)
        {
            await client.GetSecurityInfo(settings.OptionClassCode, securityCode);
        }

        try
        {
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(settings.DiscoveryTimeoutSeconds), cancellationToken);
        }
        catch (TimeoutException)
        {
            Log.Warning(
                "Не дождались всех security info за {TimeoutSeconds} сек. Получено {ReceivedCount} из {ExpectedCount}.",
                settings.DiscoveryTimeoutSeconds,
                contracts.Count,
                expectedCodes.Count);
        }

        return contracts.Values.ToArray();
    }

    private static IReadOnlyCollection<SecurityContract> FilterOptionContracts(
        IReadOnlyCollection<SecurityContract> contracts,
        LiveOptionBoardLoadTestSettings settings)
    {
        IEnumerable<SecurityContract> filtered = contracts
            .Where(contract => string.Equals(contract.class_code, settings.OptionClassCode, StringComparison.OrdinalIgnoreCase))
            .Where(contract => string.Equals(contract.base_active_seccode, settings.BaseAssetSecCode, StringComparison.OrdinalIgnoreCase))
            .Where(contract => string.IsNullOrWhiteSpace(settings.BaseAssetClassCode) || string.Equals(contract.base_active_classcode, settings.BaseAssetClassCode, StringComparison.OrdinalIgnoreCase))
            .Where(contract => settings.ExpirationDate <= 0 || contract.exp_date == settings.ExpirationDate)
            .OrderBy(contract => contract.option_strike)
            .ThenBy(contract => contract.sec_code, StringComparer.OrdinalIgnoreCase);

        if (settings.MaxInstruments > 0)
        {
            filtered = filtered.Take(settings.MaxInstruments);
        }

        return filtered.ToArray();
    }

    private static async Task<IReadOnlyCollection<QuoteParamSubscription>> SubscribeToBoardParamsAsync(
        QuikBridge client,
        LiveOptionBoardLoadTestSettings settings,
        IReadOnlyCollection<SecurityContract> contracts,
        IReadOnlyCollection<string> paramNames,
        CancellationToken cancellationToken)
    {
        var subscriptions = new ConcurrentBag<QuoteParamSubscription>();
        var failures = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            contracts,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Max(1, settings.SubscriptionParallelism)
            },
            async (contract, ct) =>
            {
                foreach (var paramName in paramNames)
                {
                    try
                    {
                        var token = await client.SubscribeToQuotesTableParams(settings.OptionClassCode, contract.sec_code, paramName);
                        subscriptions.Add(new QuoteParamSubscription(settings.OptionClassCode, contract.sec_code, paramName, token));
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{contract.sec_code}:{paramName}:{ex.Message}");
                    }
                }
            });

        if (!failures.IsEmpty)
        {
            Log.Warning("Во время подписки на доску опционов возникли ошибки: {FailureCount}", failures.Count);
            foreach (var failure in failures.Take(10))
            {
                Log.Warning("Subscribe failure: {Failure}", failure);
            }
        }

        Log.Information("Оформлено локальных подписок: {SubscriptionCount}", subscriptions.Count);
        return subscriptions.ToArray();
    }

    private static async Task UnsubscribeBoardParamsAsync(
        QuikBridge client,
        LiveOptionBoardLoadTestSettings settings,
        IReadOnlyCollection<QuoteParamSubscription> subscriptions,
        CancellationToken cancellationToken)
    {
        await Parallel.ForEachAsync(
            subscriptions,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Max(1, settings.UnsubscribeParallelism)
            },
            async (subscription, ct) =>
            {
                try
                {
                    await client.UnsubscribeToQuotesTableParams(subscription.ClassCode, subscription.SecCode, subscription.ParamName, subscription.Token);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Не удалось снять подписку {ClassCode}:{SecCode}:{ParamName}", subscription.ClassCode, subscription.SecCode, subscription.ParamName);
                }
            });
    }

    private static async Task MonitorLiveLoadAsync(
        LiveOptionBoardLoadTestSettings settings,
        Func<long> totalUpdates,
        ConcurrentDictionary<string, long> updatesByParam,
        ConcurrentDictionary<string, byte> updatedSecurities,
        int totalSecurities,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, settings.StatusIntervalSeconds)));

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var paramStats = string.Join(", ", updatesByParam.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key}={entry.Value}"));
            Log.Information(
                "Load status: totalUpdates={TotalUpdates}, updatedSecurities={UpdatedSecurities}/{TotalSecurities}, perParam=[{PerParam}]",
                totalUpdates(),
                updatedSecurities.Count,
                totalSecurities,
                paramStats);
        }
    }

    static void OnBridgeConnectionStateChanged(QuikBridgeConnectionState newState)
    {
        Log.Information($"Изменилось состояние подключения моста на {newState.GetDescription()}");
    }

    private readonly record struct QuoteParamSubscription(string ClassCode, string SecCode, string ParamName, Guid Token);
}
