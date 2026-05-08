using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MirageQueue.Outbox;

public static class MirageQueueOutboxExtensions
{
    public static IServiceCollection AddMirageQueueOutbox<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddScoped<IDbContextOutbox<TDbContext>, DbContextOutbox<TDbContext>>();
        return services;
    }
}
