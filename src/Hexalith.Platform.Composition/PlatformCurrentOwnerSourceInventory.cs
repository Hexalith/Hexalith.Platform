namespace Hexalith.Platform.Composition;

using System.Diagnostics;
using System.Text;
using Hexalith.Builds.Tooling.Runtime;

/// <summary>Enumerates a bounded immutable owner tree without accepting truncated Git output or following working-tree links.</summary>
internal static class PlatformCurrentOwnerSourceInventory
{
    /// <summary>Reads all tracked owner blobs outside references from one exact revision, preserving NUL-delimited names.</summary>
    /// <param name="root">The already verified active owner root.</param>
    /// <param name="revision">The exact SHA-1 source revision being measured.</param>
    /// <param name="startupDeadlineUtc">The existing run's optional startup deadline, never extended by enumeration.</param>
    internal static IReadOnlyList<string> Read(string root, string revision, DateTimeOffset? startupDeadlineUtc)
    {
        if (!Path.IsPathFullyQualified(root) || !ObjectId(revision)) throw Refusal("input");
        if (startupDeadlineUtc is { } deadline && deadline <= DateTimeOffset.UtcNow) throw Refusal("budget");
        RequireDirectory(root);
        Stopwatch elapsed = Stopwatch.StartNew();
        Stack<(string ObjectId, string Prefix, int Depth)> pending = new();
        pending.Push((revision, "", 0));
        HashSet<string> files = new(StringComparer.Ordinal);
        int calls = 0;
        int entries = 0;
        while (pending.TryPop(out var tree))
        {
            TimeSpan remaining = Remaining();
            if (++calls > 4096) throw Refusal("call-limit");
            if (remaining <= TimeSpan.Zero) throw Refusal("budget");
            ProcessStartInfo start = CompositionProcess.CreateStartInfo("git", ["ls-tree", "-z", tree.ObjectId], root, null);
            start.StandardOutputEncoding = new UTF8Encoding(false, true);
            start.StandardErrorEncoding = new UTF8Encoding(false, true);
            CompositionProcessResult result;
            try
            {
                result = CompositionProcess.RunAsync(start,
                    remaining < TimeSpan.FromSeconds(10) ? remaining : TimeSpan.FromSeconds(10), CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (DecoderFallbackException) { throw Refusal("ls-tree encoding"); }
            if (!result.Started) throw Refusal("ls-tree start");
            if (result.TimedOut) throw Refusal("ls-tree timeout");
            if (result.ExitCode != 0) throw Refusal("ls-tree exit " + result.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (result.OutputTruncated) throw Refusal("ls-tree truncation");
            string output = result.Output;
            if (output.Length == 0) continue;
            if (output[^1] != '\0') throw Refusal("record");
            foreach (string record in output[..^1].Split('\0'))
            {
                if (++entries > 32768) throw Refusal("entry-limit");
                int separator = record.IndexOf('\t');
                if (separator < 0) throw Refusal("record");
                string[] metadata = record[..separator].Split(' ');
                string name = record[(separator + 1)..];
                if (metadata.Length != 3 || !ObjectId(metadata[2]) || name.Length == 0 || name is "." or ".."
                    || Path.IsPathRooted(name) || name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0) throw Refusal("name");
                string relative = tree.Prefix + name;
                if (relative.Length > 4096) throw Refusal("path-limit");
                // This is the existing foreign-source exclusion, not a dependency traversal.
                if (relative == "references" || relative.StartsWith("references/", StringComparison.Ordinal)) continue;
                if (metadata[0] == "040000" && metadata[1] == "tree")
                {
                    if (tree.Depth >= 64) throw Refusal("depth-limit");
                    RequireDirectory(Path.Combine(root, relative));
                    pending.Push((metadata[2], relative + "/", tree.Depth + 1));
                }
                else if (metadata[1] == "blob" && metadata[0] is "100644" or "100755" or "120000")
                {
                    if (!files.Add(relative)) throw Refusal("duplicate");
                }
                else if (metadata[0] != "160000" || metadata[1] != "commit") throw Refusal("mode");
            }
        }
        if (Remaining() <= TimeSpan.Zero) throw Refusal("budget");
        return files.Order(StringComparer.Ordinal).ToArray();

        TimeSpan Remaining()
        {
            TimeSpan cap = TimeSpan.FromSeconds(60) - elapsed.Elapsed;
            TimeSpan startup = startupDeadlineUtc is { } deadline ? deadline - DateTimeOffset.UtcNow : cap;
            return cap < startup ? cap : startup;
        }
    }

    private static bool ObjectId(string value) => value.Length == 40 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RequireDirectory(string path)
    {
        DirectoryInfo directory = new(path);
        if (!directory.Exists) throw Refusal("missing-directory");
        for (DirectoryInfo? current = directory; current is not null; current = current.Parent)
            if (current.LinkTarget is not null) throw Refusal("directory-link");
    }

    private static InvalidDataException Refusal(string reason) => new("The complete bounded current-owner source inventory is unavailable (" + reason + ").");
}
