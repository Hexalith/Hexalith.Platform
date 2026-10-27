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
    private readonly SemaphoreSlim _receivingSlots=new(4,4);
    internal Action? ReceivingStartup { get; init; }
    internal async Task<IResult> HandleAsync(HttpContext context,string method,CancellationToken token)
    {
        if(!_receivingSlots.Wait(0)) return Results.StatusCode(503);
        IndependentReceivingOwnership? ownership=null;
        var previous=IndependentReceivingOwnership.Current.Value;
        try
        {
            ownership=new IndependentReceivingOwnership(_receivingSlots,token);
            ReceivingStartup?.Invoke();ownership.Start();
            IndependentReceivingOwnership.Current.Value=ownership;
            context.Response.RegisterForDispose(ownership);
            var work=HandleCoreAsync(context,method,ownership.Token);ownership.Track(work);
            IResult result;
            try { result=await work.WaitAsync(ownership.Token).ConfigureAwait(false); }
            catch(Exception) { result=Results.StatusCode(503); }
            return ownership.Wrap(result);
        }
        catch(Exception) { if(ownership is null) _receivingSlots.Release();else ownership.Dispose();return Results.StatusCode(503); }
        finally { IndependentReceivingOwnership.Current.Value=previous; }
    }
    private async Task<IResult> HandleCoreAsync(HttpContext context, string method, CancellationToken token)
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
            var workload=await scopes.InvokeAsync<CustodyRoleWorkloadGrant>("custody-role-authorize",new { role=installation.Role,intent,bearerSha256,method },budget.Token);
            if(workload is null || workload.BearerSha256!=bearerSha256 || workload.Role!=installation.Role || workload.ScopeReference!=intent.TenantId+"|"+intent.OwnerPurposeScope
                || workload.IntentSha256!=CustodyJson.Hash(CustodyJson.Bytes(intent)) || workload.ValidUntil<=clock.GetUtcNow()) return Results.StatusCode(403);
            async Task<CustodyRoleIssuance> Issue(object response)
            {
                string responseSha256=CustodyJson.Hash(CustodyJson.Bytes(response));
                var issued=await scopes.InvokeAsync<CustodyRoleIssuance>("custody-role-authorize",new { role=installation.Role,intent,bearerSha256,method,grant=workload,responseSha256 },budget.Token);
                if(issued is null || issued.Grant!=workload || issued.Method!=method || issued.ResponseSha256!=responseSha256 || workload.ValidUntil<=clock.GetUtcNow())
                    throw new AuthorityUnavailableException("Current exact workload issuance denied.");
                return issued;
            }
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
                var issuance=await Issue(observation).ConfigureAwait(false);
                return Results.Bytes(CustodyJson.Bytes(Sign("independent-observer",current,intent.RequestDigest,observation.Payload,issuance)),"application/json");
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
            bool keyOperation = intent.Operation is "wrap" or "unwrap" or "complete-key-use";
            var qualification = keyOperation ? verifier.CurrentQualification(current,budget.Token) : null;
            if (keyOperation && qualification is null) return Results.StatusCode(503);
            var prepared = new CustodyPreparedCall(JsonSerializer.Serialize(envelope, CustodyBindingContract.Json),
                CustodyBindingContract.CandidateRequestMessageBytes, qualification?.TransportProfileDigest ?? profile.Enrollment.ArtifactProfileDigest, qualification?.ClosureProfileDigest,workload);
            string id = installation.Role + ":" + intent.RequestAttemptId;
            if (method == "prepare")
            {
                if (installation.Role is not ("authorizer" or "control")) return Results.StatusCode(403);
                var request = JsonSerializer.SerializeToElement(new { intent, prepared }, CustodyBindingContract.Json);
                var admission = await originals.InvokeAsync<OriginalAdmission>("qualification-begin-original", new { role = installation.Role, requestId = id, original = request }, budget.Token);
                if(admission is not { Exact:true }) return Results.StatusCode(503);
                _=await Issue(prepared).ConfigureAwait(false);
                return Results.Bytes(CustodyJson.Bytes(prepared),"application/json");
            }
            if (authorization is null || !CustodyBindingContract.Digest(authorization.RequestSha256) || authorization.Prepared != prepared)
                return Results.StatusCode(403);
            var retained = await originals.InvokeAsync<IndependentOriginal>("qualification-lookup-original", new { role = installation.Role, requestId = id }, budget.Token);
            if(retained is null || retained.Request.EnumerateObject().Count()!=2
                || retained.Request.GetProperty("intent").Deserialize<CustodyCallIntent>(CustodyBindingContract.Json)!=intent
                || retained.Request.GetProperty("prepared").Deserialize<CustodyPreparedCall>(CustodyBindingContract.Json)!=prepared)
                return Results.StatusCode(503);
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
            if (installation.Role == "authorizer")
            {
                var permission = new OperationPermission(intent.Operation, intent.OperationId, intent.RequestAttemptId, intent.Challenge, intent.OwnerPurposeScope, profile.ProfileDigest);
                // Sign only after every awaited control/native/original read and current issuance admission.

                if (control.ControlStatement is null || !verifier.Verify(control.ControlStatement, "control", current, proofHash, TimeSpan.FromSeconds(30)))
                    return Results.StatusCode(503);
                metadata["custody-control-proof"] = Convert.ToBase64String(CustodyJson.Bytes(control.ControlStatement));
                if (keyOperation)
                {
                    var observed = await originals.InvokeAsync<SignedStatement>("custody-native-observation-original", originalScope, budget.Token);
                    if (observed is null || !verifier.Verify(observed, "independent-observer", current,
                        CustodyJson.Hash(CustodyJson.Bytes(observed.Payload)), TimeSpan.FromSeconds(30))) return Results.StatusCode(503);
                    if (intent.Operation is "wrap" or "unwrap")
                    {
                        var admission = CustodyJson.Read<KeyAdmission>(CustodyJson.Bytes(observed.Payload));
                        var suppliedUse = publicBody.RootElement.GetProperty("KeyUse").Deserialize<CustodyBindingKeyUseV1>(CustodyBindingContract.Json);
                        if (admission.RequestDigest != intent.RequestDigest || admission.QualifiedProfileDigest != profile.ProfileDigest
                            || admission.KeyUse != suppliedUse) return Results.StatusCode(503);
                    }
                    else if (CustodyJson.Read<KeyCompletion>(CustodyJson.Bytes(observed.Payload)).KeyUse
                        != publicBody.RootElement.GetProperty("KeyUse").Deserialize<CustodyBindingKeyUseV1>(CustodyBindingContract.Json)) return Results.StatusCode(503);
                    metadata["custody-key-admission"] = Convert.ToBase64String(CustodyJson.Bytes(observed));
                }
            }
            else if(installation.Role!="control") return Results.StatusCode(403);
            var finalIssuance=await Issue(new { authorization.RequestSha256,prepared,metadata }).ConfigureAwait(false);
            if(installation.Role=="authorizer") metadata["custody-operation-proof"]=Convert.ToBase64String(CustodyJson.Bytes(Sign("private-operation",current,authorization.RequestSha256,
                new OperationPermission(intent.Operation,intent.OperationId,intent.RequestAttemptId,intent.Challenge,intent.OwnerPurposeScope,profile.ProfileDigest),finalIssuance)));
            else metadata["custody-control-proof"]=Convert.ToBase64String(CustodyJson.Bytes(Sign("control",current,proofHash,facts,finalIssuance)));
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
    private SignedStatement Sign<T>(string purpose, CurrentState current, string subject, T payload,CustodyRoleIssuance issuance)
    {
        string requiredPurpose = installation.Role switch { "authorizer" => "private-operation", "observer" => "independent-observer", _ => installation.Role };
        if (purpose != requiredPurpose || !profile.Trust.Any(t => t.KeyId == installation.SignerId && t.Purpose == purpose))
            throw new AuthorityUnavailableException("Signing role substitution denied.");
        var now=clock.GetUtcNow();if(issuance.Grant.ValidUntil<=now) throw new AuthorityUnavailableException("Workload expired before signing.");
        var signed = new SignedStatement(purpose, installation.SignerId, profile.Enrollment.AuthorityId, profile.Enrollment.AuthorityIncarnation,
            current.Checkpoint.TenantId, current.Head.Revision, current.Head.Digest, subject,now,issuance.Grant.ValidUntil<now.AddSeconds(30)?issuance.Grant.ValidUntil:now.AddSeconds(30),
            JsonSerializer.SerializeToElement(payload, CustodyBindingContract.Json), [],issuance);
        lock (signer) return signed with { Signature = signer.SignData(CustodyJson.Bytes(signed), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence) };
    }
    private sealed record Permission(bool Allowed);
    private sealed record OriginalAdmission(bool Exact, bool First);
    private sealed record AuthorizationRequest(CustodyCallIntent Intent, CustodyPreparedCall Prepared, string RequestSha256);
}
