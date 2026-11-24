namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.EventStore.Contracts.Routing;
using Shouldly;

/// <summary>Exercises the signed fixture's separate host source and package admission boundaries.</summary>
public sealed class PlatformFixturePackageQualificationTests
{
    [Fact]
    public void CommittedGatewaySourcesAndVerifiedSeedSettingsAreRetained()
    {
        using HostWorkspace workspace = new();
        Directory.CreateDirectory(Path.Combine(workspace.Host, "bin"));
        Directory.CreateDirectory(Path.Combine(workspace.Host, "obj"));
        PlatformFixturePackageQualification.HostSources(workspace.Options).Count.ShouldBe(3);

        string shim = Path.Combine(workspace.Root, "shim");
        Directory.CreateDirectory(shim);
        File.WriteAllText(Path.Combine(shim, "appsettings.json"), "\"unverified overlay\"");
        IReadOnlyDictionary<string, string> settings = new Dictionary<string, string> { ["appsettings.json"] = "{\"Approved\":true}" };
        string content = PlatformGatewayConfiguration.Create(Path.Combine(shim, "shim.csproj"), workspace.Root, [], settings);
        File.ReadAllText(Path.Combine(content, "appsettings.json")).ShouldBe(settings["appsettings.json"]);
        File.Exists(Path.Combine(content, "appsettings.Development.json")).ShouldBeFalse();

        string seed = Path.Combine(shim, "published-seed.csproj");
        File.WriteAllText(seed, "<Project />");
        string project = PlatformEventStoreExtensionBindings.HostProjectPath(workspace.Options);
        PlatformServerBinding gateway = new(null!, "gateway", "eventstore", "eventstore", project, null, "/ready", new Dictionary<string, string>(), []);
        foreach (WorkspaceMode mode in new[] { WorkspaceMode.Package, WorkspaceMode.Source })
        {
            var options = workspace.Options with { Mapping = workspace.Options.Mapping with { Mode = mode } };
            JsonElement composed = JsonSerializer.SerializeToElement(PlatformRoutingCatalogBootstrap.DescribeHost(
                gateway with { EventStoreHost = new(seed, []) }, options));
            composed.GetProperty("artifactKind").GetString().ShouldBe("composed-project");
            composed.GetProperty("artifact").GetString().ShouldBe(project);
            composed.GetProperty("artifactSha256").ValueKind.ShouldBe(JsonValueKind.Null);
            composed.GetProperty("projectFileSha256").GetString().ShouldBe(Digest(project));
            composed.GetProperty("seedArtifact").GetString().ShouldBe(seed);
            composed.GetProperty("seedArtifactSha256").GetString().ShouldBe(Digest(seed));
        }
        JsonElement unchanged = JsonSerializer.SerializeToElement(PlatformRoutingCatalogBootstrap.DescribeHost(gateway with { ProjectPath = seed }, workspace.Options));
        unchanged.GetProperty("artifactKind").GetString().ShouldBe("published-seed");
        unchanged.GetProperty("artifactSha256").ValueKind.ShouldBe(JsonValueKind.Null);
        unchanged.GetProperty("projectFileSha256").GetString().ShouldBe(Digest(seed));
        unchanged.TryGetProperty("seedArtifact", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("extra-source")]
    [InlineData("host-import")]
    [InlineData("ancestor-import")]
    [InlineData("changed-observation")]
    [InlineData("missing-observation")]
    [InlineData("committed-project-substitution")]
    [InlineData("extra-directory")]
    public void UnapprovedCompilerInputsRefuse(string fault)
    {
        using HostWorkspace workspace = new();
        string observer = Path.Combine(workspace.Host, "SignedFixtureObservation.cs");
        switch (fault)
        {
            case "extra-source":
                File.WriteAllText(Path.Combine(workspace.Host, "Extra.cs"), "// unapproved source");
                break;
            case "host-import":
                File.WriteAllText(Path.Combine(workspace.Host, "Directory.Build.props"), "<Project />");
                break;
            case "ancestor-import":
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(workspace.Host)!, "Directory.Build.targets"), "<Project />");
                break;
            case "changed-observation":
                File.AppendAllText(observer, "\n// changed observer");
                break;
            case "missing-observation":
                File.Delete(observer);
                break;
            case "committed-project-substitution":
                File.AppendAllText(PlatformEventStoreExtensionBindings.HostProjectPath(workspace.Options), "\n<!-- replaced -->");
                PlatformPartiesProofFixture.Commit(workspace.Root);
                break;
            case "extra-directory":
                Directory.CreateDirectory(Path.Combine(workspace.Host, "Unexpected"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fault));
        }

