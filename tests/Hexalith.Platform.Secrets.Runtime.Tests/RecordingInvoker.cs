using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

internal sealed class RecordingInvoker : ISecretsCredentialBoundDaprInvoker
{
    public List<(string App, string Method, string Body)> Calls { get; } = [];

    public List<(string App, string Credential)> PrivateBindings { get; } = [];

    public Func<string, string, string, CancellationToken, string?> Reply { get; set; } = (_, _, _, _) => null;

    public bool CancelCaller { get; set; }

    public bool ThrowBudgetCancellation { get; set; }

    public Task<string?> InvokeCredentialBoundAsync(string appId, string credentialReference, string method, string jsonBody, CancellationToken cancellationToken)
    {
        PrivateBindings.Add((appId, credentialReference));
        return InvokeAsync(appId, method, jsonBody, cancellationToken);
    }

    public Task<string?> InvokeAsync(string appId, string method, string jsonBody, CancellationToken cancellationToken)
    {
        Calls.Add((appId, method, jsonBody));
        if (CancelCaller)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        if (ThrowBudgetCancellation)
        {
            throw new OperationCanceledException();
        }
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Reply(appId, method, jsonBody, cancellationToken));
    }
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal static class Fixtures
{
    public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-08T12:00:00+00:00");

    public static SecretsRuntimeConfiguration Ready()
    {
        DateTimeOffset start = Now.AddHours(-1);
        DateTimeOffset end = Now.AddDays(30);
        return new SecretsRuntimeConfiguration
        {
            ProviderBinding = "store",
            HmacCredentialReference = "hmac-credential",
            CurrentInventoryAuthorityReference = "inventory-app",
            TenantInventoryReference = "inventory-ref",
            ProfileReference = "profile-app",
            Issuer = "issuer",
            Audience = "audience",
            ProfileVersion = "profile-v1",
            ProfileNotBefore = start,
            ProfileValidUntil = end,
            CallTimeout = TimeSpan.FromSeconds(30),
            ActorRoleAuthorityReference = "role-app",
            IndependentSignerCredentialReference = "outside-approval-issuer-credential",
            CapabilitySignerReference = "signer-app",
            CapabilitySignerCredentialReference = "capability-signer-credential",
            PublicTrustReference = "trust-app",
            CurrentRevocationReference = "revocation-app",
            ReplayRegistrarReference = "replay-app",
            ReplayRegistrarCredentialReference = "replay-credential",
            CompromiseSubscriberCredentialReference = "compromise-credential",
            CompromiseSubscriptionReference = "compromise-sub",
            CompromiseGuardMirrorReference = "compromise-mirror",
            NonrollbackJournalReference = "journal-ref",
            AllCopyLifecycleAuthorityReference = "all-copy-ref",
            ExpectedProfile = PlatformAcceptedEnvelopeTiming.Bind("profile-v1", "issuer", "audience", start, end),
        };
    }

    public static string InventoryProof(string tenant, string state, int windowSeconds, string? extra = null)
    {
        DateTimeOffset until = Now.AddSeconds(windowSeconds);
        string retired = state == "Retained" ? $",\"retiredAt\":\"{Now:O}\"" : "";
        return $$"""
        {"authorityReference":"inventory-app","revision":"7","tenantId":"{{tenant}}","purpose":"ContentDigest","version":"k1","keyState":"{{state}}","notBefore":"{{Now.AddMinutes(-5):O}}","verifyUntil":"{{Now.AddHours(1):O}}"{{retired}},"storeName":"store","credentialReference":"hmac-credential","secretReference":"secret/object","secretVersion":"3","secretField":"value","observedAt":"{{Now:O}}","validUntil":"{{until:O}}"{{extra}}}
        """;
    }
}
