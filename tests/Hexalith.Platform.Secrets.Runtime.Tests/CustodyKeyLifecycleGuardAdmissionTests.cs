using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Custody.Component;
using Hexalith.Platform.Secrets.Runtime;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class CustodyKeyLifecycleGuardAdmissionTests
{
    private const string Tenant = "tenant-a";
    private const string Operation = "pin-1";
    private const string Method = "ApplyKeyLifecycle";
    private const long Revision = 2;
    private static readonly string StateDigest = Digest("state");
    private static readonly string RequestDigest = Digest("request");
    private static readonly byte[] Target = [1, 2, 3];

    [Fact]
    public async Task ExactOperation_ChecksPass_AndReadsWriteNothing()
    {
        var journal = Journal();
        var authority = Authority(Enrolled(), journal);
        var ct = TestContext.Current.CancellationToken;
        (await authority.AuthorizeOperationAsync(Key(Tenant), Operation, Method, RequestDigest, ct)).ShouldBeTrue();
        (await authority.VerifyRegistrationAsync(Registration(Tenant, Revision), ct)).ShouldBeTrue();
        (await authority.AuthorizeEffectAsync(Request(Tenant, Revision), ct)).ShouldBeTrue();
        (await authority.ValidateStateAsync(Tenant, Revision, StateDigest, ct)).ShouldBeTrue();
        (await authority.AuthorizeOperationAsync(Key(Tenant), Operation, "LookupKeyLifecycle", RequestDigest, ct)).ShouldBeFalse();
        (await authority.RecordRevisionAsync(Tenant, Revision, Revision + 1, StateDigest, ct)).ShouldBeFalse();
        (await authority.VerifyTransitionAsync(Transition(Tenant, Revision, StateDigest, Target), ct)).ShouldBeFalse();
        (await authority.VerifyOutcomeAsync(Request(Tenant, Revision), Outcome(Tenant), ct)).ShouldBeFalse();
        journal.Writes.ShouldBe(0);
        journal.Head.ShouldBe(Revision);
    }

    [Fact]
    public async Task AnchoredTransitions_AdmitRetains_RecordAdvances_RecoverThenVerify()
    {
        var ct = TestContext.Current.CancellationToken;
        var recorded = Journal();
        var recording = Authority(Enrolled(), recorded);
        var transition = Transition(Tenant, Revision, StateDigest, Target);
        (await recording.AdmitTransitionAsync(transition, ct)).ShouldBeTrue();
        recorded.Writes.ShouldBe(1);
        recorded.Head.ShouldBe(Revision);
        recorded.HeadDigest.ShouldBe(StateDigest);
        recorded.Admitted.ShouldNotBeNull();
        recorded.Admitted.ExpectedRevision.ShouldBe(Revision);
        (await recording.AdmitTransitionAsync(transition, ct)).ShouldBeTrue();
        recorded.Writes.ShouldBe(1);
        (await recording.VerifyTransitionAsync(transition, ct)).ShouldBeFalse();
        recorded.Writes.ShouldBe(1);
        (await recording.RecordTransitionAsync(transition, ct)).ShouldBeTrue();
        recorded.Writes.ShouldBe(2);
        recorded.Head.ShouldBe(Revision + 1);
        recorded.HeadDigest.ShouldBe(Convert.ToHexString(SHA256.HashData(Target)));
        (await recording.VerifyTransitionAsync(transition, ct)).ShouldBeTrue();
        (await recording.RecordTransitionAsync(transition, ct)).ShouldBeFalse();
        (await recording.RecoverTransitionAsync(transition, ct)).ShouldBeFalse();
        recorded.Writes.ShouldBe(2);
        (await recording.AuthorizeOperationAsync(Key(Tenant), Operation, Method, RequestDigest, ct)).ShouldBeTrue();
        (await recording.ValidateStateAsync(Tenant, recorded.Head, recorded.HeadDigest, ct)).ShouldBeTrue();
        (await recording.ValidateStateAsync(Tenant, Revision, StateDigest, ct)).ShouldBeFalse();
        (await recording.AuthorizeEffectAsync(Request(Tenant, Revision), ct)).ShouldBeFalse();
        (await recording.AuthorizeEffectAsync(Request(Tenant, recorded.Head), ct)).ShouldBeFalse();
        recorded.Writes.ShouldBe(2);

        var recovered = Journal();
        var recovering = Authority(Enrolled(), recovered);
        (await recovering.AdmitTransitionAsync(transition, ct)).ShouldBeTrue();
        (await recovering.VerifyTransitionAsync(transition, ct)).ShouldBeFalse();
        (await recovering.RecoverTransitionAsync(transition, ct)).ShouldBeTrue();
        recovered.Head.ShouldBe(Revision);
        recovered.HeadDigest.ShouldBe(StateDigest);
        recovered.Writes.ShouldBe(2);
        (await recovering.VerifyTransitionAsync(transition, ct)).ShouldBeTrue();
        (await recovering.RecoverTransitionAsync(transition, ct)).ShouldBeTrue();
        (await recovering.AdmitTransitionAsync(transition, ct)).ShouldBeTrue();
        recovered.Writes.ShouldBe(2);
        recovered.Head.ShouldBe(Revision);
    }

    [Fact]
    public async Task ChangedDigest_ReturnsFalse_AndWritesNothing()
    {
        var journal = Journal();
        var guard = Enrolled();
        var authority = Authority(guard, journal);
        var ct = TestContext.Current.CancellationToken;
        string other = Digest("other");
        guard.Accepts(Tenant, Operation, Method, other, Revision, StateDigest).ShouldBeFalse();
        (await authority.AuthorizeOperationAsync(Key(Tenant), Operation, Method, other, ct)).ShouldBeFalse();
        (await authority.ValidateStateAsync(Tenant, Revision, other, ct)).ShouldBeFalse();
        var changed = Transition(Tenant, Revision, other, Target);
        (await authority.AdmitTransitionAsync(changed, ct)).ShouldBeFalse();
        (await authority.RecordTransitionAsync(changed, ct)).ShouldBeFalse();
        (await authority.RecoverTransitionAsync(changed, ct)).ShouldBeFalse();
        journal.TryAdmit(changed).ShouldBeFalse();
        journal.Writes.ShouldBe(0);
        journal.Admitted.ShouldBeNull();
    }

    [Fact]
    public async Task ForeignTenantAndActorScope_ReturnFalse_AndWriteNothing()
    {
        var journal = Journal();
        var authority = Authority(Enrolled(), journal);
        var ct = TestContext.Current.CancellationToken;
        (await authority.AuthorizeOperationAsync(Key("tenant-b"), Operation, Method, RequestDigest, ct)).ShouldBeFalse();
        (await authority.VerifyRegistrationAsync(Registration("tenant-b", Revision), ct)).ShouldBeFalse();
        (await authority.AuthorizeEffectAsync(Request("tenant-b", Revision), ct)).ShouldBeFalse();
        (await authority.ValidateStateAsync("tenant-b", Revision, StateDigest, ct)).ShouldBeFalse();
        (await authority.AdmitTransitionAsync(Transition("tenant-b", Revision, StateDigest, Target), ct)).ShouldBeFalse();
        var otherActor = Transition(Tenant, Revision, StateDigest, Target) with { ScopeId = "tenant-a:custody:other|custody-key-lifecycle-candidate-v1" };
        (await authority.AdmitTransitionAsync(otherActor, ct)).ShouldBeFalse();
        var glued = Transition(Tenant, Revision, StateDigest, Target) with { ScopeId = CustodyKeyLifecycleActor.GetActorId(Tenant) + "-extra|custody-key-lifecycle-candidate-v1" };
        (await authority.RecordTransitionAsync(glued, ct)).ShouldBeFalse();
        journal.Writes.ShouldBe(0);
    }

    [Fact]
    public async Task StaleOrHigherRevision_ReturnsFalse_AndWritesNothing()
    {
        var journal = Journal();
        var guard = Enrolled();
        var authority = Authority(guard, journal);
        var ct = TestContext.Current.CancellationToken;
        guard.Accepts(Tenant, Operation, Method, RequestDigest, Revision - 1, StateDigest).ShouldBeFalse();
        guard.Accepts(Tenant, Operation, Method, RequestDigest, Revision + 1, StateDigest).ShouldBeFalse();
        (await authority.ValidateStateAsync(Tenant, Revision - 1, StateDigest, ct)).ShouldBeFalse();
        (await authority.ValidateStateAsync(Tenant, Revision + 1, StateDigest, ct)).ShouldBeFalse();
        (await authority.AuthorizeEffectAsync(Request(Tenant, Revision - 1), ct)).ShouldBeFalse();
        (await authority.AuthorizeEffectAsync(Request(Tenant, Revision + 1), ct)).ShouldBeFalse();
        (await authority.VerifyRegistrationAsync(Registration(Tenant, Revision - 1), ct)).ShouldBeFalse();
        var stale = Transition(Tenant, Revision - 1, StateDigest, Target);
        var ahead = Transition(Tenant, Revision + 1, StateDigest, Target);
        (await authority.AdmitTransitionAsync(stale, ct)).ShouldBeFalse();
        (await authority.AdmitTransitionAsync(ahead, ct)).ShouldBeFalse();
        (await authority.RecoverTransitionAsync(stale, ct)).ShouldBeFalse();
        (await authority.RecordTransitionAsync(ahead, ct)).ShouldBeFalse();
        journal.TryAdmit(stale).ShouldBeFalse();
        journal.TryRecord(Transition(Tenant, Revision, StateDigest, Target)).ShouldBeFalse();
        journal.TryRecover(stale).ShouldBeFalse();
        journal.Writes.ShouldBe(0);

        var constructedAhead = new CustodyNonrollbackJournal(Revision + 1, Digest("next"));
        (await Authority(guard, constructedAhead).AuthorizeOperationAsync(Key(Tenant), Operation, Method, RequestDigest, ct)).ShouldBeFalse();
        (await Authority(guard, constructedAhead).ValidateStateAsync(Tenant, Revision + 1, constructedAhead.HeadDigest, ct)).ShouldBeFalse();
        constructedAhead.Writes.ShouldBe(0);
    }

    [Fact]
    public async Task MissingGuardOrJournal_Denies_AndWitnessOutcomeStaysARead()
    {
        var ct = TestContext.Current.CancellationToken;
        var journal = Journal();
        var witness = new CustodyOutsideWitness();
        var request = Request(Tenant, Revision);
        var outcome = Outcome(Tenant);
        witness.Remember(new CustodyWitnessFact(Operation, outcome.RequestDigest, CustodyComponentStatus.Pinned, "receipt-1", null, request.Identity, "hold-a"));
        AnchoredStateTransition transition = Transition(Tenant, Revision, StateDigest, Target);

        var missingGuard = new CustodyKeyLifecycleBindingAuthority(witness, null, journal);
        await DeniesAdmission(missingGuard, transition);
        (await missingGuard.VerifyOutcomeAsync(request, outcome, ct)).ShouldBeTrue();
        (await missingGuard.VerifyOutcomeAsync(request, outcome with { ReceiptId = "substituted" }, ct)).ShouldBeFalse();
        journal.Writes.ShouldBe(0);

        var missingJournal = new CustodyKeyLifecycleBindingAuthority(witness, Enrolled(), null);
        await DeniesAdmission(missingJournal, transition);
        (await missingJournal.VerifyOutcomeAsync(request, outcome, ct)).ShouldBeTrue();
        journal.Writes.ShouldBe(0);

        var neither = new CustodyKeyLifecycleBindingAuthority(witness);
        await DeniesAdmission(neither, transition);
        (await neither.VerifyOutcomeAsync(request, outcome, ct)).ShouldBeTrue();
        (await new CustodyKeyLifecycleBindingAuthority(null, Enrolled(), Journal()).VerifyOutcomeAsync(request, outcome, ct)).ShouldBeFalse();
    }

    [Fact]
    public void EnrollmentAcceptsOnlyTheInstalledCustodyComponent()
    {
        Should.Throw<ArgumentException>(() => CustodyInstalledGuard.Enroll("other", CustodyBindingContract.LogicalRole, CustodyInstalledGuard.Component, Tenant, Operation, Method, RequestDigest, Revision, StateDigest));
        Should.Throw<ArgumentException>(() => CustodyInstalledGuard.Enroll(CustodyBindingContract.Protocol, "other-role", CustodyInstalledGuard.Component, Tenant, Operation, Method, RequestDigest, Revision, StateDigest));
        Should.Throw<ArgumentException>(() => CustodyInstalledGuard.Enroll(CustodyBindingContract.Protocol, CustodyBindingContract.LogicalRole, "bindings.other", Tenant, Operation, Method, RequestDigest, Revision, StateDigest));
        var enrolled = Enrolled();
        enrolled.Accepts(Tenant, Operation, Method, RequestDigest, Revision, StateDigest).ShouldBeTrue();
    }

    private static async Task DeniesAdmission(CustodyKeyLifecycleBindingAuthority authority, AnchoredStateTransition transition)
    {
        var ct = TestContext.Current.CancellationToken;
        (await authority.AuthorizeOperationAsync(Key(Tenant), Operation, Method, RequestDigest, ct)).ShouldBeFalse();
        (await authority.VerifyRegistrationAsync(Registration(Tenant, Revision), ct)).ShouldBeFalse();
        (await authority.AuthorizeEffectAsync(Request(Tenant, Revision), ct)).ShouldBeFalse();
        (await authority.ValidateStateAsync(Tenant, Revision, StateDigest, ct)).ShouldBeFalse();
        (await authority.AdmitTransitionAsync(transition, ct)).ShouldBeFalse();
        (await authority.RecordTransitionAsync(transition, ct)).ShouldBeFalse();
        (await authority.RecoverTransitionAsync(transition, ct)).ShouldBeFalse();
        (await authority.VerifyTransitionAsync(transition, ct)).ShouldBeFalse();
        (await authority.RecordRevisionAsync(Tenant, Revision, Revision + 1, StateDigest, ct)).ShouldBeFalse();
    }

    private static CustodyInstalledGuard Enrolled()
        => CustodyInstalledGuard.Enroll(CustodyBindingContract.Protocol, CustodyBindingContract.LogicalRole, CustodyInstalledGuard.Component, Tenant, Operation, Method, RequestDigest, Revision, StateDigest);

    private static CustodyNonrollbackJournal Journal() => new(Revision, StateDigest);

    private static CustodyKeyLifecycleBindingAuthority Authority(ICustodyInstalledGuard guard, ICustodyNonrollbackJournal journal)
        => new(null, guard, journal);

    private static CustodyKeyObjectIdentity Key(string tenant)
        => new(tenant, "interaction-0001", PlatformKeyPurpose.InteractionRootDek, "alias", "v1", "lifecycle", "store-1", "custody-lifecycle-v1");

    private static CustodyKeyRegistration Registration(string tenant, long revision)
        => new(Key(tenant), Operation, revision, "kek-v1", "opaque-wrap", "wrap-receipt");

    private static CustodyKeyLifecycleRequest Request(string tenant, long revision)
        => new(Key(tenant), Operation, CustodyKeyLifecycleAction.Pin, revision, "hold-a", "fence-token", 2, "decision");

    private static CustodyKeyLifecycleOutcome Outcome(string tenant)
        => new(Key(tenant), Operation, Digest("witness"), CustodyKeyLifecycleStatus.Pinned, "receipt-1");

    private static AnchoredStateTransition Transition(string tenant, long revision, string predecessor, byte[] target)
        => new(CustodyKeyLifecycleActor.GetActorId(tenant) + "|custody-key-lifecycle-candidate-v1", revision, revision + 1, predecessor, Convert.ToHexString(SHA256.HashData(target)), target);

    private static string Digest(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