        _ = Should.Throw<InvalidDataException>(() => PlatformFixturePackageQualification.HostSources(workspace.Options));
    }

    [Fact]
    public void DeclaredFixtureCannotBorrowTheExistingPackageCohort()
    {
        using HostWorkspace workspace = new();
        _ = Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.Bind(workspace.Options, [], [], developmentRun: true));
        _ = Should.Throw<InvalidDataException>(() => PlatformFixturePackageQualification.RequireOwners(workspace.Options));
        var failure = Should.Throw<InvalidDataException>(() => PlatformFixtureProtection.Attach(null!, workspace.Options));
        failure.Message.ShouldContain("protection-fixture-proof");
    }

    [Fact]
    public async Task ParentPreparationRefusesAForeignGatewayObservationBeforeProcessAccessOrRetentionAsync()
    {
        using HostWorkspace workspace = new();
        CompositionRunPlan plan = workspace.NewRun();
        string output = Path.Combine(plan.Workspace, "project-builds", "eventstore", "bin");
        Directory.CreateDirectory(output);
        string package = PlatformFixturePackageQualification.Package;
        string assembly = Path.Combine(output, package + ".dll");
        File.WriteAllText(assembly, "Exact body for this fixture observation control.");
        string archiveDigest = new('a', 64);
        string libraryDigest = Digest(assembly);
        File.WriteAllBytes(Path.Combine(plan.Workspace, "fixture-host-loaded.json"), JsonSerializer.SerializeToUtf8Bytes(new
        {
            runId = CompositionRunPlanFactory.NewRunId(), packageId = package, version = "2.2.5", extensionId = "platform-test", apiMajor = 1,
            archiveSha256 = archiveDigest, librarySha256 = libraryDigest, processId = int.MaxValue, loadedAssembly = assembly,
            assetsType = "package", depsType = "package",
        }));
        string catalog = Path.Combine(plan.Workspace, "catalog");
        Directory.CreateDirectory(catalog);
        PlatformFixtureHostPreparation preparation = new(plan, catalog, (package, "2.2.5", archiveDigest, libraryDigest));
        var refusal = await Should.ThrowAsync<InvalidDataException>(() => preparation.PrepareAsync(null!, TestContext.Current.CancellationToken));
        refusal.Message.ShouldContain("(PrepareAsync)");
        File.Exists(Path.Combine(catalog, "fixture-host-loaded.json")).ShouldBeFalse();
    }

    [Fact]
    public async Task ObserverEnvironmentBindsTheMeasuredInstallationAndRefusesAForeignReceiptAsync()
    {
        using HostWorkspace workspace = new();
        workspace.CopyCommittedProtectionInputs();
        CompositionRunPlan plan = workspace.NewRun();
        string directory = Path.Combine(plan.Workspace, "fixture-protection");
        Directory.CreateDirectory(directory);
        string installation = Path.Combine(directory, "installation.json");
        File.WriteAllText(installation, "{\"fixture\":true}");
        string digest = Digest(installation);
        void WriteReceipt(string runId) => File.WriteAllBytes(Path.Combine(directory, "installed.json"), JsonSerializer.SerializeToUtf8Bytes(new
        {
            runId, installationSha256 = digest, healthy = true, rootRevoked = true, runtimeAccess = "read-only",
            networkMode = "none", listenerAddress = "127.0.0.1", publishedPorts = 0,
        }));
        WriteReceipt(plan.RunId);

        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = [], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true,
        });
        var gateway = builder.AddResource(new ProjectResource("eventstore"));
        new PlatformFixtureProtection(builder, workspace.Root, plan).Configure(gateway);
        async Task<Dictionary<string, object>> EnvironmentAsync()
        {
            Dictionary<string, object> environment = [];
            foreach (var annotation in gateway.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
                await annotation.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, gateway.Resource, environment, TestContext.Current.CancellationToken));
            return environment;
        }

        Dictionary<string, object> actual = await EnvironmentAsync();
        actual["EventStore__PayloadProtection__Enabled"].ShouldBe("true");
        actual["EventStore__PayloadProtection__QualificationObserver"].ShouldBe("true");
        actual["EventStore__PayloadProtection__InstallationFile"].ShouldBe(installation);
        actual["EventStore__PayloadProtection__InstallationSha256"].ShouldBe(digest);
        File.WriteAllText(installation, "{\"fixture\":false}");
        (await Should.ThrowAsync<InvalidDataException>(EnvironmentAsync)).Message.ShouldContain("protection-installation-identity");
        File.WriteAllText(installation, "{\"fixture\":true}");
        (await EnvironmentAsync())["EventStore__PayloadProtection__InstallationSha256"].ShouldBe(digest);
        WriteReceipt(CompositionRunPlanFactory.NewRunId());
        var refusal = await Should.ThrowAsync<InvalidDataException>(EnvironmentAsync);
        refusal.Message.ShouldContain("protection-installation-identity");
    }

    [Fact]
    public void FixtureReaderRefusesADifferentRouteFacetWithTheSameInventoryCounts()
    {
        const string routeFacet = "44c6a777693baf59cdc2b575beaebd0a3ba74c610c78e5a2a4a13280fe9f5c03";
        const string idempotencyFacet = "676c1d71d38a8335e4bfad713152a995a8333ae44c3086759c822d6072878f83";
        // The reader admission consumes the verified envelope's counts and digests, not individual route or policy fields.
        RouteCatalogEnvelope catalog = new(3, 1, "Development", false, [], new RouteCatalogEntry[30], new IdempotencyCatalogEntry[19], routeFacet, idempotencyFacet);
        using JsonDocument receipt = JsonDocument.Parse("""
            {"ownerAssemblies":["Hexalith.Memories.EventStore.dll","Hexalith.Tenants.Contracts.dll"],
             "adapterAssemblies":["Hexalith.Memories.EventStoreExtensions.dll","Hexalith.Tenants.Server.dll"]}
            """);
        PlatformFixturePackageQualification.VerifyReader(catalog, receipt.RootElement);
        var refusal = Should.Throw<InvalidDataException>(() => PlatformFixturePackageQualification.VerifyReader(
            catalog with { RouteFacetDigest = new string('f', 64) }, receipt.RootElement));
        refusal.Message.ShouldContain("fixture-reader-inventory");
    }

    private static string Digest(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private sealed class HostWorkspace : IDisposable
    {
        internal HostWorkspace()
        {
            string relative = Path.GetDirectoryName(PlatformEventStoreExtensionBindings.HostProject)!;
            Host = Path.Combine(Root, relative);
            Directory.CreateDirectory(Host);
            foreach (string name in new[] { "Hexalith.Platform.EventStoreHost.csproj", "Program.cs", "SignedFixtureObservation.cs" })
                File.Copy(Path.Combine(PlatformTestRepository.Root, relative, name), Path.Combine(Host, name));
            PlatformPartiesProofFixture.Git(Root, "init", "-q");
            PlatformPartiesProofFixture.Commit(Root);
            Options = new PlatformCompositionOptions
            {
                PlatformRoot = Root,
                ActiveModule = PlatformFixturePackageQualification.Module,
                Mapping = new SourceMapping(WorkspaceMode.Package, Root, "Hexalith.Platform", [], true),
            };
        }

        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "platform-fixture-host-source-" + Guid.NewGuid().ToString("N"));

        internal string Host { get; }

        internal PlatformCompositionOptions Options { get; }

        internal CompositionRunPlan NewRun()
        {
            string run = CompositionRunPlanFactory.NewRunId();
            string workspace = Path.Combine(Root, run);
            Directory.CreateDirectory(workspace);
            return CompositionRunPlanFactory.Create(run, workspace, Path.Combine(Root, "dapr"),
                new CompositionRunPorts(20001, 20002, 20003, 20004, 20005, 20006, 20007, 20008, 20009, 20010, 20011, 20012, 20013,
                    [20014], 20015, 20016, 20017, [new(20018, 20019, 20020, 20021)]),
                [new("eventstore", "eventstore", "eventstore", PlatformEventStoreExtensionBindings.HostProjectPath(Options))], []);
        }

        internal void CopyCommittedProtectionInputs()
        {
            foreach (string relative in new[] { "eng/test-extension-release/provision-fixture-protection.py", "deploy/erasure-root/bootstrap.py", "deploy/erasure-root/custody-policy.hcl" })
            {
                string destination = Path.Combine(Root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(Path.Combine(PlatformTestRepository.Root, relative), destination);
            }
            PlatformPartiesProofFixture.Commit(Root);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
