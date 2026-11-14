#if HEXALITH_PLATFORM_EXTENSION_SDK && !HEXALITH_PLATFORM_SOURCE_CATALOG
namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Platform.CatalogArtifacts;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

/// <summary>Authenticates current signed public reader artifacts separately from all historical and native image authorities.</summary>
public sealed class PlatformCurrentPackageArtifactCohortTests
{
    private static readonly string[] _names = ["Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults", "Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService",
        "Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server", "Hexalith.Memories.Contracts", "Hexalith.Memories.EventStore", "Hexalith.Memories.EventStoreExtensions",
        "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions", "Hexalith.McpCli.Abstractions"];

    /// <summary>Every genuine current public body verifies under its separate exact reader authority.</summary>
    [Theory]
    [InlineData("3.0.26")]
    [InlineData("3.0.27")]
    public void CurrentPublicReaderAuthenticatesEveryActualBody(string memoriesVersion)
    {
        var context = new AssemblyLoadContext("current-public-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            foreach (string name in _names)
            {
                string path = Body(name, memoriesVersion);
                Assembly body = context.LoadFromAssemblyPath(path);
                PublishedCatalogArtifactCohort.Verify(body, PublicCohort(memoriesVersion)).ShouldBe(path);
                if (name.StartsWith("Hexalith.Tenants.", StringComparison.Ordinal) || name.StartsWith("Hexalith.Memories.", StringComparison.Ordinal)
                    || name.StartsWith("Hexalith.Parties.", StringComparison.Ordinal))
                    Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(body, PublishedCatalogArtifactCohort.PublishedPartiesSdk420Name));
            }
        }
        finally { context.Unload(); }
    }

    /// <summary>Current private receipts bind all ten actual references and reject partial, duplicate or crossed receipts.</summary>
    [Theory]
    [InlineData("3.0.26")]
    [InlineData("3.0.27")]
    public void CurrentPrivateReceiptsAuthenticateExactReferences(string memoriesVersion)
    {
        var references = _names.Where(name => !name.StartsWith("Hexalith.Parties.", StringComparison.Ordinal))
            .Select(name => (Path: Body(name, memoriesVersion), Hash: Hash(Body(name, memoriesVersion)))).ToArray();
        PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt(references, PrivateCohort(memoriesVersion));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt(references, PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt(references,
            PrivateCohort(memoriesVersion == "3.0.26" ? "3.0.27" : "3.0.26")));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt(references[1..], PrivateCohort(memoriesVersion)));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt([.. references, references[0]], PrivateCohort(memoriesVersion)));
    }

    /// <summary>Changing any current owner body loses authority even when the appended assembly remains loadable.</summary>
    [Theory]
    [InlineData("3.0.26")]
    [InlineData("3.0.27")]
    public void ChangedCurrentOwnerBodiesLoseAuthority(string memoriesVersion)
    {
        string directory = Path.Combine(Path.GetTempPath(), "current-cohort-changed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var context = new AssemblyLoadContext("current-changed-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            foreach (string name in _names.Where(name => !name.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal) && name != "Hexalith.McpCli.Abstractions"))
            {
                string path = Path.Combine(directory, name + ".dll");
                File.WriteAllBytes(path, [.. File.ReadAllBytes(Body(name, memoriesVersion)), 0]);
                Assembly body = context.LoadFromAssemblyPath(path);
                Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(body, PublicCohort(memoriesVersion)));
            }
        }
        finally { context.Unload(); Directory.Delete(directory, recursive: true); }
    }

    /// <summary>The new tuple is additive; old tuples retain their identities and mixed or unknown versions refuse.</summary>
    [Theory]
    [InlineData("3.0.26")]
    [InlineData("3.0.27")]
    public void OnlyCompleteCurrentVersionTuplesAreAdmitted(string memoriesVersion)
    {
        PublishedCatalogArtifactCohort.ForCurrentOwnerVersions("4.2.0", "7.0.7", memoriesVersion, "2.0.1").ShouldBe(PrivateCohort(memoriesVersion));
        PublishedCatalogArtifactCohort.ForPublishedPartiesVersions("4.2.0", "7.0.7", memoriesVersion, "3.1.1", "2.0.1").ShouldBe(PublicCohort(memoriesVersion));
        PublishedCatalogArtifactCohort.ForEventStorePackageTestVersions("4.2.0", "7.0.7").ShouldBe(PublishedCatalogArtifactCohort.EventStorePackageTestCurrentName);
        PublishedCatalogArtifactCohort.ForPublishedPartiesVersions("4.2.0", "7.0.5", "3.0.25", "3.1.0", "2.0.1").ShouldBe(PublishedCatalogArtifactCohort.PublishedPartiesSdk420Name);
        foreach (var tuple in new[] { ("4.1.0", "7.0.7", "3.0.26"), ("4.2.1", "7.0.7", "3.0.26"), ("4.2.0", "7.0.8", "3.0.26"),
            ("4.2.0", "7.0.7+metadata", "3.0.26"), ("4.2.0", "7.0.7", "3.0.28"), ("4.2.0", "7.0.7", "3.0.25"), ("4.2.0", "7.0.5", "3.0.26") })
        {
            PublishedCatalogArtifactCohort.ForCurrentOwnerVersions(tuple.Item1, tuple.Item2, tuple.Item3, "2.0.1").ShouldBeNull();
            PublishedCatalogArtifactCohort.ForPublishedPartiesVersions(tuple.Item1, tuple.Item2, tuple.Item3, "3.1.1", "2.0.1").ShouldBeNull();
        }
        foreach (string refused in new[] { "3.1.0", "3.1.2", "3.1.1+metadata", "3.1.1.0" })
            PublishedCatalogArtifactCohort.ForPublishedPartiesVersions("4.2.0", "7.0.7", memoriesVersion, refused, "2.0.1").ShouldBeNull();
        foreach (string? refusedMcpCli in new[] { null, "", "2.0.0", "2.0.2", "2.0.1+metadata", "3.0.0" })
        {
            PublishedCatalogArtifactCohort.ForCurrentOwnerVersions("4.2.0", "7.0.7", memoriesVersion, refusedMcpCli).ShouldBeNull();
            PublishedCatalogArtifactCohort.ForPublishedPartiesVersions("4.2.0", "7.0.7", memoriesVersion, "3.1.1", refusedMcpCli).ShouldBeNull();
        }
        PublishedCatalogArtifactCohort.ForVersions("4.2.0", "7.0.7").ShouldBeNull();
        PublishedCatalogArtifactCohort.ForVersions("4.2.0", "7.0.7", fullParties: true).ShouldBeNull();
    }

    /// <summary>Public bodies cannot acquire a private measured source purpose or a historical declaration.</summary>
    [Theory]
    [InlineData("3.0.26")]
    [InlineData("3.0.27")]
    public void PublicAndPrivatePurposesStaySeparate(string memoriesVersion)
    {
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyFullParties(PrivateCohort(memoriesVersion)));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyFullParties(PublishedCatalogArtifactCohort.EventStorePackageTestCurrentName));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.CurrentOwnerSdkVersion(PublicCohort(memoriesVersion)));
        PlatformPartiesPackageCatalogQualification.MatchesPartiesVersion("3.1.1", true, PrivateCohort(memoriesVersion)).ShouldBeTrue();
        PlatformPartiesPackageCatalogQualification.MatchesPartiesVersion("3.1.0", true, PrivateCohort(memoriesVersion)).ShouldBeFalse();
        PlatformPartiesPackageCatalogQualification.MatchesPartiesVersion("3.1.1", true, PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name).ShouldBeFalse();
        PlatformPartiesPackageCatalogQualification.MatchesPartiesVersion("3.1.1", false, PublicCohort(memoriesVersion)).ShouldBeTrue();
    }

    /// <summary>The actual source declaration belongs to the captured current owner revision, independently from published package bodies.</summary>
    [Theory]
    [InlineData("3.0.26")]
    [InlineData("3.0.27")]
    public void ActualCurrentDeclarationIsBoundToMeasuredSource(string memoriesVersion)
    {
        string root = CurrentOwnerRoot();
        string revision = Revision(root);
        string declaration = Path.Combine(root, "module", "hexalith.module-manifest.v2.json");
        Hash(declaration).ShouldBe(PlatformCurrentOwnerProof.CurrentDeclarationHash);
        PlatformCurrentOwnerSourceState source = PlatformCurrentOwnerSourceState.Capture(root, revision, DateTimeOffset.UtcNow.AddSeconds(60));
        PlatformCurrentOwnerProof.VerifyDeclaration(declaration, PrivateCohort(memoriesVersion), source);
        source.Inputs[declaration].ShouldBe(PlatformCurrentOwnerProof.CurrentDeclarationHash);
        source.Revision.ShouldBe(revision);
    }

    /// <summary>The current source declaration cannot replay into the frozen historical or public source-proof purpose.</summary>
    [Theory]
    [InlineData("3.0.26")]
    [InlineData("3.0.27")]
    public void CurrentDeclarationCannotReplayIntoHistoricalSourceAuthority(string memoriesVersion)
    {
        string root = CurrentOwnerRoot();
        string revision = Revision(root);
        string declaration = Path.Combine(root, "module", "hexalith.module-manifest.v2.json");
        PlatformCurrentOwnerSourceState source = PlatformCurrentOwnerSourceState.Capture(root, revision, DateTimeOffset.UtcNow.AddSeconds(60));
        Should.Throw<InvalidDataException>(() => PlatformCurrentOwnerProof.VerifyDeclaration(declaration, PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name, source));
        Should.Throw<InvalidDataException>(() => PlatformCurrentOwnerProof.DeclarationHashForCohort(PublicCohort(memoriesVersion)));
        PlatformCurrentOwnerProof.DeclarationHash.ShouldBe(PlatformPartiesPackageCatalogQualification.PublishedDeclarationHash);
    }

    /// <summary>Artifact authentication cannot expand ordinary image admission or relabel the existing public declaration.</summary>
    [Theory]
    [InlineData("3.0.26")]
    [InlineData("3.0.27")]
    public void CurrentArtifactsDoNotAcquireOrdinaryImageAuthority(string memoriesVersion)
    {
        PublishedCatalogArtifactCohort.ForPublishedPartiesVersions("4.2.0", "7.0.7", memoriesVersion, "3.1.1", "2.0.1").ShouldBe(PublicCohort(memoriesVersion));
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
        PublishedCatalogArtifactCohort.CurrentOwnerMemoriesVersion(PublishedCatalogArtifactCohort.CurrentOwnerMemories327Name).ShouldBe("3.0.27");
        PublishedCatalogArtifactCohort.CurrentOwnerTenantsVersion(PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name).ShouldBe("7.0.5");
        PublishedCatalogArtifactCohort.CurrentOwnerMemoriesVersion(PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name).ShouldBe("3.0.25");
        foreach (string cohort in new[] { PublishedCatalogArtifactCohort.CurrentOwnerName, PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name,
            PublishedCatalogArtifactCohort.CurrentOwnerCurrentName, PublishedCatalogArtifactCohort.CurrentOwnerMemories327Name })
            PublishedCatalogArtifactCohort.CurrentOwnerMcpCliVersion(cohort).ShouldBe("2.0.1");
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.CurrentOwnerMcpCliVersion("unknown"));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.CurrentOwnerMcpCliVersion(PublishedCatalogArtifactCohort.PublishedPartiesMemories327Name));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.CurrentOwnerTenantsVersion("unknown"));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.CurrentOwnerMemoriesVersion("unknown"));
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(PlatformCurrentOwnerProof.Profile, sdk42: true, currentArtifacts: true).ConfigureAwait(true);
#if NET11_0_OR_GREATER
        AssertCurrentConsumerCatalog(fixture);
