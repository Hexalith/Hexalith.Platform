namespace Hexalith.Platform.Custody.Component;

/// <summary>Survivor X wrapped under the successor epoch. The witness keeps this outside the volume.</summary>
/// <param name="Generation">Successor epoch.</param>
/// <param name="Nonce">Wrap nonce.</param>
/// <param name="Ciphertext">Wrapped X.</param>
/// <param name="Tag">Wrap tag.</param>
internal sealed record CustodySuccessorWrap(string Generation, byte[] Nonce, byte[] Ciphertext, byte[] Tag)
{
    internal CustodySuccessorWrap Copy() => new(Generation, (byte[])Nonce.Clone(), (byte[])Ciphertext.Clone(), (byte[])Tag.Clone());
}
