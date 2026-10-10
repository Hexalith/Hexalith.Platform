using Hexalith.Platform.Identity;

namespace Hexalith.Platform.Identity.Tests;

/// <summary>Advances a real Linux boottime sample without changing the host clock.</summary>
internal sealed class P1TestBootTimeClock : P1BootTimeClock
{
    private readonly P1BootTimeClock _real = new();
    private long _offset;

    internal override bool TryRead(out long nanoseconds)
    {
        if (!_real.TryRead(out nanoseconds)) return false;
        nanoseconds += Interlocked.Read(ref _offset);
        return true;
    }

    internal void Advance(TimeSpan elapsed) => Interlocked.Add(ref _offset, elapsed.Ticks * 100);
}
