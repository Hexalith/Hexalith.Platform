using System.Net;
using System.Security.Cryptography;

using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Hexalith.Platform.AttemptStore;

/// <summary>Object storage backed by single S3 requests with automatic retries disabled.</summary>
public sealed class S3AttemptObjectStore : IAttemptObjectStore, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;

    public S3AttemptObjectStore(Uri endpoint, string region, string bucket, AWSCredentials credentials)
        : this(endpoint, region, bucket, credentials, httpClientFactory: null)
    {
    }

    internal S3AttemptObjectStore(
        Uri endpoint, string region, string bucket, AWSCredentials credentials, HttpClientFactory? httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentNullException.ThrowIfNull(credentials);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("The S3 endpoint must use HTTPS.", nameof(endpoint));
        }

        _bucket = bucket;
        AmazonS3Config config = new()
        {
            ServiceURL = endpoint.AbsoluteUri,
            AuthenticationRegion = region,
            ForcePathStyle = true,
            MaxErrorRetry = 0,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
        };
        if (httpClientFactory is not null)
        {
            config.HttpClientFactory = httpClientFactory;
        }

        _client = new AmazonS3Client(credentials, config);
    }

    public async Task<AttemptObject?> ReadAsync(string key, int maximumBytes, CancellationToken cancellationToken)
    {
        try
        {
            using GetObjectResponse response = await _client.GetObjectAsync(_bucket, key, cancellationToken).ConfigureAwait(false);
            if (response.ETag is not { Length: > 0 } || response.ContentLength <= 0 || response.ContentLength > maximumBytes)
            {
                throw new IOException("Invalid attempt store object metadata.");
            }

            // Read only the declared bounded length, then check that the stream is exhausted.
            byte[] bytes = new byte[checked((int)response.ContentLength)];
            await response.ResponseStream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            if (await response.ResponseStream.ReadAsync(new byte[1], cancellationToken).ConfigureAwait(false) != 0)
            {
                throw new IOException("Unexpected attempt store object length.");
            }

            return new AttemptObject(bytes, response.ETag);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound && exception.ErrorCode == "NoSuchKey")
        {
            return null;
        }
    }

    public Task<bool> CreateAsync(string key, byte[] bytes, CancellationToken cancellationToken) =>
        PutAsync(key, bytes, match: null, noneMatch: "*", cancellationToken);

    public Task<bool> ReplaceAsync(string key, string etag, byte[] bytes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(etag);
        if (etag == "*")
        {
            throw new ArgumentException("An observed ETag is required.", nameof(etag));
        }

        return PutAsync(key, bytes, etag, noneMatch: null, cancellationToken);
    }

    public void Dispose() => _client.Dispose();

    private async Task<bool> PutAsync(string key, byte[] bytes, string? match, string? noneMatch, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using MemoryStream body = new(bytes, writable: false);
        try
        {
            PutObjectResponse response = await _client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = body,
                ContentType = "application/json",
                IfMatch = match,
                IfNoneMatch = noneMatch,
                UseChunkEncoding = false,
                MD5Digest = Convert.ToBase64String(MD5.HashData(bytes)),
            }, cancellationToken).ConfigureAwait(false);
            if (response.HttpStatusCode == HttpStatusCode.OK)
            {
                return true;
            }

            throw new AttemptStoreWriteUncertainException(new IOException("Unexpected response to an S3 PUT."));
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.PreconditionFailed ||
            (exception.StatusCode == HttpStatusCode.Conflict && exception.ErrorCode == "ConditionalRequestConflict"))
        {
            return false;
        }
        catch (Exception exception) when (exception is not AttemptStoreWriteUncertainException && !IsKnownRefusal(exception))
        {
            throw new AttemptStoreWriteUncertainException(exception);
        }
    }

    private static bool IsKnownRefusal(Exception exception) =>
        exception is AmazonS3Exception
        {
            StatusCode: HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or
                HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.LengthRequired or
                HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.UnsupportedMediaType,
        };
}
