namespace Hexalith.Platform.Composition;

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;

/// <summary>Resolves, checks and writes the declared EventStore extension bindings before any resource starts.</summary>
internal static class PlatformEventStoreExtensionBindings
{
    /// <summary>The Platform-owned composed host, published as image <c>eventstore</c>.</summary>
    internal const string HostProject = "src/Hexalith.Platform.EventStoreHost/Hexalith.Platform.EventStoreHost.csproj";

    /// <summary>The MSBuild property through which the composed host imports the generated bindings.</summary>
    internal const string BindingsProperty = "PlatformEventStoreExtensionBindings";

    private const string _environmentPrefix = "EventStore__Extensions__Packages__";

    /// <summary>Gets the composed EventStore server's supported extension-API majors; EventStore owns them, Platform keeps no copy.</summary>
    /// <remarks>An SDK cohort without the extension API yields none, so a declared extension is refused rather than guessed.</remarks>
    internal static IReadOnlyList<int> SupportedMajors =>
#if HEXALITH_PLATFORM_EXTENSION_SDK
        Hexalith.EventStore.DomainService.EventStoreExtensionApi.SupportedMajors;
#else
        [];
#endif

    /// <summary>Gets the Platform host project path.</summary>
    internal static string HostProjectPath(PlatformCompositionOptions options) => Path.GetFullPath(Path.Combine(options.PlatformRoot, HostProject));

    /// <summary>Checks duplicates and majors, then maps each declared package to its owner project or catalog pin.</summary>
    /// <param name="options">The selected composition inputs.</param>
    /// <param name="declarations">The validated declaration closure.</param>
    /// <returns>The bindings in declaration order; empty when no module declares an extension.</returns>
    /// <exception cref="InvalidDataException">A declared extension is duplicated, outside the server's majors, unmapped or unpinned.</exception>
    internal static IReadOnlyList<PlatformEventStoreExtensionBinding> Resolve(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(declarations);
        List<PlatformEventStoreExtensionBinding> result = [];
        Dictionary<string, string> identities = new(StringComparer.Ordinal);
        Dictionary<string, string> packages = new(StringComparer.OrdinalIgnoreCase);
        foreach (PlatformModuleDeclaration module in declarations)
        {
            if (!module.Declaration.GetProperty("runtime").TryGetProperty("eventStoreExtensions", out JsonElement entries)) continue;
            string id = PlatformDeclarationSet.Id(module.Declaration);
            int index = 0;
            foreach (JsonElement entry in entries.EnumerateArray())
            {
                string field = $"runtime.eventStoreExtensions[{index++}]";
                string package = entry.GetProperty("packageId").GetString()!;
                string extension = entry.GetProperty("extensionId").GetString()!;
                int major = entry.GetProperty("extensionApiMajor").GetInt32();
                if (identities.TryGetValue(extension, out string? first))
                    throw Failure(module, id, field + ".extensionId", "eventstore-extension-duplicate", $"extension id '{extension}' is declared by modules '{first}' and '{id}'");
                if (packages.TryGetValue(package, out first))
                    throw Failure(module, id, field + ".packageId", "eventstore-extension-duplicate", $"extension package '{package}' is declared by modules '{first}' and '{id}'");
                identities.Add(extension, id);
                packages.Add(package, id);
                IReadOnlyList<int> supported = SupportedMajors;
                if (supported.Count == 0)
                    throw Failure(module, id, field + ".extensionApiMajor", "eventstore-extension-api-unavailable", $"package '{package}' needs the EventStore extension API, which the selected EventStore server SDK does not carry");
                if (!supported.Contains(major))
                    throw Failure(module, id, field + ".extensionApiMajor", "eventstore-extension-unsupported-major",
                        $"package '{package}' declares extension-API major {major.ToString(CultureInfo.InvariantCulture)}; the composed EventStore server supports majors {string.Join(", ", supported.Select(value => value.ToString(CultureInfo.InvariantCulture)))}");
                (string reference, string? version) = Reference(options, module, id, package, field);
                result.Add(new PlatformEventStoreExtensionBinding(id, module.Source, package, extension, major, reference, version,
                    [.. entry.GetProperty("configurationInputs").EnumerateArray().Select(input => input.Clone())]));
            }
        }
        return result;
    }

