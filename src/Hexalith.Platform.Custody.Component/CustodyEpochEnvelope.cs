using System.Security.Cryptography;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Custody.Component;

/// <summary>Seals C0 inside an epoch envelope. Epoch key bytes never enter this type's stored result.</summary>
internal static class CustodyEpochEnvelope
{
    internal static byte[] SealC0(CustodyKeyObjectIdentity identity, string kekVersion, ReadOnlySpan<byte> dek, ReadOnlySpan<byte> kek, ReadOnlySpan<byte> targetKey, string generation)
    {
        WrappedCustodyKey c0 = CustodyKeyWrapCore.Wrap(identity, kekVersion, dek, kek);
        byte[] plain = new byte[60];
        try
        {
            c0.Nonce.CopyTo(plain, 0);
            c0.Ciphertext.CopyTo(plain, 12);
            c0.Tag.CopyTo(plain, 44);
            return Seal(targetKey, plain, OuterAad(identity, kekVersion, generation));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            CryptographicOperations.ZeroMemory(c0.Nonce);
            CryptographicOperations.ZeroMemory(c0.Ciphertext);
            CryptographicOperations.ZeroMemory(c0.Tag);
        }
    }

    internal static byte[] OpenDek(CustodyKeyObjectIdentity identity, string kekVersion, string generation, byte[] nonce, byte[] ciphertext, byte[] tag, ReadOnlySpan<byte> targetKey, ReadOnlySpan<byte> kek)
    {
        byte[] plain = Open(targetKey, nonce, ciphertext, tag, OuterAad(identity, kekVersion, generation));
        try
        {
            if (plain.Length != 60) { throw new CryptographicException("Outer envelope length mismatch."); }
            var wrapped = new WrappedCustodyKey(identity, kekVersion, plain[..12], plain[12..44], plain[44..60]);
            using OwnedExportKey owned = CustodyKeyWrapCore.Unwrap(identity, kekVersion, wrapped, kek);
            byte[] dek = new byte[32];
            owned.CopyTo(dek);
            CryptographicOperations.ZeroMemory(wrapped.Nonce);
            CryptographicOperations.ZeroMemory(wrapped.Ciphertext);
            CryptographicOperations.ZeroMemory(wrapped.Tag);
            return dek;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    internal static byte[] Seal(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> aad)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] cipher = new byte[plaintext.Length];
        byte[] tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plaintext, cipher, tag, aad);
        byte[] packed = new byte[12 + cipher.Length + 16];
        nonce.CopyTo(packed, 0);
        cipher.CopyTo(packed, 12);
        tag.CopyTo(packed, 12 + cipher.Length);
        CryptographicOperations.ZeroMemory(nonce);
        CryptographicOperations.ZeroMemory(cipher);
        CryptographicOperations.ZeroMemory(tag);
        return packed;
    }

    internal static byte[] Open(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, ReadOnlySpan<byte> aad)
    {
        byte[] plain = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, ciphertext, tag, plain, aad);
        return plain;
    }

    internal static void Split(byte[] packed, int plaintextLength, out byte[] nonce, out byte[] ciphertext, out byte[] tag)
    {
        if (packed.Length != 12 + plaintextLength + 16) { throw new CryptographicException("Packed envelope length mismatch."); }
        nonce = packed[..12];
        ciphertext = packed[12..(12 + plaintextLength)];
        tag = packed[(12 + plaintextLength)..];
    }

    internal static byte[] OuterAad(CustodyKeyObjectIdentity identity, string kekVersion, string generation)
        => PlatformCanonicalBytes.Components(["custody-epoch-outer-v1", identity.TenantId, identity.ObjectId, identity.Purpose.ToString(),
            identity.KeyAlias, identity.KeyVersion, identity.LifecycleVersion, identity.StoreTarget, identity.ContractVersion, kekVersion, generation]);

    internal static byte[] KeyAad(CustodyKeyObjectIdentity identity, string generation)
        => PlatformCanonicalBytes.Components(["custody-epoch-x-v1", identity.TenantId, identity.ObjectId, identity.Purpose.ToString(),
            identity.KeyAlias, identity.KeyVersion, identity.LifecycleVersion, identity.StoreTarget, identity.ContractVersion, generation]);
}
