namespace Hexalith.Platform.Composition.Tests;

using System.Runtime.InteropServices;
using Aspire.Hosting;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Platform.CatalogArtifacts;
using Shouldly;

/// <summary>Exercises the actual key-bearing ComposeRun path without starting resources or a package fleet.</summary>
public sealed class PlatformPackageCatalogEntryPointTests
{
    /// <summary>A genuine loaded cohort reaches only the first immutable image probe through entry-point proof creation.</summary>
    [Fact]
    public void ValidBoundRequestReachesImageProbeThroughComposeRun()
    {
        using PlatformQualificationPackageFixture fixture = new();
        string key = CompositionSigningKey.Create();
        CompositionRunPlan plan = CompositionQualificationOwners.Bind(fixture.Plan with { QualificationOwnersBinding = null }, key);
        var builder = Builder();
        int probes = 0;
        bool qualified = OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64
            && PlatformPackageCatalogQualification.HasQualifiedSdkBytes() && PublishedCatalogArtifactCohort.Matches("Hexalith.Tenants.Contracts");
        void Compose() => PlatformAppModel.ComposeRun(builder, plan, key, fixture.Options.EventStoreSeedProject, options => options with
        {
            ImageProbeRunner = (start, _, _) =>
            {
                probes++;
                start.ArgumentList.ShouldBe(["manifest", "inspect", "registry.hexalith.com/tenants:7.0.0@sha256:2f4ab155bc4d33e4b16f1bc8995c45de83503bd8ad99d2c7e46ebbef382b6c42"]);
                throw new InvalidOperationException("Expected immutable image-probe boundary reached.");
            },
        });
        if (qualified)
        {
            Should.Throw<InvalidOperationException>(Compose).Message.ShouldBe("Expected immutable image-probe boundary reached.");
            probes.ShouldBe(1);
        }
        else
        {
            Should.Throw<InvalidDataException>(Compose).Message.ShouldContain("owner SDK");
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
        using PlatformQualificationPackageFixture fixture = new();
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
