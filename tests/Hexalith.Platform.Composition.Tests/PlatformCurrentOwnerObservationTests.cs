namespace Hexalith.Platform.Composition.Tests;

using System.Diagnostics;
using System.Security.Cryptography;
using Hexalith.Builds.Tooling.Runtime;
using Shouldly;
using Xunit;

/// <summary>Inert controlled kernel mappings exercise the live observer's refusals; no service, proof or runtime qualification is fabricated.</summary>
public sealed class PlatformCurrentOwnerObservationTests
{
    /// <summary>Absent or different owned mappings cannot advance the observation stage that precedes readiness publication.</summary>
    [Theory]
    [InlineData("absent-main")]
    [InlineData("absent-contracts")]
    [InlineData("different-main")]
    [InlineData("different-contracts")]
    public async Task MissingOrDifferentLiveOwnerMappingsRefuseBeforeObservationPublicationAsync(string fault)
    {
        OperatingSystem.IsLinux().ShouldBeTrue("The current purpose and its actual /proc observer require Linux.");
        string directory = Path.Combine(Path.GetTempPath(), "current-owner-mapping-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string main = Path.Combine(directory, "Hexalith.Parties.dll");
        string contracts = Path.Combine(directory, "Hexalith.Parties.Contracts.dll");
        File.WriteAllText(main, "inert current executable mapping fixture");
        File.WriteAllText(contracts, "inert current contracts mapping fixture");
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Hexalith.Parties"] = Hash(main), ["Hexalith.Parties.Contracts"] = Hash(contracts),
        };
        if (fault == "different-main") expected["Hexalith.Parties"] = new string('0', 64);
        if (fault == "different-contracts") expected["Hexalith.Parties.Contracts"] = new string('0', 64);
        using Process process = new() { StartInfo = new ProcessStartInfo("python3") { RedirectStandardOutput = true, RedirectStandardInput = true, UseShellExecute = false } };
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add("import mmap,sys; fs=[open(p,'rb') for p in sys.argv[1:]]; maps=[mmap.mmap(f.fileno(),0,access=mmap.ACCESS_READ) for f in fs]; print('mapped',flush=True); sys.stdin.buffer.read(1)");
        if (fault != "absent-main") process.StartInfo.ArgumentList.Add(main);
        if (fault != "absent-contracts") process.StartInfo.ArgumentList.Add(contracts);
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            process.Start().ShouldBeTrue();
            (await process.StandardOutput.ReadLineAsync(bound.Token).ConfigureAwait(true)).ShouldBe("mapped");
            var refusal = Should.Throw<InvalidDataException>(() => PlatformCurrentOwnerObservation.ReadRunningOwners(process.Id, directory, expected));
            refusal.Message.ShouldContain(fault == "absent-main" ? "not uniquely mapped" : "differs from its measured Release body");
            File.Exists(Path.Combine(directory, "current-owner-observation.json")).ShouldBeFalse();
            File.Exists(Path.Combine(directory, NativeTestHandoff.EndpointCredentialsFileName)).ShouldBeFalse();
        }
        finally
        {
            if (!process.HasExited)
            {
                await process.StandardInput.WriteAsync("x").ConfigureAwait(true);
                await process.StandardInput.FlushAsync(bound.Token).ConfigureAwait(true);
                await process.WaitForExitAsync(bound.Token).ConfigureAwait(true);
            }
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
