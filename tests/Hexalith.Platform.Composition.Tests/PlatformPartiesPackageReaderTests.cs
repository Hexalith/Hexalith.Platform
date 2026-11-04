namespace Hexalith.Platform.Composition.Tests;

using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.Platform.CatalogArtifacts;
using Shouldly;

/// <summary>Runs the production-generated full reader only with the genuinely coherent owner fixture; no conditional positive or legacy mapping fallback.</summary>
/// <param name="diagnostics">Retains the actual generated-reader guard diagnostics.</param>
public sealed class PlatformPartiesPackageReaderTests(ITestOutputHelper diagnostics)
{
    private static readonly string[] _publicNames = ["Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults", "Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService",
        "Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server", "Hexalith.Memories.Contracts", "Hexalith.Memories.EventStore", "Hexalith.Memories.EventStoreExtensions",
        "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions", "Hexalith.McpCli.Abstractions"];

    /// <summary>The real generated reader retains all twelve measured public bodies, exact mapping and complete declaration/factory-checked canonical catalog.</summary>
    [Fact]
    public async Task CoherentGeneratedFullReaderUsesActualPublicBodiesAndOwnerPoliciesAsync()
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync();
        fixture.AssertPositiveEntry();
        PlatformRoutingCatalog catalog = fixture.Generate();
        AssertFullReader(fixture, catalog);
    }

    /// <summary>Each owner/cohort/body mutation follows a real successful full reader and preserves the previous catalog without publishing new output.</summary>
    /// <param name="fault">The altered private child input.</param>
    [Theory]
    [InlineData("missing-owner")]
    [InlineData("foreign-owner")]
    [InlineData("legacy-cohort")]
    [InlineData("unknown-cohort")]
    [InlineData("foreign-body")]
    public async Task ChangedFullReaderAuthorityRefusesBeforeOutputAsync(string fault)
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync();
        fixture.AssertPositiveEntry();
        PlatformRoutingCatalog catalog = fixture.Generate();
        AssertFullReader(fixture, catalog);
        byte[] previous = File.ReadAllBytes(catalog.CatalogPath);
        string failed = Path.Combine(fixture.Workspace, "refused-output");
        Directory.CreateDirectory(failed);
        JsonNode request = JsonNode.Parse(File.ReadAllBytes(Path.Combine(fixture.Reader, "request.json")))!;
        request["outputDirectory"] = failed;
        string output = Path.Combine(fixture.Reader, "output");
        switch (fault)
        {
            case "missing-owner": request["owners"]!.AsArray().RemoveAt(0); break;
            case "foreign-owner": request["owners"]![0]!["assembly"] = "Hexalith.Foreign.Contracts"; break;
            case "legacy-cohort": request["qualificationCohort"] = PublishedCatalogArtifactCohort.Name; break;
            case "unknown-cohort": request["qualificationCohort"] = "unknown"; break;
            case "foreign-body":
                string copy = Path.Combine(fixture.Workspace, "tampered-reader");
                foreach (string file in Directory.GetFiles(output, "*", SearchOption.AllDirectories))
                {
                    string destination = Path.Combine(copy, Path.GetRelativePath(output, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(file, destination);
                }
                output = copy;
                // Alter only a private child copy. Accepted outputs, signed archives and package caches are untouched.
                using (FileStream body = new(Path.Combine(copy, "Hexalith.Memories.Contracts.dll"), FileMode.Append, FileAccess.Write)) body.WriteByte(0);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        string path = Path.Combine(fixture.Reader, "refused-request.json");
        File.WriteAllText(path, request.ToJsonString());
        var result = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet", [Path.Combine(output, "Hexalith.Platform.CatalogBuilder.dll"), path], fixture.Reader, null),
            TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        diagnostics.WriteLine(result.Output + result.ErrorOutput);
        result.ExitCode.ShouldNotBe(0);
        result.TimedOut.ShouldBeFalse();
        (result.Output + result.ErrorOutput).ShouldContain(fault == "foreign-body" ? "exact signed public artifact bytes" : "not qualified");
        Directory.GetFiles(failed).ShouldBeEmpty();
        File.ReadAllBytes(catalog.CatalogPath).ShouldBe(previous);
    }

    /// <summary>Effective owner versions and restored package assets remain independently bound to the real pinned full catalog.</summary>
    /// <param name="fault">The changed private version or asset target.</param>
    [Theory]
    [InlineData("version")]
    [InlineData("assets")]
    public async Task FullReaderRejectsChangedOwnerVersionOrAssetsAsync(string fault)
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync();
        fixture.AssertPositiveEntry();
        PlatformRoutingCatalog catalog = fixture.Generate();
        AssertFullReader(fixture, catalog);
        byte[] previous = File.ReadAllBytes(catalog.CatalogPath);
        using JsonDocument versions = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Workspace, "source-mapping", "catalog-versions.json")));
        diagnostics.WriteLine(versions.RootElement.GetProperty("ProjectVersions").GetRawText());
        versions.RootElement.GetProperty("ProjectVersions").GetProperty("Hexalith.Platform.CatalogBuilder")
            .GetProperty("Hexalith.Memories.Contracts").GetString().ShouldBe("3.0.23");
        string path = Path.Combine(fixture.Reader, "Hexalith.Platform.CatalogBuilder.csproj");
        XDocument project = XDocument.Load(path);
        string target;
        if (fault == "version")
        {
            project.Root!.Add(new XElement("ItemGroup", new XElement("PackageVersion", new XAttribute("Update", "Hexalith.Memories.Contracts"), new XAttribute("Version", "3.0.18"))));
            target = "ValidatePrivateReaderCatalogBeforeRestore";
        }
        else
        {
            string assetsPath = Path.Combine(fixture.Reader, "obj", "project.assets.json");
            JsonNode assets = JsonNode.Parse(File.ReadAllBytes(assetsPath))!;
            JsonObject packages = assets["targets"]!.AsObject().Single().Value!.AsObject();
            JsonNode changed = packages["Hexalith.Memories.Contracts/3.0.23"]!.DeepClone();
            packages.Remove("Hexalith.Memories.Contracts/3.0.23");
            packages["Hexalith.Memories.Contracts/3.0.18"] = changed;
            File.WriteAllText(assetsPath, assets.ToJsonString());
            project.Root!.Elements("Target").Single(element => (string?)element.Attribute("Name") == "ValidatePrivateReaderCatalogAfterRestore").Attribute("DependsOnTargets")!.Remove();
            target = "ValidatePrivateReaderCatalogAfterRestore";
        }
        project.Save(path);
        var result = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet",
            ["msbuild", path, "-target:" + target, "-nologo", "-m:1", "-nodeReuse:false", "-p:AlternateCommonProps=" + Path.Combine(fixture.Reader, "reader-common.props"),
             "-p:CentralPackageVersionsFileImported=true", "-p:ImportDirectoryPackagesProps=false", "-p:UseHexalithProjectReferences=false", "-p:UseNuGetDeps=true",
             "-p:PlatformCatalogBindings=" + Path.Combine(fixture.Reader, "bindings.props")], fixture.Options.PlatformRoot, null), TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        diagnostics.WriteLine(result.Output + result.ErrorOutput);
        result.ExitCode.ShouldNotBe(0);
        (result.Output + result.ErrorOutput).ShouldContain("requires 3.0.23");
        File.ReadAllBytes(catalog.CatalogPath).ShouldBe(previous);
    }

    /// <summary>The independent full reader does not weaken the ordinary active-Parties source/package duplicate refusal.</summary>
    [Fact]
    public async Task OrdinaryConsumerRetainsActivePartiesSourceGuardAsync()
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync();
        fixture.AssertPositiveEntry();
        string path = Path.Combine(fixture.Workspace, "ordinary.csproj");
        File.WriteAllText(path, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><Import Project=\""
            + Path.Combine(fixture.Options.PlatformRoot, "Directory.Build.props") + "\"/><Import Project=\"" + Path.Combine(fixture.Options.PlatformRoot, "Directory.Packages.props")
            + "\"/><ItemGroup><PackageReference Include=\"Hexalith.Parties.Contracts\"/></ItemGroup></Project>");
        var result = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet", ["build", path, "-c", "Release", "-m:1", "-nodeReuse:false", "--disable-build-servers", "-p:CentralPackageVersionsFileImported=true", "--nologo"],
            fixture.Options.PlatformRoot, SourceMappingMaterializer.Environment(fixture.Options.Mapping, fixture.Workspace)), TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        diagnostics.WriteLine(result.Output + result.ErrorOutput);
        result.ExitCode.ShouldNotBe(0);
        (result.Output + result.ErrorOutput).ShouldContain("HXW005");
        Directory.GetFiles(fixture.Workspace, "catalog.json", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    private static void AssertFullReader(PlatformPartiesPackageFixture fixture, PlatformRoutingCatalog catalog)
    {
        using JsonDocument generated = JsonDocument.Parse(File.ReadAllBytes(catalog.CatalogPath));
        generated.RootElement.GetProperty("routes").GetArrayLength().ShouldBe(70);
        generated.RootElement.GetProperty("operations").GetArrayLength().ShouldBe(70);
        generated.RootElement.GetProperty("idempotency").GetArrayLength().ShouldBe(46);
        var declarations = generated.RootElement.GetProperty("declarations").EnumerateArray().ToArray();
        declarations.Count(declaration => declaration.GetProperty("kind").GetString() == "command").ShouldBe(46);
        declarations.Count(declaration => declaration.GetProperty("kind").GetString() == "query").ShouldBe(20);
        declarations.Count(declaration => declaration.GetProperty("kind").GetString() == "projection").ShouldBe(4);
        using JsonDocument request = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Reader, "request.json")));
        request.RootElement.GetProperty("qualificationCohort").GetString().ShouldBe(PublishedCatalogArtifactCohort.FullPartiesName);
        request.RootElement.GetProperty("sourceMappingHash").GetString().ShouldBe(fixture.Options.Mapping.ContentHash);
        SourceMapping.LoadVerified(Path.Combine(fixture.Workspace, "source-mapping", "mapping.json"), fixture.Options.Mapping.ContentHash).ContentHash.ShouldBe(fixture.Options.Mapping.ContentHash);
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.Directory, "receipt.json")));
        receipt.RootElement.GetProperty("sourceMappingHash").GetString().ShouldBe(fixture.Options.Mapping.ContentHash);
        var context = new AssemblyLoadContext("full-reader-public-bodies-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            foreach (string name in _publicNames)
            {
                string body = Path.Combine(fixture.Reader, "output", name + ".dll");
                PublishedCatalogArtifactCohort.Verify(context.LoadFromAssemblyPath(body), PublishedCatalogArtifactCohort.FullPartiesName).ShouldBe(body);
            }
        }
        finally { context.Unload(); }
        JsonNode assets = JsonNode.Parse(File.ReadAllBytes(Path.Combine(fixture.Reader, "obj", "project.assets.json")))!;
        foreach (string name in _publicNames)
        {
            string version = name.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal) ? "4.0.4" : name.StartsWith("Hexalith.Tenants.", StringComparison.Ordinal) ? "7.0.0"
                : name.StartsWith("Hexalith.Memories.", StringComparison.Ordinal) ? "3.0.23" : name.StartsWith("Hexalith.Parties.", StringComparison.Ordinal) ? "3.0.0" : "2.0.0";
            assets["libraries"]![name + "/" + version]!["type"]!.GetValue<string>().ShouldBe("package");
        }
    }
}
