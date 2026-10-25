using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Externally provisioned current caller permission, retained with the authority head.</summary>
public sealed record IndependentCallerGrant(string BearerSha256, string Identity, string ScopeReference,
    string Role, string[] Methods, DateTimeOffset ValidUntil, bool Revoked);
/// <summary>Durable exact original, including uncertain executions. No request identity is recycled.</summary>
public sealed record IndependentOriginal(JsonElement Request, JsonElement? Result);
/// <summary>Externally enrolled independent authority genesis/current state. Startup never creates this document.</summary>
public sealed record IndependentAuthorityState(string EnrollmentId, string Scope, string TrustRevision,
    long Revision, DateTimeOffset ValidUntil, IndependentCallerGrant[] Callers,
    ReplicatedSecuritySpoolTarget? Spool, long SpoolRevision, string SpoolDigest,
    Dictionary<string, AnchoredStateTransition> Admitted, Dictionary<string, AnchoredStateTransition> Recorded,
    Dictionary<string, SecurityObservationRecoveryGrant> Recoveries, Dictionary<string, IndependentOriginal> Originals,
    Dictionary<string, CustodyKeyRegistration> Registrations, Dictionary<string, DeletionBatchConsumptionRequest> Manifests);
/// <summary>Explicit independently administered service/store and signing installation. It is not enrollment authority.</summary>
public sealed record IndependentAuthorityReceiverInstallation(string EnrollmentId, string Scope, string TrustRevision,
    string Component, string StateKey, string SigningKeyFile, string PublicKeyPem, string AppChannelTokenReference);

