namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;
using Xunit;

/// <summary>Checks the separate private purpose without granting a synthetic fixture runtime or public artifact authority.</summary>
public sealed class PlatformCurrentOwnerPurposeTests
{
    /// <summary>Synthetic process metadata proves redaction and bounded classification; it is not an observed preparation failure.</summary>
    [Fact]
    public void PreparationFailuresExposeOnlyFinitePhaseStatusAndDiagnosticCodes()
    {
        string sensitive = Guid.NewGuid().ToString("N");
        var result = new CompositionProcessResult(true, 42, "HXW012 " + sensitive + " NU1100 HXW012", false)
        { ErrorOutput = "CS0006 " + sensitive };
        string message = PlatformCurrentOwnerProof.PreparationFailure("runtime-build.log", result).Message;
        message.ShouldBe("Current owner Release preparation failed: phase=runtime-build; started=True; exit=42; timedOut=False; truncated=False; codes=CS0006,HXW012,NU1100.");
        message.ShouldNotContain(sensitive);
        result = new(false, -1, sensitive, true) { OutputTruncated = true, ErrorOutput = sensitive };
        message = PlatformCurrentOwnerProof.PreparationFailure(sensitive, result).Message;
        message.ShouldBe("Current owner Release preparation failed: phase=unknown; started=False; exit=-1; timedOut=True; truncated=True; codes=none.");
        message.ShouldNotContain(sensitive);
        result = new(true, 1, string.Join(' ', Enumerable.Range(0, 32).Select(value => "CS" + value.ToString("D4", System.Globalization.CultureInfo.InvariantCulture))), false);
        message = PlatformCurrentOwnerProof.PreparationFailure("Hexalith.Parties.Contracts-build.log", result).Message;
        message.ShouldBe("Current owner Release preparation failed: phase=contracts-build; started=True; exit=1; timedOut=False; truncated=False; codes=CS0000,CS0001,CS0002,CS0003,CS0004,CS0005,CS0006,CS0007.");
    }

    [Fact]
    public void CommittedUpdatedIntegrationInputsRequireFreshParentProof()
    {
        using PlatformPartiesProofFixture fixture = new();
        PlatformCompositionOptions options = Current(fixture);
        fixture.Options.PartiesRunProof!.Matches(options).ShouldBeFalse();
        PlatformPartiesRunProof current = PlatformPartiesRunProof.Create(options, fixture.Key);
        current.Matches(options).ShouldBeTrue();
        JsonSerializer.Serialize(options).ShouldNotContain("CurrentOwnerProof");
        JsonSerializer.Serialize(options).ShouldNotContain(fixture.Key);
    }

    [Theory]
    [InlineData("mixed")]
    [InlineData("dirty-source")]
    [InlineData("entry")]
    [InlineData("snapshot")]
    [InlineData("key")]
    public void CurrentPurposeRefusesChangedParentInputs(string change)
    {
        using PlatformPartiesProofFixture fixture = new();
        PlatformCompositionOptions options = Current(fixture);
        if (change == "mixed") options = options with { RunPlan = options.RunPlan! with { SelectedTests = [options.RunPlan.SelectedTest!, fixture.Options.RunPlan!.SelectedTest!] } };
        if (change == "dirty-source") File.AppendAllText(Path.Combine(options.Mapping.Root, "Native.cs"), "// changed");
        if (change == "entry") File.AppendAllText(Path.Combine(options.Mapping.Root, options.RunPlan!.SelectedTest!.Entry), "changed");
        if (change == "snapshot") File.AppendAllText(Path.Combine(options.RunPlan!.Workspace, "source-mapping", "catalog-versions.json"), " ");
        string key = change == "key" ? "not-a-key" : fixture.Key;
        if (change == "snapshot")
        {
            // The proof captures a legitimate snapshot once, then refuses even whitespace tampering.
            File.WriteAllText(Path.Combine(options.RunPlan!.Workspace, "source-mapping", "catalog-versions.json"), "{}");
            PlatformPartiesRunProof proof = PlatformPartiesRunProof.Create(options, key);
            File.AppendAllText(Path.Combine(options.RunPlan.Workspace, "source-mapping", "catalog-versions.json"), " ");
            proof.Matches(options).ShouldBeFalse();
            AssertSeedValidationRetainsSnapshot(fixture, options);
        }
        else Should.Throw<InvalidDataException>(() => PlatformPartiesRunProof.Create(options, key));
    }

