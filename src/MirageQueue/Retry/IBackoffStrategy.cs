namespace MirageQueue.Retry;

public interface IBackoffStrategy
{
    /// <summary>
    /// Compute the delay before the next dispatch given the current attempt count
    /// (1 = compute delay before the second dispatch, etc.).
    /// </summary>
    TimeSpan ComputeDelay(int attemptCount);
}
