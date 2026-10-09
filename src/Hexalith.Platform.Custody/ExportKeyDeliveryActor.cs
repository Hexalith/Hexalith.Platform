using Hexalith.EventStore.Contracts.Security;
using System.Security.Cryptography;
using System.Text.Json;
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
    private readonly PrivateActorStateIoLifetime _stateIo = new();
    private const string StateKey = "export-key-delivery-v1";
    /// <summary>Gets the private actor registration identity.</summary>
    public const string ActorTypeName = "ExportKeyDeliveryActor";
    /// <inheritdoc/>
    public async Task<ExportKeyDeliveryOutcome> DeliverAsync(ExportKeyDeliveryIdentity identity)
    {
        var budget = new PrivateOwnerOperationDeadline(clock, CancellationToken.None);
        Check(identity);
        ExportKeyDeliveryOutcome? uncertainty = null;
        try
        {
            budget.Check(); _stateIo.CheckReady();
            if (!(authority is not null && await budget.ReadAsync(() => authority.AuthorizeOperationAsync(identity, "DeliverExportKey")).ConfigureAwait(false))) { return new(identity, ExportKeyDeliveryState.Unavailable); }
            var result = await DeliverAsyncCoreAsync(identity, budget).ConfigureAwait(false);
            if (result.State == ExportKeyDeliveryState.Unknown) { uncertainty = result; }
            if (!(authority is not null && await budget.ReadAsync(() => authority.AuthorizeOperationAsync(identity, "DeliverExportKey")).ConfigureAwait(false))) { return new(identity, ExportKeyDeliveryState.Unavailable); }
            budget.Check(); return result;

        }
        catch (TimeoutException) { return uncertainty ?? new(identity, ExportKeyDeliveryState.Unavailable); }
    }
    private async Task<ExportKeyDeliveryOutcome> DeliverAsyncCoreAsync(ExportKeyDeliveryIdentity identity, PrivateOwnerOperationDeadline budget)
    {
        Check(identity);
        if (authority is null || !await budget.ReadAsync(() => authority.AuthorizeOperationAsync(identity, "DeliverExportKey")).ConfigureAwait(false)) { return new(identity, ExportKeyDeliveryState.Unavailable); }
        ExportKeyDeliveryOutcome? existing = await ReadAsync(budget, identity, true).ConfigureAwait(false);
        if (existing is not null) { return await ResolveAsync(identity, existing, budget).ConfigureAwait(false); }
        if (provider is null || authority is null) { return new(identity, ExportKeyDeliveryState.Unavailable); }
        if (clock.GetUtcNow() >= identity.ExclusiveExpiry) { return await SaveAsync(budget, new(identity, ExportKeyDeliveryState.NotDelivered, Reason: "expired")).ConfigureAwait(false); }
        if (!await budget.ReadAsync(() => authority.AuthorizeAsync(identity)).ConfigureAwait(false))
        { return await SaveAsync(budget, new(identity, ExportKeyDeliveryState.NotDelivered, Reason: "authority-denied")).ConfigureAwait(false); }
        // Save before calling the transport. A lost save acknowledgement never permits release on this call.
        await SaveAsync(budget, new(identity, ExportKeyDeliveryState.Unknown)).ConfigureAwait(false);
        bool stillAuthorized = await budget.ReadAsync(() => authority.AuthorizeAsync(identity)).ConfigureAwait(false);
        if (clock.GetUtcNow() >= identity.ExclusiveExpiry || !stillAuthorized)
        { return await SaveAsync(budget, new(identity, ExportKeyDeliveryState.NotDelivered, Reason: "authority-or-expiry-changed")).ConfigureAwait(false); }
        try
        {
            ExportKeyDeliveryOutcome outcome = await budget.ReadAsync(() => provider.ReleaseAsync(identity)).ConfigureAwait(false);
            return await RetainProviderOutcomeAsync(identity, outcome, budget).ConfigureAwait(false);
        }
        catch (TimeoutException) { return new(identity, ExportKeyDeliveryState.Unknown); }
        catch (Exception) { budget.Check(); return new(identity, ExportKeyDeliveryState.Unknown); }
    }
    /// <inheritdoc/>
    public async Task<ExportKeyDeliveryOutcome> LookupAsync(ExportKeyDeliveryIdentity identity)
    {
        var budget = new PrivateOwnerOperationDeadline(clock, CancellationToken.None);
        Check(identity);
        ExportKeyDeliveryOutcome? uncertainty = null;
        try
        {
            budget.Check(); _stateIo.CheckReady();
            if (!(authority is not null && await budget.ReadAsync(() => authority.AuthorizeOperationAsync(identity, "LookupExportKey")).ConfigureAwait(false))) { return new(identity, ExportKeyDeliveryState.Unavailable); }
            var result = await LookupAsyncCoreAsync(identity, budget).ConfigureAwait(false);
            if (result.State == ExportKeyDeliveryState.Unknown) { uncertainty = result; }
            if (!(authority is not null && await budget.ReadAsync(() => authority.AuthorizeOperationAsync(identity, "LookupExportKey")).ConfigureAwait(false))) { return new(identity, ExportKeyDeliveryState.Unavailable); }
            budget.Check(); return result;

        }
        catch (TimeoutException) { return uncertainty ?? new(identity, ExportKeyDeliveryState.Unavailable); }
    }
    private async Task<ExportKeyDeliveryOutcome> LookupAsyncCoreAsync(ExportKeyDeliveryIdentity identity, PrivateOwnerOperationDeadline budget)
    {
        Check(identity);
        if (authority is null || !await budget.ReadAsync(() => authority.AuthorizeOperationAsync(identity, "LookupExportKey")).ConfigureAwait(false)) { return new(identity, ExportKeyDeliveryState.Unavailable); }
        var existing = await ReadAsync(budget, identity).ConfigureAwait(false);
        return existing is null ? new(identity, ExportKeyDeliveryState.Unavailable)
            : await ResolveAsync(identity, existing, budget).ConfigureAwait(false);
    }
    private async Task<ExportKeyDeliveryOutcome> ResolveAsync(ExportKeyDeliveryIdentity expected, ExportKeyDeliveryOutcome existing, PrivateOwnerOperationDeadline budget)
    {
        if (existing.Identity != expected) { return new(expected, ExportKeyDeliveryState.Conflict); }
        if (existing.State is ExportKeyDeliveryState.Delivered or ExportKeyDeliveryState.NotDelivered) { return existing; }
        if (provider is null) { return new(expected, ExportKeyDeliveryState.Unavailable); }
        try { return await RetainProviderOutcomeAsync(expected, await budget.ReadAsync(() => provider.LookupAsync(expected)).ConfigureAwait(false), budget).ConfigureAwait(false); }
        catch (TimeoutException) { return new(expected, ExportKeyDeliveryState.Unknown); }
        catch (Exception) { budget.Check(); return new(expected, ExportKeyDeliveryState.Unknown); }
    }
    private async Task<ExportKeyDeliveryOutcome> RetainProviderOutcomeAsync(ExportKeyDeliveryIdentity expected, ExportKeyDeliveryOutcome outcome, PrivateOwnerOperationDeadline budget)
    {
        budget.Check();
        if (outcome.Identity != expected) { return new(expected, ExportKeyDeliveryState.Conflict); }
        if (outcome.State == ExportKeyDeliveryState.Delivered && outcome.ObservedAt is { } at && at != default
            && at < expected.ExclusiveExpiry && at <= clock.GetUtcNow())
        { return await SaveAsync(budget, new(expected, ExportKeyDeliveryState.Delivered, at)).ConfigureAwait(false); }
        if (outcome.State == ExportKeyDeliveryState.NotDelivered && outcome.ObservedAt is null)
        { return await SaveAsync(budget, new(expected, ExportKeyDeliveryState.NotDelivered, Reason: "provider-not-delivered")).ConfigureAwait(false); }
        return new(expected, ExportKeyDeliveryState.Unknown);
    }
    private void Check(ExportKeyDeliveryIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity); identity.Validate();
        if (Host.Id.GetId() != identity.ActorId) { throw new ArgumentException("Export delivery scope mismatch.", nameof(identity)); }
    }
    private async Task<ExportKeyDeliveryOutcome?> ReadAsync(PrivateOwnerOperationDeadline budget, ExportKeyDeliveryIdentity identity, bool recoverAdmittedOriginal = false)
    {
        await _stateIo.ReadAsync(budget, () => StateManager.ClearCacheAsync()).ConfigureAwait(false);
        var state = await _stateIo.ReadAsync(budget, () => StateManager.TryGetStateAsync<ExportKeyDeliveryOutcome>(StateKey)).ConfigureAwait(false);
        if (authority is null) { throw new InvalidOperationException("Independent exact outcome authority is absent."); }
        return await RecoverableAnchoredState.ReconcileAsync(PendingScope, state.HasValue ? CaptureOutcome(state.Value, identity) : null,
            await ReadPendingAsync(budget).ConfigureAwait(false), value => value is null ? null : CaptureOutcome(value, identity),
            value => budget.ReadAsync(() => authority.ValidateStateAsync(identity, Digest(value))), new DeadlineAnchoredStateAuthority(authority, budget),
            value => value is null ? throw new InvalidOperationException("Null prospective outcome.") : PersistTargetAsync(budget, value), recoverAdmittedOriginal).ConfigureAwait(false);
    }
    private static ExportKeyDeliveryOutcome CaptureOutcome(ExportKeyDeliveryOutcome value, ExportKeyDeliveryIdentity identity)
    {
        if (value.Identity is null || value.Identity.ActorId != identity.ActorId
            || value.State is not (ExportKeyDeliveryState.Delivered or ExportKeyDeliveryState.NotDelivered or ExportKeyDeliveryState.Unknown)
            || value.State == ExportKeyDeliveryState.Delivered && (value.ObservedAt is null || value.ObservedAt >= value.Identity.ExclusiveExpiry)
            || value.State != ExportKeyDeliveryState.Delivered && value.ObservedAt is not null
            || value.Reason is not (null or "expired" or "authority-denied" or "authority-or-expiry-changed" or "provider-not-delivered")
            || value.State != ExportKeyDeliveryState.NotDelivered && value.Reason is not null)
        { throw new InvalidOperationException("Malformed persisted delivery outcome."); }
        return value;
    }
    private async Task<ExportKeyDeliveryOutcome> SaveAsync(PrivateOwnerOperationDeadline budget, ExportKeyDeliveryOutcome outcome)
    {
        var previous = await ReadAsync(budget, outcome.Identity).ConfigureAwait(false);
        if (authority is null) { throw new InvalidOperationException("Independent exact outcome authority is absent."); }
        long expected = previous is null ? 0 : 1;
        var pending = RecoverableAnchoredState.Prepare(PendingScope, expected, checked(expected + 1), previous, outcome);
        if (!await RecoverableAnchoredState.CommitAsync(pending, new DeadlineAnchoredStateAuthority(authority, budget), () => ReadPendingAsync(budget), pendingValue => PersistPendingAsync(budget, pendingValue)).ConfigureAwait(false))
        { throw new InvalidOperationException("Independent exact outcome transition compare failed."); }
        ExportKeyDeliveryOutcome? persisted = await ReadAsync(budget, outcome.Identity).ConfigureAwait(false);
        if (persisted != outcome) { throw new InvalidOperationException("Delivery outcome is not confirmed durable."); }
        return outcome;
    }

    private string PendingScope => Host.Id.GetId() + "|" + StateKey;
    private const string PendingKey = StateKey + "-pending-transition-v1";
    private async Task<AnchoredStateTransition?> ReadPendingAsync(PrivateOwnerOperationDeadline budget)
    {
        await _stateIo.ReadAsync(budget, () => StateManager.ClearCacheAsync()).ConfigureAwait(false);
        var pending = await _stateIo.ReadAsync(budget, () => StateManager.TryGetStateAsync<AnchoredStateTransition>(PendingKey)).ConfigureAwait(false);
        return pending.HasValue ? pending.Value : null;
    }
    private async Task PersistPendingAsync(PrivateOwnerOperationDeadline budget, AnchoredStateTransition pending)
    {
        await _stateIo.ReadAsync(budget, () => StateManager.SetStateAsync(PendingKey, pending)).ConfigureAwait(false); await _stateIo.ReadAsync(budget, () => StateManager.SaveStateAsync()).ConfigureAwait(false);
    }
    private async Task<ExportKeyDeliveryOutcome?> PersistTargetAsync(PrivateOwnerOperationDeadline budget, ExportKeyDeliveryOutcome next)
    {
        await _stateIo.ReadAsync(budget, () => StateManager.SetStateAsync(StateKey, next)).ConfigureAwait(false);
        _ = await _stateIo.ReadAsync(budget, () => StateManager.TryRemoveStateAsync(PendingKey)).ConfigureAwait(false); await _stateIo.ReadAsync(budget, () => StateManager.SaveStateAsync()).ConfigureAwait(false);
        await _stateIo.ReadAsync(budget, () => StateManager.ClearCacheAsync()).ConfigureAwait(false);
        var confirmed = await _stateIo.ReadAsync(budget, () => StateManager.TryGetStateAsync<ExportKeyDeliveryOutcome>(StateKey)).ConfigureAwait(false);
        return confirmed.HasValue ? confirmed.Value : throw new InvalidOperationException("Reconciled main state is missing.");
    }
    private static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

}
