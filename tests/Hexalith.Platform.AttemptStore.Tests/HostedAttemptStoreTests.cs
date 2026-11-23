using System.Net;
using System.Security.Cryptography;

using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

using Hexalith.Builds.Tooling.ReleaseRecords;

namespace Hexalith.Platform.AttemptStore.Tests;

public sealed class HostedAttemptStoreTests
{
    private const string ProductionCanary = "environments/production/verification/read-denial-canary.json";
    private const string ProductionCanaryBody = "{\"purpose\":\"attempt-store-read-denial-canary\"}";

    [Fact]
    public async Task HostedWriterSerializesAcquisitionAndProviderEnforcesScope()
    {
        LiveConfiguration? live = LiveConfiguration.Read();
        Assert.SkipWhen(live is null, "Hosted verification did not run. Supply the ATTEMPT_STORE_LIVE_* configuration.");
        Assert.NotNull(live);

        CancellationToken token = TestContext.Current.CancellationToken;
        string runId = Guid.NewGuid().ToString("N");
        string prefix = $"environments/staging/verification/{runId}/";
        string head = prefix + "head.json";
        using S3AttemptObjectStore writer = new(live.Endpoint, live.Region, live.Bucket, live.Writer);
        using S3AttemptObjectStore reader = new(live.Endpoint, live.Region, live.Bucket, live.Reader);
        using RecordFixture fixture = new();
        WaitForTwoWriters barrier = new(AttemptStoreTests.StagingHead);
        AttemptStore stagingWriter = new(new ScopedObjects(writer, prefix, barrier), fixture.Trust);
        AttemptStore stagingReader = new(new ScopedObjects(reader, prefix), fixture.Trust);
        Assert.True(await stagingWriter.InitializeAsync(AttemptStore.Staging, token));
        SignedBytes<ReleaseRecord> release = fixture.Release();
        SignedBytes<AttemptRecord> a = fixture.Attempt("attempt-a", AttemptStore.Staging, 1, release);
        SignedBytes<AttemptRecord> b = fixture.Attempt("attempt-b", AttemptStore.Staging, 1, release);

        string?[] results = await Task.WhenAll(
            AttemptStoreTests.RefusalAsync(() => stagingWriter.AcquireAsync(a.Bytes, release.Bytes, cancellationToken: token)),
            AttemptStoreTests.RefusalAsync(() => stagingWriter.AcquireAsync(b.Bytes, release.Bytes, cancellationToken: token)));
        Assert.Contains(null, results);
        Assert.Contains("lock-held", results);
        AttemptStoreState? state = await stagingReader.ReadAsync(AttemptStore.Staging, token);
        Assert.NotNull(state);
        Assert.Equal((1L, 1L), (state.Revision, state.Epoch));
        Assert.Equal(results[0] is null ? a.Reference : b.Reference, state.Holder?.Reference);
        Assert.Null(await stagingReader.ReadAttemptAsync(AttemptStore.Staging, results[0] is null ? "attempt-b" : "attempt-a", token));

        using GetObjectResponse current = await ReadRawAsync(live, head, token);
        Assert.NotNull(current.VersionId);
        Assert.False(string.IsNullOrWhiteSpace(current.ETag));
        Assert.False(await writer.ReplaceAsync(head, "\"00000000000000000000000000000000\"", "{}"u8.ToArray(), token));
        Assert.False(await writer.CreateAsync(head, "{}"u8.ToArray(), token));

        await DeniedAsync(live, live.Writer, client => PutRawAsync(client, live.Bucket, head, token));
        await DeniedAsync(live, live.Writer, client => PutRawAsync(client, live.Bucket, prefix + "records/attempt/probe/unconditional.json", token));
        await DeniedAsync(live, live.Writer, client => PutRawAsync(
            client, live.Bucket, $"environments/production/verification/{runId}/head.json", token, create: true));
        AttemptObject? canary = await reader.ReadAsync(ProductionCanary, 1024, token);
        Assert.NotNull(canary);
        Assert.Equal(ProductionCanaryBody, System.Text.Encoding.UTF8.GetString(canary.Bytes));
        await DeniedAsync(live, live.Writer, client => client.GetObjectAsync(live.Bucket, ProductionCanary, token));
        await DeniedAsync(live, live.Writer, client => client.DeleteObjectAsync(live.Bucket, head, token));
        await DeniedAsync(live, live.Writer, client => client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = live.Bucket,
            Key = head,
            VersionId = current.VersionId,
        }, token));
        await DeniedAsync(live, live.Reader, client => PutRawAsync(
            client, live.Bucket, prefix + "records/attempt/probe/reader.json", token, create: true));

        string winnerKey = StoredRecord.Key(AttemptStore.Staging, "attempt", state.Holder!.Reference);
        string record = prefix + winnerKey["environments/staging/".Length..];
        using GetObjectResponse signed = await ReadRawAsync(live, record, token);
        await DeniedAsync(live, live.Writer, client => PutRawAsync(client, live.Bucket, record, token, match: signed.ETag));
        Assert.Equal(state.Revision, (await stagingReader.ReadAsync(AttemptStore.Staging, token))?.Revision);

        using AmazonS3Client historyReader = live.Client(live.Reader);
        ListVersionsResponse history = await historyReader.ListVersionsAsync(new ListVersionsRequest
        {
            BucketName = live.Bucket,
            Prefix = head,
        }, token);
        Assert.False(history.IsTruncated == true);
        S3ObjectVersion[] headVersions = history.Versions.Where(version => version.Key == head).ToArray();
        Assert.Equal(2, headVersions.Length);
        Assert.All(headVersions, version => Assert.False(version.IsDeleteMarker == true));
        S3ObjectVersion latest = Assert.Single(headVersions, version => version.IsLatest == true);
        Assert.Equal(current.VersionId, latest.VersionId);
        Console.WriteLine($"Hosted attempt store verification passed on {prefix}");
    }

    private static async Task<GetObjectResponse> ReadRawAsync(LiveConfiguration live, string key, CancellationToken token)
    {
        using AmazonS3Client client = live.Client(live.Reader);
        return await client.GetObjectAsync(live.Bucket, key, token);
    }

    private static async Task PutRawAsync(AmazonS3Client client, string bucket, string key, CancellationToken token,
        bool create = false, string? match = null)
    {
        byte[] bytes = "{}"u8.ToArray();
        using MemoryStream body = new(bytes);
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = body,
            ContentType = "application/json",
            UseChunkEncoding = false,
            MD5Digest = Convert.ToBase64String(MD5.HashData(bytes)),
            IfMatch = match,
            IfNoneMatch = create ? "*" : null,
        }, token);
    }

    private static async Task DeniedAsync(LiveConfiguration live, AWSCredentials credentials, Func<AmazonS3Client, Task> request)
    {
        using AmazonS3Client client = live.Client(credentials);
        AmazonS3Exception exception = await Assert.ThrowsAsync<AmazonS3Exception>(() => request(client));
        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal("AccessDenied", exception.ErrorCode);
    }

    private sealed class ScopedObjects(S3AttemptObjectStore inner, string prefix, WaitForTwoWriters? barrier = null) : IAttemptObjectStore
    {
        private string Scoped(string key)
        {
            const string staging = "environments/staging/";
            Assert.StartsWith(staging, key, StringComparison.Ordinal);
            return prefix + key[staging.Length..];
        }

        public Task<AttemptObject?> ReadAsync(string key, int maximumBytes, CancellationToken cancellationToken) =>
            inner.ReadAsync(Scoped(key), maximumBytes, cancellationToken);

        public Task<bool> CreateAsync(string key, byte[] bytes, CancellationToken cancellationToken) =>
            inner.CreateAsync(Scoped(key), bytes, cancellationToken);

        public async Task<bool> ReplaceAsync(string key, string etag, byte[] bytes, CancellationToken cancellationToken)
        {
            if (barrier is not null)
            {
                await barrier.WaitAsync(key, cancellationToken);
            }

            return await inner.ReplaceAsync(Scoped(key), etag, bytes, cancellationToken);
        }
    }

    private sealed record LiveConfiguration(Uri Endpoint, string Region, string Bucket, AWSCredentials Writer, AWSCredentials Reader)
    {
        internal static LiveConfiguration? Read()
        {
            string[] keys = ["ENDPOINT", "REGION", "BUCKET", "WRITER_ACCESS_KEY", "WRITER_SECRET_KEY", "READER_ACCESS_KEY", "READER_SECRET_KEY"];
            string?[] values = keys.Select(key => Environment.GetEnvironmentVariable("ATTEMPT_STORE_LIVE_" + key)).ToArray();
            if (values.All(string.IsNullOrWhiteSpace))
            {
                return null;
            }

            Assert.All(values, value => Assert.True(!string.IsNullOrWhiteSpace(value), "Partial hosted attempt store configuration."));
            return new(new Uri(values[0]!), values[1]!, values[2]!, new BasicAWSCredentials(values[3]!, values[4]!),
                new BasicAWSCredentials(values[5]!, values[6]!));
        }

        internal AmazonS3Client Client(AWSCredentials credentials) => new(credentials, new AmazonS3Config
        {
            ServiceURL = Endpoint.AbsoluteUri,
            AuthenticationRegion = Region,
            ForcePathStyle = true,
            MaxErrorRetry = 0,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
        });
    }
}
