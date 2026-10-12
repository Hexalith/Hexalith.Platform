namespace Hexalith.Platform.Composition;

using Hexalith.Builds.Tooling.Workspace;

/// <summary>The complete inputs selected before composing an environment.</summary>
public sealed record PlatformCompositionOptions
{
    /// <summary>Gets the pinned Platform checkout.</summary>
    public required string PlatformRoot { get; init; }
    /// <summary>Gets the single command-selected mapping.</summary>
    public required SourceMapping Mapping { get; init; }
    /// <summary>Gets additional owner declarations, validated with the reference owners.</summary>
    public IReadOnlyList<string> DeclarationPaths { get; init; } = [];
    /// <summary>Gets the active v2 module, or null for the reference composition.</summary>
    public string? ActiveModule { get; init; }
    /// <summary>Gets the explicitly selected module class. Known reference technical owners need no override.</summary>
    public bool? DomainModule { get; init; }
    /// <summary>Gets project paths relative to their mapped repository, indexed by module/server.</summary>
    public IReadOnlyDictionary<string, string> Projects { get; init; } = PlatformServerBindings.ReferenceProjects;
    /// <summary>Gets released image repositories, indexed by module/server.</summary>
    public IReadOnlyDictionary<string, string> Images { get; init; } = PlatformServerBindings.ReferenceImages;
    /// <summary>Gets the full-key, server-scoped configuration binding overrides.</summary>
    public IReadOnlyDictionary<string, string> Configuration { get; init; } = new Dictionary<string, string>();
    /// <summary>Gets the package-mode EventStoreHost seed project.</summary>
    public string? EventStoreSeedProject { get; init; }
    /// <summary>Gets the shared local component directory.</summary>
    public string? ComponentsDirectory { get; init; }
    /// <summary>Gets a runner-owned run plan, when launched through Builds.</summary>
    public Hexalith.Builds.Tooling.Runtime.CompositionRunPlan? RunPlan { get; init; }
    /// <summary>Gets the image-preflight process boundary, replaceable only by the composition tests.</summary>
    internal Func<System.Diagnostics.ProcessStartInfo, TimeSpan, CancellationToken, Task<Hexalith.Builds.Tooling.Runtime.CompositionProcessResult>> ImageProbeRunner { get; init; } = Hexalith.Builds.Tooling.Runtime.CompositionProcess.RunAsync;
}
