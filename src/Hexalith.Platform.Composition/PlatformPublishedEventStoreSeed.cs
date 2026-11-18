namespace Hexalith.Platform.Composition;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Hexalith.Builds.Tooling.Runtime;

/// <summary>Supplies the signed .NET 10 EventStore 4.2.0 seed for the matching public application images.</summary>
internal static class PlatformPublishedEventStoreSeed
{
    private const string _package = "Hexalith.Builds.Module.Cli";
    private const string _version = "4.48.3";
    private const string _framework = "net10.0";
    private const string _repositoryCommit = "1ecbcd6c6f61629086d019c37557d10d4247b0c3";
    private const string _archiveSha256 = "28fa59cda1282349c6181bb32e20f7727fe4bffc8802b53591396cb3644e84a2";
    private const string _payload = "tools/" + _framework + "/any/g4-host/bin/EventStoreHost/";

    /// <summary>Restores the exact signed seed alongside a reader that uses the image cohort's schema exporter.</summary>
    internal static void BindReader(XElement project)
    {
        // Use the same ASP.NET Core shared framework as the published EventStore seed.
        project.Add(new XElement("PropertyGroup", new XElement("TargetFramework", _framework),
            new XElement("RollForward", "LatestPatch"), new XElement("RestoreEnablePackagePruning", "true")));
        project.Add(new XElement("ItemGroup", new XElement("FrameworkReference", new XAttribute("Include", "Microsoft.AspNetCore.App")),
            new XElement("PackageDownload", new XAttribute("Include", _package), new XAttribute("Version", "[" + _version + "]"))));
    }

    /// <summary>Verifies the sealed NuGet.org archive and extracts only its EventStore payload into the owning run.</summary>
    internal static string Extract(string readerRoot)
    {
        using var assets = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(readerRoot, "obj", "project.assets.json")));
        string id = _package.ToLowerInvariant();
        string archive = assets.RootElement.GetProperty("packageFolders").EnumerateObject()
            .Select(folder => Path.Combine(folder.Name, id, _version, id + "." + _version + ".nupkg"))
            .FirstOrDefault(File.Exists) ?? throw new InvalidDataException("The public EventStore seed package was not restored.");
        using FileStream bytes = File.OpenRead(archive);
        if (!Convert.ToHexStringLower(SHA256.HashData(bytes)).Equals(_archiveSha256, StringComparison.Ordinal))
            throw new InvalidDataException("The restored EventStore seed differs from the signed public release archive.");
        bytes.Position = 0;
        using var package = new ZipArchive(bytes, ZipArchiveMode.Read);
        // The same plan is composed once for preflight and again for the live AppHost.
        string directory = Path.Combine(readerRoot, "eventstore-seed-" + Guid.NewGuid().ToString("N"));
        CompositionWorkspace.CreatePrivateDirectory(directory);
        foreach (ZipArchiveEntry file in package.Entries.Where(entry => entry.FullName.StartsWith(_payload, StringComparison.Ordinal) && entry.Name.Length > 0))
        {
            string destination = Path.GetFullPath(Path.Combine(directory, file.FullName[_payload.Length..]));
            if (!destination.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidDataException("The public seed archive entry escapes the owning directory.");
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            file.ExtractToFile(destination);
        }
        const string assembly = "Hexalith.Builds.Module.EventStoreHost";
        if (!File.Exists(Path.Combine(directory, assembly + ".dll")))
            throw new InvalidDataException("The signed public seed has no managed EventStore host.");
        using var runtime = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, assembly + ".runtimeconfig.json")));
        if (runtime.RootElement.GetProperty("runtimeOptions").GetProperty("tfm").GetString() != _framework)
            throw new InvalidDataException("The public seed and catalog reader require the same framework.");
        return directory;
    }

    /// <summary>Records the same package identity that was checked before extracting its host.</summary>
    internal static object Provenance() => new { package = _package, version = _version, repositoryCommit = _repositoryCommit, archiveSha256 = _archiveSha256 };
}
