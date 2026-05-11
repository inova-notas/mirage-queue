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
}
