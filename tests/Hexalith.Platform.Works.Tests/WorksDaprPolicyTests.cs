namespace Hexalith.Platform.Works.Tests;

using Hexalith.EventStore.Contracts.Routing;
using Shouldly;
using YamlDotNet.RepresentationModel;

/// <summary>Checks the exact source policies loaded by the target Works topology.</summary>
public sealed class WorksDaprPolicyTests
{
    /// <summary>Every receiver requires mTLS and denies unknown workloads before application admission.</summary>
    [Theory]
    [InlineData("accesscontrol.yaml")]
    [InlineData("accesscontrol.works.yaml")]
    [InlineData("accesscontrol.eventstore-admin.yaml")]
    [InlineData("accesscontrol.eventstore-operations.yaml")]
    public void AllReceiversRequireMtlsAndDenyUnknownCallers(string file)
    {
        YamlMappingNode spec = Spec(file);
        Scalar((YamlMappingNode)spec["mtls"], "enabled").ShouldBe("true");
        YamlMappingNode access = (YamlMappingNode)spec["accessControl"];
        Scalar(access, "defaultAction").ShouldBe("deny");
        foreach (YamlMappingNode policy in (YamlSequenceNode)access["policies"])
        {
            Scalar(policy, "defaultAction").ShouldBe("deny");
            Scalar(policy, "trustDomain").ShouldBe(Scalar(access, "trustDomain"));
            Scalar(policy, "namespace").ShouldBe("default");
            // All receiver grants use finite catalog compatible shapes.
            foreach (YamlMappingNode operation in (YamlSequenceNode)policy["operations"])
                Scalar(operation, "name").ShouldNotBe("/**");
        }
    }

    /// <summary>
    /// The EventStore receiver admits exactly two callers: the Works module on its exact gateway routes, and Admin.Server
    /// under the finite admin receiver grant.
    /// </summary>
    [Fact]
    public void EventStoreReceiverAdmitsOnlyExactWorksRoutesAndBoundedAdminSurface()
    {
        YamlMappingNode access = (YamlMappingNode)Spec("accesscontrol.yaml")["accessControl"];
        Dictionary<string, YamlMappingNode> callers = ((YamlSequenceNode)access["policies"]).Cast<YamlMappingNode>()
            .ToDictionary(policy => Scalar(policy, "appId"), StringComparer.Ordinal);
        callers.Keys.Order(StringComparer.Ordinal).ShouldBe([AdminCaller, "works"]);
        Operations(callers["works"]).ShouldBe([
            "POST /api/v1/commands allow",
            "POST /api/v1/commands/workload allow",
            "POST /api/v1/queries allow",
            "GET /api/v1/commands/status/* allow",
            "POST /api/v1/streams/read allow",
            "POST /api/v1/streams/read/workload allow",
            "POST /api/v1/trusted-effects allow",
        ]);
        Operations(callers[AdminCaller]).ShouldBe([
            "GET,POST,PUT /api/v1/admin/** allow",
            "POST /api/v1/commands allow",
            "POST /api/v1/queries allow",
            "POST /api/v1/streams/read allow",
            "GET /health allow",
        ]);
    }

    /// <summary>Withdrawing the unusable nested scalar declaration preserves the protected request decisions.</summary>
    [Theory]
    [InlineData("works", "GET", "/api/v1/commands/status/01ARZ3NDEKTSV4RRFFQ69G5FAV", true)]
    [InlineData("works", "GET", "/api/v1/commands/status/workload/tenant-a/01ARZ3NDEKTSV4RRFFQ69G5FAV", false)]
    [InlineData("works", "DELETE", "/api/v1/commands/status/01ARZ3NDEKTSV4RRFFQ69G5FAV", false)]
    [InlineData("unknown", "GET", "/api/v1/commands/status/01ARZ3NDEKTSV4RRFFQ69G5FAV", false)]
    [InlineData("works", "GET", "/api/v1/commands/status-other/01ARZ3NDEKTSV4RRFFQ69G5FAV", false)]
    [InlineData("works", "POST", "/api/v1/trusted-effects", true)]
    public void WithdrawingNestedStatusGrantPreservesProtectedRequests(string caller, string verb, string path, bool expected)
    {
        Allows(caller, verb, path, includeWithdrawnGrant: true).ShouldBe(expected);
        Allows(caller, verb, path, includeWithdrawnGrant: false).ShouldBe(expected);
    }

    /// <summary>The withdrawn grant reached an unmatched one-segment prefix; removing it narrows that admission.</summary>
    [Fact]
    public void WithdrawingNestedStatusGrantDeniesItsUnmatchedPrefix()
    {
        const string withdrawn = "/api/v1/commands/status/workload/*/*";
        RouteCatalogDaprGrant.Intersects(withdrawn, "/api/v1/commands/status/workload/{tenant}/{messageId}").ShouldBeFalse();
        const string prefix = "/api/v1/commands/status/workload/tenant-a";
        Allows("works", "GET", prefix, includeWithdrawnGrant: true).ShouldBeTrue();
        Allows("works", "GET", prefix, includeWithdrawnGrant: false).ShouldBeFalse();
    }

