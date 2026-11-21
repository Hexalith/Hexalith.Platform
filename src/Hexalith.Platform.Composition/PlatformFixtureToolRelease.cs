namespace Hexalith.Platform.Composition;

using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Hexalith.Builds.Tooling.Runtime;

/// <summary>Authenticates the actual published runner and the materialized packaged gateway seed.</summary>
internal static partial class PlatformFixtureToolRelease
{
    private const string _package = "Hexalith.Builds.Module.Cli";
    private const string _tooling = "Hexalith.Builds.Tooling.dll";
    private const string _cli = "Hexalith.Builds.Module.Cli.dll";
    private const string _seedDll = "Hexalith.Builds.Module.EventStoreHost.dll";
    private const string _layout = "tools/net11.0/any/";
    private const string _appHost = _layout + "g4-host/";
    private const int _archiveBound = 200_000_000;
    private static readonly HttpClient _nuget = new(new HttpClientHandler { AllowAutoRedirect = false });

    internal static (string Seed, IReadOnlyDictionary<string, string> Settings) Verify(PlatformCompositionOptions options)
    {
        CompositionRunPlan plan = options.RunPlan ?? throw PlatformFixturePackageQualification.Refusal();
        string seed = Path.Combine(plan.Workspace, "hosts", "eventstore", PackagedHostProject.ProjectFileName);
        string seedDirectory = Path.GetDirectoryName(seed)!;
        if (options.EventStoreSeedProject != seed || plan.ExecutingTool is not { } executor)
            throw PlatformFixturePackageQualification.Refusal();
        string[] seedFiles = [PackagedHostProject.ProjectFileName, "Placeholder.cs", "PackagedHost.props"];
        if (Directory.EnumerateFileSystemEntries(seedDirectory).Any(path => !seedFiles.Contains(Path.GetFileName(path), StringComparer.Ordinal)))
            throw PlatformFixturePackageQualification.Refusal();

        string lockPath = Path.Combine(options.PlatformRoot, "eng", "builds-tool.lock.json");
        string tuplePath = Path.Combine(options.PlatformRoot, "eng", "builds-tuple.json");
        PlatformPartiesRunProof.RequireCommittedFile(options.PlatformRoot, lockPath);
        PlatformPartiesRunProof.RequireCommittedFile(options.PlatformRoot, tuplePath);
        using var lockDocument = JsonDocument.Parse(File.ReadAllBytes(lockPath));
        using var tuple = JsonDocument.Parse(File.ReadAllBytes(tuplePath));
        JsonElement pin = lockDocument.RootElement.GetProperty("packages").EnumerateArray().Single(value => value.GetProperty("id").GetString() == _package);
        string version = pin.GetProperty("version").GetString()!;
        string expected = pin.GetProperty("contentHash").GetString()!;
        if (!ReleaseVersion().IsMatch(version) || Convert.FromBase64String(expected).Length != 64
            || executor.Version != version || tuple.RootElement.GetProperty("version").GetString() != version || tuple.RootElement.GetProperty("releaseTag").GetString() != "v" + version)
            throw PlatformFixturePackageQualification.Refusal();

        string archive = Path.Combine(plan.Workspace, "fixture-builds-tool.nupkg");
        if (!File.Exists(archive)) RetrieveAsync(version, archive, plan.StartupDeadlineUtc).GetAwaiter().GetResult();
        FileInfo file = new(archive);
        if (!file.Exists || file.LinkTarget is not null || file.Length is <= 0 or > _archiveBound) throw PlatformFixturePackageQualification.Refusal();
        using FileStream content = file.OpenRead();
        if (Convert.ToBase64String(SHA512.HashData(content)) != expected) throw PlatformFixturePackageQualification.Refusal();
        content.Position = 0;
        using ZipArchive package = new(content);
        if (package.Entries.Select(member => member.FullName).Distinct(StringComparer.Ordinal).Count() != package.Entries.Count
            || package.GetEntry(".signature.p7s") is null) throw PlatformFixturePackageQualification.Refusal();

        using Stream nuspec = Member(package, _package + ".nuspec").Open();
        XElement metadata = XDocument.Load(nuspec).Root!.Elements().Single(element => element.Name.LocalName == "metadata");
        XElement repository = metadata.Elements().Single(element => element.Name.LocalName == "repository");
        if (metadata.Elements().Single(element => element.Name.LocalName == "id").Value != _package
            || metadata.Elements().Single(element => element.Name.LocalName == "version").Value != version
            || (string?)repository.Attribute("type") != "git"
            || ((string?)repository.Attribute("url"))?.TrimEnd('/') != "https://github.com/Hexalith/Hexalith.Builds"
            || (string?)repository.Attribute("commit") != options.Mapping.EffectiveCatalogCommit)
            throw PlatformFixturePackageQualification.Refusal();

        string tooling = BodyHash(package, _layout + _tooling);
        string cli = BodyHash(package, _layout + _cli);
        string manifest = JsonSerializer.Serialize(new
        {
            schema = "hexalith.executing-tool-manifest.v1", algorithm = "sha256",
            binaries = new[] { new { name = _cli, contentIdentity = "sha256:" + cli }, new { name = _tooling, contentIdentity = "sha256:" + tooling } },
        });
        if (executor.Manifest != manifest || executor.ContentIdentity != "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(manifest)))
            || PlatformFixturePackageQualification.Hash(typeof(CompositionFixtureProducer).Assembly.Location) != tooling)
            throw PlatformFixturePackageQualification.Refusal();

        RequireBody(package, _appHost + "Hexalith.Builds.Module.AppHost.dll", Assembly.GetEntryAssembly()?.Location ?? throw PlatformFixturePackageQualification.Refusal());
        string shim = _appHost + "projects/EventStore/";
        string binaryPrefix = _appHost + "bin/EventStoreHost/";
        RequireBody(package, shim + PackagedHostProject.ProjectFileName, seed);
        RequireBody(package, shim + "Placeholder.cs", Path.Combine(seedDirectory, "Placeholder.cs"));
        string binaryDirectory = Path.Combine(AppContext.BaseDirectory, "bin", "EventStoreHost");
        string props = Path.Combine(seedDirectory, "PackagedHost.props");
        _ = PlatformFixturePackageQualification.Hash(props);
        XElement properties = XDocument.Load(props).Root!;
        XElement? group = properties.Element("PropertyGroup");
        XElement? location = group?.Element("PackagedHostDirectory");
        string? supplied = (string?)location;
        if (supplied != Path.TrimEndingDirectorySeparator(binaryDirectory) + Path.DirectorySeparatorChar
            || properties.Name.LocalName != "Project" || properties.HasAttributes || group!.HasAttributes || location!.HasAttributes
            || properties.Elements().Count() != 1 || group.Elements().Count() != 1 || location.HasElements)
            throw PlatformFixturePackageQualification.Refusal();

        RequireBody(package, binaryPrefix + _seedDll, Path.Combine(binaryDirectory, _seedDll));
        Dictionary<string, string> settings = new(StringComparer.Ordinal);
        foreach (string configuration in new[] { "appsettings.json", "appsettings.Development.json" })
        {
            string path = Path.Combine(binaryDirectory, configuration);
            if (package.GetEntry(binaryPrefix + configuration) is { } entry)
            {
                RequireBody(package, entry.FullName, path);
                using StreamReader reader = new(entry.Open());
                settings.Add(configuration, reader.ReadToEnd());
            }
            else if (File.Exists(path))
            {
                throw PlatformFixturePackageQualification.Refusal();
            }
        }
        return (seed, settings.AsReadOnly());
    }

