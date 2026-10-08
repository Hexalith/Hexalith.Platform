namespace Hexalith.Platform.Custody;

/// <summary>Independent current exact private credentials, lifecycle/fence/hold/reservation/physical-receipt and restore-anchor verification; absent defaults deny.</summary>
public interface ICustodyKeyLifecycleAuthority
{
    /// <summary>Authenticates exact tenant/object/operation/method/request digest before release/effect.</summary>
    Task<bool> AuthorizeOperationAsync(CustodyKeyObjectIdentity identity, string operationId, string method, string requestDigest, CancellationToken cancellationToken = default);
    /// <summary>Verifies original AES-256-GCM wrapped/store object, purpose/KEK and complete durable opaque-reference receipt.</summary>
    Task<bool> VerifyRegistrationAsync(CustodyKeyRegistration registration, CancellationToken cancellationToken = default);
    /// <summary>Verifies current complete lifecycle decision/fence/control and exact root destruction reservation. A read alone is not physical authority.</summary>
    Task<bool> AuthorizeEffectAsync(CustodyKeyLifecycleRequest request, CancellationToken cancellationToken = default);
    /// <summary>Independently verifies exact original durable physical effect/never-performed and irreversible all-copy/nonrollback receipt; provider self-report is insufficient.</summary>
    Task<bool> VerifyOutcomeAsync(CustodyKeyLifecycleRequest request, CustodyKeyLifecycleOutcome outcome, CancellationToken cancellationToken = default);
    /// <summary>Validates exact captured durable tenant/revision/digest against independently installed antirollback state.</summary>
    Task<bool> ValidateStateAsync(string tenantId, long revision, string exactStateDigest, CancellationToken cancellationToken = default);
    /// <summary>Advances independent conditional anchor before metadata save; precommit/unknown loss closes availability pending reconciliation.</summary>
    Task<bool> RecordRevisionAsync(string tenantId, long expectedRevision, long nextRevision, string exactStateDigest, CancellationToken cancellationToken = default);
}
