using System.Security.Claims;
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
    [Fact]
    public async Task FirstEnrollment_RequiresIndependentExactBootstrapApprovalAndVerifiedProvenance()
    {
        var fixture = Fixture();
        var mutation = new ActorRegistryMutation(IdentityActorRegistryActor.RegistryNamespace, "enroll", new string('a', 64), Operator, 0, true, true, Operator);
        (await fixture.Service.ApplyAsync(Workload(), mutation, "absent", TestContext.Current.CancellationToken)).ShouldBeNull();
        (await fixture.Service.ApplyAsync(Workload(), mutation, "absent", TestContext.Current.CancellationToken, "approved")).ShouldNotBeNull();
        (await fixture.Service.ApplyAsync(Workload(), mutation with { ProvenanceId = "other" }, "absent", TestContext.Current.CancellationToken, "approved")).ShouldBeNull();
        await fixture.Proxy.Received(1).MutateAsync(Arg.Is<ActorRegistryMutation>(value => value == mutation), Arg.Any<string>());
    }

    [Fact]
    public async Task SameVerifiedLoginAcrossTenantClaims_UsesOneGlobalActorAndNeverEnrolls()
    {
        var fixture = Fixture();
        ActorRegistryEntry entry = new(Operator, 1, true, Operator);
        fixture.Proxy.ReadAsync(Arg.Any<string>(), true, Arg.Any<string>(), Arg.Any<string>()).Returns(entry);
        ActorRegistryEntry? first = await fixture.Service.ResolveLoginAsync(Login("tenant-a"), "identity-writer", TestContext.Current.CancellationToken);
        ActorRegistryEntry? second = await fixture.Service.ResolveLoginAsync(Login("tenant-b"), "identity-writer", TestContext.Current.CancellationToken);
        first!.ActorId.ShouldBe(second!.ActorId);
        fixture.Factory.Received(2).CreateActorProxy<IIdentityActorRegistryActor>(Arg.Is<ActorId>(value => value.GetId() == IdentityActorRegistryActor.RegistryNamespace), IdentityActorRegistryActor.ActorTypeName);
        await fixture.Proxy.DidNotReceiveWithAnyArgs().MutateAsync(default!, default!);
    }

    private static (PlatformIdentityEnrollmentService Service, IIdentityActorRegistryActor Proxy, IActorProxyFactory Factory) Fixture()
    {
        var configured = Options.Create(new PlatformIdentityOptions { IdentityWriterSources = ["identity-writer"], TrustedIssuers = ["issuer"], RegistryServiceSourceId = "registry-service",
            AliasKeyBase64 = Convert.ToBase64String(new byte[32]), AuthorityRevision = 1, BootstrapProvenanceSources = ["bootstrap-authority"] });
        IIdentityAdmissionProof operatorVerifier = Substitute.For<IIdentityAdmissionProof>();
        IIdentityAdmissionProof bootstrap = Substitute.For<IIdentityAdmissionProof>();
        bootstrap.Verify("approved", Arg.Any<IdentityAdmissionScope>()).Returns(call => new IdentityAdmissionEvidence(call.Arg<IdentityAdmissionScope>(), "bootstrap-authority", Operator, Operator, 0, true,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1), 1));
        IIdentityAdmissionSigner signer = Substitute.For<IIdentityAdmissionSigner>();
        signer.Sign(Arg.Any<IdentityAdmissionEvidence>()).Returns("registry-proof");
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdentityActorRegistryActor proxy = Substitute.For<IIdentityActorRegistryActor>();
        proxy.MutateAsync(Arg.Any<ActorRegistryMutation>(), Arg.Any<string>()).Returns(call => new ActorRegistryEntry(call.Arg<ActorRegistryMutation>().ActorId, 1, true, Operator));
        factory.CreateActorProxy<IIdentityActorRegistryActor>(Arg.Any<ActorId>(), Arg.Any<string>()).Returns(proxy);
        var registry = new PlatformActorRegistry(factory, signer, configured, TimeProvider.System);
        return (new(registry, new(operatorVerifier, configured), new(bootstrap, configured), configured), proxy, factory);
    }
    private static ClaimsPrincipal Workload() => new(new ClaimsIdentity([new Claim("dapr_caller_app_id", "identity-writer")], DaprInternalAuthenticationOptions.SchemeName));
    private static ClaimsPrincipal Login(string tenant) => new(new ClaimsIdentity([new Claim("iss", "issuer"), new Claim("sub", "private-subject"), new Claim("tenant_id", tenant)], "verified-login"));
}
