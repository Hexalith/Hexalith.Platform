namespace Hexalith.Platform.Composition.Tests;

/// <summary>Returns only explicitly controlled readiness responses without starting services or granting runtime qualification.</summary>
/// <param name="respond">Creates a distinct owned response for each observed request, including an explicitly held response.</param>
internal sealed class NativeReadinessResponseHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    /// <summary>Adapts immediate controlled responses without altering their ownership or request assertions.</summary>
    internal NativeReadinessResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : this((request, _) => Task.FromResult(respond(request)))
    {
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return respond(request, cancellationToken);
    }
}
