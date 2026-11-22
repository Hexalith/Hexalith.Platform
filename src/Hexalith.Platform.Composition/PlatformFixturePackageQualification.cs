namespace Hexalith.Platform.Composition;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.EventStore.Contracts.Routing;

/// <summary>Validates the one signed fixture's separate package composition purpose.</summary>
internal sealed class PlatformFixturePackageQualification
{
    internal const string Module = "platformtestextension";
    internal const string Package = "Hexalith.Platform.TestExtension";
    internal const string Extension = "platform-test";
    internal const string MarkerKey = Module + "/" + Extension + "/PlatformTestExtension:Marker";
    internal const string Marker = "story32-signed-fixture";

    private static readonly string[] _modules = ["eventstore", "memories", Module, "tenants"];
    private static readonly string[] _servers =
    [
        "eventstore/gateway", "memories/clock", "memories/domain", "memories/server", "memories/telemetry", "tenants/api", "tenants/domain",
    ];
    private static readonly Dictionary<string, string> _owners = new(StringComparer.Ordinal)
    {
        ["eventstore"] = "5.4.0", ["memories"] = "4.1.0", ["tenants"] = "8.0.4",
    };
    private static readonly Dictionary<string, (string Manifest, string Configuration)> _images = new(StringComparer.Ordinal)
    {
        ["memories/clock"] = ("b2ea6f42b32f403a64818d16118f40a11b000889322c81bdc0fd65eab7788572", "6f8c7ae4647ca6e0c9ec38dbc6d2930b35420bd6687527ea3abb97e01ab56ddb"),
        ["memories/domain"] = ("dbf81b2eea4d97cd50abca3dafab87ca00e6fe144de01305fc00aeaf0ef2ee64", "9fb52b28164fa7edd41ec3d21a1e6daf462ef8481f187e9cee36ab73298f1950"),
        ["memories/server"] = ("0d52ceb355eb2000b843549bd23aa685c3566c8953a97e36eeed46f7e18d9c1c", "c16528a774309db78e2d8d27dd8a86934d2f7f8889dec6560c80e38e9850c18d"),
        ["memories/telemetry"] = ("03c1f01bfb44d3a97819684e5cc638508c303aba7e723b8a6939e832edd3d63e", "a23ab4ad925b0e84cb2eca0a8adff4a8a9637f1b8a8e0b0663506d6f7589a2c8"),
        ["tenants/api"] = ("38e38bc98150b2fe3d4b8011494af65387fabe63d98e6770f00a26ee6ee0c7e4", "68dd0bc0f23e19162f0690978e318b0add5b562ee252da4ba4e7b7358efcc037"),
        ["tenants/domain"] = ("0a2290fc85d7161dec46bc0c0d616933d89efa77cb1d100ea9a91d6fb2685600", "6f988ceef8245f15ee281e4448ca39ec318e493cdb3d1d202c116242904dc57f"),
    };

    private readonly CompositionRunPlan _plan;
    private readonly string _seed;
    private readonly IReadOnlyList<string> _hostSources;
    private readonly string _configurationHash;

    private PlatformFixturePackageQualification(PlatformCompositionOptions options, string seed, IReadOnlyDictionary<string, string> settings,
        CompositionFixtureProducer producer)
    {
        _plan = options.RunPlan!;
        _seed = seed;
        _hostSources = HostSources(options);
        _configurationHash = ConfigurationHash(options);
        Settings = settings;
        Producer = producer;
    }

    internal CompositionFixtureProducer Producer { get; }

    internal IReadOnlyDictionary<string, string> Settings { get; }

    internal static bool Selects(PlatformCompositionOptions options) => options.Mapping.Mode == WorkspaceMode.Package && options.ActiveModule == Module;