    private static async Task RetrieveAsync(string version, string destination, DateTimeOffset? deadline)
    {
        using var bound = new CancellationTokenSource(PlatformRoutingCatalog.ReaderTimeout(TimeSpan.FromSeconds(60), deadline, DateTimeOffset.UtcNow));
        string normalized = _package.ToLowerInvariant();
        string packageName = $"{normalized}.{version}.nupkg";
        Uri request = new($"https://api.nuget.org/v3-flatcontainer/{normalized}/{version}/{packageName}");
        using var initial = await _nuget.GetAsync(request,
            HttpCompletionOption.ResponseHeadersRead, bound.Token).ConfigureAwait(false);
        using var redirected = (int)initial.StatusCode is 301 or 302 or 303 or 307 or 308 && initial.Headers.Location is { } location
            && Uri.TryCreate(request, location, out Uri? resolved) && resolved is { Scheme: "https", IsDefaultPort: true, UserInfo.Length: 0, Fragment.Length: 0 }
            && ((resolved.Host == request.Host && resolved.AbsolutePath == request.AbsolutePath)
                || (resolved.Host == "globalcdn.nuget.org" && resolved.AbsolutePath == "/packages/" + packageName))
            ? await _nuget.GetAsync(resolved, HttpCompletionOption.ResponseHeadersRead, bound.Token).ConfigureAwait(false)
            : null;
        var response = redirected ?? initial;
        if (!response.IsSuccessStatusCode) throw PlatformFixturePackageQualification.Refusal();
        await using Stream source = await response.Content.ReadAsStreamAsync(bound.Token).ConfigureAwait(false);
        await using FileStream target = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        byte[] buffer = new byte[64 * 1024];
        int read;
        while ((read = await source.ReadAsync(buffer, bound.Token).ConfigureAwait(false)) > 0)
        {
            if (target.Length + read > _archiveBound) throw PlatformFixturePackageQualification.Refusal();
            await target.WriteAsync(buffer.AsMemory(0, read), bound.Token).ConfigureAwait(false);
        }
    }

    private static void RequireBody(ZipArchive package, string member, string path)
    {
        if (PlatformFixturePackageQualification.Hash(path) != BodyHash(package, member)) throw PlatformFixturePackageQualification.Refusal();
    }

    private static string BodyHash(ZipArchive package, string member)
    {
        using Stream stream = Member(package, member).Open();
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static ZipArchiveEntry Member(ZipArchive package, string member)
        => package.GetEntry(member) ?? throw PlatformFixturePackageQualification.Refusal();

    [GeneratedRegex(@"\A[0-9]+\.[0-9]+\.[0-9]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex ReleaseVersion();
}
