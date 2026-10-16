namespace Hexalith.Platform.Composition;

using System.Text.Json;

/// <summary>Validates the required production binding manifest before any Works resources are composed.</summary>
/// <remarks>Manifest validation does not qualify providers or authorize preview hosting in production.</remarks>
public static class WorksProductionProfile
{
    private static readonly string[] Required =
    [
        "trustDomain", "namespace", "mtlsEnabled", "gatewayCaller", "moduleCaller", "operatorCaller",
        "brokerTlsEnabled", "brokerPolicySha256", "networkPolicySha256", "workloadAuthority",
        "workloadKeyReference", "delegationProvider", "secretsProvider", "schedulerProvider",
        "stateProvider", "auditProvider", "backupSet", "retentionPolicy", "telemetryEndpoint",
    ];

    /// <summary>Refuses absent, unknown, duplicate, foreign, plaintext, or incomplete production settings.</summary>
    /// <param name="json">The provider binding manifest, containing references rather than secret material.</param>
    public static void Validate(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement profile = document.RootElement;
        if (profile.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Works production profile must be an object.");
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in profile.EnumerateObject())
        {
            if (!Required.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name))
                throw new InvalidDataException("Works production profile contains an unknown or repeated setting.");
        }
        foreach (string name in Required)
        {
            if (!profile.TryGetProperty(name, out JsonElement value))
                throw new InvalidDataException($"Works production profile requires {name}.");
            if (name is "mtlsEnabled" or "brokerTlsEnabled")
            {
                if (value.ValueKind != JsonValueKind.True)
                    throw new InvalidDataException($"Works production profile requires {name}=true.");
                continue;
            }
            if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString())
                || value.GetString() != value.GetString()!.Trim())
                throw new InvalidDataException($"Works production profile requires a nonempty {name} reference.");
        }
        foreach ((string name, string expected) in new[]
        {
            ("gatewayCaller", "eventstore"), ("moduleCaller", "works"), ("operatorCaller", "eventstore-admin"),
        })
            if (profile.GetProperty(name).GetString() != expected)
                throw new InvalidDataException($"Works production profile has a foreign {name}.");
        foreach (string name in new[] { "trustDomain", "namespace" })
            if (profile.GetProperty(name).GetString() is "public" or "localhost" or "default" or "development")
                throw new InvalidDataException($"Works production profile cannot use a development {name}.");
        foreach (string name in new[] { "brokerPolicySha256", "networkPolicySha256" })
        {
            string hash = profile.GetProperty(name).GetString()!;
            if (hash.Length != 64 || hash.Any(static c => !char.IsAsciiHexDigit(c)))
                throw new InvalidDataException($"Works production profile requires a SHA-256 {name} binding.");
        }
        foreach (string name in new[] { "workloadAuthority", "telemetryEndpoint" })
            if (!Uri.TryCreate(profile.GetProperty(name).GetString(), UriKind.Absolute, out Uri? uri)
                || uri.Scheme != Uri.UriSchemeHttps || uri.IsLoopback || !string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidDataException($"Works production profile requires a protected {name} endpoint.");
    }
}
