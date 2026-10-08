using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>
/// Approved party actor-history policy gate. Construction fails closed for every other policy, purpose, or trigger.
/// </summary>
public sealed class IdentityHistoryCustodyOptions
{
    /// <summary>Gets the approved policy identifier.</summary>
    public const string PolicyId = "party-actor-retention-v1";

    /// <summary>Gets the approved history purpose.</summary>
    public const string Purpose = "party-actor-history-v1";

    /// <summary>Gets the approved expiry trigger.</summary>
    public const string ExpiryTrigger = "binding-effective-at";

    /// <summary>Gets the approved retention of 365 fixed 24-hour days (31,536,000 seconds).</summary>
    public static TimeSpan Retention { get; } = TimeSpan.FromDays(365);

    /// <summary>Gets the only policy that can admit actor-history custody.</summary>
    public static IdentityHistoryPolicy ApprovedPolicy { get; } = new(PolicyId, Retention, ExpiryTrigger);

    /// <summary>Creates the approved policy gate.</summary>
    /// <param name="policy">Candidate retention policy.</param>
    /// <param name="purpose">Candidate protection purpose.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="policy"/> or <paramref name="purpose"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the policy or purpose is not the approved actor-history contract.</exception>
    public IdentityHistoryCustodyOptions(IdentityHistoryPolicy policy, string purpose)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        if (!policy.IsValid
            || policy.PolicyId != PolicyId
            || policy.Retention != Retention
            || policy.ExpiryTrigger != ExpiryTrigger
            || purpose != Purpose)
        {
            throw new ArgumentException("Actor-history custody requires the approved party retention policy.", nameof(policy));
        }

        Policy = policy;
    }

    /// <summary>Gets the approved policy.</summary>
    public IdentityHistoryPolicy Policy { get; }

    /// <summary>Derives the UTC deadline as the binding-effective instant plus 365 fixed days.</summary>
    /// <param name="bindingEffectiveAt">Original binding-effective instant. Its offset is normalized away.</param>
    /// <returns>The exclusive UTC expiry.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the deadline does not fit in <see cref="DateTimeOffset"/>.</exception>
    public static DateTimeOffset DeriveDeadline(DateTimeOffset bindingEffectiveAt)
    {
        DateTimeOffset effectiveAt = bindingEffectiveAt.ToUniversalTime();
        if (!ApprovedPolicy.IsValid)
        {
            throw new ArgumentException("The approved actor-history policy is not valid.");
        }

        DateTimeOffset? derived = ApprovedPolicy.DeriveExpiry(effectiveAt);
        if (derived is null || derived.Value.ToUniversalTime() - effectiveAt != Retention)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bindingEffectiveAt),
                "The binding-effective instant plus 365 fixed days is outside DateTimeOffset.");
        }

        return derived.Value.ToUniversalTime();
    }

    /// <summary>Gets whether the unit is still readable. The deadline itself is not readable.</summary>
    /// <param name="now">Observation instant.</param>
    /// <param name="expiresAt">Exclusive UTC deadline.</param>
    /// <returns><see langword="true"/> only while <paramref name="now"/> is strictly before <paramref name="expiresAt"/>.</returns>
    public static bool IsReadable(DateTimeOffset now, DateTimeOffset expiresAt)
        => now.ToUniversalTime() < expiresAt.ToUniversalTime();

    /// <summary>
    /// Admits one original unit. A retry that supplies the same facts returns the same evidence, revision, and deadline.
    /// </summary>
    /// <param name="identity">Party aggregate scope.</param>
    /// <param name="environment">Environment scope.</param>
    /// <param name="instance">Instance scope.</param>
    /// <param name="bindingEffectiveAt">Original binding-effective instant.</param>
    /// <param name="now">Admission observation instant.</param>
    /// <param name="evidenceId">Caller-supplied original evidence identifier.</param>
    /// <param name="admissionRevision">Caller-supplied original admission revision.</param>
    /// <returns>The immutable unit.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a required object is null.</exception>
    /// <exception cref="ArgumentException">Thrown when admission is at or after the deadline or the scope is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the deadline does not fit in <see cref="DateTimeOffset"/>.</exception>
    public IdentityHistoryCustodyUnit Admit(
        AggregateIdentity identity,
        string environment,
        string instance,
        DateTimeOffset bindingEffectiveAt,
        DateTimeOffset now,
        string evidenceId,
        long admissionRevision)
    {
        DateTimeOffset expiresAt = DeriveDeadline(bindingEffectiveAt);
        if (!IsReadable(now, expiresAt))
        {
            throw new ArgumentException("Actor-history admission is not before the exclusive deadline.", nameof(now));
        }

        return new IdentityHistoryCustodyUnit(
            identity,
            environment,
            instance,
            Policy.PolicyId,
            Purpose,
            bindingEffectiveAt,
            expiresAt,
            evidenceId,
            admissionRevision);
    }
}
