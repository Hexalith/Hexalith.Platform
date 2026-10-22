using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
namespace Hexalith.Platform.Custody.Dapr;
internal interface IKeyAccess
{
    Task<byte[]> ReadTenantKekAsync(string tenant, string version, CancellationToken token);
    Task<byte[]> ReadOuterKeyAsync(StoredRegistration registration, CancellationToken token);
}
internal sealed class AwsKeyAccess(ComponentProfile profile, IAmazonKeyManagementService kms) : IKeyAccess
{
    public async Task<byte[]> ReadTenantKekAsync(string tenant, string version, CancellationToken token)
    {
        var kek = profile.Keks.SingleOrDefault(k => k.TenantId == tenant && k.Version == version)
            ?? throw new AuthorityUnavailableException("Original KEK not enrolled.");
        return await DecryptAsync(kek.ProtectorArn, kek.ProtectorKeyId, kek.EncryptedKey, new() {
            ["authority"] = profile.Enrollment.AuthorityId, ["tenant"] = tenant, ["kekVersion"] = version,
            ["purpose"] = "custody-tenant-kek-v1" }, token);
    }
    public async Task<byte[]> ReadOuterKeyAsync(StoredRegistration registration, CancellationToken token)
    {
        var epoch = profile.Epochs.SingleOrDefault(e => e.OpaqueId == registration.Epoch && e.TenantId == registration.Registration.Identity.TenantId)
            ?? throw new AuthorityUnavailableException("Original epoch not enrolled.");
        return await DecryptAsync(epoch.KeyArn, epoch.KeyId, registration.EncryptedOuterKey, new() {
            ["authority"] = profile.Enrollment.AuthorityId, ["incarnation"] = profile.Enrollment.AuthorityIncarnation,
            ["tenant"] = epoch.TenantId, ["epoch"] = epoch.OpaqueId, ["purpose"] = "custody-outer-key-v1" }, token);
    }
    private async Task<byte[]> DecryptAsync(string arn, string id, byte[] encrypted, Dictionary<string, string> context, CancellationToken token)
    {
        var response = await kms.DescribeKeyAsync(new DescribeKeyRequest { KeyId = arn }, token); var key = response.KeyMetadata;
        if (key?.Arn != arn || key.KeyId != id || key.MultiRegion != false || key.Origin != OriginType.AWS_KMS
            || key.KeySpec != KeySpec.SYMMETRIC_DEFAULT || key.KeyState != KeyState.Enabled || !string.IsNullOrEmpty(key.CustomKeyStoreId))
            throw new AuthorityUnavailableException("Key identity/origin mismatch.");
        using var ciphertext = new MemoryStream(encrypted, writable: false);
        var decrypted = await kms.DecryptAsync(new DecryptRequest { KeyId = arn, CiphertextBlob = ciphertext,
            EncryptionContext = context, EncryptionAlgorithm = EncryptionAlgorithmSpec.SYMMETRIC_DEFAULT }, token);
        using var plaintext = decrypted.Plaintext;
        try
        {
            if (decrypted.KeyId != arn || plaintext.Length != 32) throw new AuthorityUnavailableException("Invalid native key response.");
            return plaintext.ToArray();
        }
        finally { if (plaintext.TryGetBuffer(out var buffer)) System.Security.Cryptography.CryptographicOperations.ZeroMemory(buffer.AsSpan()); }
    }
}
