using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Closed plan mapping, cross-language canonical wire bytes, independent native observation and signed private receiver coverage.</summary>
public sealed class ProtectionQualificationSourceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void SourceCatalogMapsEveryUnchangedOwnerActionAndSeparatesAuthorizationFromUnpin()
    {
        using var stream = typeof(ProtectionQualificationActionCatalog).Assembly.GetManifestResourceStream("ExtProtectionOwnerPlan")!;
        using var plan = JsonDocument.Parse(stream);
        var recipes = plan.RootElement.GetProperty("cases").EnumerateArray().ToDictionary(value => value.GetProperty("id").GetString()!, value =>
            value.GetProperty("steps").EnumerateArray().ToDictionary(step => step.GetString()!, step => new ProtectionQualificationActionRecipe([], new(step.GetString()!, ProtectionQualificationActionCatalog.Actions[step.GetString()!], new JsonObject()), null, [])));
        ProtectionQualificationActionCatalog.Validate(recipes);
        recipes.Count.ShouldBe(41);
        ProtectionQualificationActionCatalog.Actions.Count.ShouldBe(57);
        ProtectionQualificationActionCatalog.Actions["AuthorizeUnpinDek"].ShouldBe(ProtectionQualificationNativeCallKind.AuthorizeLifecycle);
        recipes["P01"].Remove("SealDirectoryOutbox");
        Should.Throw<InvalidOperationException>(() => ProtectionQualificationActionCatalog.Validate(recipes));
    }

    [Fact]
    public void WireCanonicalizationMatchesPythonUnicodeIntegerAndControlRules()
    {
        var node = JsonNode.Parse("{\"é\":\"😀\",\"z\":9007199254740991,\"a\":\"\\u0001\\n\\t\\\"\\\\\",\"𐀀\":1,\"\":2}")!;
        Encoding.UTF8.GetString(ProtectionQualificationCanonical.Bytes(node)).ShouldBe("{\"a\":\"\\u0001\\n\\t\\\"\\\\\",\"z\":9007199254740991,\"é\":\"😀\",\"\":2,\"𐀀\":1}");
    }

    [Fact]
    public void TypedRecipeReferencesUseOnlyAdmittedRequestAndActualEarlierOriginals()
    {
        var request = new JsonObject { ["targets"] = new JsonArray(new JsonObject { ["TenantId"] = "tenant-a" }) };
        var call = new ProtectionQualificationNativeCall("reserve", ProtectionQualificationNativeCallKind.Reserve, new JsonObject
        {
            ["tenant"] = new JsonObject { ["$request"] = "/targets/0/TenantId" },
            ["receipt"] = new JsonObject { ["$original"] = new JsonObject { ["id"] = "guard", ["pointer"] = "/ReceiptId" } },
        });
        var bound = ProtectionQualificationRecipeBinding.Bind(call, request, new Dictionary<string, JsonNode?> { ["guard"] = new JsonObject { ["ReceiptId"] = "actual-native-receipt" } });
        bound.Payload["tenant"]!.GetValue<string>().ShouldBe("tenant-a"); bound.Payload["receipt"]!.GetValue<string>().ShouldBe("actual-native-receipt");
        Should.Throw<InvalidOperationException>(() => ProtectionQualificationRecipeBinding.Bind(call, request, new Dictionary<string, JsonNode?>()));
        var forbidden = call with { Payload = new JsonObject { ["payload"] = new JsonObject { ["$request"] = "/canaryBase64" } } };
        Should.Throw<InvalidOperationException>(() => ProtectionQualificationRecipeBinding.Bind(forbidden, request, new Dictionary<string, JsonNode?>()));
    }

    [Fact]
    public async Task ObserverUsesActualNativeStateAndNeverMutatesItsReadOnlyStore()
    {
        using var fixture = new ErasureRootEngineFixture();
        var original = fixture.Enroll();
        (await fixture.Bindings.ResolveAsync(original.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity), original.Binding.Identity.Target, true, Token)).ShouldNotBeNull();
        using var native = Substitute.For<DaprClient>();
        native.GetStateAsync<JsonElement>("read-only", "guard", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.SerializeToElement(new { Revision = 19, Receipts = new[] { new { ReceiptId = "native-original" } } }));
        var observer = new ProtectionQualificationPersistedObserver(native, fixture.Custody, fixture.Engine, new HashSet<string> { "read-only" });
        var recipe = new ProtectionQualificationObservationRecipe("tenant-a", new() { ["guard"] = new("read-only", "guard", new Dictionary<string, string>()) },
            new() { ["guardRevision"] = new("guard", "/Revision"), ["receiptCount"] = new("guard", "/Receipts", "Count") });
        var actual = await observer.ObserveAsync(recipe, [original.Binding.Identity.Target], Token);
        actual["guardRevision"]!.GetValue<int>().ShouldBe(19); actual["receiptCount"]!.GetValue<int>().ShouldBe(1);
        actual["roots"]![0]!["state"]!.GetValue<string>().ShouldBe("Active");
        native.ReceivedCalls().ShouldAllBe(call => call.GetMethodInfo().Name == "GetStateAsync");
        recipe = recipe with { Sources = new() { ["guard"] = new("uninstalled", "guard", new Dictionary<string, string>()) } };
        await Should.ThrowAsync<InvalidOperationException>(() => observer.ObserveAsync(recipe, [original.Binding.Identity.Target], Token));
    }
}
