namespace Hexalith.Platform.CatalogArtifacts;

using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>One owner package the generated lock admits: its exact id, version and NuGet SHA-512 archive hash.</summary>
/// <param name="Id">The package id.</param>
/// <param name="Version">The exact package version.</param>
/// <param name="ContentHash">The base64 SHA-512 of the exact .nupkg bytes, equal to the nuget.org packageHash.</param>
internal sealed record OwnerPackageLockEntry(string Id, string Version, string ContentHash);

/// <summary>A runtime assembly that one owner package contributes, as a dependency or restore graph records it.</summary>
/// <param name="Assembly">The assembly simple name.</param>
/// <param name="PackageId">The contributing package id.</param>
/// <param name="Version">The contributing package version.</param>
/// <param name="LibraryPath">The package folder path relative to a package root.</param>
/// <param name="Member">The archive member path of the runtime assembly.</param>
internal sealed record OwnerPackageAsset(string Assembly, string PackageId, string Version, string LibraryPath, string Member);

/// <summary>The AR-12 identity of one loaded owner assembly: the lock entry, the lock-verified archive member and the loaded file.</summary>
/// <param name="Assembly">The assembly simple name.</param>
/// <param name="PackageId">The lock entry id.</param>
/// <param name="Version">The lock entry version.</param>
/// <param name="ContentHash">The lock entry contentHash, equal to the hash of the archive actually read.</param>
/// <param name="Archive">The verified archive path.</param>
/// <param name="ArchiveMember">The archive member the expected identity is derived from.</param>
/// <param name="ExpectedSha256">The SHA-256 of that archive member.</param>
/// <param name="LoadedPath">The loaded assembly path.</param>
/// <param name="LoadedSha256">The SHA-256 of the loaded assembly bytes.</param>
internal sealed record OwnerPackageIdentity(string Assembly, string PackageId, string Version, string ContentHash, string Archive,
    string ArchiveMember, string ExpectedSha256, string LoadedPath, string LoadedSha256);

/// <summary>The generated, CI-enforced owner package lock; it is the single integrity source for owner package admission.</summary>
/// <remarks>Run <c>python3 eng/owner_packages_lock.py</c> to regenerate it after an owner or Builds version bump; no code changes.</remarks>
internal sealed class OwnerPackageLock
{
    /// <summary>The lock location relative to the Platform root.</summary>
    internal const string RelativePath = "eng/owner-packages.lock.json";

    private const string _regenerate = "; regenerate it with python3 eng/owner_packages_lock.py.";
    private static readonly string[] _ownerPrefixes =
    [
        "Hexalith.EventStore.", "Hexalith.Tenants.", "Hexalith.Memories.", "Hexalith.Parties.", "Hexalith.McpCli.",
        "Hexalith.Conversations.", "Hexalith.Folders.", "Hexalith.Projects.",
    ];
    private readonly Dictionary<string, OwnerPackageLockEntry> _entries;

    private OwnerPackageLock(string path, string sha256, Dictionary<string, OwnerPackageLockEntry> entries)
    {
        Path = path;
        Sha256 = sha256;
        _entries = entries;
    }

    /// <summary>Gets the lock file path that was read.</summary>
    internal string Path { get; }

    /// <summary>Gets the SHA-256 of the lock bytes that were read.</summary>
    internal string Sha256 { get; }

    /// <summary>Gets the locked packages ordered by id.</summary>
    internal IReadOnlyList<OwnerPackageLockEntry> Entries => _entries.Values.OrderBy(entry => entry.Id, StringComparer.Ordinal).ToArray();

    /// <summary>Returns the lock path inside a Platform root.</summary>
    /// <param name="platformRoot">The Platform root.</param>
    internal static string PathFor(string platformRoot) => System.IO.Path.Combine(platformRoot, "eng", "owner-packages.lock.json");

