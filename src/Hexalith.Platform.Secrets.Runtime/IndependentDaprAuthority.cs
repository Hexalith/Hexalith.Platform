using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Authenticated, bounded, nonce-bound exact-current service invocation. No retry or local authority fallback.</summary>
public sealed class IndependentDaprAuthority
{
    private readonly ISecretsCredentialBoundDaprInvoker _transport;
    private readonly IndependentDaprAuthorityBinding _binding;
    private readonly TimeProvider _clock;

    /// <summary>Captures an externally enrolled service with disjoint private credentials.</summary>
    public IndependentDaprAuthority(ISecretsCredentialBoundDaprInvoker transport, IndependentDaprAuthorityBinding binding, TimeProvider clock)
    {
        _transport = transport; _binding = binding; _clock = clock;
        if (!SecretsProvisioning.IsDaprApplicationId(binding.AppId) || !SecretsProvisioning.IsReference(binding.CredentialReference)
            || !SecretsProvisioning.IsReference(binding.EnrollmentId) || !SecretsProvisioning.IsReference(binding.Scope)
            || !SecretsProvisioning.IsReference(binding.CurrentTrustRevision)) throw new ArgumentException("Independent current enrollment required.");
        using var key = ECDsa.Create(); key.ImportFromPem(binding.PublicKeyPem);
        if (key.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7") throw new ArgumentException("Independent P-256 public anchor required.");
    }

    /// <summary>Calls one exact purpose and verifies the current enrolled signed original response before returning its payload.</summary>
    public async Task<T?> InvokeAsync<T>(string method, object payload, CancellationToken cancellationToken = default)
    {
        if (!SecretsProvisioning.IsReference(method)) return default;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); budget.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            string nonce = Guid.NewGuid().ToString("N");
            var request = new Request("platform-independent-authority-v1", _binding.EnrollmentId, _binding.Scope,
                _binding.CurrentTrustRevision, method, nonce, JsonSerializer.SerializeToElement(payload, CustodyBindingContract.Json));
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(request, CustodyBindingContract.Json);
            if (bytes.Length >= CustodyBindingContract.MaximumPageFrameBytes) return default;
            string? json = await _transport.InvokeCredentialBoundAsync(_binding.AppId, _binding.CredentialReference,
                "platform-independent/v1/" + method, Encoding.UTF8.GetString(bytes), budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
            if (json is null || Encoding.UTF8.GetByteCount(json) >= CustodyBindingContract.MaximumPageFrameBytes) return default;
            byte[] responseBytes = Encoding.UTF8.GetBytes(json); CustodyBindingContract.RejectDuplicateProperties(responseBytes);
            var response = JsonSerializer.Deserialize<Response>(responseBytes, CustodyBindingContract.Json);
            long now = _clock.GetUtcNow().ToUnixTimeSeconds();
            if (response is null || response.EnrollmentId != _binding.EnrollmentId || response.Scope != _binding.Scope
                || response.TrustRevision != _binding.CurrentTrustRevision || response.Nonce != nonce || response.Method != method
                || response.RequestSha256 != Convert.ToHexString(SHA256.HashData(bytes)) || response.ObservedAt > now
                || response.ObservedAt < now - 30 || response.ValidUntil <= now || response.ValidUntil > response.ObservedAt + 30) return default;
            using var key = ECDsa.Create(); key.ImportFromPem(_binding.PublicKeyPem);
            if (!key.VerifyData(JsonSerializer.SerializeToUtf8Bytes(response with { Signature = [] }, CustodyBindingContract.Json),
                response.Signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence)) return default;
            budget.Token.ThrowIfCancellationRequested();
            return response.Payload.Deserialize<T>(CustodyBindingContract.Json);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return default; }
    }
    private sealed record Request(string Protocol, string EnrollmentId, string Scope, string TrustRevision, string Method, string Nonce, JsonElement Payload);
    private sealed record Response(string EnrollmentId, string Scope, string TrustRevision, string Method, string Nonce,
        string RequestSha256, long ObservedAt, long ValidUntil, JsonElement Payload, byte[] Signature);
}
