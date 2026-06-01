using QuikBridgeNetDomain;
using QuikBridgeNetEvents;
using QuikBridgeNetEvents.Events;
using Serilog;

namespace QuikBridgeNet;

internal sealed class QuikBridgeRequestExpiryController(
    MessageRegistry messageRegistry,
    QuikBridgeEventAggregator eventAggregator,
    QuikBridgeConfig bridgeConfig)
{
    private CancellationTokenSource? _requestExpiryCancellation;
    private Task? _requestExpiryTask;

    public void Start()
    {
        if (bridgeConfig.RequestTimeoutMs <= 0 || _requestExpiryCancellation != null)
        {
            return;
        }

        _requestExpiryCancellation = new CancellationTokenSource();
        _requestExpiryTask = Task.Run(() => ProcessAsync(_requestExpiryCancellation.Token));
    }

    public async Task StopAsync()
    {
        if (_requestExpiryCancellation == null)
        {
            return;
        }

        _requestExpiryCancellation.Cancel();

        if (_requestExpiryTask != null)
        {
            try
            {
                await _requestExpiryTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _requestExpiryCancellation.Dispose();
        _requestExpiryCancellation = null;
        _requestExpiryTask = null;
    }

    public async Task ExpirePendingRequestsAsync()
    {
        if (bridgeConfig.RequestTimeoutMs <= 0)
        {
            return;
        }

        var expiredMessages = messageRegistry.ExpireOlderThan(TimeSpan.FromMilliseconds(bridgeConfig.RequestTimeoutMs));
        foreach (var expiredMessage in expiredMessages)
        {
            await eventAggregator.RaiseEvent(new RequestExpiredEvent
            {
                BridgeMessage = expiredMessage,
                TimeoutMs = bridgeConfig.RequestTimeoutMs
            });

            Log.Warning(
                "Запрос {MessageId} ({MessageType}/{Method}) истёк без ответа спустя {TimeoutMs} мс.",
                expiredMessage.Id,
                expiredMessage.MessageType,
                expiredMessage.Method,
                bridgeConfig.RequestTimeoutMs);
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var sweepIntervalMs = bridgeConfig.RequestExpirySweepIntervalMs > 0
            ? bridgeConfig.RequestExpirySweepIntervalMs
            : 1000;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(sweepIntervalMs));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await ExpirePendingRequestsAsync();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}