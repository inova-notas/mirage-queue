using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MirageQueue.IntegrationTests.Fixtures;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using Xunit;

namespace MirageQueue.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class DeadLetterTests
{
    private readonly PostgresFixture _fixture;

    public DeadLetterTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MarkDeadLettered_TransitionsToTerminalStatus()
    {
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync();
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, "endpoint-DLQ", OutboundMessageStatus.Processing, attemptCount: 5);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

        await repo.MarkDeadLettered(outboundId, attemptCount: 5, "exhausted", "trace", "Some.Exception");

        await using var verify = _fixture.CreateMirageQueueDbContext();
        var row = await verify.Set<OutboundMessage>().AsNoTracking().FirstAsync(x => x.Id == outboundId);

        Assert.Equal(OutboundMessageStatus.DeadLettered, row.Status);
        Assert.Null(row.ProcessingStartedAt);
        Assert.Equal("exhausted", row.ErrorMessage);
        Assert.Equal("trace", row.StackTrace);
        Assert.Equal("Some.Exception", row.ExceptionType);
        Assert.Equal(5, row.AttemptCount); // set to the attemptCount passed in
    }

    [Fact]
    public async Task DeadLetteredRow_IsNotPickedUpByPickupQuery()
    {
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync();
        await _fixture.SeedOutboundAsync(inboundId, "endpoint-DLQ-2", OutboundMessageStatus.DeadLettered);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

        var picked = await repo.GetQueuedMessages(limit: 10);

        Assert.Empty(picked);
    }

    [Fact]
    public async Task ReplayFromDeadLetter_ResetsToNewWithFreshState()
    {
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync();
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, "endpoint-DLQ-3", OutboundMessageStatus.DeadLettered, attemptCount: 7, nextRetryAt: DateTime.UtcNow.AddMinutes(10));

        await using var scope = _fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

        await repo.ReplayFromDeadLetter(outboundId);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        var row = await verify.Set<OutboundMessage>().AsNoTracking().FirstAsync(x => x.Id == outboundId);

        Assert.Equal(OutboundMessageStatus.New, row.Status);
        Assert.Equal(0, row.AttemptCount);
        Assert.Null(row.NextRetryAt);
        Assert.Null(row.ProcessingStartedAt);
        Assert.Null(row.ErrorMessage);
        Assert.Null(row.StackTrace);
        Assert.Null(row.ExceptionType);
    }

    [Fact]
    public async Task ReplayFromDeadLetter_NonDeadLetteredRow_NoOp()
    {
        // Guard: replay should only act on DeadLettered rows; calling it on a Processed row
        // should not corrupt state.
        await _fixture.ResetAsync();

        var inboundId = await _fixture.SeedInboundAsync();
        var outboundId = await _fixture.SeedOutboundAsync(inboundId, "endpoint-DLQ-4", OutboundMessageStatus.Processed, attemptCount: 3);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

        await repo.ReplayFromDeadLetter(outboundId);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        var row = await verify.Set<OutboundMessage>().AsNoTracking().FirstAsync(x => x.Id == outboundId);

        // Untouched.
        Assert.Equal(OutboundMessageStatus.Processed, row.Status);
        Assert.Equal(3, row.AttemptCount);
    }

}
