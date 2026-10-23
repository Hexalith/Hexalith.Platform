using System.Reflection;
using System.Text;
using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Hexalith.Platform.Custody.Dapr;

// Native API doubles exercise the real store implementation; they establish no provider guarantee.
internal static class NativeStoreTests
{
    internal static async Task Run()
    {
        using var fixture = new Fixture(); byte[] enrollment = "{}"u8.ToArray();
        var profile = fixture.Profile with {
            Checkpoints = fixture.Profile.Checkpoints with { EnrollmentSha256 = CustodyJson.Hash(enrollment) },
            Originals = fixture.Profile.Originals with { EnrollmentSha256 = CustodyJson.Hash(enrollment) },
        };
        var objects = new Dictionary<string, byte[]> { ["enrollment"] = enrollment };
        _ = await fixture.Call(fixture.Wrap("native-store"));
        objects["checkpoint/current"] = await CheckpointArchive.EncodeAsync(fixture.Store.State.Checkpoint, Seed, default);
        var checkpoint = new ObjectVersion("checkpoint/current", "v1", CustodyJson.Hash(objects["checkpoint/current"]), objects["checkpoint/current"].Length, profile.RetainUntil);
        string head = JsonSerializer.Serialize(new AuthorityHead(fixture.Store.State.Head.Revision, checkpoint.Sha256, checkpoint, profile.EnrollmentDigest), new JsonSerializerOptions(CustodyBindingContract.Json) { WriteIndented = true }) + " \n";
        bool clone = false, missing = false, loseAck = false; int updates = 0, puts = 0;
        using var dynamo = ApiDouble.Create<IAmazonDynamoDB>((method, args) => method switch {
            "DescribeTableAsync" => Task.FromResult(new DescribeTableResponse { Table = new() { TableArn = profile.TableArn, TableId = clone ? "restored-id" : profile.TableId, TableStatus = TableStatus.ACTIVE } }),
            "GetItemAsync" => Get((GetItemRequest)args[0]!),
            "UpdateItemAsync" => Update((UpdateItemRequest)args[0]!),
            _ => throw new Exception("Unexpected native method " + method),
        });
        using var s3 = ApiDouble.Create<IAmazonS3>((method, args) => method switch {
            "GetBucketVersioningAsync" => Task.FromResult(new GetBucketVersioningResponse { VersioningConfig = new() { Status = VersionStatus.Enabled } }),
            "GetObjectAsync" => Read((GetObjectRequest)args[0]!),
            "PutObjectAsync" => Put((PutObjectRequest)args[0]!),
            _ => throw new Exception("Unexpected native method " + method),
        });
        var store = new AwsAuthorityStore(profile, dynamo, s3, TimeProvider.System);
        var current = await store.ReadAsync("tenant", default);
        _ = await store.ReadImmutableAsync(checkpoint with { RetainUntil = DateTimeOffset.UtcNow.AddDays(-1) }, default);
        // Native lock is still beyond the original floor: extending retention cannot strand an exact original.
        clone = true; await Refused(() => store.ReadAsync("tenant", default)); clone = false;
        missing = true; await Refused(() => store.ReadAsync("tenant", default)); missing = false;
        var exhausted = SemanticProvider.Clone(current);
        for (int i = 0; i < 10001; i++) exhausted.Originals["capacity-" + i] = new(Fixture.Digest, "lookup-original", null, null);
        await Refused(() => store.CommitAsync(current, exhausted, default));
        if (puts != 0 || updates != 0) throw new Exception("Exhausted counts performed a native write.");
        var oversized = SemanticProvider.Clone(current);
        for (int i = 0; i < 9000; i++) { string hold = i.ToString("D5") + new string('h', 2040); oversized.Pins[hold] = new(hold, Fixture.Digest, 1, "guard"); }
        await Refused(() => store.CommitAsync(current, oversized, default));
        if (puts != 0 || updates != 0) throw new Exception("Exhausted index capacity performed a native write.");
        loseAck = true; await Refused(() => store.CommitAsync(current, SemanticProvider.Clone(current), default));
        var advanced = await store.ReadAsync("tenant", default);
        if (advanced.Head.Revision != current.Head.Revision + 1 || updates != 1 || puts != 1) throw new Exception("Lost acknowledgement retried, rewrote an unchanged payload, or failed to select durable head.");
        await Refused(() => store.CommitAsync(current, SemanticProvider.Clone(current), default));
        if (updates != 2) throw new Exception("Stale head comparison did not execute exactly one CAS.");
        return;

        Task<ObjectVersion> Seed(ReadOnlyMemory<byte> bytes, string category, CancellationToken token) {
            string key = category + "/" + Guid.NewGuid().ToString("N"); objects.Add(key, bytes.ToArray());
            return Task.FromResult(new ObjectVersion(key, "v1", CustodyJson.Hash(bytes.Span), bytes.Length, profile.RetainUntil));
        }
        Task<GetItemResponse> Get(GetItemRequest request) {
            if (request.ConsistentRead != true || request.TableName != profile.TableArn) throw new Exception("Current read lost identity/strong consistency.");
            return Task.FromResult(new GetItemResponse { Item = missing ? [] : new() { ["head"] = new() { S = head } } });
        }
        Task<UpdateItemResponse> Update(UpdateItemRequest request) {
            updates++;
            if (request.ConditionExpression != "#head = :previous" || request.ExpressionAttributeValues[":previous"].S != head)
                throw new ConditionalCheckFailedException("Synthetic exact CAS conflict.");
            head = request.ExpressionAttributeValues[":next"].S;
            if (loseAck) { loseAck = false; throw new HttpRequestException("Synthetic lost CAS acknowledgement."); }
            return Task.FromResult(new UpdateItemResponse());
        }
        Task<GetObjectResponse> Read(GetObjectRequest request) {
            if (request.ExpectedBucketOwner != "000000000000") throw new Exception("Bucket owner unpinned.");
            byte[] bytes = objects[request.Key];
            return Task.FromResult(new GetObjectResponse { VersionId = "v1", ContentLength = bytes.Length, ResponseStream = new MemoryStream(bytes),
                ObjectLockMode = ObjectLockMode.Compliance, ObjectLockRetainUntilDate = profile.RetainUntil.UtcDateTime });
        }
        async Task<PutObjectResponse> Put(PutObjectRequest request) {
            puts++;
            if (request.IfNoneMatch != "*" || request.ObjectLockMode != ObjectLockMode.Compliance) throw new Exception("Immutable write lost no-replace/retention constraint.");
            using var bytes = new MemoryStream(); await request.InputStream.CopyToAsync(bytes); objects.Add(request.Key, bytes.ToArray());
            return new PutObjectResponse { VersionId = "v1" };
        }
    }
    private static async Task Refused(Func<Task> action)
    {
        try { await action(); }
        catch (Exception e) when (e is AuthorityUnavailableException or AuthorityConflictException or HttpRequestException) { return; }
        throw new Exception("Native store did not fail closed.");
    }
}
public class ApiDouble : DispatchProxy
{
    internal Func<string, object?[], object?> Handler = null!;
    internal static T Create<T>(Func<string, object?[], object?> handler) where T : class
    { var result = Create<T, ApiDouble>(); ((ApiDouble)(object)result).Handler = handler; return result; }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name == "Dispose" ? null : Handler(targetMethod!.Name, args ?? []);
}
