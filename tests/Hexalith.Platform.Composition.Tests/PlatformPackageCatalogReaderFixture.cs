namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Text.Json;
using Aspire.Hosting;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

/// <summary>Uses synthetic active-SDK entry metadata with a real Git catalog and genuine loaded public artifacts; never starts a fleet.</summary>
internal sealed class PlatformPackageCatalogReaderFixture : IDisposable
{
    private readonly PlatformQualificationPackageFixture _owner;
    private readonly string _key;

    /// <summary>Selects genuine catalog/tool inputs and explicitly synthetic active-SDK entry files without starting resources.</summary>
    /// <param name="currentTest">Selects the separate current EventStore test inputs; historical callers keep the authentic public default.</param>
    internal PlatformPackageCatalogReaderFixture(bool currentTest = false, bool sdk42 = false)
    {
        _owner = sdk42 ? PlatformQualificationPackageFixture.ExtensionSdkTest() : currentTest ? PlatformQualificationPackageFixture.CurrentTest() : PlatformQualificationPackageFixture.HistoricalPublic();
        string platform = PlatformTestRepository.Root;
        string root = Metadata("PlatformTestReaderWorkspaceRoot");
        if (string.IsNullOrWhiteSpace(root)) root = platform;
        (Directory.Exists(Path.Combine(root, ".git")) || File.Exists(Path.Combine(root, ".git"))).ShouldBeTrue("The fixture needs a genuine existing Git root; it never creates Git metadata.");
        string builds = Path.Combine(root, "references", "Hexalith.Builds");
        string tool = Metadata("PlatformTestPublishedToolRoot");
        Seed = Path.Combine(tool, "g4-host", "projects", "EventStore", "Host.csproj");
        File.Exists(Seed).ShouldBeTrue("The actual published tool seed is required for this package component fixture.");
        using JsonDocument binding = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(platform, "eng", "builds-tuple.json")));
        string pin = binding.RootElement.GetProperty("producer").GetProperty("sourceRevision").GetString()!;
        using JsonDocument identity = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(platform, "hexalith-platform.json")));
        string range = identity.RootElement.GetProperty("toolVersionRange").GetString()!;
        var observed = CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("git", ["rev-parse", "HEAD"], platform, null), TimeSpan.FromSeconds(10), CancellationToken.None).GetAwaiter().GetResult();
        observed.ExitCode.ShouldBe(0);
        string source = observed.Output.Trim();
        var mapping = new SourceMapping(WorkspaceMode.Package, root, "Hexalith.EventStore",
            [new("Hexalith.Builds", "source", builds), new("Hexalith.EventStore", "source", _owner.Root), new("Hexalith.Platform", "source", platform),
             new("Hexalith.Tenants", "package", null), new("Hexalith.Memories", "package", null)], true)
        {
            PlatformIdentity = new PlatformIdentity(source, source, source, platform, null, pin, range, Path.Combine(platform, "hexalith-platform.json"), true),
        };
        // These empty projects are solely source-identity negative fixtures. The package reader must never consume their assemblies.
        foreach (string id in new[] { "Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService", "Hexalith.EventStore.ServiceDefaults" })
        {
            string project = Path.Combine(_owner.Root, "src", id, id + ".csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(project)!);
            File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><PackageId>" + id + "</PackageId><IsPackable>true</IsPackable></PropertyGroup></Project>");
        }
        Plan = _owner.Plan with { SourceMapping = mapping, Workspace = Path.Combine(_owner.Root, "reader-run"), StartupDeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(600), QualificationOwnersBinding = null };
        _key = CompositionSigningKey.Create();
        Plan = CompositionQualificationOwners.Bind(Plan, _key);
    }

    /// <summary>Materializes the genuine catalog before capturing the actual authenticated entry path, matching normal runner ordering.</summary>
    internal async Task MaterializeAsync()
    {
        await SourceMappingMaterializer.WriteAsync(Plan.SourceMapping!, Plan.Workspace, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--environment=Development"], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true, AllowUnsecuredTransport = true,
        });
        PlatformCompositionOptions? captured = null;
        int probes = 0;
        Should.Throw<InvalidOperationException>(() => PlatformAppModel.ComposeRun(builder, Plan, _key, Seed, options =>
        {
            captured = options;
            return options with { ImageProbeRunner = (_, _, _) => { probes++; throw new InvalidOperationException("Expected private reader fixture boundary."); } };
        })).Message.ShouldBe("Expected private reader fixture boundary.");
        probes.ShouldBe(1);
        builder.Resources.ShouldBeEmpty();
        Directory.Exists(Path.Combine(Plan.Workspace, "platform-components")).ShouldBeFalse();
        Options = captured!;
    }

    /// <summary>Calls the real authenticated model entry with this fixture's private proof key, without exposing that key.</summary>
    internal void ComposeRun(Aspire.Hosting.IDistributedApplicationBuilder builder, Func<PlatformCompositionOptions, PlatformCompositionOptions> configure)
        => PlatformAppModel.ComposeRun(builder, Plan, _key, Seed, configure);

    /// <summary>Gets the actual authenticated private options with explicitly synthetic active-SDK entry files.</summary>
    internal PlatformCompositionOptions Options { get; private set; } = null!;
    /// <summary>Gets the proof-bearing component plan; this is not a live runner acceptance record.</summary>
    internal CompositionRunPlan Plan { get; }
    /// <summary>Gets the actual published tool's seed project.</summary>
    internal string Seed { get; }
    /// <summary>Gets the independently owned reader directory.</summary>
    internal string Reader => Path.Combine(Plan.Workspace, "platform-components", "catalog-reader");

    /// <summary>Calls the production reader generator with the unchanged authoritative closure.</summary>
    internal PlatformRoutingCatalog Generate() => PlatformRoutingCatalog.Generate(Options, PlatformAppModel.Prepare(Options), Path.Combine(Plan.Workspace, "platform-components"));

    /// <summary>Reads one exact build-provided fixture path, avoiding ambient environment overrides.</summary>
    private static string Metadata(string key) => typeof(PlatformPackageCatalogReaderFixture).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .SingleOrDefault(attribute => attribute.Key == key)?.Value ?? "";

    /// <inheritdoc />
    public void Dispose() => _owner.Dispose();
}
