using System.Runtime.InteropServices;

namespace Hexalith.Platform.Identity;

/// <summary>Linux same-boot, suspend-aware time source. An unavailable clock is a refusal.</summary>
internal class P1BootTimeClock
{
    private const int ClockBootTime = 7;

    [DllImport("libc", EntryPoint = "clock_gettime", SetLastError = true)]
    private static extern int ClockGetTime(int clockId, [Out] long[] time);

    /// <summary>Reads nanoseconds since boot, including suspension.</summary>
    internal virtual bool TryRead(out long nanoseconds)
    {
        nanoseconds = 0;
        if (!OperatingSystem.IsLinux() || IntPtr.Size != 8)
        {
            return false;
        }

        try
        {
            long[] time = new long[2];
            if (ClockGetTime(ClockBootTime, time) != 0 || time[0] < 0 || time[1] is < 0 or >= 1_000_000_000)
            {
                return false;
            }

            nanoseconds = checked(time[0] * 1_000_000_000 + time[1]);
            return true;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or OverflowException)
        {
            return false;
        }
    }
}
