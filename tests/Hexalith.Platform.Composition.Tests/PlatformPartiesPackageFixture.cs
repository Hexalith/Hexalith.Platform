namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Aspire.Hosting;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.Platform.CatalogArtifacts;
using Shouldly;

/// <summary>Uses the coherent real Platform-owned direct Parties checkout and its actual accepted published tuple; never changes source pins or creates Git metadata.</summary>
internal sealed class PlatformPartiesPackageFixture : IDisposable
{
    private readonly string _key = CompositionSigningKey.Create();
    private Action? _validationRuntimeVerify;

    private PlatformPartiesPackageFixture(string workspace, CompositionRunPlan plan, string seed)
    {
        Workspace = workspace;
        Plan = plan;
        Seed = seed;
    }

    /// <summary>The seven authenticated Linux/amd64 identities expected at the real image-preflight boundary.</summary>
    internal static IReadOnlySet<string> Images => PublishedCatalogArtifactCohort.Matches("Hexalith.Parties.Contracts", PublishedCatalogArtifactCohort.PublishedPartiesName)
        ? PublishedImages : HistoricalImages;

    private static IReadOnlySet<string> HistoricalImages { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "registry.hexalith.com/tenants@sha256:adfcad2eea7c0b59e2936a62131c7925af518f1331a4e9fec0071da55874caed",
        "registry.hexalith.com/tenants-api@sha256:2922e0b1eb7b115bac472479213ca46d84c26ac4917b1d53854034618179c2e4",
        "registry.hexalith.com/memories@sha256:f8684c3d8a084a5bc357ec13e9c7c5294a350d73f570b585f5fb2bfde3d9904b",
        "registry.hexalith.com/memories-domain-service@sha256:fb0190a1fc5ffdbdcf9cf5d4941ef81600bde23d38840058f695b52623870f5e",
        "registry.hexalith.com/memories-access-telemetry-clock@sha256:b64505ee27cfd455f7efe730831a72688ca92ef6b49fd904eb0e68abdd38e54f",
        "registry.hexalith.com/memories-access-telemetry@sha256:ee01a024abb17a7d465ba8fe5fbb626114b7fb380fc521a5fa7531ca514d548a",
        "registry.hexalith.com/parties@sha256:25e4a03cfcc932f01fd9e66be647305c572a4c7d7e0a680db710cb9579bf783b",
    };

    /// <summary>The six genuine current public images; the private current Parties executable is never relabeled as a public image.</summary>
    internal static IReadOnlySet<string> CurrentImages { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "registry.hexalith.com/tenants@sha256:1ba6ff9dd0623817d667bd46229dd0ba34a23ffc51d48c889d3e67f474bec22a",
        "registry.hexalith.com/tenants-api@sha256:f4707de77c595977d5b7913b4e6119aed407da3c72475ba13b46e9e302d1ef7b",
        "registry.hexalith.com/memories@sha256:a857a0865ff3781027cc392a7d36431fd13db1e7a3522690a8ef05df7036af3a",
        "registry.hexalith.com/memories-domain-service@sha256:90d15d551cb1ada3aafbcab823cbd8e83304605ca383abdbd47573613304e820",
        "registry.hexalith.com/memories-access-telemetry-clock@sha256:e4edd4f62a43fc404f40eac2fd107c17320420b38bbd6514f31f23b19b019d3e",
        "registry.hexalith.com/memories-access-telemetry@sha256:d227044d62db58b87c26f99a2ea6264b148ffd0c8d15c452b3bf4b30dd244e0b",
    };

    /// <summary>The seven separately measured images for the genuine complete public Parties3.1 profile.</summary>
    internal static IReadOnlySet<string> PublishedImages { get; } = CurrentImages
        .Append("registry.hexalith.com/parties@sha256:48d0e8db506e4ce574e8c7bd97e528a302d3fbf97754b007176904cbc1f3abfb")
        .ToHashSet(StringComparer.Ordinal);

    private IReadOnlySet<string> SelectedImages => Plan.SelectedTest?.Name == PlatformCurrentOwnerProof.Profile ? CurrentImages : Images;

