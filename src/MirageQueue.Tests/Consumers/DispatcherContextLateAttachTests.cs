using Microsoft.Extensions.DependencyInjection;
using MirageQueue.Consumers;
using MirageQueue.Consumers.Abstractions;
using MirageQueue.Retry;

namespace MirageQueue.Tests.Consumers;

public class DispatcherContextLateAttachTests
{
    // Use unique consumer types per test to isolate from the shared static DispatcherContext.
    // Each test uses its own nested class definition.

    public class LateAttachMessage1 { }
    public class LateAttachConsumer1 : IConsumer<LateAttachMessage1>
    {
        public Task Process(LateAttachMessage1 message) => Task.CompletedTask;
    }

    public class LateAttachMessage2 { }
    public class LateAttachConsumer2 : IConsumer<LateAttachMessage2>
    {
        public Task Process(LateAttachMessage2 message) => Task.CompletedTask;
    }

    public class LateAttachMessage3 { }
    public class LateAttachConsumer3 : IConsumer<LateAttachMessage3>
    {
        public Task Process(LateAttachMessage3 message) => Task.CompletedTask;
    }

    [Fact]
    public void AddDispatchConsumer_FirstWithoutThenWithPolicy_UpdatesExistingRegistration()
    {
        // First call: no policy. Second call: explicit policy. The second call should mutate
        // the existing registration to attach the policy (assembly-scan + explicit-attach pattern).
        DispatcherContext.AddDispatchConsumer(typeof(LateAttachConsumer1));
        var beforePolicy = DispatcherContext.Consumers.Single(c => c.ConsumerType == typeof(LateAttachConsumer1));
        Assert.False(beforePolicy.HasExplicitPolicy);

        var explicitPolicy = new RetryPolicyBuilder().MaxAttempts(7).Build();
        DispatcherContext.AddDispatchConsumer(typeof(LateAttachConsumer1), explicitPolicy);

        var afterPolicy = DispatcherContext.Consumers.Single(c => c.ConsumerType == typeof(LateAttachConsumer1));
        Assert.True(afterPolicy.HasExplicitPolicy);
        Assert.Equal(7, afterPolicy.RetryPolicy.MaxAttempts);
        Assert.Same(beforePolicy, afterPolicy); // same instance, mutated in place
    }

    [Fact]
    public void AddDispatchConsumer_CalledTwiceWithoutPolicy_RemainsNoOp()
    {
        DispatcherContext.AddDispatchConsumer(typeof(LateAttachConsumer2));
        DispatcherContext.AddDispatchConsumer(typeof(LateAttachConsumer2)); // second call, no policy

        var registered = DispatcherContext.Consumers.Single(c => c.ConsumerType == typeof(LateAttachConsumer2));
        Assert.False(registered.HasExplicitPolicy);
        Assert.Same(RetryPolicy.Default, registered.RetryPolicy);
    }

    [Fact]
    public void AddDispatchConsumer_CalledTwiceWithDifferentPolicies_SecondOverwritesFirst()
    {
        var first = new RetryPolicyBuilder().MaxAttempts(2).Build();
        var second = new RetryPolicyBuilder().MaxAttempts(9).Build();

        DispatcherContext.AddDispatchConsumer(typeof(LateAttachConsumer3), first);
        DispatcherContext.AddDispatchConsumer(typeof(LateAttachConsumer3), second);

        var registered = DispatcherContext.Consumers.Single(c => c.ConsumerType == typeof(LateAttachConsumer3));
        Assert.True(registered.HasExplicitPolicy);
        Assert.Equal(9, registered.RetryPolicy.MaxAttempts);
    }
}
