using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Custody-only root record, including immutable registration, context and current hold identities; never contains plaintext.</summary>
internal sealed record ErasureRootTarget(CustodyKeyRegistration Registration, ErasureRootCiphertext Wrapped, string[] Pins,
    string[] Holders, string[] CompletedHolders);
