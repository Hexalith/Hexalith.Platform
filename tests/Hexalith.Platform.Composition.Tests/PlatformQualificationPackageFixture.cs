namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

/// <summary>Uses genuine published declaration bytes with synthetic owned entry files; confers no package runtime acceptance.</summary>
internal sealed class PlatformQualificationPackageFixture : IDisposable
{
    /// <summary>Selects the authenticated historical public SDK 4.1.0/Tenants 7.0.2 inputs for existing controls.</summary>
    internal static PlatformQualificationPackageFixture HistoricalPublic() => new(currentTest: false);

    /// <summary>Selects genuine candidate package provenance for private component controls; grants no public or runtime acceptance.</summary>
    internal static PlatformQualificationPackageFixture CurrentTest() => new(currentTest: true);

    /// <summary>Selects the independently authenticated loaded SDK for generic private component controls; grants no runtime acceptance.</summary>
    internal static PlatformQualificationPackageFixture SelectedSdkTest() => CurrentTest();

    /// <summary>Copies genuine declaration/policy bytes and creates explicitly synthetic owned entry prerequisites.</summary>
    /// <param name="currentTest">Whether to copy genuine candidate package provenance instead of the authenticated historical public702 bytes.</param>
    private PlatformQualificationPackageFixture(bool currentTest)
    {
        Root = Path.Combine(Path.GetTempPath(), "platform-qualified-cohort-" + Guid.NewGuid().ToString("N"));
        string declarations = Path.Combine(Root, "src", "Hexalith.Platform.Composition", "Declarations");
        Directory.CreateDirectory(declarations);
        foreach (string file in new[] { "eventstore.json", "tenants.json", "memories.json", "local-profile.json" })
            File.Copy(Path.Combine(PlatformTestRepository.Root, "src", "Hexalith.Platform.Composition", "Declarations", file), Path.Combine(declarations, file));
        // The candidate's generated owner package lock is the sole package admission authority for this synthetic root.
        Directory.CreateDirectory(Path.Combine(Root, "eng"));
        File.Copy(Path.Combine(PlatformTestRepository.Root, "eng", "owner-packages.lock.json"), Path.Combine(Root, "eng", "owner-packages.lock.json"));
        string provenancePath = Path.Combine(declarations, "provenance.json");
        if (currentTest)
        {
            // Genuine candidate package provenance; local entry files below confer no publication authority.
            string sourceDeclarations = Path.Combine(PlatformTestRepository.Root, "src", "Hexalith.Platform.Composition", "Declarations");
            File.Copy(Path.Combine(sourceDeclarations, "provenance.json"), provenancePath);
            PlatformOwnerFixture.CopySyntheticTenantExecutable(sourceDeclarations, Root, declarations);
        }
        else
        {
            const string fixture = "public-702-provenance.json";
            using Stream historical = typeof(PlatformQualificationPackageFixture).Assembly.GetManifestResourceStream("Hexalith.Platform.Composition.Tests.Fixtures." + fixture)
                ?? throw new InvalidDataException("The authenticated retained provenance fixture is missing.");
            using MemoryStream buffer = new();
            historical.CopyTo(buffer);
            byte[] bytes = buffer.ToArray();
            const string expected = "3e9b04109c5ddb34a0ac6801f5cdb36e1cbc102c65bfccb54b77c4d408091a61";
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != expected)
                throw new InvalidDataException("The historical public provenance fixture differs from its authenticated Git body.");
            File.WriteAllBytes(provenancePath, bytes);
            using Stream eventStore = typeof(PlatformQualificationPackageFixture).Assembly.GetManifestResourceStream(
                "Hexalith.Platform.Composition.Tests.Fixtures.public-702-eventstore.json")
                ?? throw new InvalidDataException("The retained EventStore declaration is missing.");
            using FileStream snapshot = File.Create(Path.Combine(declarations, "eventstore.json"));
            eventStore.CopyTo(snapshot);
            string historicalTenants = Path.Combine(PlatformTestRepository.ReferenceRoot(PlatformTestRepository.Root, "Hexalith.Builds"),
                "test", "fixtures", "module", "platform", "tenants-7.0.2.json");
            string tenantsHash = CompositionDeclarationOptions.DefaultReleaseSnapshotHashes["tenants/7.0.2"];
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(historicalTenants))).ShouldBe(tenantsHash);
            File.Copy(historicalTenants, Path.Combine(declarations, "tenants.json"), overwrite: true);
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
        File.WriteAllText(Path.Combine(catalog, "Directory.Packages.props"), "<Project><PropertyGroup><HexalithEventStoreVersion>" + sdkVersion + "</HexalithEventStoreVersion><HexalithTenantsVersion>" + tenantsVersion + "</HexalithTenantsVersion><HexalithMemoriesVersion>" + memoriesVersion + "</HexalithMemoriesVersion></PropertyGroup><ItemGroup><PackageVersion Include=\"Hexalith.McpCli.Abstractions\" Version=\"" + (currentTest ? PlatformTestPins.CatalogVersion("McpCli") : "2.0.0") + "\" /></ItemGroup></Project>");
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
        var suite = JsonNode.Parse(File.ReadAllText(manifest))!["modules"]![0]!["tests"]!.AsArray()
            .Single(test => test!["name"]!.GetValue<string>() == "platform-package")!;
        string entry = suite["entry"]!.GetValue<string>();
        int duration = suite["executionTimeoutSeconds"]!.GetValue<int>();
        ModuleTestSelection selected = new("eventstore", "platform-package", suite["executor"]!.GetValue<string>(), entry,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifest))),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root, entry)))),
            duration, suite["stopTimeoutSeconds"]!.GetValue<int>(), now, now.AddSeconds(duration), now.AddSeconds(duration), "package");
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
        };
    }

    /// <summary>Gets the isolated fixture root.</summary>
    internal string Root { get; }
    /// <summary>Gets the synthetic bound plan used only for component admission controls.</summary>
    internal CompositionRunPlan Plan { get; }
    /// <summary>Gets the component options with genuine released declaration bytes and no runtime acceptance.</summary>
    internal PlatformCompositionOptions Options { get; }

    /// <summary>Changes one package family in the private fixture's catalog.</summary>
    /// <param name="family">The catalog family to mutate.</param>
    /// <param name="version">The alternative version under test.</param>
    internal void ChangeCatalogVersion(string family, string version)
    {
        string path = Path.Combine(Root, "references", "Hexalith.Builds", "Props", "Directory.Packages.props");
        XDocument catalog = XDocument.Load(path);
        XElement selection = catalog.Root!.Elements("PropertyGroup").Elements("Hexalith" + family + "Version").Single();
        if (selection.Value == version)
            throw new InvalidOperationException("The test mutation must change the selected " + family + " version.");
        selection.Value = version;
        catalog.Save(path);
    }

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(Root, true);
}
