namespace MirageQueue.Retry;

public sealed class NoBackoff : IBackoffStrategy
{
    public static readonly NoBackoff Instance = new();
    public TimeSpan ComputeDelay(int attemptCount) => TimeSpan.Zero;
}

public sealed class ConstantBackoff(TimeSpan delay) : IBackoffStrategy
{
    public TimeSpan ComputeDelay(int attemptCount) => delay;
}

public sealed class LinearBackoff(TimeSpan increment, TimeSpan? max = null) : IBackoffStrategy
{
    public TimeSpan ComputeDelay(int attemptCount)
    {
        var raw = TimeSpan.FromTicks(increment.Ticks * Math.Max(1, attemptCount));
        return max is { } cap && raw > cap ? cap : raw;
    }
}

public sealed class ExponentialBackoff(TimeSpan baseDelay, double factor = 2.0, TimeSpan? max = null) : IBackoffStrategy
{
    public TimeSpan ComputeDelay(int attemptCount)
    {
        var exponent = Math.Max(0, attemptCount - 1);
        var ticks = baseDelay.Ticks * Math.Pow(factor, exponent);
        // Guard against overflow when ticks exceeds long.MaxValue.
        if (double.IsInfinity(ticks) || ticks > long.MaxValue)
            return max ?? TimeSpan.FromTicks(long.MaxValue);

        var raw = TimeSpan.FromTicks((long)ticks);
        return max is { } cap && raw > cap ? cap : raw;
    }
}

public static class Backoff
{
    public static IBackoffStrategy None => NoBackoff.Instance;
    public static IBackoffStrategy Constant(TimeSpan delay) => new ConstantBackoff(delay);
    public static IBackoffStrategy Linear(TimeSpan increment, TimeSpan? max = null) => new LinearBackoff(increment, max);
    public static IBackoffStrategy Exponential(TimeSpan baseDelay, double factor = 2.0, TimeSpan? max = null) => new ExponentialBackoff(baseDelay, factor, max);
}
