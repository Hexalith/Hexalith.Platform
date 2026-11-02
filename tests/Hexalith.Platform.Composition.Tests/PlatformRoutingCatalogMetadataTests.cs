namespace Hexalith.Platform.Composition.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Routing;

using Shouldly;

/// <summary>Checks canonical public-schema observations through an unchanged native credential.</summary>
public sealed class PlatformRoutingCatalogMetadataTests
{
    /// <summary>Refuses stale, different, internal or oversized metadata and revokes a previous proof.</summary>
    /// <param name="scenario">The response mutation.</param>
    [Theory]
    [InlineData("valid")]
    [InlineData("wrong-root")]
    [InlineData("wrong-generation")]
    [InlineData("wrong-schema")]
    [InlineData("wrong-result-schema")]
    [InlineData("wrong-policy")]
    [InlineData("wrong-digest")]
    [InlineData("wrong-contract-version")]
    [InlineData("wrong-kind")]
    [InlineData("wrong-domain")]
    [InlineData("wrong-message-type")]
    [InlineData("extra-root-field")]
    [InlineData("extra-key-field")]
    [InlineData("extra-operation-field")]
    [InlineData("internal-operation")]
    [InlineData("unauthorized")]
    [InlineData("oversized")]
    public async Task ObservedMetadataMustMatchTheCanonicalPublicGeneration(string scenario)
    {
        string directory = Path.Combine(Path.GetTempPath(), "catalog-http-proof-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string proof = Path.Combine(directory, "metadata-http.json");
        await File.WriteAllTextAsync(proof, "stale", TestContext.Current.CancellationToken);
        try
        {
            RouteCatalogEnvelope catalog = CreateCatalog(out JsonElement request, out JsonElement result);
            string root = new('a', 64);
            JsonObject metadata = CreateMetadata(catalog, root);
            MutateMetadataIdentity(metadata, scenario);
            MutateOperationSchemas(metadata, scenario, request, result);
            MutateOperationPolicy(metadata, scenario);
            string credential = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            using var response = new HttpResponseMessage(scenario == "unauthorized" ? HttpStatusCode.Unauthorized : HttpStatusCode.OK)
            {
                Content = new StringContent(metadata.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            using var handler = new CatalogMetadataResponseHandler(response, message =>
            {
                message.Method.ShouldBe(HttpMethod.Get);
                message.RequestUri!.AbsolutePath.ShouldBe("/api/v1/routing/catalog");
                (message.Headers.Authorization?.Scheme == "Bearer" && message.Headers.Authorization.Parameter == credential).ShouldBeTrue();
            });
            using var client = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:1") };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential);
            await AssertMetadataOutcomeAsync(client, catalog, root, directory, proof, scenario, credential, request);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static RouteCatalogEnvelope CreateCatalog(out JsonElement request, out JsonElement result)
    {
        request = JsonSerializer.SerializeToElement(new { type = "object", properties = new { identity = new { type = "string" } } });
        result = JsonSerializer.SerializeToElement(new { type = "array", items = new { type = "string" } });
        RouteOperationContract[] operations =
        [
            new("public-route", "v1", "public", "agent-eligible", request, result, new string('b', 64)),
            new("internal-route", "v1", "internal", "UI-only", request, result, new string('c', 64)),
        ];
        RouteContractDeclaration[] declarations =
        [
            new("public-route", "query", "fixture", "FixtureQuery", null, "v1", null, "workload", "query:read", null, []),
            new("internal-route", "command", "fixture", "FixtureCommand", null, "v1", 1, "workload", "command:submit", null, []),
        ];
        return new RouteCatalogEnvelope(3, 7, "Development", true, declarations, [], [], new string('d', 64), new string('e', 64), operations);
    }

    private static JsonObject CreateMetadata(RouteCatalogEnvelope catalog, string root)
    {
        return JsonSerializer.SerializeToNode(new
        {
            schemaVersion = 3, generation = 7, rootDigest = root,
            operations = new[] { new { routeId = "public-route", contractVersion = "v1", eligibility = "agent-eligible", digest = catalog.Operations![0].Digest,
                requestSchema = catalog.Operations![0].RequestSchema, resultSchema = catalog.Operations![0].ResultSchema, kind = "query", domain = "fixture", messageType = "FixtureQuery" } },
        })!.AsObject();
    }

    private static void MutateMetadataIdentity(JsonObject metadata, string scenario)
    {
        if (scenario == "wrong-root")
        {
            metadata["rootDigest"] = new string('f', 64);
        }
        if (scenario == "wrong-generation")
        {
            metadata["generation"] = 8;
        }
        if (scenario == "extra-root-field")
        {
            metadata["retainedIdempotency"] = new JsonArray();
        }
        if (scenario == "extra-key-field")
        {
            metadata["signingKey"] = "[redacted]";
        }
        if (scenario == "oversized")
        {
            metadata["padding"] = new string('x', 4194304);
        }
    }

    private static void MutateOperationSchemas(JsonObject metadata, string scenario, JsonElement request, JsonElement result)
    {
        if (scenario == "wrong-schema")
        {
            metadata["operations"]![0]!["requestSchema"]!["type"] = "string";
        }
        if (scenario == "wrong-result-schema")
        {
            metadata["operations"]![0]!["resultSchema"]!["type"] = "object";
        }
        if (scenario == "internal-operation") metadata["operations"]!.AsArray().Add(new JsonObject
        {
            ["routeId"] = "internal-route", ["requestSchema"] = JsonNode.Parse(request.GetRawText()), ["resultSchema"] = JsonNode.Parse(result.GetRawText()),
        });
    }

    private static void MutateOperationPolicy(JsonObject metadata, string scenario)
    {
        if (scenario == "wrong-policy")
        {
            metadata["operations"]![0]!["eligibility"] = "UI-only";
        }
        if (scenario == "wrong-digest")
        {
            metadata["operations"]![0]!["digest"] = new string('f', 64);
        }
        if (scenario == "wrong-contract-version")
        {
            metadata["operations"]![0]!["contractVersion"] = "v2";
        }
        if (scenario == "wrong-kind")
        {
            metadata["operations"]![0]!["kind"] = "command";
        }
        if (scenario == "wrong-domain")
        {
            metadata["operations"]![0]!["domain"] = "different-owner";
        }
        if (scenario == "wrong-message-type")
        {
            metadata["operations"]![0]!["messageType"] = "DifferentQuery";
        }
        if (scenario == "extra-operation-field")
        {
            metadata["operations"]![0]!["internalTarget"] = "unexpected";
        }
    }

    private static async Task AssertMetadataOutcomeAsync(HttpClient client, RouteCatalogEnvelope catalog, string root, string directory, string proof, string scenario, string credential, JsonElement request)
    {
        if (scenario == "valid")
        {
            await PlatformRoutingCatalogMetadata.ObserveAsync(client, catalog, root, directory, TestContext.Current.CancellationToken).ConfigureAwait(false);
            using JsonDocument observed = JsonDocument.Parse(await File.ReadAllBytesAsync(proof, TestContext.Current.CancellationToken).ConfigureAwait(false));
            JsonElement body = observed.RootElement.GetProperty("body");
            body.GetProperty("rootDigest").GetString().ShouldBe(root);
            JsonElement.DeepEquals(body.GetProperty("operations")[0].GetProperty("requestSchema"), request).ShouldBeTrue();
            (await File.ReadAllTextAsync(proof, TestContext.Current.CancellationToken).ConfigureAwait(false)).ShouldNotContain(credential);
        }
        else
        {
            await Should.ThrowAsync<InvalidOperationException>(() => PlatformRoutingCatalogMetadata.ObserveAsync(client, catalog, root, directory, TestContext.Current.CancellationToken)).ConfigureAwait(false);
            File.Exists(proof).ShouldBeFalse();
        }
    }

    /// <summary>Uses the caller's finite cancellation while reading a stalled response and revokes stale proof.</summary>
    [Fact]
    public async Task CancelledResponseCannotRetainAnEarlierProof()
    {
        string directory = Path.Combine(Path.GetTempPath(), "catalog-http-cancel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string proof = Path.Combine(directory, "metadata-http.json");
        await File.WriteAllTextAsync(proof, "stale", TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        try
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new CatalogMetadataStallingStream(started)) };
            using var handler = new CatalogMetadataResponseHandler(response, _ => { });
            using var client = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:1"), Timeout = Timeout.InfiniteTimeSpan };
            var catalog = new RouteCatalogEnvelope(3, 7, "Development", true, [], [], [], new string('d', 64), new string('e', 64), []);
            Task observing = PlatformRoutingCatalogMetadata.ObserveAsync(client, catalog, new string('a', 64), directory, cancellation.Token);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            cancellation.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(async () => await observing.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ConfigureAwait(false));
            File.Exists(proof).ShouldBeFalse();
        }
        finally
        {
            cancellation.Cancel();
            Directory.Delete(directory, recursive: true);
        }
    }

}
