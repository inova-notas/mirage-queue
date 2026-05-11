using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Messages.Repositories;
using MirageQueue.Publishers;
using Moq;

namespace MirageQueue.Tests.Publishers;

public class PublisherKeyValidationTests
{
    public class KeyValidationMessage
    {
        public Guid Id { get; set; }
    }

    [Fact]
    public async Task Publish_NullIdempotencyKey_Throws()
    {
        var publisher = BuildPublisher();
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            publisher.Publish(new KeyValidationMessage { Id = Guid.NewGuid() }, idempotencyKey: null!));
    }

    [Fact]
    public async Task Publish_EmptyIdempotencyKey_Throws()
    {
        var publisher = BuildPublisher();
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            publisher.Publish(new KeyValidationMessage { Id = Guid.NewGuid() }, idempotencyKey: string.Empty));
    }

    [Fact]
    public async Task Schedule_NullIdempotencyKey_Throws()
    {
        var publisher = BuildPublisher();
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            publisher.Schedule(new KeyValidationMessage { Id = Guid.NewGuid() }, DateTime.UtcNow.AddMinutes(5), idempotencyKey: null!));
    }

    [Fact]
    public async Task Schedule_EmptyIdempotencyKey_Throws()
    {
        var publisher = BuildPublisher();
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            publisher.Schedule(new KeyValidationMessage { Id = Guid.NewGuid() }, DateTime.UtcNow.AddMinutes(5), idempotencyKey: string.Empty));
    }

    [Fact]
    public async Task Publish_WithTransactionButNullKey_Throws()
    {
        var publisher = BuildPublisher();
        var tx = Mock.Of<DbTransaction>();
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            publisher.Publish(new KeyValidationMessage { Id = Guid.NewGuid() }, idempotencyKey: null!, transaction: tx));
    }

    [Fact]
    public async Task Publish_NullMessage_Throws()
    {
        var publisher = BuildPublisher();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            publisher.Publish<KeyValidationMessage>(message: null!, idempotencyKey: "k"));
    }

    [Fact]
    public async Task Publish_NullTransaction_Throws()
    {
        var publisher = BuildPublisher();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            publisher.Publish(new KeyValidationMessage { Id = Guid.NewGuid() }, idempotencyKey: "k", transaction: null!));
    }

    private static Publisher BuildPublisher() => new(
        Mock.Of<IInboundMessageRepository>(),
        Mock.Of<IScheduledMessageRepository>());
}
