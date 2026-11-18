namespace Hexalith.Platform.Composition.Tests;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;

/// <summary>Reads the Builds pin files and the lock-verified tool archive; tests never restate a Builds release identity.</summary>
internal static class PlatformTestPins
{
    private const string ToolPackage = "Hexalith.Builds.Module.Cli";

    /// <summary>Gets the pinned tool version from <c>.config/dotnet-tools.json</c>.</summary>
    internal static string ToolVersion => Read(".config/dotnet-tools.json",
        root => root.GetProperty("tools").GetProperty("hexalith.builds.module.cli").GetProperty("version").GetString());

    /// <summary>Gets the admitted <c>[pin, next major)</c> range from <c>hexalith-platform.json</c>.</summary>
    internal static string ToolVersionRange => Read("hexalith-platform.json", root => root.GetProperty("toolVersionRange").GetString());

    /// <summary>Gets the version recorded by the schema 2 binding <c>eng/builds-tuple.json</c>.</summary>
    internal static string BindingVersion => Read("eng/builds-tuple.json", root => root.GetProperty("version").GetString());

    /// <summary>Gets the recorded Builds gitlink of the Platform checkout.</summary>
    internal static string BuildsGitlink => Git(PlatformTestRepository.Root, "rev-parse", "HEAD:references/Hexalith.Builds");

    /// <summary>Gets the locked NuGet contentHash of the pinned tool from <c>eng/builds-tool.lock.json</c>.</summary>
    internal static string ToolContentHash => Read("eng/builds-tool.lock.json", root => root.GetProperty("packages").EnumerateArray()
        .Single(entry => entry.GetProperty("id").GetString() == ToolPackage).GetProperty("contentHash").GetString());

    /// <summary>Gets the AR-12 expected identity: the SHA-256 of one member of the lock-verified tool archive.</summary>
    /// <param name="toolRoot">The extracted <c>published/tools/&lt;framework&gt;/any</c> root.</param>
    /// <param name="relative">The member path below the tool root.</param>
    internal static string LockedArchiveMemberHash(string toolRoot, string relative)
    {
        string output = Path.GetFullPath(Path.Combine(toolRoot, "..", "..", "..", ".."));
        string archive = Path.Combine(output, ToolPackage.ToLowerInvariant() + "." + ToolVersion + ".nupkg");
        byte[] bytes = File.ReadAllBytes(archive);
        if (Convert.ToBase64String(SHA512.HashData(bytes)) != ToolContentHash)
            throw new InvalidDataException("The prepared tool archive differs from eng/builds-tool.lock.json.");
        string prefix = Path.GetRelativePath(Path.Combine(output, "published"), Path.GetFullPath(toolRoot)).Replace(Path.DirectorySeparatorChar, '/');
        using ZipArchive package = new(new MemoryStream(bytes), ZipArchiveMode.Read);
        ZipArchiveEntry entry = package.GetEntry(prefix + "/" + relative.Replace(Path.DirectorySeparatorChar, '/'))
            ?? throw new InvalidDataException("The lock-verified tool archive has no member " + relative + ".");
        using Stream body = entry.Open();
        return Convert.ToHexStringLower(SHA256.HashData(body));
    }

    /// <summary>Runs Git in a repository and returns its trimmed output.</summary>
    internal static string Git(string root, params string[] arguments)
    {
        var result = CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("git", arguments, root, null),
            TimeSpan.FromSeconds(30), CancellationToken.None).GetAwaiter().GetResult();
        if (result.ExitCode != 0 || result.TimedOut || result.OutputTruncated)
            throw new InvalidOperationException("git " + string.Join(' ', arguments) + " failed: " + result.ErrorOutput);
        return result.Output.Trim();
    }

    private static string Read(string relative, Func<JsonElement, string?> select)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(PlatformTestRepository.Root, relative)));
        return select(document.RootElement) ?? throw new InvalidDataException(relative + " does not declare the selected pin.");
    }
}
