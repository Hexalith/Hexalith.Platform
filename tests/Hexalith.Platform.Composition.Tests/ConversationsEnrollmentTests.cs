namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using Aspire.Hosting;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

/// <summary>Verifies ordinary Conversations source enrollment without executing a live dependency seam.</summary>
[Collection("Platform file entry")]
public sealed partial class ConversationsEnrollmentTests
{
    private static string Root => PlatformTestRepository.Root;

    /// <summary>Provides every explicit input from the real owner declaration.</summary>
    public static IEnumerable<object[]> RequiredConfigurationKeys() => Configuration().Keys.Select(key => new object[] { key });

    /// <summary>Retains the real server, app, resource, readiness and dependency identities with explicit settings.</summary>
    [Fact]
    public void RealDeclarationResolvesOnlyItsOrdinaryServerWithCompleteExplicitConfiguration()
    {
        PlatformCompositionOptions options = Options();
        PlatformManifestValidationResult validation = PlatformManifestValidator.Validate(options.DeclarationPaths, TestContext.Current.CancellationToken);
        validation.IsValid.ShouldBeTrue(string.Join("; ", validation.Diagnostics.Select(diagnostic => diagnostic.Message)));
        JsonElement declaration = validation.Declarations!.Single().Declaration;
        declaration.GetProperty("identity").GetProperty("dependencies").EnumerateArray().Select(value => value.GetString())
            .ShouldBe(["eventstore", "tenants", "memories"]);
        declaration.GetProperty("runtime").GetProperty("extensions").EnumerateArray()
            .SelectMany(extension => extension.GetProperty("configurationInputs").EnumerateArray())
            .Select(input => "conversations/domain/" + input.GetProperty("key").GetString()).Order()
            .ShouldBe(options.Configuration.Keys.Order());

        IReadOnlyList<PlatformServerBinding> servers = PlatformAppModel.Prepare(options);
        servers.Select(server => server.ResourceId).Order().ShouldBe(new[]
        {
            "eventstore", "tenants", "tenants-api", "memories", "memories-domain-service",
            "memories-access-telemetry-clock", "memories-access-telemetry", "conversations",
        }.Order());
        PlatformServerBinding conversation = servers.Single(server => PlatformDeclarationSet.Id(server.Module.Declaration) == "conversations");
        conversation.ServerId.ShouldBe("domain");
        conversation.AppId.ShouldBe("conversation");
        conversation.ResourceId.ShouldBe("conversations");
        conversation.ProjectPath.ShouldBe(Path.Combine(options.Mapping.Entries.Single(entry => entry.Identity == "Hexalith.Conversations").Path!,
            "src/Hexalith.Conversations.Server/Hexalith.Conversations.Server.csproj"));
        conversation.Image.ShouldBeNull();
        conversation.ReadinessEndpoint.ShouldBe("/ready");
        conversation.Tasks.ShouldBeEmpty();
        conversation.Environment["EventStore__DomainService__AppId"].ShouldBe("conversation");
        conversation.Environment["Authentication__JwtBearer__ValidAudiences__0"].ShouldBe("conversation");
        conversation.Environment["Authentication__Workload__AllowedCallers__0"].ShouldBe("eventstore");
        conversation.Environment["Authentication__WorkloadJwtBearer__AllowedAlgorithms__0"].ShouldBe("RS256");
        conversation.Environment["APP_API_TOKEN"].ShouldBe("composition-only-channel-token");
        conversation.Environment["EventStore__Authentication__ClientSecret"].ShouldBe("composition-only-worker-secret");
        conversation.Environment["EventStore__CommandStatus__StateStoreName"].ShouldBe("statestore");
        conversation.Environment["Dapr__DeclarativeSubscriptions"].ShouldBe("true");
        servers.Select(server => server.ResourceId).ShouldNotContain("receiver");
    }

    /// <summary>Enrollment does not invent the missing Conversations-owned compiled routing adapter.</summary>
    [Fact]
    public void OrdinaryCompositionRefusesTheAbsentOwnerCatalogAdapterBeforeAddingResources()
    {
        using PlatformOwnerFixture owner = PlatformOwnerFixture.Copy(Root, "conversations");
        string contracts = Path.Combine(owner.Root, "src", "Hexalith.Conversations.Contracts");
        Directory.CreateDirectory(contracts);
        File.Copy(Path.Combine(PlatformTestRepository.ReferenceRoot(Root, "Hexalith.Conversations"), "src",
            "Hexalith.Conversations.Contracts", "Hexalith.Conversations.Contracts.csproj"),
            Path.Combine(contracts, "Hexalith.Conversations.Contracts.csproj"));
        // The isolated copy deliberately has no adapter, independently of later owner deliveries.
        Directory.Exists(Path.Combine(owner.Root, "src", "Hexalith.Conversations.EventStoreExtensions")).ShouldBeFalse();
        RefusesBeforeComposition(Options(owner), "conversations: Contracts project Hexalith.Conversations.EventStoreExtensions is absent or outside its mapped owner");
    }

