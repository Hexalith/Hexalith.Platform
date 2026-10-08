using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Identity;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.Platform.Custody;

/// <summary>Actual durable technical purpose/version inventory. Production registration and independent authority are unavailable by default.</summary>
/// <param name="host">Exact private tenant/purpose/alias actor.</param>
/// <param name="authority">Independent authenticated provision/revocation verifier.</param>
public sealed class PlatformKeyInventoryActor(ActorHost host, IPlatformKeyInventoryAuthority? authority = null) : Actor(host), IPlatformKeyInventoryActor
{
    private const string StateKey = "platform-key-inventory-v1";
    /// <summary>Gets private actor registration identity.</summary>
    public const string ActorTypeName = "PlatformKeyInventoryActor";
    /// <summary>Gets exact tenant/purpose/alias actor; version is intentionally in the same rotation/revocation owner.</summary>
    public static string GetActorId(PlatformKeyVersion key) => new AggregateIdentity(key.TenantId, "custody", "key-inventory-" + Digest(new[] { key.Purpose.ToString(), key.KeyAlias })).ActorId;
    /// <inheritdoc/>
    public async Task<PlatformKeyInventorySignal?> ApplyAsync(PlatformKeyInventoryChange change)
    {
        ArgumentNullException.ThrowIfNull(change); Check(change.Key);
        if (!(authority is not null && await authority.AuthorizeOperationAsync(change.Key, "ApplyKeyInventory").ConfigureAwait(false))) { return null; }
        var result = await ApplyAsyncCoreAsync(change).ConfigureAwait(false);
        if (!(authority is not null && await authority.AuthorizeOperationAsync(change.Key, "ApplyKeyInventory").ConfigureAwait(false))) { return null; }
        return result;
    }
    private async Task<PlatformKeyInventorySignal?> ApplyAsyncCoreAsync(PlatformKeyInventoryChange change)
    {
        ArgumentNullException.ThrowIfNull(change); Check(change.Key); Text(change.OperationId); Text(change.AuthenticatedEvidenceId);
        if (!Enum.IsDefined(change.Action) || change.ExpectedInventoryRevision < 0) { throw new ArgumentException("Invalid inventory change.", nameof(change)); }
        if (authority is null || !await authority.AuthorizeOperationAsync(change.Key, "ApplyKeyInventory").ConfigureAwait(false)) { return null; }
        var state = await ReadStateAsync(change.Key).ConfigureAwait(false); string digest = Digest(change);
        var prior = state.Signals.SingleOrDefault(s => s.OperationId == change.OperationId);
        if (prior is not null) { if (prior.RequestDigest != digest) { throw new ArgumentException("Changed key operation identity.", nameof(change)); } return prior; }
        if (state.Revision != change.ExpectedInventoryRevision || authority is null || !await authority.AuthorizeAsync(change).ConfigureAwait(false)) { return null; }
        var found = state.Versions.SingleOrDefault(v => v.Key.Version == change.Key.Version);
        if (found is not null && found.Key != change.Key) { return null; }
        if (state.Signals.Count >= 10000 || change.Action == PlatformKeyInventoryAction.InstallCurrent && state.Versions.Count >= 1000) { return null; }
        long revision = checked(state.Revision + 1); var versions = state.Versions.ToList();
        if (change.Action == PlatformKeyInventoryAction.InstallCurrent)
        {
            if (found is not null) { return null; }
            versions = versions.Select(v => v.State == PlatformHmacKeyState.Active ? v with { State = PlatformHmacKeyState.Retained, ChangedAtRevision = revision } : v).ToList();
            versions.Add(new(change.Key, PlatformHmacKeyState.Active, revision));
        }
        else
        {
            if (found is null || found.State == PlatformHmacKeyState.Revoked) { return null; }
            versions = versions.Select(v => v == found ? v with { State = PlatformHmacKeyState.Revoked, ChangedAtRevision = revision } : v).ToList();
        }
        var signal = new PlatformKeyInventorySignal(change.OperationId, digest, revision, change.Action, change.Key);
        var next = state with { Revision = revision, Versions = Array.AsReadOnly(versions.ToArray()), Signals = Array.AsReadOnly(state.Signals.Append(signal).ToArray()) };
        if (!await authority.RecordRevisionAsync(change.Key, state.Revision, next.Revision, Digest(next)).ConfigureAwait(false))
        { throw new InvalidOperationException("Independent inventory anchor compare failed."); }
        await StateManager.SetStateAsync(StateKey, next).ConfigureAwait(false); await StateManager.SaveStateAsync().ConfigureAwait(false);
        if (Digest(await ReadStateAsync(change.Key).ConfigureAwait(false)) != Digest(next)) { throw new InvalidOperationException("Inventory outcome not confirmed durable."); }
        return signal;
    }
    /// <inheritdoc/>
    public async Task<PlatformKeyInventorySnapshot> ReadAsync(PlatformKeyVersion scope)
    {
        Check(scope);
        if (!(authority is not null && await authority.AuthorizeOperationAsync(scope, "ReadKeyInventory").ConfigureAwait(false))) { throw new UnauthorizedAccessException("Private key inventory read denied."); }
        var result = await ReadAsyncCoreAsync(scope).ConfigureAwait(false);
        if (!(authority is not null && await authority.AuthorizeOperationAsync(scope, "ReadKeyInventory").ConfigureAwait(false))) { throw new UnauthorizedAccessException("Private key inventory read denied."); }
        return result;
    }
    private async Task<PlatformKeyInventorySnapshot> ReadAsyncCoreAsync(PlatformKeyVersion scope)
    {
        Check(scope);
        if (authority is null || !await authority.AuthorizeOperationAsync(scope, "ReadKeyInventory").ConfigureAwait(false)) { throw new UnauthorizedAccessException("Private key inventory read denied."); }
        return await ReadStateAsync(scope).ConfigureAwait(false);
    }
    private async Task<PlatformKeyInventorySnapshot> ReadStateAsync(PlatformKeyVersion scope)
    {
        Check(scope); await StateManager.ClearCacheAsync().ConfigureAwait(false); var result = await StateManager.TryGetStateAsync<PlatformKeyInventorySnapshot>(StateKey).ConfigureAwait(false);
        if (!result.HasValue)
        {
            var initial = new PlatformKeyInventorySnapshot(scope.TenantId, scope.Purpose, scope.KeyAlias, 0, [], []);
            if (authority is null || !await authority.ValidateStateAsync(scope, 0, Digest(initial)).ConfigureAwait(false)) { throw new InvalidOperationException("Independent inventory anchor is absent or stale."); }
            return initial;
        }
        var state = result.Value;
        if (state.TenantId != scope.TenantId || state.Purpose != scope.Purpose || state.KeyAlias != scope.KeyAlias || state.Revision <= 0
            || state.Versions is null || state.Signals is null || state.Versions.Count > 1000 || state.Signals.Count > 10000 || state.Signals.Count != state.Revision
            || state.Versions.Count(v => v.State == PlatformHmacKeyState.Active) > 1 || state.Versions.Select(v => v.Key.Version).Distinct(StringComparer.Ordinal).Count() != state.Versions.Count)
        { throw new InvalidOperationException("Malformed durable key inventory."); }
        foreach (var entry in state.Versions)
        {
            Check(entry.Key);
            if (entry.State is not (PlatformHmacKeyState.Active or PlatformHmacKeyState.Retained or PlatformHmacKeyState.Revoked)
                || entry.ChangedAtRevision <= 0 || entry.ChangedAtRevision > state.Revision) { throw new InvalidOperationException("Malformed durable key version."); }
        }
        if (state.Signals.Where((s, i) => s.InventoryRevision != i + 1 || s.Key.TenantId != state.TenantId || s.Key.Purpose != state.Purpose
                || s.Key.KeyAlias != state.KeyAlias || !Enum.IsDefined(s.Action)).Any()
            || state.Signals.Select(s => s.OperationId).Distinct(StringComparer.Ordinal).Count() != state.Signals.Count)
        { throw new InvalidOperationException("Malformed durable key signals."); }
        var owned = state with { Versions = Array.AsReadOnly(state.Versions.ToArray()), Signals = Array.AsReadOnly(state.Signals.ToArray()) };
        if (authority is null || !await authority.ValidateStateAsync(scope, owned.Revision, Digest(owned)).ConfigureAwait(false)) { throw new InvalidOperationException("Independent inventory anchor is absent or stale."); }
        return owned;
    }
    private void Check(PlatformKeyVersion key)
    {
        ArgumentNullException.ThrowIfNull(key); foreach (var value in new[] { key.TenantId, key.KeyAlias, key.Version, key.ProviderTarget, key.AuthorityReference }) { Text(value); }
        if (!Enum.IsDefined(key.Purpose) || key.Purpose == PlatformKeyPurpose.SecurityObservationDigestKey && key.TenantId != "system"
            || (key.PublicAnchorId is null) != (key.PublicAnchorVersion is null)) { throw new ArgumentException("Invalid key purpose/anchor.", nameof(key)); }
        if (key.PublicAnchorId is not null) { Text(key.PublicAnchorId); Text(key.PublicAnchorVersion!); }
        if (Host.Id.GetId() != GetActorId(key)) { throw new ArgumentException("Key inventory scope mismatch.", nameof(key)); }
    }
    private static void Text(string value)
    { if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 || new UTF8Encoding(false, true).GetByteCount(value) > 2048) { throw new ArgumentException("Invalid key identity."); } }
    private static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
