namespace Hexalith.Platform.Composition;

using Aspire.Hosting.ApplicationModel;

/// <summary>Records the declaration that explicitly owns a composed resource; unavailable ownership remains null.</summary>
/// <param name="ModuleId">The declaring module identity, when available.</param>
public sealed record PlatformResourceOwnerAnnotation(string? ModuleId) : IResourceAnnotation;
