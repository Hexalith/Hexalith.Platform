namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;

/// <summary>Uses genuine published declaration bytes with synthetic owned entry files; confers no package runtime acceptance.</summary>
internal sealed class PlatformQualificationPackageFixture : IDisposable
{
    /// <summary>Selects the authenticated historical public SDK 4.1.0/Tenants 7.0.2 inputs for existing controls.</summary>
    internal static PlatformQualificationPackageFixture HistoricalPublic() => new(currentTest: false);

    /// <summary>Selects actual checkout provenance for separate current-test controls; grants no public or runtime acceptance.</summary>
    internal static PlatformQualificationPackageFixture CurrentTest() => new(currentTest: true);

    /// <summary>Uses the independently released SDK4.2 source binding in an isolated component fixture.</summary>
    internal static PlatformQualificationPackageFixture ExtensionSdkTest() => new(currentTest: true, sdk42: true);

    /// <summary>Copies genuine declaration/policy bytes and creates explicitly synthetic owned entry prerequisites.</summary>
    /// <param name="currentTest">Whether to copy actual current provenance instead of the authenticated historical public bytes.</param>
    private PlatformQualificationPackageFixture(bool currentTest, bool sdk42 = false)
    {
        Root = Path.Combine(Path.GetTempPath(), "platform-qualified-cohort-" + Guid.NewGuid().ToString("N"));
        string declarations = Path.Combine(Root, "src", "Hexalith.Platform.Composition", "Declarations");
        Directory.CreateDirectory(declarations);
        foreach (string file in new[] { "eventstore.json", "tenants.json", "memories.json", "local-profile.json" })
            File.Copy(Path.Combine(PlatformTestRepository.Root, "src", "Hexalith.Platform.Composition", "Declarations", file), Path.Combine(declarations, file));
        string provenancePath = Path.Combine(declarations, "provenance.json");
        if (currentTest)
            File.Copy(Path.Combine(PlatformTestRepository.Root, "src", "Hexalith.Platform.Composition", "Declarations", "provenance.json"), provenancePath);
        else
        {
            using Stream historical = typeof(PlatformQualificationPackageFixture).Assembly.GetManifestResourceStream("Hexalith.Platform.Composition.Tests.Fixtures.public-702-provenance.json")
                ?? throw new InvalidDataException("The authenticated historical public provenance fixture is missing.");
            using MemoryStream buffer = new();
            historical.CopyTo(buffer);
            byte[] bytes = buffer.ToArray();
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != "3e9b04109c5ddb34a0ac6801f5cdb36e1cbc102c65bfccb54b77c4d408091a61")
                throw new InvalidDataException("The historical public provenance fixture differs from its authenticated Git body.");
            File.WriteAllBytes(provenancePath, bytes);
        }
        if (sdk42)
        {
            JsonNode sdkProvenance = JsonNode.Parse(File.ReadAllText(provenancePath))!;
            sdkProvenance["eventstore"]!["version"] = "4.2.0";
            sdkProvenance["eventstore"]!["commit"] = "aa16227bd07f0564a3026d619a5abd973e02656f";
            File.WriteAllText(provenancePath, sdkProvenance.ToJsonString());
        }
        string components = Path.Combine(PlatformTestRepository.Root, "DaprComponents", "reference");
        foreach (string file in Directory.GetFiles(components, "*", SearchOption.AllDirectories))
        {
            string destination = Path.Combine(Root, "DaprComponents", "reference", Path.GetRelativePath(components, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
        string catalog = Path.Combine(Root, "references", "Hexalith.Builds", "Props");
        Directory.CreateDirectory(catalog);
        string memoriesVersion = JsonNode.Parse(File.ReadAllText(Path.Combine(declarations, "provenance.json")))!["memories"]!["version"]!.GetValue<string>();
        JsonNode provenance = JsonNode.Parse(File.ReadAllText(Path.Combine(declarations, "provenance.json")))!;
        string sdkVersion = provenance["eventstore"]!["version"]!.GetValue<string>();
        string tenantsVersion = provenance["tenants"]!["version"]!.GetValue<string>();
        // This explicitly synthetic component catalog must agree with its copied genuine snapshot.
        File.WriteAllText(Path.Combine(catalog, "Directory.Packages.props"), "<Project><PropertyGroup><HexalithEventStoreVersion>" + sdkVersion + "</HexalithEventStoreVersion><HexalithTenantsVersion>" + tenantsVersion + "</HexalithTenantsVersion><HexalithMemoriesVersion>" + memoriesVersion + "</HexalithMemoriesVersion></PropertyGroup></Project>");
        string manifest = Path.Combine(Root, "module", "hexalith.module-manifest.v2.json");
        Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
        File.Copy(Path.Combine(declarations, "eventstore.json"), manifest);
        PlatformOwnerFixture.MaterializeDeclaredFiles(Root, JsonNode.Parse(File.ReadAllText(manifest))!);
        string seed = Path.Combine(Root, "seed.csproj");
        File.WriteAllText(seed, "<Project />");
        string source = new('a', 40);
        SourceMappingEntry[] entries = [new("Hexalith.EventStore", "source", Root), new("Hexalith.Tenants", "package", null), new("Hexalith.Memories", "package", null)];
        SourceMapping mapping = new(WorkspaceMode.Package, Root, "Hexalith.EventStore", entries, false)
        {
            PlatformIdentity = new PlatformIdentity(source, source, source, Root, "synthetic-fixture", new string('b', 40), "[4.43.6,5.0.0-0)", "identity.json", false),
        };
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string entry = currentTest ? "tests/Hexalith.EventStore.Platform.IntegrationTests/Hexalith.EventStore.Platform.IntegrationTests.csproj" : "owned.csproj";
        ModuleTestSelection selected = new("eventstore", "platform-package", currentTest ? "mtp" : "dotnet-test", entry,
            currentTest ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifest))) : "manifest",
            currentTest ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root, entry)))) : "entry",
            120, currentTest ? 90 : 30, now, now.AddSeconds(120), now.AddSeconds(120), "package");
        CompositionRunPlan plan = CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), Path.Combine(Root, "run"), Path.Combine(Root, "dapr"),
            new CompositionRunPorts(20001, 20002, 20003, 20004, 20005, 20006, 20007, 20008, 20009, 20010, 20011, 20012, 20013, [20014], 20015, 20016, 20017, [new(20018, 20019, 20020, 20021)]),
            [new CompositionRunModule("eventstore", "eventstore", "eventstore", seed)], []) with
        {
            SourceMapping = mapping, PlatformActiveModule = "eventstore", PlatformDeclarationPaths = [manifest],
            PlatformDeclarationHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(manifest))),
            CompositionContentIdentity = "sha256:" + new string('c', 64), QualificationOwners = ["tenants"], SelectedTest = selected, SelectedTests = [selected],
        };
        string key = CompositionSigningKey.Create();
        Plan = CompositionQualificationOwners.Bind(plan, key);
        Options = new PlatformCompositionOptions
        {
            PlatformRoot = Root, Mapping = mapping, ActiveModule = "eventstore", DeclarationPaths = [manifest], QualificationOwners = ["tenants"],
            EventStoreSeedProject = seed, RunPlan = Plan, QualificationOwnerProof = PlatformQualificationOwnerProof.Create(Plan, key),
            ReleaseCommits = new Dictionary<string, string>(Hexalith.Builds.Tooling.Runtime.CompositionDeclarationOptions.DefaultReleaseCommits)
                { ["eventstore/4.2.0"] = "aa16227bd07f0564a3026d619a5abd973e02656f" },
            ReleaseSnapshotHashes = new Dictionary<string, string>(Hexalith.Builds.Tooling.Runtime.CompositionDeclarationOptions.DefaultReleaseSnapshotHashes)
                { ["eventstore/4.2.0"] = "21eda6da824d5ae6135ff342dd8a5aae950b02420319b953a836dddeb00ce495" },
        };
    }

    /// <summary>Gets the isolated fixture root.</summary>
    internal string Root { get; }
    /// <summary>Gets the synthetic bound plan used only for component admission controls.</summary>
    internal CompositionRunPlan Plan { get; }
    /// <summary>Gets the component options with genuine released declaration bytes and no runtime acceptance.</summary>
    internal PlatformCompositionOptions Options { get; }

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(Root, true);
}
