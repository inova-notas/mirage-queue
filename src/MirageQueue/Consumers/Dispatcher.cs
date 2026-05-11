using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MirageQueue.Messages.Entities;
using System.Collections.Concurrent;
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
                return;
            }
            catch (Exception ex) when (attempt < policy.TransientAttempts && policy.IsTransient(ex))
            {
                logger.LogDebug(ex, "Transient failure dispatching {MessageId} to {ConsumerEndpoint} (in-process attempt {Attempt}/{TransientAttempts})",
                    outboundMessage.Id, consumer.ConsumerEndpoint, attempt + 1, policy.TransientAttempts);
            }
        }
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