    [Fact]
    public void OrdinarySourceDependencyFixtureCannotIssueMeasuredCurrentOwnerAuthority()
    {
        using PlatformPartiesProofFixture fixture = new();
        PlatformCompositionOptions options = Current(fixture);
        Should.Throw<InvalidDataException>(() => PlatformCurrentOwnerProof.Create(options, fixture.Key, developmentRun: true));
        Directory.Exists(Path.Combine(options.RunPlan!.Workspace, "current-owner")).ShouldBeFalse();
        // Component-only version admission: these local catalog bytes grant no artifact or runtime authority.
        string catalogRoot = Path.Combine(fixture.Root, "version-selection");
        string catalog = Path.Combine(catalogRoot, "references", "Hexalith.Builds", "Props", "Directory.Packages.props");
        Directory.CreateDirectory(Path.GetDirectoryName(catalog)!);
        PlatformCompositionOptions selection = options with { PlatformRoot = catalogRoot, RunPlan = null };
        var owners = PlatformPartiesPackageFixture.Lock;
        string Locked(string id) => owners.Entries.Single(entry => entry.Id == id).Version;
        string sdk = Locked("Hexalith.EventStore.Contracts"), tenants = Locked("Hexalith.Tenants.Contracts"), memories = Locked("Hexalith.Memories.Contracts");
        string Catalog(string eventStore, string tenant, string memory) => "<Project><PropertyGroup><HexalithEventStoreVersion>" + eventStore + "</HexalithEventStoreVersion><HexalithTenantsVersion>"
            + tenant + "</HexalithTenantsVersion><HexalithMemoriesVersion>" + memory + "</HexalithMemoriesVersion></PropertyGroup><ItemGroup><PackageVersion Include=\"Hexalith.McpCli.Abstractions\" Version=\""
            + Locked("Hexalith.McpCli.Abstractions") + "\" /></ItemGroup></Project>";
        void Admit() => PlatformPackageCatalogQualification.RequireLockedVersions(owners, selection, ["eventstore", "tenants", "memories"]);
        File.WriteAllText(catalog, Catalog(sdk, tenants, memories));
        Admit();
        PlatformPackageCatalogQualification.RequireLockedMcpCli(owners, selection, required: true);
        foreach (string refused in new[] { "3.0.23", "3.0.25", "3.0.270", memories + "+unqualified", "3.0.28" })
        {
            File.WriteAllText(catalog, Catalog(sdk, tenants, refused));
            Should.Throw<InvalidDataException>(Admit).Message.ShouldContain("but " + refused + " is selected");
        }
        foreach (string refused in new[] { "7.0.0", "7.0.5", "7.0.70", tenants + "+unqualified", "8.0.1" })
        {
            File.WriteAllText(catalog, Catalog(sdk, refused, memories));
            Should.Throw<InvalidDataException>(Admit).Message.ShouldContain("but " + refused + " is selected");
        }
        File.WriteAllText(catalog, Catalog("4.1.0", tenants, memories));
        Should.Throw<InvalidDataException>(Admit).Message.ShouldContain("but 4.1.0 is selected");
        // A later owner release is admitted by the same rule once its lock entries exist; the committed lock refuses it.
        // These genuine nuget.org packageHash values are test inputs only and never enter the committed lock.
        var release = Hexalith.Platform.CatalogArtifacts.OwnerPackageLock.Parse(System.Text.Encoding.UTF8.GetBytes("""
            {"schemaVersion": 1, "packages": [
              {"id": "Hexalith.EventStore.Client", "version": "5.2.0", "contentHash": "/75VQNzzvd09DkbD6jlLzBCpLB4o/N9u+b4DTRi2NcPjNGvQXCWYXpi+QLL8JjP+g4D0HAiFcdSeaKfd/uYfpw=="},
              {"id": "Hexalith.EventStore.Contracts", "version": "5.2.0", "contentHash": "Ziuou6o21o7ppvdSY/2qAffVOLEcg4Ew1QgjE8BfOvK4smPzVPKReWTAwv8QhLm5624x6wwT+IoDBdLcrU3SCg=="},
              {"id": "Hexalith.EventStore.DomainService", "version": "5.2.0", "contentHash": "2gFWTXZshn2+cSEBXrPMcPgtapiorWfglQg8GG+RdX8nPRWrF67IDi5057JInnrP46vlakUuceTOIv1ZxpzJog=="},
              {"id": "Hexalith.EventStore.ServiceDefaults", "version": "5.2.0", "contentHash": "ZEOSUfrh+7iOOj1Qprzfio60vKA6QyZI/b+bw9FdJk06+CnpoZSDtmPK36JwjjQVbHrJnWQ4hN7GusjwPWWUIw=="},
              {"id": "Hexalith.Tenants.Contracts", "version": "8.0.1", "contentHash": "Y/CEYTJxwG5wI4P0VfDHi3ecMDunkDCj+840biy5ujuZBu1FoK9hLXxrz+hEO2bD0xv3aA/zZGZWS2tDnNWRZg=="},
              {"id": "Hexalith.Tenants.Server", "version": "8.0.1", "contentHash": "x1umR5hN7sQ+Xcv9N9a/vAsdF7BPW0qHaoTKxwet/c7Q+fhgxl9e/NRKZWrGdl/l1PSwl2MiLRbeDNBUaaCinQ=="}]}
            """), "new-release.lock.json");
        string[] releaseIds = [.. PlatformPackageCatalogQualification.LoadedPackages("eventstore"), .. PlatformPackageCatalogQualification.LoadedPackages("tenants")];
        releaseIds.ShouldAllBe(id => release.Entries.Any(entry => entry.Id == id));
        File.WriteAllText(catalog, Catalog("5.2.0", "8.0.1", memories));
        PlatformPackageCatalogQualification.RequireLockedVersions(release, selection, ["eventstore", "tenants"]);
        Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.RequireLockedVersions(owners, selection, ["eventstore", "tenants"]))
            .Message.ShouldContain("but 5.2.0 is selected");
        File.WriteAllText(catalog, Catalog(sdk, tenants, memories));
        Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.RequireLockedVersions(release, selection, ["eventstore", "tenants"]))
            .Message.ShouldContain("but " + sdk + " is selected");
        Directory.Exists(Path.Combine(options.RunPlan.Workspace, "current-owner")).ShouldBeFalse();
    }

    /// <summary>The production finalization guard rejects a clean standalone HEAD advance; this component fixture issues no artifact proof.</summary>
    [Fact]
    public void StandaloneCleanAdvanceDuringPreparedStageRefusesIssuance()
    {
        using PlatformPartiesProofFixture fixture = new();
        PlatformCompositionOptions options = Current(fixture);
        PlatformPartiesRunProof oldParent = PlatformPartiesRunProof.Create(options, fixture.Key);
        string root = options.Mapping.Root;
        string revision = PlatformPartiesProofFixture.Git(root, "rev-parse", "HEAD");
        string tree = PlatformPartiesProofFixture.Git(root, "rev-parse", "HEAD^{tree}");
        PlatformPartiesProofFixture.Git(root, "status", "--porcelain").ShouldBeEmpty();
        Action? previous = PlatformCurrentOwnerProof.PreparedObserver.Value;
        int observed = 0;
        try
        {
            PlatformCurrentOwnerProof.PreparedObserver.Value = () =>
            {
                observed++;
                PlatformPartiesProofFixture.Git(root, "-c", "user.name=Local proof fixture", "-c", "user.email=fixture@invalid",
                    "-c", "commit.gpgsign=false", "commit", "--allow-empty", "-q", "-m", "test(proof): record local boundary inputs");
                PlatformPartiesProofFixture.Git(root, "status", "--porcelain").ShouldBeEmpty();
                PlatformPartiesProofFixture.Git(root, "rev-parse", "HEAD^{tree}").ShouldBe(tree);
            };
            Should.Throw<InvalidDataException>(() => PlatformCurrentOwnerProof.VerifyPreparedRevision(root, revision));
            observed.ShouldBe(1);
            PlatformPartiesProofFixture.Git(root, "rev-parse", "HEAD").ShouldNotBe(revision);
            oldParent.Matches(options).ShouldBeFalse();
            PlatformPartiesRunProof.Create(options, fixture.Key).Matches(options).ShouldBeTrue();
            options.CurrentOwnerProof.ShouldBeNull();
            Directory.Exists(Path.Combine(fixture.Options.RunPlan!.Workspace, "current-owner")).ShouldBeFalse();
        }
        finally { PlatformCurrentOwnerProof.PreparedObserver.Value = previous; }
    }

    // Component-only real MSBuild validation: no private artifact proof or runtime qualification is issued.
    private static void AssertSeedValidationRetainsSnapshot(PlatformPartiesProofFixture fixture, PlatformCompositionOptions options)
    {
        string seed = Path.Combine(fixture.Root, "Host.csproj");
        File.Copy(options.EventStoreSeedProject!, seed);
        options = options with { EventStoreSeedProject = seed };
        string workspace = options.RunPlan!.Workspace;
        string snapshot = Path.Combine(workspace, "source-mapping", "catalog-versions.json");
        string catalogHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(workspace, "source-mapping", "pinned-tree", "Props", "Directory.Packages.props"))));
        Dictionary<string, string> pinned = new() { ["Props/Directory.Packages.props"] = catalogHash };
        Dictionary<string, string> versions = new();
        Dictionary<string, IReadOnlyDictionary<string, string>> projects = new();
        string pin = options.Mapping.PlatformIdentity!.CatalogCommit!;
        var snapshotRecord = new CatalogVersionSnapshot(options.Mapping.ContentHash, pin, catalogHash, pinned, versions, projects,
            CatalogVersionSnapshot.ComputeHash(options.Mapping.ContentHash, pin, catalogHash, pinned, versions, projects));
        File.WriteAllBytes(snapshot, JsonSerializer.SerializeToUtf8Bytes(snapshotRecord));
        string preflight = Path.Combine(workspace, "seed-preflight");
        Directory.CreateDirectory(preflight);
        _ = PlatformPackageCatalogReader.Prepare(new XDocument(new XElement("Project")), options, workspace, preflight, DateTimeOffset.UtcNow.AddMinutes(1));
        byte[] prepared = File.ReadAllBytes(snapshot);
        PlatformPartiesRunProof proof = PlatformPartiesRunProof.Create(options, fixture.Key);
        proof.Matches(options).ShouldBeTrue();
        // Execute the real generated validation task as the actual seed consumer at its later build boundary.
        XDocument validation = XDocument.Load(Path.Combine(preflight, "validate-catalog.proj"));
        XElement target = validation.Root!.Element("Target")!;
        XElement seedValidation = new(target.Elements().First());
        seedValidation.SetAttributeValue("ProjectName", Path.GetFileNameWithoutExtension(seed));
        target.ReplaceNodes(seedValidation);
        string later = Path.Combine(preflight, "validate-seed.proj");
        validation.Save(later);
        var result = CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet",
            ["msbuild", later, "-target:Validate", "-nologo", "-m:1", "-nodeReuse:false"], options.PlatformRoot, null),
            TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        result.ExitCode.ShouldBe(0, result.ErrorOutput);
        result.TimedOut.ShouldBeFalse();
        result.OutputTruncated.ShouldBeFalse();
        proof.Matches(options).ShouldBeTrue("Later legitimate seed validation must preserve the issued parent proof without repair or recapture.");
        File.ReadAllBytes(snapshot).SequenceEqual(prepared).ShouldBeTrue("The actual seed consumer must be evaluated before the parent proof seals its snapshot.");
        string ordinary = Path.Combine(workspace, "ordinary-preflight");
        Directory.CreateDirectory(ordinary);
        _ = PlatformPackageCatalogReader.Prepare(new XDocument(new XElement("Project")), options with { RunPlan = null }, workspace, ordinary, DateTimeOffset.UtcNow.AddMinutes(1));
        XDocument ordinaryValidation = XDocument.Load(Path.Combine(ordinary, "validate-catalog.proj"));
        ordinaryValidation.Root!.Element("Target")!.Elements().Select(element => (string?)element.Attribute("ProjectName"))
            .ShouldBe(["Hexalith.Platform.CatalogBuilder"], "Ordinary reader preparation must retain its existing consumer selection.");
        File.ReadAllBytes(snapshot).SequenceEqual(prepared).ShouldBeTrue();
        File.AppendAllText(snapshot, " ");
        proof.Matches(options).ShouldBeFalse("Exact snapshot tampering remains refused after early preparation.");
    }

    private static PlatformCompositionOptions Current(PlatformPartiesProofFixture fixture)
    {
        PlatformCompositionOptions original = fixture.Options;
        string declaration = original.DeclarationPaths.Single();
        JsonNode manifest = JsonNode.Parse(File.ReadAllBytes(declaration))!;
        JsonArray tests = manifest["modules"]![0]!["tests"]!.AsArray();
        JsonNode suite = tests.Single(test => test!["name"]!.GetValue<string>() == "platform-package")!.DeepClone();
        suite["executionTimeoutSeconds"] = 301;
        tests[tests.IndexOf(tests.Single(test => test!["name"]!.GetValue<string>() == PlatformCurrentOwnerProof.Profile))] = suite;
        File.WriteAllBytes(declaration, JsonSerializer.SerializeToUtf8Bytes(manifest));
        PlatformPartiesProofFixture.Commit(original.Mapping.Root);
        string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(declaration)));
        ModuleTestSelection selected = original.RunPlan!.SelectedTest! with { ManifestHash = hash, ExecutionTimeoutSeconds = 301 };
        CompositionRunPlan plan = original.RunPlan with { PlatformDeclarationHash = hash, SelectedTest = selected, SelectedTests = [selected] };
        CompositionDocumentStore.WriteAsync(Path.Combine(plan.Workspace, "plan.json"), plan, CancellationToken.None).GetAwaiter().GetResult();
        var changed = original with { RunPlan = plan, PartiesRunProof = null };
        return changed with { PartiesRunProof = PlatformPartiesRunProof.Create(changed, fixture.Key) };
    }
}