/// <summary>Receiving durable authority. All permission, anchor, original and recovery transitions share one conditional owner head.</summary>
public sealed class IndependentAuthorityReceiver(DaprClient dapr, IndependentAuthorityReceiverInstallation installation, ECDsa signer, TimeProvider clock)
{
    /// <summary>Authenticates channel/workload, reads the surviving enrolled head, commits exact transitions and signs bounded current results.</summary>
    public async Task<IResult> HandleAsync(HttpContext context, string method, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); budget.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            if (!await ChannelAsync(context, budget.Token).ConfigureAwait(false)) return Results.StatusCode(403);
            var original = await QualificationHost.ReadRequestAsync(context.Request.Body, budget.Token).ConfigureAwait(false);
            if (original is null) return Results.StatusCode(400);
            byte[] wire = Encoding.UTF8.GetBytes(original.ToJsonString());
            // Preserve the original parsed property order/encoding by using the identical closed typed serialization as the client.
            var request = JsonSerializer.Deserialize<Request>(wire, CustodyBindingContract.Json);
            if (request is null || request.Protocol != "platform-independent-authority-v1" || request.EnrollmentId != installation.EnrollmentId
                || request.Scope != installation.Scope || request.TrustRevision != installation.TrustRevision || request.Method != method
                || request.Nonce.Length != 32 || !request.Nonce.All(char.IsAsciiHexDigit)) return Results.StatusCode(403);
            wire = JsonSerializer.SerializeToUtf8Bytes(request, CustodyBindingContract.Json);
            var (state, etag) = await dapr.GetStateAndETagAsync<IndependentAuthorityState>(installation.Component, installation.StateKey,
                ConsistencyMode.Strong, cancellationToken: budget.Token).ConfigureAwait(false);
            if (state is null || string.IsNullOrWhiteSpace(etag) || state.EnrollmentId != installation.EnrollmentId || state.Scope != installation.Scope
                || state.TrustRevision != installation.TrustRevision || state.ValidUntil <= clock.GetUtcNow()) return Results.StatusCode(503);
            var grant = Authenticate(context, state, method);
            if (grant is null) return Results.StatusCode(403);
            var next = JsonSerializer.Deserialize<IndependentAuthorityState>(JsonSerializer.SerializeToUtf8Bytes(state, CustodyBindingContract.Json), CustodyBindingContract.Json)!;
            var outcome = Apply(method, request.Payload, grant, next, replacement => next = replacement);
            if (outcome.Mutated)
            {
                next = next with { Revision = checked(next.Revision + 1) };
                if (JsonSerializer.SerializeToUtf8Bytes(next, CustodyBindingContract.Json).Length > 32 * 1024 * 1024)
                    return Results.StatusCode(503); // Retained facts are never evicted to admit work.
                if (grant.ValidUntil <= clock.GetUtcNow() || next.ValidUntil <= clock.GetUtcNow()
                    || !await dapr.TrySaveStateAsync(installation.Component, installation.StateKey, next, etag,
                        new StateOptions { Concurrency = ConcurrencyMode.FirstWrite, Consistency = ConsistencyMode.Strong }, cancellationToken: budget.Token).ConfigureAwait(false))
                    return Results.StatusCode(503); // No retry after unknown CAS acknowledgement.
            }
            long now = clock.GetUtcNow().ToUnixTimeSeconds();
            if (grant.ValidUntil <= clock.GetUtcNow()) return Results.StatusCode(403);
            var result = new Response(installation.EnrollmentId, installation.Scope, installation.TrustRevision, method, request.Nonce,
                Convert.ToHexString(SHA256.HashData(wire)), now, Math.Min(now + 30, state.ValidUntil.ToUnixTimeSeconds()),
                JsonSerializer.SerializeToElement(outcome.Payload, CustodyBindingContract.Json), []);
            lock (signer) result = result with { Signature = signer.SignData(JsonSerializer.SerializeToUtf8Bytes(result, CustodyBindingContract.Json), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence) };
            byte[] resultBytes = JsonSerializer.SerializeToUtf8Bytes(result, CustodyBindingContract.Json);
            return resultBytes.Length >= CustodyBindingContract.MaximumPageFrameBytes ? Results.StatusCode(503) : Results.Bytes(resultBytes, "application/json");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Results.StatusCode(503); }
    }

    private (object? Payload, bool Mutated) Apply(string method, JsonElement payload, IndependentCallerGrant caller, IndependentAuthorityState state, Action<IndependentAuthorityState> replace)
    {
        T Read<T>() => payload.Deserialize<T>(CustodyBindingContract.Json) ?? throw new InvalidOperationException("Closed request required.");
        object Decision(bool value) => new { Allowed = value };
        bool Target(ReplicatedSecuritySpoolTarget target) => target == state.Spool && target.ValidUntil > clock.GetUtcNow();
        switch (method)
        {
            case "spool-current": return (state.Spool is { } current && Target(current) ? current : null, false);
            case "spool-authorize":
                var auth = Read<SpoolAuthorization>();
                return (Decision(Target(auth.target) && auth.method is "Observe" or "Lookup" or "Drain" or "Readiness" or "RecoverOriginal"
                    && (auth.method is "Drain" or "Readiness" || auth.intent is not null)), false);
            case "spool-authorize-original": return (Decision(Target(Read<OriginalLookup>().target)), false);
            case "spool-validate":
                var validation = Read<StateValidation>();
                return (Decision(Target(validation.target) && validation.revision == state.SpoolRevision && validation.exactStateDigest == state.SpoolDigest), false);
            case "spool-admit-transition":
            case "spool-record-transition":
            case "spool-recover-transition":
            case "spool-verify-transition":
                var t = Read<AnchoredStateTransition>();
                string scope = state.Spool?.ComponentName + "|system/security-observations/" + state.Spool?.InstallationEpoch;
                bool exact = state.Admitted.TryGetValue(t.TargetDigest, out var admitted) && Same(admitted, t);
                bool recorded = state.Recorded.TryGetValue(t.TargetDigest, out var retained) && Same(retained, t);
                if (state.Spool is null || !Target(state.Spool) || t.ScopeId != scope || t.TargetRevision != t.ExpectedRevision + 1
                    || Convert.ToHexString(SHA256.HashData(t.TargetBytes)) != t.TargetDigest) return (Decision(false), false);
                if (method == "spool-verify-transition") return (Decision(recorded), false);
                if (recorded) return (Decision(true), false);
                if (t.ExpectedRevision != state.SpoolRevision || t.PredecessorDigest != state.SpoolDigest) return (Decision(false), false);
                if (method == "spool-admit-transition")
                {
                    if (state.Admitted.Count >= 10000 || state.Admitted.Values.Any(a => a.ExpectedRevision == t.ExpectedRevision && !Same(a, t))) return (Decision(false), false);
                    state.Admitted[t.TargetDigest] = t; return (Decision(true), !exact);
                }
                if (!exact) return (Decision(false), false);
                state.Recorded[t.TargetDigest] = t;
                // Records are immutable reference values; the receiver returns a replacement head through the mutation delegate below.
                replace(state with { SpoolRevision = t.TargetRevision, SpoolDigest = t.TargetDigest });
                return (Decision(true), true);
            case "spool-admit-recovery":
                var recovery = Read<RecoveryAdmission>();
                if (!Target(recovery.target) || recovery.original.Receipt is not null || recovery.proof.State == SecurityEventRecorderLookupState.Unknown
                    || recovery.proof.State == SecurityEventRecorderLookupState.Recorded != (recovery.proof.Receipt is not null)) return (null, false);
                var grant = new SecurityObservationRecoveryGrant(recovery.target, recovery.original, recovery.proof, state.TrustRevision,
                    clock.GetUtcNow(), new[] { clock.GetUtcNow().AddMinutes(5), recovery.target.ValidUntil, caller.ValidUntil }.Min());
                string recoveryId = Hash(grant); state.Recoveries[recoveryId] = grant; return (grant, true);
            case "spool-verify-recovery":
                var verify = Read<RecoveryVerification>();
                return (Decision(verify.grant.ValidUntil > clock.GetUtcNow() && Target(verify.grant.Target)
                    && state.Recoveries.TryGetValue(Hash(verify.grant), out var original) && Same(original, verify.grant)
                    && verify.currentProof.State != SecurityEventRecorderLookupState.Unknown
                    && (Same(verify.grant.OriginalProof, verify.currentProof)
                        || verify.grant.OriginalProof.State == SecurityEventRecorderLookupState.NotRecorded && verify.currentProof.State == SecurityEventRecorderLookupState.Recorded)), false);
            case "qualification-authenticate":
                var authentication = Read<CallerAuthentication>();
                var authenticated = state.Callers.SingleOrDefault(c => c.BearerSha256 == authentication.bearerSha256 && c.Role == authentication.role
                    && !c.Revoked && c.ValidUntil > clock.GetUtcNow());
                return (authenticated is null ? null : new QualificationCallerContext(authenticated.Identity, authenticated.ScopeReference), false);
            case "qualification-authorize":
                var authorization = Read<CallerAuthorization>();
                return (Decision(state.Callers.Any(c => c.Identity == authorization.caller.Identity && c.ScopeReference == authorization.caller.ScopeReference
                    && c.Role == authorization.role && !c.Revoked && c.ValidUntil > clock.GetUtcNow())
                    && authorization.request["targetCommit"] is not null && authorization.request["isolatedTargetId"] is not null), false);
            case "qualification-begin-original":
                var begin = Read<BeginOriginal>(); string id = begin.role + ":" + begin.requestId;
                if (state.Originals.TryGetValue(id, out var prior)) return (new { Exact = Same(prior.Request, begin.original), First = false }, false);
                if (state.Originals.Count >= 10000) throw new InvalidOperationException("Original capacity exhausted.");
                state.Originals.Add(id, new(begin.original.Clone(), null)); return (new { Exact = true, First = true }, true);
            case "qualification-retain-result":
                var result = Read<RetainResult>(); string resultId = result.role + ":" + result.requestId;
                if (!state.Originals.TryGetValue(resultId, out var retainedOriginal)) return (new { Exact = false, First = false }, false);
                if (retainedOriginal.Result is { } previous) return (new { Exact = Same(previous, result.result), First = false }, false);
                state.Originals[resultId] = retainedOriginal with { Result = result.result.Clone() }; return (new { Exact = true, First = true }, true);
            case "custody-registration-original":
                var identity = Read<RegistrationLookup>().identity;
                return (state.Registrations.Values.SingleOrDefault(r => r.Identity == identity), false);
            case "custody-reserved-manifest-original":
                return (state.Manifests.GetValueOrDefault(Read<ManifestLookup>().reservationReceiptId), false);
            default: throw new InvalidOperationException("Operation is not installed for this authority role.");
        }
    }
    private static bool Same<T, U>(T left, U right) => JsonSerializer.SerializeToUtf8Bytes(left, CustodyBindingContract.Json).AsSpan()
        .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(right, CustodyBindingContract.Json));
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, CustodyBindingContract.Json)));
    private IndependentCallerGrant? Authenticate(HttpContext context, IndependentAuthorityState state, string method)
    {
        var headers = context.Request.Headers.Authorization;
        if (headers.Count != 1 || headers[0] is not { Length: > 7 and <= 8199 } value || !value.StartsWith("Bearer ", StringComparison.Ordinal)) return null;
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value[7..])));
        return state.Callers.SingleOrDefault(c => c.BearerSha256 == hash && !c.Revoked && c.ValidUntil > clock.GetUtcNow() && c.Methods.Contains(method, StringComparer.Ordinal));
    }
    private async Task<bool> ChannelAsync(HttpContext context, CancellationToken token)
    {
        string reference = installation.AppChannelTokenReference;
        if (!reference.StartsWith("file:/", StringComparison.Ordinal)) return false;
        byte[] expected = await File.ReadAllBytesAsync(reference[5..], token).ConfigureAwait(false);
        var header = context.Request.Headers["dapr-api-token"];
        if (expected.Length is 0 or > 8192 || header.Count != 1 || header[0] is not { Length: > 0 and <= 8192 } actual) return false;
        byte[] supplied = Encoding.UTF8.GetBytes(actual);
        try { return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(expected).TrimEnd('\r', '\n')), supplied); }
        finally { CryptographicOperations.ZeroMemory(expected); CryptographicOperations.ZeroMemory(supplied); }
    }
    private sealed record Request(string Protocol, string EnrollmentId, string Scope, string TrustRevision, string Method, string Nonce, JsonElement Payload);
    private sealed record Response(string EnrollmentId, string Scope, string TrustRevision, string Method, string Nonce, string RequestSha256,
        long ObservedAt, long ValidUntil, JsonElement Payload, byte[] Signature);
    private sealed record SpoolAuthorization(ReplicatedSecuritySpoolTarget target, string method, SecurityObservationIntent? intent);
    private sealed record OriginalLookup(ReplicatedSecuritySpoolTarget target, SecurityObservationOriginalLookup lookup);
    private sealed record StateValidation(ReplicatedSecuritySpoolTarget target, long revision, string exactStateDigest);
    private sealed record RecoveryAdmission(ReplicatedSecuritySpoolTarget target, SecurityObservationRecord original, SecurityEventRecorderLookup proof);
    private sealed record RecoveryVerification(SecurityObservationRecoveryGrant grant, SecurityEventRecorderLookup currentProof);
    private sealed record CallerAuthentication(string role, string bearerSha256);
    private sealed record CallerAuthorization(string role, JsonObject request, QualificationCallerContext caller);
    private sealed record BeginOriginal(string role, string requestId, JsonElement original);
    private sealed record RetainResult(string role, string requestId, JsonElement result);
    private sealed record RegistrationLookup(CustodyKeyObjectIdentity identity);
    private sealed record ManifestLookup(string reservationReceiptId);
}
