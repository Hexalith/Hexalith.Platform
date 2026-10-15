using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Explicit public references. Null identities stay null; this record never invents a tenant, key, issuer, or signer.</summary>
public sealed record SecretsRuntimeConfiguration
{
    /// <summary>Gets the enrolled Dapr secret-store component name.</summary>
    public string? ProviderBinding { get; init; }

    /// <summary>Gets the restricted HMAC application credential reference.</summary>
    public string? HmacCredentialReference { get; init; }

    /// <summary>Gets the independent inventory authority app id.</summary>
    public string? CurrentInventoryAuthorityReference { get; init; }

    /// <summary>Gets the current tenant inventory reference.</summary>
    public string? TenantInventoryReference { get; init; }

    /// <summary>Gets the independent profile reference.</summary>
    public string? ProfileReference { get; init; }

    /// <summary>Gets the independent issuer reference.</summary>
    public string? Issuer { get; init; }

    /// <summary>Gets the independent audience reference.</summary>
    public string? Audience { get; init; }

    /// <summary>Gets the published profile version.</summary>
    public string? ProfileVersion { get; init; }

    /// <summary>Gets the published profile start.</summary>
    public DateTimeOffset? ProfileNotBefore { get; init; }

    /// <summary>Gets the published exclusive profile end.</summary>
    public DateTimeOffset? ProfileValidUntil { get; init; }

    /// <summary>Gets the explicit call budget, at most thirty seconds.</summary>
    public TimeSpan? CallTimeout { get; init; }

    /// <summary>Gets the actor-role authority app id.</summary>
    public string? ActorRoleAuthorityReference { get; init; }

    /// <summary>Gets the independent signer credential reference.</summary>
    public string? IndependentSignerCredentialReference { get; init; }

    /// <summary>Gets the published trust reference.</summary>
    public string? PublicTrustReference { get; init; }

    /// <summary>Gets the current revocation authority reference.</summary>
    public string? CurrentRevocationReference { get; init; }

    /// <summary>Gets the private replay registrar reference.</summary>
    public string? ReplayRegistrarCredentialReference { get; init; }

    /// <summary>Gets the private compromise subscriber credential reference.</summary>
    public string? CompromiseSubscriberCredentialReference { get; init; }

    /// <summary>Gets the compromise subscription app id.</summary>
    public string? CompromiseSubscriptionReference { get; init; }

    /// <summary>Gets the guard-revocation mirror reference.</summary>
    public string? CompromiseGuardMirrorReference { get; init; }

    /// <summary>Gets the existing nonrollback journal reference.</summary>
    public string? NonrollbackJournalReference { get; init; }

    /// <summary>Gets the existing all-copy lifecycle reference.</summary>
    public string? AllCopyLifecycleAuthorityReference { get; init; }

    /// <summary>Gets the owner-accepted replacement SHA. This delivery leaves it unset.</summary>
    public string? AcceptedReplacementCommit { get; init; }

    /// <summary>Gets the accepted-timing profile when every identity and validity field was supplied.</summary>
    public PlatformSigningProfile? ExpectedProfile { get; init; }

    /// <summary>Loads a reference file. An unreadable file returns null and copies only known fields.</summary>
    public static SecretsRuntimeConfiguration? Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            using FileStream stream = File.OpenRead(path);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
            return Parse(reader.ReadToEnd());
        }
        catch (UnauthorizedAccessException) { return null; }
        catch (IOException) { return null; }
    }

    /// <summary>Opens through the supplied function so an unauthorized reader stays inside this method.</summary>
    internal static SecretsRuntimeConfiguration? Load(Func<Stream> open)
    {
        ArgumentNullException.ThrowIfNull(open);
        try
        {
            using Stream stream = open();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
            return Parse(reader.ReadToEnd());
        }
        catch (UnauthorizedAccessException) { return null; }
        catch (IOException) { return null; }
    }

    /// <summary>Copies known public fields. Duplicate names return null; unmapped names are ignored.</summary>
    public static SecretsRuntimeConfiguration? Parse(string json)
    {
        JsonObject? document = SecretsProof.ParseDocument(json);
        if (document is null) { return null; }
        JsonObject runtime = Nested(document, "runtime");
        string? issuer = Text(runtime, "issuer");
        string? audience = Text(runtime, "audience");
        string? version = Text(runtime, "profileVersion");
        DateTimeOffset? notBefore = Instant(runtime, "profileNotBefore");
        DateTimeOffset? validUntil = Instant(runtime, "profileValidUntil");
        PlatformSigningProfile? profile = null;
        if (SecretsProvisioning.IsReference(version) && SecretsProvisioning.IsReference(issuer) && SecretsProvisioning.IsReference(audience)
            && notBefore is DateTimeOffset start && validUntil is DateTimeOffset end)
        {
            try { profile = PlatformAcceptedEnvelopeTiming.Bind(version!, issuer!, audience!, start, end); }
            catch (ArgumentException) { profile = null; }
        }
        long? timeoutSeconds = SecretsProof.Integer(runtime, "callTimeout");
        TimeSpan? timeout = timeoutSeconds is long seconds && SecretsProof.TryLifetime(seconds, out TimeSpan parsed) && parsed <= TimeSpan.FromSeconds(30) ? parsed : null;
        return new SecretsRuntimeConfiguration
        {
            ProviderBinding = Text(runtime, "providerBinding"),
            HmacCredentialReference = Text(runtime, "hmacCredentialReference"),
            CurrentInventoryAuthorityReference = Text(runtime, "currentInventoryAuthorityReference"),
            TenantInventoryReference = Text(runtime, "tenantInventoryReference"),
            ProfileReference = Text(runtime, "profileReference"),
            Issuer = issuer,
            Audience = audience,
            ProfileVersion = version,
            ProfileNotBefore = notBefore,
            ProfileValidUntil = validUntil,
            CallTimeout = timeout,
            ActorRoleAuthorityReference = Text(runtime, "actorRoleAuthorityReference"),
            IndependentSignerCredentialReference = Text(runtime, "independentSignerCredentialReference"),
            PublicTrustReference = Text(runtime, "publicTrustReference"),
            CurrentRevocationReference = Text(runtime, "currentRevocationReference"),
            ReplayRegistrarCredentialReference = Text(runtime, "replayRegistrarCredentialReference"),
            CompromiseSubscriberCredentialReference = Text(runtime, "compromiseSubscriberCredentialReference"),
            CompromiseSubscriptionReference = Text(runtime, "compromiseSubscriptionReference"),
            CompromiseGuardMirrorReference = Text(runtime, "compromiseGuardMirrorReference"),
            NonrollbackJournalReference = Text(runtime, "nonrollbackJournalReference"),
            AllCopyLifecycleAuthorityReference = Text(runtime, "allCopyLifecycleAuthorityReference"),
            AcceptedReplacementCommit = Text(runtime, "acceptedReplacementCommit"),
            ExpectedProfile = profile,
        };
    }

    private static JsonObject Nested(JsonObject document, string name) => document[name] is JsonObject nested ? nested : new JsonObject();

    private static string? Text(JsonObject source, string name) => SecretsProof.Text(source, name);

    private static DateTimeOffset? Instant(JsonObject source, string name)
    {
        string? text = Text(source, name);
        if (text is null || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset instant) || instant.Offset != TimeSpan.Zero)
        { return null; }
        return instant;
    }
}
