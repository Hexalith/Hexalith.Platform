namespace Hexalith.Platform.Protection.Tests;

/// <summary>Records the headers of role-client requests and can refuse before a substituted endpoint is treated as genuine.</summary>
internal sealed class ProtectionQualificationClientObservation : DelegatingHandler
{
    internal List<(string Uri, string? Authorization, string? Contract)> Requests { get; } = [];
    internal bool Refuse { get; init; }
    internal ProtectionQualificationClientObservation(HttpMessageHandler inner) : base(inner) { }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add((request.RequestUri!.AbsoluteUri, request.Headers.Authorization?.ToString(),
            request.Headers.TryGetValues("X-Hexalith-Protection-Role-Contract", out var values) ? string.Join(',', values) : null));
        if (Refuse) { return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)); }
        return base.SendAsync(request, cancellationToken);
    }
}
