using System.Collections.Concurrent;

namespace QuikBridgeNet;

internal class QuikBridgeDatasourceManager
{
    internal readonly record struct AcquireResult(int MessageId, bool IsFirstReference);

    private sealed class DatasourceState
    {
        public SemaphoreSlim SyncRoot { get; } = new(1, 1);
        public int RefCount { get; set; }
        public int CreateMessageId { get; set; }
        public bool IsReady { get; set; }
        public bool CloseRequestedBeforeReady { get; set; }
    }

    private readonly ConcurrentDictionary<string, DatasourceState> _states = new();

    public async Task<AcquireResult> AcquireAsync(string key, Func<Task<int>> createAsync)
    {
        var state = _states.GetOrAdd(key, _ => new DatasourceState());
        await state.SyncRoot.WaitAsync();

        try
        {
            var isFirstReference = state.RefCount == 0;

            if (state.CreateMessageId == 0)
            {
                state.CreateMessageId = await createAsync();
            }

            state.RefCount++;
            state.CloseRequestedBeforeReady = false;

            return new AcquireResult(state.CreateMessageId, isFirstReference);
        }
        catch
        {
            if (state.RefCount == 0 && state.CreateMessageId == 0)
            {
                _states.TryRemove(new KeyValuePair<string, DatasourceState>(key, state));
            }

            throw;
        }
        finally
        {
            state.SyncRoot.Release();
        }
    }

    public async Task<int> ReleaseAsync(string key, Func<Task<int>> closeAsync)
    {
        if (!_states.TryGetValue(key, out var state))
        {
            return 0;
        }

        await state.SyncRoot.WaitAsync();

        try
        {
            if (state.RefCount == 0)
            {
                return 0;
            }

            state.RefCount--;
            if (state.RefCount > 0)
            {
                return 0;
            }

            if (!state.IsReady)
            {
                state.CloseRequestedBeforeReady = true;
                return 0;
            }

            var messageId = await closeAsync();
            _states.TryRemove(new KeyValuePair<string, DatasourceState>(key, state));
            return messageId;
        }
        catch
        {
            state.RefCount++;
            throw;
        }
        finally
        {
            state.SyncRoot.Release();
        }
    }

    public async Task<int> MarkReadyAsync(string key, Func<Task<int>> closeAsync)
    {
        if (!_states.TryGetValue(key, out var state))
        {
            return 0;
        }

        await state.SyncRoot.WaitAsync();

        try
        {
            state.IsReady = true;
            if (state.RefCount > 0 || !state.CloseRequestedBeforeReady)
            {
                return 0;
            }

            var messageId = await closeAsync();
            _states.TryRemove(new KeyValuePair<string, DatasourceState>(key, state));
            return messageId;
        }
        finally
        {
            state.SyncRoot.Release();
        }
    }

    public bool HasConsumers(string key)
    {
        return _states.TryGetValue(key, out var state) && state.RefCount > 0;
    }
}