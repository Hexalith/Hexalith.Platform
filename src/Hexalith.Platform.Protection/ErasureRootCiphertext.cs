namespace Hexalith.Platform.Protection;

/// <summary>Opaque native ciphertext and its complete authenticated derivation context.</summary>
/// <param name="Ciphertext">Native transit ciphertext.</param>
/// <param name="Context">Base64 of the original custody wrapping AAD.</param>
public sealed record ErasureRootCiphertext(string Ciphertext, string Context);
