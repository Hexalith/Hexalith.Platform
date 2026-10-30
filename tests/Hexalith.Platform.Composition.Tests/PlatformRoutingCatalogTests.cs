namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.EventStore.Contracts.Routing;
using Shouldly;

[Collection("Platform file entry")]
public class PlatformRoutingCatalogTests
{
    [Fact]
    public async Task ActualMappedOwnersProduceIdenticalBytesInIndependentOwnedInstances()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "platform-routing-generation-" + Guid.NewGuid().ToString("N"));
        CompositionWorkspace.CreatePrivateDirectory(workspace);
        var options = await OptionsAsync(includeParties: true);
        var servers = PlatformAppModel.Prepare(options);
        List<PlatformRoutingCatalog> generated = [];
        foreach (string environment in new[] { "catalog-run-one", "catalog-run-two" })
        {
            string directory = Path.Combine(workspace, environment);
            CompositionWorkspace.CreatePrivateDirectory(directory);
            await SourceMappingMaterializer.WriteAsync(options.Mapping, directory, TestContext.Current.CancellationToken);
            generated.Add(PlatformRoutingCatalog.Generate(options with { EnvironmentId = environment }, servers, directory));
        }
        File.ReadAllBytes(generated[0].CatalogPath).ShouldBe(File.ReadAllBytes(generated[1].CatalogPath));
        generated[0].InstanceId.ShouldBe("catalog-run-one-catalog");
        generated[1].InstanceId.ShouldBe("catalog-run-two-catalog");
        generated[0].CatalogPath.ShouldNotBe(generated[1].CatalogPath);
        foreach (var catalog in generated)
        {
            RouteCatalogCodec.Decode(File.ReadAllBytes(catalog.CatalogPath), catalog.RootDigest).SchemaVersion.ShouldBe(3);
            catalog.Envelope.Operations!.Count.ShouldBe(catalog.Envelope.Declarations.Count);
            catalog.Envelope.Operations.ShouldContain(operation => operation.Ingress == "public" && operation.Eligibility == "agent-eligible");
            catalog.Envelope.Operations.ShouldContain(operation => operation.Ingress == "internal");
            catalog.Envelope.Declarations.ShouldContain(declaration => declaration.Domain == "party");
            File.ReadAllText(catalog.CatalogPath).ShouldNotContain(workspace);
            using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.Directory, "receipt.json")));
            receipt.RootElement.GetProperty("instanceId").GetString().ShouldBe(catalog.InstanceId);
        }
        string committed = Path.Combine(workspace, "tampered-predecessor.json");
        File.WriteAllText(committed, "{}");
        byte[] original = File.ReadAllBytes(generated[0].CatalogPath);
        Should.Throw<InvalidDataException>(() => PlatformRoutingCatalog.Generate(options with
        { EnvironmentId = "catalog-run-one", CatalogGeneration = 2, CommittedRoutingCatalogPath = committed }, servers, Path.Combine(workspace, "catalog-run-one")));
        File.ReadAllBytes(generated[0].CatalogPath).ShouldBe(original);
        Directory.GetDirectories(Path.Combine(workspace, "catalog-run-one"), "catalog-pending-*").ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingMappedContractProjectIsRefusedWithoutPublishing()
    {
        var options = await OptionsAsync();
        var servers = PlatformAppModel.Prepare(options);
        var mapped = options.Mapping with { Entries = options.Mapping.Entries.Select(entry => entry.Identity == "Hexalith.Tenants"
            ? entry with { Path = Path.GetTempPath() } : entry).ToArray(), PlatformIdentity = null };
        Should.Throw<InvalidDataException>(() => PlatformCatalogContractBinding.Resolve(options with { Mapping = mapped }, servers))
            .Message.ShouldContain("tenants");
    }

    [Theory]
    [InlineData(121)]
    [InlineData(128)]
    public void DerivedInstanceLimitRefusesBeforeReaderFilesAreWritten(int length)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "catalog-identifier-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            var options = new PlatformCompositionOptions
            {
                PlatformRoot = workspace, EnvironmentId = new string('a', length),
                Mapping = new SourceMapping(WorkspaceMode.Source, workspace, "Hexalith.Platform", [], false),
            };
            Should.Throw<InvalidDataException>(() => PlatformRoutingCatalog.Generate(options, [], workspace)).Message.ShouldContain("128");
            Directory.GetFileSystemEntries(workspace).ShouldBeEmpty();
        }
        finally { Directory.Delete(workspace, true); }
    }

    [Fact]
    public void MaximumDerivedInstanceLengthPassesIdentifierValidation()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "catalog-identifier-" + Guid.NewGuid().ToString("N"));
        var options = new PlatformCompositionOptions
        {
            PlatformRoot = workspace, EnvironmentId = new string('a', 120),
            Mapping = new SourceMapping(WorkspaceMode.Source, workspace, "Hexalith.Platform", [], false),
        };
        Should.Throw<InvalidDataException>(() => PlatformRoutingCatalog.Generate(options, [], workspace)).Message.ShouldContain("no compiled catalog owners");
        Directory.Exists(workspace).ShouldBeFalse();
    }

    [Fact]
    public void ReaderStagesUseOnlyTheirRemainingStartupBudget()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (TimeSpan stage in new[] { TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1) })
        {
            PlatformRoutingCatalog.ReaderTimeout(stage, null, now).ShouldBe(stage);
            PlatformRoutingCatalog.ReaderTimeout(stage, now.AddHours(1), now).ShouldBe(stage);
            PlatformRoutingCatalog.ReaderTimeout(stage, now.AddSeconds(3), now).ShouldBe(TimeSpan.FromSeconds(3));
            Should.Throw<InvalidDataException>(() => PlatformRoutingCatalog.ReaderTimeout(stage, now, now)).Message.ShouldContain("deadline");
            Should.Throw<InvalidDataException>(() => PlatformRoutingCatalog.ReaderTimeout(stage, now.AddTicks(-1), now)).Message.ShouldContain("deadline");
        }
    }

    [Fact]
    public void ExpiredRunRefusesBeforeAnyReaderPreparation()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "catalog-expired-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            var mapping = new SourceMapping(WorkspaceMode.Source, workspace, "Hexalith.Platform", [], false);
            var plan = CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), workspace, workspace,
                CompositionPortAllocator.Allocate(1), [], [], mapping, [Path.Combine(workspace, "module.json")], null, typeof(PlatformAppModel).Assembly.Location)
                with { StartupDeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(-1) };
            var options = new PlatformCompositionOptions { PlatformRoot = workspace, Mapping = mapping, RunPlan = plan };
            Should.Throw<InvalidDataException>(() => PlatformRoutingCatalog.Generate(options, [], workspace)).Message.ShouldContain("deadline");
            Directory.GetFileSystemEntries(workspace).ShouldBeEmpty();
        }
        finally { Directory.Delete(workspace, true); }
    }

    internal static async Task<PlatformCompositionOptions> OptionsAsync(bool includeParties = false)
    {
        string root = Root;
        var mapping = await WorkspaceRootResolver.ResolveAsync(Path.Combine(root, "hexalith-platform.json"), WorkspaceMode.Source, TestContext.Current.CancellationToken);
        return new()
        {
            PlatformRoot = root, Mapping = mapping, ActiveModule = includeParties ? "parties" : null,
            DeclarationPaths = includeParties ? [Path.Combine(root, "references", "Hexalith.Parties", "module", "hexalith.module-manifest.v2.json")] : [],
        };
    }

    internal static string Root
    {
        get
        {
            string? selected = Environment.GetEnvironmentVariable("HEXALITH_PLATFORM_TEST_ROOT");
            DirectoryInfo? directory = new(selected ?? AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "apphost.cs"))) directory = directory.Parent;
            return directory!.FullName;
        }
    }
}
