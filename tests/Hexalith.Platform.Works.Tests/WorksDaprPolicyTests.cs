namespace Hexalith.Platform.Works.Tests;

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
            foreach (YamlMappingNode operation in (YamlSequenceNode)policy["operations"])
                Scalar(operation, "name").ShouldNotBe("/**");
        }
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
}
