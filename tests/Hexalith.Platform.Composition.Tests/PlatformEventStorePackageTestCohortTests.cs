namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.Platform.CatalogArtifacts;
using Microsoft.Extensions.Hosting;
using Shouldly;

/// <summary>Private EventStore package-test component guards over lock-admitted owner packages; no services or native acceptance are simulated.</summary>
public sealed class PlatformEventStorePackageTestCohortTests
{
    /// <summary>Even a correctly authenticated parent cannot select another purpose, graph, mode or catalog.</summary>
    /// <param name="fault">The explicit negative input.</param>
    [Theory]
    [InlineData("key")]
    [InlineData("binding")]
    [InlineData("missing-binding")]
    [InlineData("missing-proof")]
    [InlineData("profile")]
    [InlineData("executor")]
    [InlineData("entry")]
    [InlineData("entry-hash")]
    [InlineData("entry-bytes")]
    [InlineData("manifest")]
    [InlineData("stop-budget")]
    [InlineData("execution-budget")]
    [InlineData("test-module")]
    [InlineData("test-mode")]
    [InlineData("runtime")]
    [InlineData("empty-tests")]
    [InlineData("duplicate-tests")]
    [InlineData("mixed-tests")]
    [InlineData("active-module")]
    [InlineData("mapping-module")]
    [InlineData("source-mode")]
    [InlineData("owners")]
    [InlineData("extra-owner")]
    [InlineData("development")]
    [InlineData("catalog")]
    [InlineData("sdk")]
    [InlineData("near-version")]
    [InlineData("metadata-version")]
    [InlineData("snapshot")]
    [InlineData("catalog-snapshot")]
    [InlineData("declaration")]
    public void InvalidRequestsRefuseBeforeComposeRunEffects(string fault)
    {
        using PlatformQualificationPackageFixture fixture = PlatformQualificationPackageFixture.SelectedSdkTest();
        RequireBaseline(fixture);
        string key = CompositionSigningKey.Create();
        CompositionRunPlan plan = fixture.Plan;
        ModuleTestSelection test = plan.SelectedTest!;
        test = fault switch
        {
            "profile" => test with { Name = "platform-source" },
            "executor" => test with { Executor = "dotnet-test" },
            "entry" => test with { Entry = "owned.csproj" },
            "entry-hash" => test with { EntryHash = new string('0', 64) },
            "manifest" => test with { ManifestHash = new string('0', 64) },
            "stop-budget" => test with { StopTimeoutSeconds = 30 },
            "execution-budget" => test with { ExecutionTimeoutSeconds = 121 },
            "test-module" => test with { ModuleId = "tenants" },
            "test-mode" => test with { Mode = "source" },
            "runtime" => test with { DaprRuntimeVersion = "1.18.7" },
            _ => test,
        };
        plan = plan with { SelectedTest = test, SelectedTests = [test] };
        if (fault == "empty-tests") plan = plan with { SelectedTests = [] };
        if (fault == "duplicate-tests") plan = plan with { SelectedTests = [test, test] };
        if (fault == "mixed-tests") plan = plan with { SelectedTests = [test, test with { Name = "platform-source" }] };
        if (fault == "active-module") plan = plan with { PlatformActiveModule = "tenants" };
        if (fault == "mapping-module") plan = plan with { SourceMapping = plan.SourceMapping! with { ActiveModule = "Hexalith.Tenants" } };
        if (fault == "source-mode") plan = plan with { SourceMapping = plan.SourceMapping! with { Mode = WorkspaceMode.Source } };
        if (fault == "owners") plan = plan with { QualificationOwners = ["memories"] };
        if (fault == "extra-owner") plan = plan with { QualificationOwners = ["tenants", "memories"] };
        plan = Sign(plan, key);
        if (fault == "key") key = CompositionSigningKey.Create();
        if (fault == "binding") plan = plan with { QualificationOwnersBinding = new string('0', 64) };
        if (fault == "missing-binding") plan = plan with { QualificationOwnersBinding = null };
        if (fault == "missing-proof") plan = plan with { QualificationOwners = null, QualificationOwnersBinding = null };
        const string tenantsProperty = "HexalithTenantsVersion";
        string tenants = PlatformPartiesPackageFixture.LockedOwnerVersion("tenants");
        if (fault == "catalog") fixture.ChangeCatalogVersion(tenantsProperty, tenants, "7.0.4");
        if (fault == "sdk")
            fixture.ChangeCatalogVersion("HexalithEventStoreVersion", PlatformPartiesPackageFixture.SelectedSdkVersion, "4.0.4");
        if (fault == "near-version") fixture.ChangeCatalogVersion(tenantsProperty, tenants, tenants + "0");
        if (fault == "metadata-version") fixture.ChangeCatalogVersion(tenantsProperty, tenants, tenants + "+foreign");
        if (fault == "snapshot") File.AppendAllText(Path.Combine(fixture.Root, "src", "Hexalith.Platform.Composition", "Declarations", "tenants.json"), " ");
        if (fault == "declaration") File.AppendAllText(plan.PlatformDeclarationPaths.Single(), " ");
        if (fault == "entry-bytes") File.AppendAllText(Path.Combine(fixture.Root, test.Entry), " ");
        if (fault == "catalog-snapshot")
        {
            Directory.CreateDirectory(Path.Combine(plan.Workspace, "source-mapping"));
            File.WriteAllText(Path.Combine(plan.Workspace, "source-mapping", "catalog-versions.json"), "{}");
        }
        var builder = Builder(fault == "development" ? "Production" : "Development");
        Should.Throw<InvalidDataException>(() => PlatformAppModel.ComposeRun(builder, plan, key, fixture.Options.EventStoreSeedProject,
            options =>
            {
                var declarations = PlatformDeclarationSet.Load(options);
                var servers = PlatformServerBindings.Resolve(options, declarations);
                PlatformPackageCatalogQualification.ValidateTestInputs(options, declarations, servers,
                    builder.Environment.IsDevelopment() && builder.ExecutionContext.IsRunMode);
                throw new InvalidOperationException("Changed authority passed authenticated input validation before the image gate.");
            }));
        if (fault == "catalog-snapshot")
        {
            builder.Resources.ShouldBeEmpty();
            Directory.Exists(Path.Combine(plan.Workspace, "platform-components")).ShouldBeFalse();
            File.ReadAllText(Path.Combine(plan.Workspace, "source-mapping", "catalog-versions.json")).ShouldBe("{}");
        }
        else AssertNoEffects(builder, plan);
    }

