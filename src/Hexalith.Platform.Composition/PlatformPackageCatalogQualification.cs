namespace Hexalith.Platform.Composition;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.Platform.CatalogArtifacts;

/// <summary>Verifies the authenticated test topology and its independent published package bodies.</summary>
internal static class PlatformPackageCatalogQualification
{
    private const string _refusal = "Package composition requires the authenticated EventStore package test with its declared gateway and Tenants topology.";

    /// <summary>Validates the run before registry probes or application resource allocation.</summary>
    internal static IReadOnlyList<PlatformServerBinding> Bind(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations, IReadOnlyList<PlatformServerBinding> servers, bool developmentRun)
    {
        string cohort;
        if (options.ActiveModule == "parties")
        {
            PlatformPartiesPackageCatalogQualification.Bind(options, declarations, servers, developmentRun);
            cohort = RequirePackageCohort(options);
        }
        else
        {
            cohort = ValidateTestInputs(options, declarations, servers, developmentRun);
        }

        VerifyPackageBytes(options, cohort);
        return servers;
    }

    /// <summary>Validates the authenticated EventStore graph, then selects its package-byte verification table.</summary>
    internal static string ValidateTestInputs(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations,
        IReadOnlyList<PlatformServerBinding> servers, bool developmentRun)
    {
        if (!developmentRun || !OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64
            || options.Images != PlatformServerBindings.ReferenceImages
            || !MatchesTestSource(options)
            || !MatchesTopology(options, declarations, servers, ["eventstore", "tenants"], ["eventstore/gateway", "tenants/api", "tenants/domain"]))
            throw new InvalidDataException(_refusal);
        return RequirePackageCohort(options);
    }

    /// <summary>Compares the supplied graph to the exact Builds declaration closure and derived bindings.</summary>
    internal static bool MatchesTopology(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations,
        IReadOnlyList<PlatformServerBinding> servers, IReadOnlyList<string> modules, IReadOnlyList<string> workloads)
    {
        var authority = PlatformDeclarationSet.Load(options).OrderBy(module => PlatformDeclarationSet.Id(module.Declaration), StringComparer.Ordinal).ToArray();
        var supplied = declarations.OrderBy(module => PlatformDeclarationSet.Id(module.Declaration), StringComparer.Ordinal).ToArray();
        if (!authority.Select(module => PlatformDeclarationSet.Id(module.Declaration)).SequenceEqual(modules, StringComparer.Ordinal)
            || authority.Length != supplied.Length
            || !authority.Zip(supplied).All(pair => pair.First.Source == pair.Second.Source
                && JsonElement.DeepEquals(pair.First.Declaration, pair.Second.Declaration))
            || !servers.Select(Identity).Order(StringComparer.Ordinal).SequenceEqual(workloads, StringComparer.Ordinal)
            || options.Mapping.HasGit && !MatchesComposition(options, authority))
            return false;

        var expected = PlatformServerBindings.Resolve(options, authority).ToDictionary(Identity, StringComparer.Ordinal);
        return servers.All(server => expected.TryGetValue(Identity(server), out var declared)
            && server.Module.Source == declared.Module.Source
            && JsonElement.DeepEquals(server.Module.Declaration, declared.Module.Declaration)
            && server.Image == declared.Image && server.ImageDigest is null && server.ImageConfigDigest is null
            && server.ProjectPath == declared.ProjectPath && server.EventStoreHost?.ServerProject == declared.EventStoreHost?.ServerProject
            && server.ResourceId == declared.ResourceId && server.AppId == declared.AppId && server.ReadinessEndpoint == declared.ReadinessEndpoint);
    }

