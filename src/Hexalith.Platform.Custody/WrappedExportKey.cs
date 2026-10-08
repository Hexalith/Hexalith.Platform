namespace Hexalith.Platform.Custody;

/// <summary>Private encrypted key bytes; this value is not an Agents response or lifecycle receipt.</summary>
/// <param name="Context">Authenticated exact wrapping identity.</param>
/// <param name="Nonce">The fresh twelve-byte GCM nonce.</param>
/// <param name="Ciphertext">The encrypted thirty-two-byte export key.</param>
/// <param name="Tag">The sixteen-byte authentication tag.</param>
internal sealed record WrappedExportKey(ExportKeyWrapContext Context, byte[] Nonce, byte[] Ciphertext, byte[] Tag);
