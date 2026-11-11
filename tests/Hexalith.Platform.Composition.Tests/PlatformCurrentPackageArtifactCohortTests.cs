#if HEXALITH_PLATFORM_EXTENSION_SDK && !HEXALITH_PLATFORM_SOURCE_CATALOG
namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Platform.CatalogArtifacts;
using Hexalith.Builds.Tooling.Runtime;
using Shouldly;

/// <summary>Authenticates current signed public reader artifacts separately from all historical and native image authorities.</summary>
public sealed class PlatformCurrentPackageArtifactCohortTests
{
    private static readonly string[] _names = ["Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults", "Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService",
        "Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server", "Hexalith.Memories.Contracts", "Hexalith.Memories.EventStore", "Hexalith.Memories.EventStoreExtensions",
        "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions", "Hexalith.McpCli.Abstractions"];

    /// <summary>Every genuine current public body verifies under its separate exact reader authority.</summary>
    [Fact]
    public void CurrentPublicReaderAuthenticatesEveryActualBody()
    {
        var context = new AssemblyLoadContext("current-public-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            foreach (string name in _names)
            {
                string path = Body(name);
                Assembly body = context.LoadFromAssemblyPath(path);
                PublishedCatalogArtifactCohort.Verify(body, PublishedCatalogArtifactCohort.PublishedPartiesCurrentName).ShouldBe(path);
                if (name.StartsWith("Hexalith.Tenants.", StringComparison.Ordinal) || name.StartsWith("Hexalith.Memories.", StringComparison.Ordinal)
                    || name.StartsWith("Hexalith.Parties.", StringComparison.Ordinal))
                    Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(body, PublishedCatalogArtifactCohort.PublishedPartiesSdk420Name));
            }
        }
        finally { context.Unload(); }
    }

    /// <summary>Current private receipts bind all ten actual references and reject partial, duplicate or crossed receipts.</summary>
    [Fact]
    public void CurrentPrivateReceiptsAuthenticateExactReferences()
    {
        var references = _names.Where(name => !name.StartsWith("Hexalith.Parties.", StringComparison.Ordinal))
            .Select(name => (Path: Body(name), Hash: Hash(Body(name)))).ToArray();
        PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt(references, PublishedCatalogArtifactCohort.CurrentOwnerCurrentName);
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt(references, PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt(references[1..], PublishedCatalogArtifactCohort.CurrentOwnerCurrentName));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt([.. references, references[0]], PublishedCatalogArtifactCohort.CurrentOwnerCurrentName));
    }

    /// <summary>Changing any current owner body loses authority even when the appended assembly remains loadable.</summary>
    [Fact]
    public void ChangedCurrentOwnerBodiesLoseAuthority()
    {
        string directory = Path.Combine(Path.GetTempPath(), "current-cohort-changed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var context = new AssemblyLoadContext("current-changed-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            foreach (string name in _names.Where(name => !name.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal) && name != "Hexalith.McpCli.Abstractions"))
            {
                string path = Path.Combine(directory, name + ".dll");
                File.WriteAllBytes(path, [.. File.ReadAllBytes(Body(name)), 0]);
                Assembly body = context.LoadFromAssemblyPath(path);
                Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(body, PublishedCatalogArtifactCohort.PublishedPartiesCurrentName));
            }
        }
        finally { context.Unload(); Directory.Delete(directory, recursive: true); }
    }

    /// <summary>The new tuple is additive; old tuples retain their identities and mixed or unknown versions refuse.</summary>
    [Fact]
    public void OnlyCompleteCurrentVersionTuplesAreAdmitted()
    {
        PublishedCatalogArtifactCohort.ForCurrentOwnerVersions("4.2.0", "7.0.7", "3.0.26").ShouldBe(PublishedCatalogArtifactCohort.CurrentOwnerCurrentName);
        PublishedCatalogArtifactCohort.ForPublishedPartiesVersions("4.2.0", "7.0.7", "3.0.26", "3.1.1").ShouldBe(PublishedCatalogArtifactCohort.PublishedPartiesCurrentName);
        PublishedCatalogArtifactCohort.ForEventStorePackageTestVersions("4.2.0", "7.0.7").ShouldBe(PublishedCatalogArtifactCohort.EventStorePackageTestCurrentName);
        PublishedCatalogArtifactCohort.ForPublishedPartiesVersions("4.2.0", "7.0.5", "3.0.25", "3.1.0").ShouldBe(PublishedCatalogArtifactCohort.PublishedPartiesSdk420Name);
        foreach (var tuple in new[] { ("4.1.0", "7.0.7", "3.0.26"), ("4.2.1", "7.0.7", "3.0.26"), ("4.2.0", "7.0.8", "3.0.26"),
            ("4.2.0", "7.0.7+metadata", "3.0.26"), ("4.2.0", "7.0.7", "3.0.27"), ("4.2.0", "7.0.7", "3.0.25"), ("4.2.0", "7.0.5", "3.0.26") })
        {
            PublishedCatalogArtifactCohort.ForCurrentOwnerVersions(tuple.Item1, tuple.Item2, tuple.Item3).ShouldBeNull();
            PublishedCatalogArtifactCohort.ForPublishedPartiesVersions(tuple.Item1, tuple.Item2, tuple.Item3, "3.1.1").ShouldBeNull();
        }
        foreach (string refused in new[] { "3.1.0", "3.1.2", "3.1.1+metadata", "3.1.1.0" })
            PublishedCatalogArtifactCohort.ForPublishedPartiesVersions("4.2.0", "7.0.7", "3.0.26", refused).ShouldBeNull();
        PublishedCatalogArtifactCohort.ForVersions("4.2.0", "7.0.7").ShouldBeNull();
        PublishedCatalogArtifactCohort.ForVersions("4.2.0", "7.0.7", fullParties: true).ShouldBeNull();
    }

    /// <summary>Public bodies cannot acquire a private measured source purpose or a historical declaration.</summary>
    [Fact]
    public void PublicAndPrivatePurposesStaySeparate()
    {
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyFullParties(PublishedCatalogArtifactCohort.CurrentOwnerCurrentName));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyFullParties(PublishedCatalogArtifactCohort.EventStorePackageTestCurrentName));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.CurrentOwnerSdkVersion(PublishedCatalogArtifactCohort.PublishedPartiesCurrentName));
        PlatformPartiesPackageCatalogQualification.MatchesPartiesVersion("3.1.1", true, PublishedCatalogArtifactCohort.CurrentOwnerCurrentName).ShouldBeTrue();
        PlatformPartiesPackageCatalogQualification.MatchesPartiesVersion("3.1.0", true, PublishedCatalogArtifactCohort.CurrentOwnerCurrentName).ShouldBeFalse();
        PlatformPartiesPackageCatalogQualification.MatchesPartiesVersion("3.1.1", true, PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name).ShouldBeFalse();
        PlatformPartiesPackageCatalogQualification.MatchesPartiesVersion("3.1.1", false, PublishedCatalogArtifactCohort.PublishedPartiesCurrentName).ShouldBeTrue();
    }

    /// <summary>The actual source declaration belongs to the captured current owner revision, independently from published package bodies.</summary>
    [Fact]
    public void ActualCurrentDeclarationIsBoundToMeasuredSource()
    {
        string root = CurrentOwnerRoot();
        string revision = Revision(root);
        string declaration = Path.Combine(root, "module", "hexalith.module-manifest.v2.json");
        Hash(declaration).ShouldBe(PlatformCurrentOwnerProof.CurrentDeclarationHash);
        PlatformCurrentOwnerSourceState source = PlatformCurrentOwnerSourceState.Capture(root, revision, DateTimeOffset.UtcNow.AddSeconds(60));
        PlatformCurrentOwnerProof.VerifyDeclaration(declaration, PublishedCatalogArtifactCohort.CurrentOwnerCurrentName, source);
        source.Inputs[declaration].ShouldBe(PlatformCurrentOwnerProof.CurrentDeclarationHash);
        source.Revision.ShouldBe(revision);
    }

    /// <summary>The current source declaration cannot replay into the frozen historical or public source-proof purpose.</summary>
    [Fact]
    public void CurrentDeclarationCannotReplayIntoHistoricalSourceAuthority()
    {
        string root = CurrentOwnerRoot();
        string revision = Revision(root);
        string declaration = Path.Combine(root, "module", "hexalith.module-manifest.v2.json");
        PlatformCurrentOwnerSourceState source = PlatformCurrentOwnerSourceState.Capture(root, revision, DateTimeOffset.UtcNow.AddSeconds(60));
        Should.Throw<InvalidDataException>(() => PlatformCurrentOwnerProof.VerifyDeclaration(declaration, PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name, source));
        Should.Throw<InvalidDataException>(() => PlatformCurrentOwnerProof.DeclarationHashForCohort(PublishedCatalogArtifactCohort.PublishedPartiesCurrentName));
        PlatformCurrentOwnerProof.DeclarationHash.ShouldBe(PlatformPartiesPackageCatalogQualification.PublishedDeclarationHash);
    }

    /// <summary>Artifact authentication cannot expand ordinary image admission or relabel the existing public declaration.</summary>
    [Fact]
    public void CurrentArtifactsDoNotAcquireOrdinaryImageAuthority()
    {
        PlatformPartiesPackageCatalogQualification.IsOrdinaryDeclaration(PlatformCurrentOwnerProof.CurrentDeclarationHash).ShouldBeFalse();
        PlatformPartiesPackageCatalogQualification.IsOrdinaryDeclaration(PlatformPartiesPackageCatalogQualification.PublishedDeclarationHash).ShouldBeTrue();
        PublishedCatalogArtifactCohort.ForVersions("4.2.0", "7.0.7", fullParties: true).ShouldBeNull();
    }

    /// <summary>The real current private producer and reader preserve exact dependencies without admitting an image cohort.</summary>
    [Fact]
    public async Task CurrentPrivateDependencyVersionsRemainExact()
    {
        PublishedCatalogArtifactCohort.CurrentOwnerSdkVersion(PublishedCatalogArtifactCohort.CurrentOwnerCurrentName).ShouldBe("4.2.0");
        PublishedCatalogArtifactCohort.CurrentOwnerTenantsVersion(PublishedCatalogArtifactCohort.CurrentOwnerCurrentName).ShouldBe("7.0.7");
        PublishedCatalogArtifactCohort.CurrentOwnerMemoriesVersion(PublishedCatalogArtifactCohort.CurrentOwnerCurrentName).ShouldBe("3.0.26");
        PublishedCatalogArtifactCohort.CurrentOwnerTenantsVersion(PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name).ShouldBe("7.0.5");
        PublishedCatalogArtifactCohort.CurrentOwnerMemoriesVersion(PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name).ShouldBe("3.0.25");
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.CurrentOwnerTenantsVersion("unknown"));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.CurrentOwnerMemoriesVersion("unknown"));
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(PlatformCurrentOwnerProof.Profile, sdk42: true, currentArtifacts: true).ConfigureAwait(true);
        PlatformCompositionOptions options = fixture.CaptureCurrent();
        PlatformCurrentOwnerProof proof = options.CurrentOwnerProof.ShouldNotBeNull();
        proof.Cohort.ShouldBe(PublishedCatalogArtifactCohort.CurrentOwnerCurrentName);
        proof.SourceRevision.ShouldBe(Revision(CurrentOwnerRoot()));
        proof.SelectedDeclarationHash.ShouldBe(PlatformCurrentOwnerProof.CurrentDeclarationHash);
        proof.Matches(options).ShouldBeTrue();
        options.PartiesRunProof.ShouldBeNull();
        options.QualificationOwnerProof.ShouldBeNull();
        Directory.Exists(Path.Combine(fixture.Workspace, "platform-components")).ShouldBeFalse();
        var declarations = PlatformDeclarationSet.Load(options);
        var servers = PlatformAppModel.Prepare(options);
        Should.Throw<InvalidDataException>(() => PlatformPartiesPackageCatalogQualification.Bind(options, declarations, servers, developmentRun: true));
        int probes = 0;
        var builder = PlatformPartiesPackageFixture.Builder();
        Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options with
        {
            ImageProbeRunner = (_, _, _) =>
            {
                probes++;
                return Task.FromResult(new CompositionProcessResult(true, 83, "", false));
            },
        }));
        probes.ShouldBe(0, "The current package reader purpose must refuse before the first image probe.");
        fixture.AssertNoEffects(builder);
        // Reader generation authenticates the measured current private bodies without composing images or resources.
        string verification = Path.Combine(fixture.Workspace, "current-reader-verification");
        PlatformRoutingCatalog catalog = PlatformRoutingCatalog.Generate(options, servers, verification);
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.Directory, "current-reader-receipt.json")));
        proof.VerifyReader(receipt.RootElement);
        using JsonDocument request = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(verification, "catalog-reader", "request.json")));
        request.RootElement.GetProperty("qualificationCohort").GetString().ShouldBe(PublishedCatalogArtifactCohort.CurrentOwnerCurrentName);
        foreach (string name in new[] { "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions" })
            Hash(Path.Combine(verification, "catalog-reader", "output", name + ".dll")).ShouldBe(proof.Bodies[name]);
        using JsonDocument canonical = JsonDocument.Parse(File.ReadAllBytes(catalog.CatalogPath));
        foreach (string name in new[] { "routes", "operations", "declarations" }) canonical.RootElement.GetProperty(name).GetArrayLength().ShouldBe(70);
        canonical.RootElement.GetProperty("idempotency").GetArrayLength().ShouldBe(46);
        proof.Matches(options).ShouldBeTrue();
        fixture.AssertNoEffects(builder);
    }

    private static string CurrentOwnerRoot() => typeof(PlatformCurrentPackageArtifactCohortTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "PlatformTestRoot:Hexalith.Parties").Value!;

    private static string Revision(string root)
    {
        var result = CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("git", ["rev-parse", "HEAD"], root, null),
            TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        result.ExitCode.ShouldBe(0, result.ErrorOutput);
        result.TimedOut.ShouldBeFalse();
        result.OutputTruncated.ShouldBeFalse();
        return result.Output.Trim();
    }

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static string Body(string name)
    {
        string root = typeof(PlatformCurrentPackageArtifactCohortTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "PlatformHistoricalPackageRoot").Value!;
        string version = name.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal) ? "4.2.0"
            : name.StartsWith("Hexalith.Tenants.", StringComparison.Ordinal) ? "7.0.7"
            : name.StartsWith("Hexalith.Memories.", StringComparison.Ordinal) ? "3.0.26"
            : name.StartsWith("Hexalith.Parties.", StringComparison.Ordinal) ? "3.1.1" : "2.0.1";
        string path = Path.Combine(root, name.ToLowerInvariant(), version, "lib", "net10.0", name + ".dll");
        File.Exists(path).ShouldBeTrue("The measured genuine current package must be restored before checking its artifact authority.");
        return path;
    }
}
#endif
