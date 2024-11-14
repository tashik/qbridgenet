using System.Collections.Concurrent;

namespace QuikBridgeNet;

internal class QuikBridgeSubscriptionManager
{
    internal class SubscriptionEntry
    {
        public int MessageId { get; set; }
        public Guid SubscriptionToken { get; set; }
    }
    
    private readonly ConcurrentDictionary<string, int> _messageIds = new();

    // Maps topic keys to a list of subscription entries
    private readonly ConcurrentDictionary<string, List<SubscriptionEntry>> _subscriptions = new();

    // Event to trigger when a topic should be globally unsubscribed
    public event Action<string, int>? Unsubscribed;
    
    public SubscriptionEntry Subscribe(string key, int msgId)
    {
        _messageIds.TryGetValue(key, out var messageId);
        if (messageId == 0)
        {
            _messageIds.TryAdd(key, msgId);
            messageId = msgId;
        } 
        
        Guid subscriptionToken = Guid.NewGuid();
        var newEntry = new SubscriptionEntry { MessageId = messageId, SubscriptionToken = subscriptionToken };
        
        _subscriptions.AddOrUpdate(key,
            _ => new List<SubscriptionEntry> { newEntry },
            (_, entries) =>
            {
                lock (entries)
                {
                    entries.Add(newEntry);
                }
                return entries;
            });

        return newEntry;
    }
    
    public void Unsubscribe(string key, Guid token)
    {
        if (_subscriptions.TryGetValue(key, out var entries))
        {
            lock (entries)
            {
                var entryToRemove = entries.FirstOrDefault(e => e.SubscriptionToken == token);
                if (entryToRemove != null)
                {
                    entries.Remove(entryToRemove);
                    if (entries.Count == 0)
                    {
                        if (_subscriptions.TryRemove(key, out _) && _messageIds.TryRemove(key, out var messageId))
                        {
                            Unsubscribed?.Invoke(key, messageId);
                        }
                    }
                }
            }
        }
    }
    
    public bool ContainsKey(string key)
    {
        _messageIds.TryGetValue(key, out var existingMessageId);
        return existingMessageId != 0;
    }
}