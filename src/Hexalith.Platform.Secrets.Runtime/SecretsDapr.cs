using System.Text;
using System.Text.Json.Nodes;

using Dapr.Client;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>One Dapr service invocation. Reference strings are the enrolled app ids; a failed call is an unavailable proof.</summary>
public interface ISecretsDaprInvoker
{
    /// <summary>Invokes one method. The caller owns cancellation and the thirty-second budget.</summary>
    Task<string?> InvokeAsync(string appId, string method, string jsonBody, CancellationToken cancellationToken);
}

/// <summary>Dapr service-invocation transport. This type performs no secret-store read.</summary>
public sealed class DaprSecretsInvoker(DaprClient client) : ISecretsDaprInvoker
{
    /// <inheritdoc/>
    public async Task<string?> InvokeAsync(string appId, string method, string jsonBody, CancellationToken cancellationToken)
    {
        using HttpClient http = client.CreateInvokableHttpClient(appId);
        using var request = new HttpRequestMessage(HttpMethod.Post, method)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
        };
        using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) { return null; }
        if (response.Content.Headers.ContentLength > SecretsProof.MaximumBytes) { return null; }
        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await ReadBoundedAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<string?> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] bytes = new byte[SecretsProof.MaximumBytes + 1];
        int used = 0;
        while (used < bytes.Length)
        {
            int count = await stream.ReadAsync(bytes.AsMemory(used), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                try { return new UTF8Encoding(false, true).GetString(bytes, 0, used); }
                catch (DecoderFallbackException) { return null; }
            }
            used += count;
        }
        return null;
    }
}

/// <summary>A public call result. Unavailable is not an absent proof and must not be followed by an effect.</summary>
internal readonly record struct SecretsCall(JsonObject? Proof, bool Unavailable)
{
    public static SecretsCall Failed { get; } = new(null, true);
}

/// <summary>Bounds one public call. Caller cancellation propagates; only the budget timeout becomes an unavailable proof.</summary>
internal sealed class SecretsDaprSession(ISecretsDaprInvoker invoker, TimeSpan timeout, TimeProvider clock)
{
    public TimeProvider Clock { get; } = clock;

    public async Task<SecretsCall> CallAsync(string? appId, string missingInput, string method, JsonObject body, CancellationToken cancellationToken)
    {
        if (!SecretsProvisioning.IsReference(appId))
        {
            throw new SecretsDependencyNotAvailableException(missingInput, SecretsProvisioning.Action(missingInput));
        }
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30))
        {
            throw new SecretsDependencyNotAvailableException("runtime.callTimeout", SecretsProvisioning.Action("runtime.callTimeout"));
        }
        cancellationToken.ThrowIfCancellationRequested();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(timeout);
        try
        {
            string? json = await invoker.InvokeAsync(appId!, method, body.ToJsonString(), budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
            JsonObject? proof = json is null ? null : SecretsProof.Parse(json);
            return proof is null ? SecretsCall.Failed : new SecretsCall(proof, false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return SecretsCall.Failed;
        }
        catch (Exception)
        {
            return SecretsCall.Failed;
        }
    }
}
