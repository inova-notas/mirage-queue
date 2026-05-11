using Microsoft.Extensions.DependencyInjection;
using MirageQueue.Consumers;
using MirageQueue.Consumers.Abstractions;
using MirageQueue.Retry;
using MirageQueue.Tests.Consumers.Fixtures;

namespace MirageQueue.Tests.Consumers;

public class AddConsumerWithPolicyTests
{
    public class PolicyAwareConsumer : IConsumer<PolicyAwareMessage>
    {
        public Task Process(PolicyAwareMessage message) => Task.CompletedTask;
    }

    public class PolicyAwareMessage
    {
        public Guid Id { get; set; }
    }

    [Fact]
    public void AddConsumerWithPolicy_RegistersScopedConsumer()
    {
        var services = new ServiceCollection();

        services.AddConsumer<NoOpConsumer>(p => p.MaxAttempts(5));
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<NoOpConsumer>());
    }

    [Fact]
    public void AddConsumerWithPolicy_SetsHasExplicitPolicyTrue()
    {
        var services = new ServiceCollection();
        services.AddConsumer<PolicyAwareConsumer>(p => p.MaxAttempts(7));

        var registered = DispatcherContext.Consumers.Single(c => c.ConsumerType == typeof(PolicyAwareConsumer));

        Assert.True(registered.HasExplicitPolicy);
        Assert.Equal(7, registered.RetryPolicy.MaxAttempts);
    }

    [Fact]
    public void AddConsumerWithPolicy_CapturesBackoffStrategy()
    {
        var services = new ServiceCollection();
        services.AddConsumer<PolicyAwareConsumer>(p => p
            .MaxAttempts(3)
            .ExponentialBackoff(TimeSpan.FromSeconds(2), factor: 3));

        var registered = DispatcherContext.Consumers.Single(c => c.ConsumerType == typeof(PolicyAwareConsumer));

        // ExponentialBackoff(2s, factor=3): attempt 1 → 2s, attempt 2 → 6s.
        Assert.Equal(TimeSpan.FromSeconds(2), registered.RetryPolicy.Backoff.ComputeDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(6), registered.RetryPolicy.Backoff.ComputeDelay(2));
    }

    [Fact]
    public void AddConsumer_WithoutPolicy_HasExplicitPolicyIsFalse()
    {
        // Use a fresh service collection but the static DispatcherContext is shared.
        // The contract: AddConsumer<T>() — no policy arg — yields HasExplicitPolicy = false.
        var services = new ServiceCollection();
        services.AddConsumer<NoOpConsumer>();

        var registered = DispatcherContext.Consumers.Single(c => c.ConsumerType == typeof(NoOpConsumer));
        Assert.False(registered.HasExplicitPolicy);
        Assert.Same(RetryPolicy.Default, registered.RetryPolicy);
    }

    [Fact]
    public void AddConsumerWithPolicy_NullConfigureThrows()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddConsumer<NoOpConsumer>(configurePolicy: null!));
    }
}
