namespace Hexalith.Platform.Composition;

using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Workspace;

/// <summary>Atomically validates the complete owner set and selects its dependency closure.</summary>
public static class PlatformDeclarationSet
{
    /// <summary>Loads all declarations before selecting the minimum environment.</summary>
    public static IReadOnlyList<PlatformModuleDeclaration> Load(PlatformCompositionOptions options)
    {
        if (options.DeclarationPaths.Count > 0)
        {
            PlatformManifestValidationResult supplied = PlatformManifestValidator.Validate(options.DeclarationPaths);
            if (!supplied.IsValid) throw new InvalidDataException(string.Join(Environment.NewLine, supplied.Diagnostics.Select(d => $"{d.Source}: {d.Field}: {d.Message}")));
        }
        List<string> paths = [];
        Dictionary<string, (string Path, JsonElement Declaration)> snapshots = new(StringComparer.Ordinal);
        foreach (string owner in new[] { "eventstore", "tenants", "memories" })
        {
            string repository = PlatformServerBindings.Repository(owner);
            if (options.Mapping.Mode == WorkspaceMode.Source)
            {
                SourceMappingEntry? entry = options.Mapping.Entries.SingleOrDefault(e => e.Identity.Equals(repository, StringComparison.OrdinalIgnoreCase));
                if (entry is null || entry.Origin != "source" || entry.Path is null)
                {
                    throw new InvalidDataException($"{repository}: identity.dependencies: mapped source declaration is unavailable.");
                }
                paths.Add(Path.Combine(entry.Path, "module", "hexalith.module-manifest.v2.json"));
            }
            else
            {
                string directory = Path.Combine(options.PlatformRoot, "src", "Hexalith.Platform.Composition", "Declarations");
                string snapshot = Path.Combine(directory, owner + ".json");
                string provenance = Path.Combine(directory, "provenance.json");
                if (!File.Exists(snapshot) || !File.Exists(provenance))
                {
                    throw new InvalidDataException($"{repository}: release snapshot '{snapshot}' or provenance '{provenance}' is unavailable.");
                }
                using JsonDocument sources = JsonDocument.Parse(File.ReadAllText(provenance));
                JsonElement source = sources.RootElement.GetProperty(owner);
                foreach (string field in new[] { "repository", "commit", "source", "sha256" })
                    if (!source.TryGetProperty(field, out JsonElement value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                        throw new InvalidDataException($"{repository}: release snapshot provenance '{provenance}': {field}: source provenance is unavailable.");
                string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(snapshot)));
                if (source.GetProperty("repository").GetString() != repository || source.GetProperty("sha256").GetString() != hash)
                {
                    throw new InvalidDataException($"{repository}: release snapshot '{snapshot}' differs from its source provenance.");
                }
                using JsonDocument released = JsonDocument.Parse(File.ReadAllText(snapshot));
                snapshots[owner] = (snapshot, released.RootElement.GetProperty("modules").EnumerateArray().Single().Clone());
                paths.Add(snapshot);
            }
        }
        // A supplied reference-owner file is already part of the source set or release snapshot.
        foreach (string path in options.DeclarationPaths)
        {
            if (paths.Contains(Path.GetFullPath(path), StringComparer.Ordinal)) continue;
            if (options.Mapping.Mode == WorkspaceMode.Package)
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement[] supplied = document.RootElement.GetProperty("modules").EnumerateArray().ToArray();
                for (int index = 0; index < supplied.Length; index++)
                {
                    JsonElement module = supplied[index];
                    if (snapshots.TryGetValue(Id(module), out var snapshot) && !JsonElement.DeepEquals(module, snapshot.Declaration))
                        throw new InvalidDataException($"{Path.GetFullPath(path)}: {Id(module)}: modules[{index}]: supplied owner declaration conflicts with authoritative release snapshot '{snapshot.Path}'.");
                }
                if (supplied.All(module => snapshots.ContainsKey(Id(module)))) continue;
                // A mixed document can replace an equal snapshot, but never change its release content.
                foreach (JsonElement module in supplied)
                    if (snapshots.TryGetValue(Id(module), out var snapshot)) paths.Remove(snapshot.Path);
            }
            paths.Add(Path.GetFullPath(path));
        }
        PlatformManifestValidationResult result = PlatformManifestValidator.Validate(paths);
        if (!result.IsValid)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, result.Diagnostics.Select(d => $"{d.Source}: {d.Field}: {d.Message}")));
        }
        Dictionary<string, PlatformModuleDeclaration> byId = result.Declarations!.ToDictionary(d => Id(d.Declaration), StringComparer.Ordinal);
        HashSet<string> selected = new(StringComparer.Ordinal);
        void Select(string identity)
        {
            if (!byId.TryGetValue(identity, out PlatformModuleDeclaration? declaration)) throw new InvalidDataException($"{identity}: identity.dependencies: owner declaration is unavailable.");
            if (!selected.Add(identity)) return;
            foreach (JsonElement dependency in declaration.Declaration.GetProperty("identity").GetProperty("dependencies").EnumerateArray()) Select(dependency.GetString()!);
        }
        if (options.ActiveModule is not null) Select(options.ActiveModule);
        bool domain = options.DomainModule ?? (options.ActiveModule is not null && options.ActiveModule is not ("eventstore" or "memories" or "mcpcli" or "platform" or "builds"));
        if (options.ActiveModule is null || domain) foreach (string owner in new[] { "eventstore", "tenants", "memories" }) Select(owner);
        return result.Declarations!.Where(d => selected.Contains(Id(d.Declaration))).ToArray();
    }

    /// <summary>Returns the declared module identity.</summary>
    public static string Id(JsonElement declaration) => declaration.GetProperty("identity").GetProperty("moduleId").GetString()!;
}
