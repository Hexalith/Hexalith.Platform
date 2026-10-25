using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using Dapr.Client;

using Microsoft.Win32.SafeHandles;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>One Dapr service invocation. Reference strings are the enrolled app ids; a failed call is an unavailable proof.</summary>
public interface ISecretsDaprInvoker
{
    /// <summary>Invokes one method. The caller owns cancellation and the thirty-second budget.</summary>
    Task<string?> InvokeAsync(string appId, string method, string jsonBody, CancellationToken cancellationToken);
}

/// <summary>Private service invocation with a separately mounted workload bearer; implementations must fail before sending when credential resolution fails.</summary>
public interface ISecretsCredentialBoundDaprInvoker : ISecretsDaprInvoker
{
    /// <summary>Authenticates the exact explicit destination with the named credential; reference metadata is never treated as authentication.</summary>
    Task<string?> InvokeCredentialBoundAsync(string appId, string credentialReference, string method, string jsonBody, CancellationToken cancellationToken);
}

/// <summary>Dapr transport. Private calls resolve a bounded file-mounted bearer; no provider or Kubernetes Secret is read by this transport.</summary>
public sealed class DaprSecretsInvoker(DaprClient client) : ISecretsCredentialBoundDaprInvoker
{
    /// <inheritdoc/>
    public Task<string?> InvokeAsync(string appId, string method, string jsonBody, CancellationToken cancellationToken)
        => SendAsync(appId, method, jsonBody, null, cancellationToken);

    /// <inheritdoc/>
    public async Task<string?> InvokeCredentialBoundAsync(string appId, string credentialReference, string method, string jsonBody, CancellationToken cancellationToken)
    {
        if (!SecretsProvisioning.IsDaprApplicationId(appId) || credentialReference is null) { return null; }
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            string? bearer = await ReadMountedBearerAsync(credentialReference, budget.Token).ConfigureAwait(false);
            if (bearer is null) { return null; }
            return await SendAsync(appId, method, jsonBody, bearer, budget.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return null; }
    }

