using System.Security.Claims;
using System.Text;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Identity;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;
using Shouldly;

using ITenantValidator = Hexalith.EventStore.Authorization.ITenantValidator;

namespace Hexalith.Platform.Identity.Tests;

public sealed class PlatformIdentityGatewayWriterTests
{
    private const string Actor = "01HX0000000000000000000001";
    private const string Writer = "identity-writer";
    private const string Operation = "Hexalith.Parties.Contracts.Commands.EstablishHumanActorBinding";

    [Theory]
    [InlineData("7.5")]
    [InlineData("9223372036854775808")]
    [InlineData("-9223372036854775809")]
    public async Task MalformedNumericActorRevisionIsDenied(string revision)
    {
        IIdentityActorRegistryActor registry = Substitute.For<IIdentityActorRegistryActor>();
        registry.ReadAsync(Actor, false, Arg.Any<string>(), Arg.Any<string>())
            .Returns(new ActorRegistryEntry(Actor, 7, true, "provenance"));
        IIdentityAdmissionProof operatorProof = Substitute.For<IIdentityAdmissionProof>();
        using ServiceProvider provider = Provider(registry, operatorProof);
        using IServiceScope request = provider.CreateScope();
        var admission = request.ServiceProvider.GetRequiredService<IIdentityGatewayAdmission>();
        byte[] payload = Payload(revision);

        (await admission.AdmitAsync(Principal(Writer), Scope(payload), payload, TestContext.Current.CancellationToken))
            .ShouldBeNull();

        await registry.Received(1).ReadAsync(Actor, false, Arg.Any<string>(), Arg.Any<string>());
        operatorProof.DidNotReceiveWithAnyArgs().Verify(default, default!);
    }

    private static ServiceProvider Provider(IIdentityActorRegistryActor registry, IIdentityAdmissionProof operatorProof)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PlatformIdentity:AuthorityRevision"] = "1",
            ["PlatformIdentity:IdentityWriterSources:0"] = Writer,
            ["PlatformIdentity:RegistryServiceSourceId"] = "registry-service",
        }).Build();
        IIdentityAdmissionSigner registrySigner = Substitute.For<IIdentityAdmissionSigner>();
        registrySigner.Sign(Arg.Any<IdentityAdmissionEvidence>()).Returns("registry-proof");
        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        proxyFactory.CreateActorProxy<IIdentityActorRegistryActor>(Arg.Any<ActorId>(), IdentityActorRegistryActor.ActorTypeName)
            .Returns(registry);
        var services = new ServiceCollection();
        services.AddPlatformIdentity(configuration);
        services.AddSingleton(proxyFactory);
        services.AddSingleton<ITenantValidator, ClaimsTenantValidator>();
        services.AddSingleton<IRbacValidator, ClaimsRbacValidator>();
        services.AddKeyedSingleton("identity-registry", registrySigner);
        services.AddKeyedSingleton("identity-operator", operatorProof);
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal Principal(string source) => new(new ClaimsIdentity([
        new Claim("dapr_caller_app_id", source),
        new Claim("eventstore:tenant", "tenant-a"),
        new Claim("eventstore:domain", "party"),
        new Claim("eventstore:permission", "command:submit"),
    ], DaprInternalAuthenticationOptions.SchemeName));

    private static byte[] Payload(string revision) => Encoding.UTF8.GetBytes(
        $$"""{"tenantId":"tenant-a","partyId":"party-a","actorId":"{{Actor}}","actorRevision":{{revision}},"operatorProof":"test-operator-proof"}""");

    private static IdentityAdmissionScope Scope(byte[] payload) => new(
        "tenant-a", "party", "party-a", Operation, "command-id", "intent-id", IdentityAdmissionProof.Digest(payload));
}