    private static bool MatchesComposition(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations)
    {
        var selected = options.RunPlan!.SelectedTest;
        string digest = Digest(new
        {
            modules = declarations.Select(declaration => new { declaration.Declaration, SourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(declaration.Source))) }),
            selected?.Mode, selected?.DaprRuntimeVersion, retention = false,
        });
        var owners = CompositionQualificationOwners.Normalize(options.QualificationOwners);
        if (owners.Count > 0) digest = Digest(new { purpose = "hexalith.qualification-owners-composition.v1", original = digest, owners });
        return options.RunPlan.CompositionContentIdentity == digest;
    }

    private static string Digest<T>(T input) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input)));
    private static string Identity(PlatformServerBinding server) => PlatformDeclarationSet.Id(server.Module.Declaration) + "/" + server.ServerId;

    /// <summary>Returns the published package-byte table for the verified mapped package versions.</summary>
    internal static string RequirePackageCohort(PlatformCompositionOptions options)
        => (options.ActiveModule == "parties"
                ? PlatformCurrentOwnerProof.SelectsIntegration(options) ? SelectCurrentOwnerCohort(options) : SelectCohort(options, fullParties: true)
                : SelectTestCohort(options))
            ?? throw new InvalidDataException("The pinned catalog has no package byte verification table for "
                + PlatformServerBindings.Version(options, "eventstore", "package verification") + " EventStore and "
                + PlatformServerBindings.Version(options, "tenants", "package verification") + " Tenants with the selected owner dependencies.");

    private static void VerifyPackageBytes(PlatformCompositionOptions options, string cohort)
    {
        _ = PublishedCatalogArtifactCohort.VerifySdk(cohort);
        _ = PublishedCatalogArtifactCohort.Verify(System.Reflection.Assembly.Load("Hexalith.Tenants.Contracts"), cohort);
        if (options.ActiveModule == "parties")
        {
            _ = PublishedCatalogArtifactCohort.Verify(System.Reflection.Assembly.Load("Hexalith.Memories.Contracts"), cohort);
            if (!PlatformCurrentOwnerProof.SelectsIntegration(options))
                _ = PublishedCatalogArtifactCohort.Verify(System.Reflection.Assembly.Load("Hexalith.Parties.Contracts"), cohort);
        }
        else if (cohort is PublishedCatalogArtifactCohort.EventStorePackageTestName or PublishedCatalogArtifactCohort.EventStorePackageTestSdk420Name or PublishedCatalogArtifactCohort.EventStorePackageTestCurrentName)
        {
            _ = PublishedCatalogArtifactCohort.Verify(System.Reflection.Assembly.Load("Hexalith.Tenants.Server"), cohort);
        }
    }

    /// <summary>Checks published SDK bytes independently of declaration and image selection.</summary>
    internal static bool HasQualifiedSdkBytes(string? cohort = null) => cohort is null
        ? PublishedCatalogArtifactCohort.MatchesSdk() || PublishedCatalogArtifactCohort.MatchesSdk(PublishedCatalogArtifactCohort.CurrentName)
        : PublishedCatalogArtifactCohort.MatchesSdk(cohort);

    /// <summary>Selects the existing public package verification table without admitting any execution inputs.</summary>
    internal static string? SelectCohort(PlatformCompositionOptions options, bool fullParties = false)
    {
        string sdk = PlatformServerBindings.Version(options, "eventstore", "package verification");
        string tenants = PlatformServerBindings.Version(options, "tenants", "package verification");
        string? historical = PublishedCatalogArtifactCohort.ForVersions(sdk, tenants, fullParties);
        if (!fullParties) return historical;
        string? mcpCli = PlatformMcpCliCatalogVersion.Resolve(options, "package verification");
        return historical is not null ? mcpCli == "2.0.0" ? historical : null
            : PublishedCatalogArtifactCohort.ForPublishedPartiesVersions(sdk, tenants,
                PlatformServerBindings.Version(options, "memories", "package verification"), PlatformServerBindings.Version(options, "parties", "package verification"), mcpCli);
    }

    /// <summary>Selects the existing package verification table for a declared EventStore test.</summary>
    internal static string? SelectTestCohort(PlatformCompositionOptions options)
    {
        if (!MatchesTestSource(options)) return null;
        return SelectCohort(options) ?? PublishedCatalogArtifactCohort.ForEventStorePackageTestVersions(
            PlatformServerBindings.Version(options, "eventstore", "EventStore package test"),
            PlatformServerBindings.Version(options, "tenants", "EventStore package test"));
    }

    internal static string? SelectCurrentOwnerCohort(PlatformCompositionOptions options)
        => PublishedCatalogArtifactCohort.ForCurrentOwnerVersions(
            PlatformServerBindings.Version(options, "eventstore", "package verification"),
            PlatformServerBindings.Version(options, "tenants", "package verification"),
            PlatformServerBindings.Version(options, "memories", "package verification"),
            PlatformMcpCliCatalogVersion.Resolve(options, "package verification"));

    private static bool MatchesTestSource(PlatformCompositionOptions options)
    {
        CompositionRunPlan? plan = options.RunPlan;
        if (options.Mapping.Mode != WorkspaceMode.Package || options.QualificationOwnerProof?.Matches(options) != true
            || options.Mapping.ActiveModule != "Hexalith.EventStore" || options.ActiveModule != "eventstore" || plan?.PlatformActiveModule != "eventstore"
            || plan.SelectedTest is not { ModuleId: "eventstore", Mode: "package" } selected
            || plan.SelectedTests.Count == 0 || plan.SelectedTests[0] != selected
            || plan.SelectedTests.Select(test => test.Name).Distinct(StringComparer.Ordinal).Count() != plan.SelectedTests.Count
            || plan.PlatformDeclarationPaths.Count != 1
            || !string.Equals(plan.PlatformDeclarationHash, selected.ManifestHash, StringComparison.OrdinalIgnoreCase)
            || !CompositionQualificationOwners.Normalize(options.QualificationOwners).SequenceEqual(["tenants"], StringComparer.Ordinal))
            return false;
        var active = options.Mapping.Entries.SingleOrDefault(entry => entry.Identity == "Hexalith.EventStore" && entry.Origin == "source");
        if (active?.Path is not { } root) return false;
        string manifest = Path.Combine(root, "module", "hexalith.module-manifest.v2.json");
        if (plan.PlatformDeclarationPaths[0] != manifest) return false;
        try
        {
            PlatformPartiesRunProof.ValidateTests(plan, "eventstore", root, manifest, options.Mapping);
            if (options.Mapping.HasGit)
            {
                PlatformPartiesRunProof.RequireCommittedFile(root, manifest);
                foreach (ModuleTestSelection test in plan.SelectedTests)
                    PlatformPartiesRunProof.RequireCommittedFile(root, Path.Combine(root, test.Entry));
                _ = SourceMapping.LoadVerified(Path.Combine(plan.Workspace, "source-mapping", "mapping.json"), options.Mapping.ContentHash);
            }
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return false;
        }
    }
}
