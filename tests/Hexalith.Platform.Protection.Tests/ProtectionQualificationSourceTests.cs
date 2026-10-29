using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Secrets.Runtime;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Closed plan mapping, cross-language canonical wire bytes, independent native observation and signed private receiver coverage.</summary>
public sealed class ProtectionQualificationSourceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    [Fact]
    public async Task ClosedNegativeRecipesCoverAllFourteenFieldsHeadersSignatureManifestAndDispatchIdentities()
    {
        var fixture = new ErasureRootConsumptionFixture(); var request = (await fixture.SeedAsync()) with { DetachedJws = "eyJhbGciOiJFUzI1NiJ9..AQID" };
        var canonical = ProtectionQualificationSubstitutions.Create("RejectCanonicalHeaderPayloadSubstitutions", request, "tenant-b");
        canonical.Count.ShouldBe(18); canonical.Select(value => value.Mutation).Distinct(StringComparer.Ordinal).Count().ShouldBe(18);
        canonical.Take(14).Select(value => value.Mutation).ShouldBe(typeof(DeletionBatchCapabilityV1).GetProperties().Select(property => property.Name));
        canonical.ShouldAllBe(value => ErasureRootCustody.Hash(value.Request) != ErasureRootCustody.Hash(request));
        ProtectionQualificationSubstitutions.Create("RejectTenantKeyAttestationDispatchRevisionSubstitutions", request, "tenant-b").Count.ShouldBe(7);
        ProtectionQualificationSubstitutions.Create("RejectChangedManifest", request, "tenant-b").Count.ShouldBe(5);
        ProtectionQualificationSubstitutions.Create("RejectCrossTenantManifest", request, "tenant-b").Count.ShouldBe(2);
        Should.Throw<InvalidOperationException>(() => ProtectionQualificationSubstitutions.Create("caller-chosen-operation", request, "tenant-b"));
    }
    [Fact]
    public void CompleteSourceProgramsOwnEveryCaseOrderAndGuardOpcodeBeforeRuntimeEffects()
    {
        var required = ProtectionQualificationSourceCatalog.RequiredArguments();
        var values = required.ToDictionary(pair => pair.Key, pair => pair.Value.StartsWith("Guard:", StringComparison.Ordinal)
            ? new JsonObject { ["transition"] = new JsonObject { ["Operation"] = (int)Enum.Parse<GovernanceGuardOperation>(pair.Value[6..]) }, ["targets"] = new JsonArray() }
            : new JsonObject { ["nativeInputReference"] = pair.Key }, StringComparer.Ordinal);
        var compiled = ProtectionQualificationSourceCatalog.Compile(values); compiled.Count.ShouldBe(41);
        compiled["P20"]["HoldBeforeDispatch"].Before.Select(call => call.Payload["transition"]?["Operation"]?.GetValue<int>())
            .ShouldNotContain((int)GovernanceGuardOperation.CommitDispatch);
        compiled["P26"]["CompromiseBeforeRegistration"].Before.Select(call => call.Kind).ShouldNotContain(ProtectionQualificationNativeCallKind.Register);
        compiled["P16"]["DelayAndRestoreDispatch"].Effect.Payload["UseCurrentGuardRevision"]!.GetValue<bool>().ShouldBeFalse();
        var restore = compiled["P41"]["RestoreStaleWriterAndOwnerState"];
        restore.Before.TakeLast(4).Select(call => call.Kind).ShouldBe(new[] { ProtectionQualificationNativeCallKind.Backup, ProtectionQualificationNativeCallKind.Backup,
            ProtectionQualificationNativeCallKind.SealEvent, ProtectionQualificationNativeCallKind.Compromise });
        restore.After.Single().Kind.ShouldBe(ProtectionQualificationNativeCallKind.Restore);
        compiled["P39"]["TwoStaleSigningAttempts"].Before.Count(call => call.Kind == ProtectionQualificationNativeCallKind.ObsoleteUnissued).ShouldBe(2);
        string guard = required.First(pair => pair.Value.StartsWith("Guard:", StringComparison.Ordinal)).Key;
        values[guard]["transition"]!["Operation"] = 999;
        Should.Throw<InvalidOperationException>(() => ProtectionQualificationSourceCatalog.Compile(values));
        values.Remove(guard);
        Should.Throw<InvalidOperationException>(() => ProtectionQualificationSourceCatalog.Compile(values));
    }

    [Fact]
    public void SourceCatalogMapsEveryUnchangedOwnerActionAndSeparatesAuthorizationFromUnpin()
    {
        using var stream = typeof(ProtectionQualificationActionCatalog).Assembly.GetManifestResourceStream("ExtProtectionOwnerPlan")!;
        using var plan = JsonDocument.Parse(stream);
        var arguments = ProtectionQualificationSourceCatalog.RequiredArguments().ToDictionary(pair => pair.Key, pair => pair.Value.StartsWith("Guard:", StringComparison.Ordinal)
            ? new JsonObject { ["transition"] = new JsonObject { ["Operation"] = (int)Enum.Parse<GovernanceGuardOperation>(pair.Value[6..]) }, ["targets"] = new JsonArray() }
            : new JsonObject(), StringComparer.Ordinal);
        var recipes = ProtectionQualificationSourceCatalog.Compile(arguments);
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
    public async Task RuntimeIdentityCodecsBindActualTargetsSealBatchAndSignedOriginalWithoutStaticPredictions()
    {
        var fixture = new ErasureRootConsumptionFixture(); var supplied = await fixture.SeedAsync();
        var scope = new GovernanceScopeV1("tenant-a", "ExactInteraction", "interaction-a", "");
        var batch = new GovernanceBatchCommand("pending", "AcceptedSet", 0, supplied.Targets.Select(target => new GovernanceProtectionTarget(target.TenantId, target.AgentInteractionId, target.TargetProtectionKeyAlias)).ToArray(),
            supplied.Capability.ManifestDigest, 1, "key-v1", "", "", "", [], 0);
        var request = new JsonObject { ["targets"] = JsonSerializer.SerializeToNode(supplied.Targets), ["scope"] = JsonSerializer.SerializeToNode(scope) };
        JsonObject Codec(string name, JsonNode input) => new() { ["$codec"] = new JsonObject { ["name"] = name, ["input"] = input } };
        JsonObject Original(string pointer) => new() { ["$original"] = new JsonObject { ["id"] = "signed", ["pointer"] = pointer } };
        var payload = new JsonObject
        {
            ["manifest"] = Codec("TargetManifestDigest", new JsonObject { ["$request"] = "/targets" }),
            ["predicate"] = Codec("PredicateDigest", new JsonObject { ["$request"] = "/scope" }),
            ["seal"] = Codec("DestructionSealId", new JsonObject { ["tenantId"] = "tenant-a", ["requestId"] = "request-1", ["scope"] = new JsonObject { ["$request"] = "/scope" } }),
            ["signing"] = Codec("SigningRequestId", Original("/Payload")),
            ["artifact"] = Codec("SignedArtifactDigest", Original("/DetachedJws")),
        };
        string seal = GovernanceScopeGuardReducer.DestructionSealId("tenant-a", "request-1", scope);
        payload["batch"] = Codec("BatchId", new JsonObject { ["requestId"] = "request-1", ["sealId"] = seal, ["batch"] = JsonSerializer.SerializeToNode(batch) });
        var retained = new Dictionary<string, JsonNode?> { ["signed"] = new JsonObject { ["Payload"] = JsonSerializer.SerializeToNode(supplied.Capability), ["DetachedJws"] = supplied.DetachedJws } };
        var call = new ProtectionQualificationNativeCall("identity", ProtectionQualificationNativeCallKind.Lookup, payload);
        var result = ProtectionQualificationRecipeBinding.Bind(call, request, retained).Payload;
        result["manifest"]!.GetValue<string>().ShouldBe(supplied.Capability.ManifestDigest);
        result["predicate"]!.GetValue<string>().ShouldBe(GovernanceScopeGuardReducer.PredicateDigest(scope));
        result["seal"]!.GetValue<string>().ShouldBe(seal);
        result["batch"]!.GetValue<string>().ShouldBe(GovernanceScopeGuardReducer.BatchId("request-1", seal, batch));
        result["signing"]!.GetValue<string>().ShouldBe(DeletionBatchCapabilityCodec.SigningRequestId(supplied.Capability));
        result["artifact"]!.GetValue<string>().ShouldBe(DeletionBatchCapabilityIdentity.SignedArtifactDigest(supplied.DetachedJws));
        request["targets"]![0]!["TargetProtectionKeyAlias"] = "run-generated-successor-alias";
        ProtectionQualificationRecipeBinding.Bind(call, request, retained).Payload["manifest"]!.GetValue<string>().ShouldNotBe(supplied.Capability.ManifestDigest);
        payload["unknown"] = Codec("CallerSelectedIdentityProgram", new JsonObject());
        Should.Throw<InvalidOperationException>(() => ProtectionQualificationRecipeBinding.Bind(call, request, retained));
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
