namespace Hexalith.Platform.Composition;

using System.Text.Json;

/// <summary>Validates the reference-only Agents deployment profile before any Agents resource is composed.</summary>
/// <remarks>Accepted constants are host 192.168.1.30, Parties Branch B, and L/S/O/H/R of 300/30/600/86400/604800. Credential slots hold references. This type mints nothing and does not approve human exact-Conversation or class/time-range scope.</remarks>
public static class AgentsDeploymentProfile
{
    /// <summary>Gets the accepted composition host.</summary>
    public const string Host = "192.168.1.30";

    /// <summary>Gets the accepted Parties identity branch.</summary>
    public const string PartiesBranch = "B";

    internal static string ExpectedCohortPlanSha256
    {
        get
        {
            using Stream stream=typeof(AgentsDeploymentProfile).Assembly.GetManifestResourceStream("ExtSecretsFullJointPlan")!;
            return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(stream));
        }
    }

    private static readonly string[] RootKeys =
    [
        "host", "integrationDate", "partiesBranch", "timing", "services",
        "currentTenant", "globalAdministrator", "humanIdentity", "approverEvidence",
        "replay", "recorder", "worker", "spoolBackend", "replicaCount", "failureModel",
        "protectionEngine", "protectionObserver", "partiesBinding", "conversationsBinding",
    ];

    private static readonly string[] ServiceKeys = ["eventStore", "tenants", "parties", "conversations", "agents"];

    private static readonly string[] ReferenceKeys =
    [
        "currentTenant", "globalAdministrator", "humanIdentity", "approverEvidence",
        "replay", "recorder", "worker", "spoolBackend", "failureModel",
        "protectionEngine", "protectionObserver", "partiesBinding", "conversationsBinding",
    ];

    private static readonly (string Name, int Value)[] Timing =
    [
        ("L", 300), ("S", 30), ("O", 600), ("H", 86400), ("R", 604800),
    ];

    private static readonly string[] ForbiddenCredentials =
    [
        "/home/administrator/.kube/hexalith-production",
        "openbao/Secret/openbao-server-tls",
        "openbao/Secret/openbao-seal",
    ];

    /// <summary>Reference slots whose empty values block resource installation and are named by the replacement packet.</summary>
    public static IReadOnlyList<string> CredentialSlots { get; } =
    [
        ..ReferenceKeys.Append("replicaCount"),
        ..ServiceKeys.Select(static key => "services." + key),
    ];

    /// <summary>Refuses unknown keys, secrets, wrong accepted constants, and empty reference slots.</summary>
    /// <param name="json">The deployment profile. Values are references, never secret bytes.</param>
    public static AgentsDeploymentProfileModel Validate(string json)
    {
        Parsed parsed = Read(json);
        if (parsed.Missing.Count > 0)
            throw new InvalidDataException("Agents deployment profile requires every reference slot.");
        return parsed.Model;
    }

    /// <summary>Validates the profile date against the explicit accepted register and packet dates.</summary>
    public static AgentsDeploymentProfileModel ValidateAccepted(string json, string acceptedRegisterDate, string acceptedPacketDate)
    {
        AgentsDeploymentProfileModel model = Validate(json);
        using JsonDocument document = JsonDocument.Parse(json);
        string date = document.RootElement.GetProperty("integrationDate").GetString()!;
        if (date != acceptedRegisterDate || date != acceptedPacketDate)
            throw new InvalidDataException("Coordinated accepted register, packet and profile dates must agree.");
        return model;
    }

    /// <summary>Checks both coordinated accepted record sections and the exact packet/profile date before production composition.</summary>
    public static AgentsDeploymentProfileModel ValidateAcceptedContext(string json, string acceptedRegister, string acceptedPacket)
    {
        using JsonDocument packet = JsonDocument.Parse(acceptedPacket);
        string target = packet.RootElement.GetProperty("candidateCommit").GetString()!;
        string date = packet.RootElement.GetProperty("platformIntegrationDate").GetString()!;
        if (!System.Text.RegularExpressions.Regex.IsMatch(target, "^[0-9a-f]{40}$")
            || !packet.RootElement.TryGetProperty("profileSha256", out var profilePin)
            || profilePin.GetString() != Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)))
            || !packet.RootElement.TryGetProperty("cohortPlanSha256", out var planPin)
            || planPin.GetString()!=ExpectedCohortPlanSha256)
            throw new InvalidDataException("Accepted packet must bind exact profile bytes and complete shared plan.");
        foreach (string record in new[] { "EXT-HOST-1", "EXT-SECRETS-1" })
        {
            var section = System.Text.RegularExpressions.Regex.Match(acceptedRegister,
                @"^### " + System.Text.RegularExpressions.Regex.Escape(record) + @"\b[^\n]*\n(.*?)(?=^### |^## |\z)",
                System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.Singleline).Value;
            if (!section.Contains("| `TargetVersionOrCommit` | `" + target + "` |", StringComparison.Ordinal)
                || !section.Contains("| `TargetIntegrationDate` | `" + date + "` |", StringComparison.Ordinal)
                || !System.Text.RegularExpressions.Regex.IsMatch(section, @"\| `AcceptedStatus` \| `(?:Committed|Available)` \|"))
                throw new InvalidDataException("Accepted coordinated target/date context required.");
            if (record == "EXT-HOST-1")
            {
                var binding = System.Text.RegularExpressions.Regex.Match(section, @"\| `HostQualificationBinding` \| `([^`]+)` \|");
                if (!binding.Success) throw new InvalidDataException("Accepted host profile/plan binding required.");
                using JsonDocument pins = JsonDocument.Parse(binding.Groups[1].Value);
                if (pins.RootElement.GetProperty("profileSha256").GetString() != profilePin.GetString()
                    || pins.RootElement.GetProperty("cohortPlanSha256").GetString() != planPin.GetString())
                    throw new InvalidDataException("Accepted host profile or plan bytes changed.");
            }
        }
        AgentsDeploymentProfileModel model = ValidateAccepted(json, date, date);
        return model with { AcceptedContextValidated = true };
    }

    /// <summary>Names every empty reference slot. Secrets and unknown keys still fail closed.</summary>
    /// <param name="json">The deployment profile.</param>
    public static IReadOnlyList<string> MissingReferences(string json) => Read(json).Missing;

    private static Parsed Read(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        JsonElement profile;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            profile = document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Agents deployment profile must be an object.", exception);
        }
        if (profile.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Agents deployment profile must be an object.");
        RequireKeys(profile, RootKeys, "Agents deployment profile");
        if (RequiredString(profile, "host") != Host)
            throw new InvalidDataException("Agents deployment profile requires host 192.168.1.30.");
        if (!DateOnly.TryParseExact(RequiredString(profile, "integrationDate"), "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
            throw new InvalidDataException("Agents deployment profile requires an explicit real calendar date yyyy-MM-dd.");
        if (RequiredString(profile, "partiesBranch") != PartiesBranch)
            throw new InvalidDataException("Agents deployment profile requires Parties Branch B.");
        JsonElement timing = profile.GetProperty("timing");
        if (timing.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Agents deployment profile timing must be an object.");
        RequireKeys(timing, Timing.Select(static pair => pair.Name).ToArray(), "Agents deployment profile timing");
        foreach ((string name, int value) in Timing)
        {
            if (!timing.TryGetProperty(name, out JsonElement bound) || bound.ValueKind != JsonValueKind.Number || !bound.TryGetInt32(out int actual) || actual != value)
                throw new InvalidDataException("Agents deployment profile requires accepted L/S/O/H/R timing.");
        }
        JsonElement services = profile.GetProperty("services");
        if (services.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Agents deployment profile services must be an object.");
        RequireKeys(services, ServiceKeys, "Agents deployment profile services");
        Dictionary<string, string> serviceRoots = new(StringComparer.Ordinal);
        List<string> missing = [];
        foreach (string key in ServiceKeys)
        {
            string? value = OptionalReference(services, key);
            serviceRoots[key] = value ?? string.Empty;
            if (value is null)
                missing.Add("services." + key);
        }
        Dictionary<string, string> references = new(StringComparer.Ordinal);
        foreach (string key in ReferenceKeys)
        {
            string? value = OptionalReference(profile, key);
            references[key] = value ?? string.Empty;
            if (value is null)
                missing.Add(key);
        }
        int replicaCount = 0;
        JsonElement replicas = profile.GetProperty("replicaCount");
        if (replicas.ValueKind == JsonValueKind.Null)
            missing.Add("replicaCount");
        else if (replicas.ValueKind != JsonValueKind.Number || !replicas.TryGetInt32(out replicaCount) || replicaCount is < 1 or > 64)
            throw new InvalidDataException("Agents deployment profile requires a replica count reference.");
        return new Parsed(new AgentsDeploymentProfileModel(
            serviceRoots, references["currentTenant"], references["globalAdministrator"], references["humanIdentity"],
            references["approverEvidence"], references["replay"], references["recorder"], references["worker"],
            references["spoolBackend"], replicaCount, references["failureModel"], references["protectionEngine"],
            references["protectionObserver"], references["partiesBinding"], references["conversationsBinding"], RequiredString(profile, "integrationDate")), missing);
    }

    private static void RequireKeys(JsonElement element, IReadOnlyList<string> expected, string scope)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!expected.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name))
                throw new InvalidDataException(scope + " contains an unknown or repeated setting.");
        }
        foreach (string name in expected)
        {
            if (!element.TryGetProperty(name, out _))
                throw new InvalidDataException(scope + " requires " + name + ".");
        }
    }

    private static string RequiredString(JsonElement element, string name)
    {
        string? value = OptionalReference(element, name);
        if (value is null)
            throw new InvalidDataException("Agents deployment profile requires " + name + ".");
        return value;
    }

    private static string? OptionalReference(JsonElement element, string name)
    {
        JsonElement value = element.GetProperty(name);
        if (value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Agents deployment profile requires a reference for " + name + ".");
        string text = value.GetString() ?? string.Empty;
        if (text.Length == 0 || text.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            return null;
        if (text != text.Trim() || text.Length > 256 || text.Any(char.IsWhiteSpace) || IsSecret(text))
            throw new InvalidDataException("Agents deployment profile rejected a secret or malformed reference.");
        return text;
    }

    private static bool IsSecret(string value)
    {
        if (value.Contains("BEGIN ", StringComparison.OrdinalIgnoreCase) || value.Contains("PRIVATE KEY", StringComparison.OrdinalIgnoreCase)
            || value.Contains("client-certificate-data", StringComparison.OrdinalIgnoreCase)
            || value.Contains("client-key-data", StringComparison.OrdinalIgnoreCase))
            return true;
        foreach (string forbidden in ForbiddenCredentials)
        {
            if (value.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private sealed record Parsed(AgentsDeploymentProfileModel Model, IReadOnlyList<string> Missing);
}
