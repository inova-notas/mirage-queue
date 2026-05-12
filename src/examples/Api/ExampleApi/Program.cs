using ExampleApi;
using ExampleApi.Business;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MirageQueue;
using MirageQueue.Dashboard;
using MirageQueue.Diagnostics;
using MirageQueue.Outbox;
using MirageQueue.Postgres;
using MirageQueue.Publishers.Abstractions;
using MirageQueue.Retry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

// --- MirageQueue core + Postgres provider ----------------------------------------------------
// Phase 3.5 cleanup is enabled with demo-friendly knobs so retention behavior is observable
// without waiting days. In production, leave CleanupPollingTime at the daily default.
builder.Services.AddMirageQueue(options =>
{
    options.CleanupEnabled = true;
    options.MessageRetentionDays = 7;
    options.CleanupPollingTime = (int)TimeSpan.FromMinutes(15).TotalMilliseconds;
});

builder.Services.AddMirageQueuePostgres(connectionString);

// --- Phase 3: explicit retry policy on the failing consumer ---------------------------------
// AddConsumersFromAssembly registers everything in the assembly with default policies.
// Following it with AddConsumer<T>(policy) attaches an explicit policy to that consumer
// (DispatcherContext dedups on consumer type and accepts a policy override).
builder.Services.AddConsumersFromAssembly(typeof(TestMessageConsumer).Assembly);
builder.Services.AddConsumer<FailingMessageConsumer>(p => p
    .MaxAttempts(3)
    .TransientAttempts(1)
    .ExponentialBackoff(baseDelay: TimeSpan.FromSeconds(2), factor: 2.0, max: TimeSpan.FromMinutes(1)));

// --- Phase 1: business DbContext + transactional outbox -------------------------------------
builder.Services.AddDbContext<BusinessDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddMirageQueueOutbox<BusinessDbContext>();

// --- Phase 4: OpenTelemetry traces + metrics ------------------------------------------------
// MirageQueue exposes itself via AddMirageQueueInstrumentation on both providers; traces
// go to the console (swap for AddOtlpExporter to ship to a collector) and metrics go to
// Prometheus via /metrics. Add the OpenTelemetry.Instrumentation.AspNetCore package and
// call .AddAspNetCoreInstrumentation() on both builders to also capture HTTP traces/metrics.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("ExampleApi"))
    .WithTracing(t => t
        .AddMirageQueueInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(m => m
        .AddMirageQueueInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddMirageQueueDashboard();

var app = builder.Build();

// Ensure the business schema exists before serving requests. EnsureCreatedAsync is fine for
// a sample without migrations; in real apps you'd use Add-Migration + Migrate instead.
await using (var scope = app.Services.CreateAsyncScope())
{
    var business = scope.ServiceProvider.GetRequiredService<BusinessDbContext>();
    await business.Database.EnsureCreatedAsync();
}

app.UseHttpsRedirection();
app.UseRouting();

// --- Endpoints -----------------------------------------------------------------------------

app.MapPost("/publish", async ([FromServices] IPublisher publisher) =>
{
    await publisher.Publish(new TestMessage { Id = Guid.NewGuid() });
    return Results.Ok();
});

app.MapPost("/publish-failing", async ([FromServices] IPublisher publisher) =>
{
    await publisher.Publish(new FailingMessage { Id = Guid.NewGuid() });
    return Results.Ok("Failing message published. Retry policy: 3 attempts with exponential backoff. After exhaustion the row is dead-lettered — visible in the dashboard.");
});

// Phase 2: idempotent publish. Repeated calls with the same key produce one inbound row;
// PublishResult.IsDuplicate is true on subsequent calls.
app.MapPost("/publish-idempotent", async (
    [FromServices] IPublisher publisher,
    [FromQuery] string key) =>
{
    ArgumentException.ThrowIfNullOrWhiteSpace(key);

    var result = await publisher.Publish(new TestMessage { Id = Guid.NewGuid() }, key);
    return Results.Ok(new
    {
        MessageId = result.MessageId,
        IsDuplicate = result.IsDuplicate,
        Notes = result.IsDuplicate
            ? "Key already seen — no new inbound row was inserted."
            : "First time this key was published."
    });
});

// Phase 1: transactional outbox. The business write and the queue publish share one
// transaction — either both commit or neither does, so there's no ghost-publish window.
app.MapPost("/publish-outbox", async (
    [FromServices] BusinessDbContext db,
    [FromServices] IDbContextOutbox<BusinessDbContext> outbox,
    [FromQuery] string? name) =>
{
    db.BusinessEntities.Add(new BusinessEntity
    {
        Id = Guid.NewGuid(),
        Name = name ?? "anonymous",
        CreatedAt = DateTime.UtcNow,
    });
    outbox.Publish(new TestMessage { Id = Guid.NewGuid() });

    await outbox.SaveChangesAndFlushMessagesAsync();
    return Results.Ok("Business row + queued message committed atomically.");
});

app.MapPost("/pressure-test", async (
    [FromServices] IServiceScopeFactory scopeFactory,
    [FromServices] ILoggerFactory loggerFactory,
    [FromQuery] int count = 1000,
    [FromQuery] int parallelism = 50) =>
{
    if (count <= 0)
        return Results.BadRequest("count must be greater than zero");

    if (parallelism <= 0)
        return Results.BadRequest("parallelism must be greater than zero");

    var logger = loggerFactory.CreateLogger("PressureTest");
    logger.LogInformation("Starting pressure test with {Count} messages and parallelism {Parallelism}", count, parallelism);

    var semaphore = new SemaphoreSlim(parallelism, parallelism);
    var tasks = new List<Task>(count);
    var start = DateTime.UtcNow;

    for (var i = 0; i < count; i++)
    {
        await semaphore.WaitAsync();
        tasks.Add(Task.Run(async () =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
                await publisher.Publish(new PressureTestMessage { Id = Guid.NewGuid() });
            }
            finally
            {
                semaphore.Release();
            }
        }));
    }

    await Task.WhenAll(tasks);

    var elapsed = DateTime.UtcNow - start;
    logger.LogInformation("Pressure test published {Count} messages in {ElapsedMs} ms", count, (long)elapsed.TotalMilliseconds);

    return Results.Ok(new
    {
        PublishedMessages = count,
        Parallelism = parallelism,
        ElapsedMilliseconds = (long)elapsed.TotalMilliseconds,
        Notes = "PressureTestMessageConsumer adds delay to simulate outbound channel pressure"
    });
});

app.MapPost("/schedule", async ([FromServices] IPublisher publisher) =>
{
    await publisher.Schedule(new TestMessage { Id = Guid.NewGuid() }, DateTime.UtcNow.AddSeconds(3));
    Console.WriteLine($"Scheduled at {DateTime.Now:hh:mm:ss}");
    return Results.Ok();
});

// Prometheus scrape endpoint (Phase 4). MirageQueue's meter + ASP.NET Core's meter both
// flow through the same MeterProvider, so /metrics will expose messaging.client.published.messages,
// messaging.process.duration, mirage_queue.queue.wait.duration, etc.
app.MapPrometheusScrapingEndpoint();

app.MapMirageQueueDashboard();
app.UseMirageQueue();

app.Run();
