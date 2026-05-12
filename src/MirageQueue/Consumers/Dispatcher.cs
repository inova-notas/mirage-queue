using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MirageQueue.Diagnostics;
using MirageQueue.Messages.Entities;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.Json;

namespace MirageQueue.Consumers;

public class Dispatcher(IServiceProvider serviceProvider,
    ILogger<Dispatcher> logger)
{
    public IEnumerable<DispatcherConsumer> Consumers => DispatcherContext.Consumers;

    private static readonly ConcurrentDictionary<Type, Func<object, object, Task>> ProcessDelegates = new();

    public async Task ProcessOutboundMessage(OutboundMessage outboundMessage)
    {
        if (!DispatcherContext.TryGetConsumer(outboundMessage.ConsumerEndpoint, out var consumer) || consumer is null)
        {
            logger.LogWarning("No consumers found for endpoint {ConsumerEndpoint}", outboundMessage.ConsumerEndpoint);
            return;
        }

        var parentContext = TryParseTraceContext(outboundMessage.TraceParent, outboundMessage.TraceState);
        using var activity = MirageQueueDiagnostics.ActivitySource.StartActivity(
            $"{MirageQueueDiagnostics.OperationProcess} {outboundMessage.ConsumerEndpoint}",
            ActivityKind.Consumer,
            parentContext);
        activity?.SetTag(MirageQueueDiagnostics.AttrMessagingSystem, MirageQueueDiagnostics.MessagingSystemValue);
        activity?.SetTag(MirageQueueDiagnostics.AttrMessagingOperation, MirageQueueDiagnostics.OperationProcess);
        activity?.SetTag(MirageQueueDiagnostics.AttrMessagingDestination, outboundMessage.ConsumerEndpoint);
        activity?.SetTag(MirageQueueDiagnostics.AttrMessagingMessageId, outboundMessage.InboundMessageId);
        activity?.SetTag(MirageQueueDiagnostics.AttrMessagingConsumerEndpoint, outboundMessage.ConsumerEndpoint);

        var startTs = Stopwatch.GetTimestamp();
        var tags = new TagList
        {
            { MirageQueueDiagnostics.AttrMessagingSystem, MirageQueueDiagnostics.MessagingSystemValue },
            { MirageQueueDiagnostics.AttrMessagingOperation, MirageQueueDiagnostics.OperationProcess },
            { MirageQueueDiagnostics.AttrMessagingDestination, outboundMessage.ConsumerEndpoint }
        };
        MirageQueueDiagnostics.QueueWaitDuration.Record((DateTime.UtcNow - outboundMessage.CreateAt).TotalSeconds, tags);

        await using var scope = serviceProvider.CreateAsyncScope();
        var consumerInstance = scope.ServiceProvider.GetRequiredService(consumer.ConsumerType);
        var message = GetMessage(outboundMessage, consumer.MessageType);
        var processDelegate = ProcessDelegates.GetOrAdd(consumer.ConsumerType, type => BuildProcessDelegate(type, consumer.MessageType));

        var policy = consumer.RetryPolicy;
        for (var attempt = 0; attempt <= policy.TransientAttempts; attempt++)
        {
            try
            {
                await processDelegate(consumerInstance, message);
                MirageQueueDiagnostics.ConsumedCounter.Add(1, tags);
                MirageQueueDiagnostics.ProcessDuration.Record(Stopwatch.GetElapsedTime(startTs).TotalSeconds, tags);
                return;
            }
            catch (Exception ex) when (attempt < policy.TransientAttempts && policy.IsTransient(ex))
            {
                activity?.AddEvent(new ActivityEvent("retry_attempt",
                    tags: new ActivityTagsCollection
                    {
                        { "exception.type", ex.GetType().FullName },
                        { "attempt", attempt + 1 },
                        { "max_attempts", policy.TransientAttempts }
                    }));
                logger.LogDebug(ex, "Transient failure dispatching {MessageId} to {ConsumerEndpoint} (in-process attempt {Attempt}/{TransientAttempts})",
                    outboundMessage.Id, consumer.ConsumerEndpoint, attempt + 1, policy.TransientAttempts);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);
                MirageQueueDiagnostics.ProcessDuration.Record(Stopwatch.GetElapsedTime(startTs).TotalSeconds, tags);
                throw;
            }
        }
    }

    private static ActivityContext TryParseTraceContext(string? traceParent, string? traceState)
    {
        if (string.IsNullOrEmpty(traceParent))
            return default;

        return ActivityContext.TryParse(traceParent, traceState, out var ctx) ? ctx : default;
    }

    private static object GetMessage(BaseMessage baseMessage, Type messageType)
    {
        var message = JsonSerializer.Deserialize(baseMessage.Content, messageType);
        if (message == null)
            throw new InvalidOperationException($"Failed to deserialize message {baseMessage.Content} to type {messageType.FullName}");

        return message;
    }

    private static Func<object, object, Task> BuildProcessDelegate(Type consumerType, Type messageType)
    {
        var processMethod = consumerType.GetMethod("Process", [messageType]);
        if (processMethod == null)
            throw new InvalidOperationException($"No process method found for consumer {consumerType.FullName}");

        if (!typeof(Task).IsAssignableFrom(processMethod.ReturnType))
            throw new InvalidOperationException($"Process method in {consumerType.FullName} must return Task");

        var consumerParameter = Expression.Parameter(typeof(object), "consumer");
        var messageParameter = Expression.Parameter(typeof(object), "message");

        var call = Expression.Call(
            Expression.Convert(consumerParameter, consumerType),
            processMethod,
            Expression.Convert(messageParameter, messageType));

        var convertResult = Expression.Convert(call, typeof(Task));
        return Expression.Lambda<Func<object, object, Task>>(convertResult, consumerParameter, messageParameter).Compile();
    }
}
