namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json.Nodes;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

/// <summary>Composition-only synthetic enrollment evidence; ordinary CLI and real-owner live qualification remain required.</summary>
[Collection("Platform file entry")]
public sealed class DomainEnrollmentBindingTests
{
    private static string Root
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "apphost.cs"))) directory = directory.Parent;
            return directory!.FullName;
        }
    }

    [Fact]
    public void StaticValidityRequiresExplicitProjectAndScopedConfigurationBeforeComposition()
    {
        using PlatformOwnerFixture owner = PlatformOwnerFixture.CreateSample(Root, "enrollmentprobe");
        PlatformManifestValidationResult validation = PlatformManifestValidator.Validate([owner.Manifest], TestContext.Current.CancellationToken);
        validation.IsValid.ShouldBeTrue(string.Join("; ", validation.Diagnostics.Select(diagnostic => diagnostic.Message)));
        PlatformServerBindings.ReferenceProjects.ShouldNotContainKey("enrollmentprobe/api");
        SourceMappingEntry[] entries = new[] { "EventStore", "Tenants", "Memories" }
            .Select(name => new SourceMappingEntry("Hexalith." + name, "source", Path.Combine(Root, "references", "Hexalith." + name)))
            .Append(new SourceMappingEntry("Hexalith.Enrollmentprobe", "source", owner.Root)).ToArray();
        PlatformCompositionOptions options = new()
        {
            PlatformRoot = Root, Mapping = new SourceMapping(WorkspaceMode.Source, owner.Root, "Hexalith.Enrollmentprobe", entries, false),
            DeclarationPaths = [owner.Manifest], ActiveModule = "enrollmentprobe",
        };
        IDistributedApplicationBuilder unregistered = Builder();
        InvalidDataException missingProject = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(unregistered, options));
        missingProject.Message.ShouldContain("enrollmentprobe/api: project");
        unregistered.Resources.ShouldBeEmpty();

        options = options with { Projects = new Dictionary<string, string>(options.Projects) { ["enrollmentprobe/api"] = "server.csproj" } };
        IDistributedApplicationBuilder unconfigured = Builder();
        Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(unconfigured, options)).Message.ShouldContain("enrollmentprobe/api/Sample:Setting");
        unconfigured.Resources.ShouldBeEmpty();

        options = options with { Configuration = new Dictionary<string, string> { ["enrollmentprobe/api/Sample:Setting"] = "owner-scoped-value" } };
        IDistributedApplicationBuilder configured = Builder();
        PlatformAppModel.Compose(configured, options);
        using ServiceProvider cleanup = configured.Services.BuildServiceProvider();
        _ = cleanup.GetRequiredService<PlatformCompositionWorkspace>();
        PlatformServerBinding bound = configured.Resources.OfType<ProjectResource>().Single(project => project.Name == "enrollmentprobe-api-host").Annotations.OfType<PlatformServerAnnotation>().Single().Binding;
        bound.ProjectPath.ShouldBe(Path.Combine(owner.Root, "server.csproj"));
        bound.Environment["Sample__Setting"].ShouldBe("owner-scoped-value");
        VerifyRoutingPreservationAndFiltering(owner.Root, PlatformAppModel.Prepare(options));
    }

    private static void VerifyRoutingPreservationAndFiltering(string directory, IReadOnlyList<PlatformServerBinding> servers)
    {
        JsonObject selected = new()
        {
            ["TenantId"] = "tenant-a", ["Domain"] = "orders", ["Version"] = "v2",
            ["AppId"] = "enrollmentprobe-api", ["MethodName"] = "process-custom",
        };
        JsonObject settings = new()
        {
            ["PreservedSetting"] = "owner-default",
            ["EventStore"] = new JsonObject
            {
                ["DomainServices"] = new JsonObject
                {
                    ["Registrations"] = new JsonObject
                    {
                        ["approved-tuple"] = selected.DeepClone(),
                        ["unselected-owner"] = new JsonObject { ["AppId"] = "unselected", ["Domain"] = "other", ["TenantId"] = "*", ["Version"] = "v1", ["MethodName"] = "process" },
                    },
                },
            },
        };
        string project = Path.Combine(directory, "gateway.csproj");
        string source = Path.Combine(directory, "appsettings.Development.json");
        File.WriteAllText(source, settings.ToJsonString());
        string contentRoot = PlatformGatewayConfiguration.Create(project, Path.Combine(directory, "selected"), servers);
        JsonNode rendered = JsonNode.Parse(File.ReadAllText(Path.Combine(contentRoot, "appsettings.Development.json")))!;
        rendered["PreservedSetting"]!.GetValue<string>().ShouldBe("owner-default");
        JsonObject registrations = rendered["EventStore"]!["DomainServices"]!["Registrations"]!.AsObject();
        registrations.Count.ShouldBe(1);
        JsonNode.DeepEquals(registrations["approved-tuple"], selected).ShouldBeTrue("the reviewed tenant/domain/version/appId/method tuple must survive unchanged");
        File.ReadAllText(source).ShouldBe(settings.ToJsonString());
        string filtered = PlatformGatewayConfiguration.Create(project, Path.Combine(directory, "unselected"), servers.Where(server => server.AppId != "enrollmentprobe-api").ToArray());
        JsonNode.Parse(File.ReadAllText(Path.Combine(filtered, "appsettings.Development.json")))!["EventStore"]!["DomainServices"]!["Registrations"]!.AsObject().ShouldBeEmpty();
    }

    private static IDistributedApplicationBuilder Builder() => DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = [], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true, AllowUnsecuredTransport = true });
}
