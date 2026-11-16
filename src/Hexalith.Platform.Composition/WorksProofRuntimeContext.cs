using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

[assembly: InternalsVisibleTo("Hexalith.Works.IntegrationTests")]

namespace Hexalith.Platform.Composition;

/// <summary>Freezes the explicit synthetic proof import and its already compiled consumer closure.</summary>
internal sealed class WorksProofRuntimeContext
{
    private readonly string _manifest;
    private readonly string _manifestDigest;
    private readonly string _targets;
    private readonly string _assembly;
    private readonly string _consumerDirectory;
    private readonly Dictionary<string, string> _inputs;
    private readonly HashSet<string> _consumerFiles;
    private static readonly StringComparer _paths = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private WorksProofRuntimeContext(string manifest, string worksRoot)
    {
        _manifest = Absolute(manifest);
        byte[] bytes = File.ReadAllBytes(_manifest);
        if (bytes.Length > 16 * 1024 * 1024) throw new InvalidOperationException("The Works proof runtime manifest exceeds its finite input limit.");
        _manifestDigest = Digest(bytes);
        using var document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        string[] expected = ["schema", "targetsPath", "targetsSha256", "assemblyPath", "assemblySha256", "files"];
        if (root.ValueKind != JsonValueKind.Object || !root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || root.GetProperty("schema").GetString() != "hexalith.works-proof-runtime.v1")
            throw new InvalidOperationException("The Works proof runtime manifest has an unsupported schema or fields.");
        _targets = Absolute(root.GetProperty("targetsPath").GetString()!);
        string selectedTargets = Path.GetFullPath(Path.Combine(worksRoot, "tests", "Hexalith.Works.IntegrationTests", "WorksPlatformProofRuntime.targets"));
        if (!_paths.Equals(_targets, selectedTargets)) throw new InvalidOperationException("The Works proof runtime import must be the selected Works owner's fixed fixture target.");
        _assembly = Absolute(root.GetProperty("assemblyPath").GetString()!);
        if (Path.GetFileName(_assembly) != "Hexalith.Works.IntegrationTests.dll")
            throw new InvalidOperationException("The Works proof runtime requires the existing compiled integration fixture.");
        _consumerDirectory = Path.GetDirectoryName(_assembly)!;
        _inputs = new Dictionary<string, string>(_paths) { [_targets] = Hash(root.GetProperty("targetsSha256")) };
        _consumerFiles = new HashSet<string>(_paths);
        JsonElement files = root.GetProperty("files");
        if (files.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("The Works proof consumer inventory must be an object.");
        foreach (JsonProperty file in files.EnumerateObject())
        {
            string path = Absolute(file.Name);
            if (!path.StartsWith(_consumerDirectory + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
                || !_consumerFiles.Add(path) || !_inputs.TryAdd(path, Hash(file.Value)))
                throw new InvalidOperationException("The Works proof inventory contains a duplicate or foreign consumer artifact.");
        }
        if (!_inputs.TryGetValue(_assembly, out string? assemblyDigest) || assemblyDigest != Hash(root.GetProperty("assemblySha256"))
            || !_consumerFiles.Contains(Path.ChangeExtension(_assembly, ".deps.json"))
            || !_consumerFiles.Contains(Path.ChangeExtension(_assembly, ".runtimeconfig.json")))
            throw new InvalidOperationException("The Works proof manifest must bind its compiled assembly and actual runtime dependency files.");
        Verify();
    }

    /// <summary>Loads only an explicitly selected proof context; ordinary hosts acquire no fixture dependency.</summary>
    internal static WorksProofRuntimeContext? Load(string? manifest, string worksRoot)
        => string.IsNullOrEmpty(manifest) ? null : new WorksProofRuntimeContext(manifest, worksRoot);

    /// <summary>Declares the immutable proof import in the selected child's ordinary build environment.</summary>
#pragma warning disable ASPIREEXTENSION001
    internal void Configure(ProjectLaunchConfiguration launch)
    {
        Verify();
        launch.BuildEnvironment = new Dictionary<string, string>(launch.BuildEnvironment ?? [], StringComparer.Ordinal);
        Set("CustomAfterMicrosoftCommonTargets", _targets);
        Set("HexalithWorksProofAssembly", _assembly);
        void Set(string key, string value)
        {
            if (launch.BuildEnvironment.TryGetValue(key, out string? existing) && !string.IsNullOrEmpty(existing) && existing != value)
                throw new InvalidOperationException("The selected Works proof build context conflicts with " + key + ".");
            launch.BuildEnvironment[key] = value;
        }
    }

#pragma warning restore ASPIREEXTENSION001

    /// <summary>Refuses any changed manifest, import, consumer body or dependency inventory.</summary>
    internal void Verify()
    {
        if (!Ordinary(_manifest) || Digest(File.ReadAllBytes(_manifest)) != _manifestDigest
            || _inputs.Any(input => !Ordinary(input.Key) || Digest(File.ReadAllBytes(input.Key)) != input.Value)
            || !Directory.EnumerateFiles(_consumerDirectory, "*", SearchOption.AllDirectories).ToHashSet(_paths).SetEquals(_consumerFiles))
            throw new InvalidOperationException("The original Works proof runtime manifest, import or compiled consumer closure changed.");
    }

    /// <summary>Matches only the exact already bound fixture DLL copied into an enrolled host's ordinary output.</summary>
    internal bool MatchesProofAssembly(string path)
    {
        Verify();
        return Path.GetFileName(path) == "Hexalith.Works.IntegrationTests.dll" && Ordinary(path)
            && Digest(File.ReadAllBytes(path)) == _inputs[_assembly];
    }

    /// <summary>Authenticates an original artifact by its exact path and frozen consumer inventory entry.</summary>
    internal bool MatchesConsumerArtifact(string path)
    {
        Verify();
        return _consumerFiles.Contains(path) && Ordinary(path) && Digest(File.ReadAllBytes(path)) == _inputs[path];
    }

    /// <summary>Retains the same original build inputs immediately before any enrolled host starts.</summary>
    internal void ObserveStarts(IDistributedApplicationBuilder builder, IEnumerable<ProjectResource> projects)
    {
        var selected = projects.ToHashSet();
        builder.Eventing.Subscribe<BeforeResourceStartedEvent>((startup, _) =>
        {
            if (startup.Resource is ProjectResource project && selected.Contains(project)) Verify();
            return Task.CompletedTask;
        });
    }

    private static bool Ordinary(string path) => File.Exists(path) && new FileInfo(path).LinkTarget is null;
    private static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string Hash(JsonElement value)
    {
        string digest = value.GetString() ?? string.Empty;
        if (digest.Length != 64 || digest.Any(character => !char.IsAsciiHexDigit(character)))
            throw new InvalidOperationException("The Works proof runtime requires a SHA-256 for every input.");
        return digest.ToLowerInvariant();
    }
    private static string Absolute(string path)
    {
        if (string.IsNullOrEmpty(path) || !Path.IsPathFullyQualified(path) || !string.Equals(Path.GetFullPath(path), path, StringComparison.Ordinal))
            throw new InvalidOperationException("The Works proof runtime requires canonical absolute input paths.");
        return path;
    }
}
