using System.Collections.Concurrent;

namespace QuikBridgeNet;

internal class QuikBridgeSubscriptionManager
{
    private sealed class SubscriptionState
    {
        public SemaphoreSlim SyncRoot { get; } = new(1, 1);
        public HashSet<Guid> Tokens { get; } = [];
        public int TokenCount { get; set; }
        public bool IsRemoteActive { get; set; }
    }

    internal class SubscriptionEntry
    {
        public Guid SubscriptionToken { get; set; }
    }

    private readonly ConcurrentDictionary<string, SubscriptionState> _subscriptions = new();

    public async Task<SubscriptionEntry> SubscribeAsync(string key, Func<Task<int>> subscribeAsync)
    {
        var state = _subscriptions.GetOrAdd(key, _ => new SubscriptionState());
        await state.SyncRoot.WaitAsync();

        try
        {
            if (state.TokenCount == 0 || !state.IsRemoteActive)
            {
                await subscribeAsync();
                state.IsRemoteActive = true;
            }

            var entry = new SubscriptionEntry
            {
                SubscriptionToken = Guid.NewGuid()
            };

            state.Tokens.Add(entry.SubscriptionToken);
            state.TokenCount = state.Tokens.Count;

            return entry;
        }
        catch
        {
            if (state.TokenCount == 0)
            {
                _subscriptions.TryRemove(new KeyValuePair<string, SubscriptionState>(key, state));
            }

            throw;
        }
        finally
        {
            state.SyncRoot.Release();
        }
    }

    public async Task<int> UnsubscribeAsync(string key, Guid token, Func<Task<int>> unsubscribeAsync)
    {
        if (!_subscriptions.TryGetValue(key, out var state))
        {
            return 0;
        }

        await state.SyncRoot.WaitAsync();

        try
        {
            if (!state.Tokens.Remove(token))
            {
                return 0;
            }

            state.TokenCount = state.Tokens.Count;

            if (state.TokenCount > 0)
            {
                return 0;
            }

            if (!state.IsRemoteActive)
            {
                _subscriptions.TryRemove(new KeyValuePair<string, SubscriptionState>(key, state));
                return 0;
            }

            try
            {
                var messageId = await unsubscribeAsync();
                state.IsRemoteActive = false;
                _subscriptions.TryRemove(new KeyValuePair<string, SubscriptionState>(key, state));
                return messageId;
            }
            catch
            {
                state.Tokens.Add(token);
                state.TokenCount = state.Tokens.Count;
                throw;
            }
        }
        finally
        {
            state.SyncRoot.Release();
        }
    }

    public bool HasSubscribers(string key)
    {
        return _subscriptions.TryGetValue(key, out var state) && state.TokenCount > 0;
    }

    public async Task<int> RestoreAsync(string key, Func<Task<int>> subscribeAsync)
    {
        if (!_subscriptions.TryGetValue(key, out var state))
        {
            return 0;
        }

        await state.SyncRoot.WaitAsync();

        try
        {
            if (state.TokenCount == 0 || state.IsRemoteActive)
            {
                return 0;
            }

            var messageId = await subscribeAsync();
            state.IsRemoteActive = true;
            return messageId;
        }
        finally
        {
            state.SyncRoot.Release();
        }
    }

    public void InvalidateRemoteState()
    {
        foreach (var subscription in _subscriptions.Values)
        {
            subscription.IsRemoteActive = false;
        }
    }
}