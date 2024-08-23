using QuikBridgeNet.Entities;
using QuikBridgeNet.Entities.MessageMeta;

namespace QuikBridgeNet;

using System.Collections.Concurrent;

public class MessageRegistry
{
    private readonly ConcurrentDictionary<int, QMessage> _registry = new();

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
}