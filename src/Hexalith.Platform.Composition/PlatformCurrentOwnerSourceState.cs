namespace Hexalith.Platform.Composition;

using System.Collections.Frozen;
using Hexalith.Builds.Tooling.Runtime;

/// <summary>Retains one immutable owning revision and the complete eligible tracked input set through private preparation.</summary>
internal sealed class PlatformCurrentOwnerSourceState
{
    private readonly string _root;

    private PlatformCurrentOwnerSourceState(string root, string revision, IReadOnlyDictionary<string, string> inputs)
    {
        _root = root;
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
        deadline ??= DateTimeOffset.UtcNow.AddSeconds(60);
        var inputs = PlatformSourceStateWorker.Read(Eligible(root, revision, deadline), deadline.Value);
        PlatformCurrentOwnerSourceState state = new(root, revision, inputs);
        state.RequireUnchanged(root, revision, deadline);
        return state;
    }

    /// <summary>Rechecks source revision, staged/working tracked inputs, complete eligible paths and every original body.</summary>
    internal void RequireUnchanged(string root, string revision, DateTimeOffset? preparationDeadlineUtc = null)
    {
        preparationDeadlineUtc ??= DateTimeOffset.UtcNow.AddSeconds(60);
        if (root != _root || revision != Revision || Git(root, preparationDeadlineUtc.Value, "rev-parse", "HEAD").Output.Trim() != Revision)
            throw Refusal();
        // A new staged path can leave HEAD and all previously selected bodies unchanged.
        _ = Git(root, preparationDeadlineUtc.Value, "diff", "--quiet", "HEAD", "--");
        string[] paths = Eligible(root, Revision, preparationDeadlineUtc);
        if (!paths.SequenceEqual(Inputs.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || PlatformSourceStateWorker.Read(paths, preparationDeadlineUtc.Value).Any(pair => !Inputs.TryGetValue(pair.Key, out string? hash) || hash != pair.Value)) throw Refusal();
        if (Git(root, preparationDeadlineUtc.Value, "rev-parse", "HEAD").Output.Trim() != Revision) throw Refusal();
    }

    private static string[] Eligible(string root, string revision, DateTimeOffset? deadline)
        => PlatformCurrentOwnerSourceInventory.Read(root, revision, deadline)
            .Select(name => Path.Combine(root, name))
            .Where(path => Path.GetExtension(path) is ".cs" or ".csproj" or ".props" or ".targets" or ".json"
                || Path.GetFileName(path) is ".editorconfig" or ".gitattributes" or "nuget.config" or "NuGet.Config")
            .Order(StringComparer.Ordinal).ToArray();

    private static CompositionProcessResult Git(string root, DateTimeOffset deadline, params string[] arguments)
    {
        TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) throw Refusal();
        CompositionProcessResult result = CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("git", arguments, root, null),
            remaining < TimeSpan.FromSeconds(10) ? remaining : TimeSpan.FromSeconds(10), CancellationToken.None).GetAwaiter().GetResult();
        return result.Started && result.ExitCode == 0 && !result.TimedOut && !result.OutputTruncated ? result : throw Refusal();
    }

    private static InvalidDataException Refusal() => new("The complete current-owner source revision, tracked inputs or measured bodies changed during preparation.");
}
