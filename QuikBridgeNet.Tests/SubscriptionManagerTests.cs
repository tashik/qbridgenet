using System.Collections.Concurrent;

namespace QuikBridgeNet.Tests;

public class SubscriptionManagerTests
{
    [Fact]
    public async Task SubscribeAsync_for_same_key_sends_single_remote_subscribe_under_parallel_load()
    {
        var manager = new QuikBridgeSubscriptionManager();
        var remoteSubscribeCalls = 0;
        var subscriptions = await Task.WhenAll(Enumerable.Range(0, 64)
            .Select(_ => manager.SubscribeAsync("TQBR:SBER:LAST", async () =>
            {
                Interlocked.Increment(ref remoteSubscribeCalls);
                await Task.Yield();
                return 1;
            })));

        Assert.Equal(1, remoteSubscribeCalls);
        Assert.Equal(64, subscriptions.Select(x => x.SubscriptionToken).Distinct().Count());
        Assert.True(manager.HasSubscribers("TQBR:SBER:LAST"));
    }

    [Fact]
    public async Task UnsubscribeAsync_does_not_send_remote_unsubscribe_until_last_local_subscriber_leaves()
    {
        var manager = new QuikBridgeSubscriptionManager();
        var remoteUnsubscribeCalls = 0;
        var first = await manager.SubscribeAsync("TQBR:SBER:LAST", () => Task.FromResult(101));
        var second = await manager.SubscribeAsync("TQBR:SBER:LAST", () => Task.FromResult(102));

        var firstResult = await manager.UnsubscribeAsync("TQBR:SBER:LAST", first.SubscriptionToken, () =>
        {
            Interlocked.Increment(ref remoteUnsubscribeCalls);
            return Task.FromResult(201);
        });

        var secondResult = await manager.UnsubscribeAsync("TQBR:SBER:LAST", second.SubscriptionToken, () =>
        {
            Interlocked.Increment(ref remoteUnsubscribeCalls);
            return Task.FromResult(202);
        });

        Assert.Equal(0, firstResult);
        Assert.Equal(202, secondResult);
        Assert.Equal(1, remoteUnsubscribeCalls);
        Assert.False(manager.HasSubscribers("TQBR:SBER:LAST"));
    }

    [Fact]
    public async Task SubscribeAsync_after_full_unsubscribe_sends_remote_subscribe_again_once()
    {
        var manager = new QuikBridgeSubscriptionManager();
        var remoteSubscribeCalls = 0;
        var remoteUnsubscribeCalls = 0;

        var first = await manager.SubscribeAsync("SPBFUT:SiH5:orderbook", () =>
        {
            Interlocked.Increment(ref remoteSubscribeCalls);
            return Task.FromResult(1);
        });

        await manager.UnsubscribeAsync("SPBFUT:SiH5:orderbook", first.SubscriptionToken, () =>
        {
            Interlocked.Increment(ref remoteUnsubscribeCalls);
            return Task.FromResult(2);
        });

        var second = await manager.SubscribeAsync("SPBFUT:SiH5:orderbook", () =>
        {
            Interlocked.Increment(ref remoteSubscribeCalls);
            return Task.FromResult(3);
        });

        Assert.Equal(2, remoteSubscribeCalls);
        Assert.Equal(1, remoteUnsubscribeCalls);
        Assert.True(manager.HasSubscribers("SPBFUT:SiH5:orderbook"));
        Assert.NotEqual(first.SubscriptionToken, second.SubscriptionToken);
    }

    [Fact]
    public async Task DatasourceManager_deduplicates_create_until_last_close()
    {
        var manager = new QuikBridgeDatasourceManager();
        var remoteCreateCalls = 0;
        var remoteCloseCalls = 0;

        var first = await manager.AcquireAsync("SBER[5]", () =>
        {
            Interlocked.Increment(ref remoteCreateCalls);
            return Task.FromResult(101);
        });
        var second = await manager.AcquireAsync("SBER[5]", () =>
        {
            Interlocked.Increment(ref remoteCreateCalls);
            return Task.FromResult(102);
        });

        await manager.MarkReadyAsync("SBER[5]", () => Task.FromResult(0));

        var firstClose = await manager.ReleaseAsync("SBER[5]", () =>
        {
            Interlocked.Increment(ref remoteCloseCalls);
            return Task.FromResult(201);
        });
        var secondClose = await manager.ReleaseAsync("SBER[5]", () =>
        {
            Interlocked.Increment(ref remoteCloseCalls);
            return Task.FromResult(202);
        });

        Assert.Equal(1, remoteCreateCalls);
        Assert.Equal(101, first.MessageId);
        Assert.Equal(101, second.MessageId);
        Assert.Equal(0, firstClose);
        Assert.Equal(202, secondClose);
        Assert.Equal(1, remoteCloseCalls);
        Assert.False(manager.HasConsumers("SBER[5]"));
    }

