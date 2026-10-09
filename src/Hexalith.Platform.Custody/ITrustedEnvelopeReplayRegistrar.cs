namespace Hexalith.Platform.Custody;

/// <summary>Story 5.4 private pre-command exception, injected only into the cryptographic replay verifier; no general command authority.</summary>
/// <remarks>The EventStore-backed adapter must append only Agents-owned TrustedEnvelopeFirstSeen at NoStream in reserved system,
/// keyed by (Issuer, DeliveryNonce) across every target tenant. Conflict/lost acknowledgement is resolved by exact lookup.
/// No unavailable or unknown result permits a target command. Only an ordinary trusted SystemTimer can purge after stored RetainUntil.</remarks>
public interface ITrustedEnvelopeReplayRegistrar
{
    /// <summary>Conditionally records first creation only, or returns the original exact authenticated receipt without comparing retry times.</summary>
    Task<TrustedEnvelopeReplayReceipt?> RegisterAsync(TrustedEnvelopeReplayIntent intent, DateTimeOffset evaluatedAt,
        TimeSpan retention, CancellationToken cancellationToken = default);
    /// <summary>Reads only the original exact issuer/nonce source; absent/unknown/changed target fields cannot create or renew it.</summary>
    Task<TrustedEnvelopeReplayReceipt?> LookupAsync(TrustedEnvelopeReplayIntent intent, CancellationToken cancellationToken = default);
}
