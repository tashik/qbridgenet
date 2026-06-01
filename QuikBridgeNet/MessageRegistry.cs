using QuikBridgeNet.Entities;
using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNet;

using System.Collections.Concurrent;

public class MessageRegistry
{
    private readonly ConcurrentDictionary<int, QMessage> _registry = new();

    public int Count => _registry.Count;

    public void RegisterMessage(int messageId, QMessage metadata)
    {
        _registry[messageId] = metadata;
    }

    public bool TryGetMetadata(int messageId, out QMessage? metadata)
    {
        return _registry.TryGetValue(messageId, out metadata);
    }

    public void RemoveMessage(int messageId)
    {
        _registry.TryRemove(messageId, out _);
    }

    public void Clear()
    {
        _registry.Clear();
    }

    public IReadOnlyCollection<QMessage> ExpireOlderThan(TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
        {
            return [];
        }

        var now = DateTimeOffset.UtcNow;
        List<QMessage> expired = [];

        foreach (var entry in _registry)
        {
            if (now - entry.Value.RegisteredAtUtc < ttl)
            {
                continue;
            }

            if (_registry.TryRemove(entry.Key, out var removed))
            {
                expired.Add(removed);
            }
        }

        return expired;
    }
}