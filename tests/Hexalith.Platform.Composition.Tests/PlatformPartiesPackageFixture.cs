namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Aspire.Hosting;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.Platform.CatalogArtifacts;
using Shouldly;

/// <summary>Uses candidate/tool inputs for measured private preparation and local fixture histories for ordinary proof-only controls.</summary>
internal sealed class PlatformPartiesPackageFixture : IDisposable
{
    private readonly string _key = CompositionSigningKey.Create();
    private Action? _validationRuntimeVerify;
    private IDisposable? _proofFixture;

    private PlatformPartiesPackageFixture(string workspace, CompositionRunPlan plan, string seed)
    {
        Workspace = workspace;
        Plan = plan;
        Seed = seed;
    }

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
    internal static string CurrentCohort => PublishedCatalogArtifactCohort.CurrentOwnerMemories327Name;
    internal static string EventStoreTestCohort => PublishedCatalogArtifactCohort.EventStorePackageTestCurrentName;

    internal static async Task<PlatformPartiesPackageFixture> CreateAsync(string profile = "lifecycle-package", bool sdk42 = false)
    {
        if (profile != PlatformCurrentOwnerProof.Profile)
        {
            // These local histories prove parent scope only. They are never a Platform release checkout.
            var proof = new PlatformPartiesProofFixture(platformOwned: true, profile: profile);
            return new(proof.Options.RunPlan!.Workspace, proof.Options.RunPlan, PublishedSeed()) { _proofFixture = proof };
        }
        if (sdk42) SelectedSdkVersion.ShouldBe("4.2.0");
        return await CreateFromCandidateAsync(profile);
    }