    /// <summary>Gets the independently owned private workspace; no source checkout is copied or edited.</summary>
    internal string Workspace { get; }
    /// <summary>Gets the actual selected declaration/test/mapping handoff persisted by the fixture.</summary>
    internal CompositionRunPlan Plan { get; private set; }
    /// <summary>Compares an opaque transport key to the original parent key without exposing either value.</summary>
    /// <param name="key">The transport key under test.</param>
    internal bool UsesSigningKey(string key) => string.Equals(_key, key, StringComparison.Ordinal);
    /// <summary>Gets the verified published tool's package seed project.</summary>
    internal string Seed { get; }
    /// <summary>Gets the production-entry proof-bearing options captured after successful full admission.</summary>
    internal PlatformCompositionOptions Options { get; private set; } = null!;
    /// <summary>Gets the actual production-generated reader directory.</summary>
    internal string Reader => Path.Combine(Workspace, "platform-components", "catalog-reader");

    /// <summary>Requires the real full public bodies before resolving an explicitly prepared coherent workspace; there is no legacy or synthetic fallback.</summary>
    internal static string SelectedSdkVersion => PublishedCatalogArtifactCohort.MatchesSdk(PublishedCatalogArtifactCohort.PublishedPartiesSdk420Name) ? "4.2.0"
        : PublishedCatalogArtifactCohort.MatchesSdk(PublishedCatalogArtifactCohort.PublishedPartiesName) ? "4.1.0"
        : throw new InvalidDataException("The package fixture requires one complete authenticated SDK cohort.");

    internal static string PublishedCohort => SelectedSdkVersion == "4.2.0" ? PublishedCatalogArtifactCohort.PublishedPartiesSdk420Name : PublishedCatalogArtifactCohort.PublishedPartiesName;
    internal static string CurrentCohort => SelectedSdkVersion == "4.2.0" ? PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name : PublishedCatalogArtifactCohort.CurrentOwnerName;
    internal static string EventStoreTestCohort => SelectedSdkVersion == "4.2.0" ? PublishedCatalogArtifactCohort.EventStorePackageTestSdk420Name : PublishedCatalogArtifactCohort.EventStorePackageTestName;

