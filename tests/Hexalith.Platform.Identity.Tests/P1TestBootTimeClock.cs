using Hexalith.Platform.Identity;

namespace Hexalith.Platform.Identity.Tests;

/// <summary>Advances a real Linux boottime sample without changing the host clock.</summary>
internal sealed class P1TestBootTimeClock : P1BootTimeClock
{
    private readonly P1BootTimeClock _real = new();
    private long _offset;
    private int _failNext;

    internal override bool TryRead(out long nanoseconds)
    {
        if (Interlocked.Exchange(ref _failNext, 0) != 0)
        {
            nanoseconds = 0;
            return false;
        }

        if (!_real.TryRead(out nanoseconds)) return false;
        nanoseconds += Interlocked.Read(ref _offset);
        return true;
    }

    internal void Advance(TimeSpan elapsed) => Interlocked.Add(ref _offset, elapsed.Ticks * 100);

    internal void FailNextRead() => Interlocked.Exchange(ref _failNext, 1);
}