    /// <summary>Recognizes owner package ids and the assembly names they contribute.</summary>
    /// <param name="name">The package id or assembly name.</param>
    internal static bool IsOwnerPackage(string? name) => name is not null && _ownerPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>Reads and strictly validates the lock; an absent or malformed lock refuses.</summary>
    /// <param name="path">The lock path.</param>
    internal static OwnerPackageLock Load(string path)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("The owner package lock " + path + " is missing or unreadable" + _regenerate, exception);
        }

        return Parse(bytes, path);
    }

    /// <summary>Validates lock bytes with the same rules as the generator.</summary>
    /// <param name="bytes">The lock bytes.</param>
    /// <param name="path">The path used in diagnostics.</param>
    internal static OwnerPackageLock Parse(byte[] bytes, string path)
    {
        Dictionary<string, OwnerPackageLockEntry> entries = new(StringComparer.Ordinal);
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
                    .SequenceEqual(["packages", "schemaVersion"], StringComparer.Ordinal) is false
                || root.GetProperty("schemaVersion").ValueKind != JsonValueKind.Number || root.GetProperty("schemaVersion").GetInt32() != 1
                || root.GetProperty("packages").ValueKind != JsonValueKind.Array || root.GetProperty("packages").GetArrayLength() == 0)
                throw new InvalidDataException("The owner package lock " + path + " must be schema 1 with exactly schemaVersion and a nonempty packages list.");
            foreach (JsonElement item in root.GetProperty("packages").EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || item.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
                        .SequenceEqual(["contentHash", "id", "version"], StringComparer.Ordinal) is false
                    || item.EnumerateObject().Any(property => property.Value.ValueKind != JsonValueKind.String || property.Value.GetString()!.Length == 0))
                    throw new InvalidDataException("Each owner package lock entry needs one unique {id, version, contentHash}.");
                OwnerPackageLockEntry entry = new(item.GetProperty("id").GetString()!, item.GetProperty("version").GetString()!, item.GetProperty("contentHash").GetString()!);
                if (!IsOwnerPackage(entry.Id))
                    throw new InvalidDataException("The owner package lock names " + entry.Id + ", which is not an owner package.");
                byte[] hash = new byte[64];
                if (!Convert.TryFromBase64String(entry.ContentHash, hash, out int written) || written != 64 || Convert.ToBase64String(hash) != entry.ContentHash)
                    throw new InvalidDataException("The owner package lock contentHash for " + entry.Id + " is not a base64 SHA-512.");
                if (!entries.TryAdd(entry.Id, entry))
                    throw new InvalidDataException("Each owner package lock entry needs one unique {id, version, contentHash}.");
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            throw new InvalidDataException("The owner package lock " + path + " is malformed" + _regenerate, exception);
        }

        return new(path, Convert.ToHexStringLower(SHA256.HashData(bytes)), entries);
    }

    /// <summary>Admits one exact package version; a missing entry or a stale version refuses naming the package.</summary>
    /// <param name="id">The package id.</param>
    /// <param name="version">The selected or loaded package version.</param>
    internal OwnerPackageLockEntry Require(string id, string version)
    {
        if (!_entries.TryGetValue(id, out OwnerPackageLockEntry? entry))
            throw new InvalidDataException("The owner package lock has no entry for " + id + " " + version + _regenerate);
        if (!string.Equals(entry.Version, version, StringComparison.Ordinal))
            throw new InvalidDataException("The owner package lock names " + id + " " + entry.Version + ", but " + version + " is selected" + _regenerate);
        return entry;
    }

    /// <summary>Requires every locked package of one owner family to name the selected version.</summary>
    /// <param name="prefix">The owner family id prefix.</param>
    /// <param name="version">The catalog-selected version.</param>
    internal void RequireFamily(string prefix, string version)
    {
        OwnerPackageLockEntry[] family = Entries.Where(entry => entry.Id.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        if (family.Length == 0)
            throw new InvalidDataException("The owner package lock has no entry for " + prefix + "* " + version + _regenerate);
        foreach (OwnerPackageLockEntry entry in family) _ = Require(entry.Id, version);
    }

    /// <summary>Maps each owner runtime assembly of a deps.json or project.assets.json document to its package.</summary>
    /// <param name="graph">The dependency or restore graph.</param>
    internal static IReadOnlyDictionary<string, OwnerPackageAsset> ReadAssets(JsonElement graph)
    {
        Dictionary<string, OwnerPackageAsset> assets = new(StringComparer.Ordinal);
        try
        {
            JsonElement libraries = graph.GetProperty("libraries");
            foreach (JsonProperty target in graph.GetProperty("targets").EnumerateObject())
            {
                foreach (JsonProperty library in target.Value.EnumerateObject())
                {
                    int slash = library.Name.IndexOf('/', StringComparison.Ordinal);
                    if (slash <= 0) continue;
                    string id = library.Name[..slash];
                    string version = library.Name[(slash + 1)..];
                    if (!IsOwnerPackage(id) || !libraries.TryGetProperty(library.Name, out JsonElement information)
                        || information.GetProperty("type").GetString() != "package"
                        || !library.Value.TryGetProperty("runtime", out JsonElement runtime))
                        continue;
                    string path = information.TryGetProperty("path", out JsonElement relative) && relative.ValueKind == JsonValueKind.String
                        ? relative.GetString()! : id.ToLowerInvariant() + "/" + version.ToLowerInvariant();
                    foreach (JsonProperty member in runtime.EnumerateObject())
                    {
                        if (!member.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
                        OwnerPackageAsset asset = new(System.IO.Path.GetFileNameWithoutExtension(member.Name), id, version, path, member.Name);
                        if (assets.TryGetValue(asset.Assembly, out OwnerPackageAsset? existing) && existing != asset)
                            throw new InvalidDataException("The dependency graph maps assembly '" + asset.Assembly + "' to more than one owner package.");
                        assets[asset.Assembly] = asset;
                    }
                }
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
        {
            throw new InvalidDataException("The owner dependency graph is malformed.", exception);
        }

        return assets;
    }

    /// <summary>Checks one loaded file against the lock-verified archive member of its package (AR-12).</summary>
    /// <param name="asset">The package identity of the loaded assembly.</param>
    /// <param name="packageFolders">The package folders the graph was restored into, in resolution order.</param>
    /// <param name="loadedPath">The loaded assembly path.</param>
    internal OwnerPackageIdentity Verify(OwnerPackageAsset asset, IReadOnlyList<string> packageFolders, string loadedPath)
    {
        OwnerPackageLockEntry entry = Require(asset.PackageId, asset.Version);
        string archive = packageFolders.Select(folder => System.IO.Path.Combine(folder, asset.LibraryPath, ArchiveName(asset.PackageId, asset.Version))).FirstOrDefault(File.Exists)
            ?? throw new InvalidDataException("The owner package " + asset.PackageId + " " + asset.Version + " archive is absent from the restored package folders.");
        string expected = LockedMember(entry, archive, asset.Member);
        string loaded = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(loadedPath)));
        if (!string.Equals(loaded, expected, StringComparison.Ordinal))
            throw new InvalidDataException("The loaded assembly '" + asset.Assembly + "' differs from its lock-verified package member "
                + asset.PackageId + " " + asset.Version + " " + asset.Member + ".");
        return new(asset.Assembly, asset.PackageId, asset.Version, entry.ContentHash, archive, asset.Member, expected, loadedPath, loaded);
    }

    /// <summary>Returns the NuGet global-folder archive file name of one package version.</summary>
    /// <param name="id">The package id.</param>
    /// <param name="version">The package version.</param>
    internal static string ArchiveName(string id, string version) => id.ToLowerInvariant() + "." + version.ToLowerInvariant() + ".nupkg";

    /// <summary>Checks an archive against its lock contentHash and returns the SHA-256 of one member of those same bytes.</summary>
    /// <param name="entry">The lock entry the archive must match.</param>
    /// <param name="archive">The archive path.</param>
    /// <param name="member">The archive member path.</param>
    private static string LockedMember(OwnerPackageLockEntry entry, string archive, string member)
    {
        byte[] bytes = File.ReadAllBytes(archive);
        string contentHash = Convert.ToBase64String(SHA512.HashData(bytes));
        if (!string.Equals(contentHash, entry.ContentHash, StringComparison.Ordinal))
            throw new InvalidDataException("The owner package " + entry.Id + " " + entry.Version + " archive has contentHash " + contentHash
                + ", which differs from " + RelativePath + ".");
        using ZipArchive package = new(new MemoryStream(bytes, writable: false), ZipArchiveMode.Read);
        ZipArchiveEntry body = package.GetEntry(member)
            ?? throw new InvalidDataException("The owner package " + entry.Id + " " + entry.Version + " has no member " + member + ".");
        using Stream content = body.Open();
        return Convert.ToHexStringLower(SHA256.HashData(content));
    }

    /// <summary>Verifies every owner runtime assembly that a graph places in one output directory, except run-bound bodies.</summary>
    /// <param name="graph">The output's dependency graph.</param>
    /// <param name="packageFolders">The package folders used by the restore.</param>
    /// <param name="directory">The output directory.</param>
    /// <param name="runBound">Assembly names whose bodies keep their run-bound authority.</param>
    internal IReadOnlyList<OwnerPackageIdentity> VerifyDirectory(JsonElement graph, IReadOnlyList<string> packageFolders, string directory, IReadOnlySet<string> runBound)
        => ReadAssets(graph).Values.Where(asset => !runBound.Contains(asset.Assembly))
            .Select(asset => (Asset: asset, Path: System.IO.Path.Combine(directory, System.IO.Path.GetFileName(asset.Member))))
            .Where(item => File.Exists(item.Path)).OrderBy(item => item.Asset.Assembly, StringComparer.Ordinal)
            .Select(item => Verify(item.Asset, packageFolders, item.Path)).ToArray();

    /// <summary>Verifies owner assemblies loaded in this process, mapping each through the application dependency graph.</summary>
    /// <param name="names">The owner assembly names this process loads.</param>
    /// <param name="packageFolders">The package folders used by the build that produced the loaded files.</param>
    internal IReadOnlyList<OwnerPackageIdentity> VerifyLoaded(IEnumerable<string> names, IReadOnlyList<string> packageFolders)
        => VerifyAssemblies(names.Distinct(StringComparer.Ordinal).Select(name =>
        {
            try
            {
                Assembly assembly = Assembly.Load(name);
                return assembly.GetName().Name == name ? assembly : throw new InvalidDataException("The loaded assembly '" + name + "' has another identity.");
            }
            catch (Exception exception) when (exception is IOException or BadImageFormatException)
            {
                throw new InvalidDataException("The owner assembly '" + name + "' is not loadable from its owner package.", exception);
            }
        }), packageFolders);

    /// <summary>Verifies already loaded owner assemblies by their actual file, mapping each through its dependency graph.</summary>
    /// <param name="assemblies">The loaded owner assemblies.</param>
    /// <param name="packageFolders">The package folders used by the build that produced the loaded files.</param>
    internal IReadOnlyList<OwnerPackageIdentity> VerifyAssemblies(IEnumerable<Assembly> assemblies, IReadOnlyList<string> packageFolders)
        => assemblies.DistinctBy(assembly => assembly.Location, StringComparer.Ordinal).OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal).Select(assembly =>
        {
            string name = assembly.GetName().Name!;
            string location = assembly.Location;
            if (assembly.IsDynamic || string.IsNullOrWhiteSpace(location))
                throw new InvalidDataException("The loaded assembly '" + name + "' has no file identity.");
            OwnerPackageAsset asset = DependencyGraphs(location).Select(graph => ReadAssets(graph).GetValueOrDefault(name))
                .FirstOrDefault(candidate => candidate is not null
                    && string.Equals(System.IO.Path.GetFileName(candidate.Member), System.IO.Path.GetFileName(location), StringComparison.Ordinal))
                ?? throw new InvalidDataException("The loaded assembly '" + name + "' has no owner package identity in its dependency graph.");
            return Verify(asset, packageFolders, location);
        }).ToArray();

    /// <summary>Requires each AR-12 record of a reader receipt to match this lock and the unchanged loaded bytes.</summary>
    /// <param name="receipt">The reader receipt.</param>
    /// <param name="runBound">Assembly names whose bodies keep their run-bound authority.</param>
    internal IReadOnlyList<OwnerPackageIdentity> VerifyReceipt(JsonElement receipt, IReadOnlySet<string> runBound)
    {
        Dictionary<string, OwnerPackageIdentity> records = new(StringComparer.Ordinal);
        try
        {
            if (receipt.GetProperty("ownerPackageLock").GetProperty("sha256").GetString() != Sha256)
                throw new InvalidDataException("The reader receipt was produced with another owner package lock.");
            foreach (JsonElement item in receipt.GetProperty("ownerPackages").EnumerateArray())
            {
                OwnerPackageIdentity record = item.Deserialize<OwnerPackageIdentity>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? throw new InvalidDataException("The reader receipt has an empty owner package record.");
                OwnerPackageLockEntry entry = Require(record.PackageId, record.Version);
                // Re-derive the expected identity from the named archive and member instead of trusting the recorded values.
                if (record.ContentHash != entry.ContentHash
                    || System.IO.Path.GetFileName(record.Archive) != ArchiveName(record.PackageId, record.Version)
                    || !string.Equals(System.IO.Path.GetFileName(record.ArchiveMember), record.Assembly + ".dll", StringComparison.OrdinalIgnoreCase)
                    || AssemblyName.GetAssemblyName(record.LoadedPath).Name != record.Assembly
                    || LockedMember(entry, record.Archive, record.ArchiveMember) != record.ExpectedSha256
                    || record.ExpectedSha256 != record.LoadedSha256
                    || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(record.LoadedPath))) != record.LoadedSha256
                    || !records.TryAdd(record.Assembly, record))
                    throw new InvalidDataException("The reader receipt for '" + record.Assembly + "' differs from its lock-verified package member.");
            }

            foreach (JsonElement artifact in receipt.GetProperty("loadedArtifacts").EnumerateArray())
            {
                string name = new AssemblyName(artifact.GetProperty("assemblyIdentity").GetString()!).Name!;
                if (!IsOwnerPackage(name) || runBound.Contains(name)) continue;
                if (!records.TryGetValue(name, out OwnerPackageIdentity? record) || record.LoadedPath != artifact.GetProperty("path").GetString()
                    || record.LoadedSha256 != artifact.GetProperty("sha256").GetString())
                    throw new InvalidDataException("The reader loaded '" + name + "' without its lock-verified package identity.");
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or IOException or ArgumentException
            or BadImageFormatException)
        {
            throw new InvalidDataException("The reader receipt has no complete owner package evidence.", exception);
        }

        if (records.Count == 0) throw new InvalidDataException("The reader receipt has no owner package evidence.");
        return records.Values.OrderBy(record => record.Assembly, StringComparer.Ordinal).ToArray();
    }

    /// <summary>Returns the package folders a build of this assembly restored into, in NuGet resolution order.</summary>
    /// <param name="metadataSource">An assembly carrying the build's OwnerPackageRoot metadata.</param>
    internal static IReadOnlyList<string> PackageFolders(Assembly? metadataSource = null)
    {
        string? built = metadataSource?.GetCustomAttributes<AssemblyMetadataAttribute>().SingleOrDefault(attribute => attribute.Key == "OwnerPackageRoot")?.Value;
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new[] { built, Environment.GetEnvironmentVariable("NUGET_PACKAGES"), profile.Length == 0 ? null : System.IO.Path.Combine(profile, ".nuget", "packages") }
            .Where(folder => !string.IsNullOrWhiteSpace(folder)).Select(folder => System.IO.Path.GetFullPath(folder!))
            .Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Orders the graphs that can map a loaded file: this verifier's own model output, then the application, then siblings.</summary>
    /// <param name="location">The loaded assembly path.</param>
    private static IEnumerable<JsonElement> DependencyGraphs(string location)
    {
        string directory = System.IO.Path.GetDirectoryName(location)!;
        string own = System.IO.Path.Combine(directory, typeof(OwnerPackageLock).Assembly.GetName().Name + ".deps.json");
        string[] application = (AppContext.GetData("APP_CONTEXT_DEPS_FILES") as string ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        IEnumerable<string> local = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.deps.json").Order(StringComparer.Ordinal) : [];
        foreach (string path in new[] { own }.Concat(application).Concat(local).Distinct(StringComparer.Ordinal).Where(File.Exists))
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
            yield return document.RootElement.Clone();
        }
    }
}
