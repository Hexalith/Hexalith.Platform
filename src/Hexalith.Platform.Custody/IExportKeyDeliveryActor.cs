using Dapr.Actors;

namespace Hexalith.Platform.Custody;

/// <summary>Private exact delivery ledger; host credentials and provider qualification are separate prerequisites.</summary>
public interface IExportKeyDeliveryActor : IActor
{
    /// <summary>Reserves the original complete identity before one provider release; retries resolve lookup only.</summary>
    Task<ExportKeyDeliveryOutcome> DeliverAsync(ExportKeyDeliveryIdentity identity);
    /// <summary>Reads/reconciles the exact original opaque outcome, with no additional release.</summary>
    Task<ExportKeyDeliveryOutcome> LookupAsync(ExportKeyDeliveryIdentity identity);
}
