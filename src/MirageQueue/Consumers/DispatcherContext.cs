using System.Reflection;
using MirageQueue.Consumers.Abstractions;

namespace MirageQueue.Consumers;

public static class DispatcherContext
{
    public static List<DispatcherConsumer> Consumers { get; } = [];


    public static void MapFromAssembly(Assembly assembly, Action<Type> addConsumer)
    {
        var consumerTypes = assembly.GetTypes()
            .Where(x => x is { IsClass: true, IsAbstract: false } && typeof(IConsumer).IsAssignableFrom(x));

        foreach (var consumerType in consumerTypes)
        {
            AddDispatchConsumer(consumerType);
            addConsumer?.Invoke(consumerType);
        }
    }

    private static void AddDispatchConsumer(Type consumerType)
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
}