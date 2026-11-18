namespace Hexalith.Platform.Composition.Tests;

using System.Runtime.InteropServices;
using Aspire.Hosting;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Platform.CatalogArtifacts;
using Shouldly;

/// <summary>Exercises the actual key-bearing ComposeRun path without starting resources or a package fleet.</summary>
public sealed class PlatformPackageCatalogEntryPointTests
{
    /// <summary>A genuine authenticated request resolves a catalog-selected image before adding resources.</summary>
    [Fact]
    public void ValidBoundRequestReachesImageProbeThroughComposeRun()
    {
        using PlatformQualificationPackageFixture fixture = PlatformQualificationPackageFixture.SelectedSdkTest();
        string key = CompositionSigningKey.Create();
        CompositionRunPlan plan = CompositionQualificationOwners.Bind(fixture.Plan with { QualificationOwnersBinding = null }, key);
        var builder = Builder();
        int probes = 0;
        string cohort = PlatformPartiesPackageFixture.EventStoreTestCohort;
        string version = PlatformServerBindings.Version(fixture.Options, "tenants", "entry test");
        string tag = "registry.hexalith.com/tenants:" + version;
        var image = PlatformPartiesPackageFixture.ImageEvidence(tag, index: true);
        bool qualified = OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64
            && PlatformPackageCatalogQualification.HasQualifiedSdkBytes(cohort)
            && PublishedCatalogArtifactCohort.Matches("Hexalith.Tenants.Contracts", cohort)
            && PublishedCatalogArtifactCohort.Matches("Hexalith.Tenants.Server", cohort);
        void Compose() => PlatformAppModel.ComposeRun(builder, plan, key, fixture.Options.EventStoreSeedProject, options => options with
        {
            ImageProbeRunner = (start, timeout, _) =>
            {
                probes++;
                start.FileName.ShouldBe("docker");
                timeout.ShouldBe(TimeSpan.FromSeconds(20));
                if (probes == 1)
                {
                    start.ArgumentList.ShouldBe(["buildx", "imagetools", "inspect", tag, "--format", "{{json .Manifest}}"]);
                    return Task.FromResult(new CompositionProcessResult(true, 0, image.Tag, false));
                }
                if (probes == 2)
                {
                    start.ArgumentList.ShouldBe(["manifest", "inspect", "--verbose", image.Reference]);
                    return Task.FromResult(new CompositionProcessResult(true, 0, image.Manifest, false));
                }
                start.ArgumentList.ShouldBe(["buildx", "imagetools", "inspect", "registry.hexalith.com/tenants-api:" + version, "--format", "{{json .Manifest}}"]);
                return Task.FromResult(new CompositionProcessResult(true, 83, "", false));
            },
        });
        if (qualified)
        {
            Should.Throw<InvalidDataException>(Compose).Message.ShouldContain("registry.hexalith.com/tenants-api:" + version);
            probes.ShouldBe(3);
        }
        else
        {
            Should.Throw<InvalidDataException>(Compose).Message.ShouldContain("differs from its verified published bytes");
            probes.ShouldBe(0);
        }
        builder.Resources.ShouldBeEmpty();
        Directory.Exists(plan.Workspace).ShouldBeFalse();
    }

    /// <summary>Changed authentication inputs refuse in the actual entry point before configuration or image probes.</summary>
    /// <param name="fault">The parent-key or serialized-request alteration.</param>
    [Theory]
    [InlineData("key")]
    [InlineData("binding")]
    [InlineData("drop-binding")]
    [InlineData("owners")]
    public void InvalidBoundRequestRefusesBeforeComposeRunEffects(string fault)
    {
        using PlatformQualificationPackageFixture fixture = PlatformQualificationPackageFixture.HistoricalPublic();
        string key = CompositionSigningKey.Create();
        CompositionRunPlan plan = CompositionQualificationOwners.Bind(fixture.Plan with { QualificationOwnersBinding = null }, key);
        string originalBinding = plan.QualificationOwnersBinding!;
        plan = fault switch
        {
            "key" => plan,
            "binding" => plan with { QualificationOwnersBinding = (originalBinding[0] == '0' ? "1" : "0") + originalBinding[1..] },
            "drop-binding" => plan with { QualificationOwnersBinding = null },
            "owners" => plan with { QualificationOwners = ["memories"] },
            _ => throw new ArgumentOutOfRangeException(nameof(fault)),
        };
        string suppliedKey = fault == "key" ? CompositionSigningKey.Create() : key;
        var builder = Builder();
        int configured = 0;
        int probes = 0;
        Should.Throw<InvalidDataException>(() => PlatformAppModel.ComposeRun(builder, plan, suppliedKey, fixture.Options.EventStoreSeedProject, options =>
        {
            configured++;
            return options with { ImageProbeRunner = (_, _, _) => { probes++; throw new InvalidOperationException("No image probe is permitted."); } };
        }));
        configured.ShouldBe(0);
        probes.ShouldBe(0);
        builder.Resources.ShouldBeEmpty();
        Directory.Exists(plan.Workspace).ShouldBeFalse();
    }

    private static IDistributedApplicationBuilder Builder() => DistributedApplication.CreateBuilder(new DistributedApplicationOptions
    {
        Args = ["--environment=Development"], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true, AllowUnsecuredTransport = true,
    });
}
