namespace MirageQueue;

public class MirageQueueConfiguration
{
    /// <summary>
    /// Represents the time in seconds to pull the messages from database
    /// </summary>
    public int PoolingTime { get; set; }
    public int WorkersQuantity { get; set; }
    public int ScheduleWorkersQuantity { get; set; }
}