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
    /// Represents the time in minutes that a message can stay in Processing status without a heartbeat before being considered stuck.
    /// </summary>
    public int ProcessingRecoveryTimeInMinutes { get; set; }

    /// <summary>
    /// Represents the time in milliseconds that the recovery worker will wait before checking for stuck processing messages.
    /// </summary>
    public int PoolingRecoveryTime { get; set; }

    /// <summary>
    /// Represents the time in milliseconds between heartbeat updates during message processing.
    /// </summary>
    public int HeartbeatIntervalInMilliseconds { get; set; }

    /// <summary>
    /// Represents the maximum number of outbound messages buffered in memory.
    /// </summary>
    public int OutboundChannelCapacity { get; set; }
}
