namespace Hexalith.Platform.Composition;

using System.Diagnostics;
using System.Security.Cryptography;

/// <summary>Verifies the owner approved direct source tuple before hosted graph selection.</summary>
internal static class PlatformHostedReferenceMapping
{
    private const string PlatformMainBaselineRevision = "c635a2dc22f404d97c9fe7e27f7e7f4c9017943b";
    private const string ReviewedCandidateRevision = "d891af161e9eb6eabc535e0ba50f910d0b323ca8";
    private const string MappingSha256 = "0246BC7B41B28BDE97199133B790477B8C9CDB4018EAFA6CDE888B6F5C26FEB4";

    private static readonly (string Name, string Revision, string DeclarationSha256)[] Owners =
    [
        ("EventStore", "a3dbec2f8b6150a3ee33dcbfc2b6bf2fec274625", "21eda6da824d5ae6135ff342dd8a5aae950b02420319b953a836dddeb00ce495"),
        ("Tenants", "b7824e4bba174b3a459364af24c99dedaf8f0738", "1588f3428ec29296c452fdd35bf95146142315aad3ad8ceb934e7a791f1fb522"),
        ("Memories", "8aee7a1323160cb39d7b1415256dbfdcb032939a", "56f6dc8134d3f7cea06910e923a4736a450e00241e8e4a565ac9226897881840"),
        ("Parties", "1d3576084ced39ec5104dac409becdbdd1c8635f", "86002eeb916c77bd753a3c618ce394125794baf421315604c9f551b762624504"),
    ];

    /// <summary>Returns four verified source roots in approved owner order.</summary>
    internal static IReadOnlyList<(string Name, string Root)> Verify(string platformRoot, IReadOnlyDictionary<string, string> sourceRoots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformRoot);
        ArgumentNullException.ThrowIfNull(sourceRoots);
        string platform = Path.GetFullPath(platformRoot);
        string mappingPath = Path.Combine(platform, "eng", "story-1-17-main-reference-mapping.json");
        if (!File.Exists(mappingPath) || !string.Equals(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(mappingPath))), MappingSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Hosted reference mapping is missing or differs from the approved owner record.");
        }

        string candidateRevision = Git(platform, "rev-parse", "HEAD");
        if (!string.Equals(candidateRevision, PlatformMainBaselineRevision, StringComparison.Ordinal)
            && !(string.Equals(Git(platform, "rev-parse", "HEAD^"), PlatformMainBaselineRevision, StringComparison.Ordinal)
                && string.Equals(Git(platform, "rev-parse", "HEAD^2"), ReviewedCandidateRevision, StringComparison.Ordinal)))
        {
            throw new InvalidDataException("Hosted reference Platform revision is outside the approved main baseline and reviewed merge candidate.");
        }

        if (sourceRoots.Count != Owners.Length)
        {
            throw new InvalidDataException("Hosted reference selection requires exactly four explicit owner source roots.");
        }

        List<(string Name, string Root)> verified = [];
        foreach ((string name, string revision, string declarationSha256) in Owners)
        {
            string gitlinkPath = "references/Hexalith." + name;
            if (!string.Equals(Git(platform, "rev-parse", "HEAD:" + gitlinkPath), revision, StringComparison.Ordinal)
                || !string.Equals(Git(platform, "rev-parse", ":" + gitlinkPath), revision, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Hosted reference {name} gitlink differs from the approved direct revision.");
            }

            if (!sourceRoots.TryGetValue(name, out string? selected) || string.IsNullOrWhiteSpace(selected) || !Path.IsPathFullyQualified(selected))
            {
                throw new InvalidDataException($"Hosted reference {name} requires an explicit absolute source root.");
            }

            string ownerRoot = Path.GetFullPath(selected);
            if (ownerRoot.StartsWith(Path.Combine(platform, "references") + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Hosted reference {name} source must be exported outside nested Platform references.");
            }

            if (!string.Equals(Git(ownerRoot, "rev-parse", "HEAD"), revision, StringComparison.Ordinal)
                || !string.Equals(Path.GetFullPath(Git(ownerRoot, "rev-parse", "--show-toplevel")), ownerRoot, StringComparison.Ordinal)
                || GitExitCode(ownerRoot, "diff", "--quiet", "HEAD", "--") != 0
                || !string.IsNullOrEmpty(Git(ownerRoot, "ls-files", "--others", "--exclude-standard"))
                || HasIgnoredExecutableSource(ownerRoot))
            {
                throw new InvalidDataException($"Hosted reference {name} source revision or tracked bytes differ from the approved export.");
            }

            string declarationPath = Path.Combine(ownerRoot, "module", "hexalith.module-manifest.v2.json");
            if (!File.Exists(declarationPath) || !string.Equals(
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(declarationPath))), declarationSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Hosted reference {name} declaration digest differs from the approved bytes.");
            }

            verified.Add((name, ownerRoot));
        }

        return verified;
    }

    private static string Git(string root, params string[] arguments)
    {
        (int exitCode, string output) = RunGit(root, arguments);
        if (exitCode != 0)
        {
            throw new InvalidDataException("Hosted reference source identity is unavailable from Git.");
        }

        return output.Trim();
    }

    private static int GitExitCode(string root, params string[] arguments) => RunGit(root, arguments).ExitCode;

    private static bool HasIgnoredExecutableSource(string root) => Git(root, "ls-files", "--others", "--ignored", "--exclude-standard", "--", "*.cs", "*.csproj", "*.props", "*.targets", "*.json")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Any(path => !path.Split('/').Any(segment => segment is "bin" or "obj" or "artifacts" or ".artifacts"));

    private static (int ExitCode, string Output) RunGit(string root, IReadOnlyList<string> arguments)
    {
        if (!Directory.Exists(root))
        {
            throw new InvalidDataException("Hosted reference source root does not exist.");
        }

        ProcessStartInfo start = new("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ?? throw new InvalidDataException("Git could not verify the hosted reference source.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidDataException("Git timed out while verifying the hosted reference source.");
        }

        Task.WaitAll(output, error);
        return (process.ExitCode, output.Result);
    }
}
