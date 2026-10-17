using System.Net.Http.Headers;
using System.Text;

using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Secrets.Inventory;

/// <summary>Authenticated Dapr HTTP transport for the unchanged completed adapters.</summary>
public sealed class AuthenticatedSecretsInvoker(HttpClient client, IInventoryWorkloadToken identity, Func<string?> daprCredential) : ISecretsDaprInvoker
{
    /// <inheritdoc/>
    public async Task<string?> InvokeAsync(string appId, string method, string jsonBody, CancellationToken cancellationToken)
    {
        string? bearer = await identity.GetAsync(cancellationToken).ConfigureAwait(false);
        string? channel = daprCredential();
        if (string.IsNullOrWhiteSpace(bearer) || string.IsNullOrWhiteSpace(channel)) { return null; }
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "v1.0/invoke/" + Uri.EscapeDataString(appId) + "/method/" + Uri.EscapeDataString(method))
        { Content = new StringContent(jsonBody, Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        request.Headers.Add("dapr-api-token", channel);
        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 1048576) { return null; }
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        byte[] bytes = new byte[1048577];
        int used = 0;
        while (used < bytes.Length)
        {
            int read = await stream.ReadAsync(bytes.AsMemory(used), cancellationToken).ConfigureAwait(false);
            if (read == 0) { return new UTF8Encoding(false, true).GetString(bytes, 0, used); }
            used += read;
        }
        return null;
    }
}
