#if HEXALITH_PLATFORM_V2_CATALOG
namespace Hexalith.Platform.Composition.Tests;

using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Tenants.Contracts.Routing;

using Shouldly;

/// <summary>Structural file-proof controls use compiled owner schemas; only the separate real runtime lane can establish serving acceptance.</summary>
public sealed class PlatformCommittedCatalogProofTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("missing-readback")]
    [InlineData("extra-readback")]
    [InlineData("empty-cohort")]
    [InlineData("foreign-cohort")]
    [InlineData("wrong-attempt")]
    [InlineData("wrong-root")]
    [InlineData("wrong-canonical")]
    [InlineData("missing-technical")]
    [InlineData("missing-gateway-technical")]
    [InlineData("foreign-technical-receiver")]
    [InlineData("wrong-gateway-profile")]
    [InlineData("scoped-gateway")]
    [InlineData("wrong-proof")]
    [InlineData("wrong-activation-host")]
    [InlineData("not-serving")]
    [InlineData("zero-epoch")]
    [InlineData("different-peer-epoch")]
    [InlineData("wrong-generation")]
    [InlineData("wrong-profile")]
    [InlineData("missing-binding")]
    [InlineData("foreign-binding")]
    [InlineData("foreign-readback-attempt")]
    [InlineData("changed-authentication")]
    [InlineData("changed-topology")]
    [InlineData("changed-inventory")]
    [InlineData("missing-readback-host")]
    [InlineData("extra-readback-host")]
    [InlineData("duplicate-readback-host")]
    public void CompleteRunAndEveryHostIdentityMustAgree(string scenario)
    {
        const long generation = 1;
        string gatewayAppId = scenario == "scoped-gateway" ? "eventstore-scoped" : "eventstore";
        var envelope = VersionedRouteCatalogBuilder.Build(new(generation, "Development", TenantCatalogDeclarations.Routes,
            TenantCatalogDeclarations.Operations,
            TenantCatalogDeclarations.Routes.Select(route => new RouteCompositionBinding(route.RouteId, "tenants", TenantCatalogDeclarations.ReceiverMethods[route.RouteId], route.ContractVersion, false, "*")).ToArray(),
            TenantCatalogDeclarations.Commands.Select(command => DevelopmentIdempotencyBindings.Bind(command, generation, gatewayAppId)).ToArray()));
        byte[] canonical = RouteCatalogCodec.Encode(envelope);
        string root = RouteCatalogCodec.ComputeRootDigest(envelope);
        const string attempt = "unit-structural-attempt";
        var gateway = new RouteCatalogHostIdentity("eventstore", gatewayAppId, new string('a', 64), new string('b', 64), new string('c', 64));
        var tenants = gateway with { HostId = "tenants", AppId = "tenants" };
        RouteCatalogHostIdentity[] hosts = [gateway, tenants];
        var activation = new RouteCatalogActivation(attempt, canonical, root, null, hosts, "Development-fixture", "")
        {
            TechnicalHttpReceivers = [new(gatewayAppId, RouteCatalogTechnicalHttpPolicy.FenceVersion,
                [new("/unit/structural", ["GET"], "unit-file-proof-not-runtime-acceptance", new string('a', 64), new string('a', 64),
                    ["EventStoreHuman"], new string('a', 64), new string('a', 64))])],
        };
        if (scenario == "foreign-technical-receiver")
            activation = activation with { TechnicalHttpReceivers = [activation.TechnicalHttpReceivers![0] with { AppId = tenants.AppId }] };
        if (scenario == "missing-gateway-technical") activation = activation with { TechnicalHttpReceivers = [] };
        // Rebind these otherwise-valid controls so only the independently selected gateway requirement can refuse them.
        string binding = RouteCatalogActivationBinding.ComputeDigest(activation);
        activation = activation with { TrustProof = binding };
        var readback = new RouteCatalogGenerationReadback(1, generation, root, envelope.RouteFacetDigest,
            envelope.IdempotencyFacetDigest, "Development", hosts, true) { AttemptId = attempt, ActivationBindingDigest = binding };
        Dictionary<string, string> expected = new(StringComparer.Ordinal) { [gateway.HostId] = gateway.AppId, [tenants.HostId] = tenants.AppId };
        Dictionary<string, RouteCatalogGenerationReadback> readbacks = new(StringComparer.Ordinal) { [gateway.HostId] = readback, [tenants.HostId] = readback };
        if (scenario == "missing-readback") readbacks.Remove(tenants.HostId);
        if (scenario == "extra-readback") readbacks.Add("foreign", readback);
        if (scenario == "empty-cohort") expected.Clear();
        if (scenario == "foreign-cohort") expected[gateway.HostId] = "foreign-app";
        activation = MutateActivation(scenario, activation, gateway, tenants);
        readbacks[tenants.HostId] = MutateReadback(scenario, readback, gateway, tenants, readbacks.GetValueOrDefault(tenants.HostId)!);
        if (scenario == "missing-readback") readbacks.Remove(tenants.HostId);
        if (scenario is "valid" or "scoped-gateway")
            PlatformCommittedCatalogProof.Validate(activation, canonical, root, attempt, expected, readbacks);
        else
            Should.Throw<InvalidOperationException>(() => PlatformCommittedCatalogProof.Validate(activation, canonical, root, attempt, expected, readbacks));
    }
    private static RouteCatalogActivation MutateActivation(string scenario, RouteCatalogActivation activation, RouteCatalogHostIdentity gateway, RouteCatalogHostIdentity tenants)
    {
        return scenario switch
        {
            "wrong-attempt" => activation with { AttemptId = "foreign-run-same-root" },
            "wrong-root" => activation with { RootDigest = new string('f', 64) },
            "wrong-canonical" => activation with { CanonicalCatalog = "{}"u8.ToArray() },
            "missing-technical" => activation with { TechnicalHttpReceivers = null },
            "wrong-gateway-profile" => activation with { TechnicalHttpReceivers = [activation.TechnicalHttpReceivers![0] with { FenceVersion = "foreign-profile" }] },
            "wrong-proof" => activation with { TrustProof = new string('f', 64) },
            "wrong-activation-host" => activation with { RequiredHosts = [gateway with { AppId = "foreign-app" }, tenants] },
            _ => activation,
        };
    }

    private static RouteCatalogGenerationReadback MutateReadback(string scenario, RouteCatalogGenerationReadback readback, RouteCatalogHostIdentity gateway, RouteCatalogHostIdentity tenants, RouteCatalogGenerationReadback fallback)
    {
        return scenario switch
        {
            "not-serving" => readback with { Serving = false },
            "zero-epoch" => readback with { Epoch = 0 },
            "different-peer-epoch" => readback with { Epoch = 2 },
            "wrong-generation" => readback with { Generation = 2 },
            "wrong-profile" => readback with { Profile = "Hosted" },
            "missing-binding" => readback with { ActivationBindingDigest = null },
            "foreign-binding" => readback with { ActivationBindingDigest = new string('f', 64) },
            "foreign-readback-attempt" => readback with { AttemptId = "another-run" },
            "changed-authentication" => readback with { RequiredHosts = [gateway, tenants with { AuthenticationFingerprint = new string('d', 64) }] },
            "changed-topology" => readback with { RequiredHosts = [gateway, tenants with { TopologyDigest = new string('d', 64) }] },
            "changed-inventory" => readback with { RequiredHosts = [gateway, tenants with { InventoryDigest = new string('d', 64) }] },
            "missing-readback-host" => readback with { RequiredHosts = [gateway] },
            "extra-readback-host" => readback with { RequiredHosts = [gateway, tenants, tenants with { HostId = "foreign" }] },
            "duplicate-readback-host" => readback with { RequiredHosts = [gateway, gateway] },
            _ => fallback,
        };
    }

}
#endif
