namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.Platform.CatalogArtifacts;
using Shouldly;

/// <summary>Runs the generic reader refusal matrix against one genuine current private reader; no image admission is inferred.</summary>
internal static class PlatformReaderMutationControls
{
    private static readonly string[] _publicNames = ["Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults", "Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService",
        "Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server", "Hexalith.Memories.Contracts", "Hexalith.Memories.EventStore", "Hexalith.Memories.EventStoreExtensions",
        "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions", "Hexalith.McpCli.Abstractions"];

    internal static async Task AssertAsync(PlatformPartiesPackageFixture fixture, PlatformRoutingCatalog catalog)
    {
        AssertFullReader(fixture, catalog);
        AssertPreparationPolicy(fixture);
        foreach (string fault in new[] { "mapping", "snapshot", "catalog", "missing-snapshot" })
            AssertRunAuthorityRefusal(fixture, catalog, fault);
        foreach (string fault in new[] { "duplicate-owner", "foreign-owner", "missing-lock", "stale-lock", "tampered-lock", "foreign-body" })
            await AssertRequestRefusalAsync(fixture, catalog, fault);
        string project = Path.Combine(fixture.Reader, "Hexalith.Platform.CatalogBuilder.csproj");
        string assets = Path.Combine(fixture.Reader, "obj", "project.assets.json");
        byte[] originalProject = File.ReadAllBytes(project);
        byte[] originalAssets = File.ReadAllBytes(assets);
        foreach (string fault in new[] { "version", "assets" })
        {
            try { await AssertVersionOrAssetsRefusalAsync(fixture, catalog, fault); }
            finally
            {
                File.WriteAllBytes(project, originalProject);
                File.WriteAllBytes(assets, originalAssets);
            }
        }
        string mapping = Path.Combine(fixture.Workspace, "source-mapping");
        var inputs = Directory.GetFiles(mapping, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
        try { await AssertOrdinarySourceGuardAsync(fixture, catalog); }
        finally { foreach (var input in inputs) File.WriteAllBytes(input.Key, input.Value); }
        fixture.Options.CurrentOwnerProof!.Matches(fixture.Options).ShouldBeTrue();
        TestContext.Current.TestOutputHelper!.WriteLine("Current reader matrix: complete baseline; transitive pinning preserved; 4 pre-restore authority refusals; 6 request/lock/body refusals; 2 version/assets refusals; 1 source/package duplicate refusal.");
    }

    private static void AssertPreparationPolicy(PlatformPartiesPackageFixture fixture)
    {
        string build = Path.Combine(fixture.Workspace, "reader-preparation-policy");
        Directory.CreateDirectory(build);
        XDocument reader = XDocument.Load(Path.Combine(fixture.Options.PlatformRoot, "src", "Hexalith.Platform.CatalogBuilder", "Hexalith.Platform.CatalogBuilder.csproj"));
        IReadOnlyList<string> arguments = PlatformPackageCatalogReader.Prepare(reader, fixture.Options, fixture.Workspace, build, DateTimeOffset.UtcNow.AddMinutes(5));
        arguments.Where(argument => argument.StartsWith("-p:CentralPackageTransitivePinningEnabled=", StringComparison.Ordinal))
            .ShouldBe(["-p:CentralPackageTransitivePinningEnabled=true"]);
    }

    private static void AssertRunAuthorityRefusal(PlatformPartiesPackageFixture fixture, PlatformRoutingCatalog catalog, string fault)
    {
        string mapping = Path.Combine(fixture.Workspace, "source-mapping");
        var inputs = Directory.GetFiles(mapping, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
        byte[] previous = File.ReadAllBytes(catalog.CatalogPath);
        string build = Path.Combine(fixture.Workspace, "refused-authority-" + fault);
        Directory.CreateDirectory(build);
        try
        {
            if (fault == "mapping")
            {
                string file = Path.Combine(mapping, "mapping.json");
                JsonNode changed = JsonNode.Parse(File.ReadAllBytes(file))!;
                changed["ActiveModule"] = "Hexalith.Foreign";
                File.WriteAllText(file, changed.ToJsonString());
            }
            else if (fault == "catalog") File.AppendAllText(Path.Combine(mapping, "pinned-tree", "Props", "Directory.Packages.props"), "<!-- changed pinned bytes -->");
            else if (fault == "missing-snapshot") File.Delete(Path.Combine(mapping, "catalog-versions.json"));
            else
            {
                string file = Path.Combine(mapping, "catalog-versions.json");
                JsonNode changed = JsonNode.Parse(File.ReadAllBytes(file))!;
                changed["MappingHash"] = new string('0', 64);
                File.WriteAllText(file, changed.ToJsonString());
            }
            XDocument reader = XDocument.Load(Path.Combine(fixture.Options.PlatformRoot, "src", "Hexalith.Platform.CatalogBuilder", "Hexalith.Platform.CatalogBuilder.csproj"));
            var refusal = Should.Throw<InvalidDataException>(() => PlatformPackageCatalogReader.Prepare(reader, fixture.Options, fixture.Workspace, build, DateTimeOffset.UtcNow.AddMinutes(5)));
            if (fault == "mapping") refusal.Message.ShouldContain("differs from the command-resolved hash");
            else
            {
                refusal.Message.ShouldContain("pinned catalog validation failed");
                string log = File.ReadAllText(Path.Combine(build, "catalog-validation.log"));
                log.ShouldContain("HXW012");
                log.ShouldContain(fault == "missing-snapshot" ? "catalog-versions.json" : "snapshot differs from the command-resolved identity");
                if (fault == "missing-snapshot") log.ShouldContain("Could not find file");
            }
            TestContext.Current.TestOutputHelper!.WriteLine("authority-refusal=" + fault + " " + refusal.Message);
            Directory.Exists(Path.Combine(build, "obj")).ShouldBeFalse("Authority must refuse before package restore.");
            Directory.Exists(Path.Combine(build, "output")).ShouldBeFalse();
            Directory.GetFiles(build, "catalog.json", SearchOption.AllDirectories).ShouldBeEmpty();
            File.ReadAllBytes(catalog.CatalogPath).ShouldBe(previous);
        }
        finally { foreach (var input in inputs) File.WriteAllBytes(input.Key, input.Value); }
        fixture.Options.CurrentOwnerProof!.Matches(fixture.Options).ShouldBeTrue();
    }

    private static async Task AssertRequestRefusalAsync(PlatformPartiesPackageFixture fixture, PlatformRoutingCatalog catalog, string fault)
    {
        byte[] previous = File.ReadAllBytes(catalog.CatalogPath);
        string failed = Path.Combine(fixture.Workspace, "refused-output-" + fault);
        Directory.CreateDirectory(failed);
        JsonNode request = JsonNode.Parse(File.ReadAllBytes(Path.Combine(fixture.Reader, "request.json")))!;
        request["outputDirectory"] = failed;
        string output = Path.Combine(fixture.Reader, "output");
        switch (fault)
        {
            case "duplicate-owner": request["owners"]!.AsArray().Add(request["owners"]![0]!.DeepClone()); break;
            case "foreign-owner": request["owners"]![0]!["assembly"] = "Hexalith.Foreign.Contracts"; break;
            case "missing-lock": request["ownerPackageLock"] = Path.Combine(fixture.Workspace, "absent-owner-packages.lock.json"); break;
            case "stale-lock":
            case "tampered-lock":
                // A private changed copy of the lock; the committed lock is untouched.
                JsonNode changedLock = JsonNode.Parse(File.ReadAllBytes(request["ownerPackageLock"]!.GetValue<string>()))!;
                JsonNode entry = changedLock["packages"]!.AsArray().Single(item => item!["id"]!.GetValue<string>() == "Hexalith.Memories.Contracts")!;
                if (fault == "stale-lock") entry["version"] = "3.0.26";
                else entry["contentHash"] = Convert.ToBase64String(new byte[64]);
                string changedPath = Path.Combine(fixture.Workspace, fault + ".lock.json");
                File.WriteAllText(changedPath, changedLock.ToJsonString());
                request["ownerPackageLock"] = changedPath;
                break;
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
        TestContext.Current.TestOutputHelper!.WriteLine(result.Output + result.ErrorOutput);
        result.ExitCode.ShouldNotBe(0);
        result.TimedOut.ShouldBeFalse();
        (result.Output + result.ErrorOutput).ShouldContain(fault switch
        {
            "duplicate-owner" => "without duplicate owner or adapter assemblies",
            "foreign-owner" => "Hexalith.Foreign.Contracts",
            "missing-lock" => "is missing or unreadable",
            "stale-lock" => "names Hexalith.Memories.Contracts 3.0.26, but 3.0.27 is selected",
            "tampered-lock" => "owner package Hexalith.Memories.Contracts 3.0.27 archive has contentHash",
            _ => "loaded assembly 'Hexalith.Memories.Contracts' differs from its lock-verified package member",
        });
        Directory.GetFiles(failed).ShouldBeEmpty();
        File.ReadAllBytes(catalog.CatalogPath).ShouldBe(previous);
    }

    private static async Task AssertVersionOrAssetsRefusalAsync(PlatformPartiesPackageFixture fixture, PlatformRoutingCatalog catalog, string fault)
    {
        byte[] previous = File.ReadAllBytes(catalog.CatalogPath);
        using JsonDocument versions = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Workspace, "source-mapping", "catalog-versions.json")));
        XDocument bindings = XDocument.Load(Path.Combine(fixture.Reader, "bindings.props"));
        bindings.Descendants("PackageReference").Select(item => (string?)item.Attribute("Include"))
            .ShouldContain("Hexalith.Memories.EventStore", "Mutate an actual direct owner reference validated by the generated task.");
        versions.RootElement.GetProperty("ProjectVersions").GetProperty("Hexalith.Platform.CatalogBuilder")
            .GetProperty("Hexalith.Memories.EventStore").GetString().ShouldBe("3.0.27");
        string path = Path.Combine(fixture.Reader, "Hexalith.Platform.CatalogBuilder.csproj");
        XDocument project = XDocument.Load(path);
        string target;
        if (fault == "version")
        {
            project.Root!.Add(new XElement("ItemGroup", new XElement("PackageVersion", new XAttribute("Update", "Hexalith.Memories.EventStore"), new XAttribute("Version", "3.0.18"))));
            target = "ValidatePrivateReaderCatalogBeforeRestore";
        }
        else
        {
            string assetsPath = Path.Combine(fixture.Reader, "obj", "project.assets.json");
            JsonNode assets = JsonNode.Parse(File.ReadAllBytes(assetsPath))!;
            JsonObject packages = assets["targets"]!.AsObject().Single().Value!.AsObject();
            JsonNode changed = packages["Hexalith.Memories.EventStore/3.0.27"]!.DeepClone();
            packages.Remove("Hexalith.Memories.EventStore/3.0.27");
            packages["Hexalith.Memories.EventStore/3.0.18"] = changed;
            File.WriteAllText(assetsPath, assets.ToJsonString());
            project.Root!.Elements("Target").Single(element => (string?)element.Attribute("Name") == "ValidatePrivateReaderCatalogAfterRestore").Attribute("DependsOnTargets")!.Remove();
            target = "ValidatePrivateReaderCatalogAfterRestore";
        }
        project.Save(path);
        var result = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet",
            ["msbuild", path, "-target:" + target, "-nologo", "-m:1", "-nodeReuse:false", "-p:AlternateCommonProps=" + Path.Combine(fixture.Reader, "reader-common.props"),
             "-p:CentralPackageVersionsFileImported=true", "-p:ImportDirectoryPackagesProps=false", "-p:UseHexalithProjectReferences=false", "-p:UseNuGetDeps=true",
             "-p:PlatformCatalogBindings=" + Path.Combine(fixture.Reader, "bindings.props")], fixture.Options.PlatformRoot, null), TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        TestContext.Current.TestOutputHelper!.WriteLine(result.Output + result.ErrorOutput);
        result.ExitCode.ShouldNotBe(0);
        (result.Output + result.ErrorOutput).ShouldContain("requires 3.0.27");
        File.ReadAllBytes(catalog.CatalogPath).ShouldBe(previous);
    }

    private static async Task AssertOrdinarySourceGuardAsync(PlatformPartiesPackageFixture fixture, PlatformRoutingCatalog catalog)
    {
        byte[] previous = File.ReadAllBytes(catalog.CatalogPath);
        // The producer correctly reconciles an initial active-owner package to source.
        // A consumer that adds it back afterwards must still hit the real duplicate guard
        // before a restore of unrelated active-owner dependencies can mask that boundary.
        string path = Path.Combine(fixture.Workspace, "ordinary.csproj");
        File.WriteAllText(path, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><Import Project=\""
            + Path.Combine(fixture.Options.PlatformRoot, "Directory.Build.props") + "\"/><Import Project=\"" + Path.Combine(fixture.Options.PlatformRoot, "Directory.Packages.props")
            + "\"/><ItemGroup><PackageReference Include=\"Hexalith.Parties.Contracts\"/></ItemGroup>"
            + "<Target Name=\"ReintroduceOrdinaryPackage\" AfterTargets=\"HexalithReconcileSourceMapping\">"
            + "<ItemGroup><PackageReference Include=\"Hexalith.Parties.Contracts\"/></ItemGroup></Target></Project>");
        var result = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet", ["build", path, "-c", "Release", "-m:1", "-nodeReuse:false", "--disable-build-servers", "-p:CentralPackageVersionsFileImported=true", "--nologo"],
            fixture.Options.PlatformRoot, SourceMappingMaterializer.Environment(fixture.Options.Mapping, fixture.Workspace)), TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        TestContext.Current.TestOutputHelper!.WriteLine(result.Output + result.ErrorOutput);
        result.ExitCode.ShouldNotBe(0);
        result.TimedOut.ShouldBeFalse();
        (result.Output + result.ErrorOutput).ShouldContain("HXW005");
        (result.Output + result.ErrorOutput).ShouldContain("Source 'Hexalith.Parties'");
        (result.Output + result.ErrorOutput).ShouldContain("duplicates direct package 'Hexalith.Parties.Contracts/");
        File.ReadAllBytes(catalog.CatalogPath).ShouldBe(previous);
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
        TestContext.Current.TestOutputHelper!.WriteLine("full-reader catalog sha256=" + Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(catalog.CatalogPath)))
            + " root=" + catalog.RootDigest + " mapping=" + fixture.Options.Mapping.ContentHash + " public-bodies=12 routes=70 policies=46");
        using JsonDocument request = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Reader, "request.json")));
        request.RootElement.GetProperty("ownerPackageLock").GetString().ShouldBe(fixture.Options.CurrentOwnerProof!.OwnerPackages.Path);
        request.RootElement.GetProperty("sourceMappingHash").GetString().ShouldBe(fixture.Options.Mapping.ContentHash);
        SourceMapping.LoadVerified(Path.Combine(fixture.Workspace, "source-mapping", "mapping.json"), fixture.Options.Mapping.ContentHash).ContentHash.ShouldBe(fixture.Options.Mapping.ContentHash);
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.Directory, "receipt.json")));
        receipt.RootElement.GetProperty("sourceMappingHash").GetString().ShouldBe(fixture.Options.Mapping.ContentHash);
        // AR-12: every public reference body is recorded against its lock-verified archive member; run-built Parties bodies keep run-bound authority.
        var evidence = fixture.Options.CurrentOwnerProof!.OwnerPackages.VerifyReceipt(receipt.RootElement, PlatformCurrentOwnerProof.RunBuiltBodies)
            .ToDictionary(identity => identity.Assembly, StringComparer.Ordinal);
        foreach (string name in _publicNames)
        {
            string body = Path.Combine(fixture.Reader, "output", name + ".dll");
            string digest = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(body)));
            if (name.StartsWith("Hexalith.Parties.", StringComparison.Ordinal))
            {
                digest.ShouldBe(fixture.Options.CurrentOwnerProof!.Bodies[name]);
                evidence.ShouldNotContainKey(name);
            }
            else
            {
                evidence[name].LoadedPath.ShouldBe(body);
                evidence[name].ExpectedSha256.ShouldBe(digest);
                evidence[name].LoadedSha256.ShouldBe(digest);
            }
            TestContext.Current.TestOutputHelper!.WriteLine(name + " sha256=" + digest);
        }
        JsonNode assets = JsonNode.Parse(File.ReadAllBytes(Path.Combine(fixture.Reader, "obj", "project.assets.json")))!;
        foreach (string name in _publicNames)
        {
            string version = name.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal) ? "4.2.0" : name.StartsWith("Hexalith.Tenants.", StringComparison.Ordinal) ? "7.0.7"
                : name.StartsWith("Hexalith.Memories.", StringComparison.Ordinal) ? "3.0.27" : name.StartsWith("Hexalith.Parties.", StringComparison.Ordinal) ? "3.1.1" : "2.0.1";
            assets["libraries"]![name + "/" + version]!["type"]!.GetValue<string>().ShouldBe("package");
        }
    }
}
