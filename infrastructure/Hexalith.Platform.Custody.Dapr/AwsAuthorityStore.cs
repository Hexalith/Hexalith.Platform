using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Custody.Dapr;

// Native dependencies stop here. No create, restore, delete, TTL, or enrollment API exists on this store.
internal sealed class AwsAuthorityStore(ComponentProfile profile, IAmazonDynamoDB dynamo, IAmazonS3 s3, TimeProvider clock) : IAuthorityStore
{
    internal async Task VerifyIdentityAsync(CancellationToken token)
    {
        var description = await dynamo.DescribeTableAsync(new DescribeTableRequest { TableName = profile.TableArn }, token);
        var table = description.Table;
        if (table?.TableId != profile.TableId || table.TableArn != profile.TableArn || table.TableStatus != TableStatus.ACTIVE
            || table.Replicas is { Count: > 0 } || table.RestoreSummary?.RestoreInProgress == true)
            throw new AuthorityUnavailableException("Authority table incarnation changed or unavailable.");
        foreach (var bucket in new[] { profile.Checkpoints, profile.Originals })
        {
            var versioning = await s3.GetBucketVersioningAsync(new GetBucketVersioningRequest { BucketName = bucket.Name, ExpectedBucketOwner = bucket.OwnerAccountId }, token);
            if (versioning.VersioningConfig?.Status != VersionStatus.Enabled) throw new AuthorityUnavailableException("Archive versioning is not enabled.");
            var enrolled = new ObjectVersion(bucket.EnrollmentKey, bucket.EnrollmentVersion, bucket.EnrollmentSha256, -1, profile.RetainUntil);
            _ = await ReadObjectAsync(bucket, enrolled, token);
        }
    }
    public async Task<CurrentState> ReadAsync(string tenant, CancellationToken token)
    {
        if (!profile.Tenants.Contains(tenant, StringComparer.Ordinal)) throw new AuthorityUnavailableException("Tenant not enrolled.");
        await VerifyIdentityAsync(token);
        var result = await dynamo.GetItemAsync(new GetItemRequest { TableName = profile.TableArn, Key = Key(tenant), ConsistentRead = true }, token);
        if (result.Item is null || !result.Item.TryGetValue("head", out var value) || string.IsNullOrWhiteSpace(value.S))
            throw new AuthorityUnavailableException("Surviving authority missing; genesis is never bootstrapped.");
        var head = CustodyJson.Read<AuthorityHead>(System.Text.Encoding.UTF8.GetBytes(value.S));
        byte[] archiveBytes = await ReadImmutableAsync(head.Checkpoint, token);
        var archive = CustodyJson.Read<CheckpointArchiveV1>(archiveBytes);
        var checkpoint = await CheckpointArchive.DecodeAsync(archiveBytes, ReadImmutableAsync, token);
        if (head.EnrollmentDigest != profile.EnrollmentDigest || checkpoint.EnrollmentDigest != profile.EnrollmentDigest
            || head.Revision != checkpoint.Revision || head.Digest != head.Checkpoint.Sha256 || checkpoint.TenantId != tenant)
            throw new AuthorityUnavailableException("Current authority binding mismatch.");
        return new(head, checkpoint, value.S, archive);
    }
    public async Task<ObjectVersion> PutImmutableAsync(ReadOnlyMemory<byte> bytes, string category, CancellationToken token)
        => await PutAtAsync(bytes, category, category + "/" + Guid.NewGuid().ToString("N") + "/" + CustodyJson.Hash(bytes.Span), token);
    public Task<ObjectVersion> PutIntentResultAsync(ReadOnlyMemory<byte> bytes, string locator, CancellationToken token)
    {
        if (!locator.StartsWith("journal/native-result/", StringComparison.Ordinal)) throw new AuthorityUnavailableException("Invalid native-result locator.");
        return PutAtAsync(bytes, "journal", locator, token);
    }
    public async Task<ObjectVersion?> FindIntentResultAsync(string locator, CancellationToken token)
    {
        if (!locator.StartsWith("journal/native-result/", StringComparison.Ordinal)) throw new AuthorityUnavailableException("Invalid native-result locator.");
        try
        {
            using var result = await s3.GetObjectAsync(new GetObjectRequest { BucketName = profile.Originals.Name,
                ExpectedBucketOwner = profile.Originals.OwnerAccountId, Key = locator }, token);
            if (result.ContentLength is <= 0 or > CustodyBindingContract.MaximumOriginalBytes) throw new AuthorityUnavailableException("Result bounds invalid.");
            using var output = new MemoryStream(); await result.ResponseStream.CopyToAsync(output, token);
            if (output.Length != result.ContentLength) throw new AuthorityUnavailableException("Native result length mismatch.");
            var version = new ObjectVersion(locator, result.VersionId, CustodyJson.Hash(output.ToArray()), (int)output.Length, profile.RetainUntil);
            _ = await ReadObjectAsync(profile.Originals, version, token);
            return version;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound) { return null; }
    }
    private async Task<ObjectVersion> PutAtAsync(ReadOnlyMemory<byte> bytes, string category, string key, CancellationToken token)
    {
        if (category is not ("checkpoint" or "original" or "journal") || bytes.Length is 0 or > CustodyBindingContract.MaximumOriginalBytes
            || profile.RetainUntil <= clock.GetUtcNow()) throw new AuthorityUnavailableException("Immutable retention or bounds unavailable.");
        BucketIdentity bucket = category == "checkpoint" ? profile.Checkpoints : profile.Originals;
        string hash = CustodyJson.Hash(bytes.Span);
        using var input = new MemoryStream(bytes.ToArray(), writable: false);
        // No multipart writes and no retries: an unacknowledged version is an orphan journal, never current.
        var result = await s3.PutObjectAsync(new PutObjectRequest {
            BucketName = bucket.Name, ExpectedBucketOwner = bucket.OwnerAccountId, Key = key, InputStream = input,
            IfNoneMatch = "*", AutoCloseStream = false, ContentType = "application/json", ChecksumSHA256 = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(bytes.Span)),
            ObjectLockMode = ObjectLockMode.Compliance, ObjectLockRetainUntilDate = profile.RetainUntil.UtcDateTime,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
        }, token);
        if (string.IsNullOrWhiteSpace(result.VersionId) || result.VersionId == "null") throw new AuthorityUnavailableException("Immutable version missing.");
        var version = new ObjectVersion(key, result.VersionId, hash, bytes.Length, profile.RetainUntil);
        _ = await ReadObjectAsync(bucket, version, token);
        return version;
    }
    public Task<byte[]> ReadImmutableAsync(ObjectVersion version, CancellationToken token)
        => ReadObjectAsync(version.Key.StartsWith("checkpoint/", StringComparison.Ordinal) ? profile.Checkpoints : profile.Originals, version, token);
    private async Task<byte[]> ReadObjectAsync(BucketIdentity bucket, ObjectVersion version, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(version.VersionId) || version.VersionId == "null" || !CustodyBindingContract.Digest(version.Sha256))
            throw new AuthorityUnavailableException("Unpinned archive version.");
        using var result = await s3.GetObjectAsync(new GetObjectRequest { BucketName = bucket.Name, ExpectedBucketOwner = bucket.OwnerAccountId,
            Key = version.Key, VersionId = version.VersionId }, token);
        if (result.VersionId != version.VersionId || result.ContentLength is <= 0 or > CustodyBindingContract.MaximumOriginalBytes
            || version.Bytes >= 0 && result.ContentLength != version.Bytes || result.ObjectLockMode != ObjectLockMode.Compliance
            || result.ObjectLockRetainUntilDate < version.RetainUntil.UtcDateTime || result.ObjectLockRetainUntilDate <= clock.GetUtcNow().UtcDateTime)
            throw new AuthorityUnavailableException("Immutable version/retention verification failed.");
        using var output = new MemoryStream();
        var buffer = new byte[64 * 1024]; int read;
        while ((read = await result.ResponseStream.ReadAsync(buffer, token)) != 0)
        {
            if (output.Length + read > CustodyBindingContract.MaximumOriginalBytes) throw new AuthorityUnavailableException("Archive too large.");
            output.Write(buffer, 0, read);
        }
        byte[] bytes = output.ToArray();
        if (CustodyJson.Hash(bytes) != version.Sha256) throw new AuthorityUnavailableException("Immutable hash mismatch.");
        return bytes;
    }
    public async Task<CurrentState> CommitAsync(CurrentState expected, Checkpoint next, CancellationToken token)
    {
        if (next.TenantId != expected.Checkpoint.TenantId || next.EnrollmentDigest != profile.EnrollmentDigest
            || next.Revision != checked(expected.Head.Revision + 1) || next.PredecessorDigest != expected.Head.Digest)
            throw new AuthorityUnavailableException("Invalid forward authority transition.");
        if (string.IsNullOrWhiteSpace(expected.ExactHeadAttribute) || expected.Archive is null)
            throw new AuthorityUnavailableException("Exact previously read authority head/archive required.");
        bool admission = next.Originals.Count > expected.Checkpoint.Originals.Count || next.Deliveries.Count > expected.Checkpoint.Deliveries.Count
            || next.NativeIntents.Count > expected.Checkpoint.NativeIntents.Count || next.AllocationExclusions.Count > expected.Checkpoint.AllocationExclusions.Count;
        long ceiling = 32 * 1024 * 1024 - (admission ? profile.CompletionReserveBytes : 0);
        if (next.Registrations.Count > 1000 || next.Originals.Count > 10000 || next.Deliveries.Count > 10000 || next.NativeIntents.Count > 10000 || next.Anchors.Count > 10000)
            throw new AuthorityUnavailableException("Finite capacity exhausted; retained facts cannot be evicted.");
        var plan = CheckpointArchive.Prepare(next, expected.Archive, ceiling);
        byte[] bytes = await CheckpointArchive.WriteAsync(plan, PutImmutableAsync, token);
        var version = await PutImmutableAsync(bytes, "checkpoint", token);
        var head = new AuthorityHead(next.Revision, version.Sha256, version, profile.EnrollmentDigest);
        await VerifyIdentityAsync(token);
        try
        {
            await dynamo.UpdateItemAsync(new UpdateItemRequest {
                TableName = profile.TableArn, Key = Key(next.TenantId), ConditionExpression = "#head = :previous",
                UpdateExpression = "SET #head = :next", ExpressionAttributeNames = new() { ["#head"] = "head" },
                ExpressionAttributeValues = new() { [":previous"] = new AttributeValue { S = expected.ExactHeadAttribute },
                    [":next"] = new AttributeValue { S = System.Text.Encoding.UTF8.GetString(CustodyJson.Bytes(head)) } },
            }, token);
        }
        catch (ConditionalCheckFailedException) { throw new AuthorityConflictException(); }
        // Lost acknowledgement is not retried. The next invocation reads the surviving head.
        return new(head, next, System.Text.Encoding.UTF8.GetString(CustodyJson.Bytes(head)), CustodyJson.Read<CheckpointArchiveV1>(bytes));
    }
    private Dictionary<string, AttributeValue> Key(string tenant) => new() {
        ["authorityTenant"] = new AttributeValue { S = profile.Enrollment.AuthorityId + ":" + CustodyJson.Hash(System.Text.Encoding.UTF8.GetBytes(tenant)) } };
}
