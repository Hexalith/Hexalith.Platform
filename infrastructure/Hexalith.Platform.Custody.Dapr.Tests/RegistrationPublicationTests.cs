using Dapr.Client;
using Grpc.Net.Client;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Custody.Dapr;
using Hexalith.Platform.Secrets.Runtime;

/// <summary>Exercises exact durable publication recovery and the real SDK original verifier without native resources.</summary>
internal static class RegistrationPublicationTests
{
    /// <summary>Runs first publication, uncertainty, restart, original verification and cancellation scenarios.</summary>
    internal static async Task Run()
    {
        using (var missing = new Fixture())
        {
            missing.RestartPublication(null);
            await Refused(() => missing.Call(missing.Wrap("missing-publisher")));
            Check(missing.Native.Encrypts == 0 && missing.Store.Objects.Count == 0, "Missing installation created admission objects or performed native wrap.");
        }
        using (var blocked = new Fixture())
        {
            var blockedRequest=blocked.Wrap("blocked-publication");
            var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            blocked.Publisher.BeforePublication=async _=> {
                Check(blockedRequest.WrapInput!.RootKey.All(value=>value==0) && blocked.Native.LastTenantKek is { } kek && kek.All(value=>value==0),"Publisher observed uncleared root or KEK arrays.");
                entered.SetResult();await release.Task;
            };
            var pending=WrapDirectAsync(blocked,blockedRequest,default);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));Check(!pending.IsCompleted,"Publisher did not block.");
            release.SetResult();await pending;
        }
        using var f = new Fixture();
        f.Publisher.BeforePublication = async original => {
            var retained = await SemanticProvider.ReadWrapOriginalAsync(f.Store, await f.Store.ReadAsync("tenant", default), original, default);
            Check(retained.WrappedRegistration == original, "Publication preceded durable verified original.");
        };
        f.Publisher.Unavailable = true;
        var request = f.Wrap("unreceived");
        await Refused(() => f.Call(request));
        var original = f.Store.State.Checkpoint.Registrations.Values.Single().Registration;
        Check(f.Native.Encrypts == 1 && f.Publisher.Originals.Count == 0, "Publication outage did not retain unreceived original.");
        f.RestartPublication(f.Publisher); f.Publisher.Unavailable = false;
        var intent = f.Store.State.Checkpoint.NativeIntents.Values.Single();
        var recovery = new WorkerOperation("recover-wrap-result", original.OperationId, intent.IntentId, intent.SourceGeneration, intent.DestinationGeneration, "", "executor-process-1");
        f.Qualify();
        await f.Exchange.RecoverWrapAsync("tenant", recovery, f.Sign("private-operation", CustodyJson.Hash(CustodyJson.Bytes(recovery)), recovery), default);
        Check(f.Publisher.Originals[original.Identity] == original && f.Native.Encrypts == 1, "Restart recovery rewrapped or lost original.");
        f.Publisher.LoseAcknowledgement = true;
        await Refused(() => f.Call(request));
        f.RestartPublication(f.Publisher);
        Check((await f.Call(request)).WrappedRegistration == original && f.Native.Encrypts == 1, "Lost acknowledgement did not reuse exact original.");
        await f.CloseKey(original,request.WrapInput!.KeyUse);
        Check(f.Store.State.Checkpoint.NativeIntents[intent.IntentId].Phase==CustodyBindingIntentPhase.Closed,"Actual holder completion did not close wrap intent.");
        var closedHead=f.Store.State.Head;f.RestartPublication(f.Publisher);f.Qualify();
        await f.Exchange.RecoverWrapAsync("tenant",recovery,f.Sign("private-operation",CustodyJson.Hash(CustodyJson.Bytes(recovery)),recovery),default);
        Check(f.Store.State.Head==closedHead && f.Store.State.Checkpoint.NativeIntents[intent.IntentId].Phase==CustodyBindingIntentPhase.Closed
            && f.Store.State.Checkpoint.Deliveries[CustodyJson.DeliveryId(request.WrapInput.KeyUse)].Phase==CustodyBindingIntentPhase.Closed
            && f.Publisher.Originals[original.Identity]==original && f.Native.Encrypts==1,"Closed recovery changed phases, original or native wrap count.");
        f.Publisher.Substitute = true;
        await Refused(() => f.Exchange.PublishRegistrationAsync(original, default));
        f.Publisher.Substitute = false;
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await f.Exchange.PublishRegistrationAsync(original, cancelled.Token); throw new Exception("Cancellation fabricated publication success."); }
        catch (OperationCanceledException) { }

        var handler = new SdkBridge(f);
        using var sdk = new DaprClientBuilder().UseGrpcEndpoint("http://127.0.0.1:1").UseGrpcChannelOptions(new GrpcChannelOptions { HttpHandler = handler }).Build();
        var verifier = new DaprCustodyRegistrationOriginalVerifier(PublicLifecycleTests.Client(f, sdk, new LifecycleAuthorizer(f)));
        Check(await verifier.VerifyAsync(original, default), "Actual signed immutable original verification failed.");
        foreach (var changed in new[] {
            original with { OperationId = "changed" }, original with { ExpectedRevision = original.ExpectedRevision + 1 },
            original with { KekVersion = "changed" }, original with { OpaqueWrappedKeyReference = "changed" }, original with { WrapReceiptId = "changed" },
            original with { Identity = original.Identity with { TenantId = "other" } }, original with { Identity = original.Identity with { ObjectId = "changed" } },
            original with { Identity = original.Identity with { Purpose = PlatformKeyPurpose.ExportEnvelopeKey } },
            original with { Identity = original.Identity with { KeyAlias = "changed" } }, original with { Identity = original.Identity with { KeyVersion = "changed" } },
            original with { Identity = original.Identity with { LifecycleVersion = "changed" } }, original with { Identity = original.Identity with { StoreTarget = "changed" } },
            original with { Identity = original.Identity with { ContractVersion = "changed" } },
        }) Check(!await verifier.VerifyAsync(changed, default), "Substituted original admitted.");
        var retainedOriginal = f.Store.State.Checkpoint.Originals[original.OperationId];
        f.Store.State.Checkpoint.Originals[original.OperationId] = retainedOriginal with { Response = null };
        Check(!await verifier.VerifyAsync(original, default), "Missing immutable original admitted.");
        f.Store.State.Checkpoint.Originals[original.OperationId] = retainedOriginal;
        Check(f.Native.Encrypts == 1 && f.Native.Decrypts == 0 && f.Native.Schedules == 0, "Recovery/verification sent extra native work.");
        await JoinedPublicationAsync();
    }
    private static async Task JoinedPublicationAsync()
    {
        using var component=new Fixture();using var authority=new RegistrationAuthorityFixture(component);
        component.RestartPublication(authority.Publisher);
        var request=component.Wrap("joined-post-commit-cancellation");
        using var cancellation=new CancellationTokenSource();
        authority.AfterPublicationCommit=()=> {
            Check(authority.State.Registrations.Values.Single().Identity==request.WrapInput!.Identity,"Cancellation preceded durable publication.");
            Check(request.WrapInput.RootKey.All(value=>value==0) && component.Native.LastTenantKek!.All(value=>value==0),"Post-commit publication retained secret buffers.");
            cancellation.Cancel();
        };
        try { await WrapDirectAsync(component,request,cancellation.Token);throw new Exception("Post-commit cancellation fabricated success."); }
        catch(OperationCanceledException) when(cancellation.IsCancellationRequested) { }
        catch(AuthorityUnavailableException failure) { throw new Exception("Joined publication refused: "+string.Join(", ",authority.Calls),failure); }
        var original=authority.State.Registrations.Values.Single();
        Check(authority.PrepareWrites==1 && authority.State.Originals.Keys.Any(key=>key.StartsWith("authorizer:",StringComparison.Ordinal)),"Joined actual authorizer did not advance the publication head.");
        authority.AfterPublicationCommit=null;authority.Restart();component.RestartPublication(authority.Publisher);
        Check(await authority.Locator.FindAsync(original.Identity,default)==original,"Restart locator lost cancelled publication.");
        var intent=component.Store.State.Checkpoint.NativeIntents.Values.Single();
        var recovery=new WorkerOperation("recover-wrap-result",original.OperationId,intent.IntentId,intent.SourceGeneration,intent.DestinationGeneration,"","executor-process-1");
        component.Qualify();await component.Exchange.RecoverWrapAsync("tenant",recovery,component.Sign("private-operation",CustodyJson.Hash(CustodyJson.Bytes(recovery)),recovery),default);
        Check(component.Native.Encrypts==1 && authority.State.Registrations.Values.Single()==original,"Joined restart recovery repeated wrapping or changed original.");
        authority.RevokeAfterPrepare=true;
        Check(await authority.Publisher.PublishAsync(original,default) is null,"Publisher revoked during actual prepare was admitted after verification.");
        Check(component.Native.Encrypts==1 && authority.State.Registrations.Values.Single()==original,"Revoked publication mutated original or wrapped again.");
    }
    private static Task<CustodyBindingResponseV1> WrapDirectAsync(Fixture fixture,CustodyBindingRequestV1 request,CancellationToken token)
    {
        fixture.Qualify();var use=request.WrapInput!.KeyUse;
        var admission=new KeyAdmission(use,request.Envelope.RequestDigest,fixture.Profile.ProfileDigest,
            new[] { "caller","sdk","sidecar","uds","grpc","tls","component","native" }.Select(role=>new KeyHolder("copy-"+role,role,use.RecipientProcessIncarnation,use.TransportIncarnation,"owner",Fixture.Digest)).ToArray());
        return fixture.Exchange.ExecuteAsync(request,fixture.Store.State,fixture.Sign("independent-observer",CustodyJson.Hash(CustodyJson.Bytes(admission)),admission),token);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task Refused(Func<Task> operation)
    {
        try { await operation(); }
        catch (AuthorityUnavailableException) { return; }
        throw new Exception("Expected publication refusal.");
    }
}
