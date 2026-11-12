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
        using PlatformQualificationPackageFixture fixture = PlatformQualificationPackageFixture.HistoricalPublic();
        PlatformServerBindings.Version(fixture.Options, "tenants", "historical public fixture").ShouldBe("7.0.2");
        PlatformServerBindings.Version(fixture.Options, "memories", "historical public fixture").ShouldBe("3.0.23");
        using PlatformQualificationPackageFixture current = PlatformQualificationPackageFixture.CurrentTest();
        string currentProvenance = Path.Combine(current.Root, "src", "Hexalith.Platform.Composition", "Declarations", "provenance.json");
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(currentProvenance)))
            .ShouldBe("3931a6ff47adf0edd4b6d19430294f6ebed2c334b50655528e751eee049fc846");
        PlatformServerBindings.Version(current.Options, "eventstore", "retained private fixture").ShouldBe("4.1.0");
        PlatformServerBindings.Version(current.Options, "tenants", "retained private fixture").ShouldBe("7.0.5");
        PlatformServerBindings.Version(current.Options, "memories", "retained private fixture").ShouldBe("3.0.25");
        IReadOnlyList<PlatformModuleDeclaration> declarations = PlatformDeclarationSet.Load(fixture.Options);
        IReadOnlyList<PlatformServerBinding> servers = PlatformServerBindings.Resolve(fixture.Options, declarations);
        declarations.Select(module => PlatformDeclarationSet.Id(module.Declaration)).Order(StringComparer.Ordinal).ShouldBe(["eventstore", "tenants"]);
        bool actualPublicBytes = Hash(typeof(RouteCatalogEnvelope).Assembly) == "f3f2d7f6e7edf54e187af15edec8b4f13bf7e255b186b999efa0836a0c86a739"
            && Hash(Assembly.Load("Hexalith.EventStore.ServiceDefaults")) == "0e3f88ffefb12327d53f865b96bbb774beb71f7c78c9f20a3cbefa4901e9ba2d";
        PlatformPackageCatalogQualification.HasQualifiedSdkBytes().ShouldBe(actualPublicBytes);
        bool actualOwnerBytes = Hash(Assembly.Load("Hexalith.Tenants.Contracts")) == "4f07e5529c43f60859c4372fa718dd339c4e18dd0b81db301abe0e4a4e665df9";
        if (actualPublicBytes && actualOwnerBytes && OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            IReadOnlyList<PlatformServerBinding> admitted = PlatformPackageCatalogQualification.Bind(fixture.Options, declarations, servers, developmentRun: true);
            admitted.Single(server => server.ResourceId == "tenants").ImageDigest.ShouldBe("adfcad2eea7c0b59e2936a62131c7925af518f1331a4e9fec0071da55874caed");
            admitted.Single(server => server.ResourceId == "tenants-api").ImageDigest.ShouldBe("2922e0b1eb7b115bac472479213ca46d84c26ac4917b1d53854034618179c2e4");
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
        using PlatformQualificationPackageFixture fixture = PlatformQualificationPackageFixture.HistoricalPublic();
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
            case "sdk-near-miss": ChangeCatalog(fixture.Root, "<HexalithEventStoreVersion>4.1.0", "<HexalithEventStoreVersion>4.1.1"); break;
            case "tenants-near-miss":
                string tenantsVersion = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture.Root, "src", "Hexalith.Platform.Composition", "Declarations", "provenance.json")))!["tenants"]!["version"]!.GetValue<string>();
                ChangeCatalog(fixture.Root, $"<HexalithTenantsVersion>{tenantsVersion}</HexalithTenantsVersion>", $"<HexalithTenantsVersion>{tenantsVersion}0</HexalithTenantsVersion>");
                break;
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
        using PlatformQualificationPackageFixture fixture = PlatformQualificationPackageFixture.HistoricalPublic();
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
        using PlatformQualificationPackageFixture fixture = PlatformQualificationPackageFixture.HistoricalPublic();
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
        using PlatformQualificationPackageFixture fixture = PlatformQualificationPackageFixture.HistoricalPublic();
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
        string original = File.ReadAllText(path);
        original.ShouldContain(before);
        File.WriteAllText(path, original.Replace(before, after, StringComparison.Ordinal));
    }
}
