namespace Hexalith.Platform.Protection;

/// <summary>Native dedicated OpenBao ERC operations. Implementations must not retry a mutation after an unknown acknowledgement.</summary>
public interface IErasureRootStore
{
    /// <summary>Reads the current tenant head with a strong native read; absence is not genesis.</summary>
    Task<ErasureRootHeadSnapshot?> ReadHeadAsync(string tenantId, CancellationToken cancellationToken);
    /// <summary>Performs one native KV v2 CAS. False means an actual CAS conflict; transport ambiguity throws.</summary>
    Task<bool> CompareExchangeHeadAsync(string tenantId, long version, byte[] head, CancellationToken cancellationToken);
    /// <summary>Reads an exact immutable node from the same dedicated snapshot-excluded ERC; absence is never completion.</summary>
    Task<byte[]?> ReadArchiveAsync(string tenantId, string archiveId, CancellationToken cancellationToken);
    /// <summary>Creates exact archive bytes with native CAS zero, or verifies an existing byte-identical original. It cannot overwrite.</summary>
    Task<bool> CreateArchiveAsync(string tenantId, string archiveId, byte[] bytes, CancellationToken cancellationToken);
    /// <summary>Encrypts a newly generated root at the explicitly selected native epoch.</summary>
    Task<string> EncryptAsync(string tenantId, int epoch, byte[] root, string context, CancellationToken cancellationToken);
    /// <summary>Decrypts only under the complete original context; ownership transfers to the caller.</summary>
    Task<byte[]> DecryptAsync(string tenantId, ErasureRootCiphertext value, CancellationToken cancellationToken);
    /// <summary>Returns the fresh native latest key version.</summary>
    Task<int> ReadEpochAsync(string tenantId, CancellationToken cancellationToken);
    /// <summary>Rotates once. A lost acknowledgement must be resolved with ReadEpochAsync.</summary>
    Task RotateAsync(string tenantId, CancellationToken cancellationToken);
    /// <summary>Rewraps survivors at an explicit successor epoch without exporting their plaintext.</summary>
    Task<IReadOnlyList<string>> RewrapAsync(string tenantId, int epoch, IReadOnlyList<ErasureRootCiphertext> values, CancellationToken cancellationToken);
    /// <summary>Advances native min encryption/decryption versions, then performs one irreversible trim. Exact retry is safe.</summary>
    Task TrimAsync(string tenantId, int epoch, CancellationToken cancellationToken);
    /// <summary>Independently rereads native minimum available version after trim.</summary>
    Task<bool> VerifyTrimAsync(string tenantId, int epoch, CancellationToken cancellationToken);
}
