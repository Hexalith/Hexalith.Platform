namespace Hexalith.Platform.Composition;

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
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
        OwnerPackageLock owners;
        if (options.ActiveModule == "parties")
        {
            PlatformPartiesPackageCatalogQualification.Bind(options, declarations, servers, developmentRun);
            owners = RequireLockedPackages(options);
        }
        else
        {
            owners = ValidateTestInputs(options, declarations, servers, developmentRun);
        }

        _ = VerifyPackageBytes(options, owners);
        return servers;
    }

    /// <summary>Validates the authenticated EventStore graph, then admits its catalog-selected owner packages through the lock.</summary>
    internal static OwnerPackageLock ValidateTestInputs(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations,
        IReadOnlyList<PlatformServerBinding> servers, bool developmentRun)
    {
        if (!developmentRun || !OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64
            || options.Images != PlatformServerBindings.ReferenceImages
            || !MatchesTestSource(options)
            || !MatchesTopology(options, declarations, servers, ["eventstore", "tenants"], ["eventstore/gateway", "tenants/api", "tenants/domain"]))
            throw new InvalidDataException(_refusal);
        return RequireLockedPackages(options);
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

    /// <summary>Admits the catalog-selected owner versions by rule, before any run effect.</summary>
    /// <remarks>
    /// Each owner of the declared graph must have its loaded contract and adapter packages locked, and every locked package of its
    /// family must name exactly its catalog-selected version. Adopting a release is a version bump plus a regenerated lock.
    /// Run-built Parties bodies of the integration profile keep their run-bound authority and are not locked.
    /// </remarks>
    /// <param name="options">The authenticated package-mode composition.</param>
    internal static OwnerPackageLock RequireLockedPackages(PlatformCompositionOptions options)
    {
        if (options.ActiveModule != "parties" && !MatchesTestSource(options)) throw new InvalidDataException(_refusal);
        return RequireLockedPackages(options, OwnerPackageLock.Load(OwnerPackageLock.PathFor(options.PlatformRoot)));
    }

    /// <summary>Applies the version admission rule of <see cref="RequireLockedPackages(PlatformCompositionOptions)"/> to a loaded lock.</summary>
    /// <param name="options">The authenticated package-mode composition.</param>
    /// <param name="owners">The owner package lock.</param>
    internal static OwnerPackageLock RequireLockedPackages(PlatformCompositionOptions options, OwnerPackageLock owners)
    {
        bool runBoundParties = options.ActiveModule == "parties" && PlatformCurrentOwnerProof.SelectsIntegration(options);
        string[] modules = PlatformDeclarationSet.Load(options).Select(module => PlatformDeclarationSet.Id(module.Declaration))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (!modules.Contains("eventstore", StringComparer.Ordinal)) throw new InvalidDataException(_refusal);
        RequireLockedVersions(owners, options, modules.Where(module => !runBoundParties || module != "parties"));
        RequireLockedMcpCli(owners, options, required: options.ActiveModule == "parties");
        return owners;
    }

    /// <summary>Requires each owner's loaded packages, and every locked package of its family, to name its catalog-selected version.</summary>
    /// <param name="owners">The owner package lock.</param>
    /// <param name="options">The composition whose catalog selects the versions.</param>
    /// <param name="modules">The declared owner modules whose packages are locked.</param>
    internal static void RequireLockedVersions(OwnerPackageLock owners, PlatformCompositionOptions options, IEnumerable<string> modules)
    {
        foreach (string module in modules)
        {
            string version = PlatformServerBindings.Version(options, module, "owner package lock");
            foreach (string package in LoadedPackages(module)) _ = owners.Require(package, version);
            owners.RequireFamily(PlatformServerBindings.Repository(module) + ".", version);
        }
    }

    /// <summary>Requires the MCP abstraction family to be locked at the catalog's single MCP pin.</summary>
    /// <param name="owners">The owner package lock.</param>
    /// <param name="options">The composition whose catalog selects the pin.</param>
    /// <param name="required">Whether the selected owners depend on the abstraction package.</param>
    internal static void RequireLockedMcpCli(OwnerPackageLock owners, PlatformCompositionOptions options, bool required)
    {
        string? mcpCli = PlatformMcpCliCatalogVersion.Resolve(options, "owner package lock");
        if (mcpCli is not null) owners.RequireFamily("Hexalith.McpCli.", mcpCli);
        else if (required)
            throw new InvalidDataException("The pinned catalog selects no Hexalith.McpCli.Abstractions version for the Parties owner packages.");
    }

    /// <summary>Checks the owner assemblies loaded by this composition against the lock-verified archives (AR-12).</summary>
    /// <remarks>The selected owners' contracts are loaded first; every owner package assembly in the default load context is then checked too.</remarks>
    /// <param name="options">The admitted composition.</param>
    /// <param name="owners">The lock admitted for its catalog selection.</param>
    internal static IReadOnlyList<OwnerPackageIdentity> VerifyPackageBytes(PlatformCompositionOptions options, OwnerPackageLock owners)
    {
        bool runBoundParties = options.ActiveModule == "parties" && PlatformCurrentOwnerProof.SelectsIntegration(options);
        List<string> names = [.. LoadedPackages("eventstore"), "Hexalith.Tenants.Contracts"];
        if (options.ActiveModule == "parties")
        {
            names.Add("Hexalith.Memories.Contracts");
            if (!runBoundParties) names.Add("Hexalith.Parties.Contracts");
        }
        else
        {
            names.Add("Hexalith.Tenants.Server");
        }

        IReadOnlyList<string> folders = OwnerPackageLock.PackageFolders(typeof(PlatformPackageCatalogQualification).Assembly);
        IReadOnlyList<OwnerPackageIdentity> selected = owners.VerifyLoaded(names, folders);
        Assembly[] loaded = AssemblyLoadContext.Default.Assemblies.Where(assembly => !assembly.IsDynamic
            && OwnerPackageLock.IsOwnerPackage(assembly.GetName().Name)
            && !(runBoundParties && assembly.GetName().Name!.StartsWith("Hexalith.Parties.", StringComparison.Ordinal))
            && !selected.Any(identity => identity.LoadedPath == assembly.Location)).ToArray();
        return [.. selected.Concat(owners.VerifyAssemblies(loaded, folders)).OrderBy(identity => identity.Assembly, StringComparer.Ordinal)];
    }

    /// <summary>Names the owner packages Composition and its reader load for one declared owner.</summary>
    /// <param name="module">The declared owner module.</param>
    internal static IReadOnlyList<string> LoadedPackages(string module)
    {
        if (module == "eventstore") return ["Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults", "Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService"];
        (string contract, string adapter) = PlatformCatalogContractBinding.Packages(module);
        string contracts = PlatformServerBindings.Repository(module) + ".Contracts";
        return new[] { contracts, contract, adapter }.Distinct(StringComparer.Ordinal).ToArray();
    }

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
