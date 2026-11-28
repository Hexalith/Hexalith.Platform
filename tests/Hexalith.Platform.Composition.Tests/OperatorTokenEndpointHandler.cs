namespace Hexalith.Platform.Composition.Tests;

using System.Net;
using System.Text;
using System.Text.Json;

/// <summary>Answers the generated realm's token endpoint with a scripted sequence and records each form request.</summary>
internal sealed class OperatorTokenEndpointHandler(params Func<IReadOnlyDictionary<string, string>, HttpResponseMessage>[] responses) : HttpMessageHandler
{
    private int _calls;

    internal List<IReadOnlyDictionary<string, string>> Requests { get; } = [];

    /// <summary>Creates an unsigned compact JWT carrying the given claims; the relay only reads its payload.</summary>
    internal static string Token(object claims)
    {
        static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return Encode(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}")) + "." + Encode(JsonSerializer.SerializeToUtf8Bytes(claims)) + ".signature";
    }

    /// <summary>Creates a successful token response.</summary>
    internal static HttpResponseMessage Issued(object claims) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { access_token = Token(claims), token_type = "Bearer", expires_in = 300 }), Encoding.UTF8, "application/json"),
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string form = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> values = form.Split('&').Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')), pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')), StringComparer.Ordinal);
        Requests.Add(values);
        int index = Math.Min(Interlocked.Increment(ref _calls) - 1, responses.Length - 1);
        return responses[index](values);
    }
}
