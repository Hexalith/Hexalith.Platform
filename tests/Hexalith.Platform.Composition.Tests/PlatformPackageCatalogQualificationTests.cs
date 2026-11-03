namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.EventStore.Contracts.Routing;
using Shouldly;

/// <summary>Tests finite package admission with real released declarations; these controls do not claim a live package run.</summary>
public sealed class PlatformPackageCatalogQualificationTests
{
    /// <summary>Measures actual loaded producer bytes and verifies the exact immutable image bindings or finite refusal.</summary>
    [Fact]
    public void LoadedPublicSdkBytesDetermineAdmissionAndImmutableTenantImages()
    {
        using PlatformQualificationPackageFixture fixture = new();
        IReadOnlyList<PlatformModuleDeclaration> declarations = PlatformDeclarationSet.Load(fixture.Options);
        IReadOnlyList<PlatformServerBinding> servers = PlatformServerBindings.Resolve(fixture.Options, declarations);
        declarations.Select(module => PlatformDeclarationSet.Id(module.Declaration)).Order(StringComparer.Ordinal).ShouldBe(["eventstore", "tenants"]);
        bool actualPublicBytes = Hash(typeof(RouteCatalogEnvelope).Assembly) == "5680933ce8003475a3c323ddd48f299be0eeac52810424814ca6e39bab763d53"
            && Hash(Assembly.Load("Hexalith.EventStore.ServiceDefaults")) == "860b843eb81e5e1b1e6176b8865e79a116b62182c28cf5a39230f00bfac2115d";
        PlatformPackageCatalogQualification.HasQualifiedSdkBytes().ShouldBe(actualPublicBytes);
        bool actualOwnerBytes = Hash(Assembly.Load("Hexalith.Tenants.Contracts")) == "5676f656752395e149296ffbe9bc9d5e5e699e4bd6d4265df7de0f3442f05a40";
        if (actualPublicBytes && actualOwnerBytes && OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            IReadOnlyList<PlatformServerBinding> admitted = PlatformPackageCatalogQualification.Bind(fixture.Options, declarations, servers, developmentRun: true);
            admitted.Single(server => server.ResourceId == "tenants").ImageDigest.ShouldBe("2f4ab155bc4d33e4b16f1bc8995c45de83503bd8ad99d2c7e46ebbef382b6c42");
            admitted.Single(server => server.ResourceId == "tenants-api").ImageDigest.ShouldBe("2969ad3f75b154566567dbd6a8f55dd8a64364cbec6fed96398e921379dde9b6");
            admitted.Single(server => server.ResourceId == "eventstore").ImageDigest.ShouldBeNull();
            servers.ShouldAllBe(server => server.ImageDigest == null);
        }
        else
        {
            Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.Bind(fixture.Options, declarations, servers, developmentRun: true));
        }
        Directory.Exists(Path.Combine(fixture.Root, "run")).ShouldBeFalse();
    }

    /// <summary>Changed authority, source, topology or package selection refuses before resource effects.</summary>
    /// <param name="fault">The finite input mutation.</param>
    /// <exception cref="ArgumentOutOfRangeException">The fixture fault is unknown.</exception>
    [Theory]
    [InlineData("no-proof")]
    [InlineData("drop-request")]
    [InlineData("owners")]
    [InlineData("mapping")]
    [InlineData("platform-source")]
    [InlineData("platform-root")]
    [InlineData("environment")]
    [InlineData("declaration-path")]
    [InlineData("active-module")]
    [InlineData("suite")]
    [InlineData("sdk-near-miss")]
    [InlineData("tenants-near-miss")]
    [InlineData("owner-snapshot")]
    [InlineData("foreign-image")]
    [InlineData("missing-server")]
    [InlineData("extra-server")]
    [InlineData("production")]
    public void ChangedScopeCohortOrAuthorityRefusesBeforeEffects(string fault)
    {
        using PlatformQualificationPackageFixture fixture = new();
        PlatformCompositionOptions options = fixture.Options;
        IReadOnlyList<PlatformModuleDeclaration> declarations = PlatformDeclarationSet.Load(options);
        IReadOnlyList<PlatformServerBinding> servers = PlatformServerBindings.Resolve(options, declarations);
        switch (fault)
        {
            case "no-proof": options = options with { QualificationOwnerProof = null }; break;
            case "drop-request": options = options with { RunPlan = fixture.Plan with { QualificationOwners = null, QualificationOwnersBinding = null } }; break;
            case "owners": options = options with { QualificationOwners = ["memories"] }; break;
            case "mapping": options = options with { Mapping = options.Mapping with { Entries = [new("Hexalith.Foreign", "package", null)] } }; break;
            case "platform-source": options = options with { Mapping = options.Mapping with { PlatformIdentity = options.Mapping.PlatformIdentity! with { Commit = new string('c', 40) } } }; break;
            case "platform-root": options = options with { PlatformRoot = Path.Combine(fixture.Root, "foreign") }; break;
            case "environment": options = options with { EnvironmentId = CompositionRunPlanFactory.NewRunId() }; break;
            case "declaration-path": options = options with { DeclarationPaths = [] }; break;
            case "active-module": options = options with { ActiveModule = "tenants" }; break;
            case "suite": options = options with { RunPlan = fixture.Plan with { SelectedTest = fixture.Plan.SelectedTest! with { Mode = "source" } } }; break;
            case "sdk-near-miss": ChangeCatalog(fixture.Root, "<HexalithEventStoreVersion>4.0.4", "<HexalithEventStoreVersion>4.0.5"); break;
            case "tenants-near-miss": ChangeCatalog(fixture.Root, "<HexalithTenantsVersion>7.0.0", "<HexalithTenantsVersion>7.0.1"); break;
            case "owner-snapshot":
                JsonNode changed = JsonNode.Parse(declarations[0].Declaration.GetRawText())!;
                changed["identity"]!["dependencies"] = new JsonArray("memories");
                declarations = [new PlatformModuleDeclaration(declarations[0].Source, JsonSerializer.SerializeToElement(changed)), .. declarations.Skip(1)];
                break;
            case "foreign-image": servers = servers.Select(server => server.ServerId == "domain" ? server with { Image = "registry.example/foreign:7.0.0" } : server).ToArray(); break;
            case "missing-server": servers = servers.Where(server => server.ServerId != "api").ToArray(); break;
            case "extra-server": servers = [.. servers, servers[0] with { ServerId = "foreign" }]; break;
            case "production": break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.Bind(options, declarations, servers, developmentRun: fault != "production"));
        Directory.Exists(Path.Combine(fixture.Root, "run")).ShouldBeFalse();
    }

    /// <summary>Serialized options cannot carry the private verified proof into ordinary composition.</summary>
    /// <param name="stripped">Whether the serialized binding is also removed.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryComposeCannotUseSerializedRequestOrProofFields(bool stripped)
    {
        using PlatformQualificationPackageFixture fixture = new();
        int probes = 0;
        string serialized = JsonSerializer.Serialize(fixture.Options);
        serialized.Contains("QualificationOwnerProof", StringComparison.Ordinal).ShouldBeFalse();
        PlatformCompositionOptions ordinary = JsonSerializer.Deserialize<PlatformCompositionOptions>(serialized).ShouldNotBeNull();
        ordinary.QualificationOwnerProof.ShouldBeNull();
        PlatformCompositionOptions options = ordinary with
        {
            QualificationOwnerProof = null,
            RunPlan = stripped ? fixture.Plan with { QualificationOwnersBinding = null } : fixture.Plan,
            ImageProbeRunner = (_, _, _) => { probes++; throw new InvalidOperationException("No image probe is permitted before admission."); },
        };
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = ["--environment=Development"], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true, AllowUnsecuredTransport = true });
        Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options)).Message.ShouldContain("owner SDK");
        probes.ShouldBe(0);
        builder.Resources.ShouldBeEmpty();
        Directory.Exists(Path.Combine(fixture.Root, "run")).ShouldBeFalse();
    }

    /// <summary>A valid request MAC does not qualify an unsupported additional owner set.</summary>
    /// <param name="owner">The authenticated but unsupported additional owner.</param>
    [Theory]
    [InlineData("memories")]
    [InlineData("eventstore")]
    public void AuthenticatedButUnqualifiedAdditionalOwnersRemainRefused(string owner)
    {
        using PlatformQualificationPackageFixture fixture = new();
        string key = CompositionSigningKey.Create();
        CompositionRunPlan plan = CompositionQualificationOwners.Bind(fixture.Plan with { QualificationOwners = [owner], QualificationOwnersBinding = null }, key);
        PlatformCompositionOptions options = fixture.Options with { QualificationOwners = [owner], RunPlan = plan, QualificationOwnerProof = PlatformQualificationOwnerProof.Create(plan, key) };
        IReadOnlyList<PlatformModuleDeclaration> declarations = PlatformDeclarationSet.Load(fixture.Options);
        IReadOnlyList<PlatformServerBinding> servers = PlatformServerBindings.Resolve(fixture.Options, declarations);
        options.QualificationOwnerProof.Matches(options).ShouldBeTrue();
        Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.Bind(options, declarations, servers, developmentRun: true));
        Directory.Exists(Path.Combine(fixture.Root, "run")).ShouldBeFalse();
    }

    /// <summary>The proof rechecks mutable mapping contents and the issued plan metadata.</summary>
    [Fact]
    public void OpaqueProofRejectsMutatedMappingCollectionsAndSerializedPlanCopies()
    {
        using PlatformQualificationPackageFixture fixture = new();
        fixture.Options.QualificationOwnerProof!.Matches(fixture.Options).ShouldBeTrue();
        PlatformCompositionOptions changed = fixture.Options with { RunPlan = fixture.Plan with { AttemptId = CompositionRunPlanFactory.NewRunId() } };
        fixture.Options.QualificationOwnerProof.Matches(changed).ShouldBeFalse();
        SourceMappingEntry[] entries = (SourceMappingEntry[])fixture.Options.Mapping.Entries;
        entries[0] = entries[0] with { Identity = "Hexalith.Foreign" };
        fixture.Options.QualificationOwnerProof.Matches(fixture.Options).ShouldBeFalse();
    }

    private static string Hash(Assembly assembly) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location)));

    private static void ChangeCatalog(string root, string before, string after)
    {
        string path = Path.Combine(root, "references", "Hexalith.Builds", "Props", "Directory.Packages.props");
        File.WriteAllText(path, File.ReadAllText(path).Replace(before, after, StringComparison.Ordinal));
    }
}