    /// <summary>Builds an ordinary or private plan from the actual candidate and its authenticated catalog.</summary>
    internal static async Task<PlatformPartiesPackageFixture> CreateFromCandidateAsync(string profile)
    {
        bool current = profile == PlatformCurrentOwnerProof.Profile;
        string cohort = PublishedCatalogArtifactCohort.CurrentOwnerCurrentName;
        PlatformPackageCatalogQualification.HasQualifiedSdkBytes(cohort).ShouldBeTrue();
        string root = PlatformTestRepository.Root;
        string active = Path.Combine(root, "references", "Hexalith.Parties");
        foreach (string repository in new[] { "Hexalith.Builds", "Hexalith.Parties" })
        {
            string marker = Path.Combine(root, "references", repository, ".git");
            (File.Exists(marker) || Directory.Exists(marker)).ShouldBeTrue("Direct dependencies must already be prepared; this test must not initialize them.");
        }
        string manifest = Path.Combine(active, "module", "hexalith.module-manifest.v2.json");
        SourceMapping mapping = await WorkspaceRootResolver.ResolveAsync(manifest, WorkspaceMode.Package, TestContext.Current.CancellationToken);
        mapping.Root.ShouldBe(root);
        mapping.Entries.Single(entry => entry.Identity == "Hexalith.Parties").Path.ShouldBe(active);
        mapping.ActiveModule.ShouldBe("Hexalith.Parties");
        PlatformIdentity identity = mapping.PlatformIdentity.ShouldNotBeNull();
        identity.IsPlatformRepository.ShouldBeTrue("This is the real Platform-owned qualification path; it does not invent a consumer release tag.");
        identity.CheckoutCommit.ShouldBe(Git(root, "rev-parse", "HEAD"), "The actual candidate source must equal its authenticated checkout.");
        identity.CatalogCommit.ShouldBe(PlatformTestPins.BuildsGitlink);
        identity.ToolVersionRange.ShouldBe(PlatformTestPins.ToolVersionRange);
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
            cohort = (current ? PlatformPackageCatalogQualification.SelectCurrentOwnerCohort(options)
                : PlatformPackageCatalogQualification.SelectCohort(options, fullParties: true))
                ?? throw new InvalidDataException("The candidate fixture requires an independently authenticated complete tuple.");
            if (current) (cohort is PublishedCatalogArtifactCohort.CurrentOwnerCurrentName or PublishedCatalogArtifactCohort.CurrentOwnerMemories327Name).ShouldBeTrue();
            else cohort.ShouldBe(PublishedCatalogArtifactCohort.PublishedPartiesMemories327Name);
            PlatformPackageCatalogQualification.HasQualifiedSdkBytes(cohort).ShouldBeTrue();
            var declarations = PlatformDeclarationSet.Load(options);
            foreach (var pair in new[] { ("eventstore", "4.2.0"), ("tenants", "7.0.7"),
                ("memories", current ? PublishedCatalogArtifactCohort.CurrentOwnerMemoriesVersion(cohort) : "3.0.27"), ("parties", "3.1.1") })
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

    private static string PublishedSeed()
    {
        string seed = Path.Combine(Metadata("PlatformTestPublishedToolRoot"), "g4-host", "projects", "EventStore", "Host.csproj");
        File.Exists(seed).ShouldBeTrue("The candidate's authenticated published seed is required.");
        return seed;
    }

    /// <summary>Captures ordinary scope proof from fixture-owned histories without image admission.</summary>
    internal PlatformCompositionOptions CaptureOrdinary()
    {
        var builder = Builder();
        Should.Throw<InvalidOperationException>(() => Compose(builder, options =>
        {
            Options = options;
            options.PartiesRunProof!.Matches(options).ShouldBeTrue();
            throw new InvalidOperationException("Expected ordinary parent proof before image admission.");
        })).Message.ShouldBe("Expected ordinary parent proof before image admission.");
        AssertNoEffects(builder);
        return Options;
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
        // Provide disposable registry protocol fixtures for the exact catalog-selected tags.
        PlatformCompositionOptions options = new() { PlatformRoot = Plan.SourceMapping!.PlatformIdentity!.PlatformPath, Mapping = Plan.SourceMapping,
            ActiveModule = "parties", DeclarationPaths = Plan.PlatformDeclarationPaths, RunPlan = Plan, EventStoreSeedProject = Seed };
        string script = "#!/bin/sh\n";
        foreach (string tag in DependencyTags(options))
        {
            var evidence = ImageEvidence(tag, index: true);
            string prefix = Path.Combine(probes, Path.GetFileName(tag).Replace(':', '-'));
            File.WriteAllText(prefix + ".tag", evidence.Tag);
            File.WriteAllText(prefix + ".manifest", evidence.Manifest);
            script += "if [ \"$1\" = buildx ] && [ \"$4\" = '" + tag + "' ]; then printf '%s\\n' \"$4\" >> '" + log
                + "'; cat '" + prefix + ".tag'; exit; fi\n"
                + "if [ \"$1\" = manifest ] && [ \"$4\" = '" + evidence.Reference + "' ]; then cat '" + prefix + ".manifest'; exit; fi\n";
        }
        script += "exit 91\n";
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

    /// <summary>Reads the dependency repository tags selected by the mapped catalog.</summary>
    internal static IEnumerable<string> DependencyTags(PlatformCompositionOptions options)
        => PlatformServerBindings.ReferenceImages.Where(pair => !pair.Key.StartsWith("parties/", StringComparison.Ordinal))
            .Select(pair => pair.Value + ":" + PlatformServerBindings.Version(options, pair.Key.Split('/')[0], "fixture"));

    /// <summary>Exercises image resolution with registry-shaped bodies whose digest is computed from the response.</summary>
    internal static (string Tag, string Manifest, string Reference, string ImageId) ImageEvidence(string image, bool index)
    {
        string config = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(image)));
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 2, config = new { digest = config } });
        string digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(body));
        var platform = new { os = "linux", architecture = "amd64" };
        string tag = index ? JsonSerializer.Serialize(new { digest = "sha256:" + new string('a', 64), manifests = new[] { new { digest, platform } } })
            : JsonSerializer.Serialize(new { digest });
        string reference = image[..image.LastIndexOf(':')] + "@" + digest;
        string manifest = JsonSerializer.Serialize(new { Ref = reference, Descriptor = new { digest, platform }, Raw = body });
        return (tag, manifest, reference, config);
    }

    /// <summary>Checks that topology binds project owners and catalog-derived tags before registry resolution.</summary>
    internal static void AssertCatalogSelectedDependencies(IReadOnlyList<PlatformServerBinding> servers)
    {
        servers.Count(server => server.Image is not null).ShouldBe(6);
        servers.ShouldAllBe(server => server.ImageDigest == null);
        servers.Single(server => server.ResourceId == "parties").Image.ShouldBeNull();
        servers.Single(server => server.ResourceId == "eventstore").Image.ShouldBeNull();
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
    public void Dispose()
    {
        if (_proofFixture is not null) _proofFixture.Dispose();
        else Directory.Delete(Workspace, true);
    }
}
