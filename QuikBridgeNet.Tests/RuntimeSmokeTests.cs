using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using QuikBridgeNet.EventHandlers;
using QuikBridgeNet.Events;
using QuikBridgeNetEvents;
using QuikBridgeNetEvents.Events;
using QuikBridgeNetDomain;
using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNet.Tests;

public class RuntimeSmokeTests
{
    [Fact]
    public void MessageIndexer_returns_positive_ids_and_preserves_trader_suffix()
    {
        var indexer = new MessageIndexer();

        var firstId = indexer.GetIndex(17);
        var secondId = indexer.GetIndex(17);

        Assert.True(firstId > 0);
        Assert.True(secondId > firstId);
        Assert.Equal(17, indexer.GetNumberFromMsgId(firstId));
    }

    [Fact]
    public void FindEndOfJson_ignores_braces_inside_strings()
    {
        var handler = CreateProtocolHandler();
        var payload = "{\"id\":1,\"type\":\"ans\",\"data\":{\"text\":\"brace { inside } string and \\\"quoted\\\" tail\",\"result\":true}}{\"id\":2,\"type\":\"ans\"}";

        var endIndex = InvokePrivate<int>(handler, "FindEndOfJson", payload);
        var parsed = InvokePrivate<JObject?>(handler, "TryParseJson", payload);

        Assert.Equal(payload.IndexOf("}{", StringComparison.Ordinal), endIndex);
        Assert.NotNull(parsed);
        Assert.Equal(1, parsed!["id"]!.Value<int>());
    }

    [Fact]
    public async Task RespArrivedEventHandler_removes_message_from_registry_after_handling()
    {
        var registry = new MessageRegistry();
        var eventAggregator = new QuikBridgeEventAggregator(new QuikBridgeConfig());
        var responseHandler = new RespArrivedEventHandler(registry, eventAggregator, new QuikBridgeConfig());
        const int messageId = 42;

        registry.RegisterMessage(messageId, new QMessage
        {
            Id = messageId,
            MessageType = MessageType.Close,
            Method = "C"
        });

        var responseEvent = new RespArrivedEvent(new JsonMessage
        {
            id = messageId,
            type = "ans",
            body = JObject.Parse("{\"result\":[]}")
        });

        await responseHandler.HandleAsync(responseEvent);

        Assert.False(registry.TryGetMetadata(messageId, out _));
    }

    [Fact]
    public async Task SendTransaction_registers_send_transaction_metadata_before_transport_failure()
    {
        var registry = new MessageRegistry();
        var bridge = CreateBridge(registry);
        var transaction = new TransactionBase
        {
            ACCOUNT = string.Empty,
            CLIENT_CODE = string.Empty,
            TYPE = string.Empty,
            TRANS_ID = 1001,
            CLASSCODE = "SPBFUT",
            SECCODE = "SiM6",
            ACTION = string.Empty,
            OPERATION = string.Empty
        };

        var exception = await Assert.ThrowsAsync<Exception>(() => bridge.SendTransaction(transaction));
        var message = GetSingleRegisteredMessage(registry);

        Assert.Equal("Connection problem", exception.Message);
        Assert.Equal("sendTransaction", message.Method);
        Assert.Equal(MessageType.SendTransaction, message.MessageType);
        Assert.Equal(transaction.CLASSCODE, message.ClassCode);
        Assert.Equal(transaction.SECCODE, message.Ticker);
    }

        [Fact]
        public async Task GetAccountPosition_registers_account_position_request_metadata_before_transport_failure()
        {
                var registry = new MessageRegistry();
                var bridge = CreateBridge(registry);

                var exception = await Assert.ThrowsAsync<Exception>(() => bridge.GetAccountPosition("MC0061900000", "12345", "SBER", "L01+00000F00", 0));
                var message = GetSingleRegisteredMessage(registry);

                Assert.Equal("Connection problem", exception.Message);
                Assert.Equal("getDepoEx", message.Method);
                Assert.Equal(MessageType.AccountPosition, message.MessageType);
                Assert.Equal("MC0061900000", message.FirmId);
                Assert.Equal("12345", message.ClientCode);
                Assert.Equal("SBER", message.Ticker);
                Assert.Equal("L01+00000F00", message.Account);
                Assert.Equal(0, message.LimitKind);
        }

