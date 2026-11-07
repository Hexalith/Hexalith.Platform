namespace Hexalith.Platform.Composition;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.Platform.CatalogArtifacts;

/// <summary>Admits only the independently measured published SDK/Tenants qualification cohort.</summary>
internal static class PlatformPackageCatalogQualification
{
    private const string _refusal = "Run-scoped package catalog composition requires the qualified authenticated EventStore 4.0.4/Tenants 7.0.0 or EventStore 4.1.0/Tenants 7.0.2 owner SDK cohort, or the exact private EventStore 4.1.0/Tenants 7.0.5 package-test purpose; other package cohorts remain unavailable.";

    /// <summary>Checks all finite inputs before image probes, filesystem rendering or resource allocation.</summary>
    internal static IReadOnlyList<PlatformServerBinding> Bind(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations, IReadOnlyList<PlatformServerBinding> servers, bool developmentRun)
    {
        if (options.ActiveModule == "parties")
            return PlatformPartiesPackageCatalogQualification.Bind(options, declarations, servers, developmentRun);
        CompositionRunPlan? plan = options.RunPlan;
        string? cohort = null;
        if (!developmentRun || !OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64
            || options.Mapping.Mode != WorkspaceMode.Package || options.QualificationOwnerProof?.Matches(options) != true
            || options.ActiveModule != "eventstore" || plan?.PlatformActiveModule != "eventstore"
            || plan.SelectedTest is not { ModuleId: "eventstore", Mode: "package" }
            || plan.SelectedTests.Count == 0 || plan.SelectedTests.Any(test => test.ModuleId != "eventstore" || test.Mode != "package")
            || !CompositionQualificationOwners.Normalize(options.QualificationOwners).SequenceEqual(["tenants"], StringComparer.Ordinal)
            || !declarations.Select(module => PlatformDeclarationSet.Id(module.Declaration)).Order(StringComparer.Ordinal).SequenceEqual(["eventstore", "tenants"], StringComparer.Ordinal)
            || !servers.Select(server => PlatformDeclarationSet.Id(server.Module.Declaration) + "/" + server.ServerId).Order(StringComparer.Ordinal).SequenceEqual(["eventstore/gateway", "tenants/api", "tenants/domain"], StringComparer.Ordinal)
            || !Snapshot(options, declarations, "eventstore", "21eda6da824d5ae6135ff342dd8a5aae950b02420319b953a836dddeb00ce495")
            || !Snapshot(options, declarations, "tenants", "1588f3428ec29296c452fdd35bf95146142315aad3ad8ceb934e7a791f1fb522")
            || (cohort = SelectTestCohort(options)) is null
            || cohort == PublishedCatalogArtifactCohort.EventStorePackageTestName && servers.Any(server => server.ImageDigest is not null)
            || !HasQualifiedSdkBytes(cohort) || !PublishedCatalogArtifactCohort.Matches("Hexalith.Tenants.Contracts", cohort)
            || cohort == PublishedCatalogArtifactCohort.EventStorePackageTestName && !PublishedCatalogArtifactCohort.Matches("Hexalith.Tenants.Server", cohort))
            throw new InvalidDataException(_refusal);

        return servers.Select(server => (cohort, server.ServerId, server.Image) switch
        {
            (PublishedCatalogArtifactCohort.Name, "domain", "registry.hexalith.com/tenants:7.0.0") => server with { ImageDigest = "2f4ab155bc4d33e4b16f1bc8995c45de83503bd8ad99d2c7e46ebbef382b6c42" },
            (PublishedCatalogArtifactCohort.Name, "api", "registry.hexalith.com/tenants-api:7.0.0") => server with { ImageDigest = "2969ad3f75b154566567dbd6a8f55dd8a64364cbec6fed96398e921379dde9b6" },
            (PublishedCatalogArtifactCohort.CurrentName, "domain", "registry.hexalith.com/tenants:7.0.2") => server with { ImageDigest = "adfcad2eea7c0b59e2936a62131c7925af518f1331a4e9fec0071da55874caed" },
            (PublishedCatalogArtifactCohort.CurrentName, "api", "registry.hexalith.com/tenants-api:7.0.2") => server with { ImageDigest = "2922e0b1eb7b115bac472479213ca46d84c26ac4917b1d53854034618179c2e4" },
            (PublishedCatalogArtifactCohort.EventStorePackageTestName, "domain", "registry.hexalith.com/tenants:7.0.5") => server with { ImageDigest = "1ba6ff9dd0623817d667bd46229dd0ba34a23ffc51d48c889d3e67f474bec22a" },
            (PublishedCatalogArtifactCohort.EventStorePackageTestName, "api", "registry.hexalith.com/tenants-api:7.0.5") => server with { ImageDigest = "f4707de77c595977d5b7913b4e6119aed407da3c72475ba13b46e9e302d1ef7b" },
            (_, "gateway", null) when server.ProjectPath == options.EventStoreSeedProject => server,
            _ => throw new InvalidDataException(_refusal),
        }).ToArray();
    }

