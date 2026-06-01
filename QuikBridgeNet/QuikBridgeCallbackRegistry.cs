namespace QuikBridgeNet;

internal class QuikBridgeCallbackRegistry<TKey> where TKey : notnull
{
    private readonly object _syncRoot = new();
    private readonly HashSet<TKey> _desiredKeys = [];
    private readonly HashSet<TKey> _remoteRegisteredKeys = [];
    private readonly Dictionary<TKey, Task<int>> _inFlightRegistrations = new();

    public async Task<int> RegisterAsync(TKey key, Func<Task<int>> registerAsync)
    {
        Task<int>? registrationTask;
        var ownsRegistration = false;

        lock (_syncRoot)
        {
            _desiredKeys.Add(key);
            if (_remoteRegisteredKeys.Contains(key))
            {
                return 0;
            }

            if (!_inFlightRegistrations.TryGetValue(key, out registrationTask))
            {
                registrationTask = registerAsync();
                _inFlightRegistrations[key] = registrationTask;
                ownsRegistration = true;
            }
        }

        try
        {
            var messageId = await registrationTask!;

            lock (_syncRoot)
            {
                _remoteRegisteredKeys.Add(key);
                if (ownsRegistration)
                {
                    _inFlightRegistrations.Remove(key);
                }
            }

            return ownsRegistration ? messageId : 0;
        }
        catch
        {
            lock (_syncRoot)
            {
                if (ownsRegistration)
                {
                    _inFlightRegistrations.Remove(key);
                }
            }

            throw;
        }
    }

    public IReadOnlyCollection<TKey> GetRegisteredKeys()
    {
        lock (_syncRoot)
        {
            return _desiredKeys.ToArray();
        }
    }

    public void InvalidateRemoteState()
    {
        lock (_syncRoot)
        {
            _remoteRegisteredKeys.Clear();
        }
    }
}