using System.Reflection;
using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Platform.Custody;
using NSubstitute;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual production consumption actor and native-head authorities/provider; private dispatch eligibility remains an explicitly synthetic component fixture.</summary>
internal sealed class ErasureRootConsumptionFixture
{
    internal ErasureRootStoreFixture Store { get; } = new();
    internal ErasureRootAuthorizationFixture Permissions { get; } = new();
    internal InMemoryStateManager State { get; } = new();
    internal IDeletionConsumptionAuthority Authority { get; } = Substitute.For<IDeletionConsumptionAuthority>();
    internal ErasureRootCustody Custody { get; }
    internal DeletionConsumptionActor Actor { get; }
    private readonly SemaphoreSlim _turn = new(1, 1);
    internal ErasureRootConsumptionFixture()
    {
        var installed = new ErasureRootInstallation("erc-v23", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant-a"] = "epoch" });
        var genesis = new { TenantId = "tenant-a", Revision = 0L, KeyBlockSetRevision = 0L, Batches = Array.Empty<object>(), Revocations = Array.Empty<object>(), Operations = Array.Empty<object>() };
        Store.Enroll(installed, "tenant-a", DeletionConsumptionActor.GetOwnerScope("tenant-a"), ErasureRootCustody.Hash(genesis));
        Custody = new(Store, installed, Permissions);
        var owner = new ErasureRootDeletionOwnerAuthority(Custody, Permissions, "tenant-a");
        Authority.AuthorizeOperationAsync("tenant-a", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => owner.AuthorizeOperationAsync(call.ArgAt<string>(1), call.ArgAt<string>(2), call.Arg<CancellationToken>()));
        Authority.ValidateStateAsync("tenant-a", Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => owner.ValidateStateAsync(call.Arg<long>(), call.ArgAt<string>(2), call.Arg<CancellationToken>()));
        Authority.ReadCurrentOriginalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => owner.ReadCurrentOriginalAsync(call.Arg<string>(), call.Arg<CancellationToken>()));
        Authority.AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call => owner.AdmitTransitionAsync(call.Arg<AnchoredStateTransition>(), call.Arg<CancellationToken>()));
        Authority.RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call => owner.RecordTransitionAsync(call.Arg<AnchoredStateTransition>(), call.Arg<CancellationToken>()));
        Authority.RecoverTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call => owner.RecoverTransitionAsync(call.Arg<AnchoredStateTransition>(), call.Arg<CancellationToken>()));
        Authority.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call => owner.VerifyTransitionAsync(call.Arg<AnchoredStateTransition>(), call.Arg<CancellationToken>()));
        Authority.VerifyDispatchAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.VerifyAdmissionBlockAsync(Arg.Any<DeletionBatchBlockRequest>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.VerifyRevocationAsync(Arg.Any<DeletionCapabilityRevocationEnvelope>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.VerifyActivationAsync(Arg.Any<DeletionReattestationActivation>(), Arg.Any<CancellationToken>()).Returns(true);
        Actor = new(ActorHost.CreateForTest<DeletionConsumptionActor>(new ActorTestOptions { ActorId = new(DeletionConsumptionActor.GetActorId("tenant-a")) }), Authority, Custody);
        typeof(Actor).GetProperty("StateManager", BindingFlags.Instance | BindingFlags.Public)!.SetValue(Actor, State);
    }
    internal async Task<T> Invoke<T>(Func<Task<T>> operation)
    { await _turn.WaitAsync(TestContext.Current.CancellationToken); try { return await operation(); } finally { _turn.Release(); } }
    internal async Task<DeletionBatchConsumptionRequest> SeedAsync()
    {
        var targets = new[] { new ProtectionTarget("tenant-a", "interaction-a", "alias-a"), new ProtectionTarget("tenant-a", "interaction-b", "alias-b") };
        foreach (var target in targets)
        { _ = await Custody.WrapAsync(new(target.TenantId, target.AgentInteractionId, PlatformKeyPurpose.InteractionRootDek, target.TargetProtectionKeyAlias, "root-v1", "lifecycle", "erc-v23", "custody"), "wrap-" + target.AgentInteractionId, 0, TestContext.Current.CancellationToken); }
        return new(new("issuer", "protection-v1", "tenant-a", "request-1", "seal-1", "accepted", 0, "batch-1", DeletionBatchCapabilityIdentity.TargetManifestDigest(targets),
            "tenant-a:governance:guard-1", 7, 1, 1, "key-v1"), "component-signed-fixture", 8, "dispatch-1", 10, targets);
    }
    internal static DeletionCapabilityRevocationEnvelope Revocation(string version = "key-v1")
        => new("issuer", "registrar", "tenant-a", "DeletionBatchCapabilitySigningKey", version, 1, 1, "revoke-" + version, new string('A', 64));
    internal object CaptureOwner() => JsonSerializer.Deserialize(JsonSerializer.Serialize(State.CommittedState["deletion-consumption-v23"]), State.CommittedState["deletion-consumption-v23"].GetType())!;
    internal async Task RestoreOwnerAsync(object original)
    {
        await State.SetStateAsync("deletion-consumption-v23", original, TestContext.Current.CancellationToken);
        _ = await State.TryRemoveStateAsync("deletion-consumption-v23-pending-transition-v1", TestContext.Current.CancellationToken);
        await State.SaveStateAsync(TestContext.Current.CancellationToken);
    }
}
