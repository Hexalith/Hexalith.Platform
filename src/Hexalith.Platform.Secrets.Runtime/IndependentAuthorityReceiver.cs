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

/// <summary>Receiving durable authority. All permission, anchor, original and recovery transitions share one conditional owner head.</summary>
public sealed class IndependentAuthorityReceiver(DaprClient dapr, IndependentAuthorityReceiverInstallation installation, ECDsa signer, TimeProvider clock, IPrivateRecordingEventStore? recordingSource = null, IReadOnlyList<IIndependentOwnerOperationHandler>? ownerHandlers = null,IIndependentArchiveStorage? archiveStorage=null)
{
    private readonly IIndependentArchiveStorage _archive=archiveStorage??new IndependentArchiveStorage();
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
            if(method is "spool-current" or "spool-validate" or "spool-authorize")
            {
                bool admission=method=="spool-authorize" && request.Payload.Deserialize<SpoolAuthorization>(CustodyBindingContract.Json)!.method is "Observe" or "Readiness";
                await RequireArchiveAsync(admission,budget.Token).ConfigureAwait(false);
            }
            if (method is "spool-admit-recovery" or "spool-verify-recovery")
            {
                if (installation.RecorderAuthority is null) return Results.StatusCode(503);
                var recorder = new DaprSecurityObservationRecorder(new(new DaprSecretsInvoker(dapr), installation.RecorderAuthority, clock));
                SecurityObservationRecord originalRecord;
                SecurityEventRecorderLookup submitted;
                if (method == "spool-admit-recovery")
                {
                    var recovery = request.Payload.Deserialize<RecoveryAdmission>(CustodyBindingContract.Json)!;
                    originalRecord = recovery.original; submitted = recovery.proof;
                }
                else
                {
                    var recovery = request.Payload.Deserialize<RecoveryVerification>(CustodyBindingContract.Json)!;
                    originalRecord = recovery.grant.Original; submitted = recovery.currentProof;
                }
                var actual = await recorder.LookupAsync(originalRecord, budget.Token).ConfigureAwait(false);
                if (actual.State == SecurityEventRecorderLookupState.Unknown || !Same(actual, submitted)) return Results.StatusCode(503);
            }
            if(method=="recording-recovery-command-authorize")
            {
                if(recordingSource is null || installation.RecoverySpoolAuthority is null) return Results.StatusCode(503);
                var recovery=request.Payload.Deserialize<RecordingRecoveryAuthorization>(CustodyBindingContract.Json)!;
                var actual=await recordingSource.LookupAsync(recovery.grant.Original,budget.Token).ConfigureAwait(false);
                var recoveryAuthority=new DaprReplicatedSpoolAuthority(new(new DaprSecretsInvoker(dapr),installation.RecoverySpoolAuthority,clock));
                if(actual.State!=SecurityEventRecorderLookupState.NotRecorded || !await recoveryAuthority.VerifyAgedOriginalRecoveryAsync(recovery.grant,actual,budget.Token).ConfigureAwait(false))
                    return Results.StatusCode(403);
            }
            var next = JsonSerializer.Deserialize<IndependentAuthorityState>(JsonSerializer.SerializeToUtf8Bytes(state, CustodyBindingContract.Json), CustodyBindingContract.Json)!;
            (object? Payload, bool Mutated) outcome;
            if (method is "replay-register-exact" or "replay-lookup-exact" or "security-observation-lookup" or "security-observation-append" or "security-observation-recover-original")
            {
                if (recordingSource is null) return Results.StatusCode(503);
                object? recordingResult;
                if (method.StartsWith("replay-", StringComparison.Ordinal))
                {
                    if (grant.Role != "replay-registrar") return Results.StatusCode(403);
                    if (method == "replay-register-exact")
                    {
                        var replay = request.Payload.Deserialize<ReplayAdmission>(CustodyBindingContract.Json)!;
                        recordingResult = await recordingSource.RegisterAsync(replay.intent, replay.evaluatedAt, TimeSpan.FromTicks(replay.retentionTicks), budget.Token);
                    }
                    else recordingResult = await recordingSource.LookupAsync(request.Payload.Deserialize<ReplayLookup>(CustodyBindingContract.Json)!.intent, budget.Token);
                }
                else
                {
                    if (grant.Role is not ("security-recorder" or "aged-recovery")) return Results.StatusCode(403);
                    SecurityObservationRecord originalRecord;
                    if (method == "security-observation-recover-original")
                    {
                        if (grant.Role != "aged-recovery") return Results.StatusCode(403);
                        var recovery = request.Payload.Deserialize<RecorderRecovery>(CustodyBindingContract.Json)!;
                        originalRecord = recovery.original;
                        await recordingSource.RecoverOriginalAsync(recovery.original, recovery.grant, budget.Token);
                    }
                    else
                    {
                        originalRecord = request.Payload.Deserialize<RecorderOriginal>(CustodyBindingContract.Json)!.record;
                        if (method == "security-observation-append") await recordingSource.AppendAsync(originalRecord, budget.Token);
                    }
                    var lookup = await recordingSource.LookupAsync(originalRecord, budget.Token);
                    recordingResult = new { OriginalSha256 = Hash(originalRecord), Lookup = lookup };
                }
                outcome = (recordingResult, false);
            }
            else if(method is "spool-admit-recovery" or "spool-verify-recovery")
                outcome=await ApplyRecoveryAsync(method,request.Payload,grant,state,next,budget.Token).ConfigureAwait(false);
            else if (method is "spool-admit-transition" or "spool-record-transition" or "spool-recover-transition" or "spool-verify-transition")
                outcome = await ApplySpoolTransitionAsync(method, request.Payload.Deserialize<AnchoredStateTransition>(CustodyBindingContract.Json)!, next, replacement => next = replacement, budget.Token).ConfigureAwait(false);
            else if (ownerHandlers?.SingleOrDefault(handler => handler.Methods.Contains(method)) is { } handler)
                outcome = (await handler.HandleAsync(method, request.Payload, grant, state, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false), false);
            else outcome = Apply(method, request.Payload, grant, next, replacement => next = replacement);
            if (outcome.Mutated)
            {
                next = next with { Revision = checked(next.Revision + 1) };
                bool admission=method is "spool-admit-transition" or "spool-admit-recovery" or "qualification-begin-original";
                long headBound=32L*1024*1024-(admission?installation.CompletionReserveBytes:0);
                if (headBound<=0 || JsonSerializer.SerializeToUtf8Bytes(next, CustodyBindingContract.Json).LongLength > headBound)
                    return Results.StatusCode(503); // Retained facts are never evicted to admit work.
                if (grant.ValidUntil <= clock.GetUtcNow() || next.ValidUntil <= clock.GetUtcNow()
                    || !await dapr.TrySaveStateAsync(installation.Component, installation.StateKey, next, etag,
                        new StateOptions { Concurrency = ConcurrencyMode.FirstWrite, Consistency = ConsistencyMode.Strong }, cancellationToken: budget.Token).ConfigureAwait(false))
                    return Results.StatusCode(503); // No retry after unknown CAS acknowledgement.
            }
            if (method is "spool-record-transition" or "spool-recover-transition"
                && outcome.Payload is Permission { Allowed: true })
            {
                var completed = request.Payload.Deserialize<AnchoredStateTransition>(CustodyBindingContract.Json)!;
                await WriteSpoolOriginalAsync("recorded", completed, false, budget.Token).ConfigureAwait(false);
            }
            if(method=="spool-admit-recovery" && outcome.Payload is SecurityObservationRecoveryGrant issued)
                await new IndependentRecoveryGrantArchive(CurrentArchiveDirectory(),installation,signer).PublishAsync(issued,next.Revision,budget.Token).ConfigureAwait(false);
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
            case "recording-dispatch-admit":
                var dispatch = Read<RecordingDispatch>();
                string dispatchId = dispatch.role + ":" + dispatch.requestId;
                if (caller.Role != dispatch.role || !state.Originals.TryGetValue(dispatchId, out var dispatchOriginal)
                    || !Same(dispatchOriginal.Request, dispatch.original) || dispatchOriginal.SubmissionState != "Unsent")
                    return (Decision(false), false);
                state.Originals[dispatchId] = dispatchOriginal with { SubmissionState = "Dispatched" };
                return (Decision(true), true);
            case "fr34-command-authorize":
                var canary = Read<RecordingCommandAuthorization>();
                var canaryCommand = canary.command;
                bool canaryExact = state.Originals.TryGetValue("fr34-engine:" + canaryCommand.MessageId, out var canaryOriginal)
                    && Same(canaryOriginal.Request, canaryCommand.Payload);
                var content = canaryCommand.Payload.GetProperty("protectedContent");
                var canaryTarget = content.GetProperty("target");
                return (Decision(state.Fr34Target is { } fr34 && canaryExact && canaryOriginal!.SubmissionState == "Unsent"
                    && canaryCommand.Domain == "fr34-canary" && canaryCommand.Tenant == fr34.TenantId
                    && canaryCommand.CommandType == "Hexalith.Platform.Protection.PersistFr34Canary"
                    && canaryTarget.GetProperty("tenantId").GetString() == fr34.TenantId
                    && canaryTarget.GetProperty("agentInteractionId").GetString() == canaryCommand.AggregateId
                    && canaryTarget.GetProperty("targetProtectionKeyAlias").GetString() == "fr34-" + canaryCommand.AggregateId
                    && state.Callers.Any(c => c.Identity == canary.caller && c.ScopeReference == canary.principal && !c.Revoked
                        && c.ValidUntil > clock.GetUtcNow() && c.Methods.Contains("recording-source:fr34-canary",StringComparer.Ordinal))),false);
            case "recording-recovery-command-authorize":
                var aged=Read<RecordingRecoveryAuthorization>();
                var r=aged.grant.Original;
                var exactRecoveryPayload=JsonSerializer.SerializeToElement(new { securityObservationId=r.Intent.ObservationId,routingTenantId=r.Intent.RoutingTenantId,
                    utcDay=r.UtcDay,observedAt=r.ObservedAt,reasonCode=r.Intent.ReasonCode,untrustedFieldsHmac=r.Intent.UntrustedFieldsHmac,digestKeyVersion=r.Intent.DigestKeyVersion });
                return (Decision(aged.command.Domain=="security-event-log" && aged.grant.ValidUntil>clock.GetUtcNow() && Same(aged.command.Payload,exactRecoveryPayload)
                    && state.Originals.TryGetValue("private-security-observation:"+aged.command.MessageId,out var agedOriginal) && Same(agedOriginal.Request,aged.command.Payload)
                    && state.Callers.Any(c=>c.Identity==aged.caller && c.ScopeReference==aged.principal && !c.Revoked && c.ValidUntil>clock.GetUtcNow()
                        && c.Methods.Contains("recording-source:security-event-log",StringComparer.Ordinal))),false);
            case "recording-command-authorize":
                var recording = Read<RecordingCommandAuthorization>();
                string recordingRole = recording.command.Domain == "trusted-envelope-replay" ? "private-replay" : "private-security-observation";
                bool exactOriginal = state.Originals.TryGetValue(recordingRole + ":" + recording.command.MessageId, out var sourceOriginal)
                    && sourceOriginal.SubmissionState=="Unsent" && Same(sourceOriginal.Request, recording.command.Payload);
                return (Decision(exactOriginal && state.Callers.Any(c => c.Identity == recording.caller && c.ScopeReference == recording.principal
                    && !c.Revoked && c.ValidUntil > clock.GetUtcNow() && c.Methods.Contains("recording-source:" + recording.command.Domain, StringComparer.Ordinal))), false);
            case "custody-control-original":
            case "custody-native-observation-original":
                var custodyOriginal = Read<CustodyOriginalLookup>();
                string originalRole = method == "custody-control-original" ? "control-original" : "native-observation-original";
                string originalId = Hash(custodyOriginal);
                if (caller.ScopeReference != custodyOriginal.tenant + "|" + custodyOriginal.scope) return (null, false);
                return (state.Originals.TryGetValue(originalRole + ":" + originalId, out var custodyRetained) ? custodyRetained.Request : null, false);
            case "custody-role-authorize":
                var roleRequest = Read<CustodyRoleAuthorization>();
                return (Decision(state.Callers.Any(c => c.BearerSha256 == roleRequest.bearerSha256 && !c.Revoked && c.ValidUntil > clock.GetUtcNow()
                    && c.Role == roleRequest.role && c.ScopeReference == roleRequest.intent.TenantId + "|" + roleRequest.intent.OwnerPurposeScope
                    && c.Methods.Contains("custody-intent:" + roleRequest.intent.Operation, StringComparer.Ordinal))), false);
            case "qualification-lookup-original":
                var originalLookup = Read<LookupOriginal>();
                return (originalLookup.role == caller.Role || caller.Methods.Contains("qualification-read-original:"+originalLookup.role,StringComparer.Ordinal)
                    ? state.Originals.GetValueOrDefault(originalLookup.role + ":" + originalLookup.requestId) : null, false);
            case "qualification-authenticate":
                var authentication = Read<CallerAuthentication>();
                var authenticated = state.Callers.SingleOrDefault(c => c.BearerSha256 == authentication.bearerSha256 && c.Role == authentication.role
                    && !c.Revoked && c.ValidUntil > clock.GetUtcNow());
                return (authenticated is null ? null : new QualificationCallerContext(authenticated.Identity, authenticated.ScopeReference), false);
            case "qualification-authorize":
                var authorization = Read<CallerAuthorization>();
                return (Decision(state.Callers.Any(c => c.Identity == authorization.caller.Identity && c.ScopeReference == authorization.caller.ScopeReference
                    && c.Role == authorization.role && !c.Revoked && c.ValidUntil > clock.GetUtcNow())
                    && state.QualificationBindings is { Count: >= 3 }
                    && new[] { "targetCommit", "isolatedTargetId", "planSha256" }.All(state.QualificationBindings.ContainsKey)
                    && state.QualificationBindings.All(binding => authorization.request[binding.Key]?.ToJsonString() == binding.Value)
                    && (authorization.role == "enrollment" || ExactCase(authorization.request, state))), false);
            case "qualification-begin-original":
                var begin = Read<BeginOriginal>(); string id = begin.role + ":" + begin.requestId;
                if (begin.role != caller.Role) return (new { Exact = false, First = false }, false);
                if (state.Originals.TryGetValue(id, out var prior)) return (new { Exact = Same(prior.Request, begin.original), First = false }, false);
                if (state.Originals.Count >= 10000) throw new InvalidOperationException("Original capacity exhausted.");
                state.Originals.Add(id, new(begin.original.Clone(), null, RetainedRevision:checked(state.Revision+1))); return (new { Exact = true, First = true }, true);
            case "qualification-retain-result":
                var result = Read<RetainResult>(); string resultId = result.role + ":" + result.requestId;
                if (result.role != caller.Role) return (new { Exact = false, First = false }, false);
                if (!state.Originals.TryGetValue(resultId, out var retainedOriginal)) return (new { Exact = false, First = false }, false);
                if (retainedOriginal.Result is { } previous) return (new { Exact = Same(previous, result.result), First = false }, false);
                state.Originals[resultId] = retainedOriginal with { Result = result.result.Clone(), ResultRetainedRevision=checked(state.Revision+1) }; return (new { Exact = true, First = true }, true);
            case "custody-registration-original":
                var identity = Read<RegistrationLookup>().identity;
                return (caller.ScopeReference == identity.TenantId ? state.Registrations.Values.SingleOrDefault(r => r.Identity == identity) : null, false);
            case "custody-reserved-manifest-original":
                var foundManifest = state.Manifests.GetValueOrDefault(Read<ManifestLookup>().reservationReceiptId);
                return (foundManifest?.Capability.TenantId == caller.ScopeReference ? foundManifest : null, false);
            default: throw new InvalidOperationException("Operation is not installed for this authority role.");
        }
    }
    private async Task<(object? Payload, bool Mutated)> ApplySpoolTransitionAsync(string method, AnchoredStateTransition transition,
        IndependentAuthorityState state, Action<IndependentAuthorityState> replace, CancellationToken token)
    {
        object Decision(bool allowed) => new Permission(allowed);
        string scope = state.Spool?.ComponentName + "|system/security-observations/" + state.Spool?.InstallationEpoch;
        if (state.Spool is null || state.Spool.ValidUntil <= clock.GetUtcNow() || transition.ScopeId != scope
            || transition.TargetRevision != transition.ExpectedRevision + 1 || transition.TargetBytes.Length > 32 * 1024 * 1024
            || Convert.ToHexString(SHA256.HashData(transition.TargetBytes)) != transition.TargetDigest)
            return (Decision(false), false);
        // Legacy snapshots must be migrated by the independent archive owner, never silently evicted.
        if (state.Admitted.Count != 0 || state.Recorded.Count != 0) throw new InvalidOperationException("Independent spool archive migration required.");
        bool recorded = await HasSpoolOriginalAsync("recorded", transition, token).ConfigureAwait(false);
        if (method == "spool-verify-transition") return (Decision(recorded && state.SpoolRevision >= transition.TargetRevision), false);
        if (recorded) return (Decision(true), false);
        bool admitted = await HasSpoolOriginalAsync("admitted", transition, token).ConfigureAwait(false);
        if (method != "spool-admit-transition" && admitted && state.SpoolRevision == transition.TargetRevision && state.SpoolDigest == transition.TargetDigest)
            return (Decision(true), false); // Lost CAS acknowledgement: publish this exact completion, never a new transition.
        if (state.SpoolRevision != transition.ExpectedRevision || state.SpoolDigest != transition.PredecessorDigest
            || state.PendingSpoolTransitionDigest is { } pending && pending != transition.TargetDigest) return (Decision(false), false);
        if (method == "spool-admit-transition")
        {
            await WriteSpoolOriginalAsync("admitted", transition, true, token).ConfigureAwait(false);
            replace(state with { PendingSpoolTransitionDigest = transition.TargetDigest });
            return (Decision(true), state.PendingSpoolTransitionDigest != transition.TargetDigest);
        }
        if (!admitted || state.PendingSpoolTransitionDigest != transition.TargetDigest) return (Decision(false), false);
        replace(state with { SpoolRevision = transition.TargetRevision, SpoolDigest = transition.TargetDigest, PendingSpoolTransitionDigest = null });
        return (Decision(true), true);
    }
    private async Task<(object? Payload,bool Mutated)> ApplyRecoveryAsync(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState current,IndependentAuthorityState next,CancellationToken token)
    {
        await RequireArchiveAsync(false,token).ConfigureAwait(false);
        var archive=new IndependentRecoveryGrantArchive(CurrentArchiveDirectory(),installation,signer);
        bool Target(ReplicatedSecuritySpoolTarget target)=>target==current.Spool && target.ValidUntil>clock.GetUtcNow();
        // Migrate only actually retained authorizations into immutable signed originals before replacing the bounded current head.
        foreach(var retained in current.Recoveries.Values) await archive.PublishAsync(retained,current.Revision,token).ConfigureAwait(false);
        if(method=="spool-admit-recovery")
        {
            var request=payload.Deserialize<RecoveryAdmission>(CustodyBindingContract.Json)!;
            if(!Target(request.target) || request.original.Receipt is not null || request.proof.State==SecurityEventRecorderLookupState.Unknown
                || (request.proof.State==SecurityEventRecorderLookupState.Recorded)!=(request.proof.Receipt is not null)) return (null,false);
            var prior=current.Recoveries.Values.SingleOrDefault(value=>value.Target==request.target && Same(value.Original,request.original)
                && Same(value.OriginalProof,request.proof) && value.RecoveryAuthorityRevision==current.TrustRevision && value.ValidUntil>clock.GetUtcNow());
            var issued=prior??new SecurityObservationRecoveryGrant(request.target,request.original,request.proof,current.TrustRevision,clock.GetUtcNow(),
                new[]{clock.GetUtcNow().AddMinutes(5),request.target.ValidUntil,caller.ValidUntil}.Min());
            next.Recoveries.Clear();next.Recoveries.Add(Hash(new { issued.Target,issued.Original }),issued);
            return (issued,true); // Even reissue crosses the shared current-role CAS barrier; the original grant time is unchanged.
        }
        var verify=payload.Deserialize<RecoveryVerification>(CustodyBindingContract.Json)!;
        bool admitted=verify.grant.ValidUntil>clock.GetUtcNow() && verify.grant.RecoveryAuthorityRevision==current.TrustRevision && Target(verify.grant.Target)
            && await archive.ContainsAsync(verify.grant,current.Revision,token).ConfigureAwait(false)
            && verify.currentProof.State!=SecurityEventRecorderLookupState.Unknown && (Same(verify.grant.OriginalProof,verify.currentProof)
                || verify.grant.OriginalProof.State==SecurityEventRecorderLookupState.NotRecorded && verify.currentProof.State==SecurityEventRecorderLookupState.Recorded);
        if(admitted && verify.currentProof.State==SecurityEventRecorderLookupState.Recorded)
            foreach(string key in next.Recoveries.Where(pair=>Same(pair.Value.Original,verify.grant.Original) && pair.Value.Target==verify.grant.Target).Select(pair=>pair.Key).ToArray()) next.Recoveries.Remove(key);
        return (new Permission(admitted),admitted); // Successful verification is ordered atomically with role revocation, including an archive-only grant.
    }
    private string SpoolOriginalPath(string category, AnchoredStateTransition transition)
    {
        if(!CustodyBindingContract.Digest(transition.TargetDigest)) throw new InvalidOperationException("Exact immutable transition digest required.");
        return Path.Combine(CurrentArchiveDirectory(), "spool", category, Hash(transition.ScopeId), transition.TargetDigest);
    }
    private string CurrentArchiveDirectory()
    {
        string? directory = installation.OriginalArchiveDirectory;
        if (directory is null || !Path.IsPathFullyQualified(directory) || installation.CompletionReserveBytes <= 0
            || installation.ArchiveEnrollmentSha256 is not { Length: 64 } enrollmentHash
            || new DirectoryInfo(directory).LinkTarget is not null)
            throw new InvalidOperationException("Independent immutable spool archive and completion reserve required.");
        var enrollment = new FileInfo(Path.Combine(directory, "enrollment.json"));
        if (!enrollment.Exists || enrollment.LinkTarget is not null || enrollment.Length is <= 0 or > 65536
            || Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(enrollment.FullName))) != enrollmentHash)
            throw new InvalidOperationException("Current immutable spool archive enrollment required.");
        return directory;
    }
    private async Task<bool> HasSpoolOriginalAsync(string category, AnchoredStateTransition transition, CancellationToken token)
    {
        string path = SpoolOriginalPath(category, transition); var info = new FileInfo(path);
        if (!info.Exists) return false;
        if (info.LinkTarget is not null || info.Length is <= 0 or > CustodyBindingContract.MaximumOriginalBytes) throw new InvalidOperationException("Exact immutable transition required.");
        return (await File.ReadAllBytesAsync(path, token).ConfigureAwait(false)).AsSpan()
            .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(transition, CustodyBindingContract.Json));
    }
    private async Task WriteSpoolOriginalAsync(string category, AnchoredStateTransition transition, bool admission, CancellationToken token)
    {
        string path = SpoolOriginalPath(category, transition);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(transition, CustodyBindingContract.Json);
        if (admission && _archive.AvailableBytes(installation.OriginalArchiveDirectory!) < checked(bytes.LongLength * 2 + installation.CompletionReserveBytes))
            throw new InvalidOperationException("Spool completion reserve unavailable; ingress denied.");
        await ImmutableFileArchive.PublishAsync(path, bytes, token).ConfigureAwait(false);
    }
    private async Task RequireArchiveAsync(bool admission,CancellationToken token)
    {
        // Reuse exact enrollment/path validation before a settled spool may report readiness or admit new ingress.
        string directory=CurrentArchiveDirectory();
        long required=admission?checked(2L*CustodyBindingContract.MaximumOriginalBytes+installation.CompletionReserveBytes):1;
        if(_archive.AvailableBytes(directory)<required) throw new IOException("Archive admission or completion capacity unavailable.");
        await _archive.ProbeWritableAsync(directory,token).ConfigureAwait(false);
    }
    private sealed record Permission(bool Allowed);
    private static bool ExactCase(JsonObject request, IndependentAuthorityState state)
    {
        string? id = SecretsProof.Text(request, "caseId");
        if (id is null || state.QualificationCases is null || !state.QualificationCases.TryGetValue(id, out var expected)) return false;
        return new[] { "caseId", "caseTargetId", "enrolledScope" }.All(name => expected.TryGetProperty(name, out var value)
            && request[name] is { } supplied && JsonNode.DeepEquals(JsonNode.Parse(value.GetRawText()), supplied));
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
        string? expected = await DaprSecretsInvoker.ReadMountedBearerAsync(reference, token).ConfigureAwait(false);
        var header = context.Request.Headers["dapr-api-token"];
        if (expected is null || header.Count != 1 || header[0] is not { Length: > 0 and <= 8192 } actual) return false;
        byte[] supplied = Encoding.UTF8.GetBytes(actual); byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        try { return CryptographicOperations.FixedTimeEquals(expectedBytes, supplied); }
        finally { CryptographicOperations.ZeroMemory(expectedBytes); CryptographicOperations.ZeroMemory(supplied); }
    }

    private sealed record RecorderOriginal(SecurityObservationRecord record);
    private sealed record RecorderRecovery(SecurityObservationRecord original, SecurityObservationRecoveryGrant grant);
    private sealed record ReplayAdmission(TrustedEnvelopeReplayIntent intent, DateTimeOffset evaluatedAt, long retentionTicks);
    private sealed record ReplayLookup(TrustedEnvelopeReplayIntent intent);
    private sealed record CustodyOriginalLookup(string tenant, string scope, string operationId, long revision, string digest);
    private sealed record RecordingRecoveryAuthorization(string caller,string principal,Hexalith.EventStore.Contracts.Commands.SubmitCommandRequest command,SecurityObservationRecoveryGrant grant);
    private sealed record RecordingDispatch(string role, string requestId, JsonElement original);
    private sealed record RecordingCommandAuthorization(string caller, string principal, Hexalith.EventStore.Contracts.Commands.SubmitCommandRequest command);
    private sealed record CustodyRoleAuthorization(string role, CustodyCallIntent intent, string bearerSha256);
    private sealed record LookupOriginal(string role, string requestId);
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
