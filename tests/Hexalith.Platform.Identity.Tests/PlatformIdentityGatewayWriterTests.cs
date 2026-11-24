using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

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
    private const string Operator = "01HX0000000000000000000002";
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

    [Theory]
    [InlineData("valid")]
    [InlineData("unlisted-workload")]
    [InlineData("stale-target")]
    [InlineData("inactive-target")]
    [InlineData("missing-provenance")]
    [InlineData("unlisted-provenance-issuer")]
    [InlineData("stale-operator")]
    [InlineData("inactive-operator")]
    public async Task RegisteredWriterRequiresCurrentActorAndIndependentOperatorProvenance(string scenario)
    {
        using RSA gatewayKey = RSA.Create(2048);
        IIdentityActorRegistryActor registry = Substitute.For<IIdentityActorRegistryActor>();
        registry.ReadAsync(Actor, false, Arg.Any<string>(), Arg.Any<string>())
            .Returns(new ActorRegistryEntry(Actor, 7, scenario != "inactive-target", "target-provenance"));
        registry.ReadAsync(Operator, false, Arg.Any<string>(), Arg.Any<string>())
            .Returns(new ActorRegistryEntry(Operator, scenario == "stale-operator" ? 4 : 5,
                scenario != "inactive-operator", "operator-provenance"));
        string revision = scenario == "stale-target" ? "6" : "7";
        byte[] payload = Payload(revision, includeProof: scenario != "missing-provenance");
        IdentityAdmissionScope scope = Scope(payload);
        IdentityAdmissionScope expectedOperatorScope = scope with
        {
            Domain = "identity-operator",
            MessageId = scope.LogicalId,
            PayloadDigest = IdentityAdmissionProof.Digest(Payload(revision, includeProof: false)),
        };
        DateTimeOffset now = DateTimeOffset.UtcNow;
        IIdentityAdmissionProof operatorProof = Substitute.For<IIdentityAdmissionProof>();
        if (scenario != "missing-provenance")
        {
            operatorProof.Verify("test-operator-proof", expectedOperatorScope).Returns(new IdentityAdmissionEvidence(
                expectedOperatorScope, scenario == "unlisted-provenance-issuer" ? "another-issuer" : "operator-authority",
                Operator, Operator, 5, true, now, now.AddMinutes(1), 1));
        }

        using ServiceProvider provider = Provider(registry, operatorProof, gatewayKey);
        using IServiceScope request = provider.CreateScope();
        var admission = request.ServiceProvider.GetRequiredService<IIdentityGatewayAdmission>();
        admission.ShouldBeOfType<PlatformIdentityGatewayAdmission>();
        var principal = Principal(scenario == "unlisted-workload" ? "another-workload" : Writer);
        string? admitted = await admission.AdmitAsync(principal, scope, payload, TestContext.Current.CancellationToken);

        if (scenario == "valid")
        {
            IdentityAdmissionEvidence evidence = request.ServiceProvider.GetRequiredService<IIdentityAdmissionProof>()
                .Verify(admitted.ShouldNotBeNull(), scope).ShouldNotBeNull();
            evidence.TargetActorId.ShouldBe(Actor);
            evidence.OperatorActorId.ShouldBe(Operator);
            evidence.SourceId.ShouldBe(Writer);
            evidence.ActorRevision.ShouldBe(7);
            evidence.ActorActive.ShouldBeTrue();
        }
        else
        {
            admitted.ShouldBeNull();
        }

        if (scenario == "unlisted-workload")
        {
            await registry.DidNotReceiveWithAnyArgs().ReadAsync(default!, default, default!, default!);
        }
    }

    private static ServiceProvider Provider(
        IIdentityActorRegistryActor registry,
        IIdentityAdmissionProof operatorProof,
        RSA? gatewayKey = null)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PlatformIdentity:AuthorityRevision"] = "1",
            ["PlatformIdentity:IdentityWriterSources:0"] = Writer,
            ["PlatformIdentity:RegistryServiceSourceId"] = "registry-service",
            ["PlatformIdentity:OperatorProvenanceSources:0"] = "operator-authority",
            ["PlatformIdentity:GatewaySigning:PrivateKeyPem"] = gatewayKey?.ExportRSAPrivateKeyPem(),
            ["IdentityAdmission:VerificationKeyPem"] = gatewayKey?.ExportSubjectPublicKeyInfoPem(),
            ["IdentityAdmission:AuthorityRevision"] = "1",
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

    private static byte[] Payload(string revision, bool includeProof = true)
    {
        var body = new JsonObject
        {
            ["tenantId"] = "tenant-a",
            ["partyId"] = "party-a",
            ["actorId"] = Actor,
            ["actorRevision"] = JsonNode.Parse(revision),
        };
        if (includeProof)
        {
            body["operatorProof"] = "test-operator-proof";
        }

        return JsonSerializer.SerializeToUtf8Bytes(body);
    }

    private static IdentityAdmissionScope Scope(byte[] payload) => new(
        "tenant-a", "party", "party-a", Operation, "command-id", "intent-id", IdentityAdmissionProof.Digest(payload));
}
