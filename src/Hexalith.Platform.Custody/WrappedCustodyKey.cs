namespace Hexalith.Platform.Custody;

/// <summary>Private purpose/complete-object/version-bound encrypted key, never a public domain response.</summary>
internal sealed record WrappedCustodyKey(CustodyKeyObjectIdentity Identity, string KekVersion, byte[] Nonce, byte[] Ciphertext, byte[] Tag)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(WrappedCustodyKey);
}
