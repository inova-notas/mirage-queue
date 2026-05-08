using Microsoft.Extensions.DependencyInjection;
using MirageQueue.Consumers;
using MirageQueue.Tests.Consumers.Fixtures;

namespace MirageQueue.Tests.Consumers;

public class MirageQueueExtensionsTests
{
    [Fact]
    public void AddConsumer_RegistersDispatcherMetadata()
    {
        var services = new ServiceCollection();

        services.AddConsumer<NoOpConsumer>();

        Assert.Contains(DispatcherContext.Consumers, c => c.ConsumerType == typeof(NoOpConsumer));
    }

    [Fact]
    public void AddConsumer_RegistersScopedConsumerInDi()
    {
        var services = new ServiceCollection();
        services.AddConsumer<NoOpConsumer>();
        var provider = services.BuildServiceProvider();

        var consumer = provider.GetRequiredService<NoOpConsumer>();

        Assert.NotNull(consumer);
    }

    [Fact]
    public void AddConsumer_CalledTwice_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddConsumer<NoOpConsumer>();

        var exception = Record.Exception(() => services.AddConsumer<NoOpConsumer>());

        Assert.Null(exception);
    }

    [Fact]
    public void AddConsumer_CalledTwice_RegistersDispatcherMetadataOnce()
    {
        var services = new ServiceCollection();

        services.AddConsumer<NoOpConsumer>();
        services.AddConsumer<NoOpConsumer>();

        var registrations = DispatcherContext.Consumers.Count(c => c.ConsumerType == typeof(NoOpConsumer));
        Assert.Equal(1, registrations);
    }
}
