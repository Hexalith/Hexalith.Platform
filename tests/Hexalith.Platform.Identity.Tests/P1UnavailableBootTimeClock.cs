using Hexalith.Platform.Identity;

namespace Hexalith.Platform.Identity.Tests;

/// <summary>Simulates an unavailable same-boot clock.</summary>
internal sealed class P1UnavailableBootTimeClock : P1BootTimeClock
{
    internal override bool TryRead(out long nanoseconds)
    {
        nanoseconds = 0;
        return false;
    }
}
