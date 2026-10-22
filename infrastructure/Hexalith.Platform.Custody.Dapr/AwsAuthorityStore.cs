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
        var checkpoint = CustodyJson.Read<Checkpoint>(await ReadImmutableAsync(head.Checkpoint, token));
        if (head.EnrollmentDigest != profile.EnrollmentDigest || checkpoint.EnrollmentDigest != profile.EnrollmentDigest
            || head.Revision != checkpoint.Revision || head.Digest != head.Checkpoint.Sha256 || checkpoint.TenantId != tenant)
            throw new AuthorityUnavailableException("Current authority binding mismatch.");
        return new(head, checkpoint);
    }
    public async Task<ObjectVersion> PutImmutableAsync(ReadOnlyMemory<byte> bytes, string category, CancellationToken token)
    {
        if (category is not ("checkpoint" or "original" or "journal") || bytes.Length is 0 or > CustodyBindingContract.MaximumOriginalBytes
            || profile.RetainUntil <= clock.GetUtcNow()) throw new AuthorityUnavailableException("Immutable retention or bounds unavailable.");
        BucketIdentity bucket = category == "checkpoint" ? profile.Checkpoints : profile.Originals;
        string hash = CustodyJson.Hash(bytes.Span);
        string key = category + "/" + Guid.NewGuid().ToString("N") + "/" + hash;
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
            || result.ObjectLockRetainUntilDate < version.RetainUntil.UtcDateTime || version.RetainUntil <= clock.GetUtcNow())
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
        byte[] bytes = CustodyJson.Bytes(next);
        if (next.Registrations.Count > 1000 || next.Originals.Count > 10000 || bytes.Length > 32 * 1024 * 1024 - profile.CompletionReserveBytes)
            throw new AuthorityUnavailableException("Finite capacity exhausted; retained facts cannot be evicted.");
        var version = await PutImmutableAsync(bytes, "checkpoint", token);
        var head = new AuthorityHead(next.Revision, version.Sha256, version, profile.EnrollmentDigest);
        await VerifyIdentityAsync(token);
        try
        {
            await dynamo.UpdateItemAsync(new UpdateItemRequest {
                TableName = profile.TableArn, Key = Key(next.TenantId), ConditionExpression = "#head = :previous",
                UpdateExpression = "SET #head = :next", ExpressionAttributeNames = new() { ["#head"] = "head" },
                ExpressionAttributeValues = new() { [":previous"] = new AttributeValue { S = System.Text.Encoding.UTF8.GetString(CustodyJson.Bytes(expected.Head)) },
                    [":next"] = new AttributeValue { S = System.Text.Encoding.UTF8.GetString(CustodyJson.Bytes(head)) } },
            }, token);
        }
        catch (ConditionalCheckFailedException) { throw new AuthorityConflictException(); }
        // Lost acknowledgement is not retried. The next invocation reads the surviving head.
        return new(head, next);
    }
    private Dictionary<string, AttributeValue> Key(string tenant) => new() {
        ["authorityTenant"] = new AttributeValue { S = profile.Enrollment.AuthorityId + ":" + CustodyJson.Hash(System.Text.Encoding.UTF8.GetBytes(tenant)) } };
}
