namespace QuikBridgeNet;

internal class QuikBridgeCallbackRegistry<TKey> where TKey : notnull
{
    private readonly SemaphoreSlim _syncRoot = new(1, 1);
    private readonly HashSet<TKey> _registeredKeys = [];

    public async Task<int> RegisterAsync(TKey key, Func<Task<int>> registerAsync)
    {
        await _syncRoot.WaitAsync();

        try
        {
            if (_registeredKeys.Contains(key))
            {
                return 0;
            }

            var messageId = await registerAsync();
            _registeredKeys.Add(key);
            return messageId;
        }
        finally
        {
            _syncRoot.Release();
        }
    }
}