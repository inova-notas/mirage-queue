using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MirageQueue.Messages.Entities;
using Polly;
using Polly.Retry;
using System.Text.Json;

namespace MirageQueue.Consumers;

public class Dispatcher(IServiceProvider serviceProvider,
    ILogger<Dispatcher> logger)
{
    public IEnumerable<DispatcherConsumer> Consumers => DispatcherContext.Consumers;

    private readonly AsyncRetryPolicy RetryPolicy = Policy.Handle<Exception>().RetryAsync(3);

    public async Task ProcessOutboundMessage(OutboundMessage outboundMessage)
    {
        var consumer = Consumers.FirstOrDefault(x => x.ConsumerEndpoint == outboundMessage.ConsumerEndpoint);
        if (consumer is null)
        {
            logger.LogWarning("No consumers found for endpoint {ConsumerEndpoint}", outboundMessage.ConsumerEndpoint);
            return;
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var consumerInstance = scope.ServiceProvider.GetRequiredService(consumer.ConsumerType);
        var processMethod = consumer.ConsumerType.GetMethod("Process");
        if (processMethod == null)
            throw new InvalidOperationException($"No process method found for consumer {consumer.ConsumerType.FullName}");

        var message = GetMessage(outboundMessage, consumer.MessageType);

        await RetryPolicy.ExecuteAsync(() => (Task?)processMethod.Invoke(consumerInstance, new[] { message }));
    }

    private static object GetMessage(BaseMessage baseMessage, Type messageType)
    {
        var message = JsonSerializer.Deserialize(baseMessage.Content, messageType);
        if (message == null)
            throw new InvalidOperationException($"Failed to deserialize message {baseMessage.Content} to type {messageType.FullName}");

        return message;
    }
}