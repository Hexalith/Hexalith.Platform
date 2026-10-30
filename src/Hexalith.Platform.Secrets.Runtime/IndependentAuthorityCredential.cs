namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Current private authority transport credential validation, reusing the bounded nonblocking mounted-bearer reader.</summary>
public static class IndependentAuthorityCredential
{
    /// <summary>Returns only the actual mounted bearer's SHA-256 identity for disjoint-enrollment admission; missing or malformed inputs refuse.</summary>
    public static Task<byte[]?> ReadFingerprintAsync(string reference, CancellationToken cancellationToken = default)
        => DaprSecretsInvoker.ReadMountedBearerIdentityAsync(reference, cancellationToken);
}
