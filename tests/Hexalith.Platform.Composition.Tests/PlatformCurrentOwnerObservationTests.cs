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

    /// <summary>Mapped SDK bodies must equal their lock-verified members; every observed SDK body must be mapped; matches keep <c>sdkBodies</c> name-to-digest.</summary>
    /// <param name="fault">The inert mapping control.</param>
    [Theory]
    [InlineData("none")]
    [InlineData("different-sdk")]
    [InlineData("absent-sdk")]
    public async Task MappedOwnerPackageBodiesMustEqualTheirLockVerifiedMembersAsync(string fault)
    {
        OperatingSystem.IsLinux().ShouldBeTrue("The current purpose and its actual /proc observer require Linux.");
        string directory = Path.Combine(Path.GetTempPath(), "current-owner-packages-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Dictionary<string, Hexalith.Platform.CatalogArtifacts.OwnerPackageIdentity> expected = new(StringComparer.Ordinal);
            foreach (string name in PlatformCurrentOwnerProof.ObservedSdk.Append("Hexalith.Tenants.Contracts"))
            {
                string path = Path.Combine(directory, name + ".dll");
                File.WriteAllText(path, "inert " + name + " mapping fixture");
                string digest = fault == "different-sdk" && name == "Hexalith.EventStore.Client" ? new string('0', 64) : Hash(path);
                expected[name] = new(name, name, "0.0.0-fixture", Convert.ToBase64String(new byte[64]), Path.Combine(directory, name + ".nupkg"),
                    "lib/net11.0/" + name + ".dll", digest, path, Hash(path));
            }
            // The Tenants body is not mapped by the process: an unmapped non-SDK body is not observed.
            string[] mapped = PlatformCurrentOwnerProof.ObservedSdk.Where(name => fault != "absent-sdk" || name != "Hexalith.EventStore.DomainService")
                .Select(name => Path.Combine(directory, name + ".dll")).ToArray();
            await using var process = await PlatformMappedOwnerProcess.StartAsync(mapped);
            if (fault == "none")
            {
                var (sdk, ownerPackages) = PlatformCurrentOwnerObservation.ReadMappedOwnerPackages(process.Id, directory, expected);
                sdk.Keys.Order(StringComparer.Ordinal).ShouldBe(PlatformCurrentOwnerProof.ObservedSdk.Order(StringComparer.Ordinal));
                sdk.ShouldAllBe(pair => pair.Value == expected[pair.Key].ExpectedSha256);
                ownerPackages.Keys.Order(StringComparer.Ordinal).ShouldBe(PlatformCurrentOwnerProof.ObservedSdk.Order(StringComparer.Ordinal));
                return;
            }
            var refusal = Should.Throw<InvalidDataException>(() => PlatformCurrentOwnerObservation.ReadMappedOwnerPackages(process.Id, directory, expected));
            refusal.Message.ShouldContain(fault == "different-sdk"
                ? "The running Parties artifact 'Hexalith.EventStore.Client' differs from its lock-verified package member"
                : "The actual Parties SDK mapping for 'Hexalith.EventStore.DomainService' is unavailable");
            var withoutBody = expected.Where(pair => pair.Key != "Hexalith.EventStore.Contracts").ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            Should.Throw<InvalidDataException>(() => PlatformCurrentOwnerObservation.ReadMappedOwnerPackages(process.Id, directory, withoutBody))
                .Message.ShouldContain("'Hexalith.EventStore.Contracts' is unavailable");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
