namespace Hexalith.Platform.Composition.Tests;

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Runtime;
using Shouldly;

/// <summary>Unconditional full-profile component cases; publication and a coherent real owner checkout are mandatory prerequisites.</summary>
public sealed class PlatformPartiesPackageEntryTests
{
    /// <summary>Both real declared package suites reach all seven exact image probes through the trusted entry without adding owners or starting resources.</summary>
    /// <param name="profile">The actual declared suite.</param>
    [Theory]
    [InlineData("platform-package")]
    [InlineData("lifecycle-package")]
    public async Task QualifiedOrdinaryEntryReachesAllImmutableImageProbesAsync(string profile)
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(profile);
        fixture.AssertPositiveEntry();
    }

    /// <summary>Each replay/serialization/scope mutation starts from an actually admitted full closure and refuses before probes or effects.</summary>
    /// <param name="fault">The altered authority input.</param>
    [Theory]
    [InlineData("run")]
    [InlineData("attempt")]
    [InlineData("environment")]
    [InlineData("mapping")]
    [InlineData("source")]
    [InlineData("declaration-path")]
    [InlineData("selected-test")]
    [InlineData("expanded-owner")]
    [InlineData("serialized")]
    [InlineData("production")]
    [InlineData("catalog-bytes")]
    public async Task ChangedFullEntryRefusesBeforeEffectsAsync(string fault)
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync();
        fixture.AssertPositiveEntry();
        var builder = PlatformPartiesPackageFixture.Builder(fault == "production" ? "Production" : "Development");
        int probes = 0;
        Should.Throw<InvalidDataException>(() => fixture.Compose(builder, original =>
        {
            if (fault == "catalog-bytes") File.AppendAllText(Path.Combine(fixture.Workspace, "source-mapping", "catalog-versions.json"), " ");
            var plan = original.RunPlan!;
            var options = fault switch
            {
                "run" => original with { RunPlan = plan with { RunId = CompositionRunPlanFactory.NewRunId() } },
                "attempt" => original with { RunPlan = plan with { AttemptId = CompositionRunPlanFactory.NewRunId() } },
                "environment" => original with { EnvironmentId = CompositionRunPlanFactory.NewRunId() },
                "mapping" => original with { Mapping = original.Mapping with { ActiveModule = "Hexalith.Foreign" } },
                "source" => original with { Mapping = original.Mapping with { PlatformIdentity = original.Mapping.PlatformIdentity! with { CheckoutCommit = new string('0', 40) } } },
                "declaration-path" => original with { DeclarationPaths = [] },
                "selected-test" => original with { RunPlan = plan with { SelectedTest = plan.SelectedTest! with { EntryHash = new string('0', 64) } } },
                "expanded-owner" => original with { QualificationOwners = ["tenants"] },
                "serialized" => JsonSerializer.Deserialize<PlatformCompositionOptions>(JsonSerializer.Serialize(original))!,
                "production" or "catalog-bytes" => original,
                _ => throw new ArgumentOutOfRangeException(nameof(fault)),
            };
            return options with { ImageProbeRunner = (_, _, _) => { probes++; throw new InvalidOperationException("Unexpected image probe."); } };
        }));
        probes.ShouldBe(0);
        fixture.AssertNoEffects(builder);
    }

    /// <summary>Mutated closure/topology/image/declaration inputs are tested only after complete positive entry, independently of earlier missing prerequisites.</summary>
    /// <param name="fault">The altered full-profile input.</param>
    [Theory]
    [InlineData("missing-server")]
    [InlineData("extra-server")]
    [InlineData("foreign-image")]
    [InlineData("preset-digest")]
    [InlineData("missing-owner")]
    [InlineData("changed-declaration")]
    [InlineData("non-linux")]
    [InlineData("arm64")]
    public async Task ChangedQualifiedTopologyOrRuntimeRefusesAsync(string fault)
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync();
        fixture.AssertPositiveEntry();
        var options = fixture.Options;
        IReadOnlyList<PlatformModuleDeclaration> declarations = PlatformDeclarationSet.Load(options);
        var servers = PlatformAppModel.Prepare(options);
        switch (fault)
        {
            case "missing-server": servers = servers.Where(server => server.ServerId != "api").ToArray(); break;
            case "extra-server": servers = [.. servers, servers[0] with { ServerId = "mcp" }]; break;
            case "foreign-image": servers = servers.Select(server => server.Image is null ? server : server with { Image = "registry.example/foreign:3.0.23" }).ToArray(); break;
            case "preset-digest": servers = servers.Select(server => server.Image is null ? server : server with { ImageDigest = new string('0', 64) }).ToArray(); break;
            case "missing-owner": declarations = declarations.Where(module => PlatformDeclarationSet.Id(module.Declaration) != "memories").ToArray(); break;
            case "changed-declaration":
                JsonNode changed = JsonNode.Parse(declarations[0].Declaration.GetRawText())!;
                changed["identity"]!["dependencies"] = new JsonArray("foreign");
                declarations = [new(declarations[0].Source, JsonSerializer.SerializeToElement(changed)), .. declarations.Skip(1)];
                break;
            case "non-linux" or "arm64": break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        Should.Throw<InvalidDataException>(() => PlatformPartiesPackageCatalogQualification.Bind(options, declarations, servers,
            developmentRun: true, linux: fault != "non-linux", architecture: fault == "arm64" ? Architecture.Arm64 : Architecture.X64));
        fixture.AssertNoEffects(PlatformPartiesPackageFixture.Builder());
    }
}
