namespace Hexalith.Platform.Custody.Component;

/// <summary>Volume copy of one epoch envelope. C0 stays inside the outer seal. The epoch key itself is not this object.</summary>
internal sealed class CustodyComponentEnvelope
{
    internal CustodyComponentEnvelope(CustodyKeyObjectIdentity identity, string kekVersion, string sealedGeneration, string wrapGeneration,
        byte[] outerNonce, byte[] outerCiphertext, byte[] outerTag, byte[] wrappedKeyNonce, byte[] wrappedKeyCiphertext, byte[] wrappedKeyTag)
    {
        Identity = identity;
        KekVersion = kekVersion;
        SealedGeneration = sealedGeneration;
        WrapGeneration = wrapGeneration;
        OuterNonce = outerNonce;
        OuterCiphertext = outerCiphertext;
        OuterTag = outerTag;
        WrappedKeyNonce = wrappedKeyNonce;
        WrappedKeyCiphertext = wrappedKeyCiphertext;
        WrappedKeyTag = wrappedKeyTag;
    }

    internal CustodyKeyObjectIdentity Identity { get; }
    internal string KekVersion { get; }
    /// <summary>Generation bound into the outer seal. Restore must not rewrite it.</summary>
    internal string SealedGeneration { get; }
    /// <summary>Generation that wraps X on this volume. A restored old value is not current.</summary>
    internal string WrapGeneration { get; private set; }
    internal byte[] OuterNonce { get; private set; }
    internal byte[] OuterCiphertext { get; private set; }
    internal byte[] OuterTag { get; private set; }
    internal byte[] WrappedKeyNonce { get; private set; }
    internal byte[] WrappedKeyCiphertext { get; private set; }
    internal byte[] WrappedKeyTag { get; private set; }

    internal void Rewrap(string generation, byte[] nonce, byte[] ciphertext, byte[] tag)
    {
        WrapGeneration = generation;
        WrappedKeyNonce = nonce;
        WrappedKeyCiphertext = ciphertext;
        WrappedKeyTag = tag;
    }

    internal void Zero()
    {
        Zero(OuterNonce);
        Zero(OuterCiphertext);
        Zero(OuterTag);
        Zero(WrappedKeyNonce);
        Zero(WrappedKeyCiphertext);
        Zero(WrappedKeyTag);
    }

    internal CustodyComponentEnvelope Clone() => new(Identity, KekVersion, SealedGeneration, WrapGeneration,
        (byte[])OuterNonce.Clone(), (byte[])OuterCiphertext.Clone(), (byte[])OuterTag.Clone(),
        (byte[])WrappedKeyNonce.Clone(), (byte[])WrappedKeyCiphertext.Clone(), (byte[])WrappedKeyTag.Clone());

    private static void Zero(byte[] bytes) => System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
}
