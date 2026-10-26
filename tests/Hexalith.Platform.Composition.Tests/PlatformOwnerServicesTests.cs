namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json.Nodes;
using Aspire.Hosting;
using Shouldly;

public sealed class PlatformOwnerServicesTests
{
    [Fact]
    public void EnrollmentKeepsClientsSecretsTenantsAndExactScopesSeparate()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder([]);
        PlatformOwnerClientBinding projects = builder.AddPlatformOwnerClient("projects-owner", "projects-api", "projects:read", "tenant-alpha", "projects:read");
        PlatformOwnerClientBinding memories = builder.AddPlatformOwnerClient("memories-owner", "memories-api", "memories:derived-store", "tenant-alpha", "memories:derived-store");
        projects.SecretParameter.Resource.Secret.ShouldBeTrue();
        memories.SecretParameter.Resource.Secret.ShouldBeTrue();
        projects.SecretParameter.Resource.Name.ShouldNotBe(memories.SecretParameter.Resource.Name);
        JsonObject original = JsonNode.Parse("""{"clients":[{"clientId":"existing","optionalClientScopes":["sdk-scope"]}],"clientScopes":[],"users":[{"username":"untouched"}]}""")!.AsObject();
        string rendered = PlatformOwnerServices.AppendOwnerClientsToRealm(original.ToJsonString(), projects, memories);
        JsonObject realm = JsonNode.Parse(rendered)!.AsObject();
        realm["users"]!.ToJsonString().ShouldBe(original["users"]!.ToJsonString());
        realm["clients"]![0]!.ToJsonString().ShouldBe(original["clients"]![0]!.ToJsonString());
        JsonArray clients = realm["clients"]!.AsArray();
        clients.Count.ShouldBe(3);
        clients[1]!["secret"]!.GetValue<string>().Length.ShouldBe(64);
        (clients[1]!["secret"]!.GetValue<string>() != clients[2]!["secret"]!.GetValue<string>())
            .ShouldBeTrue("Owner clients must use separate generated secrets.");
        foreach (JsonNode client in clients.Skip(1).Select(node => node!))
        {
            client["fullScopeAllowed"]!.GetValue<bool>().ShouldBeFalse();
            client["publicClient"]!.GetValue<bool>().ShouldBeFalse();
            client["directAccessGrantsEnabled"]!.GetValue<bool>().ShouldBeFalse();
            client["standardFlowEnabled"]!.GetValue<bool>().ShouldBeFalse();
            client["serviceAccountsEnabled"]!.GetValue<bool>().ShouldBeTrue();
            client["defaultClientScopes"]!.AsArray().ShouldBeEmpty();
            client["optionalClientScopes"]!.AsArray().Count.ShouldBe(2);
            client["optionalClientScopes"]!.AsArray().ShouldAllBe(scope => scope!.GetValue<string>().StartsWith("platform-owner-", StringComparison.Ordinal));
            JsonNode tenant = client["protocolMappers"]!.AsArray().Single(mapper => mapper!["config"]?["claim.name"]?.GetValue<string>() == "tenant_id")!;
            tenant["config"]!["claim.value"]!.GetValue<string>().ShouldBe("tenant-alpha");
            tenant["config"]!["access.token.claim"]!.GetValue<string>().ShouldBe("true");
            JsonNode subject = client["protocolMappers"]!.AsArray().Single(mapper => mapper!["name"]?.GetValue<string>() == "owner-subject")!;
            subject["protocolMapper"]!.GetValue<string>().ShouldBe("oidc-sub-mapper");
            subject["config"]!["access.token.claim"]!.GetValue<string>().ShouldBe("true");
            client["protocolMappers"]!.AsArray().Any(mapper => mapper!["config"]?["claim.name"]?.GetValue<string>() == "eventstore:tenant")
                .ShouldBeFalse("Owner tokens must not carry EventStore's tenant claim.");
        }
        realm["clientScopes"]!.AsArray().Count.ShouldBe(4);
        realm["clientScopes"]!.AsArray().Select(scope => scope!["name"]!.GetValue<string>()).ShouldBe(
            ["platform-owner-audience.projects-api", "platform-owner-operation.projects.read", "platform-owner-audience.memories-api", "platform-owner-operation.memories.derived-store"]);
        realm["clientScopes"]![0]!["protocolMappers"]![0]!["config"]!["included.custom.audience"]!.GetValue<string>().ShouldBe("projects-api");
        realm["clientScopes"]![1]!["protocolMappers"]![0]!["config"]!["claim.value"]!.GetValue<string>().ShouldBe("projects:read");
        realm["clientScopes"]![2]!["protocolMappers"]![0]!["config"]!["included.custom.audience"]!.GetValue<string>().ShouldBe("memories-api");
        realm["clientScopes"]![3]!["protocolMappers"]![0]!["config"]!["claim.value"]!.GetValue<string>().ShouldBe("memories:derived-store");
        foreach ((int index, string permission) in new[] { (1, "projects:read"), (2, "memories:derived-store") })
        {
            JsonNode mapper = clients[index]!["protocolMappers"]!.AsArray()
                .Single(mapper => mapper!["config"]?["claim.name"]?.GetValue<string>() == "eventstore:permission")!;
            mapper["config"]!["claim.value"]!.GetValue<string>().ShouldBe(permission);
            mapper["config"]!["access.token.claim"]!.GetValue<string>().ShouldBe("true");
        }
        rendered.ShouldNotContain("eventstore-audience.");
        rendered.ShouldNotContain("AllowedCallers");
    }

    [Fact]
    public void EnrollmentRejectsExistingOrRepeatedClientIdentities()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder([]);
        PlatformOwnerClientBinding owner = builder.AddPlatformOwnerClient("owner", "owner-api", "owner:read", "tenant-alpha", "owner:read");
        Should.Throw<ArgumentException>(() => PlatformOwnerServices.AppendOwnerClientsToRealm("""{"clients":[{"clientId":"owner"}],"clientScopes":[]}""", owner));
        Should.Throw<ArgumentException>(() => PlatformOwnerServices.AppendOwnerClientsToRealm("""{"clients":[],"clientScopes":[]}""", owner, owner));
    }
}
