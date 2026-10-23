namespace Hexalith.Platform.Custody.Dapr;
internal static class StartupGuard
{
    internal static void Validate(string mode, string socketPath)
    {
        if (mode != "--serve") return;
        if (!OperatingSystem.IsLinux()) throw new AuthorityUnavailableException("Linux protected UDS required.");
        string directory = Path.GetDirectoryName(socketPath)!;
        if (!Directory.Exists(directory) || File.Exists(socketPath)
            || (File.GetUnixFileMode(directory) & (UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new AuthorityUnavailableException("Pre-owned private socket directory required; stale sockets are never removed automatically.");
    }
}
