namespace Hexalith.Platform.Custody;

/// <summary>Original durably authenticated NoStream first-seen result; retries retain stored times and never renew retention.</summary>
/// <param name="Intent">Original exact authenticated immutable fields.</param><param name="FirstSeenAt">Original verifier evaluation time.</param>
/// <param name="RetainUntil">Original persisted exclusive purge limit.</param><param name="SourceRevision">Original exact persisted source position.</param>
/// <param name="ReceiptId">Opaque durable original receipt.</param>
public sealed record TrustedEnvelopeReplayReceipt(TrustedEnvelopeReplayIntent Intent, DateTimeOffset FirstSeenAt,
    DateTimeOffset RetainUntil, long SourceRevision, string ReceiptId);
