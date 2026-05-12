using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace MirageQueue.Diagnostics;

internal static class MirageQueueDiagnostics
{
    public const string ActivitySourceName = "MirageQueue";
    public const string MeterName = "MirageQueue";
    public const string MessagingSystemValue = "mirage_queue";

    // OTel messaging semantic-convention attribute keys.
    public const string AttrMessagingSystem = "messaging.system";
    public const string AttrMessagingOperation = "messaging.operation";
    public const string AttrMessagingDestination = "messaging.destination.name";
    public const string AttrMessagingMessageId = "messaging.message.id";
    public const string AttrMessagingConsumerEndpoint = "messaging.consumer.group.name";

    // Activity names follow OTel messaging conventions: "{operation} {destination}".
    public const string OperationPublish = "publish";
    public const string OperationSchedule = "schedule";
    public const string OperationProcess = "process";
    public const string OperationFanOut = "fan-out";
    public const string OperationCleanup = "cleanup";
    public const string OperationReaper = "reaper";

    private static readonly string Version =
        typeof(MirageQueueDiagnostics).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(MirageQueueDiagnostics).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version);

    public static readonly Meter Meter = new(MeterName, Version);

    // OTel-conventional messaging metrics where they map cleanly.
    public static readonly Counter<long> PublishedCounter =
        Meter.CreateCounter<long>("messaging.client.published.messages", unit: "{message}",
            description: "Number of messages successfully published to the queue.");

    public static readonly Counter<long> ConsumedCounter =
        Meter.CreateCounter<long>("messaging.client.consumed.messages", unit: "{message}",
            description: "Number of messages successfully consumed.");

    public static readonly Histogram<double> PublishDuration =
        Meter.CreateHistogram<double>("messaging.client.operation.duration", unit: "s",
            description: "Duration of publish/schedule operations.");

    public static readonly Histogram<double> ProcessDuration =
        Meter.CreateHistogram<double>("messaging.process.duration", unit: "s",
            description: "Duration of consumer dispatch operations.");

    // Queue-specific instruments (no OTel equivalent).
    public static readonly Histogram<double> QueueWaitDuration =
        Meter.CreateHistogram<double>("mirage_queue.queue.wait.duration", unit: "s",
            description: "Time a message waited between CreateAt and first dispatch attempt.");

    public static readonly Counter<long> RetryCounter =
        Meter.CreateCounter<long>("mirage_queue.outbound.retries", unit: "{retry}",
            description: "Number of outbound message retry attempts scheduled.");

    public static readonly Counter<long> DeadLetterCounter =
        Meter.CreateCounter<long>("mirage_queue.outbound.dead_lettered", unit: "{message}",
            description: "Number of outbound messages moved to the dead-letter status.");

    public static readonly Counter<long> CleanupRowsDeleted =
        Meter.CreateCounter<long>("mirage_queue.cleanup.rows_deleted", unit: "{row}",
            description: "Rows deleted by the retention cleanup sweep, tagged by table.");

    public static readonly Counter<long> ReaperRowsReset =
        Meter.CreateCounter<long>("mirage_queue.reaper.rows_reset", unit: "{row}",
            description: "Stuck-Processing rows reclaimed by the reaper sweep, tagged by disposition.");

    static MirageQueueDiagnostics()
    {
        // Defensive: ensure W3C trace IDs so the persisted TraceParent is parseable.
        if (Activity.DefaultIdFormat != ActivityIdFormat.W3C)
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;
        }
    }
}
