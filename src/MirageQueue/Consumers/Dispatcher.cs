using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers.Abstractions;
using MirageQueue.Messages.Entities;

namespace MirageQueue.Consumers;

public class Dispatcher(IServiceProvider serviceProvider,
    ILogger<Dispatcher> logger)
{
    public List<DispatcherConsumer> Consumers { get; } = [];

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
    
    public void AddDispatchConsumer(Type consumerType)
    {
        var consumerEndpoint = consumerType.FullName!;

        if (Consumers.Any(x => x.ConsumerEndpoint == consumerEndpoint))
            throw new ArgumentException($"Consumer with endpoint {consumerEndpoint} already registered",
                nameof(consumerType));
        
        var interfaces = consumerType.GetInterfaces();
        
        if(interfaces.All(x => x.GetGenericTypeDefinition() != typeof(IConsumer<>)))
            throw new ArgumentException("Consumer must implement IConsumer<TMessage> interface", nameof(consumerType));
        
        var messageType = interfaces
            .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IConsumer<>))
            .Select(x => x.GetGenericArguments()[0])
            .First();

        var messageContract = messageType.FullName!;
        
        var consumer = new DispatcherConsumer
        {
            MessageContract = messageContract,
            ConsumerEndpoint = consumerEndpoint,
            ConsumerType = consumerType,
            MessageType = messageType
        };
        
        Consumers.Add(consumer);
    }

    private static object GetMessage(BaseMessage baseMessage, Type messageType)
    {
        var message = JsonSerializer.Deserialize(baseMessage.Content, messageType);
        if (message == null)
            throw new InvalidOperationException($"Failed to deserialize message {baseMessage.Content} to type {messageType.FullName}");

        return message;
    }
    
    public class DispatcherConsumer
    {
        public required string MessageContract { get; set; }
        public required string ConsumerEndpoint { get; set; }
        public required Type ConsumerType { get; set; }
        public required Type MessageType { get; set; }
    }
}