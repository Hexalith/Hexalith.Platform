using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal static class KeyEnvelopeBuilder
{
    // Original C0 core and outer AAD are reused byte-for-byte. Only gated ciphertext may be persisted.
    internal static async Task<StoredRegistration> WrapAsync(CustodyBindingWrapInputV1 input, byte[] tenantKek,
        string epoch, IEpochOperations native, CancellationToken token)
    {
        byte[] outerKey = RandomNumberGenerator.GetBytes(32); byte[] inner = [];
        try
        {
            var c0 = CustodyKeyWrapCore.Wrap(input.Identity, input.KekVersion, input.RootKey, tenantKek);
            inner = CustodyBindingEnvelopeBytes.Inner(c0);
            var outer = new OuterEnvelopeV1(input.Identity, input.KekVersion, input.OpaqueWrappedKeyReference,
                Guid.NewGuid().ToString("N"), RandomNumberGenerator.GetBytes(12), new byte[inner.Length], new byte[16]);
            byte[] aad = CustodyBindingEnvelopeBytes.OuterAad(outer);
            try { using var aes = new AesGcm(outerKey, 16); aes.Encrypt(outer.Nonce, inner, outer.Ciphertext, outer.Tag, aad); }
            finally { CryptographicOperations.ZeroMemory(aad); }
            byte[] encryptedOuterKey = await native.EncryptOuterKeyAsync(epoch, input.Identity.TenantId, outerKey, token);
            return new(new(input.Identity, input.OperationId, input.ExpectedRevision, input.KekVersion,
                input.OpaqueWrappedKeyReference, "wrap-" + Guid.NewGuid().ToString("N")), outer, encryptedOuterKey, epoch);
        }
        finally { CryptographicOperations.ZeroMemory(outerKey); CryptographicOperations.ZeroMemory(inner); }
    }
    internal static byte[] Unwrap(StoredRegistration registration, byte[] outerKey, byte[] tenantKek)
    {
        var outer = registration.Envelope; byte[] inner = new byte[outer.Ciphertext.Length];
        byte[] aad = CustodyBindingEnvelopeBytes.OuterAad(outer);
        try
        {
            using (var aes = new AesGcm(outerKey, 16)) aes.Decrypt(outer.Nonce, outer.Ciphertext, outer.Tag, inner, aad);
            // C0 uses JCS base64url, not the binding JSON base64 codec.
            using var json = JsonDocument.Parse(inner); var root = json.RootElement;
            byte[] Decode(string name) { string value = root.GetProperty(name).GetString()!.Replace('-', '+').Replace('_', '/');
                return Convert.FromBase64String(value.PadRight((value.Length + 3) / 4 * 4, '=')); }
            var c0 = new WrappedCustodyKey(registration.Registration.Identity, registration.Registration.KekVersion,
                Decode("Nonce"), Decode("Ciphertext"), Decode("Tag"));
            // Re-encoding verifies the complete original identity and exact C0 format, not just ciphertext fields.
            byte[] canonical = CustodyBindingEnvelopeBytes.Inner(c0);
            try { if (!inner.AsSpan().SequenceEqual(canonical)) throw new CryptographicException("C0 context substitution."); }
            finally { CryptographicOperations.ZeroMemory(canonical); }
            using var key = CustodyKeyWrapCore.Unwrap(registration.Registration.Identity, registration.Registration.KekVersion, c0, tenantKek);
            byte[] result = new byte[32]; key.CopyTo(result); return result;
        }
        finally { CryptographicOperations.ZeroMemory(inner); CryptographicOperations.ZeroMemory(aad); }
    }
}
