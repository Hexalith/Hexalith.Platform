namespace Hexalith.Platform.Composition;

using System.Text.Json.Nodes;

/// <summary>Preserves gateway defaults while removing registrations for servers outside the selected environment.</summary>
internal static class PlatformGatewayConfiguration
{
    /// <summary>Creates a composition-owned content root without modifying the owner's settings.</summary>
    public static string Create(string project, string workspace, IReadOnlyList<PlatformServerBinding> servers)
    {
        string directory = Path.Combine(workspace, "gateway");
        Hexalith.Builds.Tooling.Runtime.CompositionWorkspace.CreatePrivateDirectory(directory);
        HashSet<string> appIds = servers.Select(server => server.AppId).ToHashSet(StringComparer.Ordinal);
        foreach (string filename in new[] { "appsettings.json", "appsettings.Development.json" })
        {
            string source = Path.Combine(Path.GetDirectoryName(project)!, filename);
            if (!File.Exists(source)) continue;
            JsonNode settings;
            try { settings = JsonNode.Parse(File.ReadAllText(source)) ?? throw new InvalidDataException($"{source}: appsettings: JSON root is null."); }
            catch (System.Text.Json.JsonException exception) { throw new InvalidDataException($"{source}: appsettings: JSON is invalid: {exception.Message}", exception); }
            if (settings is not JsonObject) throw new InvalidDataException($"{source}: appsettings: JSON root must be an object.");
            if (settings["EventStore"]?["DomainServices"]?["Registrations"] is JsonObject registrations)
                foreach (string key in registrations.Where(pair => !appIds.Contains(pair.Value?["AppId"]?.GetValue<string>() ?? string.Empty)).Select(pair => pair.Key).ToArray()) registrations.Remove(key);
            File.WriteAllText(Path.Combine(directory, filename), settings.ToJsonString());
        }
        return directory;
    }
}
