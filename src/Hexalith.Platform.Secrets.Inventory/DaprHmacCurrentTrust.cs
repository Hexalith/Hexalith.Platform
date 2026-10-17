using System.Globalization;
using System.Text.Json.Nodes;

using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Secrets.Inventory;

/// <summary>Requires actual reads from independently enrolled profile and revocation services; missing services or proofs deny.</summary>
public sealed class DaprHmacCurrentTrust(SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker invoker, TimeProvider clock) : IHmacCurrentTrust
{
    /// <inheritdoc/>
    public async Task<CurrentHmacTrust?> ReadAsync(PlatformHmacScope? scope, string? version, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration.PublicTrustReference) || string.IsNullOrWhiteSpace(configuration.CurrentRevocationReference)
            || string.IsNullOrWhiteSpace(configuration.CurrentInventoryAuthorityReference) || configuration.ProfileReference is not { Length: > 0 } profileReference
            || configuration.ExpectedProfile is not { } expected) { return null; }
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var profileReader = new DaprHmacAuthorityAdapter(configuration, invoker, clock);
            Task<PlatformSigningProfile?> pending = Task.Run(() => profileReader.GetCurrentProfile(profileReference), CancellationToken.None);
            _ = pending.ContinueWith(task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            PlatformSigningProfile? profile = await pending.WaitAsync(budget.Token).ConfigureAwait(false);
            DateTimeOffset now = clock.GetUtcNow();
            if (profile != expected || now < expected.NotBefore || now >= expected.ValidUntil) { return null; }
            var request = new JsonObject
            {
                ["authorityReference"] = configuration.CurrentRevocationReference,
                ["publicTrustReference"] = configuration.PublicTrustReference, ["profileReference"] = profileReference,
                ["inventoryAuthorityReference"] = configuration.CurrentInventoryAuthorityReference,
                ["profileVersion"] = expected.Version, ["issuer"] = expected.Issuer, ["audience"] = expected.Audience,
                ["tenantId"] = scope?.TenantId, ["purpose"] = scope?.Purpose.ToString(), ["version"] = version,
            };
            string? json = await invoker.InvokeAsync(configuration.CurrentRevocationReference, "query-hmac-revocation", request.ToJsonString(), budget.Token)
                .WaitAsync(budget.Token).ConfigureAwait(false);
            JsonObject? proof = json is null ? null : InventorySources.ParseObject(json);
            if (proof is null || request.Any(pair => !JsonNode.DeepEquals(pair.Value, proof[pair.Key]))
                || proof["status"]?.GetValue<string>() != "Current" || proof["revoked"]?.GetValue<bool>() != false
                || proof["inventoryHostEnrolled"]?.GetValue<bool>() != true || proof["revision"]?.GetValue<string>() is not { Length: > 0 } revision
                || !DateTimeOffset.TryParse(proof["observedAt"]?.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset observed)
                || !DateTimeOffset.TryParse(proof["validUntil"]?.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset validUntil)
                || observed.Offset != TimeSpan.Zero || validUntil.Offset != TimeSpan.Zero) { return null; }
            now = clock.GetUtcNow();
            return observed <= now && now < validUntil && validUntil > observed && validUntil - observed <= TimeSpan.FromSeconds(30)
                && now < expected.ValidUntil && !budget.IsCancellationRequested ? new(profile!, revision, observed, validUntil) : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return null; }
    }
}
