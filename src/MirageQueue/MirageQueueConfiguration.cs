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
}
