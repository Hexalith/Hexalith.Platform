using Dapr.Actors;

namespace Hexalith.Platform.Custody;

/// <summary>Private exact delivery ledger; host credentials and provider qualification are separate prerequisites.</summary>
public interface IExportKeyDeliveryActor : IActor
{
    /// <summary>Reserves the original complete identity before one provider release; retries resolve lookup only.</summary>
    Task<ExportKeyDeliveryOutcome> DeliverAsync(ExportKeyDeliveryIdentity identity);
    /// <summary>Reads/reconciles the exact original opaque outcome, with no additional release.</summary>
    Task<ExportKeyDeliveryOutcome> LookupAsync(ExportKeyDeliveryIdentity identity);
    /// <summary>Delivers received fields only to the unchanged authorized original target; actual input/scope/identity rejections are retained independently.</summary>
    Task<ExportKeyDeliveryReceivingResult> DeliverReceivedAsync(ExportKeyDeliveryReceivingRequest request);
    /// <summary>Reads an exact v2 structural/scope rejection original without validating current actor state or repeating delivery.</summary>
    Task<ExportKeyDeliveryReceivingDenialOriginal?> LookupReceivingRejectionAsync(ExportKeyDeliveryReceivingRequest request);
    /// <summary>Reads only the versioned independently retained receiving denial, without comparison against mutable actor state or another Deliver.</summary>
    Task<ExportKeyDeliveryDenialOriginal?> LookupReceivingDenialAsync(ExportKeyDeliveryIdentity identity);
}
