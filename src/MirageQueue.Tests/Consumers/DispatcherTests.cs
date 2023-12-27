using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers;
using MirageQueue.Messages.Entities;
using MirageQueue.Tests.Consumers.Fixtures;
using Moq;

namespace MirageQueue.Tests.Consumers;

public class DispatcherTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Mock<IDummyService> _dummyService;
    public DispatcherTests()
    {
        var services = new ServiceCollection();
        
        var logger = new Mock<ILogger<Dispatcher>>();
        _dummyService = new Mock<IDummyService>();
        
        services.AddSingleton(logger.Object);
        services.AddSingleton(_dummyService.Object);
        services.AddSingleton<Dispatcher>();
        DispatcherContext.MapFromAssembly(typeof(DummyConsumer).Assembly, type => services.AddScoped(type));

        _serviceProvider = services.BuildServiceProvider();
    }
    
    [Fact]
    public async Task ShouldCallConsumerDispatch()
    {
        var sut = _serviceProvider.GetRequiredService<Dispatcher>();
        ;

        var messageContent = JsonSerializer.Serialize(new DummyMessage
        {
            Id = Guid.NewGuid()
        });
        
        await sut.ProcessOutboundMessage(new OutboundMessage
        {
            Content = messageContent,
            Id = Guid.NewGuid(),
            Status = OutboundMessageStatus.New,
            ConsumerEndpoint = typeof(DummyConsumer).FullName,
            MessageContract = typeof(DummyMessage).FullName,
            CreateAt = DateTime.Now,
            InboundMessageId = Guid.NewGuid()
        });
        
        _dummyService.Verify(x => x.DoSomething(), Times.Once);
    }
}