namespace Hexalith.Platform.Composition;

using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

/// <summary>Correlates an ordinary held file descriptor to the actual running process mapping without map_files privileges.</summary>
internal static class PlatformMappedArtifact
{
    internal static string Hash(int process, string path, string mapping)
    {
        string[] fields = mapping.Split(' ', 6, StringSplitOptions.RemoveEmptyEntries);
        if (!OperatingSystem.IsLinux() || fields.Length != 6 || fields[5] != path || Path.GetFullPath(path) != path || path.EndsWith(" (deleted)", StringComparison.Ordinal))
            throw new InvalidDataException("The loaded artifact mapping is unavailable or deleted.");
        using FileStream held = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        Status before = Inspect(held);
        string[] device = fields[3].Split(':');
        if (before.Inode != ulong.Parse(fields[4], CultureInfo.InvariantCulture)
            || before.Major != uint.Parse(device[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            || before.Minor != uint.Parse(device[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture) || held.Length > 268435456)
            throw new InvalidDataException("The mapped artifact pathname was replaced.");
        string hash = Convert.ToHexStringLower(SHA256.HashData(held));
        Status after = Inspect(held);
        if (before.Inode != after.Inode || before.Size != after.Size || before.Modified != after.Modified || before.ModifiedNanos != after.ModifiedNanos
            || before.Changed != after.Changed || before.ChangedNanos != after.ChangedNanos
            || !File.ReadLines("/proc/" + process + "/maps").Contains(mapping, StringComparer.Ordinal))
            throw new InvalidDataException("The loaded artifact changed during observation.");
        return hash;
    }

    private static Status Inspect(FileStream held)
    {
        if (Statx(held.SafeFileHandle.DangerousGetHandle().ToInt32(), string.Empty, 0x1000, 0x7ff, out Status status) != 0)
            throw new IOException("The held artifact identity could not be observed.");
        return status;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054", Justification = "Explicit Linux statx layout requires no generated unsafe code.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2101", Justification = "The fixed empty pathname is explicitly UTF-8.")]
    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int Statx(int descriptor, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mask, out Status status);

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct Status
    {
        [FieldOffset(32)] internal ulong Inode;
        [FieldOffset(40)] internal ulong Size;
        [FieldOffset(96)] internal long Changed;
        [FieldOffset(104)] internal uint ChangedNanos;
        [FieldOffset(112)] internal long Modified;
        [FieldOffset(120)] internal uint ModifiedNanos;
        [FieldOffset(136)] internal uint Major;
        [FieldOffset(140)] internal uint Minor;
    }
}
