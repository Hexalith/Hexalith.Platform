namespace Hexalith.Platform.Composition;

using System.Collections.Frozen;
using System.Security.Cryptography;
using Hexalith.Builds.Tooling.Runtime;

/// <summary>Retains one immutable owning revision and the complete eligible tracked input set through private preparation.</summary>
internal sealed class PlatformCurrentOwnerSourceState
{
    private readonly string _root;
    private readonly DateTimeOffset? _deadline;

    private PlatformCurrentOwnerSourceState(string root, string revision, DateTimeOffset? deadline, IReadOnlyDictionary<string, string> inputs)
    {
        _root = root;
        _deadline = deadline;
        Revision = revision;
        Inputs = inputs.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>Gets the revision captured before compilation, never a later live branch tip.</summary>
    internal string Revision { get; }

    /// <summary>Gets immutable absolute input paths and their measured bodies.</summary>
    internal IReadOnlyDictionary<string, string> Inputs { get; }

    /// <summary>Captures and verifies one complete eligible input set from the exact owning revision.</summary>
    internal static PlatformCurrentOwnerSourceState Capture(string root, string revision, DateTimeOffset? deadline)
    {
        var inputs = Eligible(root, revision, deadline).ToDictionary(path => path, Hash, StringComparer.Ordinal);
        PlatformCurrentOwnerSourceState state = new(root, revision, deadline, inputs);
        state.RequireUnchanged(root, revision);
        return state;
    }

    /// <summary>Rechecks source revision, staged/working tracked inputs, complete eligible paths and every original body.</summary>
    internal void RequireUnchanged(string root, string revision)
    {
        if (root != _root || revision != Revision || Git(root, "rev-parse", "HEAD").Output.Trim() != Revision)
            throw Refusal();
        // A new staged path can leave HEAD and all previously selected bodies unchanged.
        _ = Git(root, "diff", "--quiet", "HEAD", "--");
        string[] paths = Eligible(root, Revision, _deadline);
        if (!paths.SequenceEqual(Inputs.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || Inputs.Any(pair => Hash(pair.Key) != pair.Value)) throw Refusal();
        if (Git(root, "rev-parse", "HEAD").Output.Trim() != Revision) throw Refusal();
    }

    private static string[] Eligible(string root, string revision, DateTimeOffset? deadline)
        => PlatformCurrentOwnerSourceInventory.Read(root, revision, deadline)
            .Select(name => Path.Combine(root, name))
            .Where(path => Path.GetExtension(path) is ".cs" or ".csproj" or ".props" or ".targets" or ".json"
                || Path.GetFileName(path) is ".editorconfig" or ".gitattributes" or "nuget.config" or "NuGet.Config")
            .Order(StringComparer.Ordinal).ToArray();

    private static CompositionProcessResult Git(string root, params string[] arguments)
    {
        CompositionProcessResult result = CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("git", arguments, root, null),
            TimeSpan.FromSeconds(10), CancellationToken.None).GetAwaiter().GetResult();
        return result.Started && result.ExitCode == 0 && !result.TimedOut && !result.OutputTruncated ? result : throw Refusal();
    }

    private static string Hash(string path)
    {
        FileInfo file = new(path);
        if (!Path.IsPathFullyQualified(path) || !file.Exists || file.LinkTarget is not null || file.Length > 268435456) throw Refusal();
        for (DirectoryInfo? parent = file.Directory; parent is not null; parent = parent.Parent)
            if (parent.LinkTarget is not null) throw Refusal();
        return Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    }

    private static InvalidDataException Refusal() => new("The complete current-owner source revision, tracked inputs or measured bodies changed during preparation.");
}
