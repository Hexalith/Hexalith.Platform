using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Custody.Dapr;

// Separately installed service roles. No custody root key or decision-approval operation exists on this receiver.
internal sealed class CustodyIndependentRoleReceiver(ComponentProfile profile, CustodyRoleInstallation installation,
    IAuthorityStore store, DaprClient dapr, ECDsa signer, TimeProvider clock, ISecretsCredentialBoundDaprInvoker? authenticatedTransport = null)
{
    internal async Task<IResult> HandleAsync(HttpContext context, string method, CancellationToken token)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token); budget.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            if (method is not ("prepare" or "authorize" or "observe")) return Results.StatusCode(403);
            string channel = await DaprSecretsInvoker.ReadMountedBearerAsync("file:" + installation.AppChannelTokenFile, budget.Token)
                ?? throw new AuthorityUnavailableException("Installed private app-channel credential required.");
            var channelHeader = context.Request.Headers["dapr-api-token"];
            var bearerHeader = context.Request.Headers.Authorization;
            if (channel.Length is 0 or > 8192 || channelHeader.Count != 1 || channelHeader[0] is not { } actual
                || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(channel), Encoding.UTF8.GetBytes(actual))
                || bearerHeader.Count != 1 || bearerHeader[0] is not { Length: > 7 and <= 8199 } bearer || !bearer.StartsWith("Bearer ", StringComparison.Ordinal))
                return Results.StatusCode(403);
            string? body = await DaprSecretsInvoker.ReadBoundedAsync(context.Request.Body, budget.Token, CustodyBindingContract.CandidateRequestMessageBytes);
            if (body is null) return Results.StatusCode(400);
            byte[] bytes = Encoding.UTF8.GetBytes(body); CustodyBindingContract.RejectDuplicateProperties(bytes);
            CustodyCallIntent intent;
            AuthorizationRequest? authorization = null;
            if (method is "prepare" or "observe") intent = CustodyJson.Read<CustodyCallIntent>(bytes);
            else { authorization = CustodyJson.Read<AuthorizationRequest>(bytes); intent = authorization.Intent; }
            CustodyBindingContract.RejectDuplicateProperties(Encoding.UTF8.GetBytes(intent.BodyJson));
            using var publicBody = JsonDocument.Parse(intent.BodyJson);
            if (publicBody.RootElement.ValueKind != JsonValueKind.Object || ContainsPrivateFields(publicBody.RootElement)) return Results.StatusCode(403);
            ISecretsCredentialBoundDaprInvoker transport = authenticatedTransport ?? new DaprSecretsInvoker(dapr);
            var scopes = new IndependentDaprAuthority(transport, installation.CallerScopeAuthority, clock);
            string bearerSha256 = CustodyJson.Hash(Encoding.UTF8.GetBytes(bearer[7..]));
            if (await scopes.InvokeAsync<Permission>("custody-role-authorize", new { role = installation.Role, intent, bearerSha256 }, budget.Token) is not { Allowed: true })
                return Results.StatusCode(403);
            var current = await store.ReadAsync(intent.TenantId, budget.Token);
            var verifier = new StatementVerifier(profile, clock);
            var originals = new IndependentDaprAuthority(transport, installation.OriginalAuthority, clock);
            var originalScope = new { tenant = intent.TenantId, scope = intent.OwnerPurposeScope, operationId = intent.OperationId,
                revision = current.Head.Revision, digest = current.Head.Digest };
            if (installation.Role == "observer" && method == "observe")
            {
                var observation = await originals.InvokeAsync<SignedStatement>("custody-native-observation-original", originalScope, budget.Token);
                if (observation is null) return Results.StatusCode(503);
                if (!verifier.Verify(observation, "native-evidence-original", current, intent.RequestDigest, TimeSpan.FromSeconds(30))) return Results.StatusCode(503);
                return Results.Bytes(CustodyJson.Bytes(Sign("independent-observer", current, intent.RequestDigest, observation.Payload)), "application/json");
            }
            var control = await originals.InvokeAsync<RetainedControlOriginal>("custody-control-original", originalScope, budget.Token);
            if (control is null) return Results.StatusCode(503);
            string proofHash = CustodyJson.Hash(CustodyJson.Bytes(control.Proof));
            // Control is derived only from a currently signed independently retained guard/protection original.
            if (!verifier.Verify(control.GuardAndProtectionOriginal, "guard-control-original", current, proofHash, TimeSpan.FromSeconds(30))
                || control.Proof.TenantId != intent.TenantId || control.Proof.ExpectedCustodyRevision != current.Head.Revision
                || control.Proof.CheckpointDigest != current.Head.Digest) return Results.StatusCode(503);
            var facts = CustodyJson.Read<ControlClaim>(CustodyJson.Bytes(control.GuardAndProtectionOriginal.Payload));
            if (facts.ControlProofSha256 != proofHash || !CustodyBindingContract.Digest(facts.GuardOriginalSha256)
                || !CustodyBindingContract.Digest(facts.CurrentRevocationSha256)) return Results.StatusCode(503);
            var envelope = new CustodyBindingEnvelopeV1(CustodyBindingContract.Protocol, intent.Operation, intent.OperationId,
                intent.RequestDigest, intent.TenantId, intent.OwnerPurposeScope, profile.Enrollment, current.Head.Revision,
                current.Head.Digest, intent.RequestAttemptId, intent.Challenge, control.Proof);
            var prepared = new CustodyPreparedCall(JsonSerializer.Serialize(envelope, CustodyBindingContract.Json),
                CustodyBindingContract.CandidateRequestMessageBytes, profile.Enrollment.ArtifactProfileDigest, null);
            string id = installation.Role + ":" + intent.RequestAttemptId;
            if (method == "prepare")
            {
                if (installation.Role is not ("authorizer" or "control")) return Results.StatusCode(403);
                var request = JsonSerializer.SerializeToElement(new { intent, prepared }, CustodyBindingContract.Json);
                var admission = await originals.InvokeAsync<OriginalAdmission>("qualification-begin-original", new { role = installation.Role, requestId = id, original = request }, budget.Token);
                return admission is { Exact: true } ? Results.Bytes(CustodyJson.Bytes(prepared), "application/json") : Results.StatusCode(503);
            }
            if (authorization is null || !CustodyBindingContract.Digest(authorization.RequestSha256) || authorization.Prepared != prepared)
                return Results.StatusCode(403);
            var retained = await originals.InvokeAsync<IndependentOriginal>("qualification-lookup-original", new { role = installation.Role, requestId = id }, budget.Token);
            if (retained is null || !retained.Request.GetRawText().AsSpan().SequenceEqual(JsonSerializer.SerializeToElement(new { intent, prepared }, CustodyBindingContract.Json).GetRawText()))
                return Results.StatusCode(503);
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
            if (installation.Role == "authorizer")
            {
                var permission = new OperationPermission(intent.Operation, intent.OperationId, intent.RequestAttemptId, intent.Challenge, intent.OwnerPurposeScope, profile.ProfileDigest);
                metadata["custody-operation-proof"] = Convert.ToBase64String(CustodyJson.Bytes(Sign("private-operation", current, authorization.RequestSha256, permission)));
                if (control.ControlStatement is null || !verifier.Verify(control.ControlStatement, "control", current, proofHash, TimeSpan.FromSeconds(30)))
                    return Results.StatusCode(503);
                metadata["custody-control-proof"] = Convert.ToBase64String(CustodyJson.Bytes(control.ControlStatement));
            }
            else if (installation.Role == "control") metadata["custody-control-proof"] = Convert.ToBase64String(CustodyJson.Bytes(Sign("control", current, proofHash, facts)));
            else return Results.StatusCode(403); // Observation claims require actual retained native evidence and are never inferred here.
            return Results.Bytes(CustodyJson.Bytes(new CustodyCallAuthorization(authorization.RequestSha256, metadata)), "application/json");
        }
        catch (Exception) { return Results.StatusCode(503); }
    }
    private static bool ContainsPrivateFields(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
            return value.EnumerateObject().Any(p => new[] { "RootKey", "keyMaterial", "privateKey", "plaintext", "credentialValue" }
                .Contains(p.Name, StringComparer.OrdinalIgnoreCase) || ContainsPrivateFields(p.Value));
        return value.ValueKind == JsonValueKind.Array && value.EnumerateArray().Any(ContainsPrivateFields);
    }
    private SignedStatement Sign<T>(string purpose, CurrentState current, string subject, T payload)
    {
        string requiredPurpose = installation.Role switch { "authorizer" => "private-operation", "observer" => "independent-observer", _ => installation.Role };
        if (purpose != requiredPurpose || !profile.Trust.Any(t => t.KeyId == installation.SignerId && t.Purpose == purpose))
            throw new AuthorityUnavailableException("Signing role substitution denied.");
        var signed = new SignedStatement(purpose, installation.SignerId, profile.Enrollment.AuthorityId, profile.Enrollment.AuthorityIncarnation,
            current.Checkpoint.TenantId, current.Head.Revision, current.Head.Digest, subject, clock.GetUtcNow(), clock.GetUtcNow().AddSeconds(30),
            JsonSerializer.SerializeToElement(payload, CustodyBindingContract.Json), []);
        lock (signer) return signed with { Signature = signer.SignData(CustodyJson.Bytes(signed), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence) };
    }
    private sealed record Permission(bool Allowed);
    private sealed record OriginalAdmission(bool Exact, bool First);
    private sealed record AuthorizationRequest(CustodyCallIntent Intent, CustodyPreparedCall Prepared, string RequestSha256);
}
