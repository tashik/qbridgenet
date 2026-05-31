using System.Collections.Concurrent;

namespace QuikBridgeNet;

internal class QuikBridgeSubscriptionManager
{
    private sealed class SubscriptionState
    {
        public SemaphoreSlim SyncRoot { get; } = new(1, 1);
        public HashSet<Guid> Tokens { get; } = [];
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
            if (state.Tokens.Count == 0)
            {
                await subscribeAsync();
            }

            var entry = new SubscriptionEntry
            {
                SubscriptionToken = Guid.NewGuid()
            };

            state.Tokens.Add(entry.SubscriptionToken);

            return entry;
        }
        catch
        {
            if (state.Tokens.Count == 0)
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

            if (state.Tokens.Count > 0)
            {
                return 0;
            }

            try
            {
                var messageId = await unsubscribeAsync();
                _subscriptions.TryRemove(new KeyValuePair<string, SubscriptionState>(key, state));
                return messageId;
            }
            catch
            {
                state.Tokens.Add(token);
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
        return _subscriptions.TryGetValue(key, out var state) && state.Tokens.Count > 0;
    }
}