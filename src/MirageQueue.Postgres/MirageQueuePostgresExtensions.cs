using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MirageQueue.Consumers.Abstractions;
using MirageQueue.Databases;
using MirageQueue.Postgres.Consumers;
using MirageQueue.Publishers;
using MirageQueue.Publishers.Abstractions;

namespace MirageQueue.Postgres;

public static class MirageQueuePostgresExtensions
{
    public static void AddMirageQueuePostgres(this IServiceCollection services, Action<DbContextOptionsBuilder> options)
    {
        services.AddDbContext<MirageQueueDbContext>(options);
        services.AddScoped<IMessageHandler, PostgresMessageHandler>();
        services.AddScoped<IPublisher, Publisher>();
    }
}