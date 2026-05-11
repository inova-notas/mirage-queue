using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MirageQueue.Consumers;
using MirageQueue.Messages.Entities;
using MirageQueue.Retry;
using MirageQueue.Tests.Consumers.Fixtures;

namespace MirageQueue.Tests.Retry;

public class DispatcherTransientRetryTests
{
    [Fact]
    public async Task ProcessOutboundMessage_TransientFailureWithinBudget_RetriesAndSucceeds()
    {
        // Consumer fails 2 transient times, succeeds on the 3rd attempt.
        // Policy allows up to 3 transient attempts → should succeed.
        var counter = new TransientCounter { FailuresRemaining = 2 };
        var (dispatcher, message) = BuildDispatcherForTransient(counter, transientAttempts: 3);

        await dispatcher.ProcessOutboundMessage(message);

        Assert.Equal(0, counter.FailuresRemaining); // all failures consumed
        Assert.Equal(1, counter.SuccessCount);
    }

    [Fact]
    public async Task ProcessOutboundMessage_TransientFailuresExceedBudget_PropagatesException()
    {
        // Consumer fails 5 transient times. Policy allows 3 transient attempts
        // (4 dispatches total: initial + 3 retries). All exhaust → propagate.
        var counter = new TransientCounter { FailuresRemaining = 10 };
        var (dispatcher, message) = BuildDispatcherForTransient(counter, transientAttempts: 3);

        await Assert.ThrowsAsync<TimeoutException>(() => dispatcher.ProcessOutboundMessage(message));

        // 1 initial + 3 retries = 4 calls; each decremented FailuresRemaining by 1.
        Assert.Equal(10 - 4, counter.FailuresRemaining);
    }

    [Fact]
    public async Task ProcessOutboundMessage_NonTransientException_NoRetryPropagatesImmediately()
    {
        var counter = new InvocationCounter();
        var (dispatcher, message) = BuildDispatcherForNonTransient(counter, transientAttempts: 3);

        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.ProcessOutboundMessage(message));

        // Non-transient classified → no in-process retry → exactly one invocation.
        Assert.Equal(1, counter.Invocations);
    }

    [Fact]
    public async Task ProcessOutboundMessage_TransientAttemptsZero_NoRetryEvenForTransient()
    {
        var counter = new TransientCounter { FailuresRemaining = 1 };
        var (dispatcher, message) = BuildDispatcherForTransient(counter, transientAttempts: 0);

        await Assert.ThrowsAsync<TimeoutException>(() => dispatcher.ProcessOutboundMessage(message));

        // TransientAttempts=0 → one attempt, no retry → only one failure decrement.
        Assert.Equal(0, counter.FailuresRemaining);
    }

    private static (Dispatcher, OutboundMessage) BuildDispatcherForTransient(TransientCounter counter, int transientAttempts)
    {
        // Use a unique endpoint per test to isolate from the static DispatcherContext.
        var endpoint = $"{typeof(TransientFailingConsumer).FullName}#{Guid.NewGuid():N}";
        RegisterFakeConsumer<TransientFailingConsumer, TransientFailingMessage>(endpoint, new RetryPolicy
        {
            TransientAttempts = transientAttempts,
            MaxAttempts = 1,
            Backoff = Backoff.None,
        });

        var services = new ServiceCollection();
        services.AddSingleton(counter);
        services.AddScoped<TransientFailingConsumer>();
        services.AddSingleton<Dispatcher>();
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<Dispatcher>>(NullLogger<Dispatcher>.Instance);
        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<Dispatcher>();

        var message = new OutboundMessage
        {
            Id = Guid.NewGuid(),
            Status = OutboundMessageStatus.Processing,
            ConsumerEndpoint = endpoint,
            InboundMessageId = Guid.NewGuid(),
            Content = JsonSerializer.Serialize(new TransientFailingMessage()),
            MessageContract = typeof(TransientFailingMessage).FullName!,
            CreateAt = DateTime.UtcNow,
        };

        return (dispatcher, message);
    }

    private static (Dispatcher, OutboundMessage) BuildDispatcherForNonTransient(InvocationCounter counter, int transientAttempts)
    {
        var endpoint = $"{typeof(NonTransientFailingConsumer).FullName}#{Guid.NewGuid():N}";
        RegisterFakeConsumer<NonTransientFailingConsumer, NonTransientFailingMessage>(endpoint, new RetryPolicy
        {
            TransientAttempts = transientAttempts,
            MaxAttempts = 1,
            Backoff = Backoff.None,
        });

        var services = new ServiceCollection();
        services.AddSingleton(counter);
        services.AddScoped<NonTransientFailingConsumer>();
        services.AddSingleton<Dispatcher>();
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<Dispatcher>>(NullLogger<Dispatcher>.Instance);
        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<Dispatcher>();

        var message = new OutboundMessage
        {
            Id = Guid.NewGuid(),
            Status = OutboundMessageStatus.Processing,
            ConsumerEndpoint = endpoint,
            InboundMessageId = Guid.NewGuid(),
            Content = JsonSerializer.Serialize(new NonTransientFailingMessage()),
            MessageContract = typeof(NonTransientFailingMessage).FullName!,
            CreateAt = DateTime.UtcNow,
        };

        return (dispatcher, message);
    }

    private static void RegisterFakeConsumer<TConsumer, TMessage>(string endpoint, RetryPolicy policy)
    {
        // Reach into DispatcherContext's static state to register a consumer at a unique endpoint
        // so tests can run in parallel without colliding on the shared registry.
        var consumersByEndpointField = typeof(DispatcherContext).GetField("ConsumersByEndpoint",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var registeredConsumersField = typeof(DispatcherContext).GetField("RegisteredConsumers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var consumer = new DispatcherConsumer
        {
            MessageContract = typeof(TMessage).FullName!,
            ConsumerEndpoint = endpoint,
            ConsumerType = typeof(TConsumer),
            MessageType = typeof(TMessage),
            RetryPolicy = policy,
            HasExplicitPolicy = true,
        };

        var byEndpoint = consumersByEndpointField!.GetValue(null)!;
        var registered = (List<DispatcherConsumer>)registeredConsumersField!.GetValue(null)!;
        byEndpoint.GetType().GetMethod("TryAdd")!.Invoke(byEndpoint, new object[] { endpoint, consumer });
        registered.Add(consumer);
    }
}
