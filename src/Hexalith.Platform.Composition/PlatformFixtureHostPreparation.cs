namespace Hexalith.Platform.Composition;

using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;

/// <summary>Requires the packaged assembly's observation before the owner can issue native-test readiness.</summary>
internal sealed class PlatformFixtureHostPreparation(CompositionRunPlan plan, string catalogDirectory,
    (string PackageId, string Version, string ArchiveSha256, string LibrarySha256) expected)
    : ICompositionRunPreparation
{
    internal PlatformFixtureHostPreparation(CompositionRunPlan plan, CompositionFixtureProducer producer, string catalogDirectory)
        : this(plan, catalogDirectory, (producer.PackageId, producer.Version, producer.ArchiveSha256, producer.LibrarySha256))
    {
    }

    public async Task PrepareAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        string report = Path.Combine(plan.Workspace, "fixture-host-loaded.json");
        while (!File.Exists(report)) await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        FileInfo file = new(report);
        if (file.LinkTarget is not null || file.Length is <= 0 or > 32_768) throw PlatformFixturePackageQualification.Refusal();

        byte[] raw = await File.ReadAllBytesAsync(report, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(raw);
        JsonElement body = document.RootElement;
        string outputRoot = Path.Combine(plan.Workspace, "project-builds", "eventstore");
        string path = body.GetProperty("loadedAssembly").GetString()!;
        int processId = body.GetProperty("processId").GetInt32();
        if (!Path.IsPathFullyQualified(path) || !Path.GetFullPath(path).StartsWith(outputRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.GetFileName(path) != expected.PackageId + ".dll" || body.GetProperty("runId").GetString() != plan.RunId
            || body.GetProperty("packageId").GetString() != expected.PackageId || body.GetProperty("version").GetString() != expected.Version
            || body.GetProperty("extensionId").GetString() != PlatformFixturePackageQualification.Extension || body.GetProperty("apiMajor").GetInt32() != 1
            || body.GetProperty("archiveSha256").GetString() != expected.ArchiveSha256 || body.GetProperty("librarySha256").GetString() != expected.LibrarySha256
            || body.GetProperty("assetsType").GetString() != "package" || body.GetProperty("depsType").GetString() != "package"
            || processId <= 0 || PlatformFixturePackageQualification.Hash(path) != expected.LibrarySha256)
            throw PlatformFixturePackageQualification.Refusal();

        string process = Path.Combine("/proc", processId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        string variables = await File.ReadAllTextAsync(Path.Combine(process, "environ"), cancellationToken).ConfigureAwait(false);
        string command = await File.ReadAllTextAsync(Path.Combine(process, "cmdline"), cancellationToken).ConfigureAwait(false);
        if (!variables.Split('\0').Contains(CompositionEnvironment.RunId + "=" + plan.RunId, StringComparer.Ordinal)
            || !command.Split('\0').Any(argument => Path.GetFileName(argument) is "Hexalith.Platform.EventStoreHost" or "Hexalith.Platform.EventStoreHost.dll"))
            throw PlatformFixturePackageQualification.Refusal();

        await File.WriteAllBytesAsync(Path.Combine(catalogDirectory, "fixture-host-loaded.json"), raw, cancellationToken).ConfigureAwait(false);
    }
}
