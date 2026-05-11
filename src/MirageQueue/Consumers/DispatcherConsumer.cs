using MirageQueue.Retry;

namespace MirageQueue.Consumers;

public class DispatcherConsumer
{
    public required string MessageContract { get; set; }
    public required string ConsumerEndpoint { get; set; }
    public required Type ConsumerType { get; set; }
    public required Type MessageType { get; set; }

    /// <summary>
    /// The retry policy used for this consumer's dispatches. Defaults to
    /// <see cref="RetryPolicy.Default"/> (no persisted retry; terminal = Failed).
    /// </summary>
    public RetryPolicy RetryPolicy { get; set; } = RetryPolicy.Default;

    /// <summary>
    /// True when the consumer was registered with an explicit retry policy.
    /// Drives the terminal-status decision: explicit policy → DeadLettered,
    /// no policy → Failed (preserves pre-Phase-3 behavior).
    /// </summary>
    public bool HasExplicitPolicy { get; set; }
}