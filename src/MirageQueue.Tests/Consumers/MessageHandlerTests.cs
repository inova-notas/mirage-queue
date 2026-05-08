using System.Reflection;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers;
using MirageQueue.Consumers.Abstractions;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using MirageQueue.Tests.Consumers.Fixtures;
using Moq;
using System.Text.Json;

namespace MirageQueue.Tests.Consumers;

public class MessageHandlerTests
{
    private static void EnsureConsumersRegistered()
    {
        // DispatcherContext is global static — may already be registered from other test classes
        if (DispatcherContext.Consumers.Any(c => c.ConsumerEndpoint == typeof(FailingConsumer).FullName))
            return;

        try
        {
            DispatcherContext.MapFromAssembly(typeof(FailingConsumer).Assembly, _ => { });
        }
        catch (ArgumentException)
        {
            // Already registered by another test class
        }
    }

    [Fact]
    public async Task ProcessOutboundMessage_WhenConsumerThrows_ShouldReturnExceptionDetails()
    {
        EnsureConsumersRegistered();

        var services = new ServiceCollection();
        services.AddSingleton(new Mock<ILogger<Dispatcher>>().Object);
        services.AddSingleton<Dispatcher>();
        services.AddScoped<FailingConsumer>();
        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = serviceProvider.GetRequiredService<Dispatcher>();

        var channel = Channel.CreateUnbounded<OutboundMessage>();
        var sut = new MessageHandler(
            new Mock<ILogger<MessageHandler>>().Object,
            new Mock<IOutboundMessageRepository>().Object,
            new Mock<IInboundMessageRepository>().Object,
            new Mock<IScheduledMessageRepository>().Object,
            dispatcher,
            new MirageQueueConfiguration(),
            channel);

        var message = new OutboundMessage
        {
            Id = Guid.NewGuid(),
            Content = JsonSerializer.Serialize(new DummyMessage { Id = Guid.NewGuid() }),
            Status = OutboundMessageStatus.Processing,
            ConsumerEndpoint = typeof(FailingConsumer).FullName!,
            MessageContract = typeof(DummyMessage).FullName!,
            CreateAt = DateTime.UtcNow,
            InboundMessageId = Guid.NewGuid()
        };

        var result = await sut.ProcessOutboundMessage(message);

        Assert.Equal(OutboundMessageStatus.Failed, result.Status);
        Assert.NotNull(result.Exception);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.Contains("Simulated consumer failure", result.Exception.Message);
        Assert.Contains("Simulated consumer failure", result.Exception.ToString());
    }

