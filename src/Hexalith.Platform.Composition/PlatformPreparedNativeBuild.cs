namespace Hexalith.Platform.Composition;

using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.Platform.CatalogArtifacts;

/// <summary>Admits the verified Release files used by the bounded package-mode composition.</summary>
internal static class PlatformPreparedNativeBuild
{
    /// <summary>Uses the integration closure whenever a batch includes the current owner profile.</summary>
    internal static string Profile(PlatformCompositionOptions options)
        => PlatformCurrentOwnerProof.SelectsIntegration(options) ? PlatformCurrentOwnerProof.Profile : options.RunPlan!.SelectedTest!.Name;

    /// <summary>Verifies a measured composition component in the owner-private inventory.</summary>
    internal static string Entry(PlatformCompositionOptions options, string dependency)
    {
        CompositionRunPlan plan = options.RunPlan ?? throw Refusal();
        PreparedNativeBuild build = plan.PreparedNativeBuild ?? throw Refusal();
        return build.VerifyEntry(plan.Workspace, PreparedNativeBuild.CompositionRole(Profile(options), dependency));
    }

    /// <summary>Checks the complete configured EventStore closure before building the resource graph.</summary>
    internal static string EventStoreEntry(PlatformCompositionOptions options, PlatformServerBinding gateway)
    {
        string entry = Entry(options, "owner.eventstore");
        string? project = Path.GetFileName(gateway.ProjectPath) == PackagedHostProject.ProjectFileName
            ? "Hexalith.Builds.Module.EventStoreHost" : Path.GetFileNameWithoutExtension(gateway.ProjectPath);
        if (Path.GetFileNameWithoutExtension(entry) != project)
            throw Refusal();

        string output = Path.GetDirectoryName(entry)!;
        OwnerPackageLock owners = PlatformPackageCatalogQualification.RequireLockedPackages(options);
        using JsonDocument graph = JsonDocument.Parse(File.ReadAllBytes(Path.ChangeExtension(entry, ".deps.json")));
        IReadOnlyDictionary<string, OwnerPackageAsset> assets = OwnerPackageLock.ReadAssets(graph.RootElement);
        IReadOnlyList<OwnerPackageIdentity> checkedPackages = owners.VerifyDirectory(graph.RootElement,
            [Path.Combine(output, "packages")], output, new HashSet<string>(StringComparer.Ordinal));
        string version = PlatformServerBindings.Version(options, "eventstore", "prepared owner");
        string[] required = gateway.EventStoreHost is null
            ? ["Hexalith.EventStore.Server", "Hexalith.EventStore.ServiceDefaults", "Hexalith.EventStore.Contracts", "Hexalith.EventStore.Gateway"]
            : ["Hexalith.EventStore.Server", "Hexalith.EventStore.ServiceDefaults", "Hexalith.EventStore.Contracts"];
        foreach (string package in required)
            if (!checkedPackages.Any(identity => identity.Assembly == package && identity.PackageId == package && identity.Version == version))
                throw Refusal();
        foreach (PlatformEventStoreExtensionBinding extension in gateway.EventStoreHost?.Extensions ?? [])
            if (!assets.Values.Any(asset => asset.PackageId == extension.PackageId && asset.Version == extension.PackageVersion)
                || !checkedPackages.Any(asset => asset.PackageId == extension.PackageId && asset.Version == extension.PackageVersion))
                throw Refusal();

        return entry;
    }

    /// <summary>Returns a uniform refusal without including runtime configuration or credentials.</summary>
    internal static InvalidDataException Refusal() =>
        new("The prepared Platform package run requires its matching catalog reader and verified Release owner closures.");
}
