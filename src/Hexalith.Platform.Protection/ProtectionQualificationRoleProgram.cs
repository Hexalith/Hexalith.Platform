using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Executable JSON-stdin transport to actual independently operated role services. It forwards only exact signed native facts and never constructs successful observations.</summary>
public static class ProtectionQualificationRoleProgram
{
    /// <summary>Runs one bounded role operation. Missing inputs emit DependencyNotAvailable with no provider effect; ambiguous mutations are never retried.</summary>
    public static async Task<int> RunAsync(string role, string installationPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var installation = JsonSerializer.Deserialize<ProtectionQualificationRoleInstallation>(await File.ReadAllBytesAsync(installationPath, cancellationToken).ConfigureAwait(false), ErasureRootCustody.Json)
                ?? throw new InvalidOperationException();
            Validate(role, installation);
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); budget.CancelAfter(TimeSpan.FromSeconds(45));
            byte[] input = await ReadAsync(Console.OpenStandardInput(), 16 * 1024 * 1024, budget.Token).ConfigureAwait(false);
            byte[] output;
            using (var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false }) { Timeout = TimeSpan.FromSeconds(40) })
            {
                output = await InvokeAsync(http, installation, input, budget.Token).ConfigureAwait(false);
            }
            await Console.OpenStandardOutput().WriteAsync(output, budget.Token).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            await Console.Out.WriteLineAsync("{\"code\":\"DependencyNotAvailable\",\"record\":\"EXT-PROTECTION-1\"}").ConfigureAwait(false);
            return 1;
        }
    }

    /// <summary>Invokes exactly once, verifies the native independent role signature and full request correlation, and returns its unchanged proof bytes.</summary>
    public static async Task<byte[]> InvokeAsync(HttpClient http, ProtectionQualificationRoleInstallation installation, byte[] input, CancellationToken token)
    {
        Validate(installation.Role, installation);
        CustodyBindingContract.RejectDuplicateProperties(input);
        var request = JsonNode.Parse(input) as JsonObject ?? throw new InvalidOperationException();
        string action = request["action"]?.GetValue<string>() ?? throw new InvalidOperationException();
        if (!Allowed(installation.Role, action)) { throw new InvalidOperationException("Role operation unavailable."); }
        byte[] canonicalRequest = ProtectionQualificationCanonical.Bytes(request);
        string requestDigest = Convert.ToHexStringLower(SHA256.HashData(canonicalRequest));
        string? credential = Environment.GetEnvironmentVariable(installation.CredentialReference[4..]);
        if (string.IsNullOrWhiteSpace(credential)) { throw new InvalidOperationException("DependencyNotAvailable: role credential."); }
        byte[] publicBytes = File.ReadAllBytes(installation.PublicAnchorFile);
        if (Convert.ToHexString(SHA256.HashData(publicBytes)).Equals(installation.PublicAnchorSha256, StringComparison.OrdinalIgnoreCase) is false)
        { throw new InvalidOperationException("Changed role anchor."); }
        using var call = new HttpRequestMessage(HttpMethod.Post, installation.Endpoint);
        call.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        call.Headers.Add("X-Hexalith-Protection-Role-Contract", installation.Schema);
        call.Content = new ByteArrayContent(canonicalRequest); call.Content.Headers.ContentType = new("application/json");
        using var result = await http.SendAsync(call, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        if (!result.IsSuccessStatusCode || result.Content.Headers.ContentLength > 16 * 1024 * 1024) { throw new InvalidOperationException("Native role unavailable."); }
        using var stream = await result.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        byte[] output = await ReadAsync(stream, 16 * 1024 * 1024, token).ConfigureAwait(false);
        CustodyBindingContract.RejectDuplicateProperties(output);
        var envelope = JsonNode.Parse(output) as JsonObject ?? throw new InvalidOperationException();
        if (envelope.Count != 2 || envelope["statement"] is not JsonObject statement || envelope["signature"] is not JsonValue signature || statement.Count != 6
            || statement["role"]?.GetValue<string>() != installation.Role || statement["identity"]?.GetValue<string>() != installation.Identity
            || statement["operationId"]?.GetValue<string>() != request["operationId"]?.GetValue<string>()
            || statement["bindingDigest"]?.GetValue<string>() != request["bindingDigest"]?.GetValue<string>()
            || statement["requestSha256"]?.GetValue<string>() != requestDigest || statement["response"] is null) { throw new InvalidOperationException("Changed native role original."); }
        using var verifier = ECDsa.Create(); verifier.ImportFromPem(Encoding.UTF8.GetString(publicBytes));
        byte[] signatureBytes = Convert.FromBase64String(signature.GetValue<string>());
        if (verifier.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7" || signatureBytes.Length != 64
            || !verifier.VerifyData(ProtectionQualificationCanonical.Bytes(statement), signatureBytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)
            || !SHA256.HashData(File.ReadAllBytes(installation.PublicAnchorFile)).AsSpan().SequenceEqual(SHA256.HashData(publicBytes)))
        { throw new InvalidOperationException("Invalid or changed native role verifier."); }
        return output;
    }

    internal static bool Allowed(string role, string action)
    {
        if (action is "InspectInstallation" or "LookupOriginalOperation") { return role is "executor" or "observer" or "enrollment"; }
        if (role == "observer") { return action == "ObservePersistedState"; }
        if (role == "enrollment") { return action is "EnrollSingleAttempt" or "RegisterOriginalOperation" or "LookupAttemptOperations"; }
        if (role != "executor") { return false; }
        if (action == "PrepareFixture") { return true; }
        using Stream stream = typeof(ProtectionQualificationRoleProgram).Assembly.GetManifestResourceStream("ExtProtectionOwnerPlan")!;
        using var plan = JsonDocument.Parse(stream);
        return plan.RootElement.GetProperty("cases").EnumerateArray().Any(value => value.GetProperty("steps").EnumerateArray().Any(step => step.GetString() == action));
    }
    private static void Validate(string role, ProtectionQualificationRoleInstallation installation)
    {
        if (role is not ("executor" or "observer" or "enrollment") || installation.Role != role || installation.Schema != "ext-protection-1-role-program/v1"
            || string.IsNullOrWhiteSpace(installation.Identity) || !Uri.TryCreate(installation.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https"
            || !string.IsNullOrEmpty(endpoint.UserInfo) || !System.Text.RegularExpressions.Regex.IsMatch(installation.CredentialReference, "^env:[A-Z][A-Z0-9_]*$")
            || !Path.IsPathFullyQualified(installation.PublicAnchorFile) || new FileInfo(installation.PublicAnchorFile).LinkTarget is not null
            || installation.PublicAnchorSha256 is not { Length: 64 }) { throw new InvalidOperationException("Complete native role installation required."); }
    }
    private static async Task<byte[]> ReadAsync(Stream stream, int maximum, CancellationToken token)
    {
        using var bytes = new MemoryStream(); byte[] chunk = new byte[8192]; int count;
        while ((count = await stream.ReadAsync(chunk, token).ConfigureAwait(false)) > 0)
        { if (bytes.Length + count > maximum) { throw new InvalidOperationException("Bounded native role frame required."); } bytes.Write(chunk, 0, count); }
        return bytes.ToArray();
    }
}