            [Fact]
            public async Task GetFuturesHolding_registers_futures_holding_request_metadata_before_transport_failure()
            {
                var registry = new MessageRegistry();
                var bridge = CreateBridge(registry);

                var exception = await Assert.ThrowsAsync<Exception>(() => bridge.GetFuturesHolding("MC0061900000", "SPBFUT00PST", "SiM6", 0));
                var message = GetSingleRegisteredMessage(registry);

                Assert.Equal("Connection problem", exception.Message);
                Assert.Equal("getFuturesHolding", message.Method);
                Assert.Equal(MessageType.FuturesHolding, message.MessageType);
                Assert.Equal("MC0061900000", message.FirmId);
                Assert.Equal("SPBFUT00PST", message.Account);
                Assert.Equal("SiM6", message.Ticker);
                Assert.Equal(0, message.PositionType);
            }

            [Fact]
            public async Task GetFuturesLimit_registers_futures_limit_request_metadata_before_transport_failure()
            {
                var registry = new MessageRegistry();
                var bridge = CreateBridge(registry);

                var exception = await Assert.ThrowsAsync<Exception>(() => bridge.GetFuturesLimit("MC0061900000", "SPBFUT00PST", 0, "SUR"));
                var message = GetSingleRegisteredMessage(registry);

                Assert.Equal("Connection problem", exception.Message);
                Assert.Equal("getFuturesLimit", message.Method);
                Assert.Equal(MessageType.FuturesLimit, message.MessageType);
                Assert.Equal("MC0061900000", message.FirmId);
                Assert.Equal("SPBFUT00PST", message.Account);
                Assert.Equal(0, message.LimitKind);
                Assert.Equal("SUR", message.CurrencyCode);
            }

        [Fact]
        public async Task RespArrivedEventHandler_raises_account_position_event_for_account_position_response()
        {
                var registry = new MessageRegistry();
                var eventAggregator = new QuikBridgeEventAggregator(new QuikBridgeConfig());
                var responseHandler = new RespArrivedEventHandler(registry, eventAggregator, new QuikBridgeConfig());
                var completion = new TaskCompletionSource<AccountPositionArrivedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
                const int messageId = 11;

                eventAggregator.SubscribeToAccountPositions(args =>
                {
                        completion.TrySetResult(args);
                        return Task.CompletedTask;
                });

                registry.RegisterMessage(messageId, new QMessage
                {
                        Id = messageId,
                        MessageType = MessageType.AccountPosition,
                        Method = "getDepoEx",
                        Ticker = "SBER",
                        FirmId = "MC0061900000",
                        ClientCode = "12345",
                        Account = "L01+00000F00",
                        LimitKind = 0
                });

                var responseEvent = new RespArrivedEvent(new JsonMessage
                {
                        id = messageId,
                        type = "ans",
                        body = JObject.Parse("""
                                {
                                    "result": [
                                        {
                                            "firmid": "MC0061900000",
                                            "client_code": "12345",
                                            "sec_code": "SBER",
                                            "trdaccid": "L01+00000F00",
                                            "limit_kind": 0,
                                            "currentbal": 15,
                                            "wa_position_price": 252.34
                                        }
                                    ]
                                }
                                """)
                });

                await responseHandler.HandleAsync(responseEvent);

                var accountPositionEvent = await completion.Task.WaitAsync(TimeSpan.FromSeconds(1));

                Assert.NotNull(accountPositionEvent.Position);
                Assert.Equal("SBER", accountPositionEvent.Position!.sec_code);
                Assert.Equal(15, accountPositionEvent.Position.currentbal);
                Assert.Equal(252.34, accountPositionEvent.Position.wa_position_price);
                Assert.False(registry.TryGetMetadata(messageId, out _));
                eventAggregator.Close();
        }

