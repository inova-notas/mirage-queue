namespace MirageQueue.Messages.Entities;

public sealed class OutboundMessageError
{
    public int Attempt { get; init; }
    public DateTime OccurredAt { get; init; }
    public string? Message { get; init; }
    public string? ExceptionType { get; init; }
    public string? StackTrace { get; init; }

    /// <summary>
    /// "Dispatch" when a consumer threw; "Reaper" when the stuck-Processing reaper
    /// reclaimed the row because its lease expired.
    /// </summary>
    public string Source { get; init; } = "Dispatch";
}