    internal static PlatformFixturePackageQualification? Authenticate(PlatformCompositionOptions options, string signingKey)
    {
        CompositionRunPlan plan = options.RunPlan ?? throw Refusal("run-plan");
        CompositionFixtureProducer? producer = CompositionFixtureProducer.Verify(plan, signingKey);
        if (producer is null) return null;
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64 || !Selects(options)
            || plan.PlatformIdentity?.PlatformPath != options.PlatformRoot || plan.SourceMappingHash != options.Mapping.ContentHash
            || plan.PlatformModelAssemblyPath != typeof(PlatformAppModel).Assembly.Location || plan.QualificationOwners is { Count: > 0 }
            || producer.RunId != plan.RunId || producer.PackageId != Package || producer.Version != "2.2.5"
            || producer.SourceCommit != "0aacf13244fa6b1d9e08d627149d72cf12229167"
            || producer.ArchiveSha256 != "7c3b432aba236b9cd102b6fad98a15ed629b3dcdfb9bad9ee6a3d105f63d04b4"
            || producer.LibrarySha256 != "650d37d4aa1ae6de6c4633c1ecafa763349ed8c29f1c7e24d257ec0853d3f004")
            throw Refusal("signed-fixture");

        var (seed, settings) = PlatformFixtureToolRelease.Verify(options);
        return new(options, seed, settings, producer);
    }

    internal bool Matches(PlatformCompositionOptions options)
        => Selects(options) && ReferenceEquals(options.RunPlan, _plan) && options.Mapping.ContentHash == _plan.SourceMappingHash
            && options.PlatformRoot == _plan.PlatformIdentity?.PlatformPath && options.DeclarationPaths.SequenceEqual(_plan.PlatformDeclarationPaths, StringComparer.Ordinal)
            && options.EventStoreSeedProject == _seed && (options.EnvironmentId is null || options.EnvironmentId == _plan.EnvironmentId)
            && options.QualificationOwners.Count == 0 && options.QualificationOwnerProof is null && options.PartiesRunProof is null
            && options.Images == PlatformServerBindings.ReferenceImages && options.Projects == PlatformServerBindings.ReferenceProjects
            && !options.TimesheetsEnabled && !options.UsePrebuiltProjects && options.RetentionProfile is null && options.ComponentsDirectory is null
            && options.DomainModule is null && options.ReleaseCommits == CompositionDeclarationOptions.DefaultReleaseCommits
            && options.ReleaseSnapshotHashes == CompositionDeclarationOptions.DefaultReleaseSnapshotHashes
            && options.CatalogGeneration == 1 && options.CommittedRoutingCatalogPath is null && options.CommittedRoutingCatalogRootDigest is null
            && options.Configuration.GetValueOrDefault(MarkerKey) == Marker && ConfigurationHash(options) == _configurationHash
            && HostSources(options).SequenceEqual(_hostSources, StringComparer.Ordinal);

    internal static IReadOnlyList<string> RequireOwners(PlatformCompositionOptions options)
    {
        if (options.FixturePackageProof?.Matches(options) != true) throw Refusal("fixture-owner-scope");
        if (_owners.Any(pair => PlatformServerBindings.Version(options, pair.Key, "signed fixture cohort") != pair.Value))
            throw Refusal("fixture-owner-versions");
        return [.. _owners.Keys];
    }

    internal static void ValidateTopology(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations,
        IReadOnlyList<PlatformServerBinding> servers, bool developmentRun)
    {
        PlatformFixturePackageQualification proof = options.FixturePackageProof ?? throw Refusal("fixture-topology-proof");
        if (!developmentRun || !proof.Matches(options)) throw Refusal("fixture-topology-scope");
        if (!PlatformPackageCatalogQualification.MatchesTopology(options, declarations, servers, _modules, _servers)) throw Refusal("fixture-topology-closure");

        PlatformServerBinding gateway = servers.Single(server => server.ResourceId == "eventstore");
        if (gateway.ProjectPath != PlatformEventStoreExtensionBindings.HostProjectPath(options)
            || gateway.EventStoreHost is not { } host || host.ServerProject != options.EventStoreSeedProject || host.Extensions.Count != 1
            || host.Extensions[0] is not { Module: Module, PackageId: Package, ExtensionId: Extension, ApiMajor: 1 } extension
            || extension.PackageVersion != proof.Producer.Version || extension.Reference != Package)
            throw Refusal("fixture-gateway-binding");
        // Builds declarations carry a display path relative to the AppHost; the signed plan carries an absolute path.
        if (Path.GetFullPath(extension.Source) != Path.GetFullPath(options.DeclarationPaths.Single()))
            throw Refusal("fixture-gateway-declaration");
    }

    internal static void VerifyImage(PlatformServerBinding server)
    {
        string identity = PlatformDeclarationSet.Id(server.Module.Declaration) + "/" + server.ServerId;
        if (!_images.TryGetValue(identity, out var expected) || server.ImageDigest != expected.Manifest || server.ImageConfigDigest != "sha256:" + expected.Configuration)
            throw Refusal("fixture-owner-image-" + identity);
    }

    internal static void VerifyReader(RouteCatalogEnvelope catalog, JsonElement receipt)
    {
        static IEnumerable<string?> Files(JsonElement receipt, string property)
            => receipt.GetProperty(property).EnumerateArray().Select(file => Path.GetFileName(file.GetString())).Order(StringComparer.Ordinal);

        if (catalog.Generation != 1 || catalog.Routes.Count != 30 || catalog.Idempotency.Count != 19
            || catalog.RouteFacetDigest != "44c6a777693baf59cdc2b575beaebd0a3ba74c610c78e5a2a4a13280fe9f5c03"
            || catalog.IdempotencyFacetDigest != "676c1d71d38a8335e4bfad713152a995a8333ae44c3086759c822d6072878f83"
            || !Files(receipt, "ownerAssemblies").SequenceEqual(["Hexalith.Memories.EventStore.dll", "Hexalith.Tenants.Contracts.dll"], StringComparer.Ordinal)
            || !Files(receipt, "adapterAssemblies").SequenceEqual(["Hexalith.Memories.EventStoreExtensions.dll", "Hexalith.Tenants.Server.dll"], StringComparer.Ordinal))
            throw Refusal("fixture-reader-inventory");
    }

    internal static InvalidDataException Refusal([System.Runtime.CompilerServices.CallerMemberName] string stage = "admission")
        => new($"{Module}: eventstore-extension-fixture-refused ({stage}). The signed fixture requires its authenticated package, named catalog view, composed host, and released workload cohort.");

    private static string ConfigurationHash(PlatformCompositionOptions options)
        => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(options.Configuration.OrderBy(pair => pair.Key, StringComparer.Ordinal))));

    internal static IReadOnlyList<string> HostSources(PlatformCompositionOptions options)
    {
        string project = PlatformEventStoreExtensionBindings.HostProjectPath(options);
        DirectoryInfo host = new(Path.GetDirectoryName(project)!);
        DirectoryInfo sources = host.Parent!;
        string[] files = [Path.GetFileName(project), "Program.cs", "SignedFixtureObservation.cs"];
        if (!host.Exists || host.LinkTarget is not null || sources.LinkTarget is not null
            || host.EnumerateFiles().Any(file => !files.Contains(file.Name, StringComparer.Ordinal))
            || host.EnumerateDirectories().Any(directory => directory.Name is not ("bin" or "obj") || directory.LinkTarget is not null)
            || sources.EnumerateFileSystemInfos().Any(entry => entry.Name.Equals("Directory.Build.props", StringComparison.OrdinalIgnoreCase)
                || entry.Name.Equals("Directory.Build.targets", StringComparison.OrdinalIgnoreCase)
                || entry.Name.Equals("Directory.Build.rsp", StringComparison.OrdinalIgnoreCase)
                || entry.Name.Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase)
                || entry.Name.Equals("NuGet.Config", StringComparison.OrdinalIgnoreCase) || entry.Name.Equals("global.json", StringComparison.OrdinalIgnoreCase)))
            throw Refusal();
        List<string> bodies = [];
        foreach (string name in files)
        {
            string path = Path.Combine(host.FullName, name);
            PlatformPartiesRunProof.RequireCommittedFile(options.PlatformRoot, path);
            bodies.Add(Hash(path));
        }
        if (bodies[0] != "55fea30c12b61aaf350b9c88fb29b2c40ac881887b23dab757cb57e17d20f7aa") throw Refusal();
        return bodies;
    }

    internal static string Hash(string path)
    {
        FileInfo file = new(path);
        if (!file.Exists || file.LinkTarget is not null || file.Length <= 0) throw Refusal();
        foreach (DirectoryInfo directory in Ancestors(file.Directory))
            if (directory.LinkTarget is not null) throw Refusal();
        using FileStream stream = file.OpenRead();
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static IEnumerable<DirectoryInfo> Ancestors(DirectoryInfo? directory)
    {
        for (; directory is not null; directory = directory.Parent) yield return directory;
    }
}
