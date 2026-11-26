namespace Hexalith.Platform.Composition;

using System.Text.Json;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Runtime;

/// <summary>Atomically validates the complete owner set and selects its dependency closure.</summary>
public static class PlatformDeclarationSet
{
    internal const string CompleteModule = "platform";
    internal const string CompleteManifest = "module/hexalith.module-manifest.v2.json";
    /// <summary>Loads all declarations before selecting the minimum environment.</summary>
    public static IReadOnlyList<PlatformModuleDeclaration> Load(PlatformCompositionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return CompositionDeclarationClosure.Load(DeclarationOptions(options));
    }

    /// <summary>Uses one consumer context for shared declarations, module versions and package pins.</summary>
    /// <param name="options">The selected owner mapping and its unchanged catalog workspace.</param>
    internal static CompositionDeclarationOptions DeclarationOptions(PlatformCompositionOptions options)
        => new(options.Mapping, options.DeclarationPaths)
        {
            PlatformRoot = options.PlatformRoot,
#if NET11_0_OR_GREATER
            ConsumerProjectName = ConsumerProject(options),
#endif
            ActiveModule = options.ActiveModule,
            QualificationOwners = options.QualificationOwners,
            DomainModule = options.DomainModule,
            TimesheetsEnabled = options.TimesheetsEnabled,
            Workspace = options.RunPlan?.Workspace,
            ReleaseCommits = options.ReleaseCommits,
            ReleaseSnapshotHashes = options.ReleaseSnapshotHashes,
        };

#if NET11_0_OR_GREATER
    private static string? ConsumerProject(PlatformCompositionOptions options)
    {
        if (options.Mapping.PlatformIdentity is not { } identity) return null;
        if (string.IsNullOrWhiteSpace(identity.ToolVersionRange)
            || !NuGet.Versioning.VersionRange.TryParse(identity.ToolVersionRange, out var range)
            || range.MinVersion is not { } minimum || range.MaxVersion is null || range.Float is not null
            || !range.IsMinInclusive || minimum.IsPrerelease || minimum.HasMetadata
            || minimum.Major is not (4 or 5 or 6) || !range.Satisfies(minimum))
            throw new InvalidDataException("The selected Platform identity has no supported Builds generation for its catalog consumer context.");
        // The pinned version and its gitlink are authenticated by the tag, lock and catalog checks, not by a list here.
        if (minimum.Major == 6 && (range.MaxVersion.ToFullString() != "7.0.0-0" || range.IsMaxInclusive))
            throw new InvalidDataException("The Builds6 consumer context requires the range [pin, 7.0.0-0).");
        // Retained 4.x component inputs keep their original default view. Real frozen 4.x tools also
        // compile as net10.0, where the successor's typed consumer API is deliberately absent.
        return minimum.Major is 5 or 6 ? CompositionDeclarationOptions.PlatformCompositionProjectName : null;
    }
#endif

    internal static void ValidateCompleteRunner(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations)
    {
        var plan = options.RunPlan ?? throw new InvalidDataException("The complete Platform selector requires a Builds run plan.");
        string[] owners = ["conversations", "eventstore", "folders", "mcpcli", "memories", "parties", CompleteModule, "projects", "tenants"];
        var selected = declarations.Select(module => Id(module.Declaration)).Order(StringComparer.Ordinal).ToArray();
        if (!selected.SequenceEqual(owners, StringComparer.Ordinal))
            throw new InvalidDataException($"Platform complete selector: expected owners {string.Join(", ", owners)}; selected {string.Join(", ", selected)}.");

        var owner = declarations.Single(module => Id(module.Declaration) == "tenants");
        var tasks = owner.Declaration.GetProperty("lifecycle").GetProperty("tasks").EnumerateArray();
        const string taskName = "create-synthetic-tenant";
        var declared = tasks.SingleOrDefault(task => task.GetProperty("name").GetString() == taskName);
        if (declared.ValueKind != JsonValueKind.Object
            || declared.GetProperty("scope").GetString() != "once-per-environment-creation"
            || declared.GetProperty("authorityClass").GetString() != "operator")
            throw new InvalidDataException($"{owner.Source}: tenants: lifecycle.tasks: required operator task '{taskName}' is unavailable.");
        string ownerRoot = CompositionDeclarationClosure.TaskRepositoryRoot(options.Mapping, owner);
        string executable = Path.GetFullPath(Path.Combine(ownerRoot, declared.GetProperty("executable").GetString()!));
        string[] arguments = declared.GetProperty("arguments").EnumerateArray().Select(argument => argument.GetString()!).ToArray();
        var scheduled = plan.StartupTasks.Where(task => task.ModuleId == "tenants" && task.Name == taskName).ToArray();
        if (scheduled is not [var task] || task.ExecutablePath != executable || task.ExecutableError is not null
            || !task.Arguments.SequenceEqual(arguments, StringComparer.Ordinal))
            throw new InvalidDataException($"{owner.Source}: tenants: the complete Builds plan must schedule operator task '{taskName}' from '{executable}'.");
    }

    /// <summary>Returns the declared module identity.</summary>
    public static string Id(JsonElement declaration) => CompositionDeclarationClosure.Id(declaration);
}
