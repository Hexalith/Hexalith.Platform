using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Identity;

using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Identity.Tests;

public sealed class PlatformIdentityJwtReaderTests
{
    private const string Actor = "01HX0000000000000000000001";
    private const string Operation = "Hexalith.Parties.Contracts.Queries.ResolvePartyIdentity";

    [Fact]
    public async Task ExplicitJwtServiceReaderSignsExactScopeAndCurrentRegistryActor()
    {
        using RSA key = RSA.Create(2048);
        var signing = Substitute.For<IOptionsMonitor<IdentityAdmissionSigningOptions>>();
        signing.CurrentValue.Returns(new IdentityAdmissionSigningOptions { PrivateKeyPem = key.ExportRSAPrivateKeyPem() });
        var verification = Substitute.For<IOptionsMonitor<IdentityAdmissionOptions>>();
        verification.CurrentValue.Returns(new IdentityAdmissionOptions { VerificationKeyPem = key.ExportSubjectPublicKeyInfoPem(), AuthorityRevision = 1 });
        var signer = new IdentityAdmissionSigner(signing);
        var verifier = new IdentityAdmissionProof(verification, TimeProvider.System);
        var options = Microsoft.Extensions.Options.Options.Create(Options());
        var proxy = Substitute.For<IIdentityActorRegistryActor>();
        proxy.ReadAsync(Actor, false, Arg.Any<string>(), Arg.Any<string>()).Returns(new ActorRegistryEntry(Actor, 7, true, "provenance"));
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        factory.CreateActorProxy<IIdentityActorRegistryActor>(Arg.Any<ActorId>(), Arg.Any<string>()).Returns(proxy);
        var registry = new PlatformActorRegistry(factory, signer, options, TimeProvider.System);
        var admission = new PlatformIdentityGatewayAdmission(options, signer, registry, new ClaimsTenantValidator(),
            new ClaimsRbacValidator(), TimeProvider.System, new PlatformOperatorProvenanceVerifier(verifier, options));
        byte[] payload = Payload();
        IdentityAdmissionScope scope = Scope(payload);
        string proof = (await admission.AdmitAsync(Reader(), scope, payload, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        IdentityAdmissionEvidence evidence = verifier.Verify(proof, scope).ShouldNotBeNull();
        evidence.SourceId.ShouldBe("works-reader");
        evidence.TargetActorId.ShouldBe(Actor);
        evidence.ActorRevision.ShouldBe(7);
        evidence.ActorActive.ShouldBeTrue();
        evidence.OperatorActorId.ShouldBeNull();
        verifier.Verify(proof, scope with { TenantId = "tenant-other" }).ShouldBeNull();
        verifier.Verify(proof, scope with { AggregateId = "party-other" }).ShouldBeNull();
        verifier.Verify(proof, scope with { MessageId = "other" }).ShouldBeNull();
        verifier.Verify(proof, scope with { PayloadDigest = IdentityAdmissionProof.Digest(Payload("other")) }).ShouldBeNull();
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("not-allowed")]
    [InlineData("unauthenticated")]
    [InlineData("human")]
    [InlineData("issuer")]
    [InlineData("client")]
    [InlineData("expired")]
    [InlineData("tenant")]
    [InlineData("rbac")]
    [InlineData("digest")]
    [InlineData("party")]
    [InlineData("actor")]
    [InlineData("inactive")]
    [InlineData("missing")]
    public async Task UntrustedOrMismatchedReaderCannotMintIdentityAuthority(string failure)
    {
        var configured = Options();
        if (failure == "disabled") configured.JwtReaders = [];
        if (failure == "not-allowed") configured.ReaderSources = [];
        var options = Microsoft.Extensions.Options.Options.Create(configured);
        var proxy = Substitute.For<IIdentityActorRegistryActor>();
        if (failure != "missing") proxy.ReadAsync(Arg.Any<string>(), false, Arg.Any<string>(), Arg.Any<string>())
            .Returns(new ActorRegistryEntry(failure == "actor" ? "wrong-actor" : Actor, 7, failure != "inactive", "provenance"));
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        factory.CreateActorProxy<IIdentityActorRegistryActor>(Arg.Any<ActorId>(), Arg.Any<string>()).Returns(proxy);
        var signer = Substitute.For<IIdentityAdmissionSigner>();
        var registry = new PlatformActorRegistry(factory, signer, options, TimeProvider.System);
        var operatorVerifier = Substitute.For<IIdentityAdmissionProof>();
        var admission = new PlatformIdentityGatewayAdmission(options, signer, registry, new ClaimsTenantValidator(),
            new ClaimsRbacValidator(), TimeProvider.System, new PlatformOperatorProvenanceVerifier(operatorVerifier, options));
        byte[] payload = Payload();
        IdentityAdmissionScope scope = Scope(payload);
        if (failure == "digest") scope = scope with { PayloadDigest = "wrong" };
        if (failure == "party") scope = scope with { AggregateId = "party-other" };
        (await admission.AdmitAsync(Reader(failure), scope, payload, TestContext.Current.CancellationToken)).ShouldBeNull();
        signer.DidNotReceive().Sign(Arg.Is<IdentityAdmissionEvidence>(evidence => evidence.Scope.Domain == "party"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingDaprReaderPreservesOrganizationAndHistoricalInactiveActorSemantics(bool historical)
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options());
        IIdentityAdmissionSigner signer = Substitute.For<IIdentityAdmissionSigner>();
        signer.Sign(Arg.Any<IdentityAdmissionEvidence>()).Returns("signed");
        var proxy = Substitute.For<IIdentityActorRegistryActor>();
        proxy.ReadAsync(Actor, false, Arg.Any<string>(), Arg.Any<string>()).Returns(new ActorRegistryEntry(Actor, 7, false, "provenance"));
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        factory.CreateActorProxy<IIdentityActorRegistryActor>(Arg.Any<ActorId>(), Arg.Any<string>()).Returns(proxy);
        var registry = new PlatformActorRegistry(factory, signer, options, TimeProvider.System);
        var verifier = Substitute.For<IIdentityAdmissionProof>();
        var admission = new PlatformIdentityGatewayAdmission(options, signer, registry, new ClaimsTenantValidator(),
            new ClaimsRbacValidator(), TimeProvider.System, new PlatformOperatorProvenanceVerifier(verifier, options));
        byte[] payload = historical ? Payload() : JsonSerializer.SerializeToUtf8Bytes(new { tenantId = "tenant-a", partyId = "party-a" });
        IdentityAdmissionScope scope = Scope(payload) with { Operation = historical
            ? "Hexalith.Parties.Contracts.Queries.ResolveHumanActorBindingAt" : Operation };
        ClaimsPrincipal dapr = new(new ClaimsIdentity([
            new Claim("dapr_caller_app_id", "works-reader"), new Claim("eventstore:tenant", "tenant-a"),
            new Claim("eventstore:permission", "query:read"),
        ], Hexalith.EventStore.Authentication.DaprInternalAuthenticationOptions.SchemeName));
        (await admission.AdmitAsync(dapr, scope, payload, TestContext.Current.CancellationToken)).ShouldBe("signed");
        signer.Received().Sign(Arg.Is<IdentityAdmissionEvidence>(evidence => evidence.Scope == scope
            && evidence.ActorActive == false && evidence.TargetActorId == (historical ? Actor : null)));
    }

    [Theory]
    [InlineData(false, "tenant")]
    [InlineData(true, "tenant")]
    [InlineData(false, "rbac")]
    [InlineData(true, "rbac")]
    public async Task UnrelatedIdentitiesCannotSupplyMissingJwtReaderPrivileges(bool authenticated, string missing)
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options());
        var signer = Substitute.For<IIdentityAdmissionSigner>();
        var factory = Substitute.For<IActorProxyFactory>();
        var registry = new PlatformActorRegistry(factory, signer, options, TimeProvider.System);
        var admission = new PlatformIdentityGatewayAdmission(options, signer, registry, new ClaimsTenantValidator(),
            new ClaimsRbacValidator(), TimeProvider.System, new PlatformOperatorProvenanceVerifier(Substitute.For<IIdentityAdmissionProof>(), options));
        ClaimsPrincipal principal = Reader(missing);
        principal.AddIdentity(new ClaimsIdentity([
            new Claim("eventstore:tenant", "tenant-a"), new Claim("eventstore:domain", "party"),
            new Claim("eventstore:permission", "query:read"), new Claim("global_admin", "true"),
        ], authenticated ? "unrelated-authentication" : null));
        byte[] payload = Payload();
        (await admission.AdmitAsync(principal, Scope(payload), payload, TestContext.Current.CancellationToken)).ShouldBeNull();
        signer.DidNotReceive().Sign(Arg.Any<IdentityAdmissionEvidence>());
        factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdentityActorRegistryActor>(default!, default!);
    }

    private static PlatformIdentityOptions Options() => new()
    {
        AuthorityRevision = 1,
        RegistryServiceSourceId = "registry-service",
        ReaderSources = ["works-reader"],
        JwtReaders = [new() { SourceId = "works-reader", Issuer = "issuer", Subject = "service-account-works", ClientId = "works" }],
    };

    private static byte[] Payload(string actor = Actor) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        tenantId = "tenant-a", partyId = "party-a", expectedActorId = actor,
        role = "administrator", source = "works-reader", actorRevision = 999,
    });

    private static IdentityAdmissionScope Scope(byte[] payload) => new("tenant-a", "party", "party-a", Operation, "query-id", "query-id", IdentityAdmissionProof.Digest(payload));

    private static ClaimsPrincipal Reader(string? failure = null) => new(new ClaimsIdentity([
        new("iss", failure == "issuer" ? "untrusted" : "issuer"),
        new("sub", failure == "human" ? "arbitrary-human" : "service-account-works"),
        new("azp", failure == "client" ? "untrusted" : "works"),
        new("exp", DateTimeOffset.UtcNow.AddMinutes(failure == "expired" ? -1 : 5).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
        new("eventstore:tenant", failure == "tenant" ? "tenant-other" : "tenant-a"),
        new("eventstore:domain", "party"),
        new("eventstore:permission", failure == "rbac" ? "command:submit" : "query:read"),
    ], failure == "unauthenticated" ? null : "AuthenticationTypes.Federation"));
}
