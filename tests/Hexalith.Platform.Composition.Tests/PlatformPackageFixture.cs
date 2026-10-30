namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Workspace;

/// <summary>Owns internally consistent synthetic release metadata; confers no published release or live acceptance.</summary>
internal sealed class PlatformPackageFixture : IDisposable
{
    /// <summary>Creates an isolated catalog and snapshots without changing production release provenance.</summary>
    public PlatformPackageFixture(string platformRoot)
    {
        Root = Path.Combine(Path.GetTempPath(), "platform-synthetic-package-" + Guid.NewGuid().ToString("N"));
        string declarations = Path.Combine(Root, "src", "Hexalith.Platform.Composition", "Declarations");
        string catalog = Path.Combine(Root, "references", "Hexalith.Builds", "Props");
        Directory.CreateDirectory(declarations);
        Directory.CreateDirectory(catalog);
        File.Copy(Path.Combine(platformRoot, "src", "Hexalith.Platform.Composition", "Declarations", "local-profile.json"), Path.Combine(declarations, "local-profile.json"));
        foreach (string file in new[] { "Directory.Build.props", "Directory.Packages.props" })
            File.Copy(Path.Combine(platformRoot, file), Path.Combine(Root, file));
        string reader = Path.Combine(Root, "src", "Hexalith.Platform.CatalogBuilder");
        Directory.CreateDirectory(reader);
        foreach (string file in new[] { "Hexalith.Platform.CatalogBuilder.csproj", "Program.cs" })
            File.Copy(Path.Combine(platformRoot, "src", "Hexalith.Platform.CatalogBuilder", file), Path.Combine(reader, file));
        Versions = new Dictionary<string, string> { ["eventstore"] = "3.120.0", ["tenants"] = "5.8.0", ["memories"] = "2.27.1" };
        File.WriteAllText(Path.Combine(catalog, "Directory.Packages.props"), "<Project><PropertyGroup><HexalithEventStoreVersion>3.120.0</HexalithEventStoreVersion><HexalithTenantsVersion>5.8.0</HexalithTenantsVersion><HexalithMemoriesVersion>2.27.1</HexalithMemoriesVersion></PropertyGroup></Project>");
        Dictionary<string, string> commits = [];
        Dictionary<string, string> hashes = [];
        JsonObject provenance = [];
        foreach (string owner in Versions.Keys)
        {
            string snapshot = Path.Combine(declarations, owner + ".json");
            File.Copy(Path.Combine(platformRoot, "references", PlatformServerBindings.Repository(owner), "module", "hexalith.module-manifest.v2.json"), snapshot);
            PlatformOwnerFixture.MaterializeDeclaredFiles(Root, JsonNode.Parse(File.ReadAllText(snapshot))!);
            string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(snapshot)));
            string commit = new(owner[0] == 'e' ? 'a' : owner[0] == 't' ? 'b' : 'c', 40);
            commits[owner + "/" + Versions[owner]] = commit;
            hashes[owner + "/" + Versions[owner]] = hash;
            provenance[owner] = new JsonObject
            {
                ["repository"] = PlatformServerBindings.Repository(owner), ["version"] = Versions[owner],
                ["commit"] = commit, ["source"] = "module/hexalith.module-manifest.v2.json", ["sha256"] = hash,
                ["qualification"] = "synthetic composition fixture only",
            };
        }
        File.WriteAllText(Path.Combine(declarations, "provenance.json"), provenance.ToJsonString());
        // The graph uses synthetic release/image identities, while its catalog is built from
        // explicitly mapped actual owner source. This fixture confers no package acceptance.
        SourceMappingEntry[] entries = Directory.GetDirectories(Path.Combine(platformRoot, "references"), "Hexalith.*")
            .Select(path => Path.GetFileName(path) is "Hexalith.EventStore" or "Hexalith.Tenants" or "Hexalith.Memories" or "Hexalith.Builds"
                ? new SourceMappingEntry(Path.GetFileName(path), "source", path)
                : new SourceMappingEntry(Path.GetFileName(path), "package", null)).ToArray();
        Options = new PlatformCompositionOptions
        {
            PlatformRoot = Root,
            Mapping = new SourceMapping(WorkspaceMode.Package, Root, "Hexalith.Platform", Versions.Keys.Select(owner => new SourceMappingEntry(PlatformServerBindings.Repository(owner), "package", null)).ToArray(), false),
            ComponentsDirectory = Path.Combine(platformRoot, "DaprComponents", "reference"),
            EventStoreSeedProject = Path.Combine(platformRoot, "references", "Hexalith.Builds", "src", "hosts", "Hexalith.Builds.Module.EventStoreHost", "Hexalith.Builds.Module.EventStoreHost.csproj"),
            ReleaseCommits = commits, ReleaseSnapshotHashes = hashes,
        };
        SourceCatalogOptions = Options with { Mapping = new SourceMapping(WorkspaceMode.Package, Root, "Hexalith.Platform", entries, true) };
    }

    /// <summary>Gets the temporary Platform root.</summary>
    public string Root { get; }

    /// <summary>Gets the synthetic catalog versions.</summary>
    public IReadOnlyDictionary<string, string> Versions { get; }

    /// <summary>Gets the options bound to this fixture's bytes and provenance.</summary>
    public PlatformCompositionOptions Options { get; }

    /// <summary>Gets graph-only package image bindings with a real source-built catalog; confers no package acceptance.</summary>
    public PlatformCompositionOptions SourceCatalogOptions { get; }

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(Root, true);
}
