namespace Hexalith.Platform.Composition;

using System.Text.Json;

/// <summary>One declared EventStore extension package bound through the AD-4 mapping.</summary>
/// <param name="Module">The declaring module identity.</param>
/// <param name="Source">The declaration source, for diagnostics.</param>
/// <param name="PackageId">The package and assembly identity.</param>
/// <param name="ExtensionId">The declared extension identity.</param>
/// <param name="ApiMajor">The declared extension-API major, already checked against the composed server.</param>
/// <param name="Reference">The mapped owner project path in source mode, otherwise the package identity.</param>
/// <param name="PackageVersion">The Builds-catalog version in package mode; null for a project reference.</param>
/// <param name="ConfigurationInputs">The declared configuration inputs bound onto the composed host.</param>
public sealed record PlatformEventStoreExtensionBinding(
    string Module,
    string Source,
    string PackageId,
    string ExtensionId,
    int ApiMajor,
    string Reference,
    string? PackageVersion,
    IReadOnlyList<JsonElement> ConfigurationInputs);
