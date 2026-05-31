using Newtonsoft.Json.Linq;
using QuikBridgeNet.EventHandlers;
using QuikBridgeNet.Events;
using QuikBridgeNetEvents;
using QuikBridgeNetEvents.Events;
using QuikBridgeNetDomain;
using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNet.Tests;

public class ReqArrivedEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_raises_order_event_for_OnOrder_with_non_zero_trans_id()
    {
        var registry = new MessageRegistry();
        var aggregator = new QuikBridgeEventAggregator(new QuikBridgeConfig());
        var handler = new ReqArrivedEventHandler(registry, aggregator, new QuikBridgeConfig());
        var completion = new TaskCompletionSource<OrderArrivedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        aggregator.SubscribeToOrders(args =>
        {
            completion.TrySetResult(args);
            return Task.CompletedTask;
        });

        await handler.HandleAsync(new ReqArrivedEvent(new JsonMessage
        {
            id = 1,
            type = "req",
            body = JObject.Parse("""
                {
                  "method": "callback",
                  "name": "OnOrder",
                  "arguments": [
                    {
                      "trans_id": "123",
                      "sec_code": "SBER",
                      "class_code": "TQBR",
                      "qty": 10,
                      "price": 250.5
                    }
                  ]
                }
                """)
        }));

        var orderEvent = await completion.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.NotNull(orderEvent.Order);
        Assert.Equal("123", orderEvent.Order!.trans_id);
        Assert.Equal("SBER", orderEvent.Order.sec_code);
        aggregator.Close();
    }

    [Fact]
    public async Task HandleAsync_raises_transaction_reply_event_for_OnTransReply_with_non_zero_trans_id()
    {
        var registry = new MessageRegistry();
        var aggregator = new QuikBridgeEventAggregator(new QuikBridgeConfig());
        var handler = new ReqArrivedEventHandler(registry, aggregator, new QuikBridgeConfig());
        var completion = new TaskCompletionSource<TransactionReplyArrivedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        aggregator.SubscribeToTransactionReplies(args =>
        {
            completion.TrySetResult(args);
            return Task.CompletedTask;
        });

        await handler.HandleAsync(new ReqArrivedEvent(new JsonMessage
        {
            id = 2,
            type = "req",
            body = JObject.Parse("""
                {
                  "method": "callback",
                  "name": "OnTransReply",
                  "arguments": [
                    {
                      "trans_id": "777",
                      "sec_code": "SiM6",
                      "class_code": "SPBFUT",
                      "qty": 1,
                      "price": 78000
                    }
                  ]
                }
                """)
        }));

        var replyEvent = await completion.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.NotNull(replyEvent.Order);
        Assert.Equal("777", replyEvent.Order!.trans_id);
        Assert.Equal("SPBFUT", replyEvent.Order.class_code);
        aggregator.Close();
    }

    [Fact]
    public async Task HandleAsync_ignores_OnOrder_with_zero_trans_id()
    {
        var registry = new MessageRegistry();
        var aggregator = new QuikBridgeEventAggregator(new QuikBridgeConfig());
        var handler = new ReqArrivedEventHandler(registry, aggregator, new QuikBridgeConfig());
        var raised = false;

        aggregator.SubscribeToOrders(args =>
        {
            raised = true;
            return Task.CompletedTask;
        });

        await handler.HandleAsync(new ReqArrivedEvent(new JsonMessage
        {
            id = 3,
            type = "req",
            body = JObject.Parse("""
                {
                  "method": "callback",
                  "name": "OnOrder",
                  "arguments": [
                    {
                      "trans_id": "0",
                      "sec_code": "SBER",
                      "class_code": "TQBR"
                    }
                  ]
                }
                """)
        }));

        await Task.Delay(50);

        Assert.False(raised);
        aggregator.Close();
    }
}