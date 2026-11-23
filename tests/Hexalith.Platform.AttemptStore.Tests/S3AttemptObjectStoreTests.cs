using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

using Amazon.Runtime;

using Hexalith.Builds.Tooling.ReleaseRecords;

namespace Hexalith.Platform.AttemptStore.Tests;

public sealed class S3AttemptObjectStoreTests
{
    [Fact]
    public async Task CommittedPutReturningS3ErrorReconcilesByStrongRead()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using S3Handler transport = new();
        using S3AttemptObjectStore backend = new(
            new Uri("https://attempt-store.invalid"), "fr-par", "test-bucket", new BasicAWSCredentials("test", "test"),
            new FakeHttpClientFactory(transport));
        using RecordFixture fixture = new();
        AttemptStore store = new(backend, fixture.Trust);
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

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => new(handler, disposeHandler: false);

        public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => false;

        public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => true;
    }

    private sealed class S3Handler : HttpMessageHandler
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
            if (request.Method == HttpMethod.Get)
            {
                if (key == AttemptStoreTests.StagingHead && HeadReplacements != 0)
                {
                    HeadReadsAfterError++;
                }

                AttemptObject? snapshot = await _objects.ReadAsync(key, AttemptStoreHead.MaximumBytes, cancellationToken);
                if (snapshot is null)
                {
                    return Error(HttpStatusCode.NotFound, "NoSuchKey");
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
                Assert.NotNull(previous);
                Assert.Equal(ETag(previous.Bytes), expected);
                Assert.True(await _objects.ReplaceAsync(key, previous.ETag, bytes, cancellationToken));
                return Error(HttpStatusCode.InternalServerError, "InternalError");
            }

            Assert.Equal("*", Assert.Single(request.Headers.IfNoneMatch).Tag);
            Assert.True(await _objects.CreateAsync(key, bytes, cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK) { Headers = { ETag = ETag(bytes) } };
        }

        private static EntityTagHeaderValue ETag(byte[] bytes) =>
            new($"\"{Convert.ToHexStringLower(MD5.HashData(bytes))}\"");

        private static HttpResponseMessage Error(HttpStatusCode status, string code) =>
            new(status) { Content = new StringContent($"<Error><Code>{code}</Code></Error>", null, "application/xml") };
    }
}
