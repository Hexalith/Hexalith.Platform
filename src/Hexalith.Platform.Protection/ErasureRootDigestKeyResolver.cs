using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Existing tenant DigestKey inventory and native-secret resolution; it cannot generate or substitute a digest/root key.</summary>
public sealed class ErasureRootDigestKeyResolver(DaprClient dapr, IPlatformKeyInventoryActor inventory, IPlatformKeyInventoryAuthority authority)
{
    /// <summary>Transfers one exact privately owned tenant-digest buffer after independent current inventory checks before and after native read.</summary>
    public async Task<InteractionOccurrenceOwnedKey?> ResolveAsync(ErasureRootOccurrenceEnrollment enrollment, string version, CancellationToken token)
    {
        if (enrollment.Binding.DigestKeyVersion != version) { return null; }
        byte[]? bytes = await ResolveNativeAsync(enrollment.Digest, enrollment.Binding.Identity.Target.TenantId, version, token).ConfigureAwait(false);
        return bytes is null ? null : new(enrollment.Binding.Identity.Target, "tenant-digest", version, bytes);
    }
    /// <summary>Validates actual native inventory, current permissions and exact key bytes before role enrollment, without issuing roots or retaining originals.</summary>
    public async Task<bool> IsAvailableAsync(CustodyInteractionDigestReference reference, CancellationToken token)
    {
        byte[]? bytes = await ResolveNativeAsync(reference, reference.InventoryKey.TenantId, reference.InventoryKey.Version, token).ConfigureAwait(false);
        if (bytes is null) { return false; }
        CryptographicOperations.ZeroMemory(bytes); return true;
    }
    private async Task<byte[]?> ResolveNativeAsync(CustodyInteractionDigestReference reference, string tenant, string version, CancellationToken token)
    {
        var key = reference.InventoryKey;
        if (key.TenantId != tenant || key.Purpose != PlatformKeyPurpose.DigestKey || key.Version != version
            || !ulong.TryParse(reference.NativeVersion, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out ulong native) || native == 0
            || native.ToString(System.Globalization.CultureInfo.InvariantCulture) != reference.NativeVersion) { return null; }
        async Task<bool> Current()
        {
            if (!await authority.AuthorizeOperationAsync(key, "ResolveInteractionOccurrenceDigest", token).ConfigureAwait(false)) { return false; }
            var state = await inventory.ReadAsync(key).WaitAsync(token).ConfigureAwait(false);
            return state.Versions.Count(value => value.Key == key && value.State != PlatformHmacKeyState.Revoked) == 1
                && await authority.ValidateStateAsync(key, state.Revision, ErasureRootCustody.Hash(state), token).ConfigureAwait(false);
        }
        Dictionary<string, string>? response = null;
        byte[]? owned = null;
        try
        {
            if (!await Current().ConfigureAwait(false)) { return null; }
            response = await dapr.GetSecretAsync(reference.ComponentName, reference.Key, new Dictionary<string, string> { ["version_id"] = reference.NativeVersion }, token).ConfigureAwait(false);
            if (response.Count != 1 || !response.TryGetValue(reference.Field, out string? encoded) || encoded.Length != 44) { return null; }
            owned = new byte[32];
            if (!Convert.TryFromBase64String(encoded, owned, out int written) || written != 32 || Convert.ToBase64String(owned) != encoded
                || !await Current().ConfigureAwait(false)) { return null; }
            token.ThrowIfCancellationRequested();
            byte[] result = owned; owned = null; return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { token.ThrowIfCancellationRequested(); return null; }
        finally { response?.Clear(); if (owned is not null) { CryptographicOperations.ZeroMemory(owned); } }
    }
}
