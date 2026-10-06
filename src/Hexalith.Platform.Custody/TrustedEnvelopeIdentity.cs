namespace Hexalith.Platform.Custody;

/// <summary>Immutable logical identity expected independently from trusted composition.</summary>
/// <param name="SchemaVersion">Supported envelope version, currently 1.</param>
/// <param name="ProfileVersion">Accepted signing profile revision.</param>
/// <param name="Issuer">Exact issuer.</param>
/// <param name="Principal">Closed authenticated origin.</param>
/// <param name="CommandContract">Concrete contract, never just an operation family.</param>
/// <param name="OperationFamily">Logical operation.</param>
/// <param name="TargetTenantId">Authenticated target tenant.</param>
/// <param name="TargetResource">Exact aggregate/resource identity.</param>
/// <param name="CorrelationId">Correlation identity.</param>
/// <param name="CausationId">Causation identity.</param>
/// <param name="IdempotencyTuple">Immutable declared-order logical identity components.</param>
/// <param name="PayloadFingerprint">Already computed keyed command fingerprint.</param>
/// <param name="DigestKeyVersion">Original fingerprint key version.</param>
/// <param name="Audience">Exact audience.</param>
/// <param name="LogicalCommandId">Stable AD-29 logical identity.</param>
public sealed record TrustedEnvelopeIdentity(int SchemaVersion, string ProfileVersion, string Issuer,
    TrustedPrincipal Principal, string CommandContract, string OperationFamily, string TargetTenantId,
    string TargetResource, string CorrelationId, string CausationId, IReadOnlyList<string> IdempotencyTuple,
    string PayloadFingerprint, string DigestKeyVersion, string Audience, string LogicalCommandId);