    /// <summary>The loaded SDK must be the finite signed public producer, including its technical receiver API.</summary>
    internal static bool HasQualifiedSdkBytes(string? cohort = null) => cohort is null
        ? PublishedCatalogArtifactCohort.MatchesSdk() || PublishedCatalogArtifactCohort.MatchesSdk(PublishedCatalogArtifactCohort.CurrentName)
        : PublishedCatalogArtifactCohort.MatchesSdk(cohort);

    /// <summary>Binds the selected SDK/Tenants versions to one complete measured public cohort; mixed pairs have no authority.</summary>
    internal static string? SelectCohort(PlatformCompositionOptions options, bool fullParties = false)
    {
        if (fullParties && (options.CurrentOwnerProof is not null
            || options.RunPlan?.SelectedTest?.Name == PlatformCurrentOwnerProof.Profile
            || options.RunPlan?.SelectedTests.Any(test => test.Name == PlatformCurrentOwnerProof.Profile) == true)) return null;
        string sdk = PlatformServerBindings.Version(options, "eventstore", "qualification");
        string tenants = PlatformServerBindings.Version(options, "tenants", "qualification");
        string? historical = PublishedCatalogArtifactCohort.ForVersions(sdk, tenants, fullParties);
        return historical is not null || !fullParties ? historical
            : PublishedCatalogArtifactCohort.ForPublishedPartiesVersions(sdk, tenants,
                PlatformServerBindings.Version(options, "memories", "qualification"), PlatformServerBindings.Version(options, "parties", "qualification"));
    }

    /// <summary>Selects old public pairs or the separately authenticated exact EventStore package-test purpose; performs no resource effects.</summary>
    internal static string? SelectTestCohort(PlatformCompositionOptions options)
    {
        string? historical = SelectCohort(options);
        if (historical is not null) return historical;
        CompositionRunPlan? plan = options.RunPlan;
        return options.Mapping.Mode == WorkspaceMode.Package && options.QualificationOwnerProof?.Matches(options) == true
            && options.Mapping.ActiveModule == "Hexalith.EventStore" && options.ActiveModule == "eventstore" && plan?.PlatformActiveModule == "eventstore"
            && plan.SelectedTest is { ModuleId: "eventstore", Name: "platform-package", Executor: "mtp", Mode: "package",
                Entry: "tests/Hexalith.EventStore.Platform.IntegrationTests/Hexalith.EventStore.Platform.IntegrationTests.csproj",
                ExecutionTimeoutSeconds: 120, StopTimeoutSeconds: 90, DaprRuntimeVersion: null } selected
            && plan.SelectedTests.Count == 1 && plan.SelectedTests[0] == selected
            && string.Equals(selected.ManifestHash, plan.PlatformDeclarationHash, StringComparison.OrdinalIgnoreCase)
            && string.Equals(plan.PlatformDeclarationHash, "21eda6da824d5ae6135ff342dd8a5aae950b02420319b953a836dddeb00ce495", StringComparison.OrdinalIgnoreCase)
            && MatchesTestSource(options, selected)
            && CompositionQualificationOwners.Normalize(options.QualificationOwners).SequenceEqual(["tenants"], StringComparer.Ordinal)
            && PlatformServerBindings.Version(options, "eventstore", "private EventStore package test") == "4.1.0"
            && PlatformServerBindings.Version(options, "tenants", "private EventStore package test") == "7.0.5"
            ? PublishedCatalogArtifactCohort.EventStorePackageTestName : null;
    }

    internal static string? SelectCurrentOwnerCohort(PlatformCompositionOptions options)
        => PlatformServerBindings.Version(options, "eventstore", "current qualification") == "4.1.0"
            && PlatformServerBindings.Version(options, "tenants", "current qualification") == "7.0.5"
            && PlatformServerBindings.Version(options, "memories", "current qualification") == "3.0.25"
            ? PublishedCatalogArtifactCohort.CurrentOwnerName : null;

    private static bool MatchesTestSource(PlatformCompositionOptions options, ModuleTestSelection selected)
    {
        SourceMappingEntry[] active = options.Mapping.Entries.Where(entry => entry.Identity == "Hexalith.EventStore" && entry.Origin == "source" && entry.Path is not null).ToArray();
        if (active.Length != 1 || options.RunPlan!.PlatformDeclarationPaths.Count != 1) return false;
        string manifest = Path.GetFullPath(Path.Combine(active[0].Path!, "module", "hexalith.module-manifest.v2.json"));
        string entry = Path.Combine(active[0].Path!, selected.Entry);
        return Path.GetFullPath(options.RunPlan.PlatformDeclarationPaths[0]) == manifest && File.Exists(manifest)
            && string.Equals(selected.ManifestHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifest))), StringComparison.OrdinalIgnoreCase)
            && File.Exists(entry) && string.Equals(selected.EntryHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(entry))), StringComparison.OrdinalIgnoreCase);
    }

    private static bool Snapshot(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations, string owner, string expected)
    {
        PlatformModuleDeclaration selected = declarations.Single(module => PlatformDeclarationSet.Id(module.Declaration) == owner);
        if (Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(selected.Source))) != expected) return false;
        PlatformModuleDeclaration authoritative = PlatformDeclarationSet.Load(options).Single(module => PlatformDeclarationSet.Id(module.Declaration) == owner);
        return selected.Source == authoritative.Source && JsonElement.DeepEquals(selected.Declaration, authoritative.Declaration);
    }

}
