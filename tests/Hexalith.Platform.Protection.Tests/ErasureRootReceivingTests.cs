using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual signed receiver transport verifies current explicit private grants, request correlation and revocation.</summary>
public sealed class ErasureRootReceivingTests
{
    [Fact]
    public async Task PrivatePermissionRequiresExactRoleMethodAndCurrentGrant()
    {
        using var fixture = new ErasureRootEngineFixture();
        using var native = Substitute.For<DaprClient>();
        var source = new ErasureRootNativeOccurrenceSource(fixture.Custody, fixture.Permissions, native, fixture.Digests);
        var handler = new ErasureRootAuthorityOperationHandler(fixture.Custody, source, Substitute.For<IGovernanceScopeGuard>(),
            Substitute.For<IDeletionCapabilityRevocationAuthenticator>(), new string('A', 64), TimeProvider.System);
        using var receiver = new ReceivingAuthorityFixture("erc-owner", handlers: [handler]);
        receiver.State = receiver.State with
        {
            Callers = [receiver.State.Callers[0] with { ScopeReference = "tenant-a|erc", Methods = [.. receiver.State.Callers[0].Methods, .. handler.Methods] }],
            QualificationBindings = new() { ["erc.allow.WrapDek"] = "erc-owner" },
        };
        var installation = new ErasureRootInstallation("erc-test", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant-a"] = "epoch" });
        var client = new DaprErasureRootAuthorization(receiver.Authority, fixture.Store, installation);
        (await client.AuthorizeAsync("tenant-a", "root-original", "WrapDek", new string('B', 64), TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await client.AuthorizeAsync("tenant-a", "root-original", "UnwrapDek", new string('B', 64), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await client.AuthorizeAsync("tenant-b", "root-original", "WrapDek", new string('B', 64), TestContext.Current.CancellationToken)).ShouldBeFalse();
        receiver.State = receiver.State with { Callers = [receiver.State.Callers[0] with { Revoked = true }] };
        (await client.AuthorizeAsync("tenant-a", "root-original", "WrapDek", new string('B', 64), TestContext.Current.CancellationToken)).ShouldBeFalse();
    }
}
