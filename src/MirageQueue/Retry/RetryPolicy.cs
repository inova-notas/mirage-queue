using MirageQueue.Consumers;

namespace MirageQueue.Retry;

public sealed class RetryPolicy
{
    /// <summary>
    /// Max number of in-process retries within a single dispatch when a transient
    /// exception is thrown. Does not increment <c>AttemptCount</c> on the row.
    /// </summary>
    public int TransientAttempts { get; init; } = 3;

    /// <summary>
    /// Max number of dispatches (each picked-up-and-dispatched row counts as one).
    /// When <c>AttemptCount &gt;= MaxAttempts</c> after a failed dispatch, the row
    /// becomes terminal (DeadLettered or Failed depending on whether a policy
    /// was attached to the consumer).
    /// </summary>
    public int MaxAttempts { get; init; } = 1;

    public IBackoffStrategy Backoff { get; init; } = Retry.Backoff.None;

    public Func<Exception, bool> IsTransient { get; init; } = TransientClassifier.Default;

    /// <summary>
    /// The default policy used when a consumer is registered without an explicit
    /// retry configuration. Preserves the v2.6 observable behavior: up to 3
    /// in-process retries on transient exceptions, then a single failed dispatch
    /// terminates with <c>Status = Failed</c>.
    /// </summary>
    public static RetryPolicy Default { get; } = new();

    /// <summary>
    /// Resolve the retry policy + explicit-policy flag for a consumer endpoint by
    /// consulting <see cref="DispatcherContext"/>. Falls back to <see cref="Default"/>
    /// when no consumer is registered for the endpoint.
    /// </summary>
    internal static (RetryPolicy Policy, bool HasExplicitPolicy) Resolve(string consumerEndpoint)
    {
        if (DispatcherContext.TryGetConsumer(consumerEndpoint, out var consumer) && consumer is not null)
            return (consumer.RetryPolicy, consumer.HasExplicitPolicy);

        return (Default, HasExplicitPolicy: false);
    }
}
