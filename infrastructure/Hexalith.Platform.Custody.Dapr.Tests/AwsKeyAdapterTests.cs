using System.Security.Cryptography;
using System.Text.Json;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Hexalith.Platform.Custody.Dapr;

internal static class AwsKeyAdapterTests
{
    internal static async Task Run()
    {
        using var fixture = new Fixture(); var baseline = fixture.Profile;
        var native = baseline.Epochs.Select(e => (Arn: e.KeyArn, Id: e.KeyId)).Concat(baseline.Keks.Select(k => (Arn: k.ProtectorArn, Id: k.ProtectorKeyId)))
            .ToDictionary(k => k.Arn, k => (k.Id, Key: RandomNumberGenerator.GetBytes(32)), StringComparer.Ordinal);
        var tenantKek = RandomNumberGenerator.GetBytes(32); var pending = new HashSet<string>(StringComparer.Ordinal);
        bool substitute = false; int encrypts = 0, copies = 0, schedules = 0;
        Dictionary<string,string> Outer(string tenant, string epoch) => new() { ["authority"] = baseline.Enrollment.AuthorityId,
            ["incarnation"] = baseline.Enrollment.AuthorityIncarnation, ["tenant"] = tenant, ["epoch"] = epoch, ["purpose"] = "custody-outer-key-v1" };
        Dictionary<string,string> Kek(string tenant, string version) => new() { ["authority"] = baseline.Enrollment.AuthorityId,
            ["tenant"] = tenant, ["kekVersion"] = version, ["purpose"] = "custody-tenant-kek-v1" };
        var profile = baseline with { Keks = baseline.Keks.Select(k => k with { EncryptedKey = Encrypt(k.ProtectorArn, tenantKek, Kek(k.TenantId,k.Version)) }).ToArray() };
        using var kms = ApiDouble.Create<IAmazonKeyManagementService>((method,args) => method switch {
            "DescribeKeyAsync" => Describe((DescribeKeyRequest)args[0]!),
            "EncryptAsync" => EncryptRequest((EncryptRequest)args[0]!),
            "DecryptAsync" => DecryptRequest((DecryptRequest)args[0]!),
            "ReEncryptAsync" => Copy((ReEncryptRequest)args[0]!),
            "ScheduleKeyDeletionAsync" => Schedule((ScheduleKeyDeletionRequest)args[0]!),
            _ => throw new Exception("Unexpected native key method " + method),
        });
        try
        {
            var epochs = new AwsEpochOperations(profile, kms); var keys = new AwsKeyAccess(profile, kms);
            byte[] kek = await keys.ReadTenantKekAsync("tenant", "kek", default);
            var input = fixture.Wrap("aws-root").WrapInput!;
            var registration = await KeyEnvelopeBuilder.WrapAsync(input, kek, "e1", epochs, default);
            byte[] outer = await keys.ReadOuterKeyAsync(registration, default);
            byte[] root = KeyEnvelopeBuilder.Unwrap(registration, outer, kek);
            if (!root.AsSpan().SequenceEqual(Fixture.Root)) throw new Exception("Real AWS adapter roundtrip changed root.");
            CryptographicOperations.ZeroMemory(root); CryptographicOperations.ZeroMemory(outer);
            var copied = registration with { Epoch = "e2", EncryptedOuterKey = await epochs.ReencryptOuterKeyAsync("e1", "e2", "tenant", registration.EncryptedOuterKey, default) };
            outer = await keys.ReadOuterKeyAsync(copied, default); root = KeyEnvelopeBuilder.Unwrap(copied, outer, kek);
            if (!root.AsSpan().SequenceEqual(Fixture.Root) || encrypts != 1 || copies != 1) throw new Exception("Exact survivor context/copy changed root.");
            CryptographicOperations.ZeroMemory(root); CryptographicOperations.ZeroMemory(outer); CryptographicOperations.ZeroMemory(kek);
            substitute = true;
            await Refused(() => epochs.EncryptOuterKeyAsync("e2", "tenant", Fixture.Root, default));
            await Refused(() => keys.ReadTenantKekAsync("tenant", "kek", default));
            substitute = false;
            await epochs.ScheduleSourceCutAsync("e1", "tenant", default);
            await Refused(() => keys.ReadOuterKeyAsync(registration, default));
            if (schedules != 1 || !pending.Contains(profile.Epochs.Single(e => e.OpaqueId == "e1").KeyArn) || encrypts != 1)
                throw new Exception("Scheduling was replayed or substituted identity reached native encryption.");
        }
        finally { CryptographicOperations.ZeroMemory(tenantKek); foreach (var key in native.Values) CryptographicOperations.ZeroMemory(key.Key); }
        return;

        Dictionary<string,string> Context(string arn) {
            var epoch = profile.Epochs.SingleOrDefault(e => e.KeyArn == arn);
            if (epoch is not null) return Outer(epoch.TenantId, epoch.OpaqueId);
            var k = profile.Keks.Single(k => k.ProtectorArn == arn); return Kek(k.TenantId,k.Version);
        }
        void ExactContext(string arn, Dictionary<string,string> actual) {
            var expected = Context(arn);
            if (actual.Count != expected.Count || expected.Any(p => !actual.TryGetValue(p.Key,out var value) || value != p.Value))
                throw new Exception("Native encryption context changed or omitted exact identity.");
        }
        byte[] Aad(Dictionary<string,string> context) => JsonSerializer.SerializeToUtf8Bytes(new SortedDictionary<string,string>(context,StringComparer.Ordinal));
        byte[] Encrypt(string arn, byte[] bytes, Dictionary<string,string> context) {
            byte[] nonce = RandomNumberGenerator.GetBytes(12), ciphertext = new byte[bytes.Length], tag = new byte[16];
            using var aes = new AesGcm(native[arn].Key,16); aes.Encrypt(nonce,bytes,ciphertext,tag,Aad(context)); return nonce.Concat(ciphertext).Concat(tag).ToArray();
        }
        byte[] Decrypt(string arn, MemoryStream stream, Dictionary<string,string> context) {
            byte[] bytes = stream.ToArray(), result = new byte[bytes.Length-28]; using var aes = new AesGcm(native[arn].Key,16);
            aes.Decrypt(bytes.AsSpan(0,12),bytes.AsSpan(12,result.Length),bytes.AsSpan(12+result.Length,16),result,Aad(context)); return result;
        }
        Task<DescribeKeyResponse> Describe(DescribeKeyRequest request) => Task.FromResult(new DescribeKeyResponse { KeyMetadata = new() {
            Arn = request.KeyId, KeyId = substitute ? "substituted-key-id" : native[request.KeyId].Id, Origin = OriginType.AWS_KMS,
            MultiRegion = false, KeySpec = KeySpec.SYMMETRIC_DEFAULT, KeyUsage = KeyUsageType.ENCRYPT_DECRYPT,
            KeyState = pending.Contains(request.KeyId) ? KeyState.PendingDeletion : KeyState.Enabled } });
        Task<EncryptResponse> EncryptRequest(EncryptRequest request) {
            ExactContext(request.KeyId,request.EncryptionContext); encrypts++;
            if (request.EncryptionAlgorithm != EncryptionAlgorithmSpec.SYMMETRIC_DEFAULT) throw new Exception("Wrong native encryption algorithm.");
            return Task.FromResult(new EncryptResponse { KeyId = request.KeyId, CiphertextBlob = new MemoryStream(Encrypt(request.KeyId,request.Plaintext.ToArray(),request.EncryptionContext)) });
        }
        Task<DecryptResponse> DecryptRequest(DecryptRequest request) {
            ExactContext(request.KeyId,request.EncryptionContext);
            if (request.EncryptionAlgorithm != EncryptionAlgorithmSpec.SYMMETRIC_DEFAULT) throw new Exception("Wrong native decryption algorithm.");
            return Task.FromResult(new DecryptResponse { KeyId = request.KeyId, Plaintext = new MemoryStream(Decrypt(request.KeyId,request.CiphertextBlob,request.EncryptionContext)) });
        }
        Task<ReEncryptResponse> Copy(ReEncryptRequest request) {
            ExactContext(request.SourceKeyId,request.SourceEncryptionContext); ExactContext(request.DestinationKeyId,request.DestinationEncryptionContext); copies++;
            byte[] plain = Decrypt(request.SourceKeyId,request.CiphertextBlob,request.SourceEncryptionContext);
            try { return Task.FromResult(new ReEncryptResponse { SourceKeyId = request.SourceKeyId, KeyId = request.DestinationKeyId,
                CiphertextBlob = new MemoryStream(Encrypt(request.DestinationKeyId,plain,request.DestinationEncryptionContext)) }); }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        Task<ScheduleKeyDeletionResponse> Schedule(ScheduleKeyDeletionRequest request) {
            if (request.PendingWindowInDays != 7) throw new Exception("Native source cut changed deletion interval.");
            schedules++; pending.Add(request.KeyId); return Task.FromResult(new ScheduleKeyDeletionResponse { KeyId = request.KeyId, KeyState = KeyState.PendingDeletion });
        }
    }
    private static async Task Refused(Func<Task> action) { try { await action(); } catch (AuthorityUnavailableException) { return; } throw new Exception("Substituted native key was accepted."); }
}
