using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using MirageQueue.Consumers;
using MirageQueue.Consumers.Abstractions;
using MirageQueue.IntegrationTests.Fixtures;
using MirageQueue.Messages.Entities;
using MirageQueue.Postgres.Databases;
using MirageQueue.Retry;
using Npgsql;
using Xunit;

namespace MirageQueue.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class MessageHandlerFanOutTests
{
    private readonly PostgresFixture _fixture;

    public MessageHandlerFanOutTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public class FanOutMessage
    {
        public Guid Id { get; set; }
    }

    public class FanOutConsumerA : IConsumer<FanOutMessage>
    {
        public Task Process(FanOutMessage message) => Task.CompletedTask;
    }

    public class FanOutConsumerB : IConsumer<FanOutMessage>
    {
        public Task Process(FanOutMessage message) => Task.CompletedTask;
    }

    [Fact]
    public async Task HandleQueuedInboundMessages_OneConsumer_CreatesOneOutboundRow()
    {
        await _fixture.ResetAsync();

        // Unique contract per test so the static DispatcherContext from prior tests
        // doesn't accidentally fan out to extra consumers.
        var contract = $"FanOut.Single.{Guid.NewGuid():N}";
        var endpointA = RegisterConsumerStub<FanOutConsumerA>(contract);

        var inboundId = await SeedNewInboundAsync(contract);

        await InvokeHandleQueuedInboundAsync();

        await using var verify = _fixture.CreateMirageQueueDbContext();
        var outbound = await verify.Set<OutboundMessage>().AsNoTracking()
            .Where(x => x.InboundMessageId == inboundId)
            .ToListAsync();

        Assert.Single(outbound);
        Assert.Equal(endpointA, outbound[0].ConsumerEndpoint);

        var inbound = await verify.Set<InboundMessage>().AsNoTracking().FirstAsync(x => x.Id == inboundId);
        Assert.Equal(InboundMessageStatus.Queued, inbound.Status);
    }

    [Fact]
    public async Task HandleQueuedInboundMessages_TwoConsumersForSameContract_FansOutToBoth()
    {
        await _fixture.ResetAsync();

        var contract = $"FanOut.Two.{Guid.NewGuid():N}";
        var endpointA = RegisterConsumerStub<FanOutConsumerA>(contract);
        var endpointB = RegisterConsumerStub<FanOutConsumerB>(contract);

        var inboundId = await SeedNewInboundAsync(contract);

        await InvokeHandleQueuedInboundAsync();

        await using var verify = _fixture.CreateMirageQueueDbContext();
        var outbound = await verify.Set<OutboundMessage>().AsNoTracking()
            .Where(x => x.InboundMessageId == inboundId)
            .OrderBy(x => x.ConsumerEndpoint)
            .ToListAsync();

        Assert.Equal(2, outbound.Count);
        var endpoints = outbound.Select(x => x.ConsumerEndpoint).ToList();
        Assert.Contains(endpointA, endpoints);
        Assert.Contains(endpointB, endpoints);
    }

    [Fact]
    public async Task HandleQueuedInboundMessages_NoMatchingConsumer_MarksInboundQueuedWithNoOutbound()
    {
        await _fixture.ResetAsync();

        // No consumer registered for this contract.
        var orphanContract = $"Orphan.Contract.{Guid.NewGuid():N}";
        var inboundId = await SeedNewInboundAsync(orphanContract);

        await InvokeHandleQueuedInboundAsync();

        await using var verify = _fixture.CreateMirageQueueDbContext();
        Assert.Equal(0, await verify.Set<OutboundMessage>().CountAsync(x => x.InboundMessageId == inboundId));

        var inbound = await verify.Set<InboundMessage>().AsNoTracking().FirstAsync(x => x.Id == inboundId);
        Assert.Equal(InboundMessageStatus.Queued, inbound.Status);
    }

    private async Task InvokeHandleQueuedInboundAsync()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MirageQueueDbContext>();
        await using var tx = await dbContext.Database.BeginTransactionAsync();
        await handler.HandleQueuedInboundMessages(tx);
        await dbContext.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private async Task<Guid> SeedNewInboundAsync(string messageContract)
    {
        var id = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO mirage_queue."InboundMessage"
                ("Id", "Status", "Content", "MessageContract", "CreateAt", "UpdateAt")
            VALUES (@id, 0, '{}'::jsonb, @contract, now(), now())
            """;
        cmd.Parameters.Add(new NpgsqlParameter("id", id));
        cmd.Parameters.Add(new NpgsqlParameter("contract", messageContract));
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    private static string RegisterConsumerStub<TConsumer>(string messageContract)
    {
        var endpoint = $"{typeof(TConsumer).FullName}#{Guid.NewGuid():N}";

        var consumersByEndpointField = typeof(DispatcherContext).GetField("ConsumersByEndpoint",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var registeredConsumersField = typeof(DispatcherContext).GetField("RegisteredConsumers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var consumer = new DispatcherConsumer
        {
            MessageContract = messageContract,
            ConsumerEndpoint = endpoint,
            ConsumerType = typeof(TConsumer),
            MessageType = typeof(FanOutMessage),
            RetryPolicy = RetryPolicy.Default,
            HasExplicitPolicy = false,
        };

        var byEndpoint = consumersByEndpointField!.GetValue(null)!;
        var registered = (List<DispatcherConsumer>)registeredConsumersField!.GetValue(null)!;
        byEndpoint.GetType().GetMethod("TryAdd")!.Invoke(byEndpoint, new object[] { endpoint, consumer });
        registered.Add(consumer);

        return endpoint;
    }
}
