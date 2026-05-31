using QuikBridgeNetEvents;
using QuikBridgeNetEvents.Events;
using QuikBridgeNetDomain;

namespace QuikBridgeNet.Tests;

public class EventAggregatorTests
{
    [Fact]
    public async Task Slow_handler_does_not_block_following_events_for_same_type()
    {
        var aggregator = new QuikBridgeEventAggregator(new QuikBridgeConfig());
        var slowHandlerGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEventReachedFastHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fastHandlerCount = 0;

        aggregator.SubscribeToInstrumentParameterUpdate(async args =>
        {
            if (args.ParamValue == "first")
            {
                await slowHandlerGate.Task;
            }
        });

        aggregator.SubscribeToInstrumentParameterUpdate(args =>
        {
            if (args.ParamValue == "second")
            {
                Interlocked.Increment(ref fastHandlerCount);
                secondEventReachedFastHandler.TrySetResult();
            }

            return Task.CompletedTask;
        });

        await aggregator.RaiseEvent(new InstrumentParametersUpdateEvent { ParamValue = "first" });
        await aggregator.RaiseEvent(new InstrumentParametersUpdateEvent { ParamValue = "second" });

        await secondEventReachedFastHandler.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(1, fastHandlerCount);

        slowHandlerGate.TrySetResult();
        aggregator.Close();
    }

    [Fact]
    public async Task Single_handler_preserves_event_order()
    {
        var aggregator = new QuikBridgeEventAggregator(new QuikBridgeConfig());
        var processedValues = new List<string>();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        aggregator.SubscribeToInstrumentParameterUpdate(async args =>
        {
            if (args.ParamValue == "first")
            {
                await Task.Delay(50);
            }

            lock (processedValues)
            {
                processedValues.Add(args.ParamValue!);
                if (processedValues.Count == 2)
                {
                    completion.TrySetResult();
                }
            }
        });

        await aggregator.RaiseEvent(new InstrumentParametersUpdateEvent { ParamValue = "first" });
        await aggregator.RaiseEvent(new InstrumentParametersUpdateEvent { ParamValue = "second" });

        await completion.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(["first", "second"], processedValues);

        aggregator.Close();
    }

    [Fact]
    public async Task Metrics_snapshot_tracks_pending_and_active_handlers()
    {
        var aggregator = new QuikBridgeEventAggregator(new QuikBridgeConfig());
        var firstHandlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        aggregator.SubscribeToInstrumentParameterUpdate(async args =>
        {
            firstHandlerStarted.TrySetResult();

            if (args.ParamValue == "first")
            {
                await releaseFirstHandler.Task;
            }
        });

        await aggregator.RaiseEvent(new InstrumentParametersUpdateEvent { ParamValue = "first" });
        await firstHandlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await aggregator.RaiseEvent(new InstrumentParametersUpdateEvent { ParamValue = "second" });

        EventProcessingMetrics? snapshot = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            snapshot = aggregator.GetMetricsSnapshot<InstrumentParametersUpdateEvent>();
            if (snapshot.ActiveHandlerExecutions == 1 && snapshot.PendingHandlerExecutions == 1)
            {
                break;
            }

            await Task.Delay(10);
        }

        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot!.ActiveHandlerExecutions);
        Assert.Equal(1, snapshot.PendingHandlerExecutions);
        Assert.Equal(1, snapshot.SubscriberCount);

        releaseFirstHandler.TrySetResult();
        aggregator.Close();
    }
}