#endif
        PlatformCompositionOptions options = fixture.CaptureCurrent();
        PlatformCurrentOwnerProof proof = options.CurrentOwnerProof.ShouldNotBeNull();
        string memoriesVersion = PlatformServerBindings.Version(options, "memories", "current preparation");
        proof.Cohort.ShouldBe(PrivateCohort(memoriesVersion));
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
        request.RootElement.GetProperty("qualificationCohort").GetString().ShouldBe(PrivateCohort(memoriesVersion));
        foreach (string name in new[] { "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions" })
            Hash(Path.Combine(verification, "catalog-reader", "output", name + ".dll")).ShouldBe(proof.Bodies[name]);
        using JsonDocument canonical = JsonDocument.Parse(File.ReadAllBytes(catalog.CatalogPath));
        foreach (string name in new[] { "routes", "operations", "declarations" }) canonical.RootElement.GetProperty(name).GetArrayLength().ShouldBe(70);
        canonical.RootElement.GetProperty("idempotency").GetArrayLength().ShouldBe(46);
        proof.Matches(options).ShouldBeTrue();
        fixture.AssertNoEffects(builder);
    }

#if NET11_0_OR_GREATER
    private static void AssertCurrentConsumerCatalog(PlatformPartiesPackageFixture fixture)
    {
        SourceMapping mapping = fixture.Plan.SourceMapping!;
        PlatformCompositionOptions options = new()
        {
            PlatformRoot = mapping.PlatformIdentity!.PlatformPath, Mapping = mapping, ActiveModule = "parties",
            DeclarationPaths = fixture.Plan.PlatformDeclarationPaths, RunPlan = fixture.Plan, EventStoreSeedProject = fixture.Seed,
        };
        string path = Path.Combine(fixture.Workspace, "source-mapping", "catalog-versions.json");
        byte[] original = File.ReadAllBytes(path);
        CatalogVersionSnapshot snapshot = JsonSerializer.Deserialize<CatalogVersionSnapshot>(original)!;
        var defaults = new CompositionDeclarationOptions(mapping, options.DeclarationPaths) { PlatformRoot = options.PlatformRoot, Workspace = fixture.Workspace };
        NuGet.Versioning.VersionRange producerRange = NuGet.Versioning.VersionRange.Parse(mapping.PlatformIdentity.ToolVersionRange!);
        string expectedDefault = producerRange.MinVersion!.Major switch
        {
            5 => "5.0.0",
            6 when mapping.PlatformIdentity.CatalogCommit == "60ce7967607dc8b703fff11f4fc13cabb09c090a"
                && producerRange.MinVersion.ToFullString() == "6.0.2" && producerRange.MaxVersion?.ToFullString() == "7.0.0-0"
                && producerRange.IsMinInclusive && !producerRange.IsMaxInclusive => "5.1.0",
            _ => throw new InvalidDataException("Unknown current consumer catalog producer."),
        };
        defaults.CatalogVersion("eventstore", "current default control").ShouldBe(expectedDefault);
        PlatformDeclarationSet.DeclarationOptions(options).ConsumerProjectName.ShouldBe(CompositionDeclarationOptions.PlatformCompositionProjectName);
        foreach (string module in new[] { "eventstore", "tenants", "memories", "parties" })
            PlatformServerBindings.Version(options, module, "current named control")
                .ShouldBe(defaults.CatalogVersion(module, "current named control", CompositionDeclarationOptions.PlatformCompositionProjectName));
        PlatformServerBindings.Version(options, "eventstore", "current named control").ShouldBe("4.2.0");
        PlatformMcpCliCatalogVersion.Resolve(options, "current named control").ShouldBe("2.0.1");
        PlatformDeclarationSet.Load(options).Count.ShouldBe(4);
        string? cohort = PlatformPackageCatalogQualification.SelectCurrentOwnerCohort(options);
        cohort.ShouldBe(PrivateCohort(PlatformServerBindings.Version(options, "memories", "current named control")));
        File.ReadAllBytes(path).ShouldBe(original, "Reading the named view must preserve the default catalog and original snapshot bytes.");

        var projects = snapshot.ProjectVersions.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        projects.Remove(CompositionDeclarationOptions.PlatformCompositionProjectName).ShouldBeTrue();
        try
        {
            // Rehashing the negative control distinguishes an absent required view from a stale hash.
            Write(snapshot with { ProjectVersions = projects });
            Refuses(options);
            foreach (string package in new[] { "Hexalith.EventStore.Aspire", "Hexalith.McpCli.Abstractions" })
            {
                snapshot.Versions.ContainsKey(package).ShouldBeTrue("The global pin remains present during the named-pin refusal control.");
                projects = snapshot.ProjectVersions.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                var selected = projects[CompositionDeclarationOptions.PlatformCompositionProjectName].ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                selected.Remove(package).ShouldBeTrue();
                projects[CompositionDeclarationOptions.PlatformCompositionProjectName] = selected;
                Write(snapshot with { ProjectVersions = projects });
                if (package == "Hexalith.EventStore.Aspire")
                {
                    Should.Throw<InvalidDataException>(() => PlatformServerBindings.Version(options, "eventstore", "missing named pin"));
                    Should.Throw<InvalidDataException>(() => PlatformDeclarationSet.Load(options));
                    Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.SelectCurrentOwnerCohort(options));
                }
                else
                {
                    PlatformServerBindings.Version(options, "eventstore", "missing named MCP pin").ShouldBe("4.2.0");
                    PlatformMcpCliCatalogVersion.Resolve(options, "missing named MCP pin").ShouldBeNull();
                    PlatformPackageCatalogQualification.SelectCurrentOwnerCohort(options).ShouldBeNull();
                }
                File.WriteAllBytes(path, original);
            }
            File.WriteAllText(path, "{");
            Refuses(options);
            File.WriteAllText(path, "null");
            Refuses(options);
            File.WriteAllText(path, JsonSerializer.Serialize(snapshot with { Hash = new string('0', 64) }));
            Refuses(options);
            Write(snapshot with { MappingHash = new string('0', 64) });
            Refuses(options);
            Write(snapshot with { CatalogCommit = new string('0', 40) });
            Refuses(options);
            Write(snapshot with { CatalogFileHash = new string('0', 64) });
            Refuses(options);
            File.Delete(path);
            Refuses(options);
            File.WriteAllBytes(path, original);

            SourceMapping changedSource = mapping with
            {
                Entries = mapping.Entries.Select(entry => entry.Identity == "Hexalith.Parties" ? entry with { Path = Path.Combine(fixture.Workspace, "foreign-owner") } : entry).ToArray(),
            };
            Refuses(options with { Mapping = changedSource });
            foreach (string? range in new string?[] { null, "", "invalid", "5.*", "5.2.1", "(,6.0.0)", "(5.2.1,6.0.0)", "[5.2.1-preview,6.0.0)", "[6.0.0,7.0.0)",
                "6.*", "[6.0.2,7.0.0)", "[6.0.0,7.0.0-0)", "[6.0.1,7.0.0-0)", "[6.0.3,7.0.0-0)", "(6.0.2,7.0.0-0)", "[6.0.2,7.0.0-0]", "[6.0.2,8.0.0-0)",
                "[6.0.2-preview,7.0.0-0)", "[6.0.2+metadata,7.0.0-0)", "[7.0.0,8.0.0-0)", "[7.0.0,8.0.0)" })
            {
                PlatformCompositionOptions changed = options with { Mapping = mapping with { PlatformIdentity = mapping.PlatformIdentity with { ToolVersionRange = range } } };
                Should.Throw<InvalidDataException>(() => PlatformDeclarationSet.DeclarationOptions(changed));
                Refuses(changed);
            }

            if (producerRange.MinVersion.Major == 6)
            {
                foreach (string? source in new string?[] { null, new string('0', 40), "12475add3f14b06709a5ffe74f413d27d4111d75", "c91beec60ce5eaa8cd62bf055d07fa6ca3890e89", "1ebc5b84ff2c8afd21a87d80ef3d9c9048eaf5e5", "02ed624aa19781cac75d2c9112940e9bd830441a" })
                {
                    PlatformCompositionOptions changed = options with { Mapping = mapping with { PlatformIdentity = mapping.PlatformIdentity with { CatalogCommit = source } } };
                    Should.Throw<InvalidDataException>(() => PlatformDeclarationSet.DeclarationOptions(changed));
                    Refuses(changed);
                }
                // The retained producer5 branch still selects its named consumer without acquiring producer6 authority.
                PlatformCompositionOptions retained = options with { Mapping = mapping with { PlatformIdentity = mapping.PlatformIdentity with
                {
                    ToolVersionRange = "[5.2.2,6.0.0-0)", CatalogCommit = "02ed624aa19781cac75d2c9112940e9bd830441a",
                } } };
                PlatformDeclarationSet.DeclarationOptions(retained).ConsumerProjectName.ShouldBe(CompositionDeclarationOptions.PlatformCompositionProjectName);
            }

            foreach (string version in new[] { "5.0.0", "4.2.1", "4.2.0+metadata" })
            {
                projects = snapshot.ProjectVersions.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                var selected = projects[CompositionDeclarationOptions.PlatformCompositionProjectName].ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                selected["Hexalith.EventStore.Aspire"] = version;
                projects[CompositionDeclarationOptions.PlatformCompositionProjectName] = selected;
                Write(snapshot with { ProjectVersions = projects });
                PlatformServerBindings.Version(options, "eventstore", "unqualified named control").ShouldBe(version);
                PlatformPackageCatalogQualification.SelectCurrentOwnerCohort(options).ShouldBeNull();
                Should.Throw<InvalidDataException>(() => PlatformDeclarationSet.Load(options));
            }
            File.WriteAllBytes(path, original);
            foreach (string relative in snapshot.PinnedFiles.Keys)
            {
                string pinned = Path.Combine(fixture.Workspace, "source-mapping", "pinned-tree", relative);
                byte[] bytes = File.ReadAllBytes(pinned);
                try
                {
                    File.WriteAllBytes(pinned, [.. bytes, 0]);
                    Refuses(options);
                }
                finally { File.WriteAllBytes(pinned, bytes); }
            }
        }
        finally { File.WriteAllBytes(path, original); }
        PlatformPackageCatalogQualification.SelectCurrentOwnerCohort(options).ShouldBe(cohort);
        PlatformDeclarationSet.Load(options).Count.ShouldBe(4);
        File.ReadAllBytes(path).ShouldBe(original);

        void Write(CatalogVersionSnapshot changed)
            => File.WriteAllText(path, JsonSerializer.Serialize(changed with
            {
                Hash = CatalogVersionSnapshot.ComputeHash(changed.MappingHash, changed.CatalogCommit, changed.CatalogFileHash,
                    changed.PinnedFiles, changed.Versions, changed.ProjectVersions),
            }));

        static void Refuses(PlatformCompositionOptions changed)
        {
            Should.Throw<InvalidDataException>(() => PlatformServerBindings.Version(changed, "eventstore", "current refusal control"));
            Should.Throw<InvalidDataException>(() => PlatformMcpCliCatalogVersion.Resolve(changed, "current refusal control"));
            Should.Throw<InvalidDataException>(() => PlatformDeclarationSet.Load(changed));
            Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.SelectCurrentOwnerCohort(changed));
        }
    }
