namespace Hexalith.Platform.Composition.Tests;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

public sealed partial class PlatformAppModelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultIssuerLivesInsideTheRenderedWorkspaceAndFollowsItsOwnershipAsync(bool signedRun)
    {
        string directory = Path.Combine(Path.GetTempPath(), "platform-default-issuer-" + Guid.NewGuid().ToString("N"));
        string? rendered = null;
        try
        {
            var builder = Builder();
            builder.Configuration["Dapr:Mtls:CertificateDirectory"] = null;
            builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(directory, "test-dcp-not-started");
            builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(directory, "test-dashboard-not-started");
            if (signedRun)
            {
                SourceMapping mapping = Options().Mapping with { Entries = [.. Options().Mapping.Entries, new SourceMappingEntry("Hexalith.Platform", "source", Root)] };
                CompositionRunPlan plan = CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), directory, directory,
                    CompositionPortAllocator.Allocate(1), [], [], mapping,
                    [Path.Combine(Root, "references/Hexalith.Tenants/module/hexalith.module-manifest.v2.json")], "tenants", typeof(PlatformAppModel).Assembly.Location);
                await CompositionDaprComponentRenderer.WriteAsync(plan, TestContext.Current.CancellationToken);
                PlatformAppModel.ComposeRun(builder, plan, CompositionSigningKey.Create(), null);
                rendered = Path.Combine(plan.Workspace, "platform-components");
            }
            else PlatformAppModel.Compose(builder, Options(active: "tenants"));

            IResource server = builder.Resources.First(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any());
            string configuration = server.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.Config!;
            string configurationDirectory = Path.GetDirectoryName(configuration)!;
            if (signedRun) configurationDirectory.ShouldBe(rendered);
            else rendered = configurationDirectory;
            string credentials = builder.Resources.Single(resource => resource.Name == "dapr-sentry").Annotations.OfType<ContainerMountAnnotation>()
                .Single(mount => mount.Target == "/var/run/dapr/credentials").Source!;
            Path.GetDirectoryName(credentials).ShouldBe(rendered);
            Directory.Exists(credentials).ShouldBeTrue();
            File.WriteAllText(Path.Combine(credentials, "issuer.key"), "owned-default-issuer");
            await using var app = builder.Build();
            PlatformCompositionWorkspace[] owned = app.Services.GetServices<IHostedService>().OfType<PlatformCompositionWorkspace>().ToArray();
            owned.Select(workspace => workspace.Path).ShouldBe(signedRun ? [] : new[] { rendered! });
            await app.DisposeAsync();
            Directory.Exists(credentials).ShouldBe(signedRun, "Builds owns signed workspaces; file-entry disposal owns its workspace and issuer child");
            Directory.Exists(rendered).ShouldBe(signedRun);
        }
        finally
        {
            if (rendered is not null) await CompositionWorkspace.TryDeleteAsync(rendered, CancellationToken.None);
            await CompositionWorkspace.TryDeleteAsync(directory, CancellationToken.None);
        }
    }

    [Fact]
    public async Task FileEntryWithoutDaprHomeSelectsTheUserProfileForEverySidecarAsync()
    {
        string? previous = Environment.GetEnvironmentVariable("HEXALITH_DAPR_HOME");
        try
        {
            Environment.SetEnvironmentVariable("HEXALITH_DAPR_HOME", null);
            var builder = Builder();
            builder.Configuration["Dapr:Mtls:CertificateDirectory"] = null;
            PlatformAppModel.Compose(builder, Options(active: "tenants"));
            await using ServiceProvider services = builder.Services.BuildServiceProvider();
            _ = services.GetRequiredService<PlatformCompositionWorkspace>();
            IResource[] servers = builder.Resources.Where(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()).ToArray();
            servers.Length.ShouldBe(6);
            foreach (IResource server in servers)
            {
                DaprSidecarOptions options = server.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
                options.RuntimePath.ShouldBe(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), server.Name);
            }
        }
        finally { Environment.SetEnvironmentVariable("HEXALITH_DAPR_HOME", previous); }
    }

    [Theory]
    [InlineData("works")]
    [InlineData("eventstore")]
    [InlineData("platform")]
    public void WorksPreviewRefusesCertificateParentsInsideEveryCheckoutBeforeCreatingRunState(string checkout)
    {
        using WorksIsolationFixture fixture = new(Root);
        var builder = fixture.Builder();
        string target = Path.Combine(checkout switch
        {
            "works" => fixture.WorksRoot,
            "eventstore" => fixture.EventStoreRoot,
            _ => fixture.PlatformRoot,
        }, "uncreated-credentials");
        builder.Configuration["Dapr:Mtls:CertificateDirectory"] = target;
        string tempRoot = Path.Combine(Path.GetTempPath(), "hexalith-works-preview");
        string[] before = Directory.Exists(tempRoot) ? Directory.GetDirectories(tempRoot) : [];
        int hostedServices = builder.Services.Count(service => service.ServiceType == typeof(IHostedService));
        InvalidOperationException failure = Should.Throw<InvalidOperationException>(() => WorksPreviewModel.Compose(builder));
        failure.Message.ShouldContain("must resolve outside the repository");
        failure.Message.ShouldContain(target);
        builder.Resources.ShouldBeEmpty();
        builder.Services.Count(service => service.ServiceType == typeof(IHostedService)).ShouldBe(hostedServices);
        (Directory.Exists(tempRoot) ? Directory.GetDirectories(tempRoot) : []).Order(StringComparer.Ordinal).ShouldBe(before.Order(StringComparer.Ordinal));
        Directory.Exists(target).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task WorksPreviewDisposalPreservesDefaultCredentialsWithFailedWorkspaceAsync(bool configuredParent, bool failedStartup)
    {
        using WorksIsolationFixture fixture = new(Root);
        string parent = Path.Combine(fixture.Root, "external-certificate-parent");
        Directory.CreateDirectory(parent);
        string foreign = Path.Combine(parent, "foreign.txt");
        File.WriteAllText(foreign, "untouched");
        string? workspace = null;
        try
        {
            var builder = fixture.Builder();
            builder.Configuration["Dapr:Mtls:CertificateDirectory"] = configuredParent ? parent : null;
            WorksPreviewModel.Compose(builder);
            string credentials = builder.Resources.Single(resource => resource.Name == "dapr-sentry").Annotations.OfType<ContainerMountAnnotation>()
                .Single(mount => mount.Target == "/var/run/dapr/credentials").Source!;
            File.WriteAllText(Path.Combine(credentials, "issuer.key"), "owned-works-issuer");
            await using var app = builder.Build();
            PlatformCompositionWorkspace[] owned = app.Services.GetServices<IHostedService>().OfType<PlatformCompositionWorkspace>().ToArray();
            owned.Length.ShouldBe(configuredParent ? 2 : 1);
            workspace = owned.Single(service => service.Path != credentials).Path;
            Path.GetDirectoryName(credentials).ShouldBe(configuredParent ? parent : workspace);
            if (failedStartup) PlatformCompositionWorkspace.MarkFailed(workspace, "fixture startup failure");
            await app.DisposeAsync();
            Directory.Exists(workspace).ShouldBe(failedStartup);
            Directory.Exists(credentials).ShouldBe(failedStartup && !configuredParent);
            if (failedStartup)
            {
                File.ReadAllText(Path.Combine(workspace, PlatformCompositionWorkspace.FailureMarker)).ShouldBe("fixture startup failure");
                if (!configuredParent) File.ReadAllText(Path.Combine(credentials, "issuer.key")).ShouldBe("owned-works-issuer");
            }
            File.ReadAllText(foreign).ShouldBe("untouched");
        }
        finally { if (workspace is not null) await CompositionWorkspace.TryDeleteAsync(workspace, CancellationToken.None); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CertificateParentContainmentUsesThePlatformPathCaseRules(bool caseVariant)
    {
        string directory = Path.Combine(Path.GetTempPath(), "platform-path-case-" + Guid.NewGuid().ToString("N"));
        string checkout = Path.Combine(directory, "Checkout");
        string target = Path.Combine(directory, caseVariant ? "cHECKOUT" : "Checkout", "credentials");
        var builder = Builder();
        builder.Configuration["Dapr:Mtls:CertificateDirectory"] = target;
        if (!caseVariant || OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            Should.Throw<InvalidOperationException>(() => DaprSelfHostedMtls.ResolveCertificateParent(builder, checkout, directory, null))
                .Message.ShouldContain("must resolve outside the repository");
        else
            DaprSelfHostedMtls.ResolveCertificateParent(builder, checkout, directory, null).ShouldBe(target);
        Directory.Exists(directory).ShouldBeFalse();
    }

    private sealed class WorksIsolationFixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "works-isolation-test-" + Guid.NewGuid().ToString("N"));
        internal string PlatformRoot => Path.Combine(Root, "platform");
        internal string WorksRoot => Path.Combine(Root, "works");
        internal string EventStoreRoot => Path.Combine(Root, "eventstore");
        private string AppHostDirectory => Path.Combine(PlatformRoot, "nested-apphost");

        internal WorksIsolationFixture(string sourceRoot)
        {
            Directory.CreateDirectory(AppHostDirectory);
            File.WriteAllText(Path.Combine(PlatformRoot, "apphost.cs"), "// fixture");
            File.WriteAllText(Path.Combine(PlatformRoot, "aspire.config.json"), "{}");
            foreach (string project in new[]
            {
                Path.Combine(WorksRoot, "src/Hexalith.Works/Hexalith.Works.csproj"),
                Path.Combine(EventStoreRoot, "src/Hexalith.EventStore/Hexalith.EventStore.csproj"),
                Path.Combine(EventStoreRoot, "src/Hexalith.EventStore.Admin.Server.Host/Hexalith.EventStore.Admin.Server.Host.csproj"),
                Path.Combine(EventStoreRoot, "src/Hexalith.EventStore.Operations/Hexalith.EventStore.Operations.csproj"),
            })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(project)!);
                File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
                string properties = Path.Combine(Path.GetDirectoryName(project)!, "Properties");
                Directory.CreateDirectory(properties);
                File.WriteAllText(Path.Combine(properties, "launchSettings.json"), "{\"profiles\":{\"http\":{\"commandName\":\"Project\",\"applicationUrl\":\"http://localhost:5010\"}}}");
            }
            foreach (string relative in new[] { "accesscontrol.yaml", "accesscontrol.works.yaml", "accesscontrol.eventstore-admin.yaml", "accesscontrol.eventstore-operations.yaml", "resiliency/resiliency.yaml", "statestore.yaml", "pubsub.yaml", "configstore.yaml", "sentry.yaml", "works/subscription.work.events.yaml" })
            {
                string target = Path.Combine(AppHostDirectory, "DaprComponents", relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(sourceRoot, "DaprComponents", relative), target);
            }
        }

        internal IDistributedApplicationBuilder Builder()
        {
            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
            {
                Args = [], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name,
                ProjectDirectory = AppHostDirectory, DisableDashboard = true,
            });
            builder.Environment.EnvironmentName = Environments.Development;
            builder.Configuration["Platform:Works:SourceRoot"] = WorksRoot;
            builder.Configuration["Platform:Works:EventStoreSourceRoot"] = EventStoreRoot;
            builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(Root, "test-dcp-not-started");
            builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(Root, "test-dashboard-not-started");
            return builder;
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
