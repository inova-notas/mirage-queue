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

## Transactional Publishing

The basic `IPublisher.Publish` overload writes to its own connection — independent of any transaction your business code is using. If your business `SaveChangesAsync()` succeeds but the publish fails (or vice versa), you get a "ghost commit" or "ghost publish".

To make a publish happen **iff** the business commit happens, MirageQueue ships two APIs that share the caller's transaction. Pick whichever fits your code style.

### Option A: `IDbContextOutbox<TDbContext>` (recommended)

Wraps your business `DbContext` and buffers publishes until you flush. Best when business code emits one or more messages per unit-of-work.

**Setup**

```csharp
using MirageQueue;
using MirageQueue.Postgres;
using MirageQueue.Outbox;

builder.Services.AddMirageQueue();
builder.Services.AddMirageQueuePostgres(connectionString);
builder.Services.AddDbContext<OrderDbContext>(o => o.UseNpgsql(connectionString));

// Register the outbox for your DbContext
builder.Services.AddMirageQueueOutbox<OrderDbContext>();
```

**Usage**

```csharp
public class CreateOrderHandler(
    OrderDbContext db,
    IDbContextOutbox<OrderDbContext> outbox)
{
    public async Task Handle(CreateOrderCommand cmd)
    {
        var order = new Order { Id = Guid.NewGuid(), CustomerId = cmd.CustomerId };
        db.Orders.Add(order);

        // Buffer one or more publishes (synchronous — no DB I/O until flush)
        outbox.Publish(new OrderCreated(order.Id));
        outbox.Publish(new InventoryReservationRequested(order.Id, cmd.Items));

        // Atomically: SaveChanges + queue inserts + commit
        await outbox.SaveChangesAndFlushMessagesAsync();
    }
}
```

`SaveChangesAndFlushMessagesAsync` is **permissive**:
- If you already opened a transaction (`db.Database.BeginTransactionAsync()`), it joins it and lets you commit.
- If no transaction is open, it opens one, commits on success, rolls back on failure.

If anything throws — business save, queue insert, anything — both the business rows and the queue rows are rolled back together.

### Option B: Low-level `IPublisher.Publish(message, DbTransaction)`

Use when you want explicit control over the transaction or when buffering doesn't fit your code shape. The publisher writes directly through the `DbTransaction` you pass in.

```csharp
public class CreateOrderHandler(OrderDbContext db, IPublisher publisher)
{
    public async Task Handle(CreateOrderCommand cmd)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        db.Orders.Add(new Order { Id = Guid.NewGuid(), CustomerId = cmd.CustomerId });
        await db.SaveChangesAsync();

        await publisher.Publish(
            new OrderCreated(/* ... */),
            transaction.GetDbTransaction());

        await transaction.CommitAsync();
    }
}
```

The publisher writes the queue row through `transaction.Connection` and enlists on the same `DbTransaction`. No second connection, no second transaction. `Schedule` has the same overload:

```csharp
await publisher.Schedule(
    new ReminderMessage(orderId),
    DateTime.UtcNow.AddDays(1),
    transaction.GetDbTransaction());
```

### With `ExecutionStrategy` (retry-on-failure)

If your `DbContext` is configured with `EnableRetryOnFailure()`, you must wrap the unit-of-work in `ExecuteAsync` so the retry covers the whole atomic block:

```csharp
var strategy = db.Database.CreateExecutionStrategy();

await strategy.ExecuteAsync(async () =>
{
    db.Orders.Add(order);
    outbox.Publish(new OrderCreated(order.Id));
    await outbox.SaveChangesAndFlushMessagesAsync();
});
```

The same pattern works with the low-level `IPublisher` overload — just open the transaction inside the lambda. Each retry attempt opens a fresh transaction; on a transient failure the whole block re-runs as one unit.

> Caveat: a retry that succeeds server-side but loses the commit ACK will produce a duplicate inbound row unless you pass an idempotency key. Either use a keyed publish overload (`publisher.Publish(message, "order-{id}", transaction)`), or ensure your consumers are idempotent for at-least-once delivery — same as any redelivered message.

## Idempotency Keys

For at-least-once delivery semantics, a publish can sometimes be retried — either by your caller (HTTP retry, message-bus redelivery, an `ExecutionStrategy` after a commit-ACK loss) or by your own application logic. Without an idempotency key, each retry produces a fresh inbound row and consumers see duplicates.

Pass an idempotency key to make duplicate publishes a server-side no-op:

```csharp
public class OrderService(IPublisher publisher)
{
    public async Task PublishOrderCreated(Order order)
    {
        var result = await publisher.Publish(
            new OrderCreated(order.Id, order.Total),
            idempotencyKey: $"order-{order.Id}");

        if (result.IsDuplicate)
        {
            // Already published earlier; result.MessageId points to the original inbound row.
        }
    }
}
```