    internal static async Task<PlatformPartiesPackageFixture> CreateAsync(string profile = "platform-package", bool sdk42 = false, bool currentArtifacts = false)
    {
        if (sdk42) SelectedSdkVersion.ShouldBe("4.2.0", "The explicit SDK4.2 case must load its real package cohort.");
        bool current = profile == PlatformCurrentOwnerProof.Profile;
        if (currentArtifacts && !current) throw new ArgumentException("Current artifact preparation requires the explicit private current-owner profile.", nameof(profile));
        bool published = !current && PublishedCatalogArtifactCohort.Matches("Hexalith.Parties.Contracts", PublishedCatalogArtifactCohort.PublishedPartiesName);
        string cohort = currentArtifacts ? PublishedCatalogArtifactCohort.CurrentOwnerCurrentName : current ? CurrentCohort
            : published ? PublishedCohort : PublishedCatalogArtifactCohort.CurrentFullPartiesName;
        if (!currentArtifacts)
        {
            PublishedCatalogArtifactCohort.Matches("Hexalith.Memories.Contracts", cohort)
                .ShouldBeTrue(current || published
                    ? "Current Parties preparation requires its exact genuine public M25/SDK tuple and loaded public Contracts body."
                    : "Full Parties qualification requires the genuine published M23 tuple and loaded public Contracts body; the accepted M18 output must not be substituted.");
            PublishedCatalogArtifactCohort.Matches("Hexalith.Tenants.Contracts", cohort).ShouldBeTrue();
            if (!current) PublishedCatalogArtifactCohort.Matches("Hexalith.Parties.Contracts", cohort).ShouldBeTrue();
        }
        // The current private producer builds its own fresh707/26 or707/27 package closure. The collecting
        // test assembly deliberately retains historical bodies and cannot establish those identities.
        PlatformPackageCatalogQualification.HasQualifiedSdkBytes(cohort).ShouldBeTrue();
        string root = currentArtifacts ? PlatformTestRepository.Root : PreparedFixtureRoot();
        string active = Path.Combine(root, "references", "Hexalith.Parties");
        foreach (string repository in new[] { "Hexalith.Builds", "Hexalith.Parties" })
        {
            string marker = Path.Combine(root, "references", repository, ".git");
            (File.Exists(marker) || Directory.Exists(marker)).ShouldBeTrue("Direct dependencies must already be prepared; this test must not initialize them.");
        }
        string manifest = Path.Combine(active, "module", "hexalith.module-manifest.v2.json");
        Hash(manifest).ShouldBe(current ? PlatformCurrentOwnerProof.DeclarationHashForCohort(cohort)
            : published ? PlatformPartiesPackageCatalogQualification.PublishedDeclarationHash : PlatformPartiesPackageCatalogQualification.ActiveDeclarationHash);
        SourceMapping mapping = await WorkspaceRootResolver.ResolveAsync(manifest, WorkspaceMode.Package, TestContext.Current.CancellationToken);
        mapping.Root.ShouldBe(root);
        mapping.Entries.Single(entry => entry.Identity == "Hexalith.Parties").Path.ShouldBe(active);
        mapping.ActiveModule.ShouldBe("Hexalith.Parties");
        PlatformIdentity identity = mapping.PlatformIdentity.ShouldNotBeNull();
        identity.IsPlatformRepository.ShouldBeTrue("This is the real Platform-owned qualification path; it does not invent a consumer release tag.");
        identity.CheckoutCommit.ShouldBe(Git(root, "rev-parse", "HEAD"), "The actual historical-input fixture source must equal its authenticated checkout.");
        using JsonDocument tuple = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "eng", "builds-tuple.json")));
        identity.CatalogCommit.ShouldBe(tuple.RootElement.GetProperty("producer").GetProperty("sourceRevision").GetString());
        string seed = Path.Combine(Metadata("PlatformTestPublishedToolRoot"), "g4-host", "projects", "EventStore", "Host.csproj");
        File.Exists(seed).ShouldBeTrue("The actual published tool seed must be present.");
        string workspace = Path.Combine(Path.GetTempPath(), "platform-full-parties-" + Guid.NewGuid().ToString("N"));
        CompositionWorkspace.CreatePrivateDirectory(workspace);
        try
        {
            await SourceMappingMaterializer.WriteAsync(mapping, workspace, TestContext.Current.CancellationToken);
            SourceMapping.LoadVerified(Path.Combine(workspace, "source-mapping", "mapping.json"), mapping.ContentHash).ContentHash.ShouldBe(mapping.ContentHash);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(manifest));
            JsonElement declared = document.RootElement.GetProperty("modules")[0].GetProperty("tests").EnumerateArray().Single(test => test.GetProperty("name").GetString() == profile);
            string entry = declared.GetProperty("entry").GetString()!;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            int timeout = declared.GetProperty("executionTimeoutSeconds").GetInt32();
            ModuleTestSelection selected = new("parties", profile, declared.GetProperty("executor").GetString()!, entry,
                Hash(manifest), Hash(Path.Combine(active, entry)), timeout, declared.GetProperty("stopTimeoutSeconds").GetInt32(), now, now.AddSeconds(timeout), now.AddSeconds(timeout), "package");
            CompositionRunPlan plan = CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), workspace, Path.Combine(workspace, "dapr-home"),
                new CompositionRunPorts(20001, 20002, 20003, 20004, 20005, 20006, 20007, 20008, 20009, 20010, 20011, 20012, 20013, [20014], 20015, 20016, 20017, [new(20018, 20019, 20020, 20021)]), [], [],
                mapping, [manifest], "parties", typeof(PlatformAppModel).Assembly.Location) with
            {
                PlatformDeclarationHash = Hash(manifest), SelectedTest = selected, SelectedTests = [selected], StartupDeadlineUtc = now.AddSeconds(600),
            };
            PlatformCompositionOptions options = new() { PlatformRoot = identity.PlatformPath, Mapping = mapping, ActiveModule = "parties", DeclarationPaths = [manifest], RunPlan = plan, EventStoreSeedProject = seed };
            if (currentArtifacts)
            {
                cohort = PlatformPackageCatalogQualification.SelectCurrentOwnerCohort(options)
                    ?? throw new InvalidDataException("The current artifact fixture requires an independently authenticated complete tuple.");
                (cohort is PublishedCatalogArtifactCohort.CurrentOwnerCurrentName or PublishedCatalogArtifactCohort.CurrentOwnerMemories327Name).ShouldBeTrue();
                PlatformPackageCatalogQualification.HasQualifiedSdkBytes(cohort).ShouldBeTrue();
            }
            var declarations = PlatformDeclarationSet.Load(options);
            foreach (var pair in new[] { ("eventstore", currentArtifacts ? "4.2.0" : SelectedSdkVersion), ("tenants", currentArtifacts ? "7.0.7" : current || published ? "7.0.5" : "7.0.2"), ("memories", currentArtifacts ? PublishedCatalogArtifactCohort.CurrentOwnerMemoriesVersion(cohort) : current || published ? "3.0.25" : "3.0.23"), ("parties", currentArtifacts ? "3.1.1" : published || current && PlatformServerBindings.Version(options, "parties", manifest) == "3.1.0" ? "3.1.0" : "3.0.0") })
                PlatformServerBindings.Version(options, pair.Item1, manifest).ShouldBe(pair.Item2);
            // This is the existing producer's ordinary closure format, without an additive-owner MAC.
            plan = plan with { CompositionContentIdentity = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            {
                modules = declarations.OrderBy(module => PlatformDeclarationSet.Id(module.Declaration), StringComparer.Ordinal)
                    .Select(module => new { module.Declaration, SourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(module.Source))) }),
                selected.Mode, selected.DaprRuntimeVersion, retention = false,
            }))) };
            await CompositionDocumentStore.WriteAsync(Path.Combine(workspace, "plan.json"), plan, TestContext.Current.CancellationToken);
            return new(workspace, plan, seed);
        }
        catch
        {
            Directory.Delete(workspace, true);
            throw;
        }
    }

    /// <summary>Uses an explicitly prepared historical input tree while requiring every non-tuple source file to equal the collecting source.</summary>
    internal static string PreparedFixtureRoot()
    {
        string? prepared = typeof(PlatformPartiesPackageFixture).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => attribute.Key == "PlatformTestReaderWorkspaceRoot")?.Value;
        string root = string.IsNullOrWhiteSpace(prepared) ? PlatformTestRepository.Root : Path.GetFullPath(prepared);
        if (root == PlatformTestRepository.Root) return root;
        string collecting = Git(PlatformTestRepository.Root, "rev-parse", "HEAD");
        string selected = Git(root, "rev-parse", "HEAD");
        string[] changed = Git(root, "diff", "--name-only", collecting, selected).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string[] tupleInputs = ["eng/builds-tuple.json", "hexalith-platform.json", ".config/dotnet-tools.json",
            "src/Hexalith.Platform.Composition/Declarations/provenance.json", "references/Hexalith.Builds", "references/Hexalith.Parties", "references/Hexalith.EventStore",
            "eng/legacy-tuple-source.json", "eng/pre-extension-tuple-source.json"];
        changed.All(path => tupleInputs.Contains(path, StringComparer.Ordinal)).ShouldBeTrue("Historical fixture inputs must retain the exact current production, controls, workflows and helper source.");
        const string verify = "import pathlib, sys; root = pathlib.Path(sys.argv[1]); sys.path.insert(0, str(root / 'eng')); from package_reader_source import verify_source; verify_source(root, sys.argv[2], sys.argv[3])";
        var validation = CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("python3",
            ["-B", "-c", verify, PlatformTestRepository.Root, collecting, selected], PlatformTestRepository.Root, null),
            TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        validation.TimedOut.ShouldBeFalse();
        validation.OutputTruncated.ShouldBeFalse();
        validation.ExitCode.ShouldBe(0, "The production source verifier must authenticate all permitted tuple and descriptor differences: " + validation.ErrorOutput);
        Git(root, "status", "--porcelain", "--untracked-files=normal").ShouldBeEmpty("The explicitly prepared historical input checkout must be immutable and clean.");
        return root;
    }

    /// <summary>Persists the original parent claim binding before the published host reads and projects it.</summary>
    internal async Task PersistClaimStateAsync()
    {
        Plan = Plan with { ResourceClaimStateDirectory = Path.Combine(Workspace, "claim-state") };
        await CompositionDocumentStore.WriteAsync(Path.Combine(Workspace, "plan.json"), Plan, TestContext.Current.CancellationToken);
    }

    /// <summary>Runs the actual signed host's validation-only transport, stopping at an effect-free image-probe executable.</summary>
    /// <param name="completeCatalog">Whether the validation-only host proceeds through the real catalog using only the inert image probe.</param>
    /// <param name="signingKey">An explicit transport-key control; omission preserves the opaque real parent key.</param>
    internal async Task<CompositionProcessResult> RunPublishedValidationAsync(bool completeCatalog = false, string? signingKey = null)
    {
        string host = Path.Combine(Metadata("PlatformTestPublishedToolRoot"), "g4-host", "Hexalith.Builds.Module.AppHost.dll");
        File.Exists(host).ShouldBeTrue("The genuine published AppHost is mandatory; there is no source fallback.");
        var validationRuntime = PlatformPublishedValidationRuntime.Prepare(host, Workspace, Metadata("PlatformHistoricalPackageRoot"));
        host = validationRuntime.Host;
        _validationRuntimeVerify = validationRuntime.Verify;
        string probes = Path.Combine(Workspace, "validation-probes");
        Directory.CreateDirectory(probes);
        string executable = Path.Combine(probes, "docker");
        string log = Path.Combine(probes, "images.txt");
        // Never contacts a registry. Fail on the seventh exact inspect, before rendering or model allocation.
        string script = "#!/bin/sh\n[ \"$#\" = 3 ] && [ \"$1\" = manifest ] && [ \"$2\" = inspect ] || exit 90\n"
            + "case \"$3\" in\n" + string.Join("|", SelectedImages.Order(StringComparer.Ordinal)) + ") ;;\n*) exit 91 ;;\nesac\n"
            + "printf '%s\\n' \"$3\" >> '" + log + "'\n"
            + (completeCatalog ? string.Empty : "[ \"$(wc -l < '" + log + "')\" -lt " + SelectedImages.Count + " ] || exit 83\n")
            + "printf '{}\\n'\n";
        await File.WriteAllTextAsync(executable, script, TestContext.Current.CancellationToken);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var start = CompositionProcess.CreateStartInfo("dotnet", ["exec", host, "--validate-platform"], Workspace,
            new Dictionary<string, string>
            {
                [CompositionEnvironment.PlanPath] = Path.Combine(Workspace, "plan.json"),
                [CompositionEnvironment.SigningKey] = signingKey ?? _key,
                [CompositionEnvironment.RunId] = Plan.RunId,
                [CompositionEnvironment.DaprRuntimePath] = Path.Combine(Plan.DaprHome, ".dapr", "bin"),
                ["DOTNET_ENVIRONMENT"] = "Development",
                ["PATH"] = probes + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
            });
        TimeSpan timeout = TimeSpan.FromSeconds(45);
        if (completeCatalog)
        {
            DateTimeOffset deadline = Plan.StartupDeadlineUtc
                ?? throw new InvalidDataException("Complete catalog validation requires its original startup deadline.");
            timeout = CompositionStartupClock.Remaining(deadline, DateTimeOffset.UtcNow);
            if (timeout <= TimeSpan.Zero) throw new InvalidDataException("The published validation startup deadline has expired.");
        }
        CompositionProcessResult result = await CompositionProcess.RunAsync(start, timeout, TestContext.Current.CancellationToken);
        validationRuntime.Verify();
        result.Output.ShouldNotContain(_key);
        result.ErrorOutput.ShouldNotContain(_key);
        if (signingKey is not null)
        {
            result.Output.Contains(signingKey, StringComparison.Ordinal).ShouldBeFalse();
            result.ErrorOutput.Contains(signingKey, StringComparison.Ordinal).ShouldBeFalse();
        }
        // Both keys were checked absent; retain this bounded output before timeout/truncation assertions.
        TestContext.Current.TestOutputHelper!.WriteLine(result.Output + result.ErrorOutput);
        result.TimedOut.ShouldBeFalse();
        result.OutputTruncated.ShouldBeFalse();
        return result;
    }

    /// <summary>Checks unknown private runtime changes refuse, restoring only this fixture's own copies.</summary>
    internal void AssertValidationRuntimeIntegrity()
    {
        string copy = Path.Combine(Workspace, "validation-runtime-host");
        if (!Directory.Exists(copy)) return;
        Action verify = _validationRuntimeVerify ?? throw new InvalidOperationException("The validation runtime was not prepared.");
        foreach (string path in new[] { Path.Combine(copy, "Aspire.Hosting.dll"), Path.Combine(copy, "Hexalith.Builds.Module.AppHost.dll"),
            Path.Combine(copy, "Hexalith.Builds.Tooling.dll"), Path.Combine(copy, "Hexalith.Builds.Module.AppHost.deps.json"), Path.Combine(Workspace, "validation-runtime-overlay.json") })
        {
            byte[] original = File.ReadAllBytes(path);
            try
            {
                File.AppendAllText(path, "changed");
                Should.Throw<InvalidDataException>(verify);
            }
            finally { File.WriteAllBytes(path, original); }
            verify();
        }
        string extra = Path.Combine(copy, "unqualified-extra.dll");
        try
        {
            File.WriteAllText(extra, "unqualified");
            Should.Throw<InvalidDataException>(verify);
        }
        finally { File.Delete(extra); }
        verify();
    }

    /// <summary>Checks the real published transport reached every authenticated image identity without rendering or resource claims.</summary>
    internal void AssertPublishedValidationBoundary(CompositionProcessResult result)
    {
        result.ExitCode.ShouldBe(3); // Deliberate final image refusal, not service or native acceptance.
        result.ErrorOutput.ShouldNotContain("trusted parent plan/key handoff");
        result.ErrorOutput.ShouldContain("is unavailable from its released registry");
        File.ReadAllLines(Path.Combine(Workspace, "validation-probes", "images.txt")).Order(StringComparer.Ordinal)
            .ShouldBe(SelectedImages.Order(StringComparer.Ordinal));
        Directory.Exists(Path.Combine(Workspace, "platform-components")).ShouldBeFalse();
        if (Plan.ResourceClaimStateDirectory is not null) Directory.Exists(Plan.ResourceClaimStateDirectory).ShouldBeFalse();
    }

    /// <summary>Reaches all seven exact image-probe identities in the actual trusted entry, then stops before rendering or allocation.</summary>
    internal void AssertPositiveEntry()
    {
        var builder = Builder();
        HashSet<string> seen = new(StringComparer.Ordinal);
        Should.Throw<InvalidOperationException>(() => Compose(builder, options =>
        {
            Options = options;
            return options with { ImageProbeRunner = (start, timeout, _) =>
            {
                start.FileName.ShouldBe("docker");
                timeout.ShouldBe(TimeSpan.FromSeconds(20));
                start.ArgumentList.Take(2).ShouldBe(["manifest", "inspect"]);
                start.ArgumentList.Count.ShouldBe(3);
                Images.ShouldContain(start.ArgumentList[2]);
                seen.Add(start.ArgumentList[2]).ShouldBeTrue();
                if (seen.Count == Images.Count) throw new InvalidOperationException("Expected all seven full-Parties image identities; stop before rendering.");
                return Task.FromResult(new CompositionProcessResult(true, 0, "{}", false));
            } };
        })).Message.ShouldBe("Expected all seven full-Parties image identities; stop before rendering.");
        seen.Order(StringComparer.Ordinal).ShouldBe(Images.Order(StringComparer.Ordinal));
        Options.PartiesRunProof!.Matches(Options).ShouldBeTrue();
        Options.QualificationOwners.ShouldBeEmpty();
        Plan.QualificationOwners.ShouldBeNull();
        Plan.QualificationOwnersBinding.ShouldBeNull();
        PlatformAppModel.Prepare(Options).Select(server => PlatformDeclarationSet.Id(server.Module.Declaration) + "/" + server.ServerId)
            .Order(StringComparer.Ordinal).ShouldBe(["eventstore/gateway", "memories/clock", "memories/domain", "memories/server", "memories/telemetry", "parties/domain", "tenants/api", "tenants/domain"]);
        AssertNoEffects(builder);
    }

    /// <summary>Invokes the actual key-bearing entry with its opaque ordinary initial key.</summary>
    internal void Compose(IDistributedApplicationBuilder builder, Func<PlatformCompositionOptions, PlatformCompositionOptions> configure, CompositionRunPlan? suppliedPlan = null, string? signingKey = null)
        => PlatformAppModel.ComposeRun(builder, suppliedPlan ?? Plan, signingKey ?? _key, Seed, configure);

    /// <summary>Captures genuine measured private artifacts at the normal entry callback, without composing or starting resources.</summary>
    internal PlatformCompositionOptions CaptureCurrent()
    {
        var builder = Builder();
        Should.Throw<InvalidOperationException>(() => Compose(builder, options =>
        {
            Options = options;
            options.CurrentOwnerProof!.Matches(options).ShouldBeTrue();
            throw new InvalidOperationException("Expected measured runner authority; stop before resource effects.");
        })).Message.ShouldBe("Expected measured runner authority; stop before resource effects.");
        builder.Resources.ShouldBeEmpty();
        return Options;
    }

    /// <summary>Requires zero model resources and no rendering from an entry-only control.</summary>
    internal void AssertNoEffects(IDistributedApplicationBuilder builder)
    {
        builder.Resources.ShouldBeEmpty();
        Directory.Exists(Path.Combine(Workspace, "platform-components")).ShouldBeFalse();
    }

    /// <summary>Runs the actual production-generated public-package reader with this genuine mapping and proof.</summary>
    internal PlatformRoutingCatalog Generate() => PlatformRoutingCatalog.Generate(Options, PlatformAppModel.Prepare(Options), Path.Combine(Workspace, "platform-components"));

    /// <summary>Creates an unstarted Development model.</summary>
    internal static IDistributedApplicationBuilder Builder(string environment = "Development") => DistributedApplication.CreateBuilder(new DistributedApplicationOptions
    {
        Args = ["--environment=" + environment], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true, AllowUnsecuredTransport = true,
    });

    private static string Metadata(string key) => typeof(PlatformPartiesPackageFixture).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .SingleOrDefault(attribute => attribute.Key == key)?.Value ?? "";
    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Git(string root, params string[] arguments)
    {
        var result = CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("git", arguments, root, null), TimeSpan.FromSeconds(10), CancellationToken.None).GetAwaiter().GetResult();
        result.ExitCode.ShouldBe(0);
        return result.Output.Trim();
    }

    /// <summary>Retains existing process diagnostics outside the source tree before normal fixture cleanup.</summary>
    internal void RetainReaderDiagnostics()
    {
        string source = Path.Combine(Reader, "generation.log");
        if (!File.Exists(source)) return;
        try
        {
            string target = Path.Combine(Path.GetTempPath(), "hexalith-sdk42-reader-diagnostics", Path.GetFileName(Workspace));
            CompositionWorkspace.CreatePrivateDirectory(target);
            File.Copy(source, Path.Combine(target, "generation.log"), overwrite: false);
            Console.Error.WriteLine("Retained existing reader diagnostics: " + target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Reader diagnostic retention failed; the original test failure is preserved.");
        }
    }

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(Workspace, true);
}