    private static bool Allows(string caller, string verb, string path, bool includeWithdrawnGrant)
    {
        YamlMappingNode access = (YamlMappingNode)Spec("accesscontrol.yaml")["accessControl"];
        YamlMappingNode? policy = ((YamlSequenceNode)access["policies"]).Cast<YamlMappingNode>()
            .SingleOrDefault(item => Scalar(item, "appId") == caller);
        if (policy is null) return false;
        bool retained = ((YamlSequenceNode)policy["operations"]).Cast<YamlMappingNode>().Any(operation =>
            Scalar(operation, "action") == "allow"
            && ((YamlSequenceNode)operation["httpVerb"]).Cast<YamlScalarNode>().Any(item => item.Value == verb)
            && RouteCatalogDaprGrant.Allows(Scalar(operation, "name"), path));
        return retained || includeWithdrawnGrant && caller == "works" && verb == "GET"
            && RouteCatalogDaprGrant.Allows("/api/v1/commands/status/workload/*/*", path);
    }

    /// <summary>Only the SDK gateway may invoke named projection ingress and protected metadata discovery.</summary>
    [Fact]
    public void SdkIngressIsExplicitlyGrantedToGateway()
    {
        YamlMappingNode access = (YamlMappingNode)Spec("accesscontrol.works.yaml")["accessControl"];
        YamlMappingNode gateway = ((YamlSequenceNode)access["policies"]).Cast<YamlMappingNode>()
            .Single(policy => Scalar(policy, "appId") == "eventstore");
        string[] routes = [.. ((YamlSequenceNode)gateway["operations"]).Cast<YamlMappingNode>().Select(operation => Scalar(operation, "name"))];
        routes.ShouldContain("/project/v2");
        routes.ShouldContain("/admin/operational-index-metadata");
        routes.ShouldContain("/query");
        routes.ShouldNotContain(DispositionRoute);
    }

    /// <summary>Process disposition is granted to the operator workload alone; the gateway holds no disposition authority.</summary>
    [Fact]
    public void OnlyTheOperatorWorkloadHoldsProcessDisposition()
    {
        YamlMappingNode access = (YamlMappingNode)Spec("accesscontrol.works.yaml")["accessControl"];
        string[] holders = [.. ((YamlSequenceNode)access["policies"]).Cast<YamlMappingNode>()
            .Where(policy => ((YamlSequenceNode)policy["operations"]).Cast<YamlMappingNode>().Any(operation => Scalar(operation, "name") == DispositionRoute))
            .Select(policy => Scalar(policy, "appId"))];
        holders.ShouldBe(["eventstore-admin"]);
        YamlMappingNode operation = ((YamlSequenceNode)((YamlSequenceNode)access["policies"]).Cast<YamlMappingNode>()
            .Single(policy => Scalar(policy, "appId") == "eventstore-admin")["operations"]).Cast<YamlMappingNode>().ShouldHaveSingleItem();
        Scalar(operation, "name").ShouldBe(DispositionRoute);
        Scalar(operation, "action").ShouldBe("allow");
    }

    private const string DispositionRoute = "/work/processes/revalidate";

    private const string AdminCaller = "eventstore-admin";

    /// <summary>The subscriber cannot publish source events and the operations workload has no publish grant.</summary>
    [Fact]
    public void BrokerScopesKeepSourceEventsAndBothDeadLetterQueuesSeparate()
    {
        Dictionary<string, string> metadata = ((YamlSequenceNode)Spec("pubsub.yaml")["metadata"])
            .Cast<YamlMappingNode>().ToDictionary(item => Scalar(item, "name"), item => Scalar(item, "value"));
        metadata["publishingScopes"].ShouldBe("eventstore=work.events,commanddeadletter.work.events;works=deadletter.work.events;eventstore-operations=");
        metadata["subscriptionScopes"].ShouldBe("eventstore=;works=work.events;eventstore-operations=deadletter.work.events");
        metadata["protectedTopics"].ShouldBe("work.events,deadletter.work.events,commanddeadletter.work.events");
    }

    private static YamlMappingNode Spec(string file)
    {
        YamlStream stream = new();
        stream.Load(new StringReader(File.ReadAllText(Path.Combine(WorksProductionProfileTests.Root, "DaprComponents", file))));
        return (YamlMappingNode)((YamlMappingNode)stream.Documents.Single().RootNode)["spec"];
    }

    private static string Scalar(YamlMappingNode node, string key) => ((YamlScalarNode)node[key]).Value!;

    private static string[] Operations(YamlMappingNode policy) => [.. ((YamlSequenceNode)policy["operations"]).Cast<YamlMappingNode>()
        .Select(operation => string.Join(',', ((YamlSequenceNode)operation["httpVerb"]).Cast<YamlScalarNode>().Select(verb => verb.Value))
            + " " + Scalar(operation, "name") + " " + Scalar(operation, "action"))];
}
