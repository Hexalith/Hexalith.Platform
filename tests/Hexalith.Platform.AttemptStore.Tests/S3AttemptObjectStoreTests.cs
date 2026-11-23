using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

using Amazon.Runtime;
using Amazon.S3;

using Hexalith.Builds.Tooling.ReleaseRecords;

namespace Hexalith.Platform.AttemptStore.Tests;

public sealed class S3AttemptObjectStoreTests
{
    [Theory]
    [InlineData(300, false)]
    [InlineData(512, true)]
    public async Task SignedAttemptUsesExistingEscapeOrHashesAnOversizedKey(int length, bool hashed)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using S3Handler transport = new();
        using S3AttemptObjectStore backend = Backend(transport);
        using RecordFixture fixture = new();
        AttemptStore store = new(backend, fixture.Trust);
        Assert.True(await store.InitializeAsync(AttemptStore.Staging, token));
        SignedBytes<ReleaseRecord> release = fixture.Release();
        string id = new('~', length);
        SignedBytes<AttemptRecord> attempt = fixture.Attempt(id, AttemptStore.Staging, 1, release);

        await store.AcquireAsync(attempt.Bytes, release.Bytes, cancellationToken: token);

        string segment = hashed
            ? "~" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(id)))
            : string.Concat(Enumerable.Repeat("%7E", length));
        string key = $"environments/staging/records/attempt/{segment}/{attempt.Reference.Digest}.json";
        Assert.Equal(attempt.Bytes, (await backend.ReadAsync(key, attempt.Bytes.Length, token))?.Bytes);
        Assert.Equal(attempt.Reference, (await store.ReadAttemptAsync(AttemptStore.Staging, id, token))?.Reference);
    }

    [Fact]
    public async Task MissingKeyReturnsNullAndMissingBucketPropagates()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using S3Handler missingKey = new();
        using S3AttemptObjectStore keyBackend = Backend(missingKey);
        Assert.Null(await keyBackend.ReadAsync(AttemptStoreTests.StagingHead, 1024, token));

        using S3Handler missingBucket = new(missingGetCode: "NoSuchBucket");
        using S3AttemptObjectStore bucketBackend = Backend(missingBucket);
        AmazonS3Exception error = await Assert.ThrowsAsync<AmazonS3Exception>(() =>
            bucketBackend.ReadAsync(AttemptStoreTests.StagingHead, 1024, token));
        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
        Assert.Equal("NoSuchBucket", error.ErrorCode);
    }

    [Theory]
    [InlineData("server-error")]
    [InlineData("sdk-error")]
    [InlineData("malformed-success")]
    [InlineData("unrecognized-refusal")]
    public async Task CommittedPutWithoutUsableAcknowledgementReconcilesByStrongRead(string failure)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using S3Handler transport = new(committedFailure: failure);
        using S3AttemptObjectStore backend = Backend(transport);
        using RecordFixture fixture = new();
        AttemptStore store = new(backend, fixture.Trust);
        Assert.True(await store.InitializeAsync(AttemptStore.Staging, token));
        SignedBytes<ReleaseRecord> release = fixture.Release();
        SignedBytes<AttemptRecord> attempt = fixture.Attempt("initial", AttemptStore.Staging, 1, release);

        AttemptStoreCommit receipt = await store.AcquireAsync(attempt.Bytes, release.Bytes, cancellationToken: token);

        Assert.Equal(attempt.Reference, receipt.Record);
        Assert.Equal(1L, receipt.Revision);
        Assert.Equal(1, transport.HeadReplacements);
        Assert.Equal(1, transport.HeadReadsAfterError);
        Assert.Equal(2, transport.HeadVersions);
        Assert.Equal(receipt, await store.AcquireAsync(attempt.Bytes, release.Bytes, cancellationToken: token));
        Assert.Equal(1, transport.HeadReplacements);
    }

    [Theory]
    [InlineData(HttpStatusCode.PreconditionFailed, "PreconditionFailed")]
    [InlineData(HttpStatusCode.Conflict, "ConditionalRequestConflict")]
    public async Task ConfirmedConflictReturnsFalseForAnExistingKeyAndStaleETag(HttpStatusCode status, string code)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using S3Handler transport = new(status, code);
        using S3AttemptObjectStore backend = Backend(transport);
        string key = AttemptStoreTests.StagingHead;
        byte[] initial = "{\"revision\":0}"u8.ToArray();
        byte[] update = "{\"revision\":1}"u8.ToArray();
        Assert.True(await backend.CreateAsync(key, initial, token));
        AttemptObject? snapshot = await backend.ReadAsync(key, 1024, token);
        Assert.NotNull(snapshot);
        Assert.True(await backend.ReplaceAsync(key, snapshot.ETag, update, token));

        Assert.False(await backend.CreateAsync(key, initial, token));
        Assert.False(await backend.ReplaceAsync(key, snapshot.ETag, initial, token));
        Assert.Equal(2, transport.HeadVersions);
        Assert.Equal(update, (await backend.ReadAsync(key, 1024, token))?.Bytes);
    }

    private static S3AttemptObjectStore Backend(HttpMessageHandler transport) => new(
        new Uri("https://attempt-store.invalid"), "fr-par", "test-bucket", new BasicAWSCredentials("test", "test"),
        new FakeHttpClientFactory(transport));

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => new(handler, disposeHandler: false);

        public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => false;

        public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => true;
    }

    private sealed class S3Handler(
        HttpStatusCode conflictStatus = HttpStatusCode.PreconditionFailed, string conflictCode = "PreconditionFailed",
        string? committedFailure = null, string missingGetCode = "NoSuchKey") : HttpMessageHandler
    {
        private readonly InMemoryAttemptObjectStore _objects = new();

        internal int HeadReplacements { get; private set; }
        internal int HeadReadsAfterError { get; private set; }
        internal int HeadVersions => _objects.History(AttemptStoreTests.StagingHead).Count;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            const string bucketPath = "/test-bucket/";
            string path = request.RequestUri!.AbsolutePath;
            Assert.StartsWith(bucketPath, path, StringComparison.Ordinal);
            string key = Uri.UnescapeDataString(path[bucketPath.Length..]);
            if (Encoding.UTF8.GetByteCount(key) > 1024)
            {
                return Error(HttpStatusCode.BadRequest, "KeyTooLongError");
            }

            if (request.Method == HttpMethod.Get)
            {
                if (key == AttemptStoreTests.StagingHead && committedFailure is not null && HeadReplacements != 0)
                {
                    HeadReadsAfterError++;
                }

                AttemptObject? snapshot = await _objects.ReadAsync(key, AttemptStoreHead.MaximumBytes, cancellationToken);
                if (snapshot is null)
                {
                    return Error(HttpStatusCode.NotFound, missingGetCode);
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(snapshot.Bytes),
                    Headers = { ETag = ETag(snapshot.Bytes) },
                };
            }

            Assert.Equal(HttpMethod.Put, request.Method);
            byte[] bytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            if (request.Headers.IfMatch.SingleOrDefault() is { } expected)
            {
                Assert.Equal(AttemptStoreTests.StagingHead, key);
                HeadReplacements++;
                AttemptObject? previous = await _objects.ReadAsync(key, AttemptStoreHead.MaximumBytes, cancellationToken);
                if (previous is null || !ETag(previous.Bytes).Equals(expected))
                {
                    return Error(conflictStatus, conflictCode);
                }

                Assert.True(await _objects.ReplaceAsync(key, previous.ETag, bytes, cancellationToken));
                return committedFailure switch
                {
                    "server-error" => Error(HttpStatusCode.InternalServerError, "InternalError"),
                    "sdk-error" => throw new AmazonClientException("Client failed after the write."),
                    "malformed-success" => MalformedSuccess(),
                    "unrecognized-refusal" => Error(HttpStatusCode.Conflict, "UnrecognizedConflict"),
                    _ => new HttpResponseMessage(HttpStatusCode.OK) { Headers = { ETag = ETag(bytes) } },
                };
            }

            Assert.Equal("*", Assert.Single(request.Headers.IfNoneMatch).Tag);
            return await _objects.CreateAsync(key, bytes, cancellationToken)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Headers = { ETag = ETag(bytes) } }
                : Error(conflictStatus, conflictCode);
        }

        private static HttpResponseMessage MalformedSuccess()
        {
            HttpResponseMessage response = new(HttpStatusCode.OK);
            response.Headers.Add("x-amz-server-side-encryption-bucket-key-enabled", "not-a-boolean");
            return response;
        }

        private static EntityTagHeaderValue ETag(byte[] bytes) =>
            new($"\"{Convert.ToHexStringLower(MD5.HashData(bytes))}\"");

        private static HttpResponseMessage Error(HttpStatusCode status, string code) =>
            new(status) { Content = new StringContent($"<Error><Code>{code}</Code></Error>", null, "application/xml") };
    }
}
