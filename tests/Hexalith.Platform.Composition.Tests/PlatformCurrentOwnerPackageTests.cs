namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
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
            ("domain", "tenants", "1ba6ff9dd0623817d667bd46229dd0ba34a23ffc51d48c889d3e67f474bec22a"),
            ("api", "tenants-api", "f4707de77c595977d5b7913b4e6119aed407da3c72475ba13b46e9e302d1ef7b"),
        })
        {
            var actual = servers.Single(server => PlatformDeclarationSet.Id(server.Module.Declaration) == "tenants" && server.ServerId == expected.Item1);
            actual.Image.ShouldBe("registry.hexalith.com/" + expected.Item2 + ":7.0.5");
            actual.ImageDigest.ShouldBe(expected.Item3);
            foreach (string staleVersion in new[] { "7.0.0", "7.0.2", "7.0.4" })
            {
                var stale = unbound.Select(server => PlatformDeclarationSet.Id(server.Module.Declaration) == "tenants" && server.ServerId == expected.Item1
                    ? server with { Image = "registry.hexalith.com/" + expected.Item2 + ":" + staleVersion, ImageDigest = null } : server).ToArray();
                Should.Throw<InvalidDataException>(() => PlatformPartiesPackageCatalogQualification.Bind(options, declarations, stale, developmentRun: true));
                proof.Matches(options).ShouldBeTrue("Stale Tenants images cannot change the same measured private owner authority.");
            }
        }
        foreach (var expected in new[]
        {
            ("server", "memories", "a857a0865ff3781027cc392a7d36431fd13db1e7a3522690a8ef05df7036af3a"),
            ("domain", "memories-domain-service", "90d15d551cb1ada3aafbcab823cbd8e83304605ca383abdbd47573613304e820"),
            ("clock", "memories-access-telemetry-clock", "e4edd4f62a43fc404f40eac2fd107c17320420b38bbd6514f31f23b19b019d3e"),
            ("telemetry", "memories-access-telemetry", "d227044d62db58b87c26f99a2ea6264b148ffd0c8d15c452b3bf4b30dd244e0b"),
        })
        {
            var actual = servers.Single(server => PlatformDeclarationSet.Id(server.Module.Declaration) == "memories" && server.ServerId == expected.Item1);
            actual.Image.ShouldBe("registry.hexalith.com/" + expected.Item2 + ":3.0.25");
            actual.ImageDigest.ShouldBe(expected.Item3);
            foreach (string staleVersion in new[] { "3.0.23", "3.0.24" })
            {
                var stale = unbound.Select(server => PlatformDeclarationSet.Id(server.Module.Declaration) == "memories" && server.ServerId == expected.Item1
                    ? server with { Image = "registry.hexalith.com/" + expected.Item2 + ":" + staleVersion, ImageDigest = null } : server).ToArray();
                Should.Throw<InvalidDataException>(() => PlatformPartiesPackageCatalogQualification.Bind(options, declarations, stale, developmentRun: true));
            }
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
        byte[] canonical = File.ReadAllBytes(catalog.CatalogPath);
        byte[] retainedReceipt = File.ReadAllBytes(Path.Combine(catalog.Directory, "receipt.json"));
        byte[] firstReaderReceipt = File.ReadAllBytes(Path.Combine(catalog.Directory, "current-reader-receipt.json"));
        PlatformCompositionOptions replacement = Capture(fixture);
        PlatformCurrentOwnerProof replacementProof = replacement.CurrentOwnerProof!;
        replacementProof.Bodies["Hexalith.Parties.Contracts"].ShouldNotBe(proof.Bodies["Hexalith.Parties.Contracts"],
            "The real second private Release preparation must supply changed measured bodies for this reuse regression.");
        replacementProof.Matches(replacement).ShouldBeTrue();
        var replacementServers = PlatformPartiesPackageCatalogQualification.Bind(replacement,
            PlatformDeclarationSet.Load(replacement), PlatformAppModel.Prepare(replacement), developmentRun: true);
        PlatformRoutingCatalog repeated = PlatformRoutingCatalog.Generate(replacement, replacementServers, Path.Combine(fixture.Workspace, "platform-components"));
        File.ReadAllBytes(repeated.CatalogPath).ShouldBe(canonical, "Equal canonical catalog bytes retain the original immutable target.");
        File.ReadAllBytes(Path.Combine(repeated.Directory, "receipt.json")).ShouldBe(retainedReceipt,
            "The retained original catalog receipt must not be rewritten as current private authority.");
        byte[] freshReaderReceipt = File.ReadAllBytes(Path.Combine(repeated.Directory, "current-reader-receipt.json"));
        freshReaderReceipt.SequenceEqual(firstReaderReceipt).ShouldBeFalse("The current receipt must describe the actual second reader, even when canonical bytes are equal.");
        using (JsonDocument freshReceipt = JsonDocument.Parse(freshReaderReceipt))
        {
            replacementProof.VerifyReader(freshReceipt.RootElement);
            Should.Throw<InvalidDataException>(() => proof.VerifyReader(freshReceipt.RootElement));
        }
        replacementProof.Matches(replacement).ShouldBeTrue("Repeated generation preserves the second measured proof without repair or reissuance.");
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
