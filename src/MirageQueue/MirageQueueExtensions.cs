using Microsoft.Extensions.DependencyInjection;

namespace MirageQueue;

public static class MirageQueueExtensions
{
    private static readonly MirageQueueConfiguration Configuration = new MirageQueueConfiguration
    {
        PoolingTime = 10
    };
    public static void AddMirageQueue(this IServiceCollection services, Action<MirageQueueConfiguration> options)
    {
        options.Invoke(Configuration);
    }
}