        [Fact]
        public async Task RespArrivedEventHandler_raises_money_position_event_for_money_position_response()
        {
                var registry = new MessageRegistry();
                var eventAggregator = new QuikBridgeEventAggregator(new QuikBridgeConfig());
                var responseHandler = new RespArrivedEventHandler(registry, eventAggregator, new QuikBridgeConfig());
                var completion = new TaskCompletionSource<MoneyPositionArrivedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
                const int messageId = 12;

                eventAggregator.SubscribeToMoneyPositions(args =>
                {
                        completion.TrySetResult(args);
                        return Task.CompletedTask;
                });

                registry.RegisterMessage(messageId, new QMessage
                {
                        Id = messageId,
                        MessageType = MessageType.MoneyPosition,
                        Method = "getMoneyEx",
                        FirmId = "MC0061900000",
                        ClientCode = "12345",
                        CurrencyCode = "SUR",
                        Tag = "EQTV",
                        LimitKind = 0
                });

                var responseEvent = new RespArrivedEvent(new JsonMessage
                {
                        id = messageId,
                        type = "ans",
                        body = JObject.Parse("""
                                {
                                    "result": [
                                        {
                                            "firmid": "MC0061900000",
                                            "client_code": "12345",
                                            "tag": "EQTV",
                                            "currcode": "SUR",
                                            "limit_kind": 0,
                                            "currentbal": 150000.50,
                                            "locked": 25000.25
                                        }
                                    ]
                                }
                                """)
                });

                await responseHandler.HandleAsync(responseEvent);

                var moneyPositionEvent = await completion.Task.WaitAsync(TimeSpan.FromSeconds(1));

                Assert.NotNull(moneyPositionEvent.Position);
                Assert.Equal("SUR", moneyPositionEvent.Position!.currcode);
                Assert.Equal("EQTV", moneyPositionEvent.Position.tag);
                Assert.Equal(150000.50, moneyPositionEvent.Position.currentbal);
                Assert.Equal(25000.25, moneyPositionEvent.Position.locked);
                Assert.False(registry.TryGetMetadata(messageId, out _));
                eventAggregator.Close();
        }

        [Fact]
        public async Task RespArrivedEventHandler_raises_futures_holding_event_for_futures_holding_response()
        {
                var registry = new MessageRegistry();
                var eventAggregator = new QuikBridgeEventAggregator(new QuikBridgeConfig());
                var responseHandler = new RespArrivedEventHandler(registry, eventAggregator, new QuikBridgeConfig());
                var completion = new TaskCompletionSource<FuturesHoldingArrivedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
                const int messageId = 13;

                eventAggregator.SubscribeToFuturesHoldings(args =>
                {
                        completion.TrySetResult(args);
                        return Task.CompletedTask;
                });

                registry.RegisterMessage(messageId, new QMessage
                {
                        Id = messageId,
                        MessageType = MessageType.FuturesHolding,
                        Method = "getFuturesHolding",
                        FirmId = "MC0061900000",
                        Account = "SPBFUT00PST",
                        Ticker = "SiM6",
                        PositionType = 0
                });

                var responseEvent = new RespArrivedEvent(new JsonMessage
                {
                        id = messageId,
                        type = "ans",
                        body = JObject.Parse("""
                                {
                                    "result": [
                                        {
                                            "firmid": "MC0061900000",
                                            "trdaccid": "SPBFUT00PST",
                                            "sec_code": "SiM6",
                                            "totalnet": 3,
                                            "openbuys": 1,
                                            "opensells": 2,
                                            "varmargin": 1200.75,
                                            "avrposnprice": 78123.45,
                                            "session_status": 0
                                        }
                                    ]
                                }
                                """)
                });

                await responseHandler.HandleAsync(responseEvent);

                var futuresHoldingEvent = await completion.Task.WaitAsync(TimeSpan.FromSeconds(1));

                Assert.NotNull(futuresHoldingEvent.Holding);
                Assert.Equal("SiM6", futuresHoldingEvent.Holding!.sec_code);
                Assert.Equal(3, futuresHoldingEvent.Holding.totalnet);
                Assert.Equal(1200.75, futuresHoldingEvent.Holding.varmargin);
                Assert.Equal(78123.45, futuresHoldingEvent.Holding.avrposnprice);
                Assert.False(registry.TryGetMetadata(messageId, out _));
                eventAggregator.Close();
        }

