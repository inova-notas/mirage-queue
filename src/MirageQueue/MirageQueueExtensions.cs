using Microsoft.Extensions.DependencyInjection;
using MirageQueue.Consumers;
using MirageQueue.Consumers.Abstractions;
using MirageQueue.Publishers;
using MirageQueue.Publishers.Abstractions;
using System.Reflection;
using System.Threading.Channels;
using MirageQueue.Messages.Entities;
using MirageQueue.Workers;

namespace MirageQueue;

public static class MirageQueueExtensions
{
    private static readonly MirageQueueConfiguration Configuration = new MirageQueueConfiguration
    {
        PoolingScheduleTime = 1000,
        PoolingInboundTime = 500,
        PoolingOutboundTime = 500,
        WorkersQuantity = 20,
    };
    
    public static void AddMirageQueue(this IServiceCollection services)
    {
        services.AddSingleton<Dispatcher>();
        services.AddScoped<IMessageHandler, MessageHandler>();
        services.AddScoped<IPublisher, Publisher>();
        services.AddSingleton(Configuration);
        services.AddSingleton<Channel<OutboundMessage>>(_ => 
            Channel.CreateUnbounded<OutboundMessage>(new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = false,
                SingleWriter = true,
                SingleReader = false
            }));
        services.AddHostedService<ProcessOutboundMessagesWorker>();
    }
    
    public static void AddMirageQueue(this IServiceCollection services, Action<MirageQueueConfiguration> options)
    {
        options.Invoke(Configuration);
        services.AddMirageQueue();
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