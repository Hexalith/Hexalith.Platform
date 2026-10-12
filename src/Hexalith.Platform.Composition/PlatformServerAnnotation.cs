namespace Hexalith.Platform.Composition;

using Aspire.Hosting.ApplicationModel;

/// <summary>Attaches the owner readiness and executable lifecycle task metadata to a server.</summary>
public sealed record PlatformServerAnnotation(PlatformServerBinding Binding) : IResourceAnnotation;
