#if HEXALITH_PLATFORM_EXTENSION_SDK && !HEXALITH_PLATFORM_SOURCE_CATALOG
namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Platform.CatalogArtifacts;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

/// <summary>Binds the real current private producer and reader to the generated owner package lock and the measured source declaration.</summary>
public sealed class PlatformCurrentPackageArtifactTests
{
    /// <summary>The actual source declaration belongs to the captured current owner revision, independently from published package bodies.</summary>
    [Fact]
    public void ActualCurrentDeclarationIsBoundToMeasuredSource()
    {
        string root = CurrentOwnerRoot();
        string revision = Revision(root);
        string declaration = Path.Combine(root, "module", "hexalith.module-manifest.v2.json");
        PlatformCurrentOwnerSourceState source = PlatformCurrentOwnerSourceState.Capture(root, revision, DateTimeOffset.UtcNow.AddSeconds(60));
        PlatformCurrentOwnerProof.VerifyDeclaration(declaration, Hash(declaration), source);
        source.Revision.ShouldBe(revision);
    }

    /// <summary>A declaration hash other than the measured source bytes refuses the Release preparation.</summary>
    [Fact]
    public void DeclarationOutsideTheMeasuredSourceIsRefused()
    {
        string root = CurrentOwnerRoot();
        string revision = Revision(root);
        string declaration = Path.Combine(root, "module", "hexalith.module-manifest.v2.json");
        PlatformCurrentOwnerSourceState source = PlatformCurrentOwnerSourceState.Capture(root, revision, DateTimeOffset.UtcNow.AddSeconds(60));
        Should.Throw<InvalidDataException>(() => PlatformCurrentOwnerProof.VerifyDeclaration(declaration, new string('0', 64), source));
    }

