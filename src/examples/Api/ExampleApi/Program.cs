using ExampleApi;
using Microsoft.AspNetCore.Mvc;
using MirageQueue;
using MirageQueue.Postgres;
using MirageQueue.Publishers.Abstractions;
using MirageQueue.Dashboard;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMirageQueue();

builder.Services.AddMirageQueuePostgres(builder.Configuration.GetConnectionString("DefaultConnection"));

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