namespace Hexalith.Platform.Composition;

using System.Diagnostics;
using System.Security.Cryptography;

/// <summary>Verifies the owner approved direct source tuple before hosted graph selection.</summary>
internal static class PlatformHostedReferenceMapping
{
    private const string PlatformRevision = "3d344e850471b6a21fc4649df7ede9a0390689b1";
    private const string MappingSha256 = "46A4BEF075AF48791EC3F0BC4E4336DE8597C36406CCAD2F3AB5183E438B41CD";

    private static readonly (string Name, string Revision, string DeclarationSha256)[] Owners =
    [
        ("EventStore", "7c93a1234d14c66f5ee85ed48caaee1d30a671f3", "21eda6da824d5ae6135ff342dd8a5aae950b02420319b953a836dddeb00ce495"),
        ("Tenants", "a01e89854f711d1a93ae15695b438424c1ed4f0f", "1588f3428ec29296c452fdd35bf95146142315aad3ad8ceb934e7a791f1fb522"),
        ("Memories", "e8956eb5c1bd49297f86702f9606eb9b013bc2f1", "56f6dc8134d3f7cea06910e923a4736a450e00241e8e4a565ac9226897881840"),
        ("Parties", "f4285584dce0dfa08e3336fca3c203a1b5e748c6", "86002eeb916c77bd753a3c618ce394125794baf421315604c9f551b762624504"),
    ];

    /// <summary>Returns four verified source roots in approved owner order.</summary>
    internal static IReadOnlyList<(string Name, string Root)> Verify(string platformRoot, IReadOnlyDictionary<string, string> sourceRoots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformRoot);
        ArgumentNullException.ThrowIfNull(sourceRoots);
        string platform = Path.GetFullPath(platformRoot);
        string mappingPath = Path.Combine(platform, "eng", "story-1-17-reference-mapping.json");
        if (!File.Exists(mappingPath) || !string.Equals(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(mappingPath))), MappingSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Hosted reference mapping is missing or differs from the approved owner record.");
        }

        string candidateRevision = Git(platform, "rev-parse", "HEAD");
        if (!string.Equals(candidateRevision, PlatformRevision, StringComparison.Ordinal)
            && !string.Equals(Git(platform, "rev-parse", "HEAD^"), PlatformRevision, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Hosted reference Platform revision is outside the approved baseline and its immediate candidate commit.");
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
