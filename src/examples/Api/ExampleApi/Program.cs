using ExampleApi;
using Microsoft.AspNetCore.Mvc;
using MirageQueue;
using MirageQueue.Postgres;
using MirageQueue.Publishers.Abstractions;
using MirageQueue.Dashboard;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMirageQueue();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

builder.Services.AddMirageQueuePostgres(connectionString);

builder.Services.AddConsumersFromAssembly(typeof(TestMessageConsumer).Assembly);

// Add the dashboard
builder.Services.AddMirageQueueDashboard();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseRouting();

app.MapPost("/publish", async ([FromServices] IPublisher publisher) =>
{
    await publisher.Publish(new TestMessage
    {
        Id = Guid.NewGuid()
    });

    return Results.Ok();
});

app.MapPost("/publish-failing", async ([FromServices] IPublisher publisher) =>
{
    await publisher.Publish(new FailingMessage
    {
        Id = Guid.NewGuid()
    });

    return Results.Ok("Failing message published. Check the dashboard for error details.");
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
    await publisher.Schedule(new TestMessage
    {
        Id = Guid.NewGuid()
    },
        DateTime.UtcNow.AddSeconds(3));

    Console.WriteLine($"Scheduled at {DateTime.Now:hh:mm:ss}");
    return Results.Ok();
});

// Map the dashboard endpoints
app.MapMirageQueueDashboard();

app.UseMirageQueue();

app.Run();
