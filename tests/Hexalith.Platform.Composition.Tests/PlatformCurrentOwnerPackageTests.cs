namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
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
        string preparedSnapshot = Path.Combine(fixture.Workspace, "source-mapping", "catalog-versions.json");
        byte[] preparedSnapshotBytes = File.ReadAllBytes(preparedSnapshot);
        using (JsonDocument preparedCatalog = JsonDocument.Parse(preparedSnapshotBytes))
            preparedCatalog.RootElement.GetProperty("ProjectVersions").TryGetProperty(Path.GetFileNameWithoutExtension(fixture.Seed), out _).ShouldBeTrue();
        // The real published seed's actual later validation must leave the issued proof and exact snapshot unchanged.
        var seedValidation = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet",
            ["msbuild", fixture.Seed, "-target:HexalithValidateCatalogBeforeRestore", "-nologo", "-m:1", "-nodeReuse:false"], options.PlatformRoot,
            SourceMappingMaterializer.Environment(options.Mapping, fixture.Workspace)),
            TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken).ConfigureAwait(true);
        seedValidation.ExitCode.ShouldBe(0, seedValidation.ErrorOutput);
        seedValidation.TimedOut.ShouldBeFalse();
        seedValidation.OutputTruncated.ShouldBeFalse();
        File.ReadAllBytes(preparedSnapshot).ShouldBe(preparedSnapshotBytes);
        proof.Matches(options).ShouldBeTrue("The actual published seed's legitimate validation must not invalidate or recapture the sealed snapshot.");
        options.PartiesRunProof!.Matches(options).ShouldBeTrue();
        options.QualificationOwnerProof.ShouldBeNull();
        options.Mapping.Mode.ShouldBe(Hexalith.Builds.Tooling.Workspace.WorkspaceMode.Package);
        options.Mapping.Entries.Where(entry => entry.Identity is not ("Hexalith.Parties" or "Hexalith.Platform"))
            .ShouldAllBe(entry => entry.Origin == "package" && entry.Path == null);
        var declarations = PlatformDeclarationSet.Load(options);
        var unbound = PlatformAppModel.Prepare(options);
        PlatformPartiesPackageFixture.AssertCatalogSelectedDependencies(
            PlatformPartiesPackageCatalogQualification.Bind(options, declarations, unbound, developmentRun: true));
        var servers = unbound;
        servers.Single(server => server.ResourceId == "parties").ProjectPath.ShouldBe(PlatformCurrentOwnerProof.OwnerProject(options));
        servers.Single(server => server.ResourceId == "eventstore").ProjectPath.ShouldBe(fixture.Seed);
        PlatformRoutingCatalog catalog = PlatformRoutingCatalog.Generate(options, servers, Path.Combine(fixture.Workspace, "platform-components"));
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.Directory, "current-reader-receipt.json")));
        proof.VerifyReader(receipt.RootElement);
        proof.Matches(options).ShouldBeTrue("Legitimate reader evaluation must not invalidate or repair the captured snapshot.");
        using JsonDocument request = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Reader, "request.json")));
        request.RootElement.GetProperty("qualificationCohort").GetString().ShouldBe(proof.Cohort);
        foreach (string name in new[] { "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions" })
        {
            string body = Path.Combine(fixture.Reader, "output", name + ".dll");
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(body))).ShouldBe(proof.Bodies[name]);
        }
        Directory.GetFiles(Path.Combine(proof.Directory, "feed"), "*.nupkg").Length.ShouldBe(2);
        File.Exists(Path.Combine(proof.Artifacts, "bin", "Hexalith.Parties", "release", "Hexalith.Parties.dll")).ShouldBeTrue();
        await PlatformReaderMutationControls.AssertAsync(fixture, catalog);
        byte[] canonical = File.ReadAllBytes(catalog.CatalogPath);
        byte[] retainedReceipt = File.ReadAllBytes(Path.Combine(catalog.Directory, "receipt.json"));
        byte[] firstReaderReceipt = File.ReadAllBytes(Path.Combine(catalog.Directory, "current-reader-receipt.json"));
        PlatformCompositionOptions replacement = Capture(fixture);
        PlatformCurrentOwnerProof replacementProof = replacement.CurrentOwnerProof!;
        replacementProof.Directory.ShouldNotBe(proof.Directory, "The second preparation must own independent measured artifacts.");
        bool sameBodies = replacementProof.Bodies.OrderBy(pair => pair.Key).SequenceEqual(proof.Bodies.OrderBy(pair => pair.Key));
        replacementProof.Matches(replacement).ShouldBeTrue();
        var replacementServers = PlatformAppModel.Prepare(replacement);
        PlatformRoutingCatalog repeated = PlatformRoutingCatalog.Generate(replacement, replacementServers, Path.Combine(fixture.Workspace, "platform-components"));
        File.ReadAllBytes(repeated.CatalogPath).ShouldBe(canonical, "Equal canonical catalog bytes retain the original immutable target.");
        File.ReadAllBytes(Path.Combine(repeated.Directory, "receipt.json")).ShouldBe(retainedReceipt,
            "The retained original catalog receipt must not be rewritten as current private authority.");
        byte[] freshReaderReceipt = File.ReadAllBytes(Path.Combine(repeated.Directory, "current-reader-receipt.json"));
        // Deterministic current builds may produce identical bodies and identical reader measurements.
        // Freshness is not publication or runtime authority; authenticate the measured bodies themselves.
        TestContext.Current.TestOutputHelper!.WriteLine("repeatedCurrentBodiesIdentical=" + sameBodies
            + "; readerMeasurementsIdentical=" + freshReaderReceipt.SequenceEqual(firstReaderReceipt));
        using (JsonDocument freshReceipt = JsonDocument.Parse(freshReaderReceipt))
        {
            replacementProof.VerifyReader(freshReceipt.RootElement);
            if (sameBodies) proof.VerifyReader(freshReceipt.RootElement);
            else Should.Throw<InvalidDataException>(() => proof.VerifyReader(freshReceipt.RootElement));
            JsonNode changed = JsonNode.Parse(freshReaderReceipt)!;
            changed["loadedArtifacts"]![0]!["sha256"] = new string('0', 64);
            using JsonDocument changedReceipt = JsonDocument.Parse(changed.ToJsonString());
            Should.Throw<InvalidDataException>(() => replacementProof.VerifyReader(changedReceipt.RootElement));
            Should.Throw<InvalidDataException>(() => proof.VerifyReader(changedReceipt.RootElement));
        }
        replacementProof.Matches(replacement).ShouldBeTrue("Repeated generation preserves the second measured proof without repair or reissuance.");
        string snapshot = Path.Combine(fixture.Workspace, "source-mapping", "catalog-versions.json");
        byte[] enriched = File.ReadAllBytes(snapshot);
        byte[] previous = File.ReadAllBytes(catalog.CatalogPath);
        replacementProof.Matches(replacement).ShouldBeTrue();
        PlatformAppModel.MaterializeSourceMapping(replacement, fixture.Workspace);
        File.ReadAllBytes(snapshot).ShouldBe(enriched);
        File.AppendAllText(snapshot, " ");
        Should.Throw<InvalidDataException>(() => PlatformAppModel.MaterializeSourceMapping(replacement, fixture.Workspace));
        replacementProof.Matches(replacement).ShouldBeFalse();
        File.ReadAllBytes(catalog.CatalogPath).ShouldBe(previous);
    }

    [Fact]
    public async Task CurrentMeasuredAuthorityRefusesMutationsBeforeCompositionEffectsAsync()
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(PlatformCurrentOwnerProof.Profile).ConfigureAwait(true);
        PlatformCompositionOptions accepted = Capture(fixture);
        PlatformCurrentOwnerProof proof = accepted.CurrentOwnerProof!;
        int probes = 0;
        foreach (string fault in new[] { "archive", "runtime-body", "snapshot", "project-override", "dependency-source", "owner-source", "owner-missing", "reference-image" })
        {
            PlatformCompositionOptions options = accepted with { ImageProbeRunner = (_, _, _) => { probes++; throw new InvalidOperationException("Unexpected image probe."); } };
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
                var refusal = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options));
                // Changed sealed owner bytes are not parent inputs; Compose itself must refuse before registry resolution.
                if (fault is "archive" or "runtime-body") refusal.Message.ShouldContain("authenticated and sealed Release owner", customMessage: fault);
                probes.ShouldBe(0, fault);
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
