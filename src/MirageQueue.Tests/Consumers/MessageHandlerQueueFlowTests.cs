using System.Threading.Channels;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using Moq;

namespace MirageQueue.Tests.Consumers;

public class MessageHandlerQueueFlowTests
{
    [Fact]
    public async Task HandleQueuedOutboundMessages_ShouldClaimUsingProvidedTokens()
    {
        var outboundRepository = new Mock<IOutboundMessageRepository>();
        var inboundRepository = new Mock<IInboundMessageRepository>();
        var scheduledRepository = new Mock<IScheduledMessageRepository>();
        var dispatcher = new Mock<Dispatcher>(Mock.Of<IServiceProvider>(), Mock.Of<ILogger<Dispatcher>>());
        var transaction = new Mock<IDbContextTransaction>();

        var tokens = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var claimed = new List<OutboundMessage>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Status = OutboundMessageStatus.Processing,
                ProcessingToken = tokens[0],
                ConsumerEndpoint = "consumer",
                InboundMessageId = Guid.NewGuid(),
                Content = "{}",
                MessageContract = "contract",
                CreateAt = DateTime.UtcNow
            }
        };

        outboundRepository
            .Setup(x => x.ClaimQueuedMessages(tokens, transaction.Object))
            .ReturnsAsync(claimed);

        var sut = new MessageHandler(
            Mock.Of<ILogger<MessageHandler>>(),
            outboundRepository.Object,
            inboundRepository.Object,
            scheduledRepository.Object,
            dispatcher.Object,
            new MirageQueueConfiguration(),
            Channel.CreateUnbounded<OutboundMessage>());

        var result = await sut.HandleQueuedOutboundMessages(tokens, transaction.Object);

        Assert.Single(result);
        Assert.Equal(tokens[0], result[0].ProcessingToken);
        outboundRepository.Verify(x => x.ClaimQueuedMessages(tokens, transaction.Object), Times.Once);
    }

    [Fact]
    public async Task HandleQueuedInboundMessages_WhenUpdateFails_ShouldRethrow()
    {
        var outboundRepository = new Mock<IOutboundMessageRepository>();
        var inboundRepository = new Mock<IInboundMessageRepository>();
        var scheduledRepository = new Mock<IScheduledMessageRepository>();
        var dispatcher = new Mock<Dispatcher>(Mock.Of<IServiceProvider>(), Mock.Of<ILogger<Dispatcher>>());
        var transaction = new Mock<IDbContextTransaction>();

        inboundRepository
            .Setup(x => x.GetQueuedMessages(transaction.Object, It.IsAny<int>()))
            .ReturnsAsync([
                new InboundMessage
                {
                    Id = Guid.NewGuid(),
                    Status = InboundMessageStatus.New,
                    Content = "{}",
                    MessageContract = "contract",
                    CreateAt = DateTime.UtcNow
                }
            ]);

        inboundRepository
            .Setup(x => x.UpdateMessageStatus(It.IsAny<Guid>(), InboundMessageStatus.Queued, transaction.Object))
            .ThrowsAsync(new InvalidOperationException("update failed"));

        var sut = new MessageHandler(
            Mock.Of<ILogger<MessageHandler>>(),
            outboundRepository.Object,
            inboundRepository.Object,
            scheduledRepository.Object,
            dispatcher.Object,
            new MirageQueueConfiguration { WorkersQuantity = 1 },
            Channel.CreateUnbounded<OutboundMessage>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.HandleQueuedInboundMessages(transaction.Object));
    }

    [Fact]
    public async Task HandleScheduledMessages_WhenUpdateFails_ShouldRethrow()
    {
        var outboundRepository = new Mock<IOutboundMessageRepository>();
        var inboundRepository = new Mock<IInboundMessageRepository>();
        var scheduledRepository = new Mock<IScheduledMessageRepository>();
        var dispatcher = new Mock<Dispatcher>(Mock.Of<IServiceProvider>(), Mock.Of<ILogger<Dispatcher>>());
        var transaction = new Mock<IDbContextTransaction>();

        scheduledRepository
            .Setup(x => x.GetScheduledMessages(transaction.Object, It.IsAny<int>()))
            .ReturnsAsync([
                new ScheduledInboundMessage
                {
                    Id = Guid.NewGuid(),
                    Status = ScheduledInboundMessageStatus.WaitingScheduledTime,
                    Content = "{}",
                    MessageContract = "contract",
                    ExecuteAt = DateTime.UtcNow,
                    CreateAt = DateTime.UtcNow
                }
            ]);

        scheduledRepository
            .Setup(x => x.UpdateMessageStatus(It.IsAny<Guid>(), ScheduledInboundMessageStatus.Queued, transaction.Object))
            .ThrowsAsync(new InvalidOperationException("scheduled failed"));

        var sut = new MessageHandler(
            Mock.Of<ILogger<MessageHandler>>(),
            outboundRepository.Object,
            inboundRepository.Object,
            scheduledRepository.Object,
            dispatcher.Object,
            new MirageQueueConfiguration { WorkersQuantity = 1 },
            Channel.CreateUnbounded<OutboundMessage>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.HandleScheduledMessages(transaction.Object));
    }
}