        [Fact]
        public async Task RespArrivedEventHandler_raises_futures_limit_event_for_futures_limit_response()
        {
                var registry = new MessageRegistry();
                var eventAggregator = new QuikBridgeEventAggregator(new QuikBridgeConfig());
                var responseHandler = new RespArrivedEventHandler(registry, eventAggregator, new QuikBridgeConfig());
                var completion = new TaskCompletionSource<FuturesLimitArrivedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
                const int messageId = 14;

                eventAggregator.SubscribeToFuturesLimits(args =>
                {
                        completion.TrySetResult(args);
                        return Task.CompletedTask;
                });

                registry.RegisterMessage(messageId, new QMessage
                {
                        Id = messageId,
                        MessageType = MessageType.FuturesLimit,
                        Method = "getFuturesLimit",
                        FirmId = "MC0061900000",
                        Account = "SPBFUT00PST",
                        LimitKind = 0,
                        CurrencyCode = "SUR"
                });

                var responseEvent = new RespArrivedEvent(new JsonMessage
                {
                        id = messageId,
                        type = "ans",
                        body = JObject.Parse("""
                                {
                                    "result": [
                                        {
                                            "firmid": "MC0061900000",
                                            "trdaccid": "SPBFUT00PST",
                                            "limit_type": 0,
                                            "cbplimit": 250000,
                                            "cbplused": 50000,
                                            "varmargin": 1500.5,
                                            "currcode": "SUR",
                                            "real_varmargin": 1450.25
                                        }
                                    ]
                                }
                                """)
                });

                await responseHandler.HandleAsync(responseEvent);

                var futuresLimitEvent = await completion.Task.WaitAsync(TimeSpan.FromSeconds(1));

                Assert.NotNull(futuresLimitEvent.Limit);
                Assert.Equal(0, futuresLimitEvent.Limit!.limit_type);
                Assert.Equal(250000, futuresLimitEvent.Limit.cbplimit);
                Assert.Equal("SUR", futuresLimitEvent.Limit.currcode);
                Assert.Equal(1450.25, futuresLimitEvent.Limit.real_varmargin);
                Assert.False(registry.TryGetMetadata(messageId, out _));
                eventAggregator.Close();
        }

    [Fact]
    public void GetEventProcessingMetrics_returns_registered_event_types_snapshot()
    {
        var registry = new MessageRegistry();
        var bridge = CreateBridge(registry);

        var metrics = bridge.GetEventProcessingMetrics();

        Assert.NotEmpty(metrics);
        Assert.Contains(metrics, snapshot => snapshot.EventType == typeof(InstrumentParametersUpdateEvent));
    }

    [Fact]
    public void GetEventProcessingMetrics_for_specific_type_returns_matching_snapshot()
    {
        var registry = new MessageRegistry();
        var bridge = CreateBridge(registry);

        var allMetrics = bridge.GetEventProcessingMetrics();
        var specificMetrics = bridge.GetEventProcessingMetrics<InstrumentParametersUpdateEvent>();

        var expectedSnapshot = Assert.Single(allMetrics.Where(snapshot => snapshot.EventType == typeof(InstrumentParametersUpdateEvent)));

        Assert.Equal(expectedSnapshot, specificMetrics);
    }

