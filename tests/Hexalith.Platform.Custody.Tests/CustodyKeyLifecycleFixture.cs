using Hexalith.EventStore.Contracts.Security;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Actual lifecycle actor/serialized backend state with synthetic independently qualified linearizable fence/all-copy physical provider; no real hold or destruction proof.</summary>
internal sealed class CustodyKeyLifecycleFixture : ICustodyKeyLifecycleProvider
{
    internal InMemoryStateManager Backend { get; } = new();
    internal ICustodyKeyLifecycleAuthority Authority { get; } = Substitute.For<ICustodyKeyLifecycleAuthority>();
    internal CustodyKeyLifecycleActor Actor { get; }
    internal long Anchor { get; set; }
    internal string AnchorDigest { get; set; } = Digest(new CustodyKeyLifecycleLedger("tenant-a", 0, []));
    internal Dictionary<string, byte[]> PhysicalOutcomes { get; } = [];
    internal int Effects { get; private set; }
    internal bool LoseResponse { get; set; }
    internal bool UnknownLookup { get; set; }
    internal bool OmitRestoreProof { get; set; }
    internal bool NotPerformedPin { get; set; }
    internal bool NotPerformedUnpin { get; set; }
    internal bool RejectRegistration { get; set; }
    internal bool RejectOutcome { get; set; }
    internal CustodyKeyLifecycleFixture()
    {
        Authority.AuthorizeOperationAsync(Arg.Any<CustodyKeyObjectIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.VerifyRegistrationAsync(Arg.Any<CustodyKeyRegistration>(), Arg.Any<CancellationToken>()).Returns(_ => !RejectRegistration);
        Authority.AuthorizeEffectAsync(Arg.Any<CustodyKeyLifecycleRequest>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.VerifyOutcomeAsync(Arg.Any<CustodyKeyLifecycleRequest>(), Arg.Any<CustodyKeyLifecycleOutcome>(), Arg.Any<CancellationToken>()).Returns(_ => !RejectOutcome);
        Authority.ValidateStateAsync("tenant-a", Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<long>() == Anchor && call.ArgAt<string>(2) == AnchorDigest);
        Authority.RecordRevisionAsync("tenant-a", Arg.Any<long>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => {
            if (call.ArgAt<long>(1) != Anchor || call.ArgAt<long>(2) != Anchor + 1) { return false; } Anchor++; AnchorDigest = call.ArgAt<string>(3); return true;
        });
        AnchoredFixtureJournal.Attach(Authority, CustodyKeyLifecycleActor.GetActorId("tenant-a") + "|custody-key-lifecycle-candidate-v1", () => (Anchor, AnchorDigest), (revision, digest) => { Anchor = revision; AnchorDigest = digest; });
        Actor = Create(Backend, Authority, this);
    }
    public Task<CustodyKeyLifecycleOutcome> ExecuteAsync(CustodyKeyRegistration registration, CustodyKeyLifecycleRequest request, string digest, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Persisted.Keys.Single(k => k.Registration.Identity == request.Identity).Outcomes.Single(o => o.OperationId == request.OperationId).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown);
        Effects++; var status = request.Action == CustodyKeyLifecycleAction.Pin && NotPerformedPin || request.Action == CustodyKeyLifecycleAction.Unpin && NotPerformedUnpin
            ? CustodyKeyLifecycleStatus.NotPerformed : request.Action switch { CustodyKeyLifecycleAction.Pin => CustodyKeyLifecycleStatus.Pinned, CustodyKeyLifecycleAction.Unpin => CustodyKeyLifecycleStatus.Unpinned, _ => CustodyKeyLifecycleStatus.Destroyed };
        var outcome = new CustodyKeyLifecycleOutcome(request.Identity, request.OperationId, digest, status, "original-physical-" + request.OperationId,
            status == CustodyKeyLifecycleStatus.Destroyed && !OmitRestoreProof ? "independent-all-copy-nonrollback-proof" : null);
        PhysicalOutcomes[request.OperationId] = JsonSerializer.SerializeToUtf8Bytes(outcome);
        return LoseResponse ? Task.FromException<CustodyKeyLifecycleOutcome>(new HttpRequestException("Controlled physical response loss.")) : Task.FromResult(outcome);
    }
    public Task<CustodyKeyLifecycleOutcome> LookupAsync(CustodyKeyRegistration registration, CustodyKeyLifecycleRequest request, string digest, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); return Task.FromResult(!UnknownLookup && PhysicalOutcomes.TryGetValue(request.OperationId, out var bytes)
            ? JsonSerializer.Deserialize<CustodyKeyLifecycleOutcome>(bytes)! : new(request.Identity, request.OperationId, digest, CustodyKeyLifecycleStatus.Unknown));
    }
    internal CustodyKeyLifecycleLedger Persisted => (CustodyKeyLifecycleLedger)Backend.CommittedState.Single().Value;
    internal static CustodyKeyLifecycleActor Create(IActorStateManager backend, ICustodyKeyLifecycleAuthority? authority = null, ICustodyKeyLifecycleProvider? provider = null, TimeProvider? clock = null)
    {
        var actor = new CustodyKeyLifecycleActor(ActorHost.CreateForTest<CustodyKeyLifecycleActor>(new ActorTestOptions { ActorId = new(CustodyKeyLifecycleActor.GetActorId("tenant-a")) }), authority, provider, clock);
        typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(actor, backend); return actor;
    }
    internal static CustodyKeyObjectIdentity Identity(PlatformKeyPurpose purpose = PlatformKeyPurpose.ExportEnvelopeKey) => new("tenant-a", "object-a", purpose, "explicit-alias-a", "original-key-v1", "phase-pinned-lifecycle-v1", "synthetic-store-v1", "candidate-contract-v1");
    internal static CustodyKeyRegistration Registration(CustodyKeyObjectIdentity? identity = null) => new(identity ?? Identity(), "original-wrap", 0, "tenant-kek-v1", "opaque-wrapped-object", "independent-original-wrap-proof");
    internal static CustodyKeyLifecycleRequest Request(CustodyKeyLifecycleAction action, long revision, string id = "original-effect", CustodyKeyObjectIdentity? identity = null)
    {
        identity ??= Identity(); return new(identity, id, action, revision, action == CustodyKeyLifecycleAction.Destroy ? null : "hold-a", "current-linearizable-fence-reservation", 9,
            "synthetic-approved-decision-v1", action == CustodyKeyLifecycleAction.Destroy && identity.Purpose == PlatformKeyPurpose.InteractionRootDek ? "exact-original-protection-reservation" : null);
    }
    internal static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
