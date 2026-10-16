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
        routes.ShouldNotContain("/work/processes/dispose");
    }

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
