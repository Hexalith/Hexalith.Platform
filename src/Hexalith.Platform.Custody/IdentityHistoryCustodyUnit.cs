using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.Platform.Custody;

/// <summary>
/// Immutable party actor-history unit. The constructor preserves an already expired record so it can round-trip;
/// admission rejects expiry separately.
/// </summary>
public sealed record IdentityHistoryCustodyUnit
{
    /// <summary>Creates a unit whose deadline is fixed to the original binding-effective instant.</summary>
    /// <param name="identity">Tenant, domain, and aggregate scope.</param>
    /// <param name="environment">Environment scope.</param>
    /// <param name="instance">Instance scope.</param>
    /// <param name="policyId">Approved policy identifier.</param>
    /// <param name="purpose">Approved purpose.</param>
    /// <param name="bindingEffectiveAt">Original binding-effective instant.</param>
    /// <param name="expiresAt">Deadline that must equal the derived UTC expiry.</param>
    /// <param name="evidenceId">Original evidence identifier.</param>
    /// <param name="admissionRevision">Original admission revision.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required object is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the scope, policy, evidence, or deadline is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the derived deadline does not fit in <see cref="DateTimeOffset"/>.</exception>
    public IdentityHistoryCustodyUnit(
        AggregateIdentity identity,
        string environment,
        string instance,
        string policyId,
        string purpose,
        DateTimeOffset bindingEffectiveAt,
        DateTimeOffset expiresAt,
        string evidenceId,
        long admissionRevision)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceId);
        if (identity.Domain != "party")
        {
            throw new ArgumentException("Actor-history custody domain must be party.", nameof(identity));
        }

        if (policyId != IdentityHistoryCustodyOptions.PolicyId || purpose != IdentityHistoryCustodyOptions.Purpose)
        {
            throw new ArgumentException("Actor-history custody requires the approved party retention policy.", nameof(policyId));
        }

        if (admissionRevision <= 0)
        {
            throw new ArgumentException("Admission revision must be a positive original revision.", nameof(admissionRevision));
        }

        DateTimeOffset effectiveAt = bindingEffectiveAt.ToUniversalTime();
        DateTimeOffset deadline = expiresAt.ToUniversalTime();
        if (deadline != IdentityHistoryCustodyOptions.DeriveDeadline(effectiveAt))
        {
            throw new ArgumentException(
                "The custody deadline must be the original binding-effective instant plus 365 fixed days.",
                nameof(expiresAt));
        }

        Identity = identity;
        Environment = environment;
        Instance = instance;
        PolicyId = policyId;
        Purpose = purpose;
        BindingEffectiveAt = effectiveAt;
        ExpiresAt = deadline;
        EvidenceId = evidenceId;
        AdmissionRevision = admissionRevision;
    }

    /// <summary>Gets the tenant, domain, and aggregate scope.</summary>
    public AggregateIdentity Identity { get; }

    /// <summary>Gets the environment scope.</summary>
    public string Environment { get; }

    /// <summary>Gets the instance scope.</summary>
    public string Instance { get; }

    /// <summary>Gets the approved policy identifier.</summary>
    public string PolicyId { get; }

    /// <summary>Gets the approved purpose.</summary>
    public string Purpose { get; }

    /// <summary>Gets the original binding-effective instant in UTC.</summary>
    public DateTimeOffset BindingEffectiveAt { get; }

    /// <summary>Gets the exclusive UTC deadline.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>Gets the original evidence identifier.</summary>
    public string EvidenceId { get; }

    /// <summary>Gets the original admission revision.</summary>
    public long AdmissionRevision { get; }
}
