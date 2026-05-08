using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MirageQueue.IntegrationTests.Fixtures;
using MirageQueue.Messages.Repositories;
using MirageQueue.Outbox;
using MirageQueue.Postgres.Databases;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Xunit;

namespace MirageQueue.IntegrationTests;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("mirage_queue_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private Respawner? _respawner;
    private ServiceProvider? _services;

    public string ConnectionString => _container.GetConnectionString();

    public ServiceProvider Services => _services ?? throw new InvalidOperationException("Fixture not initialized");

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Apply MirageQueue migrations first (real migration history).
        await using var miragequeueDbContext = CreateMirageQueueDbContext();
        await miragequeueDbContext.Database.MigrateAsync();

        // The business DbContext has no migrations — generate DDL from its model and apply it.
        // EnsureCreatedAsync() can't be used here because the DB already has the mirage_queue schema
        // from the migration above; EnsureCreated would short-circuit and skip the business tables.
        await using (var businessDbContext = CreateBusinessDbContext())
        {
            var script = businessDbContext.Database.GenerateCreateScript();
            await businessDbContext.Database.ExecuteSqlRawAsync(script);
        }

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = new[] { "mirage_queue", SampleBusinessDbContext.SchemaName }
        });

        _services = BuildServices();
    }

    public async Task DisposeAsync()
    {
        if (_services is not null)
            await _services.DisposeAsync();

        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    public async Task ResetAsync()
    {
        if (_respawner is null)
            throw new InvalidOperationException("Fixture not initialized");

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    public MirageQueueDbContext CreateMirageQueueDbContext()
    {
        var options = new DbContextOptionsBuilder<MirageQueueDbContext>()
            .UseNpgsql(ConnectionString, x =>
            {
                x.MigrationsHistoryTable(
                    Microsoft.EntityFrameworkCore.Migrations.HistoryRepository.DefaultTableName,
                    "mirage_queue");
                x.MigrationsAssembly(typeof(MirageQueueDbContext).Assembly.FullName);
            })
            .Options;

        return new MirageQueueDbContext(options);
    }

    public SampleBusinessDbContext CreateBusinessDbContext()
    {
        var options = new DbContextOptionsBuilder<SampleBusinessDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new SampleBusinessDbContext(options);
    }

    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // Core: register publisher + dispatcher etc. without hosted services so the workers don't race the tests.
        services.AddMirageQueue();
        services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();

        // Provider: DbContext + repositories without the Postgres hosted workers.
        services.AddDbContext<MirageQueueDbContext>(o => o.UseNpgsql(ConnectionString, x =>
        {
            x.MigrationsHistoryTable(
                Microsoft.EntityFrameworkCore.Migrations.HistoryRepository.DefaultTableName,
                "mirage_queue");
            x.MigrationsAssembly(typeof(MirageQueueDbContext).Assembly.FullName);
        }));
        services.AddScoped<IInboundMessageRepository, InboundMessageRepository>();
        services.AddScoped<IOutboundMessageRepository, OutboundMessageRepository>();
        services.AddScoped<IScheduledMessageRepository, ScheduledMessageRepository>();

        // Sample business DbContext + outbox.
        services.AddDbContext<SampleBusinessDbContext>(o => o.UseNpgsql(ConnectionString));
        services.AddMirageQueueOutbox<SampleBusinessDbContext>();

        return services.BuildServiceProvider();
    }
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres collection";
}
