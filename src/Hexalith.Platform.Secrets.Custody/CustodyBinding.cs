using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Secrets.Custody;

/// <summary>The existing custody composition; readiness observes public metadata without retrieving key material.</summary>
public sealed class CustodyBinding(SecretsRuntimeConfiguration runtime, IDaprPlatformHmacAuthority authority,
    JsonArray permitted, string? missingInput, Func<bool> transportConfigured)
{
    /// <summary>Gets the missing explicit opt-in input.</summary>
    public string? MissingInput { get; } = missingInput;

    /// <summary>Composes the shipped custody services and exact HMAC adapter without adding lifecycle or signer authority.</summary>
    public static CustodyBinding Compose(IServiceCollection services, SecretsRuntimeConfiguration runtime,
        ISecretsDaprInvoker invoker, JsonArray permitted, Func<bool> transportConfigured)
    {
        services.AddPlatformCustody();
        string? missing = services.AddExplicitHmac(runtime, invoker);
        var authority = new DaprHmacAuthorityAdapter(runtime, invoker);
        services.AddSingleton<IDaprPlatformHmacAuthority>(authority);
        return new(runtime, authority, permitted, missing, transportConfigured);
    }

    /// <summary>Requires an exact allowed scope; the HTTP receiver separately authenticates its tenant grant.</summary>
    public bool Permits(string tenant, PlatformHmacPurpose purpose) => permitted.OfType<JsonObject>().Count(item =>
        item["tenantId"]?.GetValue<string>() == tenant && item["purpose"]?.GetValue<string>() == purpose.ToString()) == 1;

    /// <summary>Checks only current public profile and exact inventory proofs; no provider data or HMAC operation is requested.</summary>
    public async Task<bool> ReadyAsync(CancellationToken cancellationToken)
    {
        if (MissingInput is not null || !transportConfigured() || permitted.Count != 4 || runtime.ExpectedProfile is null
            || runtime.CurrentInventoryAuthorityReference == runtime.ProfileReference) { return false; }
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            Task<PlatformSigningProfile?> profile = Task.Run(() => authority.GetCurrentProfile(runtime.ProfileReference!), CancellationToken.None);
            _ = profile.ContinueWith(task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            if (await profile.WaitAsync(budget.Token).ConfigureAwait(false) != runtime.ExpectedProfile) { return false; }
            var seen = new HashSet<PlatformHmacScope>();
            var expiries = new List<DateTimeOffset>();
            foreach (JsonNode? node in permitted)
            {
                if (node is not JsonObject item || item["tenantId"]?.GetValue<string>() is not { Length: > 0 } tenant
                    || item["purpose"]?.GetValue<string>() is not { } purpose
                    || !Enum.TryParse(purpose, out PlatformHmacPurpose parsed)
                    || parsed is not (PlatformHmacPurpose.TrustedEnvelope or PlatformHmacPurpose.ContentDigest)
                    || parsed.ToString() != purpose) { return false; }
                var scope = new PlatformHmacScope(tenant, parsed);
                if (!scope.IsValid || !seen.Add(scope)) { return false; }
                DaprPlatformHmacInventory? proof = await authority.ResolveAsync(runtime.CurrentInventoryAuthorityReference!, scope, null, budget.Token)
                    .AsTask().WaitAsync(budget.Token).ConfigureAwait(false);
                DateTimeOffset now = DateTimeOffset.UtcNow;
                if (proof is null || proof.Scope != scope || proof.AuthorityReference != runtime.CurrentInventoryAuthorityReference
                    || proof.StoreName != runtime.ProviderBinding || proof.CredentialReference != runtime.HmacCredentialReference
                    || proof.Metadata.State != PlatformHmacKeyState.Active || proof.Metadata.NotBefore > now || now >= proof.Metadata.VerifyUntil
                    || proof.ObservedAt > now || now >= proof.ValidUntil || proof.ValidUntil - proof.ObservedAt > TimeSpan.FromSeconds(30)) { return false; }
                expiries.Add(proof.ValidUntil);
            }
            return !budget.IsCancellationRequested && expiries.All(expiry => DateTimeOffset.UtcNow < expiry)
                && DateTimeOffset.UtcNow < runtime.ExpectedProfile.ValidUntil;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return false; }
    }
}
