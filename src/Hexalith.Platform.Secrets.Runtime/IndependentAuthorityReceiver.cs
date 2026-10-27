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
public sealed class IndependentAuthorityReceiver(DaprClient dapr, IndependentAuthorityReceiverInstallation installation, ECDsa signer, TimeProvider clock, IPrivateRecordingEventStore? recordingSource = null, IReadOnlyList<IIndependentOwnerOperationHandler>? ownerHandlers = null,IIndependentArchiveStorage? archiveStorage=null,IIndependentContentArchive? privateOriginalArchive=null, ICustodyRegistrationOriginalVerifier? registrationVerifier=null)
{
    private readonly AsyncLocal<List<string>?> _completedPhysicalTickets=new();
    private readonly SemaphoreSlim _receivingSlots=new(4,4);
    /// <summary>Bounds receiving ownership before reading a body; saturated callers allocate no request frame.</summary>
    public async Task<IResult> HandleAsync(HttpContext context,string method,CancellationToken cancellationToken)
    {
        if(!_receivingSlots.Wait(0)) { IndependentReceivingDiagnostics.Record(IndependentReceivingFailure.CapacitySaturated);return Results.StatusCode(503); }
        var ownership=new IndependentReceivingOwnership(_receivingSlots,cancellationToken);ownership.Start();
        var previous=IndependentReceivingOwnership.Current.Value;IndependentReceivingOwnership.Current.Value=ownership;
        context.Response.RegisterForDispose(ownership);
        try
        {
            var handling=HandleCoreAsync(context,method,ownership.Token);ownership.Track(handling);
            IResult result;
            try { result=await handling.WaitAsync(ownership.Token).ConfigureAwait(false); }
            catch
            {
                // A late successful core can own an encoded response even after its caller left.
                // Keep its cleanup charged to this same slot before releasing the caller owner.
                var cleanup=handling.ContinueWith(task=> { if(task.Status==TaskStatus.RanToCompletion && task.Result is IDisposable response) response.Dispose();else _=task.Exception; },CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
                ownership.Track(cleanup);throw;
            }
            IndependentReceivingDiagnostics.ObserveStatus((result as IStatusCodeHttpResult)?.StatusCode);
            return ownership.Wrap(result);
        }
        catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested)
        { IndependentReceivingDiagnostics.Record(IndependentReceivingFailure.AcquisitionTimeout);ownership.Dispose();return Results.StatusCode(503); }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
        { IndependentReceivingDiagnostics.Record(IndependentReceivingFailure.CallerCancelled);ownership.Dispose();throw; }
        catch { ownership.Dispose();throw; }
        finally { IndependentReceivingOwnership.Current.Value=previous; }
    }
    private readonly IIndependentArchiveStorage _archive=archiveStorage??new IndependentArchiveStorage();
    /// <summary>Authenticates channel/workload, reads the surviving enrolled head, commits exact transitions and signs bounded current results.</summary>
    private async Task<IResult> HandleCoreAsync(HttpContext context, string method, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); budget.CancelAfter(TimeSpan.FromSeconds(30));
        bool validFrame=false;IIndependentOwnerOperationHandler? resultOwner=null;object? ownedResult=null;byte[]? requestWire=null;
        _completedPhysicalTickets.Value=[];
        try
        {
            if (!await ChannelAsync(context, budget.Token).ConfigureAwait(false)) return Results.StatusCode(403);
            string? body=await DaprSecretsInvoker.ReadBoundedAsync(context.Request.Body,budget.Token,CustodyBindingContract.MaximumIndependentAuthorityFrameBytes-1).ConfigureAwait(false);
            if(body is null) return Results.StatusCode(400);
            byte[] wire=requestWire=Encoding.UTF8.GetBytes(body);CustodyBindingContract.RejectDuplicateProperties(wire);
            // Preserve the original parsed property order/encoding by using the identical closed typed serialization as the client.
            var request = JsonSerializer.Deserialize<Request>(wire, CustodyBindingContract.Json);
            if (request is null || request.Protocol != "platform-independent-authority-v1" || request.EnrollmentId != installation.EnrollmentId
                || request.Scope != installation.Scope || request.TrustRevision != installation.TrustRevision || request.Method != method
                || request.Nonce.Length != 32 || !request.Nonce.All(char.IsAsciiHexDigit)) return Results.StatusCode(403);
            validFrame=true;CryptographicOperations.ZeroMemory(wire);wire=requestWire=JsonSerializer.SerializeToUtf8Bytes(request,CustodyBindingContract.Json);
            var (state, etag) = await dapr.GetStateAndETagAsync<IndependentAuthorityState>(installation.Component, installation.StateKey,
                ConsistencyMode.Strong, cancellationToken: budget.Token).ConfigureAwait(false);
            if (state is null || string.IsNullOrWhiteSpace(etag) || state.EnrollmentId != installation.EnrollmentId || state.Scope != installation.Scope
                || state.TrustRevision != installation.TrustRevision || state.ValidUntil <= clock.GetUtcNow()) return Results.StatusCode(503);
            budget.Token.ThrowIfCancellationRequested();
            var grant = Authenticate(context, state, method);
            if (grant is null) return Results.StatusCode(403);
            // A successful result commit pins its index basis until all exact frames are durable.
            // Every replica helps finish that commit before another source mutation can rebase the index.
            if(state.PrivateResultCommit is not null)
            {
                var recovered=await MaterializePrivateResultAsync(state,etag,budget.Token).ConfigureAwait(false);
                if(!recovered.Ready) return Results.StatusCode(503);
                state=recovered.State;etag=recovered.ETag;grant=Authenticate(context,state,method);
                if(grant is null) return Results.StatusCode(403);
            }
            if(method=="qualification-begin-original" && request.Payload.TryGetProperty("role",out var inputRole) && inputRole.GetString()=="fr34-engine"
                && (!request.Payload.TryGetProperty("original",out var canaryInput) || !Fr34CommandFingerprint.IsDescriptor(canaryInput)))
                return Results.Json(new { code="DependencyNotAvailable",missingInputs=new[]{"External.LegacyFr34RetainedPlaintextOriginalCleanupUnproven"} },statusCode:503);
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
                if(state.PendingOriginalCompletionBytes is null || state.OriginalLiabilityVersion!=1)
                {
                    var migrated=JsonSerializer.Deserialize<IndependentAuthorityState>(JsonSerializer.SerializeToUtf8Bytes(state,CustodyBindingContract.Json),CustodyBindingContract.Json)!;
                    var migration=await ApplyPrivateOriginalAsync("private-original-initialize",request.Payload,grant,state,migrated,value=>migrated=value,budget.Token).ConfigureAwait(false);
                    migrated=migrated with { Revision=checked(state.Revision+1) };
                    if(grant.ValidUntil<=clock.GetUtcNow() || !await SaveCurrentAsync(installation.Component,installation.StateKey,migrated,etag,
                        new StateOptions { Concurrency=ConcurrencyMode.FirstWrite,Consistency=ConsistencyMode.Strong },cancellationToken:budget.Token).ConfigureAwait(false)) return Results.StatusCode(503);
                    if(migration.Payload is not Permission { Allowed:true }) return Results.StatusCode(503);
                    (state,etag)=await dapr.GetStateAndETagAsync<IndependentAuthorityState>(installation.Component,installation.StateKey,ConsistencyMode.Strong,cancellationToken:budget.Token).ConfigureAwait(false);
                    if(state is null || state.OriginalLiabilityVersion!=1 || state.PendingOriginalCompletionBytes is null || (grant=Authenticate(context,state,method)) is null) return Results.StatusCode(503);
                }
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
                        if(AvailableArchiveBytes(CurrentArchiveDirectory(budget.Token),budget.Token)<checked(other+reservation.ReservedBytes+installation.CompletionReserveBytes)) return Results.StatusCode(503);
                        await ProtectPendingAsync(state,budget.Token).ConfigureAwait(false);
                        if(Capacity is { } pool) await pool.ReserveAsync(pool.Ticket("spool",Hash(transition)),checked(reservation.ReservedBytes+1024*1024),0,budget.Token,Hash(transition),"spool",Hash(transition),2).ConfigureAwait(false);
                        var reserved=state with { Revision=checked(state.Revision+1),SpoolPublication=reservation,PendingSpoolTransitionDigest=before?transition.TargetDigest:state.PendingSpoolTransitionDigest };
                        bool saved=Capacity is { } admissionPool
                            ?await AdmitPhysicalHeadAsync(admissionPool,admissionPool.Ticket("spool",Hash(transition)),reserved,etag,grant,async (head,ct)=>
                            {
                                bool retained=await HasSpoolOriginalAsync("admitted",transition,ct).ConfigureAwait(false);
                                return (head.SpoolPublication==reservation || retained,head.SpoolPublication is null && !retained && head.SpoolRevision<transition.TargetRevision);
                            },budget.Token).ConfigureAwait(false)
                            :grant.ValidUntil>clock.GetUtcNow() && await SaveCurrentAsync(installation.Component,installation.StateKey,reserved,etag,
                                new StateOptions { Concurrency=ConcurrencyMode.FirstWrite,Consistency=ConsistencyMode.Strong },cancellationToken:budget.Token).ConfigureAwait(false);
                        if(!saved) return Results.StatusCode(503);
                        (state,etag)=await dapr.GetStateAndETagAsync<IndependentAuthorityState>(installation.Component,installation.StateKey,ConsistencyMode.Strong,cancellationToken:budget.Token).ConfigureAwait(false);
                        if(state is null || state.SpoolPublication!=reservation || (grant=Authenticate(context,state,method)) is null) return Results.StatusCode(503);
                    }
                }
            }
            // Prerequisite observations can consult this same surviving authority through a restricted client.
            // Their own result-ordering CAS is journal progress, so acquire the current head before applying
            // this operation. Keep the exact original caller and semantic policy; this never renews a grant.
            var (preparedHead,preparedTag)=await dapr.GetStateAndETagAsync<IndependentAuthorityState>(installation.Component,installation.StateKey,ConsistencyMode.Strong,cancellationToken:budget.Token).ConfigureAwait(false);
            budget.Token.ThrowIfCancellationRequested();
            if(preparedHead is null || preparedHead.PrivateResultCommit is not null || string.IsNullOrWhiteSpace(preparedTag) || preparedHead.EnrollmentId!=state.EnrollmentId || preparedHead.Scope!=state.Scope || preparedHead.TrustRevision!=state.TrustRevision
                || preparedHead.ValidUntil<=clock.GetUtcNow() || Authenticate(context,preparedHead,method) is not { } preparedGrant || !Same(preparedGrant,grant)
                || !Same(new { state.Callers,state.Spool,state.QualificationBindings,state.QualificationCases,state.Fr34Target,state.Fr34RecipeSha256,state.Fr34AuthorizationRevision,state.TransportCapacity },
                    new { preparedHead.Callers,preparedHead.Spool,preparedHead.QualificationBindings,preparedHead.QualificationCases,preparedHead.Fr34Target,preparedHead.Fr34RecipeSha256,preparedHead.Fr34AuthorizationRevision,preparedHead.TransportCapacity })) return Results.StatusCode(403);
            state=preparedHead;etag=preparedTag;grant=preparedGrant;
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
                if(grant.ValidUntil<=clock.GetUtcNow() || !await SaveCurrentAsync(installation.Component,installation.StateKey,admitted,etag,
                    new StateOptions { Concurrency=ConcurrencyMode.FirstWrite,Consistency=ConsistencyMode.Strong },cancellationToken:budget.Token).ConfigureAwait(false)) return Results.StatusCode(503);
                state=admitted;await SettleCompletedTicketsAsync(budget.Token).ConfigureAwait(false);
            }
            string? resultPolicy=ownerHandler?.ResultPolicy(method,request.Payload,state);
            string commonResultPolicy=Hash(new { state.EnrollmentId,state.Scope,state.TrustRevision,state.Callers,state.Spool,state.QualificationBindings,state.QualificationCases,state.Fr34Target,state.Fr34RecipeSha256,state.Fr34AuthorizationRevision,state.TransportCapacity });
            var next = JsonSerializer.Deserialize<IndependentAuthorityState>(JsonSerializer.SerializeToUtf8Bytes(state, CustodyBindingContract.Json), CustodyBindingContract.Json)!;
            (object? Payload, bool Mutated) outcome;
            if (method is "replay-register-exact" or "replay-lookup-exact" or "replay-lookup-first-seen" or "security-observation-lookup" or "security-observation-append" or "security-observation-recover-original")
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
                    else if(method=="replay-lookup-first-seen") recordingResult=await recordingSource.LookupFirstSeenAsync(request.Payload.Deserialize<ReplayLookup>(CustodyBindingContract.Json)!.intent,budget.Token).ConfigureAwait(false);
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
            else if (method == "custody-publish-registration-original")
            {
                var original = request.Payload.Deserialize<CustodyRegistrationPublication>(CustodyBindingContract.Json)?.original;
                // Check the current restricted tenant grant before consulting the actual original.
                if (original is null || grant.Role != "custody-registration-publisher" || grant.ScopeReference != original.Identity.TenantId)
                    return Results.StatusCode(403);
                CustodyKeyLifecycleActor.ValidateIdentity(original.Identity);
                if (original.ExpectedRevision < 0 || !new[] { original.OperationId, original.KekVersion, original.OpaqueWrappedKeyReference, original.WrapReceiptId }.All(CustodyBindingContract.CustodyText)
                    || registrationVerifier is null || !await registrationVerifier.VerifyAsync(original, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false))
                    return Results.StatusCode(503);
                // Actual lookup authorization may retain its prepare original on this same head.
                // Compare publication against the surviving head after verification, never that earlier ETag.
                var publisher = grant;
                (state, etag) = await dapr.GetStateAndETagAsync<IndependentAuthorityState>(installation.Component, installation.StateKey,
                    ConsistencyMode.Strong, cancellationToken: budget.Token).ConfigureAwait(false);
                if (state is null || string.IsNullOrWhiteSpace(etag) || state.EnrollmentId != installation.EnrollmentId || state.Scope != installation.Scope
                    || state.TrustRevision != installation.TrustRevision || state.ValidUntil <= clock.GetUtcNow()) return Results.StatusCode(503);
                grant = Authenticate(context, state, method);
                if (grant is null || grant.Identity != publisher.Identity || grant.Role != "custody-registration-publisher"
                    || grant.ScopeReference != original.Identity.TenantId) return Results.StatusCode(403);
                next = JsonSerializer.Deserialize<IndependentAuthorityState>(JsonSerializer.SerializeToUtf8Bytes(state, CustodyBindingContract.Json), CustodyBindingContract.Json)!;
                var matches = state.Registrations.Values.Where(value => value.Identity == original.Identity
                    || value.Identity.TenantId == original.Identity.TenantId && (value.OperationId == original.OperationId || value.OpaqueWrappedKeyReference == original.OpaqueWrappedKeyReference)).ToArray();
                if (matches.Any(value => value != original)) return Results.StatusCode(409);
                if (matches.Length == 0)
                {
                    if (next.Registrations.Count >= 1000) return Results.StatusCode(503);
                    next.Registrations.Add(Hash(original.Identity), original);
                }
                // Even exact recovery crosses the shared CAS/revocation barrier after asynchronous verification.
                outcome = (original, true);
            }
            else if (ownerHandler is { } handler)
            {
                var handling=recoverAdmitted?handler.RecoverAsync(method,request.Payload,grant,state,budget.Token):handler.HandleAsync(method,request.Payload,grant,state,budget.Token);
                IndependentReceivingOwnership.Current.Value?.Track(handling);
                object? handled;
                try { handled=await handling.WaitAsync(budget.Token).ConfigureAwait(false); }
                catch
                {
                    _=handling.ContinueWith(task=> { if(task.Status==TaskStatus.RanToCompletion) handler.ReleaseResult(method,task.Result);else _=task.Exception; },CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
                    throw;
                }
                resultOwner=handler;ownedResult=handled;
                // Fresh framing is returned only after it is ordered against the surviving caller head.
                outcome=(handled,method=="custody-owner-call");
            }
            else if(method=="qualification-retain-result" && request.Payload.TryGetProperty("role",out var resultRole) && PrivateRole(resultRole.GetString()))
            {
                var committed=await CommitPrivateResultAsync(request.Payload,grant,state,etag,budget.Token).ConfigureAwait(false);
                if(!committed.Ready) return Results.StatusCode(503);
                state=committed.State;next=state;outcome=(committed.Payload,false);
            }
            else if(IsPrivateOriginalOperation(method,request.Payload))
                outcome=await ApplyPrivateOriginalAsync(method,request.Payload,grant,state,next,replacement=>next=replacement,budget.Token).ConfigureAwait(false);
            else if(method is "spool-admit-recovery" or "spool-verify-recovery")
                outcome=await ApplyRecoveryAsync(method,request.Payload,grant,state,next,budget.Token).ConfigureAwait(false);
            else if (method is "spool-admit-transition" or "spool-record-transition" or "spool-recover-transition" or "spool-verify-transition")
                outcome = await ApplySpoolTransitionAsync(method, request.Payload.Deserialize<AnchoredStateTransition>(CustodyBindingContract.Json)!, next, replacement => next = replacement, budget.Token).ConfigureAwait(false);
            else outcome = Apply(method, request.Payload, grant, next, replacement => next = replacement,budget.Token);
            if (outcome.Mutated)
            {
                next = next with { Revision = checked(next.Revision + 1) };
                bool admission=method is "spool-admit-transition" or "spool-admit-recovery" or "qualification-begin-original"
                    || method == "custody-publish-registration-original" && next.Registrations.Count > state.Registrations.Count;
                long headBound=32L*1024*1024-(admission?installation.CompletionReserveBytes:0);
                if (headBound<=0 || JsonSerializer.SerializeToUtf8Bytes(next, CustodyBindingContract.Json).LongLength > headBound)
                    return Results.StatusCode(503); // Retained facts are never evicted to admit work.
                if (method is "recording-dispatch-admit" or "recording-recovery-dispatch-admit" or "recording-replay-recovery-dispatch-admit"
                    && !DispatchStillCurrent(request.Payload,next)) return Results.StatusCode(403);
                bool saved;
                if(method=="spool-admit-recovery" && outcome.Payload is SecurityObservationRecoveryGrant recovering && Capacity is { } recoveryPool
                    && !await new IndependentRecoveryGrantArchive(CurrentArchiveDirectory(budget.Token),installation,signer,recoveryPool).ContainsAsync(recovering,state.Revision,budget.Token).ConfigureAwait(false))
                    saved=await AdmitPhysicalHeadAsync(recoveryPool,recoveryPool.Ticket("recovery-grant",Hash(recovering)),next,etag,grant,async (head,ct)=>
                    {
                        bool archived=await new IndependentRecoveryGrantArchive(CurrentArchiveDirectory(ct),installation,signer,recoveryPool).ContainsAsync(recovering,head.Revision,ct).ConfigureAwait(false);
                        bool retained=head.Recoveries.Values.Any(value=>Same(value,recovering));return (retained || archived,!retained && !archived);
                    },budget.Token).ConfigureAwait(false);
                else saved=grant.ValidUntil>clock.GetUtcNow() && next.ValidUntil>clock.GetUtcNow()
                    && await SaveCurrentAsync(installation.Component,installation.StateKey,next,etag,
                        new StateOptions { Concurrency=ConcurrencyMode.FirstWrite,Consistency=ConsistencyMode.Strong },cancellationToken:budget.Token).ConfigureAwait(false);
                if(!saved) return Results.StatusCode(503);
            }
            if(outcome.Mutated) await SettleCompletedTicketsAsync(budget.Token).ConfigureAwait(false);
            if(method=="qualification-begin-original" && Capacity is { } admittedPool)
                await admittedPool.CommitAsync(admittedPool.Ticket(request.Payload.GetProperty("role").GetString()!,request.Payload.GetProperty("requestId").GetString()!),next.Revision,budget.Token).ConfigureAwait(false);
            if(method=="qualification-retain-result" && outcome.Payload is not null && Capacity is { } completedPool)
                await completedPool.SettleAsync(completedPool.Ticket(request.Payload.GetProperty("role").GetString()!,request.Payload.GetProperty("requestId").GetString()!),budget.Token).ConfigureAwait(false);
            if (method is "spool-record-transition" or "spool-recover-transition"
                && outcome.Payload is Permission { Allowed: true })
            {
                var completed = request.Payload.Deserialize<AnchoredStateTransition>(CustodyBindingContract.Json)!;
                await WriteSpoolOriginalAsync("recorded", completed, false, budget.Token).ConfigureAwait(false);
                // Release only after exact durable publication; failure keeps the reservation recoverable.
                var (settled,settledTag)=await dapr.GetStateAndETagAsync<IndependentAuthorityState>(installation.Component,installation.StateKey,ConsistencyMode.Strong,cancellationToken:budget.Token).ConfigureAwait(false);
                if(settled?.SpoolPublication is { } liability && liability.TransitionSha256==Hash(completed) && settled.SpoolRevision>=completed.TargetRevision)
                    if(!await SaveCurrentAsync(installation.Component,installation.StateKey,settled with { Revision=checked(settled.Revision+1),SpoolPublication=null },settledTag,
                        new StateOptions { Concurrency=ConcurrencyMode.FirstWrite,Consistency=ConsistencyMode.Strong },cancellationToken:budget.Token).ConfigureAwait(false)) return Results.StatusCode(503);
            }
            if(method is "spool-record-transition" or "spool-recover-transition" && outcome.Payload is Permission { Allowed:true } && Capacity is { } spoolPool)
                await spoolPool.SettleAsync(spoolPool.Ticket("spool",Hash(request.Payload.Deserialize<AnchoredStateTransition>(CustodyBindingContract.Json)!)),budget.Token).ConfigureAwait(false);
            if(method=="spool-admit-recovery" && outcome.Payload is SecurityObservationRecoveryGrant issued)
                await new IndependentRecoveryGrantArchive(CurrentArchiveDirectory(budget.Token),installation,signer,Capacity).PublishAsync(issued,next.Revision,budget.Token).ConfigureAwait(false);
            // Direct decisions can consult nested workload grants, dispatch originals or exact
            // registered source facts. Compare the semantic facts actually used, allowing only
            // this operation's own acknowledged mutation and unrelated authority ordering revisions.
            string? directResultPolicy=ownerHandler is null?DirectResultPolicy(outcome.Mutated?next:state):null;
            // Every newly signed result is ordered after the last consulted asynchronous dependency.
            {
                var (current,currentTag)=await dapr.GetStateAndETagAsync<IndependentAuthorityState>(installation.Component,installation.StateKey,ConsistencyMode.Strong,cancellationToken:budget.Token).ConfigureAwait(false);
                if(current is null || current.EnrollmentId!=installation.EnrollmentId || current.Scope!=installation.Scope || current.TrustRevision!=installation.TrustRevision
                    || current.ValidUntil<=clock.GetUtcNow() || string.IsNullOrWhiteSpace(currentTag)) return Results.StatusCode(503);
                var currentGrant=Authenticate(context,current,method);
                if(currentGrant is null || !Same(currentGrant,grant) || directResultPolicy is not null && directResultPolicy!=DirectResultPolicy(current) || commonResultPolicy!=Hash(new { current.EnrollmentId,current.Scope,current.TrustRevision,current.Callers,current.Spool,current.QualificationBindings,current.QualificationCases,current.Fr34Target,current.Fr34RecipeSha256,current.Fr34AuthorizationRevision,current.TransportCapacity }) || resultPolicy!=ownerHandler?.ResultPolicy(method,request.Payload,current)) return Results.StatusCode(403);
                if(!DirectTimePolicyCurrent(method,request.Payload,currentGrant,current,outcome.Payload,budget.Token) || capacityAdmission && !UsableTransportCapacity(current) || method.StartsWith("spool-",StringComparison.Ordinal) && current.Spool is { } spoolBefore && spoolBefore.ValidUntil<=clock.GetUtcNow()) return Results.StatusCode(403);
                // Result issuance gets its own ordering point, without caching a frame nonce.
                if(!await SaveCurrentAsync(installation.Component,installation.StateKey,current with { Revision=checked(current.Revision+1) },currentTag,
                    new StateOptions { Concurrency=ConcurrencyMode.FirstWrite,Consistency=ConsistencyMode.Strong },cancellationToken:budget.Token).ConfigureAwait(false)) return Results.StatusCode(503);
                // The conditional save itself may await storage. A consulted nested
                // grant can expire without changing head bytes while that save waits.
                if(!DirectTimePolicyCurrent(method,request.Payload,currentGrant,current,outcome.Payload,budget.Token) || resultPolicy!=ownerHandler?.ResultPolicy(method,request.Payload,current) || capacityAdmission && !UsableTransportCapacity(current)
                    || method.StartsWith("spool-",StringComparison.Ordinal) && current.Spool is { } spoolAfter && spoolAfter.ValidUntil<=clock.GetUtcNow()) return Results.StatusCode(403);
                state=current;grant=currentGrant;
            }
            budget.Token.ThrowIfCancellationRequested();
            long now = clock.GetUtcNow().ToUnixTimeSeconds();
            if (grant.ValidUntil <= clock.GetUtcNow() || state.ValidUntil <= clock.GetUtcNow()) return Results.StatusCode(403);
            if(method.EndsWith("dispatch-admit",StringComparison.Ordinal) && outcome.Payload is Permission { Allowed:true })
            {
                // The conditional write can itself await transport. Expiry after an acknowledged
                // admission leaves that original intact, but cannot grant a new workload dispatch.
                var bound=request.Payload.GetProperty("boundRequest").Deserialize<Hexalith.EventStore.Contracts.Commands.BoundWorkloadCommandRequest>(CustodyBindingContract.Json)!;
                string role=bound.Command.Domain=="fr34-canary"?"fr34-engine":bound.Command.Domain=="trusted-envelope-replay"?"private-replay":"private-security-observation";
                if(DispatchBinding(request.Payload,state,role=="fr34-engine"?Fr34CommandFingerprint.Create(bound.Command,bound.PrincipalId):bound.Command.Payload,bound.Command.MessageId,role) is null) return Results.StatusCode(403);
            }
            byte[] payloadBytes=JsonSerializer.SerializeToUtf8Bytes(outcome.Payload,CustodyBindingContract.Json);
            try
            {
                using var payloadDocument=JsonDocument.Parse((ReadOnlyMemory<byte>)payloadBytes);
                var workloadGrant=outcome.Payload is CustodyRoleIssuance issuedWorkload?issuedWorkload.Grant:outcome.Payload as CustodyRoleWorkloadGrant;
                if(workloadGrant is not null && workloadGrant.ValidUntil<=clock.GetUtcNow()) return Results.StatusCode(403);
                long responseUntil=Math.Min(now+30,Math.Min(state.ValidUntil.ToUnixTimeSeconds(),grant.ValidUntil.ToUnixTimeSeconds()));
                if(workloadGrant is not null) responseUntil=Math.Min(responseUntil,workloadGrant.ValidUntil.ToUnixTimeSeconds());
                var result=new Response(installation.EnrollmentId,installation.Scope,installation.TrustRevision,method,request.Nonce,
                    Convert.ToHexString(SHA256.HashData(wire)),now,responseUntil,payloadDocument.RootElement,[]);
                byte[] signingBytes=JsonSerializer.SerializeToUtf8Bytes(result,CustodyBindingContract.Json);
                try { lock(signer) result=result with { Signature=signer.SignData(signingBytes,HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence) }; }
                finally { if(resultOwner is not null) CryptographicOperations.ZeroMemory(signingBytes); }
                byte[] resultBytes=JsonSerializer.SerializeToUtf8Bytes(result,CustodyBindingContract.Json);
                if(resultBytes.Length>=CustodyBindingContract.MaximumIndependentAuthorityFrameBytes)
                { if(resultOwner is not null) CryptographicOperations.ZeroMemory(resultBytes);return Results.StatusCode(503); }
                var response=new OwnedResponse(resultBytes);context.Response.RegisterForDispose(response);return response;
            }
            finally { if(resultOwner is not null) CryptographicOperations.ZeroMemory(payloadBytes); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            IndependentReceivingDiagnostics.Record(error is IOException?IndependentReceivingFailure.StorageFailure:error is JsonException or ArgumentException?(validFrame?IndependentReceivingFailure.MalformedEnrolledState:IndependentReceivingFailure.MalformedRequest):IndependentReceivingFailure.HandlerFailure);
            return Results.StatusCode(503);
        }
        finally { _completedPhysicalTickets.Value=null;resultOwner?.ReleaseResult(method,ownedResult);if(requestWire is not null) CryptographicOperations.ZeroMemory(requestWire); }
    }


    private bool DirectTimePolicyCurrent(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState state,object? result,CancellationToken token)
    {
        if(method is "qualification-authenticate" or "qualification-authorize" or "custody-role-authorize")
        {
            // These three decisions are pure in-memory policy evaluation: no original/native read or effect is repeated.
            var refreshed=Apply(method,payload,caller,state,_=>throw new InvalidOperationException("Pure result policy cannot mutate authority."),token);
            return Same(refreshed.Payload,result);
        }
        if(method is "recording-command-authorize" or "fr34-command-authorize" or "recording-recovery-command-authorize" or "recording-replay-recovery-command-authorize")
        {
            var decision=JsonSerializer.SerializeToElement(result,CustodyBindingContract.Json);
            if(!decision.TryGetProperty("Allowed",out var allowed) || !allowed.GetBoolean()) return true;
            string identity=payload.GetProperty("caller").GetString()!,principal=payload.GetProperty("principal").GetString()!,domain=payload.GetProperty("command").GetProperty("Domain").GetString()!;
            if(!state.Callers.Any(value=>value.Identity==identity && value.ScopeReference==principal && !value.Revoked && value.ValidUntil>clock.GetUtcNow()
                && value.Methods.Contains("recording-source:"+(domain=="agents"?"legacy-security-event-log":domain),StringComparer.Ordinal))) return false;
            if(payload.TryGetProperty("grant",out var recovery) && recovery.Deserialize<SecurityObservationRecoveryGrant>(CustodyBindingContract.Json) is { } grant && grant.ValidUntil<=clock.GetUtcNow()) return false;
        }
        return true;
    }

    private static string DirectResultPolicy(IndependentAuthorityState state)=>Hash(new {
        state.Callers,state.Spool,state.SpoolRevision,state.SpoolDigest,state.Admitted,state.Recorded,state.Recoveries,
        state.Originals,state.PrivateOriginalsRoot,state.PrivateOriginalsCount,state.Registrations,state.Manifests,
        state.QualificationBindings,state.QualificationCases,state.Fr34Target,state.Fr34RecipeSha256,state.Fr34AuthorizationRevision,state.TransportCapacity });

    private async Task<bool> SaveCurrentAsync(string component,string key,IndependentAuthorityState value,string etag,StateOptions? options=null,IReadOnlyDictionary<string,string>? metadata=null,CancellationToken cancellationToken=default)
    {
        bool saved=await dapr.TrySaveStateAsync(component,key,value,etag,options,metadata,cancellationToken).ConfigureAwait(false);
        if(!saved) IndependentReceivingDiagnostics.Record(IndependentReceivingFailure.ConditionalWriteConflict);
        return saved;
    }

    // Wire bytes belong to the receiving response through transport completion/failure.
    private sealed class OwnedResponse(byte[] bytes):IResult,IDisposable
    {
        public async Task ExecuteAsync(HttpContext context)
        { try { await Results.Bytes(bytes,"application/json").ExecuteAsync(context).ConfigureAwait(false); }finally { Dispose(); } }
        public void Dispose()=>CryptographicOperations.ZeroMemory(bytes);
    }

    private async Task SettleCompletedTicketsAsync(CancellationToken token)
    {
        if(_completedPhysicalTickets.Value is { Count:>0 } tickets && Capacity is { } pool)
        { foreach(string ticket in tickets) await pool.SettleAsync(ticket,token).ConfigureAwait(false);tickets.Clear(); }
    }
    internal Func<IndependentArchiveCapacityInstallation,string,string,IndependentArchiveCapacity>? CapacityFactory { get; init; }
    private IndependentArchiveCapacity? Capacity=>_archive is IndependentArchiveStorage
        ?(CapacityFactory??((config,directory,owner)=>new(config,directory,owner)))(installation.ArchiveCapacity??throw new IOException("External.EnrolledSharedIndependentArchivePhysicalCapacityV1"),
            installation.OriginalArchiveDirectory??throw new IOException("Enrolled archive required."),Hash(new { installation.Component,installation.StateKey,installation.EnrollmentId })):null;
    private async Task ProtectPendingAsync(IndependentAuthorityState state,CancellationToken token)
    {
        if(Capacity is { } pool)
        {
            if(state.OriginalLiabilityVersion!=1 || state.PendingOriginalCompletionBytes is null) throw new IOException("Exact pending liability reconciliation required before physical admission.");
            var exactPending=new HashSet<string>(StringComparer.Ordinal);
            var index=new IndependentPrivateOriginalIndex(PrivateArchive(token));
            foreach(var pair in await pool.OwnerTicketsAsync(token).ConfigureAwait(false))
            {
                var ticket=pair.Value;if(ticket.Role is null || ticket.RequestId is null || ticket.Completion==0) continue;
                var original=(await index.ReadAsync(state.PrivateOriginalsRoot,ticket.Role,ticket.RequestId,token).ConfigureAwait(false))?.Original??state.Originals.GetValueOrDefault(ticket.Role+":"+ticket.RequestId);
                bool pending=original is { Result:null,FinalAtAdmission:false };
                var publication=state.OriginalPublications?.GetValueOrDefault(PublicationKey(ticket.Role,ticket.RequestId,"request"));
                if(pending && OriginalCommitment(ticket.Role,ticket.RequestId,original!.Request,false)==ticket.CommitmentSha256
                    || publication is { CompletionBytes:>0 } && OriginalPublicationCommitment(publication)==ticket.CommitmentSha256) exactPending.Add(pair.Key);
            }
            await pool.EnsureLegacyAsync(state.PendingOriginalCompletionBytes.Value,state.Revision,token,exactPending).ConfigureAwait(false);
        }
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
        async Task<bool> Save(IndependentAuthorityState next,string? physicalTicket=null,IndependentOriginalPublication? publication=null)
        {
            next=next with { Revision=checked(state.Revision+1) };
            if(JsonSerializer.SerializeToUtf8Bytes(next,CustodyBindingContract.Json).Length>32*1024*1024) return false;
            bool saved;
            if(physicalTicket is not null && publication is not null && Capacity is { } physicalPool)
                saved=await AdmitPhysicalHeadAsync(physicalPool,physicalTicket,next,etag,caller,async (head,ct)=>
                {
                    var retained=(await new IndependentPrivateOriginalIndex(PrivateArchive(ct)).ReadAsync(head.PrivateOriginalsRoot,role,id,ct).ConfigureAwait(false))?.Original??head.Originals.GetValueOrDefault(role+":"+id);
                    var prepared=head.OriginalPublications?.Values.Where(value=>value.Role==role && value.RequestId==id).ToArray()??[];
                    return (retained is not null || prepared.Any(value=>value==publication),retained is null && prepared.Length==0);
                },token).ConfigureAwait(false);
            else saved=await SaveCurrentAsync(installation.Component,installation.StateKey,next,etag,new StateOptions { Concurrency=ConcurrencyMode.FirstWrite,Consistency=ConsistencyMode.Strong },cancellationToken:token).ConfigureAwait(false);
            if(!saved) return false;
            (state,etag)=await dapr.GetStateAndETagAsync<IndependentAuthorityState>(installation.Component,installation.StateKey,ConsistencyMode.Strong,cancellationToken:token).ConfigureAwait(false);
            if(state is null || string.IsNullOrWhiteSpace(etag) || state.ValidUntil<=clock.GetUtcNow()) throw new IOException("Current original publication reservation unavailable.");return true;
        }
        if(state.PendingOriginalCompletionBytes is null || state.OriginalLiabilityVersion!=1)
        {
            var initialized=JsonSerializer.Deserialize<IndependentAuthorityState>(JsonSerializer.SerializeToUtf8Bytes(state,CustodyBindingContract.Json),CustodyBindingContract.Json)!;
            var result=await ApplyPrivateOriginalAsync("private-original-initialize",payload,caller,state,initialized,value=>initialized=value,token).ConfigureAwait(false);
            if(!await Save(initialized).ConfigureAwait(false) || result.Payload is not Permission { Allowed:true }) return (state,etag,false);
        }
        var index=new IndependentPrivateOriginalIndex(PrivateArchive(token));var existing=(await index.ReadAsync(state.PrivateOriginalsRoot,role,id,token).ConfigureAwait(false))?.Original??state.Originals.GetValueOrDefault(role+":"+id);
        if((existing is not null || state.OriginalPublications?.ContainsKey(PublicationKey(role,id,"request"))==true) && Capacity is { } existingPool) await existingPool.CommitAsync(existingPool.Ticket(role,id),state.Revision,token).ConfigureAwait(false);
        await ProtectPendingAsync(state,token).ConfigureAwait(false);
        if(begin && existing is not null || !begin && existing?.Result is not null)
        {
            if((existing is { Result:not null } || existing is { FinalAtAdmission:true }) && Capacity is { } completedPool) await completedPool.SettleAsync(completedPool.Ticket(role,id),token).ConfigureAwait(false);
            return (state,etag,true);
        }
        if(!begin && existing is null) return (state,etag,false);
        byte[] body=JsonSerializer.SerializeToUtf8Bytes(payload.GetProperty(begin?"original":"result"),CustodyBindingContract.Json);
        if(body.Length>(begin?CustodyBindingContract.MaximumOriginalBytes/2:CustodyBindingContract.MaximumOriginalBytes)) return (state,etag,false);
        string kind=begin?"request":"result",key=PublicationKey(role,id,kind);var publications=state.OriginalPublications??new();
        var reserved=new IndependentOriginalPublication(role,id,kind,CustodyBindingContract.Hash(body),begin?2L*CustodyBindingContract.MaximumOriginalBytes:0,begin && !final?OriginalCompletionBytes:0,final);
        if(publications.TryGetValue(key,out var prior))
        {
            if(prior!=reserved) return (state,etag,false);
            if(begin && Capacity is { } retryPool) await retryPool.ReserveAsync(retryPool.Ticket(role,id),checked(reserved.ReservedBytes+reserved.CompletionBytes+1024*1024),reserved.CompletionBytes,token,OriginalPublicationCommitment(reserved),role,id).ConfigureAwait(false);
        }
        else
        {
            if(publications.Count>=128 || AvailableArchiveBytes(CurrentArchiveDirectory(token),token)<checked(state.PendingOriginalCompletionBytes!.Value+(state.SpoolPublication?.ReservedBytes??0)+publications.Values.Sum(value=>value.ReservedBytes)+reserved.ReservedBytes+reserved.CompletionBytes+installation.CompletionReserveBytes)) return (state,etag,false);
            var nextPublications=new Dictionary<string,IndependentOriginalPublication>(publications,StringComparer.Ordinal) { [key]=reserved };
            var nextHead=state with { PendingOriginalCompletionBytes=checked(state.PendingOriginalCompletionBytes.Value+reserved.CompletionBytes),OriginalPublications=nextPublications };
            if(JsonSerializer.SerializeToUtf8Bytes(nextHead,CustodyBindingContract.Json).Length>32*1024*1024) return (state,etag,false);
            string? physicalTicket=null;
            if(begin && Capacity is { } pool)
            {
                physicalTicket=pool.Ticket(role,id);
                await pool.ReserveAsync(physicalTicket,checked(reserved.ReservedBytes+reserved.CompletionBytes+1024*1024),reserved.CompletionBytes,token,OriginalPublicationCommitment(reserved),role,id).ConfigureAwait(false);
            }
            if(!await Save(nextHead,physicalTicket,reserved).ConfigureAwait(false)) return (state,etag,false);
            publications=state.OriginalPublications!;
        }
        long own=begin?reserved.CompletionBytes:existing is { Result:null,FinalAtAdmission:false }?OriginalCompletionBytes:0;
        long required=checked(state.PendingOriginalCompletionBytes!.Value-own+(state.SpoolPublication?.ReservedBytes??0)+publications.Values.Sum(value=>value.ReservedBytes)-reserved.ReservedBytes+body.LongLength+1024*1024+installation.CompletionReserveBytes);
        if(AvailableArchiveBytes(CurrentArchiveDirectory(token),token)<required) return (state,etag,false);
        await ValidateArchiveWriteAsync(CurrentArchiveDirectory(token),begin,token).ConfigureAwait(false);return (state,etag,true);
    }
    private static string OriginalCommitment(string role,string id,JsonElement request,bool terminal)=>Hash(new { role,id,bodySha256=CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(request,CustodyBindingContract.Json)),terminal });
    private static string OriginalPublicationCommitment(IndependentOriginalPublication value)=>Hash(new { role=value.Role,id=value.RequestId,bodySha256=value.BodySha256,terminal=value.FinalAtAdmission });
    private async Task<bool> AdmitPhysicalHeadAsync(IndependentArchiveCapacity pool,string ticket,IndependentAuthorityState next,string etag,IndependentCallerGrant grant,
        Func<IndependentAuthorityState,CancellationToken,Task<(bool Admitted,bool Absent)>> classify,CancellationToken token)
        =>await pool.AdmitHeadAsync(ticket,async ct=>
        {
            try
            {
                if(grant.ValidUntil>clock.GetUtcNow())
                    _=await SaveCurrentAsync(installation.Component,installation.StateKey,next,etag,new StateOptions { Concurrency=ConcurrencyMode.FirstWrite,Consistency=ConsistencyMode.Strong },cancellationToken:ct).ConfigureAwait(false);
            }
            catch(Exception) when(!ct.IsCancellationRequested) { } // An ambiguous acknowledgment must be independently looked up while admission remains fenced.
            var (head,headTag)=await dapr.GetStateAndETagAsync<IndependentAuthorityState>(installation.Component,installation.StateKey,ConsistencyMode.Strong,cancellationToken:ct).ConfigureAwait(false);
            if(head is null || string.IsNullOrWhiteSpace(headTag) || head.EnrollmentId!=installation.EnrollmentId || head.Scope!=installation.Scope || head.TrustRevision!=installation.TrustRevision)
                return (false,false,0);
            var exact=await classify(head,ct).ConfigureAwait(false);
            if(exact.Admitted || !exact.Absent) return (exact.Admitted,false,head.Revision);
            // A read of absence cannot cancel a delayed old-etag admission. This acknowledged
            // conditional barrier invalidates that exact old etag before the pool may reclaim.
            bool fenced;
            try { fenced=await SaveCurrentAsync(installation.Component,installation.StateKey,head with { Revision=checked(head.Revision+1) },headTag,
                new StateOptions { Concurrency=ConcurrencyMode.FirstWrite,Consistency=ConsistencyMode.Strong },cancellationToken:ct).ConfigureAwait(false); }
            catch(Exception) when(!ct.IsCancellationRequested) { return (false,false,head.Revision); }
            if(!fenced) return (false,false,head.Revision);
            var (after,afterTag)=await dapr.GetStateAndETagAsync<IndependentAuthorityState>(installation.Component,installation.StateKey,ConsistencyMode.Strong,cancellationToken:ct).ConfigureAwait(false);
            if(after is null || string.IsNullOrWhiteSpace(afterTag) || after.EnrollmentId!=installation.EnrollmentId || after.Scope!=installation.Scope || after.TrustRevision!=installation.TrustRevision)
                return (false,false,head.Revision);
            var final=await classify(after,ct).ConfigureAwait(false);return (final.Admitted,final.Absent,after.Revision);
        },token).ConfigureAwait(false);
    private const long OriginalCompletionBytes=4L*CustodyBindingContract.MaximumOriginalBytes+1024*1024;
    private async Task<(IndependentAuthorityState State,string ETag,bool Ready,object? Payload)> CommitPrivateResultAsync(JsonElement payload,
        IndependentCallerGrant caller,IndependentAuthorityState state,string etag,CancellationToken token)
    {
        var request=payload.Deserialize<RetainResult>(CustodyBindingContract.Json)!;
        var index=new IndependentPrivateOriginalIndex(PrivateArchive(token));
        var original=request.role==caller.Role?(await index.ReadAsync(state.PrivateOriginalsRoot,request.role,request.requestId,token).ConfigureAwait(false))?.Original
            ??state.Originals.GetValueOrDefault(request.role+":"+request.requestId):null;
        if(original is null || original.FinalAtAdmission) return (state,etag,true,new { Exact=false,First=false });
        if(original.Result is { } recorded) return (state,etag,true,new { Exact=Same(recorded,request.result),First=false });
        var reservation=state.OriginalPublications?.GetValueOrDefault(PublicationKey(request.role,request.requestId,"result"));
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(request.result,CustodyBindingContract.Json);
        if(reservation is null || reservation.BodySha256!=CustodyBindingContract.Hash(bytes) || state.PrivateResultCommit is not null)
            return (state,etag,false,null);
        string? ticket=null;
        if(Capacity is { } pool)
        {
            ticket=pool.Ticket(request.role,request.requestId);
            await pool.ClaimLegacyAsync(ticket,OriginalCompletionBytes,state.Revision,token,OriginalCommitment(request.role,request.requestId,original.Request,false),request.role,request.requestId).ConfigureAwait(false);
        }
        // Only this stable body is published before CAS. Losing attempts spend no revision- or
        // root-dependent frames, and every retry uses the admitted body's same content address.
        string hash=await PrivateArchive(token,ticket,true).PublishAsync(bytes,token).ConfigureAwait(false);
        var commit=new IndependentOriginalResultCommit(request.role,request.requestId,hash,Hash(original),state.PrivateOriginalsRoot,checked(state.Revision+1));
        var committed=state with { Revision=commit.Revision,PrivateResultCommit=commit };
        if(caller.ValidUntil<=clock.GetUtcNow() || state.ValidUntil<=clock.GetUtcNow()
            || !await SaveCurrentAsync(installation.Component,installation.StateKey,committed,etag,new StateOptions { Concurrency=ConcurrencyMode.FirstWrite,Consistency=ConsistencyMode.Strong },cancellationToken:token).ConfigureAwait(false))
            return (state,etag,false,null);
        // The commit's successful authority revision is the result's original retained revision.
        // Materialization may cross later unrelated ordering revisions without rewriting that fact.
        var (current,currentTag)=await dapr.GetStateAndETagAsync<IndependentAuthorityState>(installation.Component,installation.StateKey,ConsistencyMode.Strong,cancellationToken:token).ConfigureAwait(false);
        if(current is null || string.IsNullOrWhiteSpace(currentTag)) return (state,etag,false,null);
        var materialized=await MaterializePrivateResultAsync(current,currentTag,token).ConfigureAwait(false);
        return (materialized.State,materialized.ETag,materialized.Ready,new { Exact=true,First=true });
    }
    private async Task<(IndependentAuthorityState State,string ETag,bool Ready)> MaterializePrivateResultAsync(IndependentAuthorityState state,string etag,CancellationToken token)
    {
        if(state.PrivateResultCommit is not { } commit) return (state,etag,true);
        if(state.EnrollmentId!=installation.EnrollmentId || state.Scope!=installation.Scope || state.TrustRevision!=installation.TrustRevision
            || !PrivateRole(commit.Role) || !CustodyBindingContract.Text(commit.RequestId) || !CustodyBindingContract.Digest(commit.ResultSha256)
            || commit.Revision<=0 || commit.Revision>state.Revision || commit.IndexRoot!=state.PrivateOriginalsRoot || state.OriginalLiabilityVersion!=1)
            throw new IOException("Exact committed result/index basis required; uncertain completion remains owned.");
        var index=new IndependentPrivateOriginalIndex(PrivateArchive(token));
        var original=(await index.ReadAsync(commit.IndexRoot,commit.Role,commit.RequestId,token).ConfigureAwait(false))?.Original??state.Originals.GetValueOrDefault(commit.Role+":"+commit.RequestId);
        var publication=state.OriginalPublications?.GetValueOrDefault(PublicationKey(commit.Role,commit.RequestId,"result"));
        if(original is not { Result:null,FinalAtAdmission:false } || Hash(original)!=commit.OriginalSha256 || publication?.BodySha256!=commit.ResultSha256
            || state.PendingOriginalCompletionBytes is null || state.PendingOriginalCompletionBytes<OriginalCompletionBytes)
            throw new IOException("Exact committed result original and liability required.");
        using var result=JsonDocument.Parse(await PrivateArchive(token).ReadAsync(commit.ResultSha256,token).ConfigureAwait(false));
        string? ticket=Capacity?.Ticket(commit.Role,commit.RequestId);
        var writer=new IndependentPrivateOriginalIndex(PrivateArchive(token,ticket,true));
        var stored=await writer.SetAsync(commit.IndexRoot,commit.Role,commit.RequestId,original with { Result=result.RootElement.Clone(),ResultRetainedRevision=commit.Revision },token).ConfigureAwait(false);
        var publications=new Dictionary<string,IndependentOriginalPublication>(state.OriginalPublications!,StringComparer.Ordinal);
        publications.Remove(PublicationKey(commit.Role,commit.RequestId,"result"));
        var originals=new Dictionary<string,IndependentOriginal>(state.Originals,StringComparer.Ordinal);originals.Remove(commit.Role+":"+commit.RequestId);
        var next=state with { Revision=checked(state.Revision+1),PrivateOriginalsRoot=stored.Root,PrivateOriginalsCount=checked(state.PrivateOriginalsCount+(stored.First?1:0)),
            PendingOriginalCompletionBytes=state.PendingOriginalCompletionBytes.Value-OriginalCompletionBytes,OriginalPublications=publications,Originals=originals,PrivateResultCommit=null };
        if(!await SaveCurrentAsync(installation.Component,installation.StateKey,next,etag,new StateOptions { Concurrency=ConcurrencyMode.FirstWrite,Consistency=ConsistencyMode.Strong },cancellationToken:token).ConfigureAwait(false))
            return (state,etag,false);
        if(Capacity is { } completedPool) await completedPool.SettleAsync(ticket!,token).ConfigureAwait(false);
        var (current,currentTag)=await dapr.GetStateAndETagAsync<IndependentAuthorityState>(installation.Component,installation.StateKey,ConsistencyMode.Strong,cancellationToken:token).ConfigureAwait(false);
        return current is null || string.IsNullOrWhiteSpace(currentTag)?(next,etag,false):(current,currentTag,current.PrivateResultCommit is null);
    }
    private static string PublicationKey(string role,string id,string kind)=>Hash(new { role,id,kind });
    private static bool TerminalAtAdmission(string role,string id)=>role=="observer" && (id.StartsWith("native-receipt-v1:",StringComparison.Ordinal) || id.StartsWith("native-lookup-witness-v1:",StringComparison.Ordinal)) || role=="private-export-delivery-denial-v1" && (id.StartsWith("receiving-denial-v1:",StringComparison.Ordinal) && id.Length==84 && id[20..].All(char.IsAsciiHexDigitLower) || id.StartsWith("receiving-rejection-v2:",StringComparison.Ordinal) && id.Length==87 && id[23..].All(char.IsAsciiHexDigitLower)) || role=="receiver-admission" && id.StartsWith("receiver:",StringComparison.Ordinal) || role=="fr34-recipes" && (id.EndsWith(":encryption",StringComparison.Ordinal) || id.EndsWith(":completion",StringComparison.Ordinal))
        || role=="occurrence-read-use" && id.Contains(":release:",StringComparison.Ordinal) || role=="guarded-transaction" && id.StartsWith("cell:",StringComparison.Ordinal);
    private IIndependentContentArchive PrivateArchive(CancellationToken token,string? ticket=null,bool completing=false)
    {
        if(privateOriginalArchive is not null) return privateOriginalArchive;
        string directory=Path.Combine(CurrentArchiveDirectory(token),"private-originals",Hash(new { installation.EnrollmentId,installation.Scope,installation.TrustRevision }));
        // Exact historical reads do not acquire or spend a new physical completion ticket.
        if(ticket is null) return new IndependentFileContentArchive(directory);
        return Capacity is { } pool?new IndependentCapacityContentArchive(directory,pool,ticket,completing):new IndependentFileContentArchive(directory);
    }

    private (object? Payload, bool Mutated) Apply(string method, JsonElement payload, IndependentCallerGrant caller, IndependentAuthorityState state, Action<IndependentAuthorityState> replace,CancellationToken token)
    {
        T Read<T>() => payload.Deserialize<T>(CustodyBindingContract.Json) ?? throw new InvalidOperationException("Closed request required.");
        object Decision(bool value) => new { Allowed = value };
        bool Target(ReplicatedSecuritySpoolTarget target) => target == state.Spool && target.ValidUntil > clock.GetUtcNow();
        switch (method)
        {
            case "replay-read-current-ready": return (Decision(recordingSource is not null && caller.Role=="replay-registrar"
                && new[]{"replay-register-exact","replay-lookup-exact","replay-lookup-first-seen"}.All(value=>caller.Methods.Contains(value,StringComparer.Ordinal))),false);
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
                    || !Same(dispatchOriginal.Request, dispatch.original) || dispatchOriginal.SubmissionState != "Unsent" || DispatchBinding(payload,state,dispatch.original,dispatch.requestId,dispatch.role) is not { } dispatchBinding)
                    return (Decision(false), false);
                state.Originals[dispatchId] = dispatchOriginal with { SubmissionState = "Dispatched",DispatchBinding=dispatchBinding };
                return (Decision(true), true);
            case "fr34-command-authorize":
                var canary = Read<RecordingCommandAuthorization>();
                var canaryCommand = canary.command;
                bool canaryExact = state.Originals.TryGetValue("fr34-engine:" + canaryCommand.MessageId, out var canaryOriginal)
                    && Fr34CommandFingerprint.Matches(canaryOriginal.Request,canaryCommand,canary.principal);
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
                var workload=state.Callers.SingleOrDefault(c=>c.BearerSha256==roleRequest.bearerSha256 && !c.Revoked && c.ValidUntil>clock.GetUtcNow()
                    && c.Role==roleRequest.role && c.ScopeReference==roleRequest.intent.TenantId+"|"+roleRequest.intent.OwnerPurposeScope
                    && c.Methods.Contains("custody-intent:"+roleRequest.intent.Operation,StringComparer.Ordinal));
                if(workload is null || roleRequest.method is not ("prepare" or "authorize" or "observe")) return (null,false);
                var currentWorkload=new CustodyRoleWorkloadGrant(workload.Identity,workload.BearerSha256,workload.Role,workload.ScopeReference,Hash(roleRequest.intent),Hash(workload),
                    workload.ValidUntil<state.ValidUntil?workload.ValidUntil:state.ValidUntil);
                if(roleRequest.grant is null) return (currentWorkload,false);
                if(!Same(currentWorkload,roleRequest.grant) || !CustodyBindingContract.Digest(roleRequest.responseSha256)) return (null,false);
                // The issuance CAS shares revocation's surviving head and must win before any role response is released.
                return (new CustodyRoleIssuance(currentWorkload,roleRequest.method,roleRequest.responseSha256!),true);
            case "qualification-observation-original-preflight":
                if(caller.Role!="observer" || payload.GetProperty("role").GetString()!="observer"
                    || !new[]{"qualification-begin-original","qualification-retain-result","qualification-lookup-original"}.All(required=>caller.Methods.Contains(required,StringComparer.Ordinal))) return (new Permission(false),false);
                return (new Permission(ObservationArchiveReady(state,token)),false);
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
    private static bool PrivateRole(string? role)=>role is "private-export-delivery-denial-v1" or "receiver-admission" or "private-replay" or "private-security-observation" or "fr34-engine" or "fr34-observer" or "executor" or "observer" or "enrollment" or "joint-phase-secret" or "joint-phase-command" or "fr34-recipes" or "occurrence-read-use" or "guarded-transaction";
    private static bool IsPrivateOriginalOperation(string method,JsonElement payload)=>method is "recording-replay-recovery-command-authorize" or "recording-replay-recovery-dispatch-admit" or "recording-command-authorize" or "recording-recovery-command-authorize" or "recording-recovery-dispatch-admit" or "fr34-command-authorize"
        || method is "qualification-begin-original" or "qualification-retain-result" or "qualification-lookup-original" or "recording-dispatch-admit"
            && payload.TryGetProperty("role",out var role) && PrivateRole(role.GetString());
    private async Task<(object? Payload,bool Mutated)> ApplyPrivateOriginalAsync(string method,JsonElement payload,IndependentCallerGrant caller,
        IndependentAuthorityState current,IndependentAuthorityState next,Action<IndependentAuthorityState> replace,CancellationToken token)
    {
        var index=new IndependentPrivateOriginalIndex(PrivateArchive(token));
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
            if(!await InitializeLiabilities().ConfigureAwait(false)) throw new IOException("Bounded durable original liability migration required.");string directory=CurrentArchiveDirectory(token);
            if(AvailableArchiveBytes(directory,token)<checked(next.PendingOriginalCompletionBytes!.Value+extra+writes+(next.SpoolPublication?.ReservedBytes??0)+installation.CompletionReserveBytes))
                throw new IOException("Independent archive outstanding original completion liability unavailable.");
            await ValidateArchiveWriteAsync(directory,true,token).ConfigureAwait(false);
        }
        async Task<IndependentOriginal?> Read(string role,string id)
        {
            var archived=await index.ReadAsync(next.PrivateOriginalsRoot,role,id,token).ConfigureAwait(false);
            return archived?.Original??next.Originals.GetValueOrDefault(role+":"+id);
        }
        async Task Store(string role,string id,IndependentOriginal value)
        {
            if(Capacity is { } pool)
            {
                string ticket=pool.Ticket(role,id);var prior=await Read(role,id).ConfigureAwait(false);
                if(prior is { Result:null,FinalAtAdmission:false }) await pool.ClaimLegacyAsync(ticket,OriginalCompletionBytes,current.Revision,token,OriginalCommitment(role,id,prior.Request,prior.FinalAtAdmission),role,id).ConfigureAwait(false);
                else if(prior is null || prior.Result is not null || prior.FinalAtAdmission) await pool.ReserveAsync(ticket,checked(2L*CustodyBindingContract.MaximumOriginalBytes+(value.Result is null && !value.FinalAtAdmission?OriginalCompletionBytes:0)+1024*1024),value.Result is null && !value.FinalAtAdmission?OriginalCompletionBytes:0,token,OriginalCommitment(role,id,value.Request,value.FinalAtAdmission),role,id).ConfigureAwait(false);
            }
            var writeIndex=new IndependentPrivateOriginalIndex(PrivateArchive(token,Capacity?.Ticket(role,id),value.Result is not null || value.FinalAtAdmission));
            var stored=await writeIndex.SetAsync(next.PrivateOriginalsRoot,role,id,value,token).ConfigureAwait(false);
            next=next with { PrivateOriginalsRoot=stored.Root,PrivateOriginalsCount=checked(next.PrivateOriginalsCount+(stored.First?1:0)) };
            next.Originals.Remove(role+":"+id);replace(next);
            if((value.Result is not null || value.FinalAtAdmission) && Capacity is { } completedPool) _completedPhysicalTickets.Value?.Add(completedPool.Ticket(role,id));
        }
        async Task Migrate()
        {
            await RequireArchiveAsync(false,token).ConfigureAwait(false);
            long bytes=0;int migrated=0;
            foreach(var legacy in next.Originals.Where(pair=>PrivateRole(pair.Key.Split(':',2)[0])).Take(16).ToArray())
            { long size=JsonSerializer.SerializeToUtf8Bytes(legacy.Value,CustodyBindingContract.Json).LongLength;if(size>CustodyBindingContract.MaximumOriginalBytes) throw new IOException("Legacy original exceeds supported per-original archive bound.");if(migrated>0 && checked(bytes+size)>1024*1024) break;bytes=checked(bytes+size);int separator=legacy.Key.IndexOf(':');await Store(legacy.Key[..separator],legacy.Key[(separator+1)..],legacy.Value).ConfigureAwait(false);migrated++; }
        }
        bool Workload(string identity,string principal,string domain)=>current.Callers.Any(value=>value.Identity==identity && value.ScopeReference==principal && !value.Revoked
            && value.ValidUntil>clock.GetUtcNow() && value.Methods.Contains("recording-source:"+(domain=="agents"?"legacy-security-event-log":domain),StringComparer.Ordinal));
        if(method=="private-original-initialize") return (new Permission(await InitializeLiabilities().ConfigureAwait(false)),true);
        if(method=="qualification-lookup-original")
        {
            var request=payload.Deserialize<LookupOriginal>(CustodyBindingContract.Json)!;
            if(request.role!=caller.Role && !caller.Methods.Contains("qualification-read-original:"+request.role,StringComparer.Ordinal)) return (null,false);
            var original=await Read(request.role,request.requestId).ConfigureAwait(false);
            // A lost final-head/settlement acknowledgment leaves no materialization intent.
            // Only a verified terminal original proves this exact ticket's unused funds releasable.
            if(installation.ArchiveCapacity is not null && (original is { Result:not null } or { FinalAtAdmission:true }) && Capacity is { } completedPool)
                await completedPool.SettleAsync(completedPool.Ticket(request.role,request.requestId),token).ConfigureAwait(false);
            return (original,false);
        }
        // Persist each bounded migration page even when the requested mutation cannot yet be admitted.
        // No archive/index mutation races the pinned migration snapshot; authenticated exact reads remain available.
        if(method is "qualification-begin-original" or "qualification-retain-result" or "recording-dispatch-admit" or "recording-replay-recovery-dispatch-admit" or "recording-recovery-dispatch-admit")
            {
                if(!await InitializeLiabilities().ConfigureAwait(false)) return (method.StartsWith("qualification-",StringComparison.Ordinal)?new { Exact=false,First=false }:new Permission(false),true);
                // Begin/retain already reconciled their exact publication ticket in PreparePrivatePublicationAsync.
                if(method is not ("qualification-begin-original" or "qualification-retain-result")) await ProtectPendingAsync(next,token).ConfigureAwait(false);
            }
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
        if(method=="recording-dispatch-admit")
        {
            var request=payload.Deserialize<RecordingDispatch>(CustodyBindingContract.Json)!;
            var original=request.role==caller.Role?await Read(request.role,request.requestId).ConfigureAwait(false):null;
            if(original is null || original.SubmissionState!="Unsent" || !Same(original.Request,request.original)
                || DispatchBinding(payload,current,request.original,request.requestId,request.role) is not { } dispatchBinding) return (new Permission(false),false);
            await Store(request.role,request.requestId,original with { SubmissionState="Dispatched",DispatchBinding=dispatchBinding }).ConfigureAwait(false);return (new Permission(true),true);
        }
        if(method=="fr34-command-authorize")
        {
            var request=payload.Deserialize<RecordingCommandAuthorization>(CustodyBindingContract.Json)!;var command=request.command;
            var original=await Read("fr34-engine",command.MessageId).ConfigureAwait(false);var target=command.Payload.GetProperty("protectedContent").GetProperty("target");
            return (new Permission(current.Fr34Target is { } fr34 && original is { SubmissionState:"Unsent" } && Fr34CommandFingerprint.Matches(original.Request,command,request.principal)
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
            if(!allowed || DispatchBinding(payload,current,command.Payload,command.MessageId,"private-replay") is not { } replayBinding) return (new Permission(false),false);
            await Store("private-replay",command.MessageId,attempt! with { SubmissionState="Dispatched",DispatchBinding=replayBinding }).ConfigureAwait(false);
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
            || !Same(RecoveryPayload(dispatch.grant),recovered.Payload) || !Same(recoveryOriginal.Request,recovered.Payload)
            || DispatchBinding(payload,current,recovered.Payload,recovered.MessageId,"private-security-observation") is not { } recoveryBinding) return (new Permission(false),false);
        await Store("private-security-observation",recovered.MessageId,recoveryOriginal with { SubmissionState="Dispatched",DispatchBinding=recoveryBinding }).ConfigureAwait(false);
        await Store("private-security-observation",ordinaryId,baseOriginal with { SubmissionState="Dispatched",ActiveTransportMessageId=recovered.MessageId }).ConfigureAwait(false);
        return (new Permission(true),true);
    }
    private IndependentCommandDispatchBinding? DispatchBinding(JsonElement payload,IndependentAuthorityState state,JsonElement original,string requestId,string role)
    {
        if(!payload.TryGetProperty("boundRequest",out var wire) || !payload.TryGetProperty("workloadProof",out var proof) || proof.ValueKind!=JsonValueKind.String) return null;
        var bound=wire.Deserialize<Hexalith.EventStore.Contracts.Commands.BoundWorkloadCommandRequest>(CustodyBindingContract.Json);
        if(bound is null || bound.Command.MessageId!=requestId || bound.Command.IdempotencyKey!=requestId || !(role=="fr34-engine"?Fr34CommandFingerprint.Matches(original,bound.Command,bound.PrincipalId):Same(bound.Command.Payload,original))
            || role!=(bound.Command.Domain=="fr34-canary"?"fr34-engine":bound.Command.Domain=="trusted-envelope-replay"?"private-replay":"private-security-observation")
            || bound.Command.Domain is not ("fr34-canary" or "trusted-envelope-replay" or "security-event-log" or "agents")
            || payload.TryGetProperty("command",out var exactCommand) && !Same(bound.Command,exactCommand)) return null;
        string source="recording-source:"+(bound.Command.Domain=="agents"?"legacy-security-event-log":bound.Command.Domain);
        var matches=state.Callers.Where(grant=>grant.ScopeReference==bound.PrincipalId && !grant.Revoked && grant.ValidUntil>clock.GetUtcNow()
            && grant.CommandProofPublicKeySpkiBase64 is not null && grant.Methods.Contains(source,StringComparer.Ordinal)
            && Hexalith.EventStore.Client.Gateway.BoundWorkloadCommandProof.Verify(bound,proof.GetString(),grant.Identity,grant.CommandProofPublicKeySpkiBase64,clock.GetUtcNow())).ToArray();
        return matches.Length==1?new(matches[0].Identity,bound.PrincipalId,bound.Command.Domain,Hash(bound.Command),Hash(matches[0]),
            new("IndependentCommandDispatchV2",state.EnrollmentId,state.TrustRevision,checked(state.Revision+1),clock.GetUtcNow().ToUnixTimeMilliseconds(),
                bound.Command.MessageId,bound.Command.Tenant,bound.Command.Domain,bound.Command.AggregateId,bound.Command.CommandType,source)):null;
    }
    private bool DispatchStillCurrent(JsonElement payload,IndependentAuthorityState state)
    {
        if(!payload.TryGetProperty("boundRequest",out var wire)) return false;
        var bound=wire.Deserialize<Hexalith.EventStore.Contracts.Commands.BoundWorkloadCommandRequest>(CustodyBindingContract.Json);
        if(bound is null) return false;
        string role=bound.Command.Domain=="fr34-canary"?"fr34-engine":bound.Command.Domain=="trusted-envelope-replay"?"private-replay":"private-security-observation";
        return DispatchBinding(payload,state,role=="fr34-engine"?Fr34CommandFingerprint.Create(bound.Command,bound.PrincipalId):bound.Command.Payload,bound.Command.MessageId,role) is not null;
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
        var archive=new IndependentRecoveryGrantArchive(CurrentArchiveDirectory(token),installation,signer,Capacity);
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
            if(prior is null) { await ProtectPendingAsync(current,token).ConfigureAwait(false);await RequireArchiveAsync(true,token).ConfigureAwait(false); }
            await archive.PrepareAsync(issued,current.Revision,token).ConfigureAwait(false);
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
    private string SpoolOriginalPath(string category, AnchoredStateTransition transition,CancellationToken token)
    {
        if(!CustodyBindingContract.Digest(transition.TargetDigest)) throw new InvalidOperationException("Exact immutable transition digest required.");
        return Path.Combine(CurrentArchiveDirectory(token), "spool", category, Hash(transition.ScopeId), transition.TargetDigest);
    }
    private long AvailableArchiveBytes(string directory,CancellationToken token)
        =>Capacity is { } pool?pool.AvailableForOwnerAsync(token).GetAwaiter().GetResult():BoundedDurableFileOperation.RunAsync(ct=> { ct.ThrowIfCancellationRequested();long available=_archive.AvailableBytes(directory);ct.ThrowIfCancellationRequested();return Task.FromResult(available); },token).GetAwaiter().GetResult();
    private bool ObservationArchiveReady(IndependentAuthorityState state,CancellationToken token)
        =>BoundedDurableFileOperation.RunAsync(ct=>
        {
            string directory=CurrentArchiveDirectory(ct);_=ArchiveFileLease.FilesystemIdentity(directory);
            using var held=HeldArchiveDirectory.Acquire(directory);
            try { held.RequireWritable(); }catch(IOException) { return Task.FromResult(false); }
            bool ready=AvailableArchiveBytes(directory,ct)>checked((state.PendingOriginalCompletionBytes??0)+installation.CompletionReserveBytes);
            held.VerifyLocator();ct.ThrowIfCancellationRequested();return Task.FromResult(ready);
        },token).GetAwaiter().GetResult();
    private string CurrentArchiveDirectory(CancellationToken token)
        =>BoundedDurableFileOperation.RunAsync(async ct=>
        {
            string? directory=installation.OriginalArchiveDirectory;
            if(directory is null || !Path.IsPathFullyQualified(directory) || installation.CompletionReserveBytes<=0 || installation.ArchiveEnrollmentSha256 is not { Length:64 } enrollmentHash)
                throw new InvalidOperationException("Independent immutable spool archive and completion reserve required.");
            using var held=HeldArchiveDirectory.Acquire(directory);
            byte[] enrollment=await BoundedRegularFile.ReadAsync(Path.Combine(directory,"enrollment.json"),65536,ct).ConfigureAwait(false);
            if(Convert.ToHexString(SHA256.HashData(enrollment))!=enrollmentHash) throw new InvalidOperationException("Current immutable spool archive enrollment required.");
            held.VerifyLocator();ct.ThrowIfCancellationRequested();return directory;
        },token).GetAwaiter().GetResult();
    private async Task<bool> HasSpoolOriginalAsync(string category, AnchoredStateTransition transition, CancellationToken token)
    {
        string path=SpoolOriginalPath(category,transition,token);byte[] bytes;
        try { bytes=await BoundedRegularFile.ReadAsync(path,CustodyBindingContract.MaximumOriginalBytes,token).ConfigureAwait(false); }
        catch(IOException) { return false; }
        return bytes.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(transition,CustodyBindingContract.Json));
    }
    private async Task WriteSpoolOriginalAsync(string category, AnchoredStateTransition transition, bool admission, CancellationToken token)
    {
        string path = SpoolOriginalPath(category, transition,token);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(transition, CustodyBindingContract.Json);
        if (admission && AvailableArchiveBytes(installation.OriginalArchiveDirectory!,token) < checked(bytes.LongLength * 2 + installation.CompletionReserveBytes))
            throw new InvalidOperationException("Spool completion reserve unavailable; ingress denied.");
        if(Capacity is { } pool) await pool.PublishAsync(pool.Ticket("spool",Hash(transition)),path,bytes,token).ConfigureAwait(false);
        else await ImmutableFileArchive.PublishAsync(path, bytes, token).ConfigureAwait(false);
    }
    private async Task RequireArchiveAsync(bool admission,CancellationToken token)
    {
        // Reuse exact enrollment/path validation before a settled spool may report readiness or admit new ingress.
        string directory=CurrentArchiveDirectory(token);
        long required=admission?checked(2L*CustodyBindingContract.MaximumOriginalBytes+installation.CompletionReserveBytes):1;
        if((admission || Capacity is null) && AvailableArchiveBytes(directory,token)<required) throw new IOException("Archive admission or completion capacity unavailable.");
        await ValidateArchiveWriteAsync(directory,admission,token).ConfigureAwait(false);
    }
    private async Task ValidateArchiveWriteAsync(string directory,bool admission,CancellationToken token)
    {
        // Admitted work transfers its enrolled physical ticket into the actual destination. A fresh
        // allocating probe would demand ordinary free blocks outside that surviving reservation.
        if(admission || Capacity is null) { await _archive.ProbeWritableAsync(directory,token).ConfigureAwait(false);return; }
        _=await BoundedDurableFileOperation.RunAsync(ct=>
        {
            using var held=HeldArchiveDirectory.Acquire(directory);held.RequireWritable();held.VerifyLocator();ct.ThrowIfCancellationRequested();return Task.FromResult(true);
        },token).ConfigureAwait(false);
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

    private sealed record RecordingRecoveryDispatch(Hexalith.EventStore.Contracts.Commands.SubmitCommandRequest command,SecurityObservationRecoveryGrant grant,Hexalith.EventStore.Contracts.Commands.BoundWorkloadCommandRequest? boundRequest=null,string? workloadProof=null);
    private sealed record RecorderOriginal(SecurityObservationRecord record);
    private sealed record RecorderRecovery(SecurityObservationRecord original, SecurityObservationRecoveryGrant grant);
    private sealed record ReplayAdmission(TrustedEnvelopeReplayIntent intent, DateTimeOffset evaluatedAt, long retentionTicks);
    private sealed record ReplayLookup(TrustedEnvelopeReplayIntent intent);
    private sealed record CustodyOriginalLookup(string tenant, string scope, string operationId, long revision, string digest);
    private sealed record RecordingRecoveryAuthorization(string caller,string principal,Hexalith.EventStore.Contracts.Commands.SubmitCommandRequest command,SecurityObservationRecoveryGrant grant);
    private sealed record RecordingDispatch(string role,string requestId,JsonElement original,Hexalith.EventStore.Contracts.Commands.BoundWorkloadCommandRequest? boundRequest=null,string? workloadProof=null);
    private sealed record RecordingCommandAuthorization(string caller, string principal, Hexalith.EventStore.Contracts.Commands.SubmitCommandRequest command);
    private sealed record CustodyRoleAuthorization(string role, CustodyCallIntent intent, string bearerSha256,string method,CustodyRoleWorkloadGrant? grant=null,string? responseSha256=null);
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
