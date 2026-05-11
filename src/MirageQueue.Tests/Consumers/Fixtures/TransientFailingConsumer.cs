using MirageQueue.Consumers.Abstractions;

namespace MirageQueue.Tests.Consumers.Fixtures;

/// <summary>
/// Test consumer that throws <see cref="TimeoutException"/> (classified as transient
/// by the default classifier) for the first N invocations, then succeeds. The counter
/// is injected so tests can configure how many transient failures to simulate.
/// </summary>
public class TransientFailingConsumer(TransientCounter counter) : IConsumer<TransientFailingMessage>
{
    public Task Process(TransientFailingMessage message)
    {
        if (counter.FailuresRemaining > 0)
        {
            counter.FailuresRemaining--;
            throw new TimeoutException("simulated transient failure");
        }

        counter.SuccessCount++;
        return Task.CompletedTask;
    }
}

public class TransientCounter
{
    public int FailuresRemaining { get; set; }
    public int SuccessCount { get; set; }
}

public class NonTransientFailingMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

/// <summary>
/// Test consumer that always throws <see cref="InvalidOperationException"/> (NOT
/// transient under the default classifier). Used to verify the dispatcher does
/// not retry non-transient exceptions.
/// </summary>
public class NonTransientFailingConsumer(InvocationCounter counter) : IConsumer<NonTransientFailingMessage>
{
    public Task Process(NonTransientFailingMessage message)
    {
        counter.Invocations++;
        throw new InvalidOperationException("simulated non-transient failure");
    }
}

public class InvocationCounter
{
    public int Invocations { get; set; }
}
