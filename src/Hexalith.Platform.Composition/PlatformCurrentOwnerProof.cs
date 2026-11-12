namespace Hexalith.Platform.Composition;

using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.Platform.CatalogArtifacts;

/// <summary>Private runner authority for one measured current owner; never candidate or public-package authority.</summary>
internal sealed class PlatformCurrentOwnerProof
{
    internal const string Profile = "platform-current-package";
    internal const string DeclarationHash = "d9c193b0e7b61601b8fcceb906b5dafe4769a1b9d7538a6ce164b4aa0840b348";
    /// <summary>The independently measured current source declaration; the historical/public hash remains unchanged.</summary>
    internal const string CurrentDeclarationHash = "dfb03b6e397944c4f24737f4ac9278fba033399a0efa30a80447c9112709efee";
    internal const string TestEntry = "tests/Hexalith.Parties.Platform.IntegrationTests/Hexalith.Parties.Platform.IntegrationTests.csproj";
    private readonly PlatformPartiesRunProof _parent;
    private readonly IReadOnlyDictionary<string, string> _files;
    private readonly string _receipt;
    private readonly string _seal;
    private readonly byte[] _key;
    private readonly PlatformCurrentOwnerSourceState _source;

    private PlatformCurrentOwnerProof(PlatformCompositionOptions options, string key, string directory, IReadOnlyDictionary<string, string> bodies,
        IReadOnlyDictionary<string, string> files, PlatformCurrentOwnerSourceState source)
    {
        _parent = PlatformPartiesRunProof.CreateCurrent(options, key);
        source.RequireUnchanged(ActiveRoot(options), source.Revision, options.RunPlan!.StartupDeadlineUtc);
        _source = source;
        Directory = directory;
        Cohort = PlatformPackageCatalogQualification.SelectCurrentOwnerCohort(options) ?? throw Refusal();
        Bodies = bodies;
        PackageArchives = files.Where(pair => pair.Key.EndsWith(".nupkg", StringComparison.Ordinal)).ToDictionary(pair => Path.GetFileName(pair.Key), pair => pair.Value, StringComparer.Ordinal);
        _files = files;
        _key = Convert.FromBase64String(key);
        _receipt = JsonSerializer.Serialize(new
        {
            profile = Profile, revision = source.Revision, options.Mapping.ContentHash,
            options.RunPlan!.RunId, options.RunPlan.AttemptId, options.RunPlan.EnvironmentId,
            platform = options.Mapping.PlatformIdentity, configuration = "Release", cohort = Cohort, sdkVersion = SdkVersion, bodies, files,
        });
        _seal = Convert.ToHexStringLower(HMACSHA256.HashData(_key, System.Text.Encoding.UTF8.GetBytes(_receipt)));
    }

    /// <summary>Optional isolated test observation after real measurement; cannot replace artifacts or grant authority.</summary>
    internal static AsyncLocal<Action?> PreparedObserver { get; } = new();

    internal string Cohort { get; }
    internal string SelectedDeclarationHash => DeclarationHashForCohort(Cohort);

    /// <summary>Selects exact current-source declaration bytes without relabeling a historical/public declaration.</summary>
    internal static string DeclarationHashForCohort(string cohort) => cohort switch
    {
        PublishedCatalogArtifactCohort.CurrentOwnerName or PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name => DeclarationHash,
        PublishedCatalogArtifactCohort.CurrentOwnerCurrentName or PublishedCatalogArtifactCohort.CurrentOwnerMemories327Name => CurrentDeclarationHash,
        _ => throw Refusal(),
    };
    internal string SdkVersion => PublishedCatalogArtifactCohort.CurrentOwnerSdkVersion(Cohort);
    internal string Directory { get; }
    internal string Artifacts => Path.Combine(Directory, "build");
    internal string MappingWorkspace => Path.Combine(Directory, "owner-mapping");
    internal string Cache => Path.Combine(Directory, "reader-packages");
    internal string ReaderConfig => Path.Combine(Directory, "reader.nuget.config");
    /// <summary>Gets the immutable pre-build source identity retained by the proof and readiness observation.</summary>
    internal string SourceRevision => _source.Revision;
    internal IReadOnlyDictionary<string, string> Bodies { get; }
    internal IReadOnlyDictionary<string, string> PackageArchives { get; }

