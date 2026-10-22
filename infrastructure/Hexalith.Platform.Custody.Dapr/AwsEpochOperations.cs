using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
namespace Hexalith.Platform.Custody.Dapr;

internal interface IEpochOperations
{
    Task<byte[]> EncryptOuterKeyAsync(string epoch, string tenant, byte[] key, CancellationToken token);
    Task<byte[]> ReencryptOuterKeyAsync(string source, string destination, string tenant, byte[] encryptedKey, CancellationToken token);
    Task ScheduleSourceCutAsync(string epoch, string tenant, CancellationToken token);
}

// Invoked only by an admitted durable worker, never lookup or ordinary host startup.
// This type deliberately has no CreateKey, CancelKeyDeletion, ImportKeyMaterial, replica, or export capability.
internal sealed class AwsEpochOperations(ComponentProfile profile, IAmazonKeyManagementService kms) : IEpochOperations
{
    private async Task<NativeEpoch> VerifyEpochAsync(string opaque, string tenant, CancellationToken token)
    {
        var epoch = profile.Epochs.SingleOrDefault(e => e.OpaqueId == opaque && e.TenantId == tenant)
            ?? throw new AuthorityUnavailableException("Epoch is not independently enrolled.");
        var response = await kms.DescribeKeyAsync(new DescribeKeyRequest { KeyId = epoch.KeyArn }, token);
        var key = response.KeyMetadata;
        if (key?.Arn != epoch.KeyArn || key.KeyId != epoch.KeyId || key.MultiRegion != false || key.Origin != OriginType.AWS_KMS
            || key.KeySpec != KeySpec.SYMMETRIC_DEFAULT || key.KeyUsage != KeyUsageType.ENCRYPT_DECRYPT || key.KeyState != KeyState.Enabled
            || !string.IsNullOrEmpty(key.CustomKeyStoreId))
            throw new AuthorityUnavailableException("Epoch identity/origin/state is unsuitable.");
        return epoch;
    }
    public async Task<byte[]> EncryptOuterKeyAsync(string epoch, string tenant, byte[] key, CancellationToken token)
    {
        if (key.Length != 32) throw new AuthorityUnavailableException("Exact outer key length required.");
        var enrolled = await VerifyEpochAsync(epoch, tenant, token);
        using var plain = new MemoryStream(key, writable: false);
        var response = await kms.EncryptAsync(new EncryptRequest { KeyId = enrolled.KeyArn, Plaintext = plain,
            EncryptionContext = Context(tenant, epoch), EncryptionAlgorithm = EncryptionAlgorithmSpec.SYMMETRIC_DEFAULT }, token);
        if (response.KeyId != enrolled.KeyArn) throw new AuthorityUnavailableException("Unexpected native encryption key.");
        return response.CiphertextBlob.ToArray();
    }
    public async Task<byte[]> ReencryptOuterKeyAsync(string source, string destination, string tenant, byte[] encryptedKey, CancellationToken token)
    {
        var from = await VerifyEpochAsync(source, tenant, token); var to = await VerifyEpochAsync(destination, tenant, token);
        using var cipher = new MemoryStream(encryptedKey, writable: false);
        var response = await kms.ReEncryptAsync(new ReEncryptRequest { SourceKeyId = from.KeyArn, DestinationKeyId = to.KeyArn,
            CiphertextBlob = cipher, SourceEncryptionContext = Context(tenant, source), DestinationEncryptionContext = Context(tenant, destination) }, token);
        if (response.SourceKeyId != from.KeyArn || response.KeyId != to.KeyArn) throw new AuthorityUnavailableException("Unexpected native survivor lineage.");
        return response.CiphertextBlob.ToArray();
    }
    public async Task ScheduleSourceCutAsync(string epoch, string tenant, CancellationToken token)
    {
        var enrolled = await VerifyEpochAsync(epoch, tenant, token);
        var response = await kms.ScheduleKeyDeletionAsync(new ScheduleKeyDeletionRequest { KeyId = enrolled.KeyArn, PendingWindowInDays = 7 }, token);
        if (response.KeyId != enrolled.KeyArn || response.KeyState != KeyState.PendingDeletion)
            throw new AuthorityUnavailableException("Native scheduling result uncertain.");
        // Successful scheduling is not final erasure evidence and returns no terminal receipt.
    }
    private Dictionary<string, string> Context(string tenant, string epoch) => new() {
        ["authority"] = profile.Enrollment.AuthorityId, ["incarnation"] = profile.Enrollment.AuthorityIncarnation,
        ["tenant"] = tenant, ["epoch"] = epoch, ["purpose"] = "custody-outer-key-v1" };
}
