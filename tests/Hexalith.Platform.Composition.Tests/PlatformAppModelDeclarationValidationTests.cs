namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json.Nodes;
using Aspire.Hosting;
using Shouldly;

public sealed partial class PlatformAppModelTests
{
    [Theory]
    [InlineData("lifecycle.tasks", "verify", "scope", "operator-only")]
    [InlineData("lifecycle.recoveryHooks", "verify", "task", "recover")]
    [InlineData("lifecycle.fenceHooks", "verify", "task", "recover")]
    [InlineData("surfaces.interfaces", "validation-test-api", "routePrefix", "/other-path")]
    [InlineData("surfaces.interfaces[validation-test-api].operations", "read", "authorizationPolicy", "other.read")]
    public void CompositionRefusesDuplicateDeclarationNamesBeforeAddingResources(string field, string name, string changedField, string changedValue)
    {
        DirectoryInfo workspace = Directory.CreateTempSubdirectory("platform-declaration-names-");
        try
        {
            string path = Fixture(workspace.FullName, "validation-test", tasks: true);
            JsonNode document = JsonNode.Parse(File.ReadAllText(path))!;
            JsonNode module = document["modules"]![0]!;
            AddNamedHooks(module);
            JsonArray tasks = module["lifecycle"]!["tasks"]!.AsArray();
            JsonNode recoveryTask = tasks[0]!.DeepClone();
            recoveryTask["name"] = "recover";
            recoveryTask["scope"] = "recovery";
            tasks.Add(recoveryTask);
            JsonArray collection = field switch
            {
                "surfaces.interfaces" => module["surfaces"]!["interfaces"]!.AsArray(),
                "surfaces.interfaces[validation-test-api].operations" => module["surfaces"]!["interfaces"]![0]!["operations"]!.AsArray(),
                _ => module["lifecycle"]![field["lifecycle.".Length..]]!.AsArray(),
            };
            JsonNode duplicate = collection[0]!.DeepClone();
            duplicate[changedField] = changedValue;
            collection.Add(duplicate);
            File.WriteAllText(path, document.ToJsonString());

            IDistributedApplicationBuilder builder = Builder();
            PlatformCompositionOptions options = Options(active: "validation-test") with { DeclarationPaths = [path] };
            InvalidDataException failure = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options));
            failure.Message.ShouldEndWith($"{Path.GetFileName(path)}: validation-test: {field}: duplicate name '{name}'.");
            builder.Resources.ShouldBeEmpty();
        }
        finally
        {
            workspace.Delete(recursive: true);
        }
    }

    [Fact]
    public void CompositionAllowsNamesReusedAcrossModulesCollectionsAndInterfaces()
    {
        DirectoryInfo workspace = Directory.CreateTempSubdirectory("platform-declaration-name-scopes-");
        try
        {
            string first = Fixture(workspace.FullName, "validation-test", tasks: true);
            string second = Fixture(workspace.FullName, "validation-dependency", tasks: true);
            foreach (string path in new[] { first, second })
            {
                JsonNode document = JsonNode.Parse(File.ReadAllText(path))!;
                JsonNode module = document["modules"]![0]!;
                AddNamedHooks(module);
                JsonArray interfaces = module["surfaces"]!["interfaces"]!.AsArray();
                JsonNode original = interfaces[0]!;
                original["name"] = "verify";
                JsonNode alternative = original.DeepClone();
                alternative["name"] = "alternate";
                alternative["routePrefix"] = original["routePrefix"]!.GetValue<string>() + "-other";
                interfaces.Add(alternative);
                if (path == first)
                {
                    module["identity"]!["dependencies"] = new JsonArray(JsonValue.Create("validation-dependency"));
                }
                File.WriteAllText(path, document.ToJsonString());
            }

            PlatformCompositionOptions options = Options(active: "validation-test") with
            {
                DomainModule = false,
                DeclarationPaths = [first, second],
            };
            PlatformApplicationGraph.Resolve(options).Workloads.Select(workload => workload.Identity)
                .ShouldBe(["validation-test/api", "validation-dependency/api"], ignoreOrder: true);
        }
        finally
        {
            workspace.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("cpu", "0.25", "0.2", true)]
    [InlineData("cpu", "1.234567890123456789012345678901234567891", "1.234567890123456789012345678901234567890", true)]
    [InlineData("cpu", "1.234567890123456789012345678901234567890", "1.234567890123456789012345678901234567891", false)]
    [InlineData("cpu", "1.234567890123456789012345678901234567890E-1", "0.1234567890123456789012345678901234567890", false)]
    [InlineData("cpu", "1.25e+2", "125.00", false)]
    [InlineData("cpu", "1e-6", null, false)]
    [InlineData("cpu", null, "1e-6", false)]
    [InlineData("memoryMiB", "1024", "512", true)]
    [InlineData("memoryMiB", "9007199254740993", "9007199254740992", true)]
    [InlineData("memoryMiB", "1000", "1e3", false)]
    public void CompositionComparesResourceRequestsWithMatchingLimitsExactly(string quantity, string? request, string? limit, bool exceedsLimit)
    {
        DirectoryInfo workspace = Directory.CreateTempSubdirectory("platform-declaration-resource-limits-");
        try
        {
            string path = Fixture(workspace.FullName, "validation-test", tasks: false);
            JsonNode document = JsonNode.Parse(File.ReadAllText(path))!;
            JsonNode resource = document["modules"]![0]!["runtime"]!["resources"]![0]!;
            foreach (var (field, value) in new[] { ("requests", request), ("limits", limit) })
            {
                JsonObject quantities = resource[field]!.AsObject();
                if (value is null)
                {
                    quantities.Remove(quantity);
                }
                else
                {
                    quantities[quantity] = JsonNode.Parse(value);
                }
            }
            File.WriteAllText(path, document.ToJsonString());

            PlatformCompositionOptions options = Options(active: "validation-test") with
            {
                DomainModule = false,
                DeclarationPaths = [path],
            };
            if (exceedsLimit)
            {
                IDistributedApplicationBuilder builder = Builder();
                InvalidDataException failure = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options));
                failure.Message.ShouldEndWith($"{Path.GetFileName(path)}: validation-test/api: runtime.resources.requests.{quantity}: request exceeds the declared limit.");
                builder.Resources.ShouldBeEmpty();
            }
            else
            {
                PlatformApplicationGraph.Resolve(options).Workloads.Single().Identity.ShouldBe("validation-test/api");
            }
        }
        finally
        {
            workspace.Delete(recursive: true);
        }
    }

    private static void AddNamedHooks(JsonNode module)
    {
        JsonObject hook = new() { ["name"] = "verify", ["task"] = "verify", ["resource"] = "data" };
        module["lifecycle"]!["recoveryHooks"]!.AsArray().Add(hook);
        module["lifecycle"]!["fenceHooks"]!.AsArray().Add(hook.DeepClone());
    }
}