#endif

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

    private static string PublicCohort(string memoriesVersion) => memoriesVersion switch
    {
        "3.0.26" => PublishedCatalogArtifactCohort.PublishedPartiesCurrentName,
        "3.0.27" => PublishedCatalogArtifactCohort.PublishedPartiesMemories327Name,
        _ => throw new InvalidDataException("Unknown public current reference tuple."),
    };

    private static string PrivateCohort(string memoriesVersion) => memoriesVersion switch
    {
        "3.0.26" => PublishedCatalogArtifactCohort.CurrentOwnerCurrentName,
        "3.0.27" => PublishedCatalogArtifactCohort.CurrentOwnerMemories327Name,
        _ => throw new InvalidDataException("Unknown private current reference tuple."),
    };

    private static string Body(string name, string memoriesVersion)
    {
        string root = typeof(PlatformCurrentPackageArtifactCohortTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "PlatformHistoricalPackageRoot").Value!;
        string version = name.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal) ? "4.2.0"
            : name.StartsWith("Hexalith.Tenants.", StringComparison.Ordinal) ? "7.0.7"
            : name.StartsWith("Hexalith.Memories.", StringComparison.Ordinal) ? memoriesVersion
            : name.StartsWith("Hexalith.Parties.", StringComparison.Ordinal) ? "3.1.1" : "2.0.1";
        string path = Path.Combine(root, name.ToLowerInvariant(), version, "lib", "net10.0", name + ".dll");
        File.Exists(path).ShouldBeTrue("The measured genuine current package must be restored before checking its artifact authority.");
        return path;
    }
}
#endif
