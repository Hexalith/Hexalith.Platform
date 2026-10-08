using System.Security.Cryptography;

namespace Hexalith.Platform.Custody;

/// <summary>Stateless private AES-256-GCM prerequisite; it grants no pin, destruction or delivery authority.</summary>
internal static class ExportKeyWrappingCore
{
    /// <summary>Wraps one exact export key with fresh nonce and purpose/tenant/export/version authenticated data.</summary>
    internal static WrappedExportKey Wrap(ExportKeyWrapContext context, ReadOnlySpan<byte> exportKey, ReadOnlySpan<byte> kek)
    {
        byte[] aad = ContextBytes(context);
        try
        {
            if (exportKey.Length != 32 || kek.Length != 32)
            {
                throw new ArgumentException("Export wrapping requires exact AES-256 keys.");
            }

            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] ciphertext = new byte[32];
            byte[] tag = new byte[16];
            using var aes = new AesGcm(kek, 16);
            aes.Encrypt(nonce, exportKey, ciphertext, tag, aad);
            return new(context, nonce, ciphertext, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aad);
        }
    }

    /// <summary>Unwraps only matching complete identity into a short-lived owned private key copy.</summary>
    internal static OwnedExportKey Unwrap(ExportKeyWrapContext expected, WrappedExportKey wrapped, ReadOnlySpan<byte> kek)
    {
        ArgumentNullException.ThrowIfNull(wrapped);
        byte[] aad = ContextBytes(expected);
        byte[] plaintext = new byte[32];
        try
        {
            if (wrapped.Context != expected || kek.Length != 32 || wrapped.Nonce is not { Length: 12 }
                || wrapped.Ciphertext is not { Length: 32 } || wrapped.Tag is not { Length: 16 })
            {
                throw new CryptographicException("Export wrapping scope or format is invalid.");
            }

            using var aes = new AesGcm(kek, 16);
            aes.Decrypt(wrapped.Nonce, wrapped.Ciphertext, wrapped.Tag, plaintext, aad);
            var owned = new OwnedExportKey(plaintext);
            plaintext = [];
            return owned;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aad);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] ContextBytes(ExportKeyWrapContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.TenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.ExportId);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.KekVersion);
        return PlatformCanonicalBytes.Components(["ExportKeyWrapV1", context.TenantId, context.ExportId, context.KekVersion]);
    }
}
