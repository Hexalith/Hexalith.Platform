using Dapr.Actors.Runtime;

namespace Hexalith.Platform.Custody;

/// <summary>Actual durable DAPR delivery outcome owner. Missing authority/provider remains unavailable; no production registration is installed.</summary>
/// <param name="host">Private tenant/delivery scoped runtime actor.</param>
/// <param name="clock">Current exclusive-expiry clock.</param>
/// <param name="authority">Independent current identity/lifecycle authority.</param>
/// <param name="provider">Qualified direct principal delivery owner with atomic release and exact lookup.</param>
public sealed class ExportKeyDeliveryActor(ActorHost host, TimeProvider clock, IExportKeyDeliveryAuthority? authority = null,
    IExportKeyDirectDeliveryProvider? provider = null) : Actor(host), IExportKeyDeliveryActor
{
    private const string StateKey = "export-key-delivery-v1";
    /// <summary>Gets the private actor registration identity.</summary>
    public const string ActorTypeName = "ExportKeyDeliveryActor";
    /// <inheritdoc/>
    public async Task<ExportKeyDeliveryOutcome> DeliverAsync(ExportKeyDeliveryIdentity identity)
    {
        Check(identity);
        ExportKeyDeliveryOutcome? existing = await ReadAsync(identity).ConfigureAwait(false);
        if (existing is not null) { return await ResolveAsync(identity, existing).ConfigureAwait(false); }
        if (provider is null || authority is null) { return new(identity, ExportKeyDeliveryState.Unavailable); }
        if (clock.GetUtcNow() >= identity.ExclusiveExpiry) { return await SaveAsync(new(identity, ExportKeyDeliveryState.NotDelivered, Reason: "expired")).ConfigureAwait(false); }
        if (!await authority.AuthorizeAsync(identity).ConfigureAwait(false))
        { return await SaveAsync(new(identity, ExportKeyDeliveryState.NotDelivered, Reason: "authority-denied")).ConfigureAwait(false); }
        // Save before calling the transport. A lost save acknowledgement never permits release on this call.
        await SaveAsync(new(identity, ExportKeyDeliveryState.Unknown)).ConfigureAwait(false);
        bool stillAuthorized = await authority.AuthorizeAsync(identity).ConfigureAwait(false);
        if (clock.GetUtcNow() >= identity.ExclusiveExpiry || !stillAuthorized)
        { return await SaveAsync(new(identity, ExportKeyDeliveryState.NotDelivered, Reason: "authority-or-expiry-changed")).ConfigureAwait(false); }
        try
        {
            ExportKeyDeliveryOutcome outcome = await provider.ReleaseAsync(identity).ConfigureAwait(false);
            return await RetainProviderOutcomeAsync(identity, outcome).ConfigureAwait(false);
        }
        catch (Exception) { return new(identity, ExportKeyDeliveryState.Unknown); }
    }
    /// <inheritdoc/>
    public async Task<ExportKeyDeliveryOutcome> LookupAsync(ExportKeyDeliveryIdentity identity)
    {
        Check(identity);
        var existing = await ReadAsync(identity).ConfigureAwait(false);
        return existing is null ? new(identity, ExportKeyDeliveryState.Unavailable)
            : await ResolveAsync(identity, existing).ConfigureAwait(false);
    }
    private async Task<ExportKeyDeliveryOutcome> ResolveAsync(ExportKeyDeliveryIdentity expected, ExportKeyDeliveryOutcome existing)
    {
        if (existing.Identity != expected) { return new(expected, ExportKeyDeliveryState.Conflict); }
        if (existing.State is ExportKeyDeliveryState.Delivered or ExportKeyDeliveryState.NotDelivered) { return existing; }
        if (provider is null) { return new(expected, ExportKeyDeliveryState.Unavailable); }
        try { return await RetainProviderOutcomeAsync(expected, await provider.LookupAsync(expected).ConfigureAwait(false)).ConfigureAwait(false); }
        catch (Exception) { return new(expected, ExportKeyDeliveryState.Unknown); }
    }
    private async Task<ExportKeyDeliveryOutcome> RetainProviderOutcomeAsync(ExportKeyDeliveryIdentity expected, ExportKeyDeliveryOutcome outcome)
    {
        if (outcome.Identity != expected) { return new(expected, ExportKeyDeliveryState.Conflict); }
        if (outcome.State == ExportKeyDeliveryState.Delivered && outcome.ObservedAt is { } at && at != default
            && at < expected.ExclusiveExpiry && at <= clock.GetUtcNow())
        { return await SaveAsync(new(expected, ExportKeyDeliveryState.Delivered, at)).ConfigureAwait(false); }
        if (outcome.State == ExportKeyDeliveryState.NotDelivered && outcome.ObservedAt is null)
        { return await SaveAsync(new(expected, ExportKeyDeliveryState.NotDelivered, Reason: "provider-not-delivered")).ConfigureAwait(false); }
        return new(expected, ExportKeyDeliveryState.Unknown);
    }
    private void Check(ExportKeyDeliveryIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity); identity.Validate();
        if (Host.Id.GetId() != identity.ActorId) { throw new ArgumentException("Export delivery scope mismatch.", nameof(identity)); }
    }
    private async Task<ExportKeyDeliveryOutcome?> ReadAsync(ExportKeyDeliveryIdentity identity)
    {
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        var state = await StateManager.TryGetStateAsync<ExportKeyDeliveryOutcome>(StateKey).ConfigureAwait(false);
        if (!state.HasValue) { return null; }
        var value = state.Value;
        if (value.Identity is null || value.Identity.ActorId != identity.ActorId
            || value.State is not (ExportKeyDeliveryState.Delivered or ExportKeyDeliveryState.NotDelivered or ExportKeyDeliveryState.Unknown)
            || value.State == ExportKeyDeliveryState.Delivered && (value.ObservedAt is null || value.ObservedAt >= value.Identity.ExclusiveExpiry)
            || value.State != ExportKeyDeliveryState.Delivered && value.ObservedAt is not null
            || value.Reason is not (null or "expired" or "authority-denied" or "authority-or-expiry-changed" or "provider-not-delivered")
            || value.State != ExportKeyDeliveryState.NotDelivered && value.Reason is not null)
        { throw new InvalidOperationException("Malformed persisted delivery outcome."); }
        return value;
    }
    private async Task<ExportKeyDeliveryOutcome> SaveAsync(ExportKeyDeliveryOutcome outcome)
    {
        await StateManager.SetStateAsync(StateKey, outcome).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        ExportKeyDeliveryOutcome? persisted = await ReadAsync(outcome.Identity).ConfigureAwait(false);
        if (persisted != outcome) { throw new InvalidOperationException("Delivery outcome is not confirmed durable."); }
        return outcome;
    }
}
