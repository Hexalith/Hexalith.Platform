namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Runtime;
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
    public void PublishedPurposeCannotAuthorizeCurrentProfile()
    {
        using PlatformPartiesProofFixture fixture = new();
        PlatformCompositionOptions options = Current(fixture);
        Should.Throw<InvalidDataException>(() => PlatformPartiesRunProof.Create(options, fixture.Key));
        fixture.Options.PartiesRunProof!.Matches(options).ShouldBeFalse();
        PlatformPartiesRunProof current = PlatformPartiesRunProof.CreateCurrent(options, fixture.Key);
        current.MatchesCurrent(options).ShouldBeTrue();
        current.Matches(options).ShouldBeFalse();
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
        if (change == "entry") File.AppendAllText(Path.Combine(options.Mapping.Root, PlatformCurrentOwnerProof.TestEntry), "changed");
        if (change == "snapshot") File.AppendAllText(Path.Combine(options.RunPlan!.Workspace, "source-mapping", "catalog-versions.json"), " ");
        string key = change == "key" ? "not-a-key" : fixture.Key;
        if (change == "snapshot")
        {
            // The proof captures a legitimate snapshot once, then refuses even whitespace tampering.
            File.WriteAllText(Path.Combine(options.RunPlan!.Workspace, "source-mapping", "catalog-versions.json"), "{}");
            PlatformPartiesRunProof proof = PlatformPartiesRunProof.CreateCurrent(options, key);
            File.AppendAllText(Path.Combine(options.RunPlan.Workspace, "source-mapping", "catalog-versions.json"), " ");
            proof.MatchesCurrent(options).ShouldBeFalse();
        }
        else Should.Throw<InvalidDataException>(() => PlatformPartiesRunProof.CreateCurrent(options, key));
    }

    [Fact]
    public void OrdinarySourceDependencyFixtureCannotIssueMeasuredCurrentOwnerAuthority()
    {
        using PlatformPartiesProofFixture fixture = new();
        PlatformCompositionOptions options = Current(fixture);
        Should.Throw<InvalidDataException>(() => PlatformCurrentOwnerProof.Create(options, fixture.Key, developmentRun: true));
        Directory.Exists(Path.Combine(options.RunPlan!.Workspace, "current-owner")).ShouldBeFalse();
        // Component-only version selection: these local catalog bytes grant no artifact or runtime authority.
        string catalogRoot = Path.Combine(fixture.Root, "version-selection");
        string catalog = Path.Combine(catalogRoot, "references", "Hexalith.Builds", "Props", "Directory.Packages.props");
        Directory.CreateDirectory(Path.GetDirectoryName(catalog)!);
        PlatformCompositionOptions selection = options with { PlatformRoot = catalogRoot, RunPlan = null };
        const string Prefix = "<Project><PropertyGroup><HexalithEventStoreVersion>4.1.0</HexalithEventStoreVersion><HexalithTenantsVersion>7.0.4</HexalithTenantsVersion><HexalithMemoriesVersion>";
        const string Suffix = "</HexalithMemoriesVersion></PropertyGroup></Project>";
        File.WriteAllText(catalog, Prefix + "3.0.25" + Suffix);
        PlatformPackageCatalogQualification.SelectCurrentOwnerCohort(selection).ShouldBe(Hexalith.Platform.CatalogArtifacts.PublishedCatalogArtifactCohort.CurrentOwnerName);
        foreach (string refused in new[] { "3.0.23", "3.0.24", "3.0.250", "3.0.25+unqualified", "3.0.26" })
        {
            File.WriteAllText(catalog, Prefix + refused + Suffix);
            PlatformPackageCatalogQualification.SelectCurrentOwnerCohort(selection).ShouldBeNull();
        }
        Directory.Exists(Path.Combine(options.RunPlan.Workspace, "current-owner")).ShouldBeFalse();
    }

    /// <summary>The production finalization guard rejects a clean standalone HEAD advance; this component fixture issues no artifact proof.</summary>
    [Fact]
    public void StandaloneCleanAdvanceDuringPreparedStageRefusesIssuance()
    {
        using PlatformPartiesProofFixture fixture = new();
        PlatformCompositionOptions options = Current(fixture);
        PlatformPartiesRunProof oldParent = PlatformPartiesRunProof.CreateCurrent(options, fixture.Key);
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
            oldParent.MatchesCurrent(options).ShouldBeFalse();
            PlatformPartiesRunProof.CreateCurrent(options, fixture.Key).MatchesCurrent(options).ShouldBeTrue();
            options.CurrentOwnerProof.ShouldBeNull();
            Directory.Exists(Path.Combine(fixture.Options.RunPlan!.Workspace, "current-owner")).ShouldBeFalse();
        }
        finally { PlatformCurrentOwnerProof.PreparedObserver.Value = previous; }
    }

    private static PlatformCompositionOptions Current(PlatformPartiesProofFixture fixture)
    {
        PlatformCompositionOptions original = fixture.Options;
        string declaration = original.DeclarationPaths.Single();
        JsonNode manifest = JsonNode.Parse(File.ReadAllBytes(declaration))!;
        JsonArray tests = manifest["modules"]![0]!["tests"]!.AsArray();
        JsonNode suite = tests.Single(test => test!["name"]!.GetValue<string>() == "platform-package")!.DeepClone();
        suite["name"] = PlatformCurrentOwnerProof.Profile;
        tests.Add(suite);
        File.WriteAllBytes(declaration, JsonSerializer.SerializeToUtf8Bytes(manifest));
        PlatformPartiesProofFixture.Commit(original.Mapping.Root);
        string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(declaration)));
        ModuleTestSelection selected = original.RunPlan!.SelectedTest! with { Name = PlatformCurrentOwnerProof.Profile, ManifestHash = hash };
        CompositionRunPlan plan = original.RunPlan with { PlatformDeclarationHash = hash, SelectedTest = selected, SelectedTests = [selected] };
        CompositionDocumentStore.WriteAsync(Path.Combine(plan.Workspace, "plan.json"), plan, CancellationToken.None).GetAwaiter().GetResult();
        return original with { RunPlan = plan, PartiesRunProof = null };
    }
}
