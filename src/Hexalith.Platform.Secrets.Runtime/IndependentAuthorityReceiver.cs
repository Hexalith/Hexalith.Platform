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
public sealed class IndependentAuthorityReceiver(DaprClient dapr, IndependentAuthorityReceiverInstallation installation, ECDsa signer, TimeProvider clock, IPrivateRecordingEventStore? recordingSource = null, IReadOnlyList<IIndependentOwnerOperationHandler>? ownerHandlers = null,IIndependentArchiveStorage? archiveStorage=null,IIndependentContentArchive? privateOriginalArchive=null)
{
    private readonly IIndependentArchiveStorage _archive=archiveStorage??new IndependentArchiveStorage();
    /// <summary>Authenticates channel/workload, reads the surviving enrolled head, commits exact transitions and signs bounded current results.</summary>
    public async Task<IResult> HandleAsync(HttpContext context, string method, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); budget.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            if (!await ChannelAsync(context, budget.Token).ConfigureAwait(false)) return Results.StatusCode(403);
            string? body=await DaprSecretsInvoker.ReadBoundedAsync(context.Request.Body,budget.Token,CustodyBindingContract.MaximumIndependentAuthorityFrameBytes-1).ConfigureAwait(false);
            if(body is null) return Results.StatusCode(400);
            byte[] wire=Encoding.UTF8.GetBytes(body);CustodyBindingContract.RejectDuplicateProperties(wire);
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
            if(method is "qualification-begin-original" or "qualification-retain-result" && request.Payload.TryGetProperty("role",out var privateRole) && PrivateRole(privateRole.GetString()))
            {
                var publication=await PreparePrivatePublicationAsync(method,request.Payload,grant,state,etag,budget.Token).ConfigureAwait(false);
                if(!publication.Ready) return Results.StatusCode(503);
                state=publication.State;etag=publication.ETag;grant=Authenticate(context,state,method);if(grant is null) return Results.StatusCode(403);
            }
            bool capacityAdmission=method=="spool-admit-transition" || method=="spool-authorize"
                && request.Payload.Deserialize<SpoolAuthorization>(CustodyBindingContract.Json)?.method is "Observe" or "Readiness";
            if(capacityAdmission && !UsableTransportCapacity(state))
                return Results.Json(new { code="DependencyNotAvailable",missingInputs=new[]{"IndependentMeasuredDaprRequestInvokeReceiverResponseCapacity128MiB"} },statusCode:503);
            if(method is "spool-current" or "spool-validate" or "spool-authorize")
            {
                bool admission=method=="spool-authorize" && request.Payload.Deserialize<SpoolAuthorization>(CustodyBindingContract.Json)!.method is "Observe" or "Readiness";
                await RequireArchiveAsync(admission,budget.Token).ConfigureAwait(false);
            }
            if (method is "spool-admit-recovery" or "spool-verify-recovery")
            {
                if (recordingSource is null && installation.RecorderAuthority is null) return Results.StatusCode(503);
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
                var actual = recordingSource is not null?await recordingSource.LookupAsync(originalRecord,budget.Token).ConfigureAwait(false)
                    :await new DaprSecurityObservationRecorder(new(new DaprSecretsInvoker(dapr),installation.RecorderAuthority!,clock)).LookupAsync(originalRecord,budget.Token).ConfigureAwait(false);
                if (actual.State == SecurityEventRecorderLookupState.Unknown || !Same(actual, submitted)) return Results.StatusCode(503);
            }
            if(method is "recording-recovery-command-authorize" or "recording-recovery-dispatch-admit")
            {
                if(recordingSource is null || installation.RecoverySpoolAuthority is null) return Results.StatusCode(503);
                var recoveryGrant=method=="recording-recovery-command-authorize"?request.Payload.Deserialize<RecordingRecoveryAuthorization>(CustodyBindingContract.Json)!.grant:request.Payload.Deserialize<RecordingRecoveryDispatch>(CustodyBindingContract.Json)!.grant;
                var actual=await recordingSource.LookupAsync(recoveryGrant.Original,budget.Token).ConfigureAwait(false);
                var recoveryAuthority=new DaprReplicatedSpoolAuthority(new(new DaprSecretsInvoker(dapr),installation.RecoverySpoolAuthority,clock));
                if(actual.State!=SecurityEventRecorderLookupState.NotRecorded || !await recoveryAuthority.VerifyAgedOriginalRecoveryAsync(recoveryGrant,actual,budget.Token).ConfigureAwait(false))
                    return Results.StatusCode(403);
            }
            if(method=="qualification-authorize" && state.QualificationBindings is { } bindings && !bindings.ContainsKey("runId"))
            {
                var authorization=request.Payload.Deserialize<CallerAuthorization>(CustodyBindingContract.Json)!;
                if(installation.CohortAttempt is null || !await new DaprQualificationAttemptAuthority(new DaprSecretsInvoker(dapr),installation.CohortAttempt,clock).ValidateAsync(authorization.request,budget.Token).ConfigureAwait(false))
                    return Results.StatusCode(403);
            }
            if(method is "recording-replay-recovery-command-authorize" or "recording-replay-recovery-dispatch-admit")
            {
                var proof=request.Payload.GetProperty("proof").Deserialize<PrivateReplayRecoveryProof>(CustodyBindingContract.Json)!;
                if(recordingSource is not IPrivateReplayRecoverySource native || !Same(await native.ReadReplayRecoveryProofAsync(proof.Intent,budget.Token).ConfigureAwait(false),proof)) return Results.StatusCode(403);
            }
            if(method is "spool-admit-transition" or "spool-record-transition" or "spool-recover-transition")
            {
                var transition=request.Payload.Deserialize<AnchoredStateTransition>(CustodyBindingContract.Json)!;
                string scope=state.Spool?.ComponentName+"|system/security-observations/"+state.Spool?.InstallationEpoch;
                if(state.Spool is null || state.Spool.ValidUntil<=clock.GetUtcNow() || transition.ScopeId!=scope || transition.TargetRevision!=transition.ExpectedRevision+1
                    || transition.TargetBytes.Length>32*1024*1024 || Convert.ToHexString(SHA256.HashData(transition.TargetBytes))!=transition.TargetDigest) return Results.StatusCode(403);
                if(!await HasSpoolOriginalAsync("recorded",transition,budget.Token).ConfigureAwait(false))
                {
                    bool archivedAdmission=await HasSpoolOriginalAsync("admitted",transition,budget.Token).ConfigureAwait(false);
                    var reservation=new IndependentSpoolPublication(Hash(transition),transition.TargetDigest,transition.TargetRevision,
                        checked((archivedAdmission?1L:2L)*JsonSerializer.SerializeToUtf8Bytes(transition,CustodyBindingContract.Json).LongLength+1024*1024));
                    if(state.SpoolPublication is { } prior)
                    {
                        if(prior.TransitionSha256!=reservation.TransitionSha256 || prior.TargetDigest!=reservation.TargetDigest || prior.TargetRevision!=reservation.TargetRevision || prior.ReservedBytes<reservation.ReservedBytes) return Results.StatusCode(503);
                        reservation=prior;
                    }
                    else
                    {
                        bool before=state.SpoolRevision==transition.ExpectedRevision && state.SpoolDigest==transition.PredecessorDigest;
                        bool committed=archivedAdmission && state.SpoolRevision==transition.TargetRevision && state.SpoolDigest==transition.TargetDigest;
                        if(!before && !committed || state.PendingSpoolTransitionDigest is { } pending && (pending!=transition.TargetDigest || !archivedAdmission)
                            || method!="spool-admit-transition" && !archivedAdmission) return Results.StatusCode(503);
                        // Reserve before even the first large archive write. A losing conditional writer publishes no bytes.
                        if(state.PendingOriginalCompletionBytes is null && (state.PrivateOriginalsCount!=0 || state.Originals.Count!=0)) return Results.StatusCode(503);
                        long other=checked((state.PendingOriginalCompletionBytes??0)+(state.OriginalPublications?.Values.Sum(value=>value.ReservedBytes)??0));
                        if(_archive.AvailableBytes(CurrentArchiveDirectory())<checked(other+reservation.ReservedBytes+installation.CompletionReserveBytes)) return Results.StatusCode(503);
                        var reserved=state with { Revision=checked(state.Revision+1),SpoolPublication=reservation,PendingSpoolTransitionDigest=before?transition.TargetDigest:state.PendingSpoolTransitionDigest };
                        if(grant.ValidUntil<=clock.GetUtcNow() || !await dapr.TrySaveStateAsync(installation.Component,installation.StateKey,reserved,etag,
                            new StateOptions { Concurrency=ConcurrencyMode.FirstWrite,Consistency=ConsistencyMode.Strong },cancellationToken:budget.Token).ConfigureAwait(false)) return Results.StatusCode(503);
                        (state,etag)=await dapr.GetStateAndETagAsync<IndependentAuthorityState>(installation.Component,installation.StateKey,ConsistencyMode.Strong,cancellationToken:budget.Token).ConfigureAwait(false);
                        if(state is null || state.SpoolPublication!=reservation || (grant=Authenticate(context,state,method)) is null) return Results.StatusCode(503);
                    }
                }
            }
            var ownerHandler=ownerHandlers?.SingleOrDefault(handler=>handler.Methods.Contains(method));
            bool externalEffect=method is "replay-register-exact" or "security-observation-append" or "security-observation-recover-original"
                || ownerHandler is not null && !ownerHandler.IsReadOnly(method);
            bool recoverAdmitted=false;
            if(externalEffect)
            {
                // The logical admission excludes frame nonce: a retry cannot turn unknown work into a new dispatch.
                var exact=JsonSerializer.SerializeToElement(new { caller=grant.Identity,grant.Role,grant.ScopeReference,method,payloadSha256=QualificationProtocolSchema.Digest(JsonNode.Parse(request.Payload.GetRawText())!) },CustodyBindingContract.Json);
                string admissionId="receiver:"+Hash(exact);
                var internalCaller=grant with { Role="receiver-admission" };
                var publicationPayload=JsonSerializer.SerializeToElement(new { role="receiver-admission",requestId=admissionId,original=exact,finalAtAdmission=true },CustodyBindingContract.Json);
                var prepared=await PreparePrivatePublicationAsync("qualification-begin-original",publicationPayload,internalCaller,state,etag,budget.Token).ConfigureAwait(false);
                if(!prepared.Ready) return Results.StatusCode(503);
                state=prepared.State;etag=prepared.ETag;var fresh=Authenticate(context,state,method);
                if(fresh is null || fresh.Identity!=grant.Identity || fresh.Role!=grant.Role || fresh.ScopeReference!=grant.ScopeReference) return Results.StatusCode(403);grant=fresh;
                var admitted=JsonSerializer.Deserialize<IndependentAuthorityState>(JsonSerializer.SerializeToUtf8Bytes(state,CustodyBindingContract.Json),CustodyBindingContract.Json)!;
                var admission=await ApplyPrivateOriginalAsync("qualification-begin-original",publicationPayload,internalCaller,state,admitted,value=>admitted=value,budget.Token).ConfigureAwait(false);
                var admittedBody=JsonSerializer.SerializeToElement(admission.Payload,CustodyBindingContract.Json);
                if(!admittedBody.GetProperty("Exact").GetBoolean()) return Results.StatusCode(503);
                recoverAdmitted=!admittedBody.GetProperty("First").GetBoolean();
                // Even exact recovery authorization is ordered with the same independently surviving revocation head.
                admitted=admitted with { Revision=checked(state.Revision+1) };
                if(grant.ValidUntil<=clock.GetUtcNow() || !await dapr.TrySaveStateAsync(installation.Component,installation.StateKey,admitted,etag,
                    new StateOptions { Concurrency=ConcurrencyMode.FirstWrite,Consistency=ConsistencyMode.Strong },cancellationToken:budget.Token).ConfigureAwait(false)) return Results.StatusCode(503);
                state=admitted;
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
                        recordingResult = recoverAdmitted?await recordingSource.LookupAsync(replay.intent,budget.Token):await recordingSource.RegisterAsync(replay.intent, replay.evaluatedAt, TimeSpan.FromTicks(replay.retentionTicks), budget.Token);
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
                        if(!recoverAdmitted) await recordingSource.RecoverOriginalAsync(recovery.original, recovery.grant, budget.Token);
                    }
                    else
                    {
                        originalRecord = request.Payload.Deserialize<RecorderOriginal>(CustodyBindingContract.Json)!.record;
                        if (method == "security-observation-append" && !recoverAdmitted) await recordingSource.AppendAsync(originalRecord, budget.Token);
                    }
                    var lookup = await recordingSource.LookupAsync(originalRecord, budget.Token);
                    recordingResult = new { OriginalSha256 = Hash(originalRecord), Lookup = lookup };
                }
                outcome = (recordingResult, false);
            }
            else if (ownerHandler is { } handler)
                outcome = (await (recoverAdmitted?handler.RecoverAsync(method,request.Payload,grant,state,budget.Token):handler.HandleAsync(method, request.Payload, grant, state, budget.Token)).WaitAsync(budget.Token).ConfigureAwait(false), false);
            else if(IsPrivateOriginalOperation(method,request.Payload))
                outcome=await ApplyPrivateOriginalAsync(method,request.Payload,grant,state,next,replacement=>next=replacement,budget.Token).ConfigureAwait(false);
            else if(method is "spool-admit-recovery" or "spool-verify-recovery")
                outcome=await ApplyRecoveryAsync(method,request.Payload,grant,state,next,budget.Token).ConfigureAwait(false);
            else if (method is "spool-admit-transition" or "spool-record-transition" or "spool-recover-transition" or "spool-verify-transition")
                outcome = await ApplySpoolTransitionAsync(method, request.Payload.Deserialize<AnchoredStateTransition>(CustodyBindingContract.Json)!, next, replacement => next = replacement, budget.Token).ConfigureAwait(false);
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
                // Release only after exact durable publication; failure keeps the reservation recoverable.
                var (settled,settledTag)=await dapr.GetStateAndETagAsync<IndependentAuthorityState>(installation.Component,installation.StateKey,ConsistencyMode.Strong,cancellationToken:budget.Token).ConfigureAwait(false);
                if(settled?.SpoolPublication is { } liability && liability.TransitionSha256==Hash(completed) && settled.SpoolRevision>=completed.TargetRevision)
                    if(!await dapr.TrySaveStateAsync(installation.Component,installation.StateKey,settled with { Revision=checked(settled.Revision+1),SpoolPublication=null },settledTag,
                        new StateOptions { Concurrency=ConcurrencyMode.FirstWrite,Consistency=ConsistencyMode.Strong },cancellationToken:budget.Token).ConfigureAwait(false)) return Results.StatusCode(503);
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
            return resultBytes.Length >= CustodyBindingContract.MaximumIndependentAuthorityFrameBytes ? Results.StatusCode(503) : Results.Bytes(resultBytes, "application/json");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Results.StatusCode(503); }
    }

    private bool UsableTransportCapacity(IndependentAuthorityState state)
    {
        var capacity=state.TransportCapacity;long required=CustodyBindingContract.MaximumIndependentAuthorityFrameBytes;
        return capacity is not null && capacity.ValidUntil>clock.GetUtcNow() && SecretsProvisioning.IsReference(capacity.InstallationRevision)
            && SecretsProvisioning.IsReference(capacity.DaprSidecarIdentity) && capacity.MeasurementOriginalSha256 is { Length:64 } && capacity.MeasurementOriginalSha256.All(char.IsAsciiHexDigit)
            && capacity.MaximumDaprRequestBytes>=required && capacity.MaximumDaprInvokeBytes>=required && capacity.MaximumReceiverBytes>=required && capacity.MaximumResponseBytes>=required;
    }

    private async Task<(IndependentAuthorityState State,string ETag,bool Ready)> PreparePrivatePublicationAsync(string method,JsonElement payload,IndependentCallerGrant caller,
        IndependentAuthorityState state,string etag,CancellationToken token)
    {
        string role=payload.GetProperty("role").GetString()!,id=payload.GetProperty("requestId").GetString()!;
        if(role!=caller.Role || !CustodyBindingContract.Text(id) || !PrivateRole(role)) return (state,etag,false);
        bool begin=method=="qualification-begin-original",final=begin && payload.TryGetProperty("finalAtAdmission",out var terminal) && terminal.GetBoolean();
        if(final && !TerminalAtAdmission(role,id)) return (state,etag,false);
        async Task<bool> Save(IndependentAuthorityState next)
        {
            next=next with { Revision=checked(state.Revision+1) };
            if(JsonSerializer.SerializeToUtf8Bytes(next,CustodyBindingContract.Json).Length>32*1024*1024) return false;
            if(!await dapr.TrySaveStateAsync(installation.Component,installation.StateKey,next,etag,new StateOptions { Concurrency=ConcurrencyMode.FirstWrite,Consistency=ConsistencyMode.Strong },cancellationToken:token).ConfigureAwait(false)) return false;
            (state,etag)=await dapr.GetStateAndETagAsync<IndependentAuthorityState>(installation.Component,installation.StateKey,ConsistencyMode.Strong,cancellationToken:token).ConfigureAwait(false);
            if(state is null || string.IsNullOrWhiteSpace(etag) || state.ValidUntil<=clock.GetUtcNow()) throw new IOException("Current original publication reservation unavailable.");return true;
        }
        if(state.PendingOriginalCompletionBytes is null || state.OriginalLiabilityVersion!=1)
        {
            var initialized=JsonSerializer.Deserialize<IndependentAuthorityState>(JsonSerializer.SerializeToUtf8Bytes(state,CustodyBindingContract.Json),CustodyBindingContract.Json)!;
            var result=await ApplyPrivateOriginalAsync("private-original-initialize",payload,caller,state,initialized,value=>initialized=value,token).ConfigureAwait(false);
            if(!await Save(initialized).ConfigureAwait(false) || result.Payload is not Permission { Allowed:true }) return (state,etag,false);
        }
        var index=new IndependentPrivateOriginalIndex(PrivateArchive());var existing=(await index.ReadAsync(state.PrivateOriginalsRoot,role,id,token).ConfigureAwait(false))?.Original??state.Originals.GetValueOrDefault(role+":"+id);
        if(begin && existing is not null || !begin && existing?.Result is not null) return (state,etag,true);
        if(!begin && existing is null) return (state,etag,false);
        byte[] body=JsonSerializer.SerializeToUtf8Bytes(payload.GetProperty(begin?"original":"result"),CustodyBindingContract.Json);
        if(body.Length>(begin?CustodyBindingContract.MaximumOriginalBytes/2:CustodyBindingContract.MaximumOriginalBytes)) return (state,etag,false);
        string kind=begin?"request":"result",key=PublicationKey(role,id,kind);var publications=state.OriginalPublications??new();
        var reserved=new IndependentOriginalPublication(role,id,kind,CustodyBindingContract.Hash(body),begin?2L*CustodyBindingContract.MaximumOriginalBytes:0,begin && !final?OriginalCompletionBytes:0,final);
        if(publications.TryGetValue(key,out var prior)) { if(prior!=reserved) return (state,etag,false); }
        else
        {
            if(publications.Count>=128 || _archive.AvailableBytes(CurrentArchiveDirectory())<checked(state.PendingOriginalCompletionBytes!.Value+(state.SpoolPublication?.ReservedBytes??0)+publications.Values.Sum(value=>value.ReservedBytes)+reserved.ReservedBytes+reserved.CompletionBytes+installation.CompletionReserveBytes)) return (state,etag,false);
            var nextPublications=new Dictionary<string,IndependentOriginalPublication>(publications,StringComparer.Ordinal) { [key]=reserved };
            if(!await Save(state with { PendingOriginalCompletionBytes=checked(state.PendingOriginalCompletionBytes.Value+reserved.CompletionBytes),OriginalPublications=nextPublications }).ConfigureAwait(false)) return (state,etag,false);
            publications=state.OriginalPublications!;
        }
        long own=begin?reserved.CompletionBytes:existing is { Result:null,FinalAtAdmission:false }?OriginalCompletionBytes:0;
        long required=checked(state.PendingOriginalCompletionBytes!.Value-own+(state.SpoolPublication?.ReservedBytes??0)+publications.Values.Sum(value=>value.ReservedBytes)-reserved.ReservedBytes+body.LongLength+1024*1024+installation.CompletionReserveBytes);
        if(_archive.AvailableBytes(CurrentArchiveDirectory())<required) return (state,etag,false);
        await _archive.ProbeWritableAsync(CurrentArchiveDirectory(),token).ConfigureAwait(false);return (state,etag,true);
    }
    private const long OriginalCompletionBytes=4L*CustodyBindingContract.MaximumOriginalBytes+1024*1024;
    private static string PublicationKey(string role,string id,string kind)=>Hash(new { role,id,kind });
    private static bool TerminalAtAdmission(string role,string id)=>role=="receiver-admission" && id.StartsWith("receiver:",StringComparison.Ordinal) || role=="fr34-recipes" && (id.EndsWith(":encryption",StringComparison.Ordinal) || id.EndsWith(":completion",StringComparison.Ordinal))
        || role=="occurrence-read-use" && id.Contains(":release:",StringComparison.Ordinal) || role=="guarded-transaction" && id.StartsWith("cell:",StringComparison.Ordinal);
    private IIndependentContentArchive PrivateArchive()=>privateOriginalArchive??new IndependentFileContentArchive(Path.Combine(CurrentArchiveDirectory(),"private-originals",Hash(new { installation.EnrollmentId,installation.Scope,installation.TrustRevision })));

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
                    && new[]{"targetCommit","isolatedTargetId","planSha256"}.All(name=>authorization.request[name]?.ToJsonString()==state.QualificationBindings[name])
                    && (!state.QualificationBindings.TryGetValue("runId",out var exactRun) || authorization.request["runId"]?.ToJsonString()==exactRun)
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
            if(state.SpoolPublication is not { } reservation || reservation.TransitionSha256!=Hash(transition)) return (Decision(false),false);
            await WriteSpoolOriginalAsync("admitted", transition, false, token).ConfigureAwait(false);
            replace(state with { PendingSpoolTransitionDigest = transition.TargetDigest });
            return (Decision(true), state.PendingSpoolTransitionDigest != transition.TargetDigest);
        }
        if (!admitted || state.PendingSpoolTransitionDigest != transition.TargetDigest) return (Decision(false), false);
        replace(state with { SpoolRevision = transition.TargetRevision, SpoolDigest = transition.TargetDigest, PendingSpoolTransitionDigest = null });
        return (Decision(true), true);
    }
    private static bool PrivateRole(string? role)=>role is "receiver-admission" or "private-replay" or "private-security-observation" or "fr34-engine" or "fr34-observer" or "executor" or "observer" or "enrollment" or "joint-phase-secret" or "joint-phase-command" or "fr34-recipes" or "occurrence-read-use" or "guarded-transaction";
    private static bool IsPrivateOriginalOperation(string method,JsonElement payload)=>method is "recording-replay-recovery-command-authorize" or "recording-replay-recovery-dispatch-admit" or "recording-command-authorize" or "recording-recovery-command-authorize" or "recording-recovery-dispatch-admit" or "fr34-command-authorize"
        || method is "qualification-begin-original" or "qualification-retain-result" or "qualification-lookup-original" or "recording-dispatch-admit"
            && payload.TryGetProperty("role",out var role) && PrivateRole(role.GetString());
    private async Task<(object? Payload,bool Mutated)> ApplyPrivateOriginalAsync(string method,JsonElement payload,IndependentCallerGrant caller,
        IndependentAuthorityState current,IndependentAuthorityState next,Action<IndependentAuthorityState> replace,CancellationToken token)
    {
        var index=new IndependentPrivateOriginalIndex(PrivateArchive());
        // Includes a maximum result body, retained dispatch/history bodies and bounded AVL path publication.
        const long liability=OriginalCompletionBytes;
        async Task<bool> InitializeLiabilities()
        {
            if(next.PendingOriginalCompletionBytes is not null && next.OriginalLiabilityVersion==1) return true;
            var migration=next.OriginalLiabilityMigration??new(next.PrivateOriginalsRoot,[],0,next.OriginalPublications?.Values.Sum(value=>value.CompletionBytes)??0);
            if(migration.IndexRoot!=next.PrivateOriginalsRoot) throw new IOException("Liability migration index changed.");
            IReadOnlyList<string> cursor=migration.IndexStarted?migration.Remaining:migration.IndexRoot is null?[]:[migration.IndexRoot];
            var page=await index.PageAsync(cursor,64,token).ConfigureAwait(false);long pending=migration.PendingBytes;
            foreach(var entry in page.Entries) if(entry.Pending) pending=checked(pending+liability);
            migration=migration with { Remaining=page.Remaining,IndexStarted=true,Processed=checked(migration.Processed+page.Entries.Count),PendingBytes=pending };
            if(migration.Processed>next.PrivateOriginalsCount) throw new IOException("Original migration count substitution.");
            bool legacyComplete=false;
            if(page.Remaining.Count==0)
            {
                var legacy=next.Originals.Where(pair=>PrivateRole(pair.Key.Split(':',2)[0]) && (migration.LegacyAfter is null || string.CompareOrdinal(pair.Key,migration.LegacyAfter)>0))
                    .OrderBy(pair=>pair.Key,StringComparer.Ordinal).Take(65).ToArray();
                foreach(var entry in legacy.Take(64)) { if(entry.Value.Result is null && !entry.Value.FinalAtAdmission) pending=checked(pending+liability);migration=migration with { LegacyAfter=entry.Key,PendingBytes=pending }; }
                legacyComplete=legacy.Length<=64;
            }
            bool complete=page.Remaining.Count==0 && legacyComplete;
            next=next with { PendingOriginalCompletionBytes=complete?pending:null,OriginalLiabilityVersion=complete?1:0,OriginalLiabilityMigration=complete?null:migration };replace(next);return complete;
        }
        async Task Reserve(long extra,long writes)
        {
            if(!await InitializeLiabilities().ConfigureAwait(false)) throw new IOException("Bounded durable original liability migration required.");string directory=CurrentArchiveDirectory();
            if(_archive.AvailableBytes(directory)<checked(next.PendingOriginalCompletionBytes!.Value+extra+writes+(next.SpoolPublication?.ReservedBytes??0)+installation.CompletionReserveBytes))
                throw new IOException("Independent archive outstanding original completion liability unavailable.");
            await _archive.ProbeWritableAsync(directory,token).ConfigureAwait(false);
        }
        async Task<IndependentOriginal?> Read(string role,string id)
        {
            var archived=await index.ReadAsync(next.PrivateOriginalsRoot,role,id,token).ConfigureAwait(false);
            return archived?.Original??next.Originals.GetValueOrDefault(role+":"+id);
        }
        async Task Store(string role,string id,IndependentOriginal value)
        {
            var stored=await index.SetAsync(next.PrivateOriginalsRoot,role,id,value,token).ConfigureAwait(false);
            next=next with { PrivateOriginalsRoot=stored.Root,PrivateOriginalsCount=checked(next.PrivateOriginalsCount+(stored.First?1:0)) };
            next.Originals.Remove(role+":"+id);replace(next);
        }
        async Task Migrate()
        {
            await RequireArchiveAsync(false,token).ConfigureAwait(false);
            long bytes=0;
            foreach(var legacy in next.Originals.Where(pair=>PrivateRole(pair.Key.Split(':',2)[0])).Take(16).ToArray())
            { bytes=checked(bytes+JsonSerializer.SerializeToUtf8Bytes(legacy.Value,CustodyBindingContract.Json).LongLength);if(bytes>1024*1024) break;int separator=legacy.Key.IndexOf(':');await Store(legacy.Key[..separator],legacy.Key[(separator+1)..],legacy.Value).ConfigureAwait(false); }
        }
        bool Workload(string identity,string principal,string domain)=>current.Callers.Any(value=>value.Identity==identity && value.ScopeReference==principal && !value.Revoked
            && value.ValidUntil>clock.GetUtcNow() && value.Methods.Contains("recording-source:"+(domain=="agents"?"legacy-security-event-log":domain),StringComparer.Ordinal));
        if(method=="private-original-initialize") return (new Permission(await InitializeLiabilities().ConfigureAwait(false)),true);
        if(method=="qualification-lookup-original")
        {
            var request=payload.Deserialize<LookupOriginal>(CustodyBindingContract.Json)!;
            return (request.role==caller.Role || caller.Methods.Contains("qualification-read-original:"+request.role,StringComparer.Ordinal)?await Read(request.role,request.requestId).ConfigureAwait(false):null,false);
        }
        // Persist each bounded migration page even when the requested mutation cannot yet be admitted.
        // No archive/index mutation races the pinned migration snapshot; authenticated exact reads remain available.
        if(method is "qualification-begin-original" or "qualification-retain-result" or "recording-dispatch-admit" or "recording-replay-recovery-dispatch-admit" or "recording-recovery-dispatch-admit")
            if(!await InitializeLiabilities().ConfigureAwait(false)) return (method.StartsWith("qualification-",StringComparison.Ordinal)?new { Exact=false,First=false }:new Permission(false),true);
        if(method=="qualification-begin-original")
        {
            var request=payload.Deserialize<BeginOriginal>(CustodyBindingContract.Json)!;
            if(request.role!=caller.Role) return (new { Exact=false,First=false },false);
            var existing=await Read(request.role,request.requestId).ConfigureAwait(false);
            if(existing is not null) return (new { Exact=Same(existing.Request,request.original) && existing.FinalAtAdmission==request.finalAtAdmission,First=false },false);
            if(request.finalAtAdmission && !TerminalAtAdmission(request.role,request.requestId)) return (new { Exact=false,First=false },false);
            if(JsonSerializer.SerializeToUtf8Bytes(request.original,CustodyBindingContract.Json).Length>CustodyBindingContract.MaximumOriginalBytes/2) throw new IOException("Bounded original request leaves no result capacity.");
            await RequireArchiveAsync(true,token).ConfigureAwait(false);
            string publicationKey=PublicationKey(request.role,request.requestId,"request");
            bool preReserved=next.OriginalPublications?.ContainsKey(publicationKey)==true;
            if(!preReserved) await Reserve(request.finalAtAdmission?0:liability,2L*CustodyBindingContract.MaximumOriginalBytes).ConfigureAwait(false);
            await Migrate().ConfigureAwait(false);
            await Store(request.role,request.requestId,new(request.original.Clone(),null,RetainedRevision:checked(current.Revision+1),FinalAtAdmission:request.finalAtAdmission)).ConfigureAwait(false);
            next.OriginalPublications?.Remove(publicationKey);
            next=next with { PendingOriginalCompletionBytes=checked(next.PendingOriginalCompletionBytes!.Value+(preReserved || request.finalAtAdmission?0:liability)) };replace(next);
            return (new { Exact=true,First=true },true);
        }
        if(method=="qualification-retain-result")
        {
            var request=payload.Deserialize<RetainResult>(CustodyBindingContract.Json)!;
            var original=request.role==caller.Role?await Read(request.role,request.requestId).ConfigureAwait(false):null;
            if(original is null || original.FinalAtAdmission) return (new { Exact=false,First=false },false);
            if(original.Result is { } recorded) return (new { Exact=Same(recorded,request.result),First=false },false);
            _=await InitializeLiabilities().ConfigureAwait(false);
            await Migrate().ConfigureAwait(false);await Store(request.role,request.requestId,original with { Result=request.result.Clone(),ResultRetainedRevision=checked(current.Revision+1) }).ConfigureAwait(false);
            next=next with { PendingOriginalCompletionBytes=checked(next.PendingOriginalCompletionBytes!.Value>=liability?next.PendingOriginalCompletionBytes.Value-liability:throw new IOException("Uncharged original completion liability requires reconciliation.")) };replace(next);
            next.OriginalPublications?.Remove(PublicationKey(request.role,request.requestId,"result"));
            return (new { Exact=true,First=true },true);
        }
        if(method=="recording-dispatch-admit")
        {
            var request=payload.Deserialize<RecordingDispatch>(CustodyBindingContract.Json)!;
            var original=request.role==caller.Role?await Read(request.role,request.requestId).ConfigureAwait(false):null;
            if(original is null || original.SubmissionState!="Unsent" || !Same(original.Request,request.original)) return (new Permission(false),false);
            await Migrate().ConfigureAwait(false);await Store(request.role,request.requestId,original with { SubmissionState="Dispatched" }).ConfigureAwait(false);return (new Permission(true),true);
        }
        if(method=="fr34-command-authorize")
        {
            var request=payload.Deserialize<RecordingCommandAuthorization>(CustodyBindingContract.Json)!;var command=request.command;
            var original=await Read("fr34-engine",command.MessageId).ConfigureAwait(false);var target=command.Payload.GetProperty("protectedContent").GetProperty("target");
            return (new Permission(current.Fr34Target is { } fr34 && original is { SubmissionState:"Unsent" } && Same(original.Request,command.Payload)
                && command.Domain=="fr34-canary" && command.Tenant==fr34.TenantId && command.CommandType=="Hexalith.Platform.Protection.PersistFr34Canary"
                && target.GetProperty("tenantId").GetString()==fr34.TenantId && target.GetProperty("agentInteractionId").GetString()==command.AggregateId
                && target.GetProperty("targetProtectionKeyAlias").GetString()=="fr34-"+command.AggregateId && Workload(request.caller,request.principal,"fr34-canary")),false);
        }
        if(method=="recording-command-authorize")
        {
            var request=payload.Deserialize<RecordingCommandAuthorization>(CustodyBindingContract.Json)!;var command=request.command;
            string role=command.Domain=="trusted-envelope-replay"?"private-replay":"private-security-observation";
            var original=await Read(role,command.MessageId).ConfigureAwait(false);
            return (new Permission(command.Domain is "trusted-envelope-replay" or "security-event-log" or "agents" && original is { SubmissionState:"Unsent" }
                && Same(original.Request,command.Payload) && Workload(request.caller,request.principal,command.Domain)),false);
        }
        if(method is "recording-replay-recovery-command-authorize" or "recording-replay-recovery-dispatch-admit")
        {
            var command=payload.GetProperty("command").Deserialize<Hexalith.EventStore.Contracts.Commands.SubmitCommandRequest>(CustodyBindingContract.Json)!;
            var proof=payload.GetProperty("proof").Deserialize<PrivateReplayRecoveryProof>(CustodyBindingContract.Json)!;
            var main=await Read("private-replay",proof.OriginalRequestId).ConfigureAwait(false);var attempt=await Read("private-replay",command.MessageId).ConfigureAwait(false);
            bool allowed=caller.Role=="private-replay" && command.Domain=="trusted-envelope-replay" && command.Tenant=="system" && command.MessageId==PrivateRecordingIdentity.ReplayRecoveryMessageId(proof)
                && command.IdempotencyKey==command.MessageId && main is { SubmissionState:"Dispatched" } && (main.ActiveTransportMessageId??proof.OriginalRequestId)==proof.PreviousTransportMessageId
                && attempt is { SubmissionState:"Unsent" } && Same(main.Request,proof.OriginalCommand) && Same(command.Payload,proof.OriginalCommand) && Same(attempt.Request,proof.OriginalCommand);
            if(method=="recording-replay-recovery-command-authorize")
                return (new Permission(allowed && Workload(payload.GetProperty("caller").GetString()!,payload.GetProperty("principal").GetString()!,command.Domain)),false);
            if(!allowed) return (new Permission(false),false);
            await Migrate().ConfigureAwait(false);await Store("private-replay",command.MessageId,attempt! with { SubmissionState="Dispatched" }).ConfigureAwait(false);
            await Store("private-replay",proof.OriginalRequestId,main! with { ActiveTransportMessageId=command.MessageId }).ConfigureAwait(false);return (new Permission(true),true);
        }
        if(method=="recording-recovery-command-authorize")
        {
            var request=payload.Deserialize<RecordingRecoveryAuthorization>(CustodyBindingContract.Json)!;var command=request.command;
            var attempt=await Read("private-security-observation",command.MessageId).ConfigureAwait(false);
            var main=await Read("private-security-observation",PrivateRecordingIdentity.SecurityOriginalMessageId(request.grant.Original.Intent.ObservationId)).ConfigureAwait(false);
            return (new Permission(command.Domain is "security-event-log" or "agents" && command.MessageId==PrivateRecordingIdentity.SecurityRecoveryMessageId(request.grant)
                && command.IdempotencyKey==command.MessageId && request.grant.ValidUntil>clock.GetUtcNow() && attempt is { SubmissionState:"Unsent" }
                && (main is not null?Same(main.Request,command.Payload):request.grant.OriginalProof.NoRecordProof?.Basis=="DefinitiveAbsent")
                && Same(RecoveryPayload(request.grant),command.Payload) && Same(attempt.Request,command.Payload) && Workload(request.caller,request.principal,command.Domain)),false);
        }
        var dispatch=payload.Deserialize<RecordingRecoveryDispatch>(CustodyBindingContract.Json)!;var recovered=dispatch.command;
        string ordinaryId=PrivateRecordingIdentity.SecurityOriginalMessageId(dispatch.grant.Original.Intent.ObservationId);
        var baseOriginal=await Read("private-security-observation",ordinaryId).ConfigureAwait(false);
        var recoveryOriginal=await Read("private-security-observation",recovered.MessageId).ConfigureAwait(false);
        if(caller.Role!="private-security-observation" || recovered.Domain is not ("security-event-log" or "agents") || recovered.MessageId!=PrivateRecordingIdentity.SecurityRecoveryMessageId(dispatch.grant)
            || recovered.IdempotencyKey!=recovered.MessageId || dispatch.grant.ValidUntil<=clock.GetUtcNow() || recoveryOriginal is not { SubmissionState:"Unsent" }
            || baseOriginal is null || !Same(baseOriginal.Request,recovered.Payload)
            || !Same(RecoveryPayload(dispatch.grant),recovered.Payload) || !Same(recoveryOriginal.Request,recovered.Payload)) return (new Permission(false),false);
        await Migrate().ConfigureAwait(false);
        await Store("private-security-observation",recovered.MessageId,recoveryOriginal with { SubmissionState="Dispatched" }).ConfigureAwait(false);
        await Store("private-security-observation",ordinaryId,baseOriginal with { SubmissionState="Dispatched",ActiveTransportMessageId=recovered.MessageId }).ConfigureAwait(false);
        return (new Permission(true),true);
    }
    private static JsonElement RecoveryPayload(SecurityObservationRecoveryGrant grant)
    {
        var r=grant.Original;
        return JsonSerializer.SerializeToElement(new { securityObservationId=r.Intent.ObservationId,routingTenantId=r.Intent.RoutingTenantId,utcDay=r.UtcDay,observedAt=r.ObservedAt,
            reasonCode=r.Intent.ReasonCode,untrustedFieldsHmac=r.Intent.UntrustedFieldsHmac,digestKeyVersion=r.Intent.DigestKeyVersion });
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
                || MonotonicUnsentBase(verify.grant.OriginalProof,verify.currentProof)
                || verify.grant.OriginalProof.State==SecurityEventRecorderLookupState.NotRecorded && verify.currentProof.State==SecurityEventRecorderLookupState.Recorded);
        if(admitted && verify.currentProof.State==SecurityEventRecorderLookupState.Recorded)
            foreach(string key in next.Recoveries.Where(pair=>Same(pair.Value.Original,verify.grant.Original) && pair.Value.Target==verify.grant.Target).Select(pair=>pair.Key).ToArray()) next.Recoveries.Remove(key);
        return (new Permission(admitted),admitted); // Successful verification is ordered atomically with role revocation, including an archive-only grant.
    }
    private static bool MonotonicUnsentBase(SecurityEventRecorderLookup before,SecurityEventRecorderLookup after)
        =>before.State==SecurityEventRecorderLookupState.NotRecorded && after.State==SecurityEventRecorderLookupState.NotRecorded
            && before.NoRecordProof is { Basis:"DefinitiveAbsent" } absent && after.NoRecordProof is { Basis:"RetainedUnsent" } admitted
            && absent.OriginalMessageId==admitted.OriginalMessageId && admitted.CurrentTransportMessageId==admitted.OriginalMessageId
            && absent.NativeSourceIncarnation==admitted.NativeSourceIncarnation && absent.NativeHead==admitted.NativeHead && absent.NativePrefixSha256==admitted.NativePrefixSha256;
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

    private sealed record RecordingRecoveryDispatch(Hexalith.EventStore.Contracts.Commands.SubmitCommandRequest command,SecurityObservationRecoveryGrant grant);
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
    private sealed record BeginOriginal(string role, string requestId, JsonElement original,bool finalAtAdmission=false);
    private sealed record RetainResult(string role, string requestId, JsonElement result);
    private sealed record RegistrationLookup(CustodyKeyObjectIdentity identity);
    private sealed record ManifestLookup(string reservationReceiptId);
}
