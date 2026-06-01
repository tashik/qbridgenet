# Библиотека QuikBridgeNet

QuikBridgeNet - это .NET-клиент для работы с [QuikQtBridge](https://github.com/tashik/QuikQtBridge), который даёт доступ к QLUA-функциям терминала QUIK через сокетный мост.

Библиотека решает четыре прикладные задачи:

- поднимает и поддерживает соединение с QuikQtBridge;
- отправляет команды и запросы в QLUA;
- переводит ответы и callback-сообщения в typed events;
- управляет подписками и shared-ресурсами без дублей и преждевременных отписок.

## Состав решения

- `QuikBridgeNet` - основной клиентский API, транспорт, обработчики входящих сообщений, менеджеры подписок и datasource.
- `QuikBridgeNetDomain` - конфигурация и domain-объекты QUIK.
- `QuikBridgeNetEvents` - event aggregator и типы событий, на которые подписывается приложение.
- `QuikBridgeNetApp` - минимальный пример использования.

## Быстрый старт

1. Подготовьте QuikQtBridge по инструкции из его репозитория.
2. Скопируйте файлы моста в окружение QUIK, где доступен `info.exe`.
3. Проверьте настройки хоста, порта и пути до DLL на стороне моста.
4. Запустите `QuikQtBridge.lua` внутри QUIK.
5. В .NET-приложении зарегистрируйте сервисы QuikBridge и настройте секцию `Bridge`.

Пример конфигурации:

```json
{
  "Bridge": {
    "Host": "127.0.0.1",
    "Port": 57777,
    "UseExtendedLogging": false,
    "UseExtendedEventLogging": false,
    "EventQueueCapacity": 10000,
    "EventHandlerQueueCapacity": 1000,
    "DataSourceQueueCapacity": 10000,
    "RequestTimeoutMs": 0,
    "RequestExpirySweepIntervalMs": 1000,
    "EventQueueWarningThreshold": 0,
    "EventHandlerBacklogWarningThreshold": 0
  }
}
```

`EventQueueCapacity`, `EventHandlerQueueCapacity` и `DataSourceQueueCapacity` ограничивают рост внутренних очередей под нагрузкой.

`RequestTimeoutMs` задаёт TTL для ожидающих ответов запросов. Значение `0` оставляет expiry выключенным. Если timeout включён, библиотека периодически удаляет зависшие request metadata, пишет warning в лог и публикует `RequestExpiredEvent`. Частота проверки задаётся через `RequestExpirySweepIntervalMs`.

`EventQueueWarningThreshold` и `EventHandlerBacklogWarningThreshold` по умолчанию выключены. Если задать значение больше `0`, библиотека начнёт писать warning при росте backlog-а в event pipeline.

## Минимальный пример

```csharp
var services = new ServiceCollection();
QuikBridgeServiceConfiguration.ConfigureServices(services, configuration);

using var serviceProvider = services.BuildServiceProvider();

var client = serviceProvider.GetRequiredService<QuikBridge>();
var events = serviceProvider.GetRequiredService<QuikBridgeNetEvents.QuikBridgeEventAggregator>();

events.SubscribeToInstrumentParameterUpdate(args =>
{
    Console.WriteLine($"{args.ClassCode}:{args.SecCode} {args.ParamName} = {args.ParamValue}");
    return Task.CompletedTask;
});

await client.StartAsync(CancellationToken.None);

var token = await client.SubscribeToQuotesTableParams("SPBFUT", "SiH5", "LAST");

Console.ReadKey();

await client.UnsubscribeToQuotesTableParams("SPBFUT", "SiH5", "LAST", token);
client.Finish();
```

Рабочий пример приложения находится в [QuikBridgeNetApp/Program.cs](QuikBridgeNetApp/Program.cs).

## Нагрузочное тестирование через QuikBridgeNetApp

`QuikBridgeNetApp` можно использовать как простой live-runner для нагрузочного прогона подписок на параметры по опционной доске.

Сценарий работает так:

1. вызывает `GetClassSecurities(optionClassCode)` и получает весь список инструментов класса;
2. делает prefilter по префиксу `BaseAssetSecCode`, чтобы не запрашивать `SecurityInfo` по чужим базовым активам;
3. запрашивает `GetSecurityInfo(classCode, secCode)` только по отфильтрованным кодам;
4. выбирает нужную серию по `exp_date`;
5. подписывается на параметры из `ParamNames` по всем отобранным инструментам;
6. пишет в лог статус потока обновлений и при долгой тишине отправляет probe через `GetQuotesTableParam(...)`.

Для включения режима задайте секцию `LiveOptionBoardLoadTest` в `QuikBridgeNetApp/appsettings.json`:

```json
{
  "LiveOptionBoardLoadTest": {
    "Enabled": true,
    "OptionClassCode": "SPBOPT",
    "BaseAssetClassCode": "SPBFUT",
    "BaseAssetSecCode": "Si",
    "ExpirationDate": 0,
    "ParamNames": [ "LAST", "BID", "OFFER" ],
    "StartupDelayMs": 2000,
    "DiscoveryTimeoutSeconds": 60,
    "StatusIntervalSeconds": 30,
    "NoUpdatesWarningThresholdSeconds": 120,
    "ProbeIntervalSeconds": 180,
    "SubscriptionParallelism": 32,
    "UnsubscribeParallelism": 32,
    "MaxInstruments": 0
  }
}
```

Практический смысл параметров:

- `OptionClassCode` - код класса опционов, например `SPBOPT`;
- `BaseAssetClassCode` и `BaseAssetSecCode` - фильтр на базовый актив, например `SPBFUT` и `Si`;
- `ExpirationDate = 0` - взять ближайший четверг как целевую дату серии; если такой серии нет, приложение выберет ближайшую доступную из реально загруженных контрактов;
- `ParamNames` - список параметров, на которые нужно подписаться по всей доске;
- `SubscriptionParallelism` - степень параллелизма при массовом оформлении подписок;
- `StatusIntervalSeconds` - как часто печатать статус в лог;
- `NoUpdatesWarningThresholdSeconds` и `ProbeIntervalSeconds` - пороги диагностики, если поток `paramChange` надолго затих.

Что смотреть в логах во время прогона:

- `Live load: getClassSecurities sent ...` - запрос списка инструментов отправлен;
- `Live load: prefiltered security codes by prefix ...` - сколько кодов осталось после отрезания чужих базовых активов;
- `Live load: getSecurityInfo sent for ... instruments` - сколько карточек инструментов реально запрашивается;
- `Live load: filtered option board to ... instruments ...` - сколько контрактов попало в итоговую серию;
- `Load status: ...` - периодический health snapshot по потоку обновлений;
- `Live load: probe sent ...` - активная проверка, если обычные `paramChange` долго не приходят.

Важно: этот сценарий не доказывает торговую корректность стратегии и не гарантирует, что рынок будет слать `paramChange` непрерывно. Его задача - проверить, выдерживает ли библиотека массовое оформление подписок, продолжает ли принимать обновления и не залипает ли event pipeline без явных ошибок.

## Как устроена библиотека

Поток данных выглядит так:

1. `QuikBridge` формирует запрос и отправляет его через `QuikBridgeProtocolHandler`.
2. `MessageRegistry` сохраняет метаданные исходящего сообщения, чтобы потом корректно сопоставить ответ с контекстом запроса.
3. `QuikBridgeProtocolHandler` принимает `req` и `ans` сообщения от QuikQtBridge.
4. `ReqArrivedEventHandler` и `RespArrivedEventHandler` переводят сырые JSON-сообщения в доменные события.
5. `QuikBridgeEventAggregator` публикует typed events для приложения.

Дополнительные защитные слои:

- `QuikBridgeSubscriptionManager` не допускает повторных удалённых подписок на один и тот же инструмент/параметр, делает реальную отписку только после ухода последнего локального подписчика и умеет восстановить remote subscribe после реконнекта.
- `QuikBridgeDatasourceManager` ведёт ref-count для datasource, не закрывает общий datasource раньше времени и умеет переоткрыть datasource после реконнекта.
- `QuikBridgeCallbackRegistry` не дублирует регистрацию глобальных callback-ов и повторно регистрирует их на новой socket session.
- `QuikBridgeEventAggregator` сериализует выполнение на одного handler-а, но не даёт одному медленному handler-у остановить весь поток событий данного типа.

Если при replay после реконнекта какая-то часть session state не восстановилась, библиотека не обрывает весь запуск клиента из-за первой такой ошибки. Вместо этого она пишет warning и публикует `SessionStateRestoreFailedEvent`, чтобы приложение могло решить, нужен ли повтор восстановления, деградация функциональности или аварийный stop.

## Публичный API QuikBridge

Ниже перечислены основные методы верхнего уровня.

### Управление жизненным циклом

- `StartAsync(CancellationToken)` - открывает соединение и регистрирует встроенные callback-и `OnAllTrade`, `OnOrder`, `OnTransReply`.
- `Finish()` - останавливает клиент, завершает обработку событий и закрывает соединение.
- `FinishAsync()` - асинхронный вариант остановки клиента с ожиданием завершения внутренних background loop-ов.
- `RestoreSessionStateAsync()` - вручную повторяет регистрацию session-scoped remote state для текущего соединения, если нужен явный повторный replay.
- `IsExtendedLogging` - включает расширенное логирование транспортного уровня.
- `ConnectionState` и событие `ConnectionStateChanged` - текущее состояние соединения.

После разрыва соединения или `FinishAsync()` библиотека сбрасывает только volatile remote state: pending requests и удалённые session objects текущей socket session. Локальное намерение при этом сохраняется. Поэтому новый `StartAsync()` на том же экземпляре `QuikBridge` автоматически повторно регистрирует callback-и, восстанавливает активные market subscriptions и переоткрывает datasource, если у них оставались локальные потребители.

### Информационные запросы

- `GetClassesList()` - запрос списка классов инструментов.
- `GetClassSecurities(string classCode)` - запрос списка инструментов для класса.
- `GetSecurityInfo(string classCode, string secCode)` - запрос описания инструмента.
- `GetQuotesTableParam(string classCode, string secCode, string paramName)` - разовый запрос параметра без подписки.
- `GetAccountPosition(string firmId, string clientCode, string secCode, string account, int limitKind)` - запрос бумажной позиции по счёту.
- `GetMoneyPosition(string firmId, string clientCode, string tag, string currencyCode, int limitKind)` - запрос денежной позиции.
- `GetFuturesHolding(string firmId, string account, string secCode, int positionType)` - запрос текущей срочной позиции по инструменту.
- `GetFuturesLimit(string firmId, string account, int limitType, string currencyCode)` - запрос состояния срочного лимита.

Эти методы возвращают `int` - внутренний `messageId` отправленного запроса. Полезная нагрузка приходит позже через события.

Практически это значит следующее:

- `GetAccountPosition` оборачивает вызов QLua `getDepoEx`.
- `GetMoneyPosition` оборачивает вызов QLua `getMoneyEx`.
- `GetFuturesHolding` оборачивает вызов QLua `getFuturesHolding`.
- `GetFuturesLimit` оборачивает вызов QLua `getFuturesLimit`.

### Datasource

- `CreateDs(string classCode, string secCode, string interval)` - создаёт datasource или увеличивает ссылку на уже существующий shared datasource.
- `GetBar(string dataSourceName, MessageType barFunc, int barIndex)` - запрашивает значение бара из уже созданного datasource.
- `CloseDs(string dataSourceName)` - уменьшает ссылку на datasource и закрывает его только при уходе последнего потребителя.
- `RegisterDataSourceCallback(DatasourceCallbackReceived callback)` - регистрирует callback обновлений datasource на стороне клиента.

Формат имени datasource: `TICKER[INTERVAL]`, например `SBER[60]`.

### Подписки

- `SubscribeToOrderBook(string classCode, string secCode)` - подписка на стакан. Возвращает `Guid subscriptionToken`.
- `UnsubscribeToOrderBook(string classCode, string secCode, Guid subscriptionToken)` - снимает один локальный токен подписки.
- `SubscribeToQuotesTableParams(string classCode, string secCode, string paramName)` - подписка на параметр таблицы котировок. Возвращает `Guid subscriptionToken`.
- `UnsubscribeToQuotesTableParams(string classCode, string secCode, string paramName, Guid subscriptionToken)` - снимает один локальный токен подписки.

Важно:

- повторные локальные подписки на один и тот же ключ не порождают повторный удалённый subscribe;
- удалённый unsubscribe уходит только когда снят последний локальный `subscriptionToken`;
- `Guid` нужно сохранять на стороне вызывающего кода, иначе корректно снять конкретную подписку не получится.

### Торговые действия и callback-и

- `SendTransaction(TransactionBase transaction)` - отправляет транзакцию в QUIK.
- `SetGlobalCallback(MessageType name)` - вручную регистрирует callback QUIK, если нужен отдельный тип callback-а сверх стандартно включённых.

## События QuikBridgeEventAggregator

Приложение подписывается на события через `QuikBridgeEventAggregator`.

Есть два режима подписки:

- typed-методы `SubscribeTo...` подходят для долгоживущих обработчиков на весь срок жизни приложения;
- generic-метод `Subscribe<TEvent>(Func<TEvent, Task>)` возвращает `IDisposable`, который можно вызвать, если нужен явный unsubscribe от event handler-а.

Доступные методы подписки:

- `SubscribeToInstrumentClassesUpdate`
- `SubscribeToInstrumentParameterUpdate`
- `SubscribeToOrderBookUpdate`
- `SubscribeToServiceMessages`
- `SubscribeToDataSourceSet`
- `SubscribeToAllTrades`
- `SubscribeToOrders`
- `SubscribeToTransactionReplies`
- `SubscribeToSecurityInfo`
- `SubscribeToAccountPositions`
- `SubscribeToMoneyPositions`
- `SubscribeToFuturesHoldings`
- `SubscribeToFuturesLimits`
- `SubscribeToRequestExpired`
- `SubscribeToSessionStateRestoreFailed`

Пример явной отписки от handler-а:

```csharp
using var priceSubscription = events.Subscribe<InstrumentParametersUpdateEvent>(args =>
{
  Console.WriteLine($"{args.ClassCode}:{args.SecCode} {args.ParamName} = {args.ParamValue}");
  return Task.CompletedTask;
});

// ... работа с событиями

priceSubscription.Dispose();
```

Практически это нужно, когда набор обработчиков меняется во время работы приложения, например при динамическом создании и удалении UI-экранов, стратегий или временных сервисов.

Практически это означает следующее:

- ответы на разовые запросы превращаются в typed events, если для них есть явная маршрутизация;
- callback-и QUIK `OnAllTrade`, `OnOrder`, `OnTransReply` поднимаются как отдельные события;
- запросы позиций и лимитов по счетам тоже приходят не как return value метода, а как отдельные typed events;
- истечение TTL у зависшего request поднимается как `RequestExpiredEvent`;
- сбой отдельного шага replay после реконнекта поднимается как `SessionStateRestoreFailedEvent`;
- служебные или неразобранные сообщения попадают в `ServiceMessageArrivedEvent`.

### События по позициям и лимитам

- `AccountPositionArrivedEvent` - бумажная позиция из `getDepoEx`.
- `MoneyPositionArrivedEvent` - денежная позиция из `getMoneyEx`.
- `FuturesHoldingArrivedEvent` - срочная позиция из `getFuturesHolding`.
- `FuturesLimitArrivedEvent` - срочный лимит из `getFuturesLimit`.

Для всех четырёх случаев паттерн одинаковый: сначала вызывается request method у `QuikBridge`, затем `RespArrivedEventHandler` поднимает typed event через `QuikBridgeEventAggregator`.

## Карта контрактов

| Метод `QuikBridge` | Вызов QLua | Событие ответа |
| --- | --- | --- |
| `GetClassesList()` | `getClassesList` | `InstrumentClassesUpdateEvent` |
| `GetClassSecurities(classCode)` | `getClassSecurities` | `InstrumentClassesUpdateEvent` |
| `GetSecurityInfo(classCode, secCode)` | `getSecurityInfo` | `SecurityContractArrivedEvent` |
| `GetQuotesTableParam(classCode, secCode, paramName)` | `getParamEx2` | `InstrumentParametersUpdateEvent` |
| `GetAccountPosition(firmId, clientCode, secCode, account, limitKind)` | `getDepoEx` | `AccountPositionArrivedEvent` |
| `GetMoneyPosition(firmId, clientCode, tag, currencyCode, limitKind)` | `getMoneyEx` | `MoneyPositionArrivedEvent` |
| `GetFuturesHolding(firmId, account, secCode, positionType)` | `getFuturesHolding` | `FuturesHoldingArrivedEvent` |
| `GetFuturesLimit(firmId, account, limitType, currencyCode)` | `getFuturesLimit` | `FuturesLimitArrivedEvent` |
| `SubscribeToQuotesTableParams(classCode, secCode, paramName)` | `subscribeParamChanges` | `InstrumentParametersUpdateEvent` |
| `SubscribeToOrderBook(classCode, secCode)` | `subscribeQuotes` | `OrderBookUpdateEvent` |
| `SendTransaction(transaction)` | `sendTransaction` | `ServiceMessageArrivedEvent` или callback `TransactionReplyArrivedEvent` |
| встроенный callback `OnAllTrade` | `OnAllTrade` | `AllTradeArrivedEvent` |
| встроенный callback `OnOrder` | `OnOrder` | `OrderArrivedEvent` |
| встроенный callback `OnTransReply` | `OnTransReply` | `TransactionReplyArrivedEvent` |

Если для метода не существует отдельного typed event, ответ уходит в `ServiceMessageArrivedEvent`.

## Что возвращается сразу, а что приходит позже

У `QuikBridge` есть два принципиально разных типа вызовов.

Сразу возвращают результат выполнения локально:

- `StartAsync(...)` возвращает `Task`, то есть завершение попытки установить соединение;
- `Finish()` завершает клиент локально;
- `FinishAsync()` завершает клиент асинхронно и дожидается остановки внутренних loop-ов;
- `GetEventProcessingMetrics()` и `GetEventProcessingMetrics<TEvent>()` возвращают готовый snapshot внутреннего состояния event pipeline.

Не возвращают полезную нагрузку сразу, а только подтверждают отправку запроса:

- `GetClassesList()`
- `GetClassSecurities(...)`
- `GetSecurityInfo(...)`
- `GetQuotesTableParam(...)`
- `GetAccountPosition(...)`
- `GetMoneyPosition(...)`
- `GetFuturesHolding(...)`
- `GetFuturesLimit(...)`
- `CreateDs(...)`
- `GetBar(...)`
- `CloseDs(...)`
- `SendTransaction(...)`
- `SetGlobalCallback(...)`

Для этих методов возвращаемое значение `int` - это `messageId`, а не сами данные из QUIK. Данные или статус операции приходят позже через `QuikBridgeEventAggregator`.

Отдельная группа - подписки:

- `SubscribeToOrderBook(...)`
- `SubscribeToQuotesTableParams(...)`

Они возвращают `Guid subscriptionToken`, который нужен только для управления жизненным циклом локальной подписки. Сами обновления тоже приходят позже через события.

Практическое правило:

- если метод возвращает `int`, почти всегда это идентификатор отправленного сообщения;
- если нужен результат запроса, его надо ловить в соответствующем typed event;
- если метод возвращает `Guid`, это токен подписки, который нужно сохранить для корректной отписки.

## Пример запросов состояния счёта

```csharp
events.SubscribeToAccountPositions(args =>
{
  Console.WriteLine($"Paper position: {args.Position?.sec_code} = {args.Position?.currentbal}");
  return Task.CompletedTask;
});

events.SubscribeToMoneyPositions(args =>
{
  Console.WriteLine($"Money position: {args.Position?.currcode} = {args.Position?.currentbal}");
  return Task.CompletedTask;
});

events.SubscribeToFuturesHoldings(args =>
{
  Console.WriteLine($"Futures holding: {args.Holding?.sec_code} totalnet = {args.Holding?.totalnet}");
  return Task.CompletedTask;
});

events.SubscribeToFuturesLimits(args =>
{
  Console.WriteLine($"Futures limit: {args.Limit?.currcode} cbplimit = {args.Limit?.cbplimit}");
  return Task.CompletedTask;
});

await client.StartAsync(CancellationToken.None);

await client.GetAccountPosition(firmId, clientCode, "SBER", account, 0);
await client.GetMoneyPosition(firmId, clientCode, tag, "SUR", 0);
await client.GetFuturesHolding(firmId, futuresAccount, "SiM6", 0);
await client.GetFuturesLimit(firmId, futuresAccount, 0, "SUR");
```

## Наблюдаемость и диагностика

Для диагностики event pipeline доступны два метода:

- `GetEventProcessingMetrics()` - снимок метрик по всем типам событий;
- `GetEventProcessingMetrics<TEvent>()` - снимок метрик по одному типу события.

Каждый snapshot содержит:

- `PendingEvents` - сколько событий ожидает чтения из канала данного типа;
- `PendingHandlerExecutions` - сколько handler execution уже поставлено в очереди, но ещё не стартовало;
- `ActiveHandlerExecutions` - сколько handler execution сейчас выполняется;
- `SubscriberCount` - сколько обработчиков подписано на этот тип события.

Эти методы полезны, если нужно понять, где именно образуется давление под нагрузкой: на входе в канал или уже в очередях конкретных обработчиков.

## Практические рекомендации

- Сначала регистрируйте обработчики событий, затем вызывайте `StartAsync`.
- Для длительных обработчиков событий лучше делать быстрый capture данных и передавать тяжёлую работу во внешний pipeline, чтобы не копить backlog.
- Храните `subscriptionToken` рядом с жизненным циклом конкретной подписки.
- Если используете shared datasource в нескольких местах, всегда закрывайте его через `CloseDs`, а не через собственную логику.
- Для разбора проблем под нагрузкой сначала включайте event-метрики и только затем расширенное логирование.
