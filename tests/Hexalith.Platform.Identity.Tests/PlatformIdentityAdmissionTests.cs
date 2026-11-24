using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Identity;
using Hexalith.Platform.Identity;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Identity.Tests;

public sealed class PlatformIdentityAdmissionTests
{
    private const string Operator = "01HX0000000000000000000001";
    private static readonly string AliasKey = Convert.ToBase64String(new byte[32]);
    [Fact]
    public async Task FirstEnrollment_RequiresIndependentExactBootstrapApprovalAndVerifiedProvenance()
    {
        var mutation = new ActorRegistryMutation(IdentityActorRegistryActor.RegistryNamespace, "enroll", new string('a', 64), Operator, 0, true, true, Operator);
        (IIdentityAdmissionProof verifier, string approval) = BootstrapApproval(mutation);
        var fixture = Fixture(bootstrapVerifier: verifier);
        ActorRegistryMutation changedAlias = mutation with { AliasDigest = new string('b', 64) };

        (await fixture.Service.ApplyAsync(Workload(), mutation, "absent", TestContext.Current.CancellationToken)).ShouldBeNull();
        (await fixture.Service.ApplyAsync(Workload(), changedAlias, "absent", TestContext.Current.CancellationToken, approval)).ShouldBeNull();
        (await fixture.Service.ApplyAsync(Workload(), mutation with { ProvenanceId = "other" }, "absent", TestContext.Current.CancellationToken, approval)).ShouldBeNull();
        await fixture.Proxy.DidNotReceiveWithAnyArgs().MutateAsync(default!, default!);

        (await fixture.Service.ApplyAsync(Workload(), mutation, "absent", TestContext.Current.CancellationToken, approval)).ShouldNotBeNull();
        await fixture.Proxy.Received(1).MutateAsync(mutation, "registry-proof");
    }

    [Theory]
    [InlineData("untrusted-writer", "registry-service")]
    [InlineData("registry-service", "registry-service")]
    [InlineData("identity-writer", null)]
    [InlineData("identity-writer", " ")]
    public async Task RegistryMutation_RefusesInvalidCallerOrMissingServiceIdentity(string source, string? registrySource)
    {
        var fixture = Fixture(registrySource);
        var mutation = new ActorRegistryMutation(IdentityActorRegistryActor.RegistryNamespace, "enroll", new string('a', 64), Operator, 0, true, true, Operator);

        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Registry.MutateAsync(
            mutation, source, Operator, TestContext.Current.CancellationToken));

