using System.Threading;

namespace MirageQueue.Workers;

public sealed class OutboundChannelState
{
    private int _pendingCount;

    public int PendingCount => Volatile.Read(ref _pendingCount);

    public void IncrementPending()
    {
        Interlocked.Increment(ref _pendingCount);
    }

    public void DecrementPending()
    {
        while (true)
        {
            var current = Volatile.Read(ref _pendingCount);
            if (current == 0)
                return;

            var next = current - 1;
            if (Interlocked.CompareExchange(ref _pendingCount, next, current) == current)
                return;
        }
    }
}
