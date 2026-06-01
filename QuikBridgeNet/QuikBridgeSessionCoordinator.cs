using System.Collections.Concurrent;
using QuikBridgeNetDomain.Entities;
using QuikBridgeNetEvents;
using QuikBridgeNetEvents.Events;
using Serilog;

namespace QuikBridgeNet;

internal sealed class QuikBridgeSessionCoordinator(
    MessageRegistry messageRegistry,
    QuikBridgeEventAggregator eventAggregator,
    QuikBridgeSubscriptionManager subscriptionManager,
    QuikBridgeDatasourceManager datasourceManager,
    QuikBridgeCallbackRegistry<MessageType> globalCallbackRegistry,
    ConcurrentDictionary<string, object> dataSources,
    ConcurrentDictionary<string, SessionResourceDefinition> sessionResources)
{
    public void InvalidateRemoteState()
    {
        messageRegistry.Clear();
        dataSources.Clear();
        subscriptionManager.InvalidateRemoteState();
        datasourceManager.InvalidateRemoteState();
        globalCallbackRegistry.InvalidateRemoteState();
    }

    public async Task RestoreAsync(
        Func<MessageType, Task<int>> restoreGlobalCallbackAsync,
        Func<string, string, Task<int>> restoreOrderBookAsync,
        Func<string, string, string, Task<int>> restoreQuoteParameterAsync,
        Func<string, string, string, Task<int>> restoreDatasourceAsync)
    {
        foreach (var callback in globalCallbackRegistry.GetRegisteredKeys())
        {
            await TryRestoreRemoteStateAsync(
                "GlobalCallback",
                callback.ToString(),
                () => restoreGlobalCallbackAsync(callback));
        }

        foreach (var sessionResource in sessionResources.Values.ToArray())
        {
            switch (sessionResource.Kind)
            {
                case SessionResourceKind.OrderBookSubscription:
                    await TryRestoreRemoteStateAsync(
                        nameof(SessionResourceKind.OrderBookSubscription),
                        sessionResource.ResourceKey,
                        () => subscriptionManager.RestoreAsync(
                            sessionResource.ResourceKey,
                            () => restoreOrderBookAsync(sessionResource.ClassCode, sessionResource.SecCode)));
                    break;
                case SessionResourceKind.QuoteParameterSubscription:
                    await TryRestoreRemoteStateAsync(
                        nameof(SessionResourceKind.QuoteParameterSubscription),
                        sessionResource.ResourceKey,
                        () => subscriptionManager.RestoreAsync(
                            sessionResource.ResourceKey,
                            () => restoreQuoteParameterAsync(sessionResource.ClassCode, sessionResource.SecCode, sessionResource.ParamName!)));
                    break;
                case SessionResourceKind.Datasource:
                    await TryRestoreRemoteStateAsync(
                        nameof(SessionResourceKind.Datasource),
                        sessionResource.ResourceKey,
                        () => datasourceManager.RestoreAsync(
                            sessionResource.ResourceKey,
                            () => restoreDatasourceAsync(sessionResource.ClassCode, sessionResource.SecCode, sessionResource.Interval!)));
                    break;
            }
        }
    }

    private async Task TryRestoreRemoteStateAsync(string operation, string key, Func<Task<int>> restoreAsync)
    {
        try
        {
            await restoreAsync();
        }
        catch (Exception ex)
        {
            await eventAggregator.RaiseEvent(new SessionStateRestoreFailedEvent
            {
                Operation = operation,
                Key = key,
                ErrorMessage = ex.Message
            });

            Log.Warning(ex, "Не удалось восстановить session state {Operation} для ключа {Key}", operation, key);
        }
    }
}