    /// <summary>Rejects absent and non-source owner enrollment before any resource is added.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingOrNonSourceOwnerMappingRefusesBeforeComposition(bool missing)
    {
        PlatformCompositionOptions options = Options();
        SourceMappingEntry[] entries = options.Mapping.Entries
            .Where(entry => entry.Identity != "Hexalith.Conversations" || !missing)
            .Select(entry => entry.Identity == "Hexalith.Conversations" ? entry with { Origin = "package" } : entry).ToArray();
        RefusesBeforeComposition(options with { Mapping = options.Mapping with { Entries = entries } }, "conversations/domain: project");
    }

    /// <summary>Rejects both missing project enrollment and a physically absent owner project.</summary>
    [Fact]
    public void MissingProjectBindingOrFileRefusesBeforeComposition()
    {
        PlatformCompositionOptions options = Options();
        Dictionary<string, string> projects = new(options.Projects);
        projects.Remove("conversations/domain").ShouldBeTrue();
        RefusesBeforeComposition(options with { Projects = projects }, "conversations/domain: project");
        using PlatformOwnerFixture owner = PlatformOwnerFixture.Copy(Root, "conversations");
        File.Delete(Path.Combine(owner.Root, PlatformServerBindings.ReferenceProjects["conversations/domain"]));
        RefusesBeforeComposition(Options(owner), "missing or physically outside");
    }

    /// <summary>A foreign owner cannot pass containment through relative traversal or a symbolic link.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PhysicallyForeignProjectRefusesBeforeComposition(bool symbolicLink)
    {
        using PlatformOwnerFixture owner = PlatformOwnerFixture.Copy(Root, "conversations");
        using PlatformOwnerFixture foreign = PlatformOwnerFixture.Copy(Root, "conversations");
        string project = PlatformServerBindings.ReferenceProjects["conversations/domain"];
        string relative;
        if (symbolicLink)
        {
            Directory.CreateSymbolicLink(Path.Combine(owner.Root, "foreign"), foreign.Root);
            relative = Path.Combine("foreign", project);
        }
        else relative = Path.GetRelativePath(owner.Root, Path.Combine(foreign.Root, project));
        PlatformCompositionOptions options = Options(owner);
        Dictionary<string, string> projects = new(options.Projects) { ["conversations/domain"] = relative };
        RefusesBeforeComposition(options with { Projects = projects }, "physically outside its owner root");
    }

    /// <summary>Every declared setting remains mandatory; the enrollment adds no implicit local defaults.</summary>
    [Theory]
    [MemberData(nameof(RequiredConfigurationKeys))]
    public void EveryMissingDeclaredSettingRefusesBeforeComposition(string key)
    {
        PlatformCompositionOptions options = Options();
        Dictionary<string, string> configuration = new(options.Configuration);
        configuration.Remove(key).ShouldBeTrue();
        RefusesBeforeComposition(options with { Configuration = configuration }, key);
    }

    /// <summary>Foreign server scoping cannot supply a missing ordinary server input.</summary>
    [Fact]
    public void ForeignScopedConfigurationRefusesBeforeComposition()
    {
        PlatformCompositionOptions options = Options();
        Dictionary<string, string> configuration = new(options.Configuration);
        string key = "conversations/domain/EventStore:DomainService:AppId";
        configuration.Remove(key).ShouldBeTrue();
        configuration["conversations/receiver/EventStore:DomainService:AppId"] = "receiver";
        RefusesBeforeComposition(options with { Configuration = configuration }, key);
    }

    /// <summary>The ordinary project enrollment does not assert a released image or enroll a conformance receiver.</summary>
    [Fact]
    public void PackageImageAndConformanceReceiverRemainUnavailable()
    {
        PlatformCompositionOptions options = Options();
        PlatformManifestValidationResult validation = PlatformManifestValidator.Validate(options.DeclarationPaths, TestContext.Current.CancellationToken);
        validation.IsValid.ShouldBeTrue();
        PlatformServerBindings.ReferenceImages.ShouldNotContainKey("conversations/domain");
        PlatformServerBindings.ReferenceProjects.Keys.Where(key => key.StartsWith("conversations/", StringComparison.Ordinal))
            .ShouldBe(["conversations/domain"]);
        InvalidDataException failure = Should.Throw<InvalidDataException>(() => PlatformServerBindings.Resolve(
            options with { Mapping = options.Mapping with { Mode = WorkspaceMode.Package } }, validation.Declarations!));
        failure.Message.ShouldContain("conversations/domain: image: released image is unavailable");
    }

