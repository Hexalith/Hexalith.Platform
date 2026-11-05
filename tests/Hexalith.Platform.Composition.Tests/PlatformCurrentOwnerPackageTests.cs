namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Platform.CatalogArtifacts;
using Shouldly;
using Xunit;

/// <summary>Real clean current owner, public dependencies and private Release artifacts; no service acceptance is simulated.</summary>
public sealed class PlatformCurrentOwnerPackageTests
{
    [Fact]
    public async Task CurrentOwnerBuildAndReaderUseIdenticalMeasuredReleaseContractsAsync()
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(PlatformCurrentOwnerProof.Profile).ConfigureAwait(true);
        PlatformCompositionOptions options;
        int prepared = 0;
        Action? previousObserver = PlatformCurrentOwnerProof.PreparedObserver.Value;
        try
        {
            PlatformCurrentOwnerProof.PreparedObserver.Value = () => prepared++;
            options = Capture(fixture);
        }
        finally { PlatformCurrentOwnerProof.PreparedObserver.Value = previousObserver; }
        prepared.ShouldBe(1, "The actual build/pack/final-reader producer must invoke the tested final revision guard before issuance.");
        PlatformCurrentOwnerProof proof = options.CurrentOwnerProof!;
        proof.SourceRevision.ShouldBe(PlatformPartiesProofFixture.Git(options.Mapping.Entries.Single(entry => entry.Identity == "Hexalith.Parties").Path!, "rev-parse", "HEAD"));
        proof.Matches(options).ShouldBeTrue();
        options.PartiesRunProof.ShouldBeNull();
        PlatformPackageCatalogQualification.SelectCohort(options, fullParties: true).ShouldBeNull("The selected new tuple is not an admitted full public Parties cohort.");
        options.QualificationOwnerProof.ShouldBeNull();
        options.Mapping.Mode.ShouldBe(Hexalith.Builds.Tooling.Workspace.WorkspaceMode.Package);
        options.Mapping.Entries.Where(entry => entry.Identity is not ("Hexalith.Parties" or "Hexalith.Platform"))
            .ShouldAllBe(entry => entry.Origin == "package" && entry.Path == null);
        var declarations = PlatformDeclarationSet.Load(options);
        var unbound = PlatformAppModel.Prepare(options);
        var servers = PlatformPartiesPackageCatalogQualification.Bind(options, declarations, unbound, developmentRun: true);
        foreach (var expected in new[]
        {
            ("server", "memories", "7cbba1867709359bf7a4c5f115e8879bfbb9548cdf209947dc0f8c633ad859d7"),
            ("domain", "memories-domain-service", "efbd5e1d37e8ac75c78855ae4f22664ea294b8f6e1a65e4a8c6f44c1efb5eacb"),
            ("clock", "memories-access-telemetry-clock", "7da6f0849a1994513e126f3cbea50754c414be563d499850830e34c10ac411ef"),
            ("telemetry", "memories-access-telemetry", "7c4f498dc7f7f0a2ab965efd05f76853f6f42a1b2513e3e0d94eeea96971620a"),
        })
        {
            var actual = servers.Single(server => PlatformDeclarationSet.Id(server.Module.Declaration) == "memories" && server.ServerId == expected.Item1);
            actual.Image.ShouldBe("registry.hexalith.com/" + expected.Item2 + ":3.0.24");
            actual.ImageDigest.ShouldBe(expected.Item3);
            var stale = unbound.Select(server => PlatformDeclarationSet.Id(server.Module.Declaration) == "memories" && server.ServerId == expected.Item1
                ? server with { Image = "registry.hexalith.com/" + expected.Item2 + ":3.0.23", ImageDigest = null } : server).ToArray();
            Should.Throw<InvalidDataException>(() => PlatformPartiesPackageCatalogQualification.Bind(options, declarations, stale, developmentRun: true));
            proof.Matches(options).ShouldBeTrue("A stale reference binding must be refused without changing the actual measured owner authority.");
        }
        servers.Single(server => server.ResourceId == "parties").ProjectPath.ShouldBe(PlatformCurrentOwnerProof.OwnerProject(options));
        servers.Single(server => server.ResourceId == "eventstore").ProjectPath.ShouldBe(fixture.Seed);
        servers.Where(server => server.Image is not null).Count().ShouldBe(6);
        servers.Where(server => server.Image is not null).ShouldAllBe(server => server.ImageDigest != null && server.ImageDigest.Length == 64 && server.ProjectPath == null);
        PlatformRoutingCatalog catalog = PlatformRoutingCatalog.Generate(options, servers, Path.Combine(fixture.Workspace, "platform-components"));
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.Directory, "current-reader-receipt.json")));
        proof.VerifyReader(receipt.RootElement);
        proof.Matches(options).ShouldBeTrue("Legitimate reader evaluation must not invalidate or repair the captured snapshot.");
        using JsonDocument request = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Reader, "request.json")));
        request.RootElement.GetProperty("qualificationCohort").GetString().ShouldBe(PublishedCatalogArtifactCohort.CurrentOwnerName);
        foreach (string name in new[] { "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions" })
        {
            string body = Path.Combine(fixture.Reader, "output", name + ".dll");
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(body))).ShouldBe(proof.Bodies[name]);
        }
        Directory.GetFiles(Path.Combine(proof.Directory, "feed"), "*.nupkg").Length.ShouldBe(2);
        File.Exists(Path.Combine(proof.Artifacts, "bin", "Hexalith.Parties", "release", "Hexalith.Parties.dll")).ShouldBeTrue();
        string snapshot = Path.Combine(fixture.Workspace, "source-mapping", "catalog-versions.json");
        byte[] enriched = File.ReadAllBytes(snapshot);
        byte[] previous = File.ReadAllBytes(catalog.CatalogPath);
        var builder = PlatformPartiesPackageFixture.Builder();
        Should.Throw<InvalidOperationException>(() => fixture.Compose(builder, fresh =>
        {
            fresh.CurrentOwnerProof!.Matches(fresh).ShouldBeTrue();
            PlatformAppModel.MaterializeSourceMapping(fresh, fixture.Workspace);
            File.ReadAllBytes(snapshot).ShouldBe(enriched);
            File.AppendAllText(snapshot, " ");
            Should.Throw<InvalidDataException>(() => PlatformAppModel.MaterializeSourceMapping(fresh, fixture.Workspace));
            fresh.CurrentOwnerProof.Matches(fresh).ShouldBeFalse();
            throw new InvalidOperationException("Expected fresh current proof and exact enriched snapshot; stop before effects.");
        })).Message.ShouldBe("Expected fresh current proof and exact enriched snapshot; stop before effects.");
        builder.Resources.ShouldBeEmpty();
        File.ReadAllBytes(catalog.CatalogPath).ShouldBe(previous);
    }

    [Fact]
    public async Task CurrentMeasuredAuthorityRefusesMutationsBeforeCompositionEffectsAsync()
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(PlatformCurrentOwnerProof.Profile).ConfigureAwait(true);
        PlatformCompositionOptions accepted = Capture(fixture);
        PlatformCurrentOwnerProof proof = accepted.CurrentOwnerProof!;
        foreach (string fault in new[] { "archive", "runtime-body", "snapshot", "project-override", "dependency-source", "owner-source", "owner-missing", "reference-image" })
        {
            PlatformCompositionOptions options = accepted;
            string? path = fault switch
            {
                "archive" => Directory.GetFiles(Path.Combine(proof.Directory, "feed"), "*.nupkg").First(),
                "runtime-body" => Path.Combine(proof.Artifacts, "bin", "Hexalith.Parties", "release", "Hexalith.Parties.dll"),
                "snapshot" => Path.Combine(fixture.Workspace, "source-mapping", "catalog-versions.json"),
                _ => null,
            };
            byte[]? original = path is null ? null : File.ReadAllBytes(path);
            try
            {
                if (path is not null) File.AppendAllText(path, "changed");
                if (fault == "project-override") options = options with { Projects = new Dictionary<string, string>(PlatformServerBindings.ReferenceProjects) { ["parties/domain"] = "foreign.csproj" } };
                if (fault == "dependency-source") options = options with { Mapping = options.Mapping with { Entries = options.Mapping.Entries.Select(entry => entry.Identity == "Hexalith.EventStore" ? entry with { Origin = "source", Path = options.PlatformRoot } : entry).ToArray() } };
                if (fault == "owner-source") options = options with { Mapping = options.Mapping with { Entries = options.Mapping.Entries.Select(entry => entry.Identity == "Hexalith.Parties" ? entry with { Origin = "package", Path = null } : entry).ToArray() } };
                if (fault == "owner-missing") options = options with { Mapping = options.Mapping with { Entries = options.Mapping.Entries.Where(entry => entry.Identity != "Hexalith.Parties").ToArray() } };
                if (fault == "reference-image") options = options with { Images = new Dictionary<string, string>(PlatformServerBindings.ReferenceImages) { ["tenants/domain"] = "registry.invalid/tenants" } };
                proof.Matches(options).ShouldBeFalse(fault);
                var builder = PlatformPartiesPackageFixture.Builder();
                Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options));
                builder.Resources.ShouldBeEmpty();
            }
            finally { if (path is not null) File.WriteAllBytes(path, original!); }
            proof.Matches(accepted).ShouldBeTrue("The private fixture restores original bytes without minting a new proof.");
        }
    }

    private static PlatformCompositionOptions Capture(PlatformPartiesPackageFixture fixture)
    {
        return fixture.CaptureCurrent();
    }
}
