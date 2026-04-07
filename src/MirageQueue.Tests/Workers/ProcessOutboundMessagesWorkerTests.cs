using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers.Abstractions;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using MirageQueue.Workers;
using Moq;

namespace MirageQueue.Tests.Workers;

public class ProcessOutboundMessagesWorkerTests
{
    [Fact]
    public async Task Worker_WhenProcessingFails_ShouldCallUpdateWithErrorDetails()
    {
        var exception = new InvalidOperationException("Test failure");
        var messageId = Guid.NewGuid();

        var channel = Channel.CreateUnbounded<OutboundMessage>();
        var messageHandler = new Mock<IMessageHandler>();
        var outboundRepo = new Mock<IOutboundMessageRepository>();
        var logger = new Mock<ILogger<OutboundMessageHandlerWorker>>();
        var configuration = new MirageQueueConfiguration { WorkersQuantity = 1, HeartbeatIntervalInMilliseconds = 50 };

        messageHandler
            .Setup(x => x.ProcessOutboundMessage(It.IsAny<OutboundMessage>()))
            .ReturnsAsync((messageId, OutboundMessageStatus.Failed, (Exception)exception));

        var services = new ServiceCollection();
        services.AddScoped(_ => messageHandler.Object);
        services.AddScoped(_ => outboundRepo.Object);
        var serviceProvider = services.BuildServiceProvider();

        var worker = new ProcessOutboundMessagesWorker(serviceProvider, logger.Object, configuration, channel);

        var message = new OutboundMessage
        {
            Id = messageId,
            Content = "{}",
            Status = OutboundMessageStatus.Processing,
            ProcessingToken = Guid.NewGuid(),
            ConsumerEndpoint = "TestEndpoint",
            MessageContract = "TestContract",
            CreateAt = DateTime.UtcNow,
            InboundMessageId = Guid.NewGuid()
        };

        await channel.Writer.WriteAsync(message);
        channel.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await worker.StopAsync(CancellationToken.None);

        outboundRepo.Verify(x => x.TryUpdateMessageStatus(
            messageId,
            message.ProcessingToken!.Value,
            OutboundMessageStatus.Failed,
            exception.Message,
            exception.ToString(),
            exception.GetType().FullName), Times.Once);
    }

    [Fact]
    public async Task Worker_WhenProcessingSucceeds_ShouldCallUpdateWithProcessedStatus()
    {
        var messageId = Guid.NewGuid();

        var channel = Channel.CreateUnbounded<OutboundMessage>();
        var messageHandler = new Mock<IMessageHandler>();
        var outboundRepo = new Mock<IOutboundMessageRepository>();
        var logger = new Mock<ILogger<OutboundMessageHandlerWorker>>();
        var configuration = new MirageQueueConfiguration { WorkersQuantity = 1, HeartbeatIntervalInMilliseconds = 50 };

        messageHandler
            .Setup(x => x.ProcessOutboundMessage(It.IsAny<OutboundMessage>()))
            .ReturnsAsync((messageId, OutboundMessageStatus.Processed, (Exception?)null));

        var services = new ServiceCollection();
        services.AddScoped(_ => messageHandler.Object);
        services.AddScoped(_ => outboundRepo.Object);
        var serviceProvider = services.BuildServiceProvider();

        var worker = new ProcessOutboundMessagesWorker(serviceProvider, logger.Object, configuration, channel);

        var message = new OutboundMessage
        {
            Id = messageId,
            Content = "{}",
            Status = OutboundMessageStatus.Processing,
            ProcessingToken = Guid.NewGuid(),
            ConsumerEndpoint = "TestEndpoint",
            MessageContract = "TestContract",
            CreateAt = DateTime.UtcNow,
            InboundMessageId = Guid.NewGuid()
        };

        await channel.Writer.WriteAsync(message);
        channel.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await worker.StopAsync(CancellationToken.None);

        outboundRepo.Verify(x => x.TryUpdateMessageStatus(
            messageId,
            message.ProcessingToken!.Value,
            OutboundMessageStatus.Processed), Times.Once);
    }

    [Fact]
    public async Task Worker_WhenHeartbeatLosesOwnership_ShouldStopUpdatingHeartbeat()
    {
        var messageId = Guid.NewGuid();
        var processingToken = Guid.NewGuid();

        var channel = Channel.CreateUnbounded<OutboundMessage>();
        var messageHandler = new Mock<IMessageHandler>();
        var outboundRepo = new Mock<IOutboundMessageRepository>();
        var logger = new Mock<ILogger<OutboundMessageHandlerWorker>>();
        var configuration = new MirageQueueConfiguration { WorkersQuantity = 1, HeartbeatIntervalInMilliseconds = 10 };

        messageHandler
            .Setup(x => x.ProcessOutboundMessage(It.IsAny<OutboundMessage>()))
            .Returns(async () =>
            {
                await Task.Delay(50);
                return (messageId, OutboundMessageStatus.Processed, (Exception?)null);
            });

        outboundRepo
            .SetupSequence(x => x.TryUpdateHeartbeat(messageId, processingToken))
            .ReturnsAsync(false);

        outboundRepo
            .Setup(x => x.TryUpdateMessageStatus(messageId, processingToken, OutboundMessageStatus.Processed))
            .ReturnsAsync(true);

        var services = new ServiceCollection();
        services.AddScoped(_ => messageHandler.Object);
        services.AddScoped(_ => outboundRepo.Object);
        var serviceProvider = services.BuildServiceProvider();

        var worker = new ProcessOutboundMessagesWorker(serviceProvider, logger.Object, configuration, channel);

        var message = new OutboundMessage
        {
            Id = messageId,
            Content = "{}",
            Status = OutboundMessageStatus.Processing,
            ProcessingToken = processingToken,
            ConsumerEndpoint = "TestEndpoint",
            MessageContract = "TestContract",
            CreateAt = DateTime.UtcNow,
            InboundMessageId = Guid.NewGuid()
        };

        await channel.Writer.WriteAsync(message);
        channel.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StartAsync(cts.Token);
        await Task.Delay(300);
        await worker.StopAsync(CancellationToken.None);

        outboundRepo.Verify(x => x.TryUpdateHeartbeat(messageId, processingToken), Times.Once);
    }
}
