using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MirageQueue.IntegrationTests.Fixtures;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using Npgsql;
using Xunit;

namespace MirageQueue.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class FanOutDedupTests
{
    private readonly PostgresFixture _fixture;

    public FanOutDedupTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task InsertOutboundIfNotExists_FirstCall_ReturnsTrueAndPersistsRow()
    {
        await _fixture.ResetAsync();
        var inboundId = await _fixture.SeedInboundAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var outboundRepo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

        var inserted = await outboundRepo.InsertIfNotExists(BuildOutbound(inboundId, "endpoint-A"));

        Assert.True(inserted);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(1, await verify.Set<OutboundMessage>().CountAsync());
    }

    [Fact]
    public async Task InsertOutboundIfNotExists_SameInboundAndConsumer_SecondCallReturnsFalseAndPersistsNothingNew()
    {
        await _fixture.ResetAsync();
        var inboundId = await _fixture.SeedInboundAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var outboundRepo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

        var first = await outboundRepo.InsertIfNotExists(BuildOutbound(inboundId, "endpoint-A"));
        var second = await outboundRepo.InsertIfNotExists(BuildOutbound(inboundId, "endpoint-A"));

        Assert.True(first);
        Assert.False(second);

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(1, await verify.Set<OutboundMessage>().CountAsync());
    }

    [Fact]
    public async Task InsertOutboundIfNotExists_SameInboundDifferentConsumers_ProducesTwoRows()
    {
        await _fixture.ResetAsync();
        var inboundId = await _fixture.SeedInboundAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var outboundRepo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

        Assert.True(await outboundRepo.InsertIfNotExists(BuildOutbound(inboundId, "endpoint-A")));
        Assert.True(await outboundRepo.InsertIfNotExists(BuildOutbound(inboundId, "endpoint-B")));

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(2, await verify.Set<OutboundMessage>().CountAsync());
    }

    [Fact]
    public async Task InsertOutboundIfNotExists_ParallelInsertsForSameInboundConsumer_OneWinsOneLoses()
    {
        await _fixture.ResetAsync();
        var inboundId = await _fixture.SeedInboundAsync();

        // Race two scoped repositories with their own DbContext + connection.
        async Task<bool> RaceOne()
        {
            await using var scope = _fixture.Services.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();
            return await repo.InsertIfNotExists(BuildOutbound(inboundId, "endpoint-race"));
        }

        var (a, b) = await TaskRunInParallelAsync(RaceOne, RaceOne);

        // Exactly one of the two should report inserted=true.
        Assert.True(a ^ b, $"Expected exactly one true; got a={a}, b={b}");

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(1, await verify.Set<OutboundMessage>().CountAsync());
    }

    private static OutboundMessage BuildOutbound(Guid inboundId, string consumerEndpoint) => new()
    {
        Id = Guid.NewGuid(),
        Status = OutboundMessageStatus.New,
        ConsumerEndpoint = consumerEndpoint,
        InboundMessageId = inboundId,
        Content = "{}",
        MessageContract = "Some.Contract",
        CreateAt = DateTime.UtcNow,
        UpdateAt = null,
    };

    private static async Task<(T a, T b)> TaskRunInParallelAsync<T>(Func<Task<T>> a, Func<Task<T>> b)
    {
        var ta = Task.Run(a);
        var tb = Task.Run(b);
        await Task.WhenAll(ta, tb);
        return (ta.Result, tb.Result);
    }
}
