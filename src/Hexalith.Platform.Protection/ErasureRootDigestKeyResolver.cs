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
        var reference = enrollment.Digest;
        var identity = enrollment.Binding.Identity;
        var key = reference.InventoryKey;
        if (key.TenantId != identity.Target.TenantId || key.Purpose != PlatformKeyPurpose.DigestKey || key.Version != version
            || enrollment.Binding.DigestKeyVersion != version || !ulong.TryParse(reference.NativeVersion, System.Globalization.NumberStyles.None,
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
            var result = new InteractionOccurrenceOwnedKey(identity.Target, "tenant-digest", version, owned);
            owned = null;
            return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { token.ThrowIfCancellationRequested(); return null; }
        finally { response?.Clear(); if (owned is not null) { CryptographicOperations.ZeroMemory(owned); } }
    }
}