    /// <summary>Supplies the declared identities and configuration inputs to the composed host, refusing any key it already binds.</summary>
    /// <remarks>Inputs bind exactly as <see cref="PlatformLocalProfile"/> binds owner inputs: every input needs a configured or profile value,
    /// an empty secret is generated once per <c>module/key</c> identity in the shared <paramref name="secretValues"/>, and structured values flatten.</remarks>
    /// <param name="environment">The gateway environment, already holding its own bindings.</param>
    /// <param name="options">The selected composition inputs.</param>
    /// <param name="extensions">The resolved extension bindings.</param>
    /// <param name="secretValues">The composition-wide generated secrets, shared with the owner servers.</param>
    /// <exception cref="InvalidDataException">An input is unbound or overlaps a key already bound by the gateway or another extension.</exception>
    internal static void Bind(Dictionary<string, string> environment, PlatformCompositionOptions options, IReadOnlyList<PlatformEventStoreExtensionBinding> extensions,
        Dictionary<string, string> secretValues)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(secretValues);
        string profile = Path.Combine(options.PlatformRoot, "src", "Hexalith.Platform.Composition", "Declarations", "local-profile.json");
        Dictionary<string, string> defaults = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(profile))!;
        for (int index = 0; index < extensions.Count; index++)
        {
            PlatformEventStoreExtensionBinding extension = extensions[index];
            string prefix = _environmentPrefix + index.ToString(CultureInfo.InvariantCulture) + "__";
            environment[prefix + "PackageId"] = extension.PackageId;
            environment[prefix + "ExtensionId"] = extension.ExtensionId;
            environment[prefix + "ApiMajor"] = extension.ApiMajor.ToString(CultureInfo.InvariantCulture);
        }
        foreach (PlatformEventStoreExtensionBinding extension in extensions)
        foreach (JsonElement input in extension.ConfigurationInputs)
        {
            string key = input.GetProperty("key").GetString()!;
            string scoped = extension.Module + "/" + extension.ExtensionId + "/" + key;
            string source = input.GetProperty("source").GetString()!;
            if (!options.Configuration.TryGetValue(scoped, out string? value) && !defaults.TryGetValue(scoped, out value))
                throw new InvalidDataException($"{extension.Source}: {scoped}: runtime.eventStoreExtensions.configurationInputs: eventstore-extension-input-unbound: binding is unavailable.");
            if (source == "secret" && string.IsNullOrEmpty(value))
            {
                string identity = extension.Module + "/" + key;
                if (!secretValues.TryGetValue(identity, out value))
                {
                    value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
                    secretValues.Add(identity, value);
                }
            }
            Dictionary<string, string> bound = new(StringComparer.Ordinal);
            ReadOnlySpan<char> structuredValue = value.AsSpan().TrimStart();
            if (source is not ("secret" or "dynamic-secret") && !structuredValue.IsEmpty && structuredValue[0] is '[' or '{')
            {
                try
                {
                    using JsonDocument structured = JsonDocument.Parse(value);
                    PlatformLocalProfile.Flatten(bound, key, structured.RootElement);
                }
                catch (JsonException exception)
                {
                    throw new InvalidDataException($"{extension.Source}: {scoped}: runtime.eventStoreExtensions.configurationInputs: eventstore-extension-input-invalid: structured configuration is invalid: {exception.Message}", exception);
                }
            }
            else bound[key.Replace(":", "__", StringComparison.Ordinal)] = value;
            foreach (KeyValuePair<string, string> pair in bound)
            {
                // Configuration keys ignore case and a parent or child key rebinds the same section, so any overlap is a conflict.
                if (Overlaps(pair.Key, "EventStore__Extensions") || environment.Keys.Any(existing => Overlaps(pair.Key, existing)))
                    throw new InvalidDataException($"{extension.Source}: {scoped}: runtime.eventStoreExtensions.configurationInputs: eventstore-extension-configuration-conflict: '{pair.Key}' overlaps a key already bound on the composed eventstore host.");
                environment[pair.Key] = pair.Value;
            }
        }
    }

    /// <summary>Writes the generated bindings props: project references in source mode, catalog-pinned package references otherwise.</summary>
    /// <param name="host">The composed host binding.</param>
    /// <param name="directory">The run-owned output directory.</param>
    /// <returns>The written props path.</returns>
    internal static string Write(PlatformEventStoreHostBinding host, string directory)
    {
        ArgumentNullException.ThrowIfNull(host);
        CompositionWorkspace.CreatePrivateDirectory(directory);
        XElement references = new("ItemGroup");
        foreach (PlatformEventStoreExtensionBinding extension in host.Extensions)
        {
            if (extension.PackageVersion is null)
            {
                references.Add(new XElement("ProjectReference", new XAttribute("Include", Escape(extension.Reference))));
                continue;
            }
            references.Add(new XElement("PackageReference", new XAttribute("Include", extension.Reference)));
            references.Add(new XElement("PackageVersion", new XAttribute("Update", extension.Reference), new XAttribute("Version", extension.PackageVersion)));
        }
        string props = Path.Combine(directory, "bindings.props");
        new XDocument(new XElement("Project", references)).Save(props);
        return props;
    }

    private static (string Reference, string? Version) Reference(PlatformCompositionOptions options, PlatformModuleDeclaration module, string id, string package, string field)
    {
        if (options.Mapping.Mode == WorkspaceMode.Source)
        {
            SourceMappingEntry owner = options.Mapping.Entries.SingleOrDefault(entry => entry.Identity.Equals(PlatformServerBindings.Repository(id), StringComparison.OrdinalIgnoreCase))
                ?? throw Failure(module, id, field + ".packageId", "eventstore-extension-unmapped", $"owner repository '{PlatformServerBindings.Repository(id)}' is absent from the selected mapping");
            if (owner is { Origin: "source", Path: { } root })
            {
                string project = Path.GetFullPath(Path.Combine(root, "src", package, package + ".csproj"));
                // Limit containment to the selected owner so a link into another mapped repository cannot pass.
                if (!(options.Mapping with { Entries = [owner], PlatformIdentity = null }).ContainsProject(project))
                    throw Failure(module, id, field + ".packageId", "eventstore-extension-unavailable", $"mapped extension project '{project}' is missing or physically outside its owner root");
                return (project, null);
            }
        }
#if HEXALITH_PLATFORM_EXTENSION_SDK
        string? version = new CompositionDeclarationOptions(options.Mapping, options.DeclarationPaths)
        {
            PlatformRoot = options.PlatformRoot,
            Workspace = options.RunPlan?.Workspace,
        }.CatalogPackageVersion(package, module.Source);
        return string.IsNullOrWhiteSpace(version)
            ? throw Failure(module, id, field + ".packageId", "eventstore-extension-unpinned", $"package '{package}' has no Builds catalog pin")
            : (package, version);
#else
        // A cohort without the extension SDK also binds a published Builds tool that may predate the catalog lookup;
        // Resolve refuses every declared extension before mapping it, so this stays fail-closed.
        throw Failure(module, id, field + ".extensionApiMajor", "eventstore-extension-api-unavailable", $"package '{package}' needs the EventStore extension API, which the selected EventStore server SDK does not carry");
#endif
    }

    private static bool Overlaps(string first, string second)
    {
        string[] left = first.Split("__");
        string[] right = second.Split("__");
        return left.Zip(right).All(pair => string.Equals(pair.First, pair.Second, StringComparison.OrdinalIgnoreCase));
    }

    private static InvalidDataException Failure(PlatformModuleDeclaration module, string id, string field, string condition, string reason)
        => new($"{module.Source}: {id}: {field}: {condition}: {reason}.");

    private static string Escape(string value) => value.Replace("%", "%25", StringComparison.Ordinal).Replace("$", "%24", StringComparison.Ordinal)
        .Replace("@", "%40", StringComparison.Ordinal).Replace("'", "%27", StringComparison.Ordinal).Replace(";", "%3B", StringComparison.Ordinal)
        .Replace("?", "%3F", StringComparison.Ordinal).Replace("*", "%2A", StringComparison.Ordinal);
}