    /// <summary>The real current private producer and reader admit their dependencies only through the lock, recording AR-12 identities.</summary>
    [Fact]
    public async Task CurrentPrivateDependencyVersionsRemainExact()
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(PlatformCurrentOwnerProof.Profile).ConfigureAwait(true);
#if NET11_0_OR_GREATER
        AssertCurrentConsumerCatalog(fixture);
#endif
        PlatformCompositionOptions options = fixture.CaptureCurrent();
        PlatformCurrentOwnerProof proof = options.CurrentOwnerProof.ShouldNotBeNull();
        proof.OwnerPackages.Path.ShouldBe(OwnerPackageLock.PathFor(options.PlatformRoot));
        foreach (OwnerPackageIdentity body in proof.OwnerBodies.Values)
        {
            body.ContentHash.ShouldBe(proof.OwnerPackages.Require(body.PackageId, body.Version).ContentHash);
            body.ExpectedSha256.ShouldBe(body.LoadedSha256);
            body.PackageId.ShouldNotStartWith("Hexalith.Parties.", Case.Sensitive, "Run-built Parties bodies keep their run-bound authority.");
        }
        proof.SourceRevision.ShouldBe(Revision(CurrentOwnerRoot()));
        proof.Matches(options).ShouldBeTrue();
        options.PartiesRunProof!.Matches(options).ShouldBeTrue();
        options.QualificationOwnerProof.ShouldBeNull();
        Directory.Exists(Path.Combine(fixture.Workspace, "platform-components")).ShouldBeFalse();
        var declarations = PlatformDeclarationSet.Load(options);
        var servers = PlatformAppModel.Prepare(options);
        PlatformPartiesPackageFixture.AssertCatalogSelectedDependencies(
            PlatformPartiesPackageCatalogQualification.Bind(options, declarations, servers, developmentRun: true));
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
        probes.ShouldBe(1, "Admitted current owner images reach the first probe; a failed probe still refuses before rendering.");
        fixture.AssertNoEffects(builder);
        // Reader generation authenticates the measured current private bodies without composing images or resources.
        string verification = Path.Combine(fixture.Workspace, "current-reader-verification");
        PlatformRoutingCatalog catalog = PlatformRoutingCatalog.Generate(options, servers, verification);
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.Directory, "current-reader-receipt.json")));
        proof.VerifyReader(receipt.RootElement);
        using JsonDocument request = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(verification, "catalog-reader", "request.json")));
        request.RootElement.GetProperty("ownerPackageLock").GetString().ShouldBe(proof.OwnerPackages.Path);
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
        // The producer is the pinned version at the recorded gitlink; its generic default is read from that catalog blob.
        mapping.PlatformIdentity.ToolVersionRange.ShouldBe(PlatformTestPins.ToolVersionRange);
        mapping.PlatformIdentity.CatalogCommit.ShouldBe(PlatformTestPins.BuildsGitlink);
        string expectedDefault = PlatformTestPins.CatalogVersion("EventStore");
        defaults.CatalogVersion("eventstore", "current default control").ShouldBe(expectedDefault);
        PlatformDeclarationSet.DeclarationOptions(options).ConsumerProjectName.ShouldBe(CompositionDeclarationOptions.PlatformCompositionProjectName);
        foreach (string module in new[] { "eventstore", "tenants", "memories", "parties" })
            PlatformServerBindings.Version(options, module, "current named control")
                .ShouldBe(defaults.CatalogVersion(module, "current named control", CompositionDeclarationOptions.PlatformCompositionProjectName));
        PlatformServerBindings.Version(options, "eventstore", "current named control").ShouldBe(expectedDefault);
        PlatformMcpCliCatalogVersion.Resolve(options, "current named control").ShouldBe(PlatformTestPins.CatalogVersion("McpCli"));
        PlatformDeclarationSet.Load(options).Count.ShouldBe(4);
        PlatformPackageCatalogQualification.RequireLockedPackages(options).Path.ShouldBe(OwnerPackageLock.PathFor(options.PlatformRoot));
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
                    Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.RequireLockedPackages(options));
                }
                else
                {
                    PlatformServerBindings.Version(options, "eventstore", "missing named MCP pin").ShouldBe(expectedDefault);
                    PlatformMcpCliCatalogVersion.Resolve(options, "missing named MCP pin").ShouldBeNull();
                    Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.RequireLockedPackages(options))
                        .Message.ShouldContain("selects no Hexalith.McpCli.Abstractions version");
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
            // Structurally invalid ranges refuse in the consumer context itself.
            foreach (string? range in new string?[] { null, "", "invalid", "5.*", "5.2.1", "(,6.0.0)", "(5.2.1,6.0.0)", "[5.2.1-preview,6.0.0)", "[6.0.0,7.0.0)",
                "6.*", "[6.0.5,7.0.0)", "(6.0.5,7.0.0-0)", "[6.0.5,7.0.0-0]", "[6.0.5,8.0.0-0)", "[6.0.5-preview,7.0.0-0)", "[6.0.5+metadata,7.0.0-0)",
                "[7.0.0,8.0.0-0)", "[7.0.0,8.0.0)" })
            {
                PlatformCompositionOptions changed = options with { Mapping = mapping with { PlatformIdentity = mapping.PlatformIdentity with { ToolVersionRange = range } } };
                Should.Throw<InvalidDataException>(() => PlatformDeclarationSet.DeclarationOptions(changed));
                Refuses(changed);
            }

            // Any well-formed [pin, next major) range or catalog commit selects the named consumer by the version rule; the
            // persisted snapshot still binds the exact mapping, so a changed identity cannot reuse it.
            foreach (string range in new[] { "[6.0.0,7.0.0-0)", "[6.0.5,7.0.0-0)", "[6.1.0,7.0.0-0)", "[5.2.2,6.0.0-0)" })
            {
                PlatformCompositionOptions changed = options with { Mapping = mapping with { PlatformIdentity = mapping.PlatformIdentity with { ToolVersionRange = range } } };
                PlatformDeclarationSet.DeclarationOptions(changed).ConsumerProjectName.ShouldBe(CompositionDeclarationOptions.PlatformCompositionProjectName);
                Refuses(changed);
            }
            foreach (string? source in new string?[] { null, new string('0', 40), new string('e', 40) })
            {
                PlatformCompositionOptions changed = options with { Mapping = mapping with { PlatformIdentity = mapping.PlatformIdentity with { CatalogCommit = source } } };
                PlatformDeclarationSet.DeclarationOptions(changed).ConsumerProjectName.ShouldBe(CompositionDeclarationOptions.PlatformCompositionProjectName);
                Refuses(changed);
            }

            foreach (string version in new[] { "5.0.0", "4.2.1", "4.2.0+metadata" })
            {
                projects = snapshot.ProjectVersions.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                var selected = projects[CompositionDeclarationOptions.PlatformCompositionProjectName].ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                selected["Hexalith.EventStore.Aspire"] = version;
                projects[CompositionDeclarationOptions.PlatformCompositionProjectName] = selected;
                Write(snapshot with { ProjectVersions = projects });
                PlatformServerBindings.Version(options, "eventstore", "unqualified named control").ShouldBe(version);
                Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.RequireLockedPackages(options));
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
        PlatformPackageCatalogQualification.RequireLockedPackages(options).Sha256.ShouldBe(PlatformPartiesPackageFixture.Lock.Sha256);
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
            Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.RequireLockedPackages(changed));
        }
    }
#endif

    private static string CurrentOwnerRoot() => typeof(PlatformCurrentPackageArtifactTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
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
}
#endif