    [Fact]
    public async Task ProcessOutboundMessage_WhenConsumerSucceeds_ShouldReturnNullException()
    {
        EnsureConsumersRegistered();

        var dummyService = new Mock<IDummyService>();
        var services = new ServiceCollection();
        services.AddSingleton(new Mock<ILogger<Dispatcher>>().Object);
        services.AddSingleton(dummyService.Object);
        services.AddSingleton<Dispatcher>();
        services.AddScoped<DummyConsumer>();
        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = serviceProvider.GetRequiredService<Dispatcher>();

        var channel = Channel.CreateUnbounded<OutboundMessage>();
        var sut = new MessageHandler(
            new Mock<ILogger<MessageHandler>>().Object,
            new Mock<IOutboundMessageRepository>().Object,
            new Mock<IInboundMessageRepository>().Object,
            new Mock<IScheduledMessageRepository>().Object,
            dispatcher,
            new MirageQueueConfiguration(),
            channel);

        var message = new OutboundMessage
        {
            Id = Guid.NewGuid(),
            Content = JsonSerializer.Serialize(new DummyMessage { Id = Guid.NewGuid() }),
            Status = OutboundMessageStatus.Processing,
            ConsumerEndpoint = typeof(DummyConsumer).FullName!,
            MessageContract = typeof(DummyMessage).FullName!,
            CreateAt = DateTime.UtcNow,
            InboundMessageId = Guid.NewGuid()
        };

        var result = await sut.ProcessOutboundMessage(message);

        Assert.Equal(OutboundMessageStatus.Processed, result.Status);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task HandleQueuedInboundMessages_WhenConversionThrows_ShouldPropagateException()
    {
        var transaction = Mock.Of<IDbContextTransaction>();
        var inboundMessage = new InboundMessage
        {
            Id = Guid.NewGuid(),
            Content = JsonSerializer.Serialize(new DummyMessage { Id = Guid.NewGuid() }),
            MessageContract = typeof(DummyMessage).FullName!,
            Status = InboundMessageStatus.New,
            CreateAt = DateTime.UtcNow,
            UpdateAt = DateTime.UtcNow
        };

        var inboundRepo = new Mock<IInboundMessageRepository>();
        inboundRepo
            .Setup(x => x.GetQueuedMessages(It.IsAny<IDbContextTransaction>(), It.IsAny<int>()))
            .ReturnsAsync(new List<InboundMessage> { inboundMessage });
        inboundRepo
            .Setup(x => x.UpdateMessageStatus(It.IsAny<Guid>(), It.IsAny<InboundMessageStatus>(), It.IsAny<IDbContextTransaction>()))
            .ThrowsAsync(new InvalidOperationException("simulated repository failure"));

        var outboundRepo = new Mock<IOutboundMessageRepository>();
        outboundRepo
            .Setup(x => x.Any(It.IsAny<System.Linq.Expressions.Expression<Func<OutboundMessage, bool>>>(), It.IsAny<IDbContextTransaction>()))
            .ReturnsAsync(false);

        var services = new ServiceCollection();
        services.AddSingleton(new Mock<ILogger<Dispatcher>>().Object);
        services.AddSingleton<Dispatcher>();
        var dispatcher = services.BuildServiceProvider().GetRequiredService<Dispatcher>();

        var sut = new MessageHandler(
            new Mock<ILogger<MessageHandler>>().Object,
            outboundRepo.Object,
            inboundRepo.Object,
            new Mock<IScheduledMessageRepository>().Object,
            dispatcher,
            new MirageQueueConfiguration(),
            Channel.CreateUnbounded<OutboundMessage>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.HandleQueuedInboundMessages(transaction));
        Assert.Equal("simulated repository failure", ex.Message);
    }

    [Fact]
    public async Task HandleScheduledMessages_WhenConversionThrows_ShouldPropagateException()
    {
        var transaction = Mock.Of<IDbContextTransaction>();
        var scheduledMessage = new ScheduledInboundMessage
        {
            Id = Guid.NewGuid(),
            Content = JsonSerializer.Serialize(new DummyMessage { Id = Guid.NewGuid() }),
            MessageContract = typeof(DummyMessage).FullName!,
            Status = ScheduledInboundMessageStatus.WaitingScheduledTime,
            ExecuteAt = DateTime.UtcNow,
            CreateAt = DateTime.UtcNow,
            UpdateAt = DateTime.UtcNow
        };

        var scheduledRepo = new Mock<IScheduledMessageRepository>();
        scheduledRepo
            .Setup(x => x.GetScheduledMessages(It.IsAny<IDbContextTransaction>(), It.IsAny<int>()))
            .ReturnsAsync(new List<ScheduledInboundMessage> { scheduledMessage });
        scheduledRepo
            .Setup(x => x.UpdateMessageStatus(It.IsAny<Guid>(), It.IsAny<ScheduledInboundMessageStatus>(), It.IsAny<IDbContextTransaction>()))
            .ThrowsAsync(new InvalidOperationException("simulated scheduled failure"));

        var services = new ServiceCollection();
        services.AddSingleton(new Mock<ILogger<Dispatcher>>().Object);
        services.AddSingleton<Dispatcher>();
        var dispatcher = services.BuildServiceProvider().GetRequiredService<Dispatcher>();

        var sut = new MessageHandler(
            new Mock<ILogger<MessageHandler>>().Object,
            new Mock<IOutboundMessageRepository>().Object,
            new Mock<IInboundMessageRepository>().Object,
            scheduledRepo.Object,
            dispatcher,
            new MirageQueueConfiguration(),
            Channel.CreateUnbounded<OutboundMessage>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.HandleScheduledMessages(transaction));
        Assert.Equal("simulated scheduled failure", ex.Message);
    }
}
