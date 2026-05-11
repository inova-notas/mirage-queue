using Microsoft.EntityFrameworkCore;
using MirageQueue.Retry;

namespace MirageQueue.Tests.Retry;

public class RetryPolicyBuilderTests
{
    [Fact]
    public void Default_PreservesV26ObservableBehavior()
    {
        var def = RetryPolicy.Default;

        Assert.Equal(3, def.TransientAttempts);
        Assert.Equal(1, def.MaxAttempts);
        Assert.Equal(TimeSpan.Zero, def.Backoff.ComputeDelay(1));
    }

    [Fact]
    public void Builder_DefaultsAreSensibleForExplicitPolicies()
    {
        var policy = new RetryPolicyBuilder().Build();

        Assert.Equal(3, policy.TransientAttempts);
        Assert.Equal(3, policy.MaxAttempts);
        // Default explicit-policy backoff is exponential, not zero.
        Assert.True(policy.Backoff.ComputeDelay(1) > TimeSpan.Zero);
    }

    [Fact]
    public void Builder_MaxAttempts_RejectsLessThanOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicyBuilder().MaxAttempts(0));
    }

    [Fact]
    public void Builder_TransientAttempts_RejectsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicyBuilder().TransientAttempts(-1));
    }

    [Fact]
    public void NoBackoff_AlwaysZero()
    {
        var b = Backoff.None;
        Assert.Equal(TimeSpan.Zero, b.ComputeDelay(1));
        Assert.Equal(TimeSpan.Zero, b.ComputeDelay(100));
    }

    [Fact]
    public void ConstantBackoff_ReturnsSameDelay()
    {
        var b = Backoff.Constant(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromSeconds(2), b.ComputeDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(2), b.ComputeDelay(5));
    }

    [Fact]
    public void LinearBackoff_ScalesWithAttempt()
    {
        var b = Backoff.Linear(TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(1), b.ComputeDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(2), b.ComputeDelay(2));
        Assert.Equal(TimeSpan.FromSeconds(3), b.ComputeDelay(3));
    }

    [Fact]
    public void LinearBackoff_RespectsMax()
    {
        var b = Backoff.Linear(TimeSpan.FromSeconds(1), max: TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromSeconds(1), b.ComputeDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(2), b.ComputeDelay(2));
        Assert.Equal(TimeSpan.FromSeconds(2), b.ComputeDelay(10));
    }

    [Fact]
    public void ExponentialBackoff_DoublesByDefault()
    {
        var b = Backoff.Exponential(TimeSpan.FromSeconds(1));

        Assert.Equal(TimeSpan.FromSeconds(1), b.ComputeDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(2), b.ComputeDelay(2));
        Assert.Equal(TimeSpan.FromSeconds(4), b.ComputeDelay(3));
        Assert.Equal(TimeSpan.FromSeconds(8), b.ComputeDelay(4));
    }

    [Fact]
    public void ExponentialBackoff_RespectsMax()
    {
        var b = Backoff.Exponential(TimeSpan.FromSeconds(1), factor: 2, max: TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(1), b.ComputeDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(2), b.ComputeDelay(2));
        Assert.Equal(TimeSpan.FromSeconds(4), b.ComputeDelay(3));
        Assert.Equal(TimeSpan.FromSeconds(5), b.ComputeDelay(4));   // capped
        Assert.Equal(TimeSpan.FromSeconds(5), b.ComputeDelay(100)); // still capped, no overflow
    }

    [Fact]
    public void ExponentialBackoff_HandlesLargeAttemptCountsWithoutOverflow()
    {
        var b = Backoff.Exponential(TimeSpan.FromSeconds(1), factor: 2, max: TimeSpan.FromMinutes(5));
        var delay = b.ComputeDelay(1000);
        Assert.Equal(TimeSpan.FromMinutes(5), delay);
    }

    [Fact]
    public void TransientClassifier_TimeoutIsTransient()
    {
        Assert.True(TransientClassifier.Default(new TimeoutException()));
    }

    [Fact]
    public void TransientClassifier_DbConcurrencyIsTransient()
    {
        Assert.True(TransientClassifier.Default(new DbUpdateConcurrencyException("simulated")));
    }

    [Fact]
    public void TransientClassifier_CancellationIsNotTransient()
    {
        Assert.False(TransientClassifier.Default(new OperationCanceledException()));
        Assert.False(TransientClassifier.Default(new TaskCanceledException()));
    }

    [Fact]
    public void TransientClassifier_UnknownExceptionIsNotTransient()
    {
        Assert.False(TransientClassifier.Default(new InvalidOperationException("bad data")));
        Assert.False(TransientClassifier.Default(new NullReferenceException()));
    }

    [Fact]
    public void TransientClassifier_UnwrapsInnerException()
    {
        var wrapped = new InvalidOperationException("wrapper", new TimeoutException());
        Assert.True(TransientClassifier.Default(wrapped));
    }

    [Fact]
    public void Builder_TransientWhen_OverridesDefault()
    {
        var policy = new RetryPolicyBuilder()
            .TransientWhen(ex => ex is InvalidOperationException)
            .Build();

        Assert.True(policy.IsTransient(new InvalidOperationException()));
        Assert.False(policy.IsTransient(new TimeoutException())); // default no longer applies
    }

    [Fact]
    public void Builder_TransientWhenAlso_ExtendsDefault()
    {
        var policy = new RetryPolicyBuilder()
            .TransientWhenAlso(ex => ex is InvalidOperationException)
            .Build();

        Assert.True(policy.IsTransient(new InvalidOperationException())); // new predicate
        Assert.True(policy.IsTransient(new TimeoutException()));           // default still kicks in
    }
}
