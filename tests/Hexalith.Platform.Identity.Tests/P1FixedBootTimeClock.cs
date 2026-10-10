using Hexalith.Platform.Identity;

namespace Hexalith.Platform.Identity.Tests;

/// <summary>Deterministic boottime samples for exact deadline and rollback tests.</summary>
internal sealed class P1FixedBootTimeClock(long initialNanoseconds) : P1BootTimeClock
{
    private long _nanoseconds = initialNanoseconds;

    internal override bool TryRead(out long nanoseconds)
    {
        nanoseconds = Interlocked.Read(ref _nanoseconds);
        return true;
    }

    internal void Set(long nanoseconds) => Interlocked.Exchange(ref _nanoseconds, nanoseconds);
}