    [Fact]
    public async Task DatasourceManager_closes_after_ready_if_last_consumer_left_early()
    {
        var manager = new QuikBridgeDatasourceManager();
        var remoteCreateCalls = 0;
        var remoteCloseCalls = 0;

        await manager.AcquireAsync("SBER[1]", () =>
        {
            Interlocked.Increment(ref remoteCreateCalls);
            return Task.FromResult(301);
        });

        var earlyClose = await manager.ReleaseAsync("SBER[1]", () =>
        {
            Interlocked.Increment(ref remoteCloseCalls);
            return Task.FromResult(401);
        });
        var deferredClose = await manager.MarkReadyAsync("SBER[1]", () =>
        {
            Interlocked.Increment(ref remoteCloseCalls);
            return Task.FromResult(402);
        });

        Assert.Equal(1, remoteCreateCalls);
        Assert.Equal(0, earlyClose);
        Assert.Equal(402, deferredClose);
        Assert.Equal(1, remoteCloseCalls);
        Assert.False(manager.HasConsumers("SBER[1]"));
    }

    [Fact]
    public async Task CallbackRegistry_registers_each_callback_only_once()
    {
        var registry = new QuikBridgeCallbackRegistry<string>();
        var remoteRegisterCalls = 0;

        var first = await registry.RegisterAsync("OnAllTrade", () =>
        {
            Interlocked.Increment(ref remoteRegisterCalls);
            return Task.FromResult(11);
        });
        var second = await registry.RegisterAsync("OnAllTrade", () =>
        {
            Interlocked.Increment(ref remoteRegisterCalls);
            return Task.FromResult(12);
        });

        Assert.Equal(11, first);
        Assert.Equal(0, second);
        Assert.Equal(1, remoteRegisterCalls);
    }

    [Fact]
    public async Task SubscribeAsync_restores_remote_subscription_after_remote_state_loss()
    {
        var manager = new QuikBridgeSubscriptionManager();
        var remoteSubscribeCalls = 0;

        await manager.SubscribeAsync("TQBR:SBER:LAST", () =>
        {
            Interlocked.Increment(ref remoteSubscribeCalls);
            return Task.FromResult(101);
        });

        manager.InvalidateRemoteState();

        var restoreResult = await manager.RestoreAsync("TQBR:SBER:LAST", () =>
        {
            Interlocked.Increment(ref remoteSubscribeCalls);
            return Task.FromResult(202);
        });

        Assert.Equal(202, restoreResult);
        Assert.Equal(2, remoteSubscribeCalls);
        Assert.True(manager.HasSubscribers("TQBR:SBER:LAST"));
    }

    [Fact]
    public async Task DatasourceManager_restores_remote_datasource_after_remote_state_loss()
    {
        var manager = new QuikBridgeDatasourceManager();
        var remoteCreateCalls = 0;

        await manager.AcquireAsync("SBER[5]", () =>
        {
            Interlocked.Increment(ref remoteCreateCalls);
            return Task.FromResult(101);
        });

        await manager.MarkReadyAsync("SBER[5]", () => Task.FromResult(0));
        manager.InvalidateRemoteState();

        var restoreResult = await manager.RestoreAsync("SBER[5]", () =>
        {
            Interlocked.Increment(ref remoteCreateCalls);
            return Task.FromResult(202);
        });

        Assert.Equal(202, restoreResult);
        Assert.Equal(2, remoteCreateCalls);
        Assert.True(manager.HasConsumers("SBER[5]"));
    }

    [Fact]
    public async Task CallbackRegistry_restores_remote_callback_after_remote_state_loss()
    {
        var registry = new QuikBridgeCallbackRegistry<string>();
        var remoteRegisterCalls = 0;

        await registry.RegisterAsync("OnAllTrade", () =>
        {
            Interlocked.Increment(ref remoteRegisterCalls);
            return Task.FromResult(11);
        });

        registry.InvalidateRemoteState();

        var restoreResult = await registry.RegisterAsync("OnAllTrade", () =>
        {
            Interlocked.Increment(ref remoteRegisterCalls);
            return Task.FromResult(22);
        });

        Assert.Equal(22, restoreResult);
        Assert.Equal(2, remoteRegisterCalls);
        Assert.Single(registry.GetRegisteredKeys());
    }

    [Fact]
    public async Task CallbackRegistry_parallel_registers_same_callback_with_single_remote_call()
    {
        var registry = new QuikBridgeCallbackRegistry<string>();
        var remoteRegisterCalls = 0;

        var results = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => registry.RegisterAsync("OnOrder", async () =>
            {
                Interlocked.Increment(ref remoteRegisterCalls);
                await Task.Delay(10);
                return 55;
            })));

        Assert.Equal(1, remoteRegisterCalls);
        Assert.Equal(1, results.Count(result => result == 55));
        Assert.Equal(31, results.Count(result => result == 0));
        Assert.Single(registry.GetRegisteredKeys());
    }
}