    private async Task<string?> SendAsync(string appId, string method, string jsonBody, string? bearer, CancellationToken cancellationToken)
    {
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false });
        using HttpRequestMessage request = client.CreateInvokeMethodRequest(HttpMethod.Post, appId, method);
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        // The SDK exposes the configured channel token on this client's default headers.
        // Copy them without sending through its redirect-enabled handler.
        using HttpClient configured = client.CreateInvokableHttpClient(appId);
        foreach (var header in configured.DefaultRequestHeaders)
        { if (!request.Headers.Contains(header.Key)) { request.Headers.TryAddWithoutValidation(header.Key, header.Value); } }
        if (bearer is not null) { request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer); }
        using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) { return null; }
        int maximumBytes=method.StartsWith("platform-independent/v1/",StringComparison.Ordinal)?CustodyBindingContract.MaximumIndependentAuthorityFrameBytes-1:SecretsProof.MaximumBytes;
        if (response.Content.Headers.ContentLength > maximumBytes) { return null; }
        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await ReadBoundedAsync(stream, cancellationToken,maximumBytes).ConfigureAwait(false);
    }

    internal static async Task<string?> ReadMountedBearerAsync(string reference, CancellationToken cancellationToken)
    {
        const int maximumBytes = 8192;
        if (!SecretsProvisioning.IsReference(reference) || !reference.StartsWith("file:/", StringComparison.Ordinal)) { return null; }
        string path = reference[5..];
        if (!Path.IsPathFullyQualified(path) || path.Contains('#') || path.Contains('?')) { return null; }
        // A path metadata check followed by ordinary open can race a replacement FIFO and block.
        // Follow projected Secret symlinks, then validate the exact nonblocking opened descriptor.
        if (!OperatingSystem.IsLinux()) { return null; }
        cancellationToken.ThrowIfCancellationRequested();
        int descriptor = LinuxOpen(path, 0x800 | 0x80000); // O_RDONLY | O_NONBLOCK | O_CLOEXEC
        if (descriptor < 0) { return null; }
        using var handle = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
        var stream = new FileStream(handle, FileAccess.Read, 1, isAsync: false);
        await using var streamLifetime = stream.ConfigureAwait(false);
        if (!stream.CanSeek || stream.Length is <= 0 or > maximumBytes) { return null; }
        byte[] bytes = new byte[maximumBytes + 1];
        try
        {
            int used = 0;
            while (used < bytes.Length)
            {
                int count = await stream.ReadAsync(bytes.AsMemory(used), cancellationToken).ConfigureAwait(false);
                if (count == 0) { break; }
                used += count;
            }
            if (used is 0 or > maximumBytes) { return null; }
            if (bytes[used - 1] == (byte)'\n')
            {
                used--;
                if (used > 0 && bytes[used - 1] == (byte)'\r') { used--; }
            }
            if (used == 0 || bytes.AsSpan(0, used).ContainsAnyExceptInRange((byte)33, (byte)126)) { return null; }
            return Encoding.ASCII.GetString(bytes, 0, used);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int LinuxOpen([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    internal static async Task<string?> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken, int maximumBytes = SecretsProof.MaximumBytes)
    {
        if (maximumBytes is <= 0 or > CustodyBindingContract.CandidateRequestMessageBytes) return null;
        using var bytes=new MemoryStream();byte[] buffer=new byte[65536];
        while(true)
        {
            int count=await stream.ReadAsync(buffer.AsMemory(0,Math.Min(buffer.Length,maximumBytes+1-(int)bytes.Length)),cancellationToken).ConfigureAwait(false);
            if(count==0)
            {
                try { return new UTF8Encoding(false,true).GetString(bytes.GetBuffer(),0,(int)bytes.Length); }
                catch(DecoderFallbackException) { return null; }
            }
            await bytes.WriteAsync(buffer.AsMemory(0,count),cancellationToken).ConfigureAwait(false);
            if(bytes.Length>maximumBytes) return null;
        }
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
        => await CallCoreAsync(appId, missingInput, null, null, method, body, cancellationToken).ConfigureAwait(false);

    public async Task<SecretsCall> CallPrivateAsync(string? appId, string missingInput, string? credentialReference, string missingCredential, string method, JsonObject body, CancellationToken cancellationToken)
        => await CallCoreAsync(appId, missingInput, credentialReference, missingCredential, method, body, cancellationToken).ConfigureAwait(false);

    private async Task<SecretsCall> CallCoreAsync(string? appId, string missingInput, string? credentialReference, string? missingCredential, string method, JsonObject body, CancellationToken cancellationToken)
    {
        if (missingCredential is null ? !SecretsProvisioning.IsReference(appId) : !SecretsProvisioning.IsDaprApplicationId(appId))
        {
            throw new SecretsDependencyNotAvailableException(missingInput, SecretsProvisioning.Action(missingInput));
        }
        if (missingCredential is not null)
        {
            if (!SecretsProvisioning.IsReference(credentialReference))
            { throw new SecretsDependencyNotAvailableException(missingCredential, SecretsProvisioning.Action(missingCredential)); }
            if (invoker is not ISecretsCredentialBoundDaprInvoker)
            { throw new SecretsDependencyNotAvailableException("runtime.privateCredentialBoundTransport", SecretsProvisioning.Action("runtime.privateCredentialBoundTransport")); }
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
            Task<string?> call = missingCredential is null
                ? invoker.InvokeAsync(appId!, method, body.ToJsonString(), budget.Token)
                : ((ISecretsCredentialBoundDaprInvoker)invoker).InvokeCredentialBoundAsync(appId!, credentialReference!, method, body.ToJsonString(), budget.Token);
            string? json = await call.WaitAsync(budget.Token).ConfigureAwait(false);
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
