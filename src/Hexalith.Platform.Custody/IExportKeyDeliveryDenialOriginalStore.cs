namespace Hexalith.Platform.Custody;

/// <summary>Explicit independently authorized immutable originals outside the application restore cut. No fallback enrollment or mutable actor state is permitted.</summary>
public interface IExportKeyDeliveryDenialOriginalStore
{
    /// <summary>Retains an executed bounded original-target structural/scope rejection in its separate versioned namespace.</summary>
    Task<ExportKeyDeliveryReceivingDenialOriginal?> RetainReceivedAsync(ExportKeyDeliveryReceivingDenialOriginal original,CancellationToken cancellationToken=default);
    /// <summary>Reads a previously retained raw receiving rejection, including candidates whose contract/address validation fails.</summary>
    Task<ExportKeyDeliveryReceivingDenialOriginal?> LookupReceivedAsync(ExportKeyDeliveryReceivingRequest request,CancellationToken cancellationToken=default);
    /// <summary>Atomically retains the actual receiving denial or returns its unchanged first original.</summary>
    Task<ExportKeyDeliveryDenialOriginal?> RetainAsync(ExportKeyDeliveryDenialOriginal original,CancellationToken cancellationToken=default);
    /// <summary>Reads only a previously retained exact receiving denial; current identity comparison is never execution evidence.</summary>
    Task<ExportKeyDeliveryDenialOriginal?> LookupAsync(ExportKeyDeliveryIdentity requested,CancellationToken cancellationToken=default);
}
