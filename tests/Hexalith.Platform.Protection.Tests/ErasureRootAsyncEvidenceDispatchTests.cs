using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Server.Tests.Security;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Concrete production async Dapr adapter traverses the actual published-trust caller and nonce-bound signed receiving transport.</summary>
public sealed class ErasureRootAsyncEvidenceDispatchTests
{
    [Fact]
    public async Task PublishedAuthorityRequiresCurrentConcreteAsyncEvidenceForRevocationActivationAndReplacement()
    {
        var fixture = new ErasureRootReplacementEvidenceFixture(); var token = TestContext.Current.CancellationToken;
        bool available = false;
        var handler = Substitute.For<IIndependentOwnerOperationHandler>();
        handler.Methods.Returns(new HashSet<string> { "erc-registrar-proof" });
        handler.IsReadOnly("erc-registrar-proof").Returns(true);
        handler.RequiresCurrentResultAdmission("erc-registrar-proof").Returns(true);
        handler.HandleAsync("erc-registrar-proof", Arg.Any<JsonElement>(), Arg.Any<IndependentCallerGrant>(), Arg.Any<IndependentAuthorityState>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<JsonElement>();
            return (object?)new ErasureRootPermissionEvidence(request.GetProperty("tenantId").GetString()!, request.GetProperty("identity").GetString()!,
                request.GetProperty("operation").GetString()!, request.GetProperty("requestDigest").GetString()!, "current-independent-component-receipt", available);
        });
        using var receiver = new ReceivingAuthorityFixture("erc-owner", handlers: [handler]);
        receiver.State = receiver.State with { Callers = [receiver.State.Callers[0] with { ScopeReference = "tenant-a|erc", Methods = ["erc-registrar-proof"] }] };
        var evidence = new DaprErasureRootRevocationEvidence(receiver.Authority);
        // The revocation fixture's registrar audience has its own independently retained public trust binding.
        var registrarTrust = fixture.Trust.Single(value => value.CapabilityKeyVersion == fixture.Blocked.RevocationReceipt.Envelope.KeyVersion) with { Audience = fixture.Blocked.RevocationReceipt.Envelope.Audience };
        var authority = new PublishedTrustDeletionConsumptionAuthority([.. fixture.Trust, registrarTrust], fixture.Guard, fixture.Journal, evidence);
        foreach (bool current in new[] { false, true, false })
        {
            available = current;
            (await authority.VerifyRevocationAsync(fixture.Blocked.RevocationReceipt.Envelope, token)).ShouldBe(current);
            (await authority.VerifyActivationAsync(fixture.Activation, token)).ShouldBe(current);
            (await authority.VerifyBlockedReplacementAsync(fixture.Blocked, token)).ShouldBe(current);
        }
        receiver.Methods.Count(method => method == "erc-registrar-proof").ShouldBeGreaterThanOrEqualTo(9);
        evidence.Matches(fixture.Blocked.RevocationReceipt.Envelope).ShouldBeFalse(); evidence.Pending(fixture.Blocked.RevocationReceipt.Envelope).ShouldBeTrue();
    }
}
