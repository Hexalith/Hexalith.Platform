namespace Hexalith.Platform.Composition.Tests;

/// <summary>Returns owned metadata bytes while observing the existing authenticated request.</summary>
/// <param name="response">The owned response.</param>
/// <param name="observe">The request assertion.</param>
internal sealed class CatalogMetadataResponseHandler(HttpResponseMessage response, Action<HttpRequestMessage> observe) : HttpMessageHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        observe(request);
        return Task.FromResult(response);
    }
}
