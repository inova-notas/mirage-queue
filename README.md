# Mirage Queue

Mirage Queue is a library designed to provide the benefits of a message broker without introducing additional 
infrastructure dependencies. Instead, it leverages a database to function as a message broker.

> **Note**: Currently, this library only supports **PostgreSQL** databases. 

## Installation

You can install the required packages via NuGet:

### Core Package
``` shell
dotnet add package InovaNotas.MirageQueue.Postgres
```

### Optional Dashboard (Recommended)
``` shell
dotnet add package InovaNotas.MirageQueue.Dashboard
```

The dashboard provides a web-based interface to monitor and manage your message queues, similar to Hangfire's dashboard.

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

## Dashboard Integration

The MirageQueue Dashboard provides a comprehensive web interface for monitoring and managing your message queues.

### Features

- **Real-time Statistics**: Live metrics for inbound, outbound, and scheduled messages
- **Message Management**: Browse, filter, and search through all message types
- **Message Details**: View complete message information with JSON prettification
- **Interactive Tooltips**: Hover over truncated message content to see full payload
- **Advanced Filtering**: Filter outbound messages by contract and endpoint
- **Requeue Functionality**: Requeue failed or processed messages
- **Dark/Light Theme**: Toggle between themes
- **Responsive Design**: Works on desktop and mobile devices

### Basic Setup

Add the dashboard to your ASP.NET Core application:

```csharp
using MirageQueue;
using MirageQueue.Postgres;
using MirageQueue.Dashboard; // Add this for dashboard

var builder = WebApplication.CreateBuilder(args);

// Configure MirageQueue
builder.Services.AddMirageQueue();
builder.Services.AddMirageQueuePostgres(builder.Configuration.GetConnectionString("DefaultConnection"));
builder.Services.AddConsumersFromAssembly(typeof(Program).Assembly);

// Add dashboard (optional but recommended)
builder.Services.AddMirageQueueDashboard();

var app = builder.Build();

app.UseRouting();

// Map dashboard endpoints
app.MapMirageQueueDashboard();

// Initialize MirageQueue
app.UseMirageQueue();

app.Run();
```

### Accessing the Dashboard

Once configured, the dashboard is available at:
```
https://your-app-domain/mirage-dashboard
```

You can customize the route prefix:
```csharp
// Custom route prefix
app.MapMirageQueueDashboard("my-custom-path");
// Accessible at: https://your-app-domain/my-custom-path
```

### Security Configuration

**Important**: The dashboard doesn't include built-in authentication. For production environments:

```csharp
// Secure with authentication
app.MapMirageQueueDashboard()
   .RequireAuthorization("AdminPolicy");

// Or restrict to specific roles
app.MapMirageQueueDashboard()
   .RequireAuthorization("Admin");

// Or use custom authentication
app.MapMirageQueueDashboard()
   .RequireHost("localhost") // Only local access
   .RequireAuthorization();
```

### Dashboard Sections

- **Overview**: Real-time statistics and system status
- **Inbound Messages**: Messages received for processing
- **Outbound Messages**: Messages being sent to external endpoints (with contract/endpoint filtering)
- **Scheduled Messages**: Messages scheduled for future processing
- **Message Details**: Complete message information with requeue options

For detailed dashboard documentation, see the [Dashboard README](src/MirageQueue.Dashboard/README.md).