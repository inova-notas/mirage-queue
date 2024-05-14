using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MirageQueue.Messages.Repositories;
using MirageQueue.Postgres.Databases;
using MirageQueue.Postgres.Workers;

namespace MirageQueue.Postgres;

public static class MirageQueuePostgresExtensions
{
    public static void AddMirageQueuePostgres(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        services.AddDbContext<MirageQueueDbContext>(options =>
        {
            options.UseNpgsql(connectionString, x =>
            {
                x.MigrationsHistoryTable(HistoryRepository.DefaultTableName, "mirage_queue");
                x.MigrationsAssembly(typeof(MirageQueueDbContext).Assembly.FullName);
            });
        });
        services.AddHostedService<PgOutMessageWorker>();
        services.AddHostedService<PgInMessageWorker>();
        services.AddHostedService<PgScheduledMessageWorker>();
        services.AddScoped<IInboundMessageRepository, InboundMessageRepository>();
        services.AddScoped<IOutboundMessageRepository, OutboundMessageRepository>();
        services.AddScoped<IScheduledMessageRepository, ScheduledMessageRepository>();
    }
    
     
    
    public static void UseMirageQueue(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MirageQueueDbContext>();
        dbContext.Database.Migrate();
    }
}