    [Fact]
    public async Task RespArrivedEventHandler_raises_parameter_update_event_for_get_param_response()
    {
        var registry = new MessageRegistry();
        var eventAggregator = new QuikBridgeEventAggregator(new QuikBridgeConfig());
        var responseHandler = new RespArrivedEventHandler(registry, eventAggregator, new QuikBridgeConfig());
        var completion = new TaskCompletionSource<InstrumentParametersUpdateEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        const int messageId = 7;

        eventAggregator.SubscribeToInstrumentParameterUpdate(args =>
        {
            completion.TrySetResult(args);
            return Task.CompletedTask;
        });

        registry.RegisterMessage(messageId, new QMessage
        {
            Id = messageId,
            MessageType = MessageType.GetParam,
            Method = "getParamEx2",
            ClassCode = "TQBR",
            Ticker = "SBER",
            ParamName = "LAST"
        });

        var responseEvent = new RespArrivedEvent(new JsonMessage
        {
            id = messageId,
            type = "ans",
            body = JObject.Parse("{\"result\":[{\"param_value\":\"123.45\"}]}")
        });

        await responseHandler.HandleAsync(responseEvent);

        var parameterUpdate = await completion.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal("TQBR", parameterUpdate.ClassCode);
        Assert.Equal("SBER", parameterUpdate.SecCode);
        Assert.Equal("LAST", parameterUpdate.ParamName);
        Assert.Equal("123.45", parameterUpdate.ParamValue);
        Assert.False(registry.TryGetMetadata(messageId, out _));

        eventAggregator.Close();
    }

        [Fact]
        public async Task RespArrivedEventHandler_raises_orderbook_update_event_for_snapshot_response()
        {
                var registry = new MessageRegistry();
                var eventAggregator = new QuikBridgeEventAggregator(new QuikBridgeConfig());
                var responseHandler = new RespArrivedEventHandler(registry, eventAggregator, new QuikBridgeConfig());
                var completion = new TaskCompletionSource<OrderBookUpdateEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
                const int messageId = 9;

                eventAggregator.SubscribeToOrderBookUpdate(args =>
                {
                        completion.TrySetResult(args);
                        return Task.CompletedTask;
                });

                registry.RegisterMessage(messageId, new QMessage
                {
                        Id = messageId,
                        MessageType = MessageType.OrderBookSnapshot,
                        Method = "getQuoteLevel2",
                        ClassCode = "TQBR",
                        Ticker = "SBER"
                });

                var responseEvent = new RespArrivedEvent(new JsonMessage
                {
                        id = messageId,
                        type = "ans",
                        body = JObject.Parse("""
                                {
                                    "result": [
                                        {
                                            "bid_count": "1",
                                            "offer_count": "1",
                                            "bid": [
                                                { "price": "10", "quantity": "2" }
                                            ],
                                            "offer": [
                                                { "price": "11", "quantity": "3" }
                                            ]
                                        }
                                    ]
                                }
                                """)
                });

                await responseHandler.HandleAsync(responseEvent);

                var orderBookUpdate = await completion.Task.WaitAsync(TimeSpan.FromSeconds(1));

                Assert.Equal("TQBR", orderBookUpdate.ClassCode);
                Assert.Equal("SBER", orderBookUpdate.SecCode);
                Assert.NotNull(orderBookUpdate.OrderBook);
                Assert.Equal("1", orderBookUpdate.OrderBook!.bid_count);
                Assert.Equal("1", orderBookUpdate.OrderBook.offer_count);
                Assert.Single(orderBookUpdate.OrderBook.bid!);
                Assert.Single(orderBookUpdate.OrderBook.offer!);
                Assert.False(registry.TryGetMetadata(messageId, out _));

                eventAggregator.Close();
        }

    private static QuikBridge CreateBridge(MessageRegistry registry)
    {
        var config = new QuikBridgeConfig();
        var eventAggregator = new QuikBridgeEventAggregator(config);
        var protocolHandler = CreateProtocolHandler(config);
        return new QuikBridge(protocolHandler, registry, eventAggregator, config);
    }

    private static QuikBridgeProtocolHandler CreateProtocolHandler(QuikBridgeConfig? config = null)
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var dispatcher = new QuikBridgeEventDispatcher(serviceProvider);
        return new QuikBridgeProtocolHandler(dispatcher, config ?? new QuikBridgeConfig());
    }

    private static QMessage GetSingleRegisteredMessage(MessageRegistry registry)
    {
        var field = typeof(MessageRegistry).GetField("_registry", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);

        var entries = (ConcurrentDictionary<int, QMessage>)field!.GetValue(registry)!;

        return Assert.Single(entries.Values);
    }

    private static T InvokePrivate<T>(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        return (T)method!.Invoke(target, args)!;
    }
}