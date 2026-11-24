namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json.Nodes;
using Aspire.Hosting;
using Shouldly;

public sealed partial class PlatformAppModelTests
{
    [Theory]
    [InlineData("lifecycle.tasks", "verify")]
    [InlineData("lifecycle.recoveryHooks", "verify")]
    [InlineData("lifecycle.fenceHooks", "verify")]
    [InlineData("surfaces.interfaces", "validation-test-api")]
    [InlineData("surfaces.interfaces[validation-test-api].operations", "read")]
    public void CompositionRefusesDuplicateDeclarationNamesBeforeAddingResources(string field, string name)
    {
        DirectoryInfo workspace = Directory.CreateTempSubdirectory("platform-declaration-names-");
        try
        {
            string path = Fixture(workspace.FullName, "validation-test", tasks: true);
            JsonNode document = JsonNode.Parse(File.ReadAllText(path))!;
            JsonNode module = document["modules"]![0]!;
            AddNamedHooks(module);
            JsonArray collection = field switch
            {
                "surfaces.interfaces" => module["surfaces"]!["interfaces"]!.AsArray(),
                "surfaces.interfaces[validation-test-api].operations" => module["surfaces"]!["interfaces"]![0]!["operations"]!.AsArray(),
                _ => module["lifecycle"]![field["lifecycle.".Length..]]!.AsArray(),
            };
            collection.Add(collection[0]!.DeepClone());
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

    private static void AddNamedHooks(JsonNode module)
    {
        JsonObject hook = new() { ["name"] = "verify", ["task"] = "verify", ["resource"] = "data" };
        module["lifecycle"]!["recoveryHooks"]!.AsArray().Add(hook);
        module["lifecycle"]!["fenceHooks"]!.AsArray().Add(hook.DeepClone());
    }
}
