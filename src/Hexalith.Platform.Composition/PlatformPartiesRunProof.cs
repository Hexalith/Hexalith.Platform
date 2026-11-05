namespace Hexalith.Platform.Composition;

using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;

/// <summary>A nonserialized ordinary-Parties purpose proof issued only inside the trusted key-bearing ComposeRun call.</summary>
/// <remarks>The ordinary plan has no issuer MAC. This proof preserves the verified parent handoff in memory; it is not candidate authority.</remarks>
internal sealed class PlatformPartiesRunProof
{
    private readonly string _optionsDigest;
    private readonly string _activeRevision;
    private readonly IReadOnlyDictionary<string, string> _files;

    private PlatformPartiesRunProof(PlatformCompositionOptions options)
    {
        _optionsDigest = Digest(options);
        _activeRevision = Git(ActiveRoot(options.Mapping), "rev-parse", "HEAD");
        _files = Inputs(options).Distinct(StringComparer.Ordinal).ToDictionary(path => path, Hash, StringComparer.Ordinal);
    }

    /// <summary>Checks actual parent plan, mapping, selected tests and source inputs before retaining their private in-memory binding.</summary>
    internal static PlatformPartiesRunProof Create(PlatformCompositionOptions options, string signingKey)
    {
        Span<byte> key = stackalloc byte[48];
        if (!Convert.TryFromBase64String(signingKey, key, out int size) || size != key.Length)
            throw Refusal();
        CryptographicOperations.ZeroMemory(key);
        Validate(options);
        return new(options);
    }

    /// <summary>Refuses replay or mutable plan, source, mapping, declaration, configuration or test inputs before composition effects.</summary>
    internal bool Matches(PlatformCompositionOptions options)
    {
        try
        {
            Validate(options);
            return Digest(options) == _optionsDigest && Git(ActiveRoot(options.Mapping), "rev-parse", "HEAD") == _activeRevision
                && _files.All(pair => Hash(pair.Key) == pair.Value);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return false;
        }
    }

    private static void Validate(PlatformCompositionOptions options)
    {
        CompositionRunPlan plan = options.RunPlan ?? throw Refusal();
        SourceMapping mapping = options.Mapping;
        PlatformIdentity identity = mapping.PlatformIdentity ?? throw Refusal();
        if (plan.Schema != CompositionRunPlan.SupportedSchema || !CompositionRunPlanFactory.IsRunId(plan.RunId)
            || !CompositionRunPlanFactory.IsRunId(plan.AttemptId) || !CompositionRunPlanFactory.IsRunId(plan.EnvironmentId)
            || new[] { plan.RunId, plan.AttemptId, plan.EnvironmentId }.Distinct(StringComparer.Ordinal).Count() != 3
            || plan.PlatformModelAssemblyPath != typeof(PlatformAppModel).Assembly.Location
            || plan.CandidatePolicy is not null || plan.QualificationOwners is not null || plan.QualificationOwnersBinding is not null
            || options.QualificationOwners.Count != 0 || options.ActiveModule != "parties" || plan.PlatformActiveModule != "parties"
            || mapping.Mode != WorkspaceMode.Package || !mapping.HasGit || mapping.ActiveModule != "Hexalith.Parties"
            || plan.SourceMappingHash != mapping.ContentHash || options.PlatformRoot != identity.PlatformPath
            || identity.Commit != identity.CompositionCommit || identity.Commit != identity.CheckoutCommit
            || options.EnvironmentId is not null && options.EnvironmentId != plan.EnvironmentId
            || !options.DeclarationPaths.SequenceEqual(plan.PlatformDeclarationPaths, StringComparer.Ordinal)
            || plan.PlatformDeclarationPaths.Count != 1 || plan.SelectedTest is null || plan.SelectedTests.Count is < 1 or > 2
            || plan.SelectedTests[0] != plan.SelectedTest || plan.SelectedTests.Select(test => test.Name).Distinct(StringComparer.Ordinal).Count() != plan.SelectedTests.Count)
            throw Refusal();
        string activeRoot = ActiveRoot(mapping);
        string declaration = Path.Combine(activeRoot, "module", "hexalith.module-manifest.v2.json");
        if (plan.PlatformDeclarationPaths[0] != declaration || !Hash(declaration).Equals(plan.PlatformDeclarationHash, StringComparison.OrdinalIgnoreCase))
            throw Refusal();
        SourceMapping persisted = SourceMapping.LoadVerified(Path.Combine(plan.Workspace, "source-mapping", "mapping.json"), mapping.ContentHash);
        if (persisted.ContentHash != mapping.ContentHash) throw Refusal();
        CompositionRunPlan recorded = CompositionDocumentStore.TryReadAsync<CompositionRunPlan>(Path.Combine(plan.Workspace, "plan.json"), CancellationToken.None)
            .GetAwaiter().GetResult() ?? throw Refusal();
        if (CompositionDocumentStore.Serialize(recorded) != CompositionDocumentStore.Serialize(plan)
            && !(PlatformPartiesValidationContext.IsCurrentPublishedValidation(plan)
                && PlatformPartiesValidationContext.IsExactClaimProjection(recorded, plan))) throw Refusal();
        ValidateTests(plan, declaration, mapping);
        SourceMappingEntry active = mapping.Entries.Single(entry => entry.Identity == "Hexalith.Parties");
        SourceMappingEntry builds = mapping.Entries.Single(entry => entry.Identity == "Hexalith.Builds");
        string buildsRoot = builds.Origin switch
        {
            "package" when builds.Path is null => Path.Combine(mapping.Root, "references", "Hexalith.Builds"),
            "source" when builds.Path is not null => builds.Path,
            _ => throw Refusal(),
        };
        if (active.Origin != "source" || Git(activeRoot, "rev-parse", "--show-toplevel") != activeRoot
            || activeRoot != mapping.Root && (activeRoot != Path.Combine(mapping.Root, "references", "Hexalith.Parties")
                || Git(mapping.Root, "rev-parse", "HEAD:references/Hexalith.Parties") != Git(activeRoot, "rev-parse", "HEAD"))
            || Git(identity.PlatformPath, "rev-parse", "HEAD") != identity.CheckoutCommit
            || Git(buildsRoot, "rev-parse", "--show-toplevel") != buildsRoot
            || Git(buildsRoot, "rev-parse", "HEAD") != identity.CatalogCommit
            || builds.Origin == "package" && Git(mapping.Root, "rev-parse", "HEAD:references/Hexalith.Builds") != identity.CatalogCommit
            || new[] { activeRoot, mapping.Root, identity.PlatformPath, buildsRoot }.Distinct(StringComparer.Ordinal).Any(root => Git(root, "status", "--porcelain", "--untracked-files=normal").Length != 0))
            throw Refusal();
    }

