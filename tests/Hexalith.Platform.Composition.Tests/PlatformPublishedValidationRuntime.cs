namespace Hexalith.Platform.Composition.Tests;

using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Hexalith.Builds.Tooling.Runtime;

/// <summary>Prepares an effect-free test transport with one authenticated security dependency patch; grants no deployed runtime authority.</summary>
internal static class PlatformPublishedValidationRuntime
{
    private const string _oldHosting = "7ede5667cdfe3ae9fa91cc71ccd8ba1cbd4a96236c73706ba1562eab6549b06d";
    private const string _patchedHosting = "4833f3e80792a88d5b1ebd55366a3362776e8ca1513b8e7e8bb24834f38d5c98";
    private const string _patchedArchive = "57a52672d4ec2793d2cf1fe05c313cbb373c926ad133cad31c75ea0189dbc8cd";
    private const string _patchedSource = "b33d222049f1ecc3bc7257ec07b622fe550b2580";

    /// <summary>Keeps the signed AppHost/Tooling pair and every original file unchanged while a private copy loads the measured Aspire security patch.</summary>
    internal static (string Host, Action Verify) Prepare(string originalHost, string workspace, string packageRoot)
    {
        string originalRoot = Path.GetDirectoryName(originalHost)!;
        string tooling = Path.Combine(originalRoot, "Hexalith.Builds.Tooling.dll");
        if (!PlatformPartiesValidationContext.IsPublishedPair(Hash(originalHost), Hash(tooling))) throw Refusal();
        string originalHosting = Path.Combine(originalRoot, "Aspire.Hosting.dll");
        string originalHostingHash = Hash(originalHosting);
        if (originalHostingHash == _patchedHosting)
        {
            return (originalHost, () =>
            {
                if (!PlatformPartiesValidationContext.IsPublishedPair(Hash(originalHost), Hash(tooling)) || Hash(originalHosting) != _patchedHosting) throw Refusal();
            });
        }
        if (originalHostingHash != _oldHosting || AssemblyName.GetAssemblyName(originalHosting).Version != new Version(13, 6, 4, 0)) throw Refusal();
        string archive = Path.Combine(packageRoot, "aspire.hosting", "13.6.5", "aspire.hosting.13.6.5.nupkg");
        if (Hash(archive) != _patchedArchive) throw Refusal();
        var signature = CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet", ["nuget", "verify", archive, "--all"], workspace, null),
            TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(workspace, "validation-runtime-signature.log"), signature.Output + signature.ErrorOutput);
        if (signature.ExitCode != 0 || signature.TimedOut || signature.OutputTruncated) throw Refusal();
        byte[] patched;
        using (ZipArchive package = ZipFile.OpenRead(archive))
        {
            using Stream specification = package.GetEntry("Aspire.Hosting.nuspec")?.Open() ?? throw Refusal();
            XElement metadata = XDocument.Load(specification).Descendants().Single(element => element.Name.LocalName == "metadata");
            XElement repository = metadata.Elements().Single(element => element.Name.LocalName == "repository");
            if (metadata.Elements().Single(element => element.Name.LocalName == "id").Value != "Aspire.Hosting"
                || metadata.Elements().Single(element => element.Name.LocalName == "version").Value != "13.6.5"
                || (string?)repository.Attribute("url") != "https://github.com/microsoft/aspire"
                || (string?)repository.Attribute("commit") != _patchedSource) throw Refusal();
            using Stream body = package.GetEntry("lib/net8.0/Aspire.Hosting.dll")?.Open() ?? throw Refusal();
            using MemoryStream bytes = new();
            body.CopyTo(bytes);
            patched = bytes.ToArray();
            if (Convert.ToHexStringLower(SHA256.HashData(patched)) != _patchedHosting) throw Refusal();
        }
        string modelHosting = typeof(Aspire.Hosting.IDistributedApplicationBuilder).Assembly.Location;
        if (Hash(modelHosting) != _patchedHosting) throw Refusal();
        Dictionary<string, string> original = Files(originalRoot);
        string archiveDirectory = Path.GetFullPath(Path.Combine(originalRoot, "..", "..", "..", "..", ".."));
        string[] publishedArchives = Directory.GetFiles(archiveDirectory, "hexalith.builds.module.cli.*.nupkg");
        if (publishedArchives.Length != 1) throw Refusal();
        string publishedArchive = publishedArchives[0];
        string publishedArchiveHash = Hash(publishedArchive);
        var publishedSignature = CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet", ["nuget", "verify", publishedArchive, "--all"], workspace, null),
            TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(workspace, "validation-original-host-signature.log"), publishedSignature.Output + publishedSignature.ErrorOutput);
        if (publishedSignature.ExitCode != 0 || publishedSignature.TimedOut || publishedSignature.OutputTruncated) throw Refusal();
        Dictionary<string, string> signedOriginal = new(StringComparer.Ordinal);
        using (ZipArchive package = ZipFile.OpenRead(publishedArchive))
        {
            using Stream specification = package.GetEntry("Hexalith.Builds.Module.Cli.nuspec")?.Open() ?? throw Refusal();
            XElement metadata = XDocument.Load(specification).Descendants().Single(element => element.Name.LocalName == "metadata");
            XElement repository = metadata.Elements().Single(element => element.Name.LocalName == "repository");
            if (metadata.Elements().Single(element => element.Name.LocalName == "id").Value != "Hexalith.Builds.Module.Cli"
                || (string?)repository.Attribute("url") != "https://github.com/Hexalith/Hexalith.Builds") throw Refusal();
            const string prefix = "tools/net10.0/any/g4-host/";
            foreach (ZipArchiveEntry entry in package.Entries.Where(entry => entry.FullName.StartsWith(prefix, StringComparison.Ordinal) && !entry.FullName.EndsWith('/')))
            {
                string relative = entry.FullName[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
                using Stream body = entry.Open();
                signedOriginal.Add(relative, Convert.ToHexStringLower(SHA256.HashData(body)));
            }
        }
        if (!Equal(original, signedOriginal)) throw Refusal();
        string copy = Path.Combine(workspace, "validation-runtime-host");
        if (Directory.Exists(copy) || File.Exists(copy)) throw Refusal();
        CompositionWorkspace.CreatePrivateDirectory(copy);
        foreach (string relative in original.Keys)
        {
            string target = Path.Combine(copy, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(originalRoot, relative), target, overwrite: false);
        }
        string hosting = Path.Combine(copy, "Aspire.Hosting.dll");
        File.WriteAllBytes(hosting, patched);
        if (AssemblyName.GetAssemblyName(hosting).Version != new Version(13, 6, 5, 0)) throw Refusal();
        Dictionary<string, string> expected = new(original, StringComparer.Ordinal) { ["Aspire.Hosting.dll"] = _patchedHosting };
        string copiedHost = Path.Combine(copy, Path.GetFileName(originalHost));
        string receipt = Path.Combine(workspace, "validation-runtime-overlay.json");
        byte[] evidence = JsonSerializer.SerializeToUtf8Bytes(new
        {
            scope = "test-only signed validation entry with an authenticated security dependency overlay; exits before application Build/Run",
            candidateAcceptance = false, runtimeAcceptance = false, imageAcceptance = false,
            originalHost = new { path = originalHost, sha256 = original[Path.GetFileName(originalHost)] },
            originalTooling = new { path = tooling, sha256 = original["Hexalith.Builds.Tooling.dll"] },
            originalSignedArchiveSha256 = publishedArchiveHash,
            originalClosureSha256 = ClosureHash(original), validationClosureSha256 = ClosureHash(expected),
            changedFiles = new[] { "Aspire.Hosting.dll" }, fileCount = expected.Count,
            dependency = new { id = "Aspire.Hosting", version = "13.6.5", repository = "https://github.com/microsoft/aspire", sourceRevision = _patchedSource,
                url = "https://api.nuget.org/v3-flatcontainer/aspire.hosting/13.6.5/aspire.hosting.13.6.5.nupkg", archiveSha256 = _patchedArchive,
                originalSha256 = _oldHosting, patchedSha256 = _patchedHosting },
        });
        File.WriteAllBytes(receipt, evidence);
        void Verify()
        {
            if (!Equal(Files(originalRoot), original) || !Equal(Files(copy), expected)
                || !File.ReadAllBytes(receipt).AsSpan().SequenceEqual(evidence)
                || Hash(archive) != _patchedArchive || Hash(publishedArchive) != publishedArchiveHash || Hash(modelHosting) != _patchedHosting
                || !PlatformPartiesValidationContext.IsPublishedPair(Hash(copiedHost), Hash(Path.Combine(copy, "Hexalith.Builds.Tooling.dll")))) throw Refusal();
        }
        Verify();
        TestContext.Current.TestOutputHelper!.WriteLine(System.Text.Encoding.UTF8.GetString(evidence));
        return (copiedHost, Verify);
    }

    private static Dictionary<string, string> Files(string root)
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        Queue<string> directories = new();
        directories.Enqueue(root);
        while (directories.Count > 0)
        {
            string directory = directories.Dequeue();
            if (new DirectoryInfo(directory).LinkTarget is not null) throw Refusal();
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                if (Directory.Exists(path)) directories.Enqueue(path);
                else result.Add(Path.GetRelativePath(root, path), Hash(path));
            }
        }
        return result;
    }

    private static bool Equal(IReadOnlyDictionary<string, string> actual, IReadOnlyDictionary<string, string> expected)
        => actual.Count == expected.Count && expected.All(pair => actual.TryGetValue(pair.Key, out string? hash) && hash == pair.Value);
    private static string ClosureHash(IReadOnlyDictionary<string, string> files)
        => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(files.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray())));
    private static string Hash(string path)
    {
        FileInfo file = new(path);
        if (!file.Exists || file.LinkTarget is not null) throw Refusal();
        for (DirectoryInfo? directory = file.Directory; directory is not null; directory = directory.Parent)
            if (directory.LinkTarget is not null) throw Refusal();
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
    private static InvalidDataException Refusal() => new("The test-only published validation runtime requires its exact signed entry/tooling pair and authenticated security dependency; unknown changes refuse.");
}
