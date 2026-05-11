using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MirageQueue.IntegrationTests.Fixtures;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using Xunit;

namespace MirageQueue.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class RetryPolicyTests
{
    private readonly PostgresFixture _fixture;

    public RetryPolicyTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetQueuedMessages_RowWithFutureNextRetryAt_NotPickedUp()
    {
        await _fixture.ResetAsync();

        // Seed an outbound row with NextRetryAt in the future.
        var inboundId = await _fixture.SeedInboundAsync();
        await _fixture.SeedOutboundAsync(inboundId, "endpoint-A", OutboundMessageStatus.New, nextRetryAt: DateTime.UtcNow.AddMinutes(10));

        await using var scope = _fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

        var picked = await repo.GetQueuedMessages(limit: 10);

        Assert.Empty(picked);
    }

    [Fact]
    public async Task GetQueuedMessages_RowWithPastNextRetryAt_IsPickedUp()
    {
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync();
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, "endpoint-B", OutboundMessageStatus.New, nextRetryAt: DateTime.UtcNow.AddMinutes(-1));

        await using var scope = _fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

        var picked = await repo.GetQueuedMessages(limit: 10);

        Assert.Single(picked);
        Assert.Equal(outboundId, picked[0].Id);
    }

    [Fact]
    public async Task GetQueuedMessages_RowWithNullNextRetryAt_IsPickedUp()
    {
        // Backward compat: rows without NextRetryAt set behave like today (immediately eligible).
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync();
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, "endpoint-C", OutboundMessageStatus.New, nextRetryAt: null);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

        var picked = await repo.GetQueuedMessages(limit: 10);

        Assert.Single(picked);
        Assert.Equal(outboundId, picked[0].Id);
    }

    [Fact]
    public async Task MarkForRetry_SetsAllExpectedFields()
    {
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync();
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, "endpoint-D", OutboundMessageStatus.Processing);

        var retryAt = DateTime.UtcNow.AddSeconds(30);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

        await repo.MarkForRetry(outboundId, attemptCount: 2, retryAt, errorMessage: "boom", stackTrace: "stack", exceptionType: "System.Exception");

        await using var verify = _fixture.CreateMirageQueueDbContext();
        var row = await verify.Set<OutboundMessage>().AsNoTracking().FirstAsync(x => x.Id == outboundId);

        Assert.Equal(OutboundMessageStatus.New, row.Status);
        Assert.Equal(2, row.AttemptCount);
        Assert.NotNull(row.NextRetryAt);
        Assert.True(Math.Abs((row.NextRetryAt!.Value - retryAt).TotalSeconds) < 1);
        Assert.Null(row.ProcessingStartedAt);
        Assert.Equal("boom", row.ErrorMessage);
        Assert.Equal("stack", row.StackTrace);
        Assert.Equal("System.Exception", row.ExceptionType);
    }

    [Fact]
    public async Task MarkProcessing_StampsProcessingStartedAt()
    {
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync();
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, "endpoint-E", OutboundMessageStatus.New);

        var before = DateTime.UtcNow;

        await using var scope = _fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

        await repo.MarkProcessing(outboundId);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        var row = await verify.Set<OutboundMessage>().AsNoTracking().FirstAsync(x => x.Id == outboundId);

        Assert.Equal(OutboundMessageStatus.Processing, row.Status);
        Assert.NotNull(row.ProcessingStartedAt);
        Assert.True(row.ProcessingStartedAt >= before);
    }

}