        fixture.Signer.DidNotReceiveWithAnyArgs().Sign(default!);
        fixture.Factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdentityActorRegistryActor>(default!, default!);
    }

    [Fact]
    public async Task RegistryMutation_SignsAsConfiguredRegistryService()
    {
        var mutation = new ActorRegistryMutation(IdentityActorRegistryActor.RegistryNamespace, "enroll", new string('a', 64), Operator, 0, true, true, Operator);
        (IIdentityAdmissionProof verifier, string approval) = BootstrapApproval(mutation);
        var fixture = Fixture(bootstrapVerifier: verifier);

        (await fixture.Service.ApplyAsync(Workload(), mutation, "absent", TestContext.Current.CancellationToken, approval)).ShouldNotBeNull();

        fixture.Signer.Received(1).Sign(Arg.Is<IdentityAdmissionEvidence>(evidence =>
            evidence.Scope.Operation == "RegistryMutation" && evidence.SourceId == "registry-service" && evidence.OperatorActorId == Operator));
        await fixture.Proxy.Received(1).MutateAsync(mutation, "registry-proof");
    }

    [Fact]
    public async Task SameVerifiedLoginAcrossTenantClaims_UsesOneGlobalActorAndNeverEnrolls()
    {
        var fixture = Fixture();
        ActorRegistryEntry entry = new(Operator, 1, true, Operator);
        string alias = PlatformLoginAliasDigest.Create("issuer", "private-subject", AliasKey);
        string otherAlias = PlatformLoginAliasDigest.Create("issuer", "other-subject", AliasKey);
        fixture.Proxy.ReadAsync(alias, true, Arg.Any<string>(), Arg.Any<string>()).Returns(entry);
        ActorRegistryEntry? first = await fixture.Service.ResolveLoginAsync(Login("tenant-a"), "identity-writer", TestContext.Current.CancellationToken);
        ActorRegistryEntry? second = await fixture.Service.ResolveLoginAsync(Login("tenant-b"), "identity-writer", TestContext.Current.CancellationToken);
        ActorRegistryEntry? other = await fixture.Service.ResolveLoginAsync(Login("tenant-a", "other-subject"), "identity-writer", TestContext.Current.CancellationToken);
        first.ShouldBe(entry);
        second.ShouldBe(entry);
        other.ShouldBeNull();
        await fixture.Proxy.Received(2).ReadAsync(alias, true, Arg.Any<string>(), Arg.Any<string>());
        await fixture.Proxy.Received(1).ReadAsync(otherAlias, true, Arg.Any<string>(), Arg.Any<string>());
        fixture.Factory.Received(3).CreateActorProxy<IIdentityActorRegistryActor>(Arg.Is<ActorId>(value => value.GetId() == IdentityActorRegistryActor.RegistryNamespace), IdentityActorRegistryActor.ActorTypeName);
        await fixture.Proxy.DidNotReceiveWithAnyArgs().MutateAsync(default!, default!);
    }

    [Fact]
    public async Task LoginWithClaimsSplitAcrossAuthenticatedIdentities_DoesNotReadRegistry()
    {
        var fixture = Fixture();
        var principal = new ClaimsPrincipal([
            new ClaimsIdentity([new Claim("iss", "issuer")], "verified-login"),
            new ClaimsIdentity([new Claim("sub", "private-subject")], "verified-login"),
        ]);

        (await fixture.Service.ResolveLoginAsync(principal, "identity-writer", TestContext.Current.CancellationToken)).ShouldBeNull();
        await fixture.Proxy.DidNotReceiveWithAnyArgs().ReadAsync(default!, default, default!, default!);
    }

    [Fact]
    public async Task LoginWithAmbiguousAuthenticatedIdentities_DoesNotReadRegistry()
    {
        var fixture = Fixture();
        var principal = new ClaimsPrincipal([
            new ClaimsIdentity([new Claim("iss", "issuer"), new Claim("sub", "private-subject")], "verified-login"),
            new ClaimsIdentity([new Claim("iss", "issuer"), new Claim("sub", "other-subject")], "verified-login"),
        ]);

        (await fixture.Service.ResolveLoginAsync(principal, "identity-writer", TestContext.Current.CancellationToken)).ShouldBeNull();
        await fixture.Proxy.DidNotReceiveWithAnyArgs().ReadAsync(default!, default, default!, default!);
    }

    [Fact]
    public async Task LoginForInactiveActor_IsIneligible()
    {
        var fixture = Fixture();
        fixture.Proxy.ReadAsync(Arg.Any<string>(), true, Arg.Any<string>(), Arg.Any<string>())
            .Returns(new ActorRegistryEntry(Operator, 2, false, Operator));

        (await fixture.Service.ResolveLoginAsync(Login("tenant-a"), "identity-writer", TestContext.Current.CancellationToken)).ShouldBeNull();
        await fixture.Proxy.Received(1).ReadAsync(Arg.Any<string>(), true, Arg.Any<string>(), Arg.Any<string>());
    }

    private static (IIdentityAdmissionProof Verifier, string Approval) BootstrapApproval(ActorRegistryMutation mutation)
    {
        using RSA key = RSA.Create(2048);
        var signing = Substitute.For<IOptionsMonitor<IdentityAdmissionSigningOptions>>();
        signing.CurrentValue.Returns(new IdentityAdmissionSigningOptions { PrivateKeyPem = key.ExportRSAPrivateKeyPem() });
        var verification = Substitute.For<IOptionsMonitor<IdentityAdmissionOptions>>();
        verification.CurrentValue.Returns(new IdentityAdmissionOptions { VerificationKeyPem = key.ExportSubjectPublicKeyInfoPem(), AuthorityRevision = 1 });

        var scope = new IdentityAdmissionScope("identity-authority", "identity-bootstrap", mutation.RegistryNamespace,
            "RegistryBootstrap", mutation.OperationId, mutation.OperationId, IdentityAdmissionProof.Digest(JsonSerializer.SerializeToUtf8Bytes(mutation)));
        DateTimeOffset now = TimeProvider.System.GetUtcNow();
        string approval = new IdentityAdmissionSigner(signing).Sign(new(scope, "bootstrap-authority", Operator, Operator, 0, true, now, now.AddMinutes(1), 1));
        return (new IdentityAdmissionProof(verification, TimeProvider.System), approval);
    }

    private static (PlatformIdentityEnrollmentService Service, IIdentityActorRegistryActor Proxy, IActorProxyFactory Factory,
        PlatformActorRegistry Registry, IIdentityAdmissionSigner Signer) Fixture(
            string? registrySource = "registry-service", IIdentityAdmissionProof? bootstrapVerifier = null)
    {
        var configured = Options.Create(new PlatformIdentityOptions { IdentityWriterSources = ["identity-writer"], TrustedIssuers = ["issuer"], RegistryServiceSourceId = registrySource,
            AliasKeyBase64 = AliasKey, AuthorityRevision = 1, BootstrapProvenanceSources = ["bootstrap-authority"] });
        IIdentityAdmissionProof operatorVerifier = Substitute.For<IIdentityAdmissionProof>();
        bootstrapVerifier ??= Substitute.For<IIdentityAdmissionProof>();
        IIdentityAdmissionSigner signer = Substitute.For<IIdentityAdmissionSigner>();
        signer.Sign(Arg.Any<IdentityAdmissionEvidence>()).Returns("registry-proof");
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdentityActorRegistryActor proxy = Substitute.For<IIdentityActorRegistryActor>();
        proxy.MutateAsync(Arg.Any<ActorRegistryMutation>(), Arg.Any<string>()).Returns(call => new ActorRegistryEntry(call.Arg<ActorRegistryMutation>().ActorId, 1, true, Operator));
        factory.CreateActorProxy<IIdentityActorRegistryActor>(Arg.Any<ActorId>(), Arg.Any<string>()).Returns(proxy);
        var registry = new PlatformActorRegistry(factory, signer, configured, TimeProvider.System);
        return (new(registry, new(operatorVerifier, configured), new(bootstrapVerifier, configured), configured), proxy, factory, registry, signer);
    }
    private static ClaimsPrincipal Workload() => new(new ClaimsIdentity([new Claim("dapr_caller_app_id", "identity-writer")], DaprInternalAuthenticationOptions.SchemeName));
    private static ClaimsPrincipal Login(string tenant, string subject = "private-subject") => new(new ClaimsIdentity([new Claim("iss", "issuer"), new Claim("sub", subject), new Claim("tenant_id", tenant)], "verified-login"));
}
