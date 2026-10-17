using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Inventory;

/// <summary>Uncached, read-only current native enrollment and exact provider metadata.</summary>
public interface IInventorySources
{
    /// <summary>Reads and folds the complete native persisted tenant stream.</summary>
    Task<NativeEnrollment?> EnrollmentAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>Reads only KV2 metadata, never a data endpoint or key value.</summary>
    Task<JsonObject?> MetadataAsync(string secretReference, CancellationToken cancellationToken);
}
