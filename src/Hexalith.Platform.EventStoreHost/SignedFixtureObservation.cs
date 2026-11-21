using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Records the actual package and assembly loaded by the finite fixture host.</summary>
internal static class SignedFixtureObservation
{
    private const string _package = "Hexalith.Platform.TestExtension";
    private const string _member = "lib/net11.0/Hexalith.Platform.TestExtension.dll";
    private const string _refusal = "The signed Platform fixture did not resolve and load its approved package assembly.";

    internal static void Record(IConfiguration configuration)
    {
        IConfigurationSection proof = configuration.GetSection("Platform:SignedFixtureQualification");
        if (!proof.Exists()) return;

        string workspace = proof["Workspace"] ?? throw new InvalidDataException(_refusal);
        string run = proof["RunId"] ?? throw new InvalidDataException(_refusal);
        string version = proof["Version"] ?? throw new InvalidDataException(_refusal);
        if (!Path.IsPathFullyQualified(workspace) || run.Length != 32 || run.Any(character => !Uri.IsHexDigit(character))
            || version != "2.2.5" || Environment.GetEnvironmentVariable("HEXALITH_G4_RUN_ID") != run)
            throw new InvalidDataException(_refusal);

        string identity = _package + "/" + version;
        string hostName = Assembly.GetEntryAssembly()?.GetName().Name ?? throw new InvalidDataException(_refusal);
        string assetsPath = Path.Combine(workspace, "project-builds", "eventstore", "obj", hostName, "project.assets.json");
        string depsPath = Path.Combine(AppContext.BaseDirectory, hostName + ".deps.json");
        using var assets = JsonDocument.Parse(File.ReadAllBytes(assetsPath));
        using var deps = JsonDocument.Parse(File.ReadAllBytes(depsPath));
        if (assets.RootElement.GetProperty("libraries").GetProperty(identity).GetProperty("type").GetString() != "package"
            || deps.RootElement.GetProperty("libraries").GetProperty(identity).GetProperty("type").GetString() != "package"
            || !assets.RootElement.GetProperty("targets").EnumerateObject().Any(target => HasRuntimeMember(target.Value, identity))
            || !deps.RootElement.GetProperty("targets").EnumerateObject().Any(target => HasRuntimeMember(target.Value, identity)))
            throw new InvalidDataException(_refusal);

        string name = _package.ToLowerInvariant();
        string restore = Path.Combine(name, version, name + "." + version + ".nupkg");
        string archive = assets.RootElement.GetProperty("packageFolders").EnumerateObject()
            .Select(folder => Path.Combine(folder.Name, restore)).Single(File.Exists);
        using FileStream raw = OpenRegularFile(archive);
        string archiveHash = Convert.ToHexStringLower(SHA256.HashData(raw));
        if (archiveHash != proof["ArchiveSha256"]) throw new InvalidDataException(_refusal);
        raw.Position = 0;
        using ZipArchive zip = new(raw);
        using Stream member = (zip.GetEntry(_member) ?? throw new InvalidDataException(_refusal)).Open();
        string expectedAssembly = Convert.ToHexStringLower(SHA256.HashData(member));
        Assembly loaded = AppDomain.CurrentDomain.GetAssemblies().Single(assembly => assembly.GetName().Name == _package);
        string expectedLocation = Path.Combine(AppContext.BaseDirectory, _package + ".dll");
        if (expectedAssembly != proof["LibrarySha256"] || loaded.Location != expectedLocation) throw new InvalidDataException(_refusal);
        using FileStream loadedFile = OpenRegularFile(loaded.Location);
        string observedAssembly = Convert.ToHexStringLower(SHA256.HashData(loadedFile));
        if (observedAssembly != expectedAssembly) throw new InvalidDataException(_refusal);

        byte[] observation = JsonSerializer.SerializeToUtf8Bytes(new
        {
            runId = run,
            packageId = _package,
            version,
            extensionId = "platform-test",
            apiMajor = 1,
            archiveSha256 = archiveHash,
            librarySha256 = observedAssembly,
            processId = Environment.ProcessId,
            loadedAssembly = loaded.Location,
            assetsType = "package",
            depsType = "package",
        });
        string report = Path.Combine(workspace, "fixture-host-loaded.json");
        string pending = report + "." + Environment.ProcessId + ".tmp";
        try
        {
            using (FileStream destination = new(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                destination.Write(observation);
            File.Move(pending, report);
        }
        finally
        {
            if (File.Exists(pending)) File.Delete(pending);
        }
    }

    private static bool HasRuntimeMember(JsonElement target, string identity)
        => target.TryGetProperty(identity, out var library) && library.TryGetProperty("runtime", out var runtime) && runtime.TryGetProperty(_member, out _);

    private static FileStream OpenRegularFile(string path)
    {
        FileInfo info = new(path);
        if (!info.Exists || info.LinkTarget is not null || info.Length <= 0) throw new InvalidDataException(_refusal);
        return info.OpenRead();
    }
}
