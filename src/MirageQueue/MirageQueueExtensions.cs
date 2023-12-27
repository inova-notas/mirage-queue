using Microsoft.Extensions.DependencyInjection;
using MirageQueue.Consumers;
using MirageQueue.Consumers.Abstractions;
using MirageQueue.Publishers;
using MirageQueue.Publishers.Abstractions;
using System.Reflection;

namespace MirageQueue;

public static class MirageQueueExtensions
{
    private static readonly MirageQueueConfiguration Configuration = new MirageQueueConfiguration
    {
        PoolingTime = 10,
        WorkersAmount = 5
    };
    public static void AddMirageQueue(this IServiceCollection services, Action<MirageQueueConfiguration> options)
    {
        options.Invoke(Configuration);
        services.AddSingleton<Dispatcher>();
        services.AddScoped<IMessageHandler, MessageHandler>();
        services.AddScoped<IPublisher, Publisher>();
        services.AddSingleton(Configuration);
    }

    public static void AddConsumer<TConsumer>(this IServiceCollection services) where TConsumer : class, IConsumer
    {
        services.AddScoped<TConsumer>();
    }

    public static void AddConsumersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        DispatcherContext.MapFromAssembly(assembly, type => services.AddScoped(type));
    }
}