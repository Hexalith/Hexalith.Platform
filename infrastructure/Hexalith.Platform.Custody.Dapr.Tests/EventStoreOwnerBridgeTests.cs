using System.Text;
using System.Text.Json;
using Dapr.Client;
using Grpc.Net.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody.Dapr;
using Hexalith.Platform.Protection;
using Hexalith.Platform.Secrets.Runtime;

/// <summary>Real SDK/semantic-provider bridge interoperability with disposable keys/native doubles; no installed qualification.</summary>
internal static class EventStoreOwnerBridgeTests
{
    /// <summary>Executes exact owner genesis, operation, actual-transition, recovery and denial boundaries.</summary>
    internal static async Task Run()
    {
        const string tenant = "tenant";
        string scope = DeletionConsumptionActor.GetOwnerScope(tenant);
        string genesis = CustodyProtectionOwnerIdentity.GenesisDigest(tenant);
        using var fixture = new Fixture(genesis, scope);
        var handler = new SdkBridge(fixture);
        using var client = new DaprClientBuilder().UseGrpcEndpoint("http://127.0.0.1:1")
            .UseGrpcChannelOptions(new GrpcChannelOptions { HttpHandler = handler }).Build();
        var calls = new ControlledOwnerCallSource(new OwnerCalls(fixture));
        var bridge = new CustodyDeletionConsumptionOwnerAuthority(client, "custody", tenant,
            Encoding.UTF8.GetString(CustodyJson.Bytes(fixture.Profile.Enrollment)), fixture.Profile.ComponentPublicKeyPem, calls);
        var authority = new PublishedTrustDeletionConsumptionAuthority([], new UnavailableOwnerTestGuard(), null, null, [bridge]);
        Check(bridge.TenantId == tenant && bridge.ScopeId == scope, "Installed routing changed.");
        Check(await authority.ValidateStateAsync(tenant, 0, genesis), "Exact independently enrolled signed genesis refused.");
        Check(!await authority.ValidateStateAsync("foreign", 0, genesis), "Foreign tenant selected installed owner.");
        Check(!await authority.ValidateStateAsync(tenant, 0, Fixture.Digest), "Changed genesis accepted.");
        Check(await authority.AuthorizeOperationAsync(tenant, "original", "RegisterDeletionBatch"), "Exact private operation refused.");
        string operationId = CustodyBindingContract.Hash(Hexalith.Platform.Custody.PlatformCanonicalBytes.Components(
            ["custody-owner-operation-v1", tenant, scope, "original", "RegisterDeletionBatch"]));
        Check(calls.Calls.Last().Identity == operationId, "Operation framing lost domain, tenant, scope or exact method.");

        byte[] firstBytes = "first-actual-owner-state"u8.ToArray();
        var first = new AnchoredStateTransition(scope, 0, 1, genesis, CustodyJson.Hash(firstBytes), firstBytes);
        Check(!await authority.RecordRevisionAsync(tenant, 0, 1, first.TargetDigest), "Legacy hook advanced an unadmitted target.");
        Check(!await authority.AdmitTransitionAsync(first with { ScopeId = "foreign" }), "Foreign transition scope selected owner.");
        Check(await authority.AdmitTransitionAsync(first), "Exact first admission refused.");
        Check(!await authority.VerifyTransitionAsync(first), "Admission advanced current owner.");
        Check(await authority.RecoverTransitionAsync(first), "Exact admitted original recovery refused.");
        Check(await authority.VerifyTransitionAsync(first), "Recovered original unavailable.");
        Check(await authority.RecordRevisionAsync(tenant, 0, 1, first.TargetDigest), "Legacy retained target verification refused.");
        Check(!await authority.ValidateStateAsync(tenant, 0, genesis), "Restored genesis revived after owner advancement.");

        byte[] secondBytes = "second-actual-owner-state"u8.ToArray();
        var second = new AnchoredStateTransition(scope, 1, 2, first.TargetDigest, CustodyJson.Hash(secondBytes), secondBytes);
        Check(await authority.AdmitTransitionAsync(second), "Consecutive exact admission refused.");
        handler.LoseNextRecordAcknowledgement = true;
        Check(!await authority.RecordTransitionAsync(second), "Lost acknowledgement unexpectedly reported record success.");
        Check(await authority.VerifyTransitionAsync(second), "Recorded original lost after acknowledgement loss.");
        Check(await authority.RecoverTransitionAsync(second), "Exact lost-result recovery refused.");
        Check(await authority.ValidateStateAsync(tenant, 2, second.TargetDigest), "Independent current owner target refused.");
        Check(await authority.VerifyTransitionAsync(first), "Consecutive owner advance lost historical immutable original.");
        foreach (var call in calls.Calls)
        {
            Check(call.Tenant == tenant && call.Scope == scope, "Private authorizer received substituted tenant/scope.");
            if (call.Transition is not null)
            {
                var exact = CustodyJson.Read<AnchoredStateTransition>(Encoding.UTF8.GetBytes(call.Transition));
                var expected = exact.TargetRevision == 1 ? first : second;
                Check(JsonSerializer.Serialize(exact) == JsonSerializer.Serialize(expected), "Actor transition was synthesized or changed.");
            }
        }

        foreach (string mode in new[] { "refuse", "throw", "foreign-tenant", "foreign-scope" })
        {
            calls.Mode = mode;
            int before = handler.Calls;
            Check(!await authority.ValidateStateAsync(tenant, 2, second.TargetDigest), mode + " granted currentness.");
            Check(!await authority.AuthorizeOperationAsync(tenant, "original", "RegisterDeletionBatch"), mode + " granted private operation.");
            Check(!await authority.AdmitTransitionAsync(second), mode + " granted admission.");
            Check(!await authority.RecordTransitionAsync(second), mode + " granted record.");
            Check(!await authority.RecoverTransitionAsync(second), mode + " granted recovery.");
            Check(!await authority.VerifyTransitionAsync(second), mode + " granted verify.");
            Check(handler.Calls == before, mode + " reached provider transport.");
        }
        calls.Mode = "normal";
        handler.CorruptSignature = true;
        Check(!await authority.ValidateStateAsync(tenant, 2, second.TargetDigest), "Invalid current-state signature accepted.");
        handler.CorruptSignature = false;
        foreach (string phase in new[] { "current", "operation", "admit", "record", "recover", "verify" })
        {
            using var cancellation = new CancellationTokenSource();
            calls.Mode = "cancel";
            calls.Cancellation = cancellation;
            Task<bool> pending = phase switch
            {
                "current" => bridge.ValidateStateAsync(2, second.TargetDigest, cancellation.Token),
                "operation" => bridge.AuthorizeOperationAsync("original", "RegisterDeletionBatch", cancellation.Token),
                "admit" => bridge.AdmitTransitionAsync(second, cancellation.Token),
                "record" => bridge.RecordTransitionAsync(second, cancellation.Token),
                "recover" => bridge.RecoverTransitionAsync(second, cancellation.Token),
                _ => bridge.VerifyTransitionAsync(second, cancellation.Token),
            };
            bool cancelled = false;
            try { await pending; }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { cancelled = true; }
            Check(cancelled, "Explicit cancellation swallowed by " + phase);
            Check(calls.Calls.Last().Token == cancellation.Token, "Explicit cancellation token was substituted by " + phase);
        }
        Check(fixture.Native.Encrypts == 0 && fixture.Native.Schedules == 0, "Owner protocol caused a physical key effect.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
}
