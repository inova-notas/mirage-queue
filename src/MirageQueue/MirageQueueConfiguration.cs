namespace MirageQueue;

public class MirageQueueConfiguration
{

    /// <summary>
    /// Represents the time in milliseconds that the queue will wait before checking for scheduled messages.
    /// </summary>
    public int PoolingScheduleTime { get; set; }
    
    /// <summary>
    /// Represents the time in milliseconds that the queue will wait before checking for inbound messages.
    /// </summary>
    public int PoolingInboundTime { get; set; }
    
    /// <summary>
    /// Represents the time in milliseconds that the queue will wait before checking for outbound messages.
    /// </summary>
    public int PoolingOutboundTime { get; set; }
    public int WorkersQuantity { get; set; }

    /// <summary>
    /// Represents the maximum number of outbound messages buffered in memory.
    /// </summary>
    public int OutboundChannelCapacity { get; set; }

    /// <summary>
    /// How long a row may sit in <c>Status = Processing</c> before the stuck-Processing
    /// reaper considers it abandoned (e.g., the worker crashed mid-dispatch). Defaults to
    /// 5 minutes. Should be longer than any reasonable consumer dispatch.
    /// </summary>
    public TimeSpan ProcessingLeaseDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Milliseconds between stuck-Processing reaper sweeps. Defaults to 60 seconds.
    /// </summary>
    public int StuckProcessingPollingTime { get; set; } = 60000;

    /// <summary>
    /// Enables the periodic cleanup of old terminal message rows. Opt-in (default <c>false</c>)
    /// so an upgrade never silently deletes historical data.
    /// </summary>
    public bool CleanupEnabled { get; set; } = false;

    /// <summary>
    /// Age threshold (in days) for cleanup. Terminal rows older than this many days are
    /// eligible for deletion. Defaults to 90.
    /// </summary>
    public int MessageRetentionDays { get; set; } = 90;

    /// <summary>
    /// Milliseconds between cleanup sweeps. Defaults to once per day (86,400,000 ms).
    /// </summary>
    public int CleanupPollingTime { get; set; } = 86_400_000;

    /// <summary>
    /// Maximum number of rows deleted per table per sweep. Bounds lock duration on large
    /// backlogs. Defaults to 1000.
    /// </summary>
    public int CleanupBatchSize { get; set; } = 1000;
}
