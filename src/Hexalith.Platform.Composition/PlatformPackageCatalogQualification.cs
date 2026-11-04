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
    private const string _refusal = "Run-scoped package catalog composition requires the qualified authenticated EventStore 4.0.4/Tenants 7.0.0 owner SDK cohort; other package cohorts remain unavailable.";

    /// <summary>Checks all finite inputs before image probes, filesystem rendering or resource allocation.</summary>
    internal static IReadOnlyList<PlatformServerBinding> Bind(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations, IReadOnlyList<PlatformServerBinding> servers, bool developmentRun)
    {
        CompositionRunPlan? plan = options.RunPlan;
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
            || PlatformServerBindings.Version(options, "eventstore", "qualification") != "4.0.4"
            || PlatformServerBindings.Version(options, "tenants", "qualification") != "7.0.0"
            || !HasQualifiedSdkBytes() || !PublishedCatalogArtifactCohort.Matches("Hexalith.Tenants.Contracts"))
            throw new InvalidDataException(_refusal);

        return servers.Select(server => server.ServerId switch
        {
            "domain" when server.Image == "registry.hexalith.com/tenants:7.0.0" => server with { ImageDigest = "2f4ab155bc4d33e4b16f1bc8995c45de83503bd8ad99d2c7e46ebbef382b6c42" },
            "api" when server.Image == "registry.hexalith.com/tenants-api:7.0.0" => server with { ImageDigest = "2969ad3f75b154566567dbd6a8f55dd8a64364cbec6fed96398e921379dde9b6" },
            "gateway" when server.Image is null && server.ProjectPath == options.EventStoreSeedProject => server,
            _ => throw new InvalidDataException(_refusal),
        }).ToArray();
    }

    /// <summary>The loaded SDK must be the finite signed public producer, including its technical receiver API.</summary>
    internal static bool HasQualifiedSdkBytes() => PublishedCatalogArtifactCohort.MatchesSdk();

    private static bool Snapshot(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations, string owner, string expected)
    {
        PlatformModuleDeclaration selected = declarations.Single(module => PlatformDeclarationSet.Id(module.Declaration) == owner);
        if (Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(selected.Source))) != expected) return false;
        PlatformModuleDeclaration authoritative = PlatformDeclarationSet.Load(options).Single(module => PlatformDeclarationSet.Id(module.Declaration) == owner);
        return selected.Source == authoritative.Source && JsonElement.DeepEquals(selected.Declaration, authoritative.Declaration);
    }

}
