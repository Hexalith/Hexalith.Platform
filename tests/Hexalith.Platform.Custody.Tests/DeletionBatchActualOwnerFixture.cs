using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Actual guard owner/Dapr transaction, retained signer actors and protection actor with serialized synthetic backend/independent authority and real ES256. It is source evidence, not live qualification.</summary>
internal sealed class DeletionBatchActualOwnerFixture : IDisposable
{
    internal DeletionBatchTransactionFixture Backend { get; } = new();
    internal GovernanceScopeGuardOwner Guard { get; }
    internal DeletionBatchExecutionCoordinator Coordinator { get; }
    internal DeletionBatchCapabilityV1 Payload { get; }
    internal ECDsa Key { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    internal DeletionConsumptionActor ProtectionActor { get; }
    internal InMemoryStateManager ProtectionState { get; } = new();
    internal int Signatures { get; private set; }
    internal int Reservations { get; private set; }
    internal bool LoseSignatureResponse { get; set; }
    internal bool LoseConsumptionResponse { get; set; }
    internal bool MakeFirstIssueStale { get; set; }
    internal bool UnknownConsumptionLookup { get; set; }
    internal TenantGovernanceGuardState State => JsonSerializer.Deserialize<TenantGovernanceGuardState>(JsonSerializer.Deserialize<GuardedStateCell>(Backend.Stored[Backend.Key(Backend.Target.GuardCellId)])!.Value)!;
    private readonly Dictionary<string, DeletionCapabilitySigningResult> _retainedSignatures = [];
    private readonly Dictionary<string, InMemoryStateManager> _signerStates = [];
    private readonly Dictionary<string, string> _signerAnchors = [];
    private readonly Dictionary<string, DeletionManifestProviderResult> _destructions = [];
    internal DeletionBatchActualOwnerFixture()
    {
        using var template = new DeletionBatchExecutionFixture(); Payload = template.Payload;
        SetState(template.State with { InstallationId = Backend.Target.InstallationId });
        var sourceAuthority = Substitute.For<IGovernanceGuardAuthority>();
        sourceAuthority.ReadLookupAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Evidence(call.Arg<GovernanceGuardTransition>(), call.ArgAt<string>(1), call.ArgAt<string>(2), State, true));
        sourceAuthority.ReadAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TenantGovernanceGuardState>(), Arg.Any<CancellationToken>())
            .Returns(call => Evidence(call.Arg<GovernanceGuardTransition>(), call.ArgAt<string>(1), call.ArgAt<string>(2), call.Arg<TenantGovernanceGuardState>(), false));
        sourceAuthority.ReadSigningRecoveryAsync(Arg.Any<GovernanceProtocolReceipt>(), Arg.Any<TenantGovernanceGuardState>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var proof = call.Arg<GovernanceProtocolReceipt>(); var state = call.Arg<TenantGovernanceGuardState>();
            return new GovernanceSigningRecovery(proof.Capability!, proof.SigningRequestId, proof.DetachedJwsDigest, proof.ReceiptId, state.Revision,
                "key-b", "independent-installed-recovery", Backend.Now, Backend.Now.AddMinutes(1));
        });
        Guard = new(Backend.Owner, TimeProvider.System, sourceAuthority);
        var protectionAuthority = Substitute.For<IDeletionConsumptionAuthority>(); long protectionAnchor = 0;
        string protectionDigest = Hash(new { TenantId = "tenant-a", Revision = 0L, KeyBlockSetRevision = 0L, Batches = Array.Empty<object>(), Revocations = Array.Empty<object>(), Operations = Array.Empty<object>() });
        protectionAuthority.AuthorizeOperationAsync("tenant-a", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
            !(UnknownConsumptionLookup && call.ArgAt<string>(2) == "LookupDeletionBatch"));
        protectionAuthority.ValidateStateAsync("tenant-a", Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => call.ArgAt<long>(1) == protectionAnchor && call.ArgAt<string>(2) == protectionDigest);
        protectionAuthority.RecordRevisionAsync("tenant-a", Arg.Any<long>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        { if (call.ArgAt<long>(1) != protectionAnchor || call.ArgAt<long>(2) != protectionAnchor + 1) { return false; } protectionAnchor++; protectionDigest = call.ArgAt<string>(3); return true; });
        protectionAuthority.VerifyDispatchAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<CancellationToken>()).Returns(call => VerifyDispatch(call.Arg<DeletionBatchConsumptionRequest>()));
        protectionAuthority.VerifyActivationAsync(Arg.Any<DeletionReattestationActivation>(), Arg.Any<CancellationToken>()).Returns(call => VerifyDispatch(call.Arg<DeletionReattestationActivation>().Replacement));
        protectionAuthority.VerifyRevocationAsync(Arg.Any<DeletionCapabilityRevocationEnvelope>(), Arg.Any<CancellationToken>()).Returns(true);
        var provider = Substitute.For<IAtomicDeletionManifestProvider>();
        provider.ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            Reservations++; var request = call.Arg<DeletionBatchConsumptionRequest>(); string receipt = call.ArgAt<string>(1);
            var result = new DeletionManifestProviderResult("tenant-a", request.Capability.BatchId, receipt, DeletionManifestProviderState.Consumed,
                request.Targets.Select(value => new DeletionTargetReceipt(value, request.Capability.BatchId, "irreversible-" + value.AgentInteractionId)).ToArray());
            _destructions[receipt] = result;
            return LoseConsumptionResponse ? Task.FromException<DeletionManifestProviderResult>(new IOException("Controlled physical response loss.")) : Task.FromResult(result);
        });
        provider.LookupAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => _destructions[call.ArgAt<string>(1)]);
        ProtectionActor = new(ActorHost.CreateForTest<DeletionConsumptionActor>(new ActorTestOptions { ActorId = new(DeletionConsumptionActor.GetActorId("tenant-a")) }), protectionAuthority, provider);
        InstallStateManager(ProtectionActor, ProtectionState);
        var proxies = Substitute.For<IActorProxyFactory>(); proxies.CreateActorProxy<IDeletionConsumptionActor>(Arg.Any<Dapr.Actors.ActorId>(), DeletionConsumptionActor.ActorTypeName).Returns(ProtectionActor);
        var protection = new DaprDeletionProtectionOwner("tenant-a", proxies, TimeProvider.System);
        Coordinator = new(TimeProvider.System, new EventStoreDeletionBatchGuardPort(Guard), Signer, _ => protection);
    }
    internal IDeletionCapabilitySigningActor Signer(DeletionBatchCapabilityV1 payload)
    {
        string id = DeletionBatchCapabilityIdentity.SigningRequestId(payload);
        if (!_signerStates.TryGetValue(id, out var state)) { _signerStates[id] = state = new(); _signerAnchors[id] = Hash((DeletionCapabilitySigningOutcome?)null); }
        var authority = Substitute.For<IDeletionCapabilitySigningAuthority>();
        authority.AuthorizeOperationAsync(payload, id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        authority.AuthorizeAsync(payload, id, Arg.Any<CancellationToken>()).Returns(true);
        authority.ValidateStateAsync(payload, id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => call.ArgAt<string>(2) == _signerAnchors[id]);
        authority.RecordStateAsync(payload, id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        { if (call.ArgAt<string>(2) != _signerAnchors[id]) { return false; } _signerAnchors[id] = call.ArgAt<string>(3); return true; });
        var trust = Substitute.For<IDeletionCapabilitySigningTrustProvider>();
        trust.ResolveAsync("tenant-a", "DeletionBatchCapabilitySigningKey", payload.CapabilityKeyVersion, Arg.Any<CancellationToken>()).Returns(new DeletionCapabilityPublishedTrust("tenant-a",
            "DeletionBatchCapabilitySigningKey", payload.CapabilityKeyVersion, "issuer-a", "protection-a", "anchor-a", "v1", Key.ExportSubjectPublicKeyInfo(), 1, true));
        var provider = Substitute.For<IDeletionCapabilitySigningProvider>();
        provider.SignAsync(payload, id, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Signatures++; var profile = new DeletionCapabilityTrustProfile("issuer-a", "protection-a", "tenant-a", payload.CapabilityKeyVersion, "anchor-a", "v1");
            var signed = new DeletionCapabilitySigningResult(new(id, payload, DeletionCapabilitySigningState.Signed, DeletionBatchCapabilityCodec.Sign(payload, profile, Key), "anchor-a", "v1"), Key.ExportSubjectPublicKeyInfo());
            _retainedSignatures[id] = signed;
            if (MakeFirstIssueStale) { SetState(State with { Revision = State.Revision + 1 }); MakeFirstIssueStale = false; }
            return LoseSignatureResponse ? Task.FromException<DeletionCapabilitySigningResult>(new IOException("Controlled retained signature response loss.")) : Task.FromResult(signed);
        });
        provider.LookupAsync(payload, id, Arg.Any<CancellationToken>()).Returns(_ => _retainedSignatures[id]);
        var actor = new DeletionCapabilitySigningActor(ActorHost.CreateForTest<DeletionCapabilitySigningActor>(new ActorTestOptions { ActorId = new(DeletionCapabilitySigningActor.GetActorId(payload)) }),
            authority, provider, trust, noIssueAuthority: new EventStoreDeletionCapabilityNoIssueAuthority(Guard), timeProvider: TimeProvider.System);
        InstallStateManager(actor, state); return actor;
    }
    internal void SetState(TenantGovernanceGuardState state)
    {
        string key = Backend.Key(Backend.Target.GuardCellId);
        long revision = Backend.Stored.TryGetValue(key, out var previous) ? Math.Max(state.Revision, JsonSerializer.Deserialize<GuardedStateCell>(previous)!.Revision + 1) : state.Revision;
        Backend.Stored[key] = JsonSerializer.SerializeToUtf8Bytes(new GuardedStateCell("tenant-a", Backend.Target.InstallationId, Backend.Target.GuardCellId, revision, JsonSerializer.SerializeToUtf8Bytes(state)));
    }
    private GovernanceGuardEvidence? Evidence(GovernanceGuardTransition command, string intent, string target, TenantGovernanceGuardState state, bool lookup)
    {
        if (!lookup && command.Batch?.Capability is { } payload && command.Batch.DetachedJws != "" && !DeletionBatchCapabilityCodec.Verify(payload,
            new("issuer-a", "protection-a", "tenant-a", payload.CapabilityKeyVersion, "anchor-a", "v1"), command.Batch.DetachedJws, Key)) { return null; }
        return new("tenant-a", intent, target, "independent-current-authority", command.Batch?.ProtectionReceiptId is { Length: > 0 } receipt ? receipt : "independent-source-approval",
            Backend.Now, Backend.Now.AddMinutes(1), lookup ? [] : ["owner-a"], lookup ? [] : ["original-obligation"], [], "all-writers-installed", "legacy-revoked",
            "current-zero", lookup ? 0 : 1, "Open", "", lookup ? [] : state.Deletions.Single().Batches.Where(value => value.ProtectionReceiptId != "").Select(value => value.ProtectionReceiptId).ToArray(), command.RevocationReceipt?.ReceiptId ?? "block-receipt")
            { CapabilityIssuer = "issuer-a", CapabilityAudience = "protection-a", GuardStreamId = "guard-a", RevocationReceipt = command.RevocationReceipt };
    }
    private bool VerifyDispatch(DeletionBatchConsumptionRequest request)
    {
        var batch = State.Deletions.Single().Batches.Single(); var payload = request.Capability;
        return batch.Capability == payload && batch.DetachedJws == request.DetachedJws && batch.IssuedGuardRevision == request.CommittedIssuedGuardRevision
            && batch.DispatchReceiptId == request.DispatchReceiptId && batch.DispatchGuardRevision == request.DispatchGuardRevision
            && DeletionBatchCapabilityCodec.Verify(payload, new("issuer-a", "protection-a", "tenant-a", payload.CapabilityKeyVersion, "anchor-a", "v1"), request.DetachedJws, Key);
    }
    private static void InstallStateManager(Dapr.Actors.Runtime.Actor actor, InMemoryStateManager state)
        => typeof(Dapr.Actors.Runtime.Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(actor, state);
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    public void Dispose() => Key.Dispose();
}
