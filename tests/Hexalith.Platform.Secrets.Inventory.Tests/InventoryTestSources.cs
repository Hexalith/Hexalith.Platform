using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Inventory.Tests;

internal sealed class InventoryTestSources(JsonObject? metadata) : IInventorySources
{
    public List<string> MetadataReferences { get; } = [];
    public int EnrollmentReads { get; private set; }
    public bool DisableAfterFirstRead { get; init; }
    public bool ChangeMetadata { get; init; }
    public Action<int>? OnMetadataRead { get; init; }
    private int _metadataReads;

    public Task<NativeEnrollment?> EnrollmentAsync(string tenantId, CancellationToken cancellationToken)
    {
        EnrollmentReads++;
        return Task.FromResult<NativeEnrollment?>(new(tenantId, "native-stream-head-one", !(DisableAfterFirstRead && EnrollmentReads > 1)));
    }

    public Task<JsonObject?> MetadataAsync(string secretReference, CancellationToken cancellationToken)
    {
        MetadataReferences.Add(secretReference);
        _metadataReads++;
        OnMetadataRead?.Invoke(_metadataReads);
        JsonObject? result = metadata?.DeepClone() as JsonObject;
        if (ChangeMetadata && _metadataReads > 1 && result is not null) { result["current_version"] = 2; }
        return Task.FromResult(result);
    }
}
