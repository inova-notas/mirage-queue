using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers.Abstractions;
using MirageQueue.Messages.Entities;

namespace MirageQueue.Consumers;

public class Dispatcher(IServiceProvider serviceProvider,
    ILogger<Dispatcher> logger)
{
    public List<DispatcherConsumer> Consumers => DispatcherContext.Consumers;

    public async Task ProcessOutboundMessage(OutboundMessage outboundMessage)
    {
        var consumer = Consumers?.FirstOrDefault(x => x.ConsumerEndpoint == outboundMessage.ConsumerEndpoint);
        if (consumer is null)
        {
            logger.LogWarning("No consumers found for endpoint {ConsumerEndpoint}", outboundMessage.ConsumerEndpoint);
            return;
        }
        
        await using var scope = serviceProvider.CreateAsyncScope();
        var consumerInstance = scope.ServiceProvider.GetRequiredService(consumer.ConsumerType);
        var processMethod = consumer.ConsumerType.GetMethod(nameof(IConsumer<BaseMessage>.Process));
        if (processMethod == null)
            throw new InvalidOperationException($"No process method found for consumer {consumer.ConsumerType.FullName}");

        var message = GetMessage(outboundMessage, consumer.MessageType);
        await (Task) processMethod.Invoke(consumerInstance, new[] {message});
    }

    private static object GetMessage(BaseMessage baseMessage, Type messageType)
    {
        var message = JsonSerializer.Deserialize(baseMessage.Content, messageType);
        if (message == null)
            throw new InvalidOperationException($"Failed to deserialize message {baseMessage.Content} to type {messageType.FullName}");

        return message;
    }
}