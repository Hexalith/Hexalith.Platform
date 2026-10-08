namespace Hexalith.Platform.Custody;

/// <summary>Qualified principal-bound transport owner. No key bytes or secret references enter its result.</summary>
/// <remarks>Release must atomically validate current requester authority and exclusive expiry at its irreversible delivery instant,
/// deliver only to that authenticated principal, retain the immutable complete identity/outcome across restore, and provide exact lookup.
/// This required guarantee is not supplied by a prior authorization read or by this interface.</remarks>
public interface IExportKeyDirectDeliveryProvider
{
    /// <summary>Performs the first exact idempotent release; unavailable/lost results require lookup and never blind repeat.</summary>
    Task<ExportKeyDeliveryOutcome> ReleaseAsync(ExportKeyDeliveryIdentity identity, CancellationToken cancellationToken = default);
    /// <summary>Authenticates original Delivered/NotDelivered/Unknown outcome without performing any new release.</summary>
    Task<ExportKeyDeliveryOutcome> LookupAsync(ExportKeyDeliveryIdentity identity, CancellationToken cancellationToken = default);
}
