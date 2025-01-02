# Mirage Queue

Mirage Queue is a library designed to provide the benefits of a message broker without introducing additional 
infrastructure dependencies. Instead, it leverages a database to function as a message broker.

> **Note**: Currently, this library only supports **PostgreSQL** databases. 

## Installation

You can install the package via NuGet using the following command or through the NuGet interface in your IDE:

``` shell
dotnet add package InovaNotas.MirageQueue.PostgreSQL
```

## Getting Started

Setting up Mirage Queue is straightforward. Here’s an example of how to configure it in your application:
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
## Registering Consumers
Instead of registering all consumers from an assembly, you can register them individually like this:

``` csharp
builder.Services.AddConsumer<TestMessageConsumer>();
```
## Creating a Consumer
To create a consumer, implement the `IConsumer<T>` interface, where `T` is the type of message you want to process. 
You can have multiple consumers handling the same message type.

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

## Message Delivery Options

This library provides two methods for delivering messages to the consumer:

1. **Instant Processing**:  
   The consumer can receive and process the message immediately after it’s sent.

2. **Scheduled Processing**:  
   You can schedule the message to be processed at a specified time in the future.

### Usage

To use these features, inject the `IPublisher` interface via dependency injection

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