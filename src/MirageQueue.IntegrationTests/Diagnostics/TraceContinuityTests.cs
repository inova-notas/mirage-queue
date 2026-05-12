using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MirageQueue.Consumers;
using MirageQueue.Consumers.Abstractions;
using MirageQueue.Diagnostics;
using MirageQueue.IntegrationTests.Fixtures;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using MirageQueue.Postgres.Databases;
using MirageQueue.Publishers.Abstractions;
using Xunit;

namespace MirageQueue.IntegrationTests.Diagnostics;

[Collection(PostgresCollection.Name)]
public class TraceContinuityTests
{
    private readonly PostgresFixture _fixture;
    private static readonly string ConsumerEndpoint = typeof(TraceTestConsumer).FullName!;

    public TraceContinuityTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Publish_PersistsCurrentTraceContextOnInboundRow()
    {
        await _fixture.ResetAsync();
        TraceTestConsumer.Behavior = _ => Task.CompletedTask;

        using var recorder = new ActivityRecorder();
        using var testSource = new ActivitySource("TraceContinuityTests");

        using (testSource.StartActivity("test-parent"))
        {
            await using var scope = _fixture.Services.CreateAsyncScope();
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(new TraceTestMessage { Id = Guid.NewGuid() });
        }

        await using var verify = _fixture.CreateMirageQueueDbContext();
        var row = await verify.Set<InboundMessage>().AsNoTracking().SingleAsync();
        Assert.False(string.IsNullOrEmpty(row.TraceParent));
        Assert.StartsWith("00-", row.TraceParent);
        Assert.Equal(55, row.TraceParent!.Length);
    }

    [Fact]
    public async Task PublishThenDispatch_LinksConsumerSpanAsChildOfPublishSpan()
    {
        await _fixture.ResetAsync();
        TraceTestConsumer.Behavior = _ => Task.CompletedTask;

        using var recorder = new ActivityRecorder();
        using var testSource = new ActivitySource("TraceContinuityTests");

        using (testSource.StartActivity("test-parent"))
        {
            await using var scope = _fixture.Services.CreateAsyncScope();
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(new TraceTestMessage { Id = Guid.NewGuid() });
        }

        await DriveFanOutAsync();
        var outbound = await PickOutboundForConsumerAsync();
        await DispatchAsync(outbound);

        var publish = recorder.Activities.SingleOrDefault(a => a.OperationName.StartsWith("publish "));
        var process = recorder.Activities.SingleOrDefault(a => a.OperationName.StartsWith("process "));
        Assert.NotNull(publish);
        Assert.NotNull(process);
        Assert.Equal(publish!.TraceId, process!.TraceId);
        Assert.Equal(publish.SpanId, process.ParentSpanId);
        Assert.Equal(ActivityStatusCode.Unset, process.Status);
    }

    [Fact]
    public async Task DispatchWithNullStoredTraceParent_StartsFreshRootSpan()
    {
        await _fixture.ResetAsync();
        TraceTestConsumer.Behavior = _ => Task.CompletedTask;

        using var recorder = new ActivityRecorder();

        var inboundId = await _fixture.SeedInboundAsync(InboundMessageStatus.Queued);
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, ConsumerEndpoint, OutboundMessageStatus.Processing);
        var outbound = await LoadOutboundAsync(outboundId);
        Assert.Null(outbound.TraceParent);

        await DispatchAsync(outbound);

        var process = recorder.Activities.SingleOrDefault(a => a.OperationName.StartsWith("process "));
        Assert.NotNull(process);
        Assert.Null(process!.Parent);
        Assert.Equal(default, process.ParentSpanId);
    }

    [Fact]
    public async Task FailedConsumer_SetsErrorStatusAndRecordsExceptionEvent()
    {
        await _fixture.ResetAsync();
        TraceTestConsumer.Behavior = _ => throw new InvalidOperationException("boom");

        using var recorder = new ActivityRecorder();

        var inboundId = await _fixture.SeedInboundAsync(InboundMessageStatus.Queued);
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, ConsumerEndpoint, OutboundMessageStatus.Processing);
        var outbound = await LoadOutboundAsync(outboundId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => DispatchAsync(outbound));

        var process = recorder.Activities.SingleOrDefault(a => a.OperationName.StartsWith("process "));
        Assert.NotNull(process);
        Assert.Equal(ActivityStatusCode.Error, process!.Status);
        Assert.Equal("boom", process.StatusDescription);
        Assert.Contains(process.Events, e => e.Name == "exception");
    }

    [Fact]
    public async Task FanOut_PropagatesInboundTraceContextToEveryOutboundRow()
    {
        await _fixture.ResetAsync();
        TraceTestConsumer.Behavior = _ => Task.CompletedTask;

        using var recorder = new ActivityRecorder();
        using var testSource = new ActivitySource("TraceContinuityTests");

        using (testSource.StartActivity("test-parent"))
        {
            await using var scope = _fixture.Services.CreateAsyncScope();
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(new TraceTestMessage { Id = Guid.NewGuid() });
        }

        await DriveFanOutAsync();

        await using var verify = _fixture.CreateMirageQueueDbContext();
        var inbound = await verify.Set<InboundMessage>().AsNoTracking().SingleAsync();
        var outbound = await verify.Set<OutboundMessage>().AsNoTracking()
            .Where(o => o.InboundMessageId == inbound.Id)
            .ToListAsync();

        Assert.NotEmpty(outbound);
        Assert.NotNull(inbound.TraceParent);
        Assert.All(outbound, o =>
        {
            Assert.Equal(inbound.TraceParent, o.TraceParent);
            Assert.Equal(inbound.TraceState, o.TraceState);
        });
    }

    [Fact]
    public async Task Publish_IncrementsCounter_AndDispatch_RecordsProcessDuration()
    {
        await _fixture.ResetAsync();
        TraceTestConsumer.Behavior = _ => Task.CompletedTask;

        using var recorder = new ActivityRecorder();

        var publishedCount = 0L;
        var processDurationSamples = 0;
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name != MirageQueueDiagnostics.MeterName) return;
                if (instrument.Name is "messaging.client.published.messages" or "messaging.process.duration")
                    listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
        {
            if (instrument.Name == "messaging.client.published.messages")
                Interlocked.Add(ref publishedCount, value);
        });
        meterListener.SetMeasurementEventCallback<double>((instrument, _, _, _) =>
        {
            if (instrument.Name == "messaging.process.duration")
                Interlocked.Increment(ref processDurationSamples);
        });
        meterListener.Start();

        await using (var scope = _fixture.Services.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(new TraceTestMessage { Id = Guid.NewGuid() });
        }

        await DriveFanOutAsync();
        var outbound = await PickOutboundForConsumerAsync();
        await DispatchAsync(outbound);

        Assert.Equal(1, Interlocked.Read(ref publishedCount));
        Assert.True(processDurationSamples >= 1, $"expected at least one process.duration sample, got {processDurationSamples}");
    }

    // ---- helpers ----

    private async Task DriveFanOutAsync()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MirageQueueDbContext>();
        await using var tx = await dbContext.Database.BeginTransactionAsync();
        await handler.HandleQueuedInboundMessages(tx);
        await dbContext.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private async Task<OutboundMessage> PickOutboundForConsumerAsync()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();
        var rows = await repo.GetQueuedMessages(limit: 20);
        var match = rows.Single(r => r.ConsumerEndpoint == ConsumerEndpoint);
        await repo.MarkProcessing(match.Id);
        return await LoadOutboundAsync(match.Id);
    }

    private async Task<OutboundMessage> LoadOutboundAsync(Guid id)
    {
        await using var verify = _fixture.CreateMirageQueueDbContext();
        return await verify.Set<OutboundMessage>().AsNoTracking().FirstAsync(x => x.Id == id);
    }

    private async Task DispatchAsync(OutboundMessage outbound)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
        await dispatcher.ProcessOutboundMessage(outbound);
    }

    private sealed class ActivityRecorder : IDisposable
    {
        private readonly ActivityListener _listener;
        public ConcurrentBag<Activity> Activities { get; } = new();

        public ActivityRecorder()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source =>
                    source.Name == MirageQueueDiagnostics.ActivitySourceName
                    || source.Name == "TraceContinuityTests",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = a => Activities.Add(a)
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public void Dispose() => _listener.Dispose();
    }
}
