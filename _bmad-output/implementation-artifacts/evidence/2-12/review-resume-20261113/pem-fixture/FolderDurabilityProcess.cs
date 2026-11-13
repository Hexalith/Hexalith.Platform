using System.Diagnostics;
namespace Hexalith.Folders.AppHost.Tests;
internal static class FolderDurabilityProcess
{
    internal static async Task<string> RunAsync(params string[] arguments)
    {
        if (arguments[0] != "openssl") throw new InvalidOperationException("stop-before-docker");
        ProcessStartInfo start = new(arguments[0]) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments.Skip(1)) start.ArgumentList.Add(argument);
        using Process child = Process.Start(start)!;
        Task<string> output = child.StandardOutput.ReadToEndAsync(), errors = child.StandardError.ReadToEndAsync();
        await child.WaitForExitAsync();
        if (child.ExitCode != 0) throw new InvalidOperationException("openssl-failed");
        await errors;
        return await output;
    }
}