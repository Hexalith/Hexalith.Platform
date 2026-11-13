namespace Hexalith.Platform.Composition;

using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Hexalith.Builds.Tooling.Workspace;

/// <summary>Resolves the actual MCP abstraction pin without inventing an Aspire package or a version fallback.</summary>
internal static class PlatformMcpCliCatalogVersion
{
    private const string _package = "Hexalith.McpCli.Abstractions";

    /// <summary>Reads the verified recorded package view, or the unique selected physical catalog pin.</summary>
    /// <param name="options">The selected composition inputs.</param>
    /// <param name="source">The diagnostic source.</param>
    internal static string? Resolve(PlatformCompositionOptions options, string source)
    {
        try
        {
            return ResolveCore(options, source);
        }
        catch (Exception exception) when (exception is JsonException or XmlException or InvalidOperationException)
        {
            throw new InvalidDataException($"{source}: {_package}: catalog metadata is malformed.", exception);
        }
    }

    private static string? ResolveCore(PlatformCompositionOptions options, string source)
    {
        string? snapshotPath = options.RunPlan?.Workspace is { } workspace
            ? Path.Combine(workspace, "source-mapping", "catalog-versions.json") : null;
#if NET11_0_OR_GREATER || HEXALITH_PLATFORM_EXTENSION_SDK
        string? selected = PlatformDeclarationSet.DeclarationOptions(options).CatalogPackageVersion(_package, source);
        // The current API authenticates its recorded view. A physical catalog also needs one unambiguous pin.
        return snapshotPath is not null && File.Exists(snapshotPath) ? selected
            : selected == PhysicalPin(options) ? selected : null;
#else
        // Frozen published tools predate the package API. Preserve their exact snapshot validation,
        // then select this package directly; their generic module lookup would request McpCli.Aspire.
        if (snapshotPath is not null && File.Exists(snapshotPath))
        {
            CatalogVersionSnapshot snapshot = JsonSerializer.Deserialize<CatalogVersionSnapshot>(File.ReadAllText(snapshotPath))
                ?? throw new InvalidDataException($"{source}: {_package}: catalog snapshot is null.");
            if (snapshot.PinnedFiles is null || snapshot.Versions is null || snapshot.ProjectVersions?.Any(pair => pair.Value is null) != false
                || snapshot.MappingHash != options.Mapping.ContentHash
                || snapshot.Hash != CatalogVersionSnapshot.ComputeHash(snapshot.MappingHash, snapshot.CatalogCommit,
                    snapshot.CatalogFileHash, snapshot.PinnedFiles, snapshot.Versions, snapshot.ProjectVersions))
                throw new InvalidDataException($"{source}: {_package}: catalog snapshot differs from the selected mapping.");
            KeyValuePair<string, string>[] selected = snapshot.Versions.Where(pair => string.Equals(pair.Key, _package, StringComparison.OrdinalIgnoreCase)).ToArray();
            return selected.Length == 1 ? selected[0].Value : null;
        }
        return PhysicalPin(options);
#endif
    }

    private static string? PhysicalPin(PlatformCompositionOptions options)
    {
        string root = options.Mapping.PlatformIdentity is { IsPlatformRepository: false } ? options.Mapping.Root : options.PlatformRoot;
        XDocument catalog = XDocument.Load(Path.Combine(root, "references", "Hexalith.Builds", "Props", "Directory.Packages.props"));
        if (catalog.Descendants().Any(element => element.Name.LocalName.Equals("Import", StringComparison.OrdinalIgnoreCase))) return null;
        XElement[] items = catalog.Descendants().Where(element => element.Name.LocalName.Equals("PackageVersion", StringComparison.OrdinalIgnoreCase)
            && element.Attributes().Any(attribute => (attribute.Name.LocalName.Equals("Include", StringComparison.OrdinalIgnoreCase)
                || attribute.Name.LocalName.Equals("Update", StringComparison.OrdinalIgnoreCase))
                && string.Equals(attribute.Value, _package, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (items.Length != 1 || items[0].Name != "PackageVersion" || items[0].Attributes().Count() != 2
            || items[0].Attribute("Include") is null || items[0].Attribute("Version") is null
            || items[0].AncestorsAndSelf().Any(HasCondition)) return null;
        string? version = items[0].Attribute("Version")?.Value.Trim();
        if (version != "$(HexalithMcpCliVersion)")
            return version is not null && !version.Contains('$') && !string.IsNullOrWhiteSpace(version) ? version : null;
        XElement[] properties = catalog.Descendants().Where(element => element.Name.LocalName.Equals("HexalithMcpCliVersion", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (properties.Length != 1 || properties[0].Name != "HexalithMcpCliVersion" || properties[0].Ancestors().Any(HasCondition)
            || properties[0].Attributes().Any(attribute => attribute.Name != "Condition" || attribute.Value != "'$(HexalithMcpCliVersion)' == ''")) return null;
        string value = properties[0].Value.Trim();
        return !string.IsNullOrWhiteSpace(value) && !value.Contains('$') ? value : null;
    }

    private static bool HasCondition(XElement element) => element.Attributes().Any(attribute => attribute.Name.LocalName.Equals("Condition", StringComparison.OrdinalIgnoreCase));
}
