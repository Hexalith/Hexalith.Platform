namespace Hexalith.Platform.Composition;

using System.Text.Json;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Runtime;

/// <summary>Atomically validates the complete owner set and selects its dependency closure.</summary>
public static class PlatformDeclarationSet
{
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
            || !range.IsMinInclusive || minimum.IsPrerelease
            || minimum.Major is not (4 or 5 or 6) || !range.Satisfies(minimum))
            throw new InvalidDataException("The selected Platform identity has no supported Builds generation for its catalog consumer context.");
        // Retained 4.x component inputs keep their original default view. Real frozen 4.x tools also
        // compile as net10.0, where the successor's typed consumer API is deliberately absent.
        return minimum.Major is 5 or 6 ? CompositionDeclarationOptions.PlatformCompositionProjectName : null;
    }
#endif

    /// <summary>Returns the declared module identity.</summary>
    public static string Id(JsonElement declaration) => CompositionDeclarationClosure.Id(declaration);
}
