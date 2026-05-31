using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuikBridgeNet;
using QuikBridgeNet.Helpers;
using QuikBridgeNetDomain.Entities;
using Serilog;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false, true);
        var configuration = builder.Build();

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

        RegisterMarketDataHandlers(globalEventAggregator, client);
        RegisterAccountStateHandlers(globalEventAggregator);

        await client.StartAsync(cts.Token);

        var subscriptionToken = await RunMarketDataExample(client, classCode, secCode, paramName);
        await RunAccountStateExample(client, firmId, clientCode, moneyTag, accountPositionAccount, futuresAccount, secCode, moneyCurrencyCode);

        Console.WriteLine("Press any key to stop...");
        Console.ReadKey();
        await client.UnsubscribeToQuotesTableParams(classCode, secCode, paramName, subscriptionToken);
        client.Finish();
        await Log.CloseAndFlushAsync();
    }

    private static void RegisterMarketDataHandlers(QuikBridgeNetEvents.QuikBridgeEventAggregator globalEventAggregator, QuikBridge client)
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
            Log.Information("Instrument parameter {Name} current value {Val} ", eventArgs.ParamName, eventArgs.ParamValue);
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

    static void OnBridgeConnectionStateChanged(QuikBridgeConnectionState newState)
    {
        Log.Information($"Изменилось состояние подключения моста на {newState.GetDescription()}");
    }
}