    internal static PlatformCurrentOwnerProof Create(PlatformCompositionOptions options, string key, bool developmentRun)
    {
        string? cohort = null;
        if (!developmentRun || !OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64
            || options.Mapping.Entries.Any(entry => entry.Identity is not ("Hexalith.Parties" or "Hexalith.Platform") && (entry.Origin != "package" || entry.Path is not null))
            || options.Projects != PlatformServerBindings.ReferenceProjects || options.TimesheetsEnabled || options.UsePrebuiltProjects
            || !PublishedCatalogArtifactCohort.IsCurrentOwnerCohort(cohort = PlatformPackageCatalogQualification.SelectCurrentOwnerCohort(options))
            || !PlatformPartiesPackageCatalogQualification.MatchesPartiesVersion(PlatformServerBindings.Version(options, "parties", "current owner"), true, cohort!))
            throw Refusal();
        _ = PlatformPartiesRunProof.CreateCurrent(options, key);
        string sdkVersion = PublishedCatalogArtifactCohort.CurrentOwnerSdkVersion(cohort!);
        string active = ActiveRoot(options);
        string revision = Git(active, "rev-parse", "HEAD");
        PlatformCurrentOwnerSourceState source = PlatformCurrentOwnerSourceState.Capture(active, revision, options.RunPlan!.StartupDeadlineUtc);
        IReadOnlyDictionary<string, string> inputs = source.Inputs;
        string declaration = options.DeclarationPaths.Single();
        VerifyDeclaration(declaration, cohort!, source);
        string project = OwnerProject(options);
        string directory = Path.Combine(options.RunPlan!.Workspace, "current-owner", Guid.NewGuid().ToString("N"));
        CompositionWorkspace.CreatePrivateDirectory(directory);
        string artifacts = Path.Combine(directory, "build");
        string cache = Path.Combine(directory, "build-packages");
        string feed = Path.Combine(directory, "feed");
        CompositionWorkspace.CreatePrivateDirectory(feed);
        string publicConfig = Path.Combine(directory, "public.nuget.config");
        File.WriteAllText(publicConfig, "<configuration><packageSources><clear/><add key=\"public\" value=\"https://api.nuget.org/v3/index.json\"/></packageSources></configuration>");
        string readerConfig = Path.Combine(directory, "reader.nuget.config");
        new System.Xml.Linq.XDocument(new System.Xml.Linq.XElement("configuration",
            new System.Xml.Linq.XElement("packageSources", new System.Xml.Linq.XElement("clear"),
                new System.Xml.Linq.XElement("add", new System.Xml.Linq.XAttribute("key", "current"), new System.Xml.Linq.XAttribute("value", feed)),
                new System.Xml.Linq.XElement("add", new System.Xml.Linq.XAttribute("key", "public"), new System.Xml.Linq.XAttribute("value", "https://api.nuget.org/v3/index.json"))),
            new System.Xml.Linq.XElement("packageSourceMapping",
                new System.Xml.Linq.XElement("packageSource", new System.Xml.Linq.XAttribute("key", "current"),
                    new System.Xml.Linq.XElement("package", new System.Xml.Linq.XAttribute("pattern", "Hexalith.Parties.Contracts")),
                    new System.Xml.Linq.XElement("package", new System.Xml.Linq.XAttribute("pattern", "Hexalith.Parties.EventStoreExtensions"))),
                new System.Xml.Linq.XElement("packageSource", new System.Xml.Linq.XAttribute("key", "public"),
                    new System.Xml.Linq.XElement("package", new System.Xml.Linq.XAttribute("pattern", "*")))))).Save(readerConfig);
        string ownerMapping = Path.Combine(directory, "owner-mapping");
        SourceMappingMaterializer.WriteAsync(options.Mapping, ownerMapping, CancellationToken.None).GetAwaiter().GetResult();
        if (Hash(Path.Combine(ownerMapping, "source-mapping", "pinned-tree", "Props", "Directory.Packages.props"))
            != Hash(Path.Combine(options.RunPlan.Workspace, "source-mapping", "pinned-tree", "Props", "Directory.Packages.props"))) throw Refusal();
        Dictionary<string, string> environment = new(SourceMappingMaterializer.Environment(options.Mapping, ownerMapping), StringComparer.Ordinal)
        { ["NUGET_PACKAGES"] = cache };
        string version = PlatformServerBindings.Version(options, "parties", "current owner");
        string[] arguments = ["-c", "Release", "--artifacts-path", artifacts, "-p:GeneratePackageOnBuild=false",
            "-p:HexalithPinnedModelCatalogBuild=true",
            "-p:HexalithPartiesPackageVersion=" + version, "-p:ContinuousIntegrationBuild=true", "-p:RepositoryCommit=" + revision, "-p:RepositoryUrl=https://github.com/Hexalith/Hexalith.Parties", "-p:RestoreConfigFile=" + publicConfig,
            "-m:1", "-nodeReuse:false", "--disable-build-servers", "--nologo"];
        Run(["build", project, .. arguments], "runtime-build.log");
        foreach (string name in new[] { "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions" })
        {
            string ownProject = Path.Combine(active, "src", name, name + ".csproj");
            RequireOwned(options, ownProject);
            Run(["build", ownProject, .. arguments], name + "-build.log");
            Run(["pack", ownProject, "--no-build", "--output", feed, .. arguments], name + "-pack.log");
        }
        string output = Path.Combine(artifacts, "bin", "Hexalith.Parties", "release");
        foreach (string name in new[] { "Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults", "Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService" })
        {
            string path = Path.Combine(output, name + ".dll");
            PublishedCatalogArtifactCohort.VerifyCurrentSdkFile(path, Hash(path), cohort!);
        }
        Dictionary<string, string> bodies = new(StringComparer.Ordinal);
        Dictionary<string, string> files = new(inputs, StringComparer.Ordinal);
        foreach (string path in System.IO.Directory.GetFiles(output, "*")) files[path] = Hash(path);
        foreach (string name in new[] { "Hexalith.Parties", "Hexalith.Parties.Contracts", "Hexalith.Parties.Projections", "Hexalith.Parties.Authentication", "Hexalith.Parties.Security" })
            bodies[name] = files[Path.Combine(output, name + ".dll")];
        foreach (string name in new[] { "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions" })
        {
            string body = Path.Combine(artifacts, "bin", name, "release", name + ".dll");
            string archive = Path.Combine(feed, name + "." + version + ".nupkg");
            using ZipArchive package = ZipFile.OpenRead(archive);
            using Stream nuspec = package.GetEntry(name + ".nuspec")?.Open() ?? throw Refusal();
            var metadata = System.Xml.Linq.XDocument.Load(nuspec).Descendants().Single(element => element.Name.LocalName == "metadata");
            var repository = metadata.Elements().Single(element => element.Name.LocalName == "repository");
            if (metadata.Elements().Single(element => element.Name.LocalName == "id").Value != name
                || metadata.Elements().Single(element => element.Name.LocalName == "version").Value != version
                || (string?)repository.Attribute("commit") != revision
                || (string?)repository.Attribute("url") != "https://github.com/Hexalith/Hexalith.Parties") throw Refusal();
            using Stream embedded = package.GetEntry("lib/net10.0/" + name + ".dll")?.Open() ?? throw Refusal();
            string digest = Convert.ToHexStringLower(SHA256.HashData(embedded));
            if (digest != Hash(body) || bodies.TryGetValue(name, out string? runtime) && runtime != digest) throw Refusal();
            bodies[name] = digest;
            files[body] = digest;
            files[archive] = Hash(archive);
        }
        foreach (string path in new[] { publicConfig, readerConfig, project }) files[path] = Hash(path);
        // Every project dependency must remain owned; reference repositories are package-only.
        foreach (string asset in System.IO.Directory.GetFiles(Path.Combine(artifacts, "obj"), "project.assets.json", SearchOption.AllDirectories))
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(asset));
            foreach (JsonProperty library in document.RootElement.GetProperty("libraries").EnumerateObject())
            {
                string name = library.Name.Split('/')[0];
                string actualVersion = library.Name.Split('/')[1];
                if (library.Value.GetProperty("type").GetString() == "package"
                    && (name.StartsWith("Hexalith.Parties.", StringComparison.Ordinal)
                        || name.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal) && actualVersion != sdkVersion
                        || name.StartsWith("Hexalith.Tenants.", StringComparison.Ordinal) && actualVersion != PublishedCatalogArtifactCohort.CurrentOwnerTenantsVersion(cohort!)
                        || name.StartsWith("Hexalith.Memories.", StringComparison.Ordinal) && actualVersion != PublishedCatalogArtifactCohort.CurrentOwnerMemoriesVersion(cohort!)
                        || name == "Hexalith.McpCli.Abstractions" && actualVersion != PublishedCatalogArtifactCohort.CurrentOwnerMcpCliVersion(cohort!))) throw Refusal();
                if (library.Value.GetProperty("type").GetString() == "project")
                {
                    string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(document.RootElement.GetProperty("project").GetProperty("restore").GetProperty("projectPath").GetString()!)!, library.Value.GetProperty("msbuildProject").GetString()!));
                    RequireOwned(options, path);
                }
            }
            files[asset] = Hash(asset);
        }
        foreach (string path in System.IO.Directory.GetFiles(ownerMapping, "*", SearchOption.AllDirectories)) files[path] = Hash(path);
        source.RequireUnchanged(active, revision, options.RunPlan.StartupDeadlineUtc);
        string readerPreflight = Path.Combine(directory, "reader-preflight");
        CompositionWorkspace.CreatePrivateDirectory(readerPreflight);
        _ = PlatformPackageCatalogReader.Prepare(new System.Xml.Linq.XDocument(new System.Xml.Linq.XElement("Project")), options,
            options.RunPlan.Workspace, readerPreflight, DateTimeOffset.UtcNow + PlatformRoutingCatalog.ReaderTimeout(TimeSpan.FromMinutes(5), options.RunPlan.StartupDeadlineUtc, DateTimeOffset.UtcNow));
        VerifyPreparedRevision(active, revision, source, options.RunPlan.StartupDeadlineUtc);
        return new(options, key, directory, bodies, files, source);

        void Run(string[] command, string log)
        {
            TimeSpan timeout = PlatformRoutingCatalog.ReaderTimeout(TimeSpan.FromMinutes(5), options.RunPlan.StartupDeadlineUtc, DateTimeOffset.UtcNow);
            CompositionProcessResult result = CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet", command, active, environment), timeout, CancellationToken.None).GetAwaiter().GetResult();
            File.WriteAllText(Path.Combine(directory, log), result.Output + result.ErrorOutput);
            if (result.ExitCode != 0 || result.TimedOut || result.OutputTruncated) throw PreparationFailure(log, result);
        }
    }

    /// <summary>Retains only finite preparation phases, process status and bounded public diagnostic codes.</summary>
    internal static InvalidDataException PreparationFailure(string log, CompositionProcessResult result)
    {
        string phase = log switch
        {
            "runtime-build.log" => "runtime-build",
            "Hexalith.Parties.Contracts-build.log" => "contracts-build",
            "Hexalith.Parties.Contracts-pack.log" => "contracts-pack",
            "Hexalith.Parties.EventStoreExtensions-build.log" => "extensions-build",
            "Hexalith.Parties.EventStoreExtensions-pack.log" => "extensions-pack",
            _ => "unknown",
        };
        string[] codes = Regex.Matches(result.Output + "\n" + result.ErrorOutput,
            @"\b(?:HXW[0-9]{3}|NU[0-9]{4}|CS[0-9]{4}|MSB[0-9]{4})\b", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)
            .Select(match => match.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(8).ToArray();
        return new InvalidDataException(FormattableString.Invariant(
            $"Current owner Release preparation failed: phase={phase}; started={result.Started}; exit={result.ExitCode}; timedOut={result.TimedOut}; truncated={result.OutputTruncated}; codes={(codes.Length == 0 ? "none" : string.Join(',', codes))}."));
    }

    /// <summary>Requires the pinned revision after final real reader preparation, before any proof can be issued.</summary>
    /// <param name="root">The actual active owner root.</param>
    /// <param name="revision">The immutable revision whose inputs and bodies were measured.</param>
    /// <param name="source">The complete pre-build input state, or a fresh component-only boundary capture.</param>
    /// <param name="startupDeadlineUtc">The existing preparation deadline; it does not add expiry to the retained source identity.</param>
    internal static void VerifyPreparedRevision(string root, string revision, PlatformCurrentOwnerSourceState? source = null, DateTimeOffset? startupDeadlineUtc = null)
    {
        source ??= PlatformCurrentOwnerSourceState.Capture(root, revision, startupDeadlineUtc);
        PreparedObserver.Value?.Invoke();
        source.RequireUnchanged(root, revision, startupDeadlineUtc);
    }

    internal void VerifyReader(JsonElement receipt)
    {
        var artifacts = receipt.GetProperty("loadedArtifacts").EnumerateArray().ToArray();
        foreach (string name in new[] { "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions" })
        {
            var matches = artifacts.Where(item => new System.Reflection.AssemblyName(item.GetProperty("assemblyIdentity").GetString()!).Name == name).ToArray();
            if (matches.Length != 1 || matches[0].GetProperty("sha256").GetString() != Bodies[name]
                || Hash(matches[0].GetProperty("path").GetString()!) != Bodies[name]) throw Refusal();
        }
        PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt(artifacts.Select(item => (item.GetProperty("path").GetString()!, item.GetProperty("sha256").GetString()!)).ToArray(), Cohort);
    }

    /// <summary>Requires the selected declaration to belong to the already measured immutable source and exact purpose.</summary>
    internal static void VerifyDeclaration(string path, string cohort, PlatformCurrentOwnerSourceState source)
    {
        if (!source.Inputs.TryGetValue(path, out string? measured)
            || measured != DeclarationHashForCohort(cohort) || Hash(path) != measured) throw Refusal();
    }

    internal bool Matches(PlatformCompositionOptions options)
    {
        try
        {
            if (!_parent.MatchesCurrent(options) || options.Projects != PlatformServerBindings.ReferenceProjects
                || PlatformPackageCatalogQualification.SelectCurrentOwnerCohort(options) != Cohort) return false;
            _source.RequireUnchanged(ActiveRoot(options), SourceRevision);
            return _files.All(pair => Hash(pair.Key) == pair.Value)
                && Convert.ToHexStringLower(HMACSHA256.HashData(_key, System.Text.Encoding.UTF8.GetBytes(_receipt))) == _seal;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException) { return false; }
    }

    internal static string OwnerProject(PlatformCompositionOptions options)
    {
        string project = Path.Combine(ActiveRoot(options), "src", "Hexalith.Parties", "Hexalith.Parties.csproj");
        RequireOwned(options, project);
        return project;
    }

    private static string ActiveRoot(PlatformCompositionOptions options) => options.Mapping.Entries.Single(entry => entry.Identity == "Hexalith.Parties" && entry.Origin == "source").Path ?? throw Refusal();
    private static void RequireOwned(PlatformCompositionOptions options, string path)
    {
        SourceMapping owner = options.Mapping with { Entries = options.Mapping.Entries.Where(entry => entry.Identity == "Hexalith.Parties").ToArray(), PlatformIdentity = null };
        if (!owner.ContainsProject(path) || Git(Path.GetDirectoryName(path)!, "rev-parse", "--show-toplevel") != ActiveRoot(options)) throw Refusal();
        _ = Hash(path);
    }
    private static string Git(string root, params string[] arguments)
    {
        CompositionProcessResult result = CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("git", arguments, root, null), TimeSpan.FromSeconds(10), CancellationToken.None).GetAwaiter().GetResult();
        return result.ExitCode == 0 && !result.TimedOut && !result.OutputTruncated ? result.Output.Trim() : throw Refusal();
    }
    private static string Hash(string path)
    {
        FileInfo file = new(path);
        if (!Path.IsPathFullyQualified(path) || !file.Exists || file.LinkTarget is not null || file.Length > 268435456) throw Refusal();
        for (DirectoryInfo? parent = file.Directory; parent is not null; parent = parent.Parent) if (parent.LinkTarget is not null) throw Refusal();
        return Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    }
    private static InvalidDataException Refusal() => new("The current-owner package profile requires its private runner authority and exact Release/public dependency artifacts.");
}
