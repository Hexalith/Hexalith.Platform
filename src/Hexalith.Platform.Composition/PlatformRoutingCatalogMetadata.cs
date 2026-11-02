namespace Hexalith.Platform.Composition;

using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;

/// <summary>Observes public catalog metadata through the existing run credential and canonical owner schemas.</summary>
internal static class PlatformRoutingCatalogMetadata
{
    /// <summary>Requires the serving public metadata to match this run's complete canonical generation.</summary>
    internal static async Task ObserveAsync(HttpClient gateway, RouteCatalogEnvelope catalog, string rootDigest,
        string catalogDirectory, CancellationToken cancellationToken)
    {
        string proof = Path.Combine(catalogDirectory, "metadata-http.json");
        File.Delete(proof);
        using HttpResponseMessage response = await gateway.GetAsync("/api/v1/routing/catalog", HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException("The existing native credential could not observe public catalog metadata.");
        }

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        byte[] bytes = await ReadBoundedAsync(stream, cancellationToken).ConfigureAwait(false);

        using JsonDocument metadata = JsonDocument.Parse(bytes);
        JsonElement body = metadata.RootElement;
        ValidateInventory(body, catalog, rootDigest, out RouteOperationContract[] expected, out JsonElement[] actual);
        foreach (RouteOperationContract operation in expected)
        {
            JsonElement observed = actual.Single(candidate => candidate.GetProperty("routeId").GetString() == operation.RouteId);
            RouteContractDeclaration declaration = catalog.Declarations.Single(candidate => candidate.RouteId == operation.RouteId);
            ValidateOperation(observed, operation, declaration);
        }

        await File.WriteAllBytesAsync(proof, JsonSerializer.SerializeToUtf8Bytes(new
        {
            method = "GET", path = "/api/v1/routing/catalog", status = (int)response.StatusCode,
            authentication = "existing native agent credential; bearer omitted", body,
        }), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        byte[] block = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(block, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > 4194304)
            {
                throw new InvalidOperationException("Public catalog metadata exceeds the bounded observation contract.");
            }

            await buffer.WriteAsync(block.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return buffer.ToArray();
    }

    private static void ValidateInventory(JsonElement body, RouteCatalogEnvelope catalog, string rootDigest,
        out RouteOperationContract[] expected, out JsonElement[] actual)
    {
        if (!HasFields(body, ["schemaVersion", "generation", "rootDigest", "operations"]))
        {
            throw new InvalidOperationException("Public catalog metadata contains an unexpected field inventory.");
        }

        expected = catalog.Operations!.Where(operation => operation.Ingress == "public").ToArray();
        actual = body.GetProperty("operations").EnumerateArray().ToArray();
        if (catalog.SchemaVersion != 3 || catalog.Profile != "Development" || expected.Length == 0
            || body.GetProperty("schemaVersion").GetInt32() != catalog.SchemaVersion
            || body.GetProperty("generation").GetInt64() != catalog.Generation
            || body.GetProperty("rootDigest").GetString() != rootDigest
            || !actual.Select(operation => operation.GetProperty("routeId").GetString()).Order(StringComparer.Ordinal)
                .SequenceEqual(expected.Select(operation => operation.RouteId).Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Public metadata differs from this run's canonical catalog identity or public inventory.");
        }

    }

    private static void ValidateOperation(JsonElement observed, RouteOperationContract operation, RouteContractDeclaration declaration)
    {
        if (!HasFields(observed, ["routeId", "contractVersion", "eligibility", "digest", "requestSchema", "resultSchema", "kind", "domain", "messageType"])
            || observed.GetProperty("contractVersion").GetString() != operation.ContractVersion
            || observed.GetProperty("eligibility").GetString() != operation.Eligibility
            || observed.GetProperty("digest").GetString() != operation.Digest
            || observed.GetProperty("kind").GetString() != declaration.Kind
            || observed.GetProperty("domain").GetString() != declaration.Domain
            || observed.GetProperty("messageType").GetString() != declaration.MessageType
            || !JsonElement.DeepEquals(operation.RequestSchema, observed.GetProperty("requestSchema"))
            || !JsonElement.DeepEquals(operation.ResultSchema, observed.GetProperty("resultSchema")))
        {
            throw new InvalidOperationException("Public operation policy, declaration or schemas differ from the compiled owner catalog.");
        }
    }

    private static bool HasFields(JsonElement value, IReadOnlyList<string> fields) => value.ValueKind == JsonValueKind.Object
        && value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(fields.Order(StringComparer.Ordinal), StringComparer.Ordinal);
}
