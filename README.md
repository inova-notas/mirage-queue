# Mirage Queue

This library was intended to help us have the benefits of a message broker without having another infrastructure dependency, meaning it was designed to use a database as a message broker.

###  Currently this library only supports PostgreSQL database 

You can download the package via Nuget using the command below or the Nuget interface in the IDE.

``` shell
dotnet add package MirageQueue.Postgres
```

The initial is pretty simple as you can see:
``` csharp
using MirageQueue;
using MirageQueue.Postgres;
using MirageQueue.Publishers.Abstractions;

var builder = WebApplication.CreateBuilder(args);


//Configure the default options for MirageQueue
builder.Services.AddMirageQueue();

//Configure Mirage Queue to use the postgres database 
builder.Services.AddMirageQueuePostgres(builder.Configuration.GetConnectionString("DefaultConnection"));

//Register all consumers in the given assembly
builder.Services.AddConsumersFromAssembly(typeof(TestMessageConsumer).Assembly);


var app = builder.Build();

// Create the database and all tables needed to run the Mirage Queue
app.UseMirageQueue();

app.Run();
```

Instead of registering all consumers from a specific assembly you can off course register one by one like this:

``` csharp
builder.Services.AddConsumer<TestMessageConsumer>();
```

To create a consumer you need to implement the interface **IConsumer** with the type parameter of the expected message that you want to receive, and you can have multiple consumers receiving the same message type.

``` csharp
using MirageQueue.Consumers.Abstractions;
using System.Text.Json;

namespace ExampleApi;

public class TestMessageConsumer : IConsumer<TestMessage>
{
    private readonly Random _random = new Random();
    public async Task Process(TestMessage message)
    {
        Console.WriteLine($"Test message {DateTime.Now:hh:mm:ss} {JsonSerializer.Serialize(message)}");
        await Task.Delay(TimeSpan.FromMicroseconds(_random.Next(40, 100)));
    }
}
```

This library provides two ways to deliver messages to the consumer. The first option lets the consumer receive and process the message immediately after it’s sent. The second option allows you to schedule the message for processing at a later time. You can use the **IPublisher** interface via dependency injection.

``` csharp
public class MyService(IPublisher publisher){
    public async Task PublishMessage(){
        await publisher.Publish(new TestMessage
        {
            Id = Guid.NewGuid()
        });
    }

    public async Task ScheduleMessage(){
        await publisher.Publish(new TestMessage
        {
            Id = Guid.NewGuid()
        },
        DateTime.UtcNow.AddSeconds(3));
    }
}
```