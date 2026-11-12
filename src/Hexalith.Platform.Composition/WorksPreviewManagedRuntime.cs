using System.Runtime.CompilerServices;
using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;

[assembly: InternalsVisibleTo("Hexalith.Platform.Works.Tests")]

namespace Hexalith.Platform.Composition;

/// <summary>Keeps the Development-only owner reader and its managed source resources on one finite shared JSON exporter.</summary>
internal static class WorksPreviewManagedRuntime
{
    /// <summary>The independently checked current shared Core and ASP.NET Core runtime.</summary>
    internal const string Version = "11.0.0";
    private const string _refusal = "Works Development requires the qualified shared .NET 11.0.0 Core/JSON runtime and highest installed Core/ASP.NET Core 11.0.0; other managed runtimes require qualification.";

    /// <summary>Checks the actual host and installed runtimes before the Development model creates resources or private run state.</summary>
    internal static void Require()
    {
        ValidateHost(Environment.Version, typeof(object).Assembly.Location,
            typeof(JsonElement).Assembly.GetName().Version, typeof(JsonElement).Assembly.Location);
        var result = CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet", ["--list-runtimes"], null, null),
            TimeSpan.FromSeconds(10), CancellationToken.None).GetAwaiter().GetResult();
        if (!result.Started || result.ExitCode != 0 || result.TimedOut || result.OutputTruncated)
            throw new InvalidOperationException(_refusal);
        ValidateInventory(result.Output);
    }

    /// <summary>Rejects package-local JSON and unqualified host runtimes even when their assembly names can bind.</summary>
    /// <param name="runtime">The actual executing Core runtime version.</param>
    /// <param name="coreLibrary">The actual loaded Core library path.</param>
    /// <param name="jsonVersion">The loaded JSON assembly identity.</param>
    /// <param name="jsonLibrary">The actual loaded JSON path.</param>
    internal static void ValidateHost(System.Version runtime, string coreLibrary, System.Version? jsonVersion, string jsonLibrary)
    {
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        DirectoryInfo directory = new(Path.GetDirectoryName(Path.GetFullPath(coreLibrary)) ?? throw new InvalidOperationException(_refusal));
        if (runtime != new System.Version(Version) || jsonVersion != new System.Version(11, 0, 0, 0)
            || directory.Name != Version || directory.Parent?.Name != "Microsoft.NETCore.App" || directory.Parent.Parent?.Name != "shared"
            || !Path.GetFileName(coreLibrary).Equals("System.Private.CoreLib.dll", comparison)
            || !Path.GetFullPath(jsonLibrary).Equals(Path.Combine(directory.FullName, "System.Text.Json.dll"), comparison))
            throw new InvalidOperationException(_refusal);
    }

    /// <summary>Bounds LatestMajor to the measured current Core and ASP.NET Core installations; missing or future inventories refuse.</summary>
    /// <param name="inventory">The actual dotnet --list-runtimes output.</param>
    internal static void ValidateInventory(string inventory)
    {
        foreach (string family in new[] { "Microsoft.NETCore.App", "Microsoft.AspNetCore.App" })
        {
            List<System.Version> versions = [];
            foreach (string line in inventory.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (fields[0] != family) continue;
                if (fields.Length < 3 || !fields[2].StartsWith('[') || !line.EndsWith(']')
                    || !System.Version.TryParse(fields[1], out System.Version? version))
                    throw new InvalidOperationException(_refusal);
                versions.Add(version);
            }
            if (versions.Count == 0 || versions.Max() != new System.Version(Version))
                throw new InvalidOperationException(_refusal);
        }
    }
}
