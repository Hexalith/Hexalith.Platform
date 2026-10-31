using System.Net;
using System.Text;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Bounded HTTP protocol fixture without any native provider or production endpoint.</summary>
internal sealed class ErasureRootTransportHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> response, bool readBodyAsString = true) : HttpMessageHandler
{
    internal int Calls { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return response(request, request.Content is null || !readBodyAsString ? null : await request.Content.ReadAsStringAsync(cancellationToken));
    }
    internal static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
