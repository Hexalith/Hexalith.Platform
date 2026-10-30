using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual signed receiving boundaries reject contradictory native identities before every source/guard/owner effect and scope retirement exactly.</summary>
public sealed class ErasureRootLoop3ReceivingTests
{
    [Fact]
    public async Task OwnGrantCannotRelabelForeignSourceGuardOrOwnerIdentity()
    {
        var token = TestContext.Current.CancellationToken;
        using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>();
        var guard = Substitute.For<IGovernanceScopeGuard>();
        var source = new ErasureRootNativeOccurrenceSource(fixture.Custody, fixture.Permissions, native, fixture.Digests);
        var handler = new ErasureRootAuthorityOperationHandler(fixture.Custody, source, guard, Substitute.For<IDeletionCapabilityRevocationAuthenticator>(), new string('A', 64), TimeProvider.System);
        using var receiver = new ReceivingAuthorityFixture("erc-owner", handlers: [handler]);
        receiver.State = receiver.State with { Callers = [receiver.State.Callers[0] with { ScopeReference = "tenant-a|erc", Methods = [.. handler.Methods] }] };
        var original = fixture.Enroll();
        string before = ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a"));
        var foreign = original with { RootIdentity = original.RootIdentity with { TenantId = "tenant-b" } };
        (await receiver.Authority.InvokeAsync<bool>("erc-register-source-original", new { tenantId = "tenant-a", enrollment = foreign }, token)).ShouldBeFalse();
        var transition = new GovernanceGuardTransition("tenant-b", "foreign", GovernanceGuardOperation.RegisterHold, 0, "epoch", "request", null, null, null, null, null, null, "", "");
        (await receiver.Authority.InvokeAsync<GovernanceProtocolReceipt>("erc-execute-guard", new { tenantId = "tenant-a", transition, targets = Array.Empty<GuardedStateMutation>() }, token)).ShouldBeNull();
        (await receiver.Authority.InvokeAsync<ErasureRootOccurrenceEnrollment>("erc-occurrence-original", new { tenantId = "tenant-a", owner = new Hexalith.EventStore.Contracts.Identity.AggregateIdentity("tenant-b", original.Binding.Identity.Owner.Domain, original.Binding.Identity.Owner.AggregateId), occurrence = ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity) }, token)).ShouldBeNull();
        guard.ReceivedCalls().ShouldBeEmpty(); native.ReceivedCalls().ShouldBeEmpty();
        ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a")).ShouldBe(before);
        var own = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        own.Head.SourceOriginals.Add(ErasureRootOccurrenceBindings.Id(original.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity)), original);
        (await fixture.Custody.SaveAsync("tenant-a", own.Version, own.Head, token)).ShouldBeTrue();
        (await new DaprErasureRootOccurrenceSource(receiver.Authority).ResolveAsync(original.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity), token)).ShouldBe(original);
        _ = await new DaprErasureRootGovernanceGuard(receiver.Authority).ReadAsync("tenant-a", token);
        await guard.Received(1).ReadAsync("tenant-a", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualPublishedTrustProtocolScopeAuthenticatesItsExactTenantWithoutAcceptingALabelOverride()
    {
        var token = TestContext.Current.CancellationToken;
        using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>();
        var guard = Substitute.For<IGovernanceScopeGuard>();
        guard.ReadAsync("tenant-a", Arg.Any<CancellationToken>()).Returns(new TenantGovernanceGuardState("tenant-a", "installation", 1, "epoch", "legacy", "writer", null, [], [], [], [],
            [new("guard-operation", new string('A', 64), "Committed", 1, 0, "request", "receipt")], []));
        var handler = new ErasureRootAuthorityOperationHandler(fixture.Custody, new(fixture.Custody, fixture.Permissions, native, fixture.Digests), guard,
            Substitute.For<IDeletionCapabilityRevocationAuthenticator>(), new string('A', 64), TimeProvider.System);
        using var receiver = new ReceivingAuthorityFixture("erc-owner", handlers: [handler]);
        receiver.State = receiver.State with { Callers = [receiver.State.Callers[0] with { ScopeReference = "tenant-a|erc", Methods = [.. handler.Methods] }] };
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes("admission-block\ntenant-a\nrequest\n1\nreceipt");
        var transition = new AnchoredStateTransition("published-trust|admission-block|tenant-a|request", 0, 1, new string('B', 64), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)), bytes);
        (await new DaprErasureRootEvidenceJournal(receiver.Authority).VerifyTransitionAsync(transition, token)).ShouldBeTrue();
        guard.ClearReceivedCalls();
        (await receiver.Authority.InvokeAsync<ErasureRootPermissionEvidence>("erc-current-protocol-evidence", new { tenantId = "tenant-b", transition }, token)).ShouldBeNull();
        (await new DaprErasureRootEvidenceJournal(receiver.Authority).VerifyTransitionAsync(transition with { ScopeId = "published-trust|admission-block|tenant-b|request" }, token)).ShouldBeFalse();
        guard.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task LegacyArchivedSigningAdmissionsMigrateOnlyUnderExplicitCurrentPermission()
    {
        var token = TestContext.Current.CancellationToken;
        using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>();
        var signed = new ErasureRootReplacementEvidenceFixture(); var capability = signed.Replacement.Capability;
        var batch = new GovernanceBatchCommand(capability.BatchId, capability.BatchKind, capability.BatchOrdinal,
            signed.Replacement.Targets.Select(target => new GovernanceProtectionTarget(target.TenantId, target.AgentInteractionId, target.TargetProtectionKeyAlias)).ToArray(), capability.ManifestDigest,
            capability.AttestationOrdinal, capability.CapabilityKeyVersion, "", "", "", [], 0)
            { Capability = capability, DetachedJws = signed.Replacement.DetachedJws, SigningRequestId = signed.Blocked.SigningRequestId };
        var transition = new GovernanceGuardTransition("tenant-a", "legacy-replacement-attempt", GovernanceGuardOperation.ReplaceAttestation, 1, "epoch", capability.DeletionRequestId, null, null, null, null, null, batch, "", "");
        string intent = ErasureRootCustody.Hash(new { Transition = transition, TargetMutationDigest = ErasureRootCustody.Hash(Array.Empty<GuardedStateMutation>()) });
        var initial = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        initial.Head.GuardAdmissions.Add(intent, new(transition, [], 1));
        (await fixture.Custody.SaveAsync("tenant-a", initial.Version, initial.Head, token)).ShouldBeTrue();
        var legacy = await fixture.Store.HeadAsync("tenant-a"); legacy.GuardSigningIntents.Clear(); fixture.Store.ReplaceHead("tenant-a", legacy);
        var handler = new ErasureRootAuthorityOperationHandler(fixture.Custody, new(fixture.Custody, fixture.Permissions, native, fixture.Digests), Substitute.For<IGovernanceScopeGuard>(),
            Substitute.For<IDeletionCapabilityRevocationAuthenticator>(), new string('A', 64), TimeProvider.System);
        using var receiver = new ReceivingAuthorityFixture("erc-owner", handlers: [handler]);
        receiver.State = receiver.State with { Callers = [receiver.State.Callers[0] with { ScopeReference = "tenant-a|erc", Methods = [.. handler.Methods] }] };
        var client = new DaprErasureRootGovernanceGuard(receiver.Authority);
        string before = ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a"));
        (await client.MigrateLegacyHistoryAsync("tenant-a", "refused-migration", token)).ShouldBeFalse(); ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a")).ShouldBe(before);
        receiver.State = receiver.State with { QualificationBindings = new() { ["erc.allow.MigrateGuardArchiveIndexes"] = "erc-owner" } };
        (await client.MigrateLegacyHistoryAsync("tenant-a", "authorized-migration", token)).ShouldBeTrue();
        var current = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value.Head;
        current.GuardSigningIntents[signed.Blocked.SigningRequestId].ShouldBe(intent);
        ErasureRootCustody.Hash((await fixture.Custody.ReadGuardAdmissionAsync("tenant-a", current, intent, token))!.Transition).ShouldBe(ErasureRootCustody.Hash(transition));
        fixture.Store.TrimCount.ShouldBe(0); current.Targets.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("other-tenant")]
    [InlineData("former-store")]
    [InlineData("former-enrollment")]
    [InlineData("malformed")]
    [InlineData("unaccepted-policy")]
    [InlineData("current")]
    public async Task SignedPermissionRetirementAppliesOnlyToAuthenticatedCurrentInstallation(string mode)
    {
        var token = TestContext.Current.CancellationToken;
        using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>();
        string policy = new('A', 64);
        var handler = new ErasureRootAuthorityOperationHandler(fixture.Custody, new(fixture.Custody, fixture.Permissions, native, fixture.Digests), Substitute.For<IGovernanceScopeGuard>(), Substitute.For<IDeletionCapabilityRevocationAuthenticator>(), policy, TimeProvider.System);
        using var receiver = new ReceivingAuthorityFixture("erc-owner", handlers: [handler]);
        var receipt = new ErasureRootPermanentLossReceipt(mode == "former-store" ? "former" : "erc-test", mode == "former-enrollment" ? "former" : "enrollment",
            mode == "other-tenant" ? "tenant-b" : "tenant-a", "volume", "independent-retirement", mode == "unaccepted-policy" ? new string('B', 64) : policy);
        receiver.State = receiver.State with { Callers = [receiver.State.Callers[0] with { ScopeReference = "tenant-a|erc", Methods = [.. handler.Methods] }],
            QualificationBindings = new() { ["erc.allow.WrapDek"] = "erc-owner", ["erc.copyPolicySha256"] = policy, ["erc.volume." + receipt.StoreId] = "volume" },
            QualificationCases = new() { ["erc.permanent-loss:" + receipt.StoreId + ":" + receipt.EnrollmentId] = mode == "malformed" ? JsonSerializer.SerializeToElement("malformed") : JsonSerializer.SerializeToElement(receipt) } };
        var client = new DaprErasureRootAuthorization(receiver.Authority, fixture.Store, fixture.Custody.Installation);
        (await client.AuthorizeAsync("tenant-a", "root", "WrapDek", new string('B', 64), token)).ShouldBe(mode != "current");
    }
}
