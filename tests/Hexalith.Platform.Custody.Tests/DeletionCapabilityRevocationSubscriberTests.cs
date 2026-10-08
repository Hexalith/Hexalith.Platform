using Hexalith.EventStore.Contracts.Security;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Private authenticated source/protection-block/guard-mirror ordering; independent enrollment and owners are synthetic.</summary>
public sealed class DeletionCapabilityRevocationSubscriberTests
{
    private static DeletionCapabilityRevocationEnvelope Envelope() => new("independent-issuer", "protection-target", "tenant-a",
        "DeletionBatchCapabilitySigningKey", "compromised-v1", 4, 3, "revocation-event", new string('A', 64));

    /// <summary>Lost acknowledgements and restarted duplicate delivery reuse the exact protection block before the mirror.</summary>
    [Fact]
    public async Task LostBlockAndMirrorAcknowledgementsResolveOriginalExactReceiptInOrder()
    {
        var clock = new CustodyFixtureClock(); var envelope = Envelope();
        var authenticator = Substitute.For<IDeletionCapabilityRevocationAuthenticator>();
        authenticator.VerifyAsync(envelope, "signed-independent-evidence", Arg.Any<CancellationToken>()).Returns(
            new DeletionCapabilityRevocationAuthorization(envelope, "current-independent-authority", clock.Now, clock.Now.AddMinutes(1)));
        var registrar = Substitute.For<IDeletionCapabilityCompromiseRegistrar>();
        var mirror = Substitute.For<IDeletionCapabilityGuardRevocationMirror>();
        var receipt = new DeletionCapabilityRevocationReceipt(envelope, 5, 1, "original-block", ["batch-a", "batch-b"]);
        DeletionCapabilityRevocationReceipt? blocked = null; DeletionCapabilityRevocationReceipt? mirrored = null;
        var order = new List<string>();
        registrar.LookupAsync(envelope, Arg.Any<CancellationToken>()).Returns(_ => blocked);
        registrar.RegisterAsync(envelope, Arg.Any<CancellationToken>()).Returns<DeletionCapabilityRevocationReceipt?>(_ =>
        { order.Add("block"); blocked = receipt; throw new IOException("lost block acknowledgement"); });
        mirror.LookupAsync(envelope, Arg.Any<CancellationToken>()).Returns(_ => mirrored);
        mirror.RecordAsync(Arg.Any<DeletionCapabilityRevocationReceipt>(), Arg.Any<CancellationToken>()).Returns<bool>(call =>
        { order.Add("mirror"); blocked.ShouldNotBeNull(); mirrored = call.Arg<DeletionCapabilityRevocationReceipt>(); throw new IOException("lost mirror acknowledgement"); });
        var subscriber = new DeletionCapabilityRevocationSubscriber(new(envelope.Issuer, envelope.Audience, envelope.TenantId),
            authenticator, registrar, mirror, clock);
        (await subscriber.ReceiveAsync(envelope, "signed-independent-evidence", TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await new DeletionCapabilityRevocationSubscriber(new(envelope.Issuer, envelope.Audience, envelope.TenantId),
            authenticator, registrar, mirror, clock).ReceiveAsync(envelope, "signed-independent-evidence", TestContext.Current.CancellationToken)).ShouldBeTrue();
        order.ShouldBe(["block", "mirror"]); mirrored!.ReceiptId.ShouldBe("original-block"); mirrored.AffectedBatchIds.ShouldBe(["batch-a", "batch-b"]);
    }

    /// <summary>Scope/source authentication failures perform no protection or guard lookup/effect.</summary>
    [Theory]
    [InlineData("foreign")]
    [InlineData("audience")]
    [InlineData("family")]
    [InlineData("digest")]
    [InlineData("unauthenticated")]
    public async Task WrongEnvelopeOrIndependentAuthenticationDeniesBeforeOwnerCalls(string vector)
    {
        var clock = new CustodyFixtureClock(); var original = Envelope();
        var envelope = vector switch { "foreign" => original with { TenantId = "tenant-b" }, "audience" => original with { Audience = "public-api" },
            "family" => original with { KeyFamily = "DecisionApprovalSigningKey" }, "digest" => original with { SignatureDigest = "bad" }, _ => original };
        var auth = Substitute.For<IDeletionCapabilityRevocationAuthenticator>();
        var registrar = Substitute.For<IDeletionCapabilityCompromiseRegistrar>(); var mirror = Substitute.For<IDeletionCapabilityGuardRevocationMirror>();
        (await new DeletionCapabilityRevocationSubscriber(new(original.Issuer, original.Audience, original.TenantId), auth, registrar, mirror, clock)
            .ReceiveAsync(envelope, "evidence", TestContext.Current.CancellationToken)).ShouldBeFalse();
        registrar.ReceivedCalls().ShouldBeEmpty(); mirror.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Unknown, changed or stale protection evidence cannot reach guard mirroring.</summary>
    [Theory]
    [InlineData("unknown")]
    [InlineData("changed")]
    [InlineData("expiry")]
    public async Task UnknownChangedOrExpiredBlockCannotMirror(string vector)
    {
        var clock = new CustodyFixtureClock(); var envelope = Envelope();
        var auth = Substitute.For<IDeletionCapabilityRevocationAuthenticator>();
        var authorization = new DeletionCapabilityRevocationAuthorization(envelope, "authority", clock.Now, clock.Now.AddMinutes(1));
        auth.VerifyAsync(envelope, "evidence", Arg.Any<CancellationToken>()).Returns(authorization);
        var registrar = Substitute.For<IDeletionCapabilityCompromiseRegistrar>(); var mirror = Substitute.For<IDeletionCapabilityGuardRevocationMirror>();
        registrar.LookupAsync(envelope, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (vector == "expiry") { clock.Now = authorization.ValidUntil; }
            return vector == "unknown" ? null : new DeletionCapabilityRevocationReceipt(vector == "changed" ? envelope with { KeyVersion = "other" } : envelope,
                5, 1, "block", ["batch-a"]);
        });
        (await new DeletionCapabilityRevocationSubscriber(new(envelope.Issuer, envelope.Audience, envelope.TenantId), auth, registrar, mirror, clock)
            .ReceiveAsync(envelope, "evidence", TestContext.Current.CancellationToken)).ShouldBeFalse();
        mirror.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Ordinary custody registration gives public/Workflow/general-dispatcher services no compromise capability.</summary>
    [Fact]
    public void OrdinaryCustodyCompositionDoesNotExposeCompromiseRegistrar()
    {
        using var services = new ServiceCollection().AddPlatformCustody().BuildServiceProvider();
        services.GetService<IDeletionCapabilityCompromiseRegistrar>().ShouldBeNull();
        services.GetService<DeletionCapabilityRevocationSubscriber>().ShouldBeNull();
        services.GetService<IDeletionCapabilityGuardRevocationMirror>().ShouldBeNull();
    }
}
