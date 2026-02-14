using System.Reflection;
using System.Threading.Channels;
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
        Assert.IsType<TargetInvocationException>(result.Exception);
        Assert.NotNull(result.Exception.InnerException);
        Assert.IsType<InvalidOperationException>(result.Exception.InnerException);
        Assert.Contains("Simulated consumer failure", result.Exception.InnerException.Message);
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
}
