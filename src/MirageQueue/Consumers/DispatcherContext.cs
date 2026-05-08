using MirageQueue.Consumers.Abstractions;
using System.Reflection;
using System.Collections.Concurrent;

namespace MirageQueue.Consumers;

public static class DispatcherContext
{
    private static readonly object SyncRoot = new();
    private static readonly List<DispatcherConsumer> RegisteredConsumers = [];
    private static readonly ConcurrentDictionary<string, DispatcherConsumer> ConsumersByEndpoint = new(StringComparer.Ordinal);

    public static IReadOnlyCollection<DispatcherConsumer> Consumers => RegisteredConsumers;


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

    internal static void AddDispatchConsumer(Type consumerType)
    {
        var consumerEndpoint = consumerType.FullName!;

        lock (SyncRoot)
        {
            if (ConsumersByEndpoint.ContainsKey(consumerEndpoint))
                return;

            var interfaces = consumerType.GetInterfaces();

            var consumerInterfaces = interfaces
                .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IConsumer<>))
                .ToList();

            if (consumerInterfaces.Count == 0)
                throw new ArgumentException("Consumer must implement IConsumer<TMessage> interface", nameof(consumerType));

            var messageType = consumerInterfaces
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

            RegisteredConsumers.Add(consumer);
            ConsumersByEndpoint[consumerEndpoint] = consumer;
        }
    }

    public static bool TryGetConsumer(string consumerEndpoint, out DispatcherConsumer? consumer)
    {
        return ConsumersByEndpoint.TryGetValue(consumerEndpoint, out consumer);
    }
}
