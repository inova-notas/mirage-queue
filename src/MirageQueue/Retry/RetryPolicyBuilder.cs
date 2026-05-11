namespace MirageQueue.Retry;

public sealed class RetryPolicyBuilder
{
    private int _transientAttempts = 3;
    private int _maxAttempts = 3;
    private IBackoffStrategy _backoff = Backoff.Exponential(TimeSpan.FromSeconds(1), factor: 2.0, max: TimeSpan.FromMinutes(5));
    private Func<Exception, bool> _isTransient = TransientClassifier.Default;

    public RetryPolicyBuilder TransientAttempts(int attempts)
    {
        if (attempts < 0) throw new ArgumentOutOfRangeException(nameof(attempts), "Must be >= 0");
        _transientAttempts = attempts;
        return this;
    }

    public RetryPolicyBuilder MaxAttempts(int attempts)
    {
        if (attempts < 1) throw new ArgumentOutOfRangeException(nameof(attempts), "Must be >= 1");
        _maxAttempts = attempts;
        return this;
    }

    public RetryPolicyBuilder NoBackoff()
    {
        _backoff = Backoff.None;
        return this;
    }

    public RetryPolicyBuilder ConstantBackoff(TimeSpan delay)
    {
        _backoff = Backoff.Constant(delay);
        return this;
    }

    public RetryPolicyBuilder LinearBackoff(TimeSpan increment, TimeSpan? max = null)
    {
        _backoff = Backoff.Linear(increment, max);
        return this;
    }

    public RetryPolicyBuilder ExponentialBackoff(TimeSpan baseDelay, double factor = 2.0, TimeSpan? max = null)
    {
        _backoff = Backoff.Exponential(baseDelay, factor, max);
        return this;
    }

    public RetryPolicyBuilder UseBackoff(IBackoffStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        _backoff = strategy;
        return this;
    }

    /// <summary>
    /// Replace the transient classifier entirely. Use <see cref="TransientWhenAlso"/>
    /// to extend the default rather than replace it.
    /// </summary>
    public RetryPolicyBuilder TransientWhen(Func<Exception, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _isTransient = predicate;
        return this;
    }

    /// <summary>
    /// Extend (OR) the current transient classifier with an additional predicate.
    /// </summary>
    public RetryPolicyBuilder TransientWhenAlso(Func<Exception, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var current = _isTransient;
        _isTransient = ex => current(ex) || predicate(ex);
        return this;
    }

    internal RetryPolicy Build() => new()
    {
        TransientAttempts = _transientAttempts,
        MaxAttempts = _maxAttempts,
        Backoff = _backoff,
        IsTransient = _isTransient,
    };
}
