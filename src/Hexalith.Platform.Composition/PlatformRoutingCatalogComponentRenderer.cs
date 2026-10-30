namespace Hexalith.Platform.Composition;

using System.Text.Json;

/// <summary>Renders the private shared authority component with exact participant scope and no application key prefix.</summary>
internal static class PlatformRoutingCatalogComponentRenderer
{
    internal static string Render(string name, IEnumerable<string> appIds, int redisPort) => JsonSerializer.Serialize(new
    {
        apiVersion = "dapr.io/v1alpha1", kind = "Component", metadata = new { name },
        spec = new { type = "state.redis", version = "v1", metadata = new[]
        {
            new { name = "redisHost", value = "127.0.0.1:" + redisPort }, new { name = "redisPassword", value = "" },
            new { name = "keyPrefix", value = "none" }, new { name = "actorStateStore", value = "false" },
        } },
        scopes = appIds.Order(StringComparer.Ordinal).ToArray(),
    });
}
