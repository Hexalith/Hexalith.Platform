namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>One enrolled exact custody operation. It names the tenant, operation, method, request digest, revision, and state digest.</summary>
internal interface ICustodyInstalledGuard
{
    /// <summary>Gets the enrolled tenant.</summary>
    string TenantId { get; }

    /// <summary>Gets the enrolled operation.</summary>
    string OperationId { get; }

    /// <summary>Gets the enrolled actor method.</summary>
    string Method { get; }

    /// <summary>Gets the enrolled request digest.</summary>
    string RequestDigest { get; }

    /// <summary>Gets the enrolled revision.</summary>
    long Revision { get; }

    /// <summary>Gets the enrolled state digest.</summary>
    string StateDigest { get; }

    /// <summary>True only when every enrolled field matches. Any other tenant, digest, or revision is refused.</summary>
    bool Accepts(string tenantId, string operationId, string method, string requestDigest, long revision, string stateDigest);
}
