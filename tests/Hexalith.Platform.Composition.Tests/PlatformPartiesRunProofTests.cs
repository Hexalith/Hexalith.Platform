namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using Aspire.Hosting;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Platform.CatalogArtifacts;
using Shouldly;

/// <summary>Exercises the ordinary trusted-entry proof separately from public/image cohort admission and real service qualification.</summary>
public sealed class PlatformPartiesRunProofTests
{
    /// <summary>The real key-bearing entry creates the private proof before its composition callback; this is not image or fleet qualification.</summary>
    [Fact]
    public void TrustedComposeRunCreatesOrdinaryProofWithoutExpandedOwners()
    {
        using PlatformPartiesProofFixture fixture = new();
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--environment=Development"], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true, AllowUnsecuredTransport = true,
        });
        int configured = 0;
        Should.Throw<InvalidOperationException>(() => PlatformAppModel.ComposeRun(builder, fixture.Options.RunPlan!, fixture.Key,
            fixture.Options.EventStoreSeedProject, options =>
            {
                configured++;
                options.PartiesRunProof!.Matches(options).ShouldBeTrue();
                options.QualificationOwnerProof.ShouldBeNull();
                options.QualificationOwners.ShouldBeEmpty();
                options.RunPlan!.QualificationOwners.ShouldBeNull();
                options.RunPlan.QualificationOwnersBinding.ShouldBeNull();
                throw new InvalidOperationException("Expected trusted-entry proof boundary.");
            })).Message.ShouldBe("Expected trusted-entry proof boundary.");
        configured.ShouldBe(1);
        builder.Resources.ShouldBeEmpty();
        Directory.Exists(Path.Combine(fixture.Options.RunPlan!.Workspace, "platform-components")).ShouldBeFalse();
    }

    /// <summary>Snapshot reuse never repairs a missing or stale proof and never accepts another workspace.</summary>
    /// <param name="fault">The missing authority, changed bytes or foreign workspace.</param>
    [Theory]
    [InlineData("missing-proof")]
    [InlineData("changed-snapshot")]
    [InlineData("foreign-workspace")]
    public void RunnerSnapshotReuseRefusesUnverifiedInputsWithoutRematerializing(string fault)
    {
        using PlatformPartiesProofFixture fixture = new();
        PlatformCompositionOptions options = fixture.Options;
        string workspace = options.RunPlan!.Workspace;
        string path = Path.Combine(workspace, "source-mapping", "catalog-versions.json");
        if (fault == "missing-proof") options = options with { PartiesRunProof = null };
        if (fault == "changed-snapshot") File.AppendAllText(path, " ");
        byte[] before = File.ReadAllBytes(path);
        string destination = fault == "foreign-workspace" ? Path.Combine(workspace, "foreign") : workspace;
        Should.Throw<InvalidDataException>(() => PlatformAppModel.MaterializeSourceMapping(options, destination));
        File.ReadAllBytes(path).ShouldBe(before);
        Directory.Exists(Path.Combine(workspace, "foreign")).ShouldBeFalse();
        Directory.Exists(Path.Combine(workspace, "platform-components")).ShouldBeFalse();
    }

    /// <summary>The actual private parent files and clean local source retain the proof without creating additive owner authority.</summary>
    [Fact]
    public void OrdinaryProofBindsActualParentInputsWithoutAnExpansionMac()
    {
        using PlatformPartiesProofFixture fixture = new();
        fixture.Options.PartiesRunProof!.Matches(fixture.Options).ShouldBeTrue();
        fixture.Options.RunPlan!.QualificationOwners.ShouldBeNull();
        fixture.Options.RunPlan.QualificationOwnersBinding.ShouldBeNull();
        fixture.Options.QualificationOwners.ShouldBeEmpty();
        fixture.Options.QualificationOwnerProof.ShouldBeNull();
        Directory.Exists(Path.Combine(fixture.Options.RunPlan.Workspace, "platform-components")).ShouldBeFalse();
    }

    /// <summary>A clean active revision advance changes native source while preserving manifest/project bytes and must invalidate the proof.</summary>
    [Fact]
    public void CleanActiveGitRevisionDriftInvalidatesTheOriginalProof()
    {
        using PlatformPartiesProofFixture fixture = new();
        string active = fixture.Options.Mapping.Root;
        string before = PlatformPartiesProofFixture.Git(active, "rev-parse", "HEAD");
        File.AppendAllText(Path.Combine(active, "Native.cs"), "// A different committed native implementation.\n");
        PlatformPartiesProofFixture.Commit(active);
        PlatformPartiesProofFixture.Git(active, "status", "--porcelain", "--untracked-files=normal").ShouldBeEmpty();
        PlatformPartiesProofFixture.Git(active, "rev-parse", "HEAD").ShouldNotBe(before);
        fixture.Options.PartiesRunProof!.Matches(fixture.Options).ShouldBeFalse();
    }

    /// <summary>Copies or mutations of the ordinary plan cannot replay the in-memory proof across its issued inputs.</summary>
    /// <param name="fault">The changed trusted input.</param>
    [Theory]
    [InlineData("run")]
    [InlineData("attempt")]
    [InlineData("environment")]
    [InlineData("source")]
    [InlineData("mapping")]
    [InlineData("platform-root")]
    [InlineData("owner")]
    [InlineData("selected-test")]
    [InlineData("declaration")]
    [InlineData("model")]
    [InlineData("catalog-generation")]
    [InlineData("configuration")]
    [InlineData("images")]
    [InlineData("projects")]
    [InlineData("expanded-owner")]
    [InlineData("expansion-binding")]
    public void MutatedOptionsCannotReplayProof(string fault)
    {
        using PlatformPartiesProofFixture fixture = new();
        PlatformCompositionOptions original = fixture.Options;
        CompositionRunPlan plan = original.RunPlan!;
        PlatformCompositionOptions changed = fault switch
        {
            "run" => original with { RunPlan = plan with { RunId = CompositionRunPlanFactory.NewRunId() } },
            "attempt" => original with { RunPlan = plan with { AttemptId = CompositionRunPlanFactory.NewRunId() } },
            "environment" => original with { EnvironmentId = CompositionRunPlanFactory.NewRunId() },
            "source" => original with { Mapping = original.Mapping with { PlatformIdentity = original.Mapping.PlatformIdentity! with { CheckoutCommit = new string('0', 40) } } },
            "mapping" => original with { Mapping = original.Mapping with { ActiveModule = "Hexalith.Foreign" } },
            "platform-root" => original with { PlatformRoot = fixture.Root },
            "owner" => original with { ActiveModule = "eventstore" },
            "selected-test" => original with { RunPlan = plan with { SelectedTest = plan.SelectedTest! with { Name = "undeclared" } } },
            "declaration" => original with { DeclarationPaths = [] },
            "model" => original with { RunPlan = plan with { PlatformModelAssemblyPath = typeof(PlatformPartiesRunProofTests).Assembly.Location } },
            "catalog-generation" => original with { CatalogGeneration = 2 },
            "configuration" => original with { Configuration = new Dictionary<string, string> { ["foreign"] = "value" } },
            "images" => original with { Images = new Dictionary<string, string>() },
            "projects" => original with { Projects = new Dictionary<string, string>() },
            "expanded-owner" => original with { QualificationOwners = ["tenants"] },
            "expansion-binding" => original with { RunPlan = plan with { QualificationOwnersBinding = new string('a', 64) } },
            _ => throw new ArgumentOutOfRangeException(nameof(fault)),
        };
        original.PartiesRunProof!.Matches(changed).ShouldBeFalse();
        Directory.Exists(Path.Combine(plan.Workspace, "platform-components")).ShouldBeFalse();
    }

    /// <summary>Private handoff, selected entry and source changes invalidate the original proof even with unchanged option values.</summary>
    /// <param name="file">The mutated private or source input.</param>
    [Theory]
    [InlineData("plan")]
    [InlineData("mapping")]
    [InlineData("catalog")]
    [InlineData("pinned-catalog")]
    [InlineData("declaration")]
    [InlineData("test")]
    [InlineData("seed")]
    [InlineData("identity")]
    public void ChangedPhysicalInputsInvalidateProof(string file)
    {
        using PlatformPartiesProofFixture fixture = new();
        PlatformCompositionOptions options = fixture.Options;
        CompositionRunPlan plan = options.RunPlan!;
        string selected = file switch
        {
            "plan" => Path.Combine(plan.Workspace, "plan.json"),
            "mapping" => Path.Combine(plan.Workspace, "source-mapping", "mapping.json"),
            "catalog" => Path.Combine(plan.Workspace, "source-mapping", "catalog-versions.json"),
            "pinned-catalog" => Path.Combine(plan.Workspace, "source-mapping", "pinned-tree", "Props", "Directory.Packages.props"),
            "declaration" => options.DeclarationPaths[0],
            "test" => Path.Combine(options.Mapping.Root, plan.SelectedTest!.Entry),
            "seed" => options.EventStoreSeedProject!,
            "identity" => options.Mapping.PlatformIdentity!.RecordPath,
            _ => throw new ArgumentOutOfRangeException(nameof(file)),
        };
        File.AppendAllText(selected, " ");
        options.PartiesRunProof!.Matches(options).ShouldBeFalse();
    }

    /// <summary>The actual selected project must belong to the active root rather than a nested repository.</summary>
    [Fact]
    public void NestedSelectedTestRepositoryIsRefusedAtProofCreation()
    {
        using PlatformPartiesProofFixture fixture = new();
        string nested = Path.GetDirectoryName(Path.Combine(fixture.Options.Mapping.Root, fixture.Options.RunPlan!.SelectedTest!.Entry))!;
        PlatformPartiesProofFixture.Git(nested, "init", "-q");
        Should.Throw<InvalidDataException>(() => PlatformPartiesRunProof.Create(fixture.Options, fixture.Key));
    }

    /// <summary>The ordinary handoff requires the actual 384-bit parent key shape; no ordinary-plan MAC is invented.</summary>
    /// <param name="key">A missing or malformed parent handoff key.</param>
    [Theory]
    [InlineData("")]
    [InlineData("not-base64")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA==")]
    public void InvalidParentKeyRefusesProofCreation(string key)
    {
        using PlatformPartiesProofFixture fixture = new();
        Should.Throw<InvalidDataException>(() => PlatformPartiesRunProof.Create(fixture.Options, key));
    }

    /// <summary>Serialized options never carry the proof and cannot reach probes or resources through ordinary Compose.</summary>
    [Fact]
    public void SerializedOrdinaryOptionsCannotAcquireAdmission()
    {
        using PlatformPartiesProofFixture fixture = new();
        string serialized = JsonSerializer.Serialize(fixture.Options);
        serialized.ShouldNotContain("PartiesRunProof");
        PlatformCompositionOptions unbound = JsonSerializer.Deserialize<PlatformCompositionOptions>(serialized)!;
        unbound.PartiesRunProof.ShouldBeNull();
        Should.Throw<InvalidDataException>(() => PlatformPartiesPackageCatalogQualification.Bind(unbound, [], [], true));
    }

    /// <summary>A valid ordinary proof alone does not authorize an empty or foreign cohort, topology or production scope.</summary>
    /// <param name="development">Whether the caller requested the sole Development scope.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProofDoesNotReplaceFiniteCohortAndTopologyChecks(bool development)
    {
        using PlatformPartiesProofFixture fixture = new();
        fixture.Options.PartiesRunProof!.Matches(fixture.Options).ShouldBeTrue();
        Should.Throw<InvalidDataException>(() => PlatformPartiesPackageCatalogQualification.Bind(fixture.Options, [], [], development));
    }

    /// <summary>The independent reader allowlist refuses foreign bodies in both separate public cohorts.</summary>
    /// <param name="cohort">The supported or unknown requested cohort.</param>
    [Theory]
    [InlineData(PublishedCatalogArtifactCohort.Name)]
    [InlineData(PublishedCatalogArtifactCohort.FullPartiesName)]
    [InlineData("unknown")]
    public void ReaderAuthorityRefusesForeignPublicBytes(string cohort)
    {
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(typeof(PlatformPartiesRunProofTests).Assembly, cohort));
    }
}