The keyed overload returns `PublishResult`:

```csharp
public readonly record struct PublishResult(Guid? MessageId, bool IsDuplicate);
```

- `IsDuplicate = false`: a new inbound row was written; `MessageId` is its id.
- `IsDuplicate = true`: a row with the same key already existed; `MessageId` is the existing row's id (so you can join back to it).

Under the hood, the inbound and scheduled tables have a nullable `IdempotencyKey` column with a partial unique index (`WHERE IdempotencyKey IS NOT NULL`). Unkeyed publishes still insert a fresh row every time — the index ignores them — so opting in is per-call.

### Keyed overloads

All four publishing entry points have a keyed variant:

```csharp
// Standalone publish
await publisher.Publish(message, idempotencyKey: "key");
await publisher.Schedule(message, scheduledTime, idempotencyKey: "key");

// Transactional publish (combines with Phase 1's outbox)
await publisher.Publish(message, idempotencyKey: "key", transaction.GetDbTransaction());
await publisher.Schedule(message, scheduledTime, idempotencyKey: "key", transaction.GetDbTransaction());
```

### With `IDbContextOutbox<TDbContext>`

The outbox wrapper has matching overloads. `SaveChangesAndFlushMessagesAsync` returns `IReadOnlyList<PublishResult>` — one entry per buffered publish, in call order:

```csharp
public class CreateOrderHandler(
    OrderDbContext db,
    IDbContextOutbox<OrderDbContext> outbox)
{
    public async Task Handle(CreateOrderCommand cmd)
    {
        var order = new Order { Id = Guid.NewGuid(), CustomerId = cmd.CustomerId };
        db.Orders.Add(order);

        outbox.Publish(new OrderCreated(order.Id), idempotencyKey: $"order-{order.Id}");
        outbox.Publish(new InventoryReservationRequested(order.Id, cmd.Items));  // unkeyed

        var results = await outbox.SaveChangesAndFlushMessagesAsync();

        // results[0] is the keyed OrderCreated: IsDuplicate tells you whether it was new.
        // results[1] is the unkeyed publish: IsDuplicate is always false, MessageId is null.
    }
}
```

### What the key dedups (and what it doesn't)

- **Per-table**: the inbound and scheduled tables have separate partial unique indexes, so `Publish(msg, "key-X")` and `Schedule(msg, time, "key-X")` are **independent**. Use distinct prefixes (`"publish-x"` vs `"schedule-x"`) if you want them to share a namespace.
- **Per-row, not per-handler**: the key dedups the inbound row. **Fan-out** to multiple consumers is also deduped — but via a separate, automatic mechanism: a unique index on `(InboundMessageId, ConsumerEndpoint)` in the outbound table. You don't pass anything for fan-out dedup; it's always on.
- **Not for consumer-side idempotency**: at-least-once delivery still applies. If a worker crashes after the consumer ran but before the row transitioned to `Processed`, the row will be re-dispatched. Consumers should still be idempotent for that case (which the stuck-Processing reaper handles — see below).

### Choosing a key

Natural business identifiers work best: `"order-{orderId}"`, `"webhook-{providerEventId}"`, `"stripe-{idempotencyKeyHeader}"`. The column is `varchar(200)`; pick something stable across retries.

## Retry Policies and Dead Letter Queue

By default, when a consumer throws an exception MirageQueue retries up to 3 times in-process for transient errors (timeouts, deadlocks, socket failures, transient Postgres SQL states), then transitions the row to `Status = Failed`. That preserves the pre-v2.7 baseline.

For more control, attach a **retry policy** when registering the consumer:

```csharp
builder.Services.AddConsumer<OrderShippedConsumer>(p => p
    .MaxAttempts(5)                                              // up to 5 total dispatches
    .TransientAttempts(3)                                        // 3 in-process retries per dispatch (transient only)
    .ExponentialBackoff(TimeSpan.FromSeconds(1), factor: 2));    // wait 1s, 2s, 4s, 8s between dispatches
```

Two retry layers stack:

1. **In-process retries** (`TransientAttempts`) — tight loop within one dispatch. For transient errors only. No DB write, no `AttemptCount` increment. Survives nothing — process death loses this.
2. **Persisted retries** (`MaxAttempts`) — on dispatch failure (transient-exhausted or non-transient), the row goes back to `Status = New` with `AttemptCount++` and `NextRetryAt` computed from the backoff strategy. Worker pickup honors `NextRetryAt`, so retries survive application restarts.

When `AttemptCount >= MaxAttempts` and the dispatch still fails, the row transitions to a terminal state:

- `Status = DeadLettered` if a retry policy was attached (Phase 3 terminal).
- `Status = Failed` if no policy was attached (legacy behavior).

### Backoff strategies

```csharp
.NoBackoff()                                                    // retry immediately
.ConstantBackoff(TimeSpan.FromSeconds(30))                      // always 30s between dispatches
.LinearBackoff(TimeSpan.FromSeconds(10),                        // 10s, 20s, 30s, ...
               max: TimeSpan.FromMinutes(2))                    //   capped at 2 min
.ExponentialBackoff(TimeSpan.FromSeconds(1), factor: 2,         // 1s, 2s, 4s, 8s, ...
                    max: TimeSpan.FromMinutes(5))               //   capped at 5 min
```

Backoff is computed purely from `AttemptCount` (a persisted column), so a restart between attempts can't drift or compress the schedule — the next worker reads the stored count, picks the row up at `NextRetryAt`, and computes the next delay from there.

### Customizing transient classification

The default classifier recognises `TimeoutException`, `DbUpdateConcurrencyException`, `SocketException`, and `Npgsql.PostgresException` with transient SQL states (serialization failures, deadlocks, connection failures). Override or extend per consumer:

```csharp
.TransientWhen(ex => ex is MyDomainTimeoutException)        // replace the default classifier
.TransientWhenAlso(ex => ex is MyDomainTransientException)  // OR-extend the default
```

### Dead Letter Queue

`DeadLettered` rows appear in the existing dashboard via the outbound message list with status filter `DeadLettered`. The dashboard's Requeue action on a `DeadLettered` row resets:

- `Status = New`
- `AttemptCount = 0`
- `NextRetryAt = null`
- `ProcessingStartedAt = null`
- error fields cleared

The retry policy then attempts the row from scratch. Programmatic replay is available via `IOutboundMessageRepository.ReplayFromDeadLetter(Guid id)`.

### Stuck-Processing recovery

If a worker dies mid-dispatch (crash, SIGKILL, OOM) the row sits at `Status = Processing` indefinitely without recovery — the standard pickup query only sees `Status = New`. The `PgStuckProcessingReaperWorker` is registered automatically by `AddMirageQueuePostgres` and periodically scans for rows whose `ProcessingStartedAt` is older than `ProcessingLeaseDuration` (default 5 minutes), then reclaims them via the same retry/DLQ decision as a normal dispatch failure:

- Room to retry → `Status = New` with backoff (consults the consumer's policy)
- `MaxAttempts` exhausted → terminal (`DeadLettered` if policy attached, else `Failed`)

Two knobs on `MirageQueueConfiguration`:

```csharp
builder.Services.AddMirageQueue(options =>
{
    options.ProcessingLeaseDuration = TimeSpan.FromMinutes(10);   // increase if consumers can run >5 minutes
    options.StuckProcessingPollingTime = 30000;                   // ms between reaper sweeps; default 60s
});
```

> **Important**: the lease must be longer than the longest legitimate consumer execution. Otherwise the reaper will reclaim still-running messages and you'll get duplicates. When in doubt, err on the longer side.

## Retention Cleanup

Message tables grow monotonically — every published message produces an inbound row plus N outbound rows (one per consumer endpoint), and Phase 3 added the `DeadLettered` terminal status. Over time the tables become dominated by old terminal rows that have no operational value beyond audit. MirageQueue ships a background cleanup worker that deletes them on a configurable retention schedule.

**Cleanup is opt-in.** Many operators rely on terminal rows for forensic / audit reads, so an upgrade never silently starts deleting historical data. Set `CleanupEnabled = true` to turn it on:

```csharp
builder.Services.AddMirageQueue(options =>
{
    options.CleanupEnabled = true;                  // default false
    options.MessageRetentionDays = 90;              // default 90
    options.CleanupPollingTime = 86_400_000;        // default 24h between sweeps
    options.CleanupBatchSize = 1000;                // default; bounds per-sweep lock duration
});
```

When enabled, the `PgMessageCleanupWorker` wakes up periodically and deletes:

| Table | Eligible rows |
|---|---|
| `OutboundMessage` | `Status = Processed` or `Status = DeadLettered`, with `COALESCE(UpdateAt, CreateAt)` older than the cutoff |
| `InboundMessage` | `Status = Queued` (post-fan-out terminal), older than the cutoff, **and no outbound child in a non-terminal state** |
| `ScheduledInboundMessage` | `Status = Queued` (converted to inbound, terminal here), older than the cutoff |

Each sweep deletes at most `CleanupBatchSize` rows per table — set lower if your DB locks need to stay tight, higher if your backlog is large and you want to drain faster.

### Why `Failed` is not cleaned

`OutboundMessageStatus.Failed` is the legacy terminal state from before Phase 3 (consumer registered without an explicit retry policy → fails → `Failed`). Cleaning it would silently remove error-diagnostic context for the operators most likely to be running without policies. Either attach a retry policy (so terminals become `DeadLettered`, which **is** cleaned) or run a manual `DELETE` if you want them gone.

### Safety: the FK cascade guard

`OutboundMessage` has `ON DELETE CASCADE` to `InboundMessage`. Deleting an inbound row drops **all** its outbound children. The cleanup query therefore only deletes an inbound row if every outbound child is itself in `Processed` or `DeadLettered` — meaning the cascade is just finishing a sweep that would have happened anyway. Inbound rows with any `New`, `Processing`, or `Failed` child stay put even if they're past the retention cutoff.

### Multi-replica safety

Each delete uses `FOR UPDATE SKIP LOCKED` on the inner row-pick query, so multiple replicas running the cleanup worker won't block each other — each will claim a different slice of eligible rows per sweep.

## Observability (OpenTelemetry)

MirageQueue emits OpenTelemetry traces and metrics out of the box. Trace context (W3C `traceparent` / `tracestate`) is captured at publish time, persisted on the message row, and read back on dispatch so the consumer span attaches as a child of the publish span — giving a single end-to-end trace from upstream HTTP request through the queue to consumer execution.

### Registering instrumentation

```csharp
using MirageQueue.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddMirageQueueInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddMirageQueueInstrumentation()
        .AddOtlpExporter());
```

`AddMirageQueueInstrumentation()` is defined on both `TracerProviderBuilder` and `MeterProviderBuilder`. The library itself only uses the BCL `ActivitySource` / `Meter`; you supply the SDK and the exporter.

### Prometheus scraping

Because the metrics flow through the standard OpenTelemetry pipeline, swapping in (or adding) the Prometheus exporter requires no changes to MirageQueue:

```csharp
// dotnet add package OpenTelemetry.Exporter.Prometheus.AspNetCore --prerelease

builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m
        .AddMirageQueueInstrumentation()
        .AddPrometheusExporter());

var app = builder.Build();
app.MapPrometheusScrapingEndpoint(); // exposes /metrics
```

You can chain multiple exporters — e.g., `.AddPrometheusExporter().AddOtlpExporter()` — to scrape locally and forward to a collector at the same time. For non-ASP.NET hosts use `OpenTelemetry.Exporter.Prometheus.HttpListener` instead.

### Activity sources and span kinds

| Span | Kind | When |
|---|---|---|
| `publish <ContractName>` | Producer | Every `IPublisher.Publish(...)` call |
| `schedule <ContractName>` | Producer | Every `IPublisher.Schedule(...)` call |
| `process <ConsumerEndpoint>` | Consumer | Each consumer dispatch, child of stored `traceparent` |
| `cleanup` | Internal | Retention cleanup sweep — emitted only when at least one row was deleted |
| `reaper` | Internal | Stuck-Processing reaper sweep — emitted only when at least one row was reclaimed |

Failed dispatches set `Status = Error`, record the exception via `Activity.AddException(ex)`, and the span carries an `exception` event with type / message / stack.

### Metrics

OTel messaging semantic-convention names where they apply; `mirage_queue.*` for queue-specific ones.

| Name | Type | Unit |
|---|---|---|
| `messaging.client.published.messages` | Counter | `{message}` |
| `messaging.client.consumed.messages` | Counter | `{message}` |
| `messaging.client.operation.duration` | Histogram | `s` |
| `messaging.process.duration` | Histogram | `s` |
| `mirage_queue.queue.wait.duration` | Histogram | `s` |
| `mirage_queue.outbound.retries` | Counter | `{retry}` |
| `mirage_queue.outbound.dead_lettered` | Counter | `{message}` |
| `mirage_queue.cleanup.rows_deleted` | Counter | `{row}` (tagged by `table`) |
| `mirage_queue.reaper.rows_reset` | Counter | `{row}` (tagged by `disposition`) |

All messaging metrics are tagged with `messaging.system="mirage_queue"`, `messaging.operation`, and `messaging.destination.name` (the message contract or consumer endpoint).

### Trace columns and rolling upgrade

Three message tables (`InboundMessage`, `ScheduledInboundMessage`, `OutboundMessage`) gain two nullable columns: `TraceParent varchar(55)` and `TraceState varchar(256)`. Pre-Phase-4 rows have NULL values; consumers see them as "no incoming context" and start a fresh root span. The migration is additive and rolling-upgrade safe — older app instances simply ignore the columns.

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