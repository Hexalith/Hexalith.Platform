using System.Security.Cryptography;

namespace Hexalith.Platform.Custody;

/// <summary>Private complete export/interaction-root wrapping prerequisite, separate from lifecycle/physical authorization.</summary>
internal static class CustodyKeyWrapCore
{
    /// <summary>Wraps exactly one original 32-byte key with fresh nonce and complete purpose/object/alias/version/lifecycle/target/KEK authenticated context.</summary>
    internal static WrappedCustodyKey Wrap(CustodyKeyObjectIdentity identity, string kekVersion, ReadOnlySpan<byte> key, ReadOnlySpan<byte> kek)
    {
        byte[] aad = Context(identity, kekVersion);
        try
        {
            if (key.Length != 32 || kek.Length != 32) { throw new CryptographicException("Exact AES-256 keys required."); }
            byte[] nonce = RandomNumberGenerator.GetBytes(12), cipher = new byte[32], tag = new byte[16];
            using var aes = new AesGcm(kek, 16); aes.Encrypt(nonce, key, cipher, tag, aad); return new(identity, kekVersion, nonce, cipher, tag);
        }
        finally { CryptographicOperations.ZeroMemory(aad); }
    }
    /// <summary>Unwraps only the original complete purpose-bound object into an actually zeroed short-lived owned private buffer.</summary>
    internal static OwnedExportKey Unwrap(CustodyKeyObjectIdentity expected, string kekVersion, WrappedCustodyKey wrapped, ReadOnlySpan<byte> kek)
    {
        ArgumentNullException.ThrowIfNull(wrapped); byte[] aad = Context(expected, kekVersion); byte[] plaintext = new byte[32];
        try
        {
            if (wrapped.Identity != expected || wrapped.KekVersion != kekVersion || kek.Length != 32 || wrapped.Nonce is not { Length: 12 }
                || wrapped.Ciphertext is not { Length: 32 } || wrapped.Tag is not { Length: 16 }) { throw new CryptographicException("Wrapped object context mismatch."); }
            using var aes = new AesGcm(kek, 16); aes.Decrypt(wrapped.Nonce, wrapped.Ciphertext, wrapped.Tag, plaintext, aad);
            var result = new OwnedExportKey(plaintext); plaintext = []; return result;
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); CryptographicOperations.ZeroMemory(aad); }
    }
    private static byte[] Context(CustodyKeyObjectIdentity i, string kek)
    { CustodyKeyLifecycleActor.ValidateIdentity(i); return PlatformCanonicalBytes.Components(["candidate-custody-key-wrap-v1", i.TenantId, i.ObjectId, i.Purpose.ToString(),
        i.KeyAlias, i.KeyVersion, i.LifecycleVersion, i.StoreTarget, i.ContractVersion, kek]); }
}
