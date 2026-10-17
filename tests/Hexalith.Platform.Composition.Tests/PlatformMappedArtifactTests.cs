namespace Hexalith.Platform.Composition.Tests;

using System.Diagnostics;
using System.Security.Cryptography;
using Shouldly;

/// <summary>Ordinary unprivileged inert mmap process; no service or target is started.</summary>
public sealed class PlatformMappedArtifactTests
{
    [Fact]
    public async Task HeldMappedInodeAuthenticatesAndRejectsReplacementOrDeletionAsync()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Path.Combine(Path.GetTempPath(), "mapped-artifact-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "fixture.dll");
        await File.WriteAllTextAsync(path, "inert mapped fixture bytes", TestContext.Current.CancellationToken);
        using Process process = new() { StartInfo = new ProcessStartInfo("python3") { RedirectStandardOutput = true, RedirectStandardInput = true, UseShellExecute = false } };
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add("import mmap,sys; f=open(sys.argv[1],'rb'); m=mmap.mmap(f.fileno(),0,access=mmap.ACCESS_READ); print('mapped',flush=True); sys.stdin.buffer.read(1)");
        process.StartInfo.ArgumentList.Add(path);
        try
        {
            process.Start().ShouldBeTrue();
            (await process.StandardOutput.ReadLineAsync(TestContext.Current.CancellationToken)).ShouldBe("mapped");
            string mapping = File.ReadLines("/proc/" + process.Id + "/maps").Single(line => line.EndsWith(" " + path, StringComparison.Ordinal));
            PlatformMappedArtifact.Hash(process.Id, path, mapping).ShouldBe(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))));
            string substitute = path + ".new";
            await File.WriteAllTextAsync(substitute, "substituted bytes", TestContext.Current.CancellationToken);
            File.Move(substitute, path, overwrite: true);
            Should.Throw<InvalidDataException>(() => PlatformMappedArtifact.Hash(process.Id, path, mapping));
            string deleted = File.ReadLines("/proc/" + process.Id + "/maps").Single(line => line.EndsWith(path + " (deleted)", StringComparison.Ordinal));
            Should.Throw<InvalidDataException>(() => PlatformMappedArtifact.Hash(process.Id, path + " (deleted)", deleted));
        }
        finally
        {
            if (process.Id > 0 && !process.HasExited) { process.StandardInput.Close(); await process.WaitForExitAsync(TestContext.Current.CancellationToken); }
            Directory.Delete(directory, recursive: true);
        }
    }
}
