using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Duplicate refused-issue originals must fail closed without throwing out of the handler.</summary>
public sealed class ErasureRootRejectedIssueDuplicateTests
{
    [Fact]
    public async Task TwoMatchingGuardAttemptsReturnNullAndOneMatchReturnsTheOriginal()
    {
        var token = TestContext.Current.CancellationToken;
        using var fixture = new ErasureRootEngineFixture();
        using var native = Substitute.For<DaprClient>();
        var guard = Substitute.For<IGovernanceScopeGuard>();
        guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new TenantGovernanceGuardState("tenant-a", "install", 1, "epoch", "legacy", "writer", null, [], [], [], [], [], []));
        var handler = new ErasureRootAuthorityOperationHandler(fixture.Custody, new(fixture.Custody, fixture.Permissions, native, fixture.Digests), guard, Substitute.For<IDeletionCapabilityRevocationAuthenticator>(), new string('A', 64), TimeProvider.System);
        var capability = new DeletionBatchCapabilityV1("issuer", "audience", "tenant-a", "request", "seal", "AcceptedSet", 0, "batch", new string('A', 64), "guard", 1, 1, 1, "key");
        var batch = new GovernanceBatchCommand("batch", "AcceptedSet", 0, [new("tenant-a", "interaction-a", "alias")], new string('A', 64), 1, "key", new string('B', 64), "blocked", "receipt", [], 1)
        { Capability = capability, DetachedJws = "jws", SigningRequestId = "signing-1" };
        var transition = new GovernanceGuardTransition("tenant-a", "replace-unissued", GovernanceGuardOperation.ReplaceAttestation, 1, "epoch", "request", null, null, null, null, null, batch, "authorization", "reference");
        var receipt = new GovernanceProtocolReceipt("replace-unissued", new string('C', 64), "Stale", 1, 1, "reference", "receipt-id")
        { Capability = capability, SigningRequestId = "signing-1", DetachedJwsDigest = "digest-1" };
        var attempt = new ErasureRootGuardAttempt(transition, new string('D', 64), 1, 1, receipt);
        var head = await fixture.Store.HeadAsync("tenant-a");
        head.GuardAttempts["one"] = attempt;
        head.GuardAttempts["two"] = attempt;
        fixture.Store.ReplaceHead("tenant-a", head);
        var caller = new IndependentCallerGrant(new string('e', 64), "owner", "tenant-a|erc", "erc-owner", ["erc-read-rejected-issue"], DateTimeOffset.UtcNow.AddHours(1), false);
        var authority = new IndependentAuthorityState("enrollment", "scope", "trust", 1, DateTimeOffset.UtcNow.AddHours(1), [caller], null, 0, new string('F', 64), new(), new(), new(), new(), new(), new());
        var payload = JsonSerializer.SerializeToElement(new { payload = capability, signingRequestId = "signing-1", detachedJwsDigest = "digest-1" });
        (await handler.HandleAsync("erc-read-rejected-issue", payload, caller, authority, token)).ShouldBeNull();
        head = await fixture.Store.HeadAsync("tenant-a");
        head.GuardAttempts.Remove("two");
        fixture.Store.ReplaceHead("tenant-a", head);
        (await handler.HandleAsync("erc-read-rejected-issue", payload, caller, authority, token)).ShouldNotBeNull();
    }
}
