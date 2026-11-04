namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;

/// <summary>Uses genuine published declaration bytes with synthetic owned entry files; confers no package runtime acceptance.</summary>
internal sealed class PlatformQualificationPackageFixture : IDisposable
{
    /// <summary>Copies genuine declaration/policy bytes and creates explicitly synthetic owned entry prerequisites.</summary>
    internal PlatformQualificationPackageFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "platform-qualified-cohort-" + Guid.NewGuid().ToString("N"));
        string declarations = Path.Combine(Root, "src", "Hexalith.Platform.Composition", "Declarations");
        Directory.CreateDirectory(declarations);
        foreach (string file in new[] { "eventstore.json", "tenants.json", "memories.json", "provenance.json", "local-profile.json" })
            File.Copy(Path.Combine(PlatformTestRepository.Root, "src", "Hexalith.Platform.Composition", "Declarations", file), Path.Combine(declarations, file));
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
        // This explicitly synthetic component catalog must agree with its copied genuine snapshot.
        File.WriteAllText(Path.Combine(catalog, "Directory.Packages.props"), "<Project><PropertyGroup><HexalithEventStoreVersion>4.0.4</HexalithEventStoreVersion><HexalithTenantsVersion>7.0.0</HexalithTenantsVersion><HexalithMemoriesVersion>" + memoriesVersion + "</HexalithMemoriesVersion></PropertyGroup></Project>");
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
        ModuleTestSelection selected = new("eventstore", "platform-package", "dotnet-test", "owned.csproj", "manifest", "entry", 120, 30, now, now.AddSeconds(120), now.AddSeconds(120), "package");
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

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(Root, true);
}
