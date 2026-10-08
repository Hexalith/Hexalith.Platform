namespace Hexalith.Platform.Custody;

/// <summary>Second Story 5.4 pre-command capability: computes authenticated routing and a stable system-keyed identity, then accepts a content-free replicated spool observation.</summary>
/// <param name="authenticator">Cryptographic origin verification; raw client-claimed routing is never used.</param>
/// <param name="digests">Purpose-separated retained system SecurityObservation key.</param><param name="spool">Private replicated recorder capability.</param>
/// <param name="clock">Whole-operation budget.</param>
/// <remarks>No target command or general worker authority is exposed. A null result means SecurityAuditUnavailable and must block the consuming ingress/readiness gate.</remarks>
public sealed class PlatformSecurityDenialRecorder(TrustedEnvelopeAuthenticator authenticator, PlatformHmacService digests,
    ReplicatedSecurityObservationSpool spool, TimeProvider clock)
{
    /// <summary>Retains exactly one safe observation before a classified denial; original key version belongs to the retained server receipt and is never refreshed on retry.</summary>
    /// <param name="retainedServerReceiptId">Original trusted server receipt identity, not a client idempotency key.</param>
    /// <param name="reasonCode">Closed safe denial category.</param><param name="untrustedFields">Fields hashed only through the reserved-system retained purpose key.</param>
    /// <param name="originalDigestKeyVersion">Original version retained with the server receipt before its first observation.</param>
    /// <param name="envelope">Optional claimed origin; it affects routing only after current cryptographic authentication.</param>
    /// <param name="expected">Independently supplied exact origin scope.</param><param name="cancellationToken">Caller cancellation.</param>
    /// <returns>Durably confirmed original safe spool record, or no processed-denial evidence.</returns>
    public async Task<SecurityObservationRecord?> ObserveAsync(string retainedServerReceiptId, string reasonCode,
        IReadOnlyList<string?> untrustedFields, string originalDigestKeyVersion, TrustedEnvelope? envelope = null,
        TrustedEnvelopeIdentity? expected = null, CancellationToken cancellationToken = default)
    {
        var budget = new PrivateOwnerOperationDeadline(clock, cancellationToken);
        try
        {
            budget.Check();
            if (string.IsNullOrWhiteSpace(retainedServerReceiptId) || retainedServerReceiptId.Length > 2048
                || string.IsNullOrWhiteSpace(originalDigestKeyVersion) || originalDigestKeyVersion.Length > 2048
                || untrustedFields is null || untrustedFields.Count > 128
                || reasonCode is not ("invalid-tag" or "scope-mismatch" or "unknown-principal" or "stale-authority" or "replay-conflict" or "unavailable-owner")) { return null; }
            var owned = new List<string?>();
            foreach (var field in untrustedFields)
            { budget.Check(); if (owned.Count >= 128 || field?.Length > 4096) { return null; } owned.Add(field); }
            string routing = "system";
            if (envelope is not null && expected is not null)
            {
                var verified = await budget.ReadAsync(() => authenticator.VerifyAsync(envelope, expected, CancellationToken.None)).ConfigureAwait(false);
                if (verified.Status == CustodyStatus.Succeeded && verified.Envelope is { } captured)
                {
                    routing = captured.Identity.Principal.Kind switch
                    {
                        TrustedPrincipalKind.User or TrustedPrincipalKind.Administrator => captured.Identity.Principal.ActorTenantId!,
                        TrustedPrincipalKind.Workflow => captured.Identity.TargetTenantId,
                        _ => "system",
                    };
                }
            }
            var digest = await budget.ReadAsync(() => digests.DigestAsync(new("system", PlatformHmacPurpose.SecurityObservation),
                new string?[] { "SecurityObservation.v1", retainedServerReceiptId, reasonCode, routing }.Concat(owned).ToArray(),
                originalDigestKeyVersion, CancellationToken.None)).ConfigureAwait(false);
            if (digest.Status != CustodyStatus.Succeeded || digest.Digest is not { Length: 64 } || digest.KeyVersion != originalDigestKeyVersion) { return null; }
            var intent = new SecurityObservationIntent("security-observation-" + digest.Digest, routing, reasonCode, digest.Digest, digest.KeyVersion);
            var result = await budget.ReadAsync(() => spool.ObserveAsync(intent, CancellationToken.None)).ConfigureAwait(false);
            budget.Check(); return result?.Intent == intent ? result : null;
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return null; }
    }
}