    private static PlatformCompositionOptions Options(PlatformOwnerFixture? owner = null)
    {
        SourceMappingEntry[] entries = new[] { "EventStore", "Tenants", "Memories", "Conversations" }
            .Select(name => new SourceMappingEntry("Hexalith." + name, "source",
                name == "Conversations" && owner is not null ? owner.Root : PlatformTestRepository.ReferenceRoot(Root, "Hexalith." + name))).ToArray();
        return new PlatformCompositionOptions
        {
            PlatformRoot = Root,
            Mapping = new SourceMapping(WorkspaceMode.Source, Root, "Hexalith.Platform", entries, false),
            ActiveModule = "conversations",
            DeclarationPaths = [owner?.Manifest ?? Path.Combine(PlatformTestRepository.ReferenceRoot(Root, "Hexalith.Conversations"), "module", "hexalith.module-manifest.v2.json")],
            Configuration = Configuration(),
        };
    }

    private static Dictionary<string, string> Configuration() => new(StringComparer.Ordinal)
    {
        ["conversations/domain/EventStore:DomainService:AppId"] = "conversation",
        ["conversations/domain/Authentication:JwtBearer:Authority"] = "https://issuer.example.invalid",
        ["conversations/domain/Authentication:JwtBearer:Issuer"] = "https://issuer.example.invalid",
        ["conversations/domain/Authentication:JwtBearer:Audience"] = "conversation",
        ["conversations/domain/Authentication:JwtBearer:ValidAudiences"] = "[\"conversation\"]",
        ["conversations/domain/Authentication:JwtBearer:AllowedAlgorithms"] = "[\"RS256\"]",
        ["conversations/domain/Authentication:JwtBearer:RequireHttpsMetadata"] = "true",
        ["conversations/domain/Authentication:JwtBearer:AllowInsecureSymmetricKey"] = "false",
        ["conversations/domain/Authentication:JwtBearer:SigningKey"] = "composition-only-signing-key",
        ["conversations/domain/Authentication:Workload:Audience"] = "conversation",
        ["conversations/domain/Authentication:Workload:AllowedCallers"] = "[\"eventstore\"]",
        ["conversations/domain/Authentication:Workload:MaximumLifetimeSeconds"] = "60",
        ["conversations/domain/APP_API_TOKEN"] = "composition-only-channel-token",
        ["conversations/domain/EventStore:DataProtection:PersistToStateStore"] = "true",
        ["conversations/domain/Tenants:TopicName"] = "system.tenants.events",
        ["conversations/domain/Authentication:WorkloadJwtBearer:Authority"] = "https://issuer.example.invalid",
        ["conversations/domain/Authentication:WorkloadJwtBearer:Issuer"] = "https://issuer.example.invalid",
        ["conversations/domain/Authentication:WorkloadJwtBearer:Audience"] = "conversation",
        ["conversations/domain/Authentication:WorkloadJwtBearer:ValidAudiences"] = "[\"conversation\"]",
        ["conversations/domain/Authentication:WorkloadJwtBearer:AllowedAlgorithms"] = "[\"RS256\"]",
        ["conversations/domain/Authentication:WorkloadJwtBearer:RequireHttpsMetadata"] = "true",
        ["conversations/domain/Dapr:DeclarativeSubscriptions"] = "true",
        ["conversations/domain/Hexalith:Conversations:TenantAccess:GatewayBaseAddress"] = "https://gateway.example.invalid",
        ["conversations/domain/EventStore:Authentication:Authority"] = "https://issuer.example.invalid",
        ["conversations/domain/EventStore:Authentication:TokenEndpoint"] = "https://issuer.example.invalid/token",
        ["conversations/domain/EventStore:Authentication:GrantType"] = "client_credentials",
        ["conversations/domain/EventStore:Authentication:ClientId"] = "composition-only-worker",
        ["conversations/domain/EventStore:Authentication:ClientSecret"] = "composition-only-worker-secret",
    };

    private static void RefusesBeforeComposition(PlatformCompositionOptions options, string expected)
    {
        IDistributedApplicationBuilder builder = Builder();
        Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options)).Message.ShouldContain(expected);
        builder.Resources.ShouldBeEmpty();
    }

    private static IDistributedApplicationBuilder Builder() => DistributedApplication.CreateBuilder(new DistributedApplicationOptions
    {
        Args = [], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true, AllowUnsecuredTransport = true,
    });
}