    private static void ValidateTests(CompositionRunPlan plan, string declaration, SourceMapping mapping)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(declaration));
        JsonElement tests = document.RootElement.GetProperty("modules").EnumerateArray()
            .Single(module => module.GetProperty("identity").GetProperty("moduleId").GetString() == "parties").GetProperty("tests");
        SourceMapping owner = mapping with
        {
            Entries = mapping.Entries.Where(entry => entry.Identity == "Hexalith.Parties").ToArray(), PlatformIdentity = null,
        };
        foreach (ModuleTestSelection selected in plan.SelectedTests)
        {
            string entry = Path.GetFullPath(Path.Combine(ActiveRoot(mapping), selected.Entry));
            if (selected.ModuleId != "parties" || selected.Mode != "package" || selected.Name is not ("platform-package" or "lifecycle-package")
                || Path.IsPathRooted(selected.Entry) || !owner.ContainsProject(entry)
                || Git(Path.GetDirectoryName(entry)!, "rev-parse", "--show-toplevel") != ActiveRoot(mapping)
                || selected.Entry.StartsWith("references/", StringComparison.OrdinalIgnoreCase)
                || !Hash(entry).Equals(selected.EntryHash, StringComparison.OrdinalIgnoreCase)
                || !Hash(declaration).Equals(selected.ManifestHash, StringComparison.OrdinalIgnoreCase)) throw Refusal();
            JsonElement declared = tests.EnumerateArray().Single(test => test.GetProperty("name").GetString() == selected.Name);
            if (declared.GetProperty("entry").GetString() != selected.Entry || declared.GetProperty("mode").GetString() != selected.Mode
                || declared.GetProperty("executor").GetString() != selected.Executor
                || declared.GetProperty("executionTimeoutSeconds").GetInt32() != selected.ExecutionTimeoutSeconds
                || declared.GetProperty("stopTimeoutSeconds").GetInt32() != selected.StopTimeoutSeconds) throw Refusal();
        }
    }

    private static IEnumerable<string> Inputs(PlatformCompositionOptions options)
    {
        CompositionRunPlan plan = options.RunPlan!;
        return options.DeclarationPaths.Concat(plan.SelectedTests.Select(test => Path.Combine(ActiveRoot(options.Mapping), test.Entry)))
            .Concat([Path.Combine(plan.Workspace, "plan.json"), Path.Combine(plan.Workspace, "source-mapping", "mapping.json"),
                Path.Combine(plan.Workspace, "source-mapping", "catalog-versions.json"),
                Path.Combine(plan.Workspace, "source-mapping", "pinned-tree", "Props", "Directory.Packages.props"),
                options.Mapping.PlatformIdentity!.RecordPath,
                options.EventStoreSeedProject ?? throw Refusal(), plan.PlatformModelAssemblyPath ?? throw Refusal()]);
    }

    private static string ActiveRoot(SourceMapping mapping) => mapping.Entries.Single(entry => entry.Identity == "Hexalith.Parties").Path ?? throw Refusal();

    private static string Git(string root, params string[] arguments)
    {
        var result = CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("git", arguments, root, null), TimeSpan.FromSeconds(10), CancellationToken.None)
            .GetAwaiter().GetResult();
        return result.ExitCode == 0 && !result.TimedOut && !result.OutputTruncated ? result.Output.Trim() : throw Refusal();
    }

    private static string Hash(string path)
    {
        FileInfo file = new(path);
        if (!Path.IsPathFullyQualified(path) || !file.Exists || file.Length > 4 * 1024 * 1024 || file.LinkTarget is not null) throw Refusal();
        for (DirectoryInfo? directory = file.Directory; directory is not null; directory = directory.Parent)
            if (directory.LinkTarget is not null) throw Refusal();
        return Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    }

    private static string Digest(PlatformCompositionOptions options) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(options)));
    private static InvalidDataException Refusal() => new("The ordinary Parties package profile requires its trusted parent plan/key handoff and unchanged source/test inputs.");
}