    /// <summary>Changed resolved declarations, server sets, image tags or supplied digests refuse against the same valid proof.</summary>
    /// <param name="fault">The post-resolution mutation.</param>
    [Theory]
    [InlineData("missing-server")]
    [InlineData("extra-server")]
    [InlineData("old-image")]
    [InlineData("foreign-image")]
    [InlineData("digest")]
    [InlineData("seed")]
    [InlineData("changed-declaration")]
    public void ChangedResolvedTopologyRefusesBeforeEffects(string fault)
    {
        using PlatformQualificationPackageFixture fixture = PlatformQualificationPackageFixture.SelectedSdkTest();
        var declarations = PlatformDeclarationSet.Load(fixture.Options);
        var original = PlatformServerBindings.Resolve(fixture.Options, declarations);
        PlatformPackageCatalogQualification.ValidateTestInputs(fixture.Options, declarations, original, true)
            .Path.ShouldBe(OwnerPackageLock.PathFor(fixture.Root));
        IReadOnlyList<PlatformServerBinding> servers = fault switch
        {
            "missing-server" => original.Where(server => server.ServerId != "api").ToArray(),
            "extra-server" => [.. original, original[0] with { ServerId = "foreign" }],
            "old-image" => original.Select(server => server.Image is null ? server : server with { Image = server.Image[..server.Image.LastIndexOf(':')] + ":7.0.4" }).ToArray(),
            "foreign-image" => original.Select(server => server.Image is null ? server : server with { Image = "registry.invalid/foreign:7.0.5" }).ToArray(),
            "digest" => original.Select(server => server.Image is null ? server : server with { ImageDigest = new string('0', 64) }).ToArray(),
            "seed" => original.Select(server => server.ServerId == "gateway" ? server with { ProjectPath = "foreign.csproj" } : server).ToArray(),
            _ => original,
        };
        if (fault == "changed-declaration")
        {
            JsonNode changed = JsonNode.Parse(declarations[0].Declaration.GetRawText())!;
            changed["foreign"] = true;
            declarations = [declarations[0] with { Declaration = JsonSerializer.SerializeToElement(changed) }, .. declarations.Skip(1)];
        }
        Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.ValidateTestInputs(fixture.Options, declarations, servers, true));
        Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.Bind(fixture.Options, declarations, servers, true));
        fixture.Options.QualificationOwnerProof!.Matches(fixture.Options).ShouldBeTrue();
        Directory.Exists(fixture.Plan.Workspace).ShouldBeFalse();
    }

    /// <summary>Serialized and absent proofs cannot admit ordinary launch or reach a reader workspace.</summary>
    [Fact]
    public void OrdinaryAndSerializedOptionsCannotSelectThePrivatePurpose()
    {
        using PlatformQualificationPackageFixture fixture = PlatformQualificationPackageFixture.SelectedSdkTest();
        RequireBaseline(fixture);
        string serialized = JsonSerializer.Serialize(fixture.Options);
        serialized.ShouldNotContain("QualificationOwnerProof");
        PlatformCompositionOptions ordinary = JsonSerializer.Deserialize<PlatformCompositionOptions>(serialized)!;
        ordinary.QualificationOwnerProof.ShouldBeNull();
        Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.RequireLockedPackages(ordinary));
        var builder = Builder();
        Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, ordinary));
        AssertNoEffects(builder, fixture.Plan);
        var servers = PlatformServerBindings.Resolve(fixture.Options, PlatformDeclarationSet.Load(fixture.Options));
        Should.Throw<InvalidDataException>(() => PlatformRoutingCatalog.Generate(ordinary, servers, Path.Combine(fixture.Plan.Workspace, "platform-components")));
        Directory.Exists(fixture.Plan.Workspace).ShouldBeFalse();
    }

    private static void RequireBaseline(PlatformQualificationPackageFixture fixture)
    {
        PlatformPackageCatalogQualification.RequireLockedPackages(fixture.Options).Path.ShouldBe(OwnerPackageLock.PathFor(fixture.Root));
        var declarations = PlatformDeclarationSet.Load(fixture.Options);
        var servers = PlatformServerBindings.Resolve(fixture.Options, declarations);
        fixture.Options.QualificationOwnerProof!.Matches(fixture.Options).ShouldBeTrue();
        PlatformPackageCatalogQualification.ValidateTestInputs(fixture.Options, declarations, servers, true)
            .Path.ShouldBe(OwnerPackageLock.PathFor(fixture.Root));
        PlatformPackageCatalogQualification.Bind(fixture.Options, declarations, servers, true)
            .ShouldAllBe(server => server.ImageDigest == null);
    }

    private static CompositionRunPlan Sign(CompositionRunPlan plan, string key) => CompositionQualificationOwners.Bind(plan with { QualificationOwnersBinding = null }, key);
    private static IDistributedApplicationBuilder Builder(string environment = "Development") => DistributedApplication.CreateBuilder(new DistributedApplicationOptions
    {
        Args = ["--environment=" + environment], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true, AllowUnsecuredTransport = true,
    });
    private static void AssertNoEffects(IDistributedApplicationBuilder builder, CompositionRunPlan plan)
    {
        builder.Resources.ShouldBeEmpty();
        Directory.Exists(plan.Workspace).ShouldBeFalse();
    }
}
