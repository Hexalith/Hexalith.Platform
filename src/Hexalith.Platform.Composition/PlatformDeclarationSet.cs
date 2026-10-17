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
        return CompositionDeclarationClosure.Load(new CompositionDeclarationOptions(options.Mapping, options.DeclarationPaths)
        {
            PlatformRoot = options.PlatformRoot,
            ActiveModule = options.ActiveModule,
            DomainModule = options.DomainModule,
            TimesheetsEnabled = options.TimesheetsEnabled,
            Workspace = options.RunPlan?.Workspace,
            ReleaseCommits = options.ReleaseCommits,
            ReleaseSnapshotHashes = options.ReleaseSnapshotHashes,
        });
    }

    /// <summary>Returns the declared module identity.</summary>
    public static string Id(JsonElement declaration) => CompositionDeclarationClosure.Id(declaration);
}
