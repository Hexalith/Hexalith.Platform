namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using System.Security.Cryptography;
using System.Net.Http.Headers;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.EventStore.Contracts.Routing;
using Shouldly;

[Collection("Platform file entry")]
public class PlatformRoutingCatalogIntegrationTests
{
    [Fact]
    public async Task ActualSidecarsRequireEveryHostToAgreeOnTheRunOwnedCatalog()
    {
        if (Environment.GetEnvironmentVariable("HEXALITH_PLATFORM_LIVE_SMOKE") != "1")
            Assert.Skip("Set HEXALITH_PLATFORM_LIVE_SMOKE=1 for actual Aspire and Dapr activation.");
        var options = await PlatformRoutingCatalogTests.OptionsAsync(includeParties: true);
        string root = options.PlatformRoot;
        string environment = "catalog-live-" + Guid.NewGuid().ToString("N");
        string evidence = Path.Combine(root, "_bmad-output", "implementation-artifacts", "evidence", "3-3", "verification", environment);
        CompositionWorkspace.CreatePrivateDirectory(evidence);
        await SourceMappingMaterializer.WriteAsync(options.Mapping, evidence, TestContext.Current.CancellationToken);
        var launch = new Dictionary<string, string>(SourceMappingMaterializer.Environment(options.Mapping, evidence))
        {
            ["HEXALITH_SOURCE_MAPPING_PATH"] = Path.Combine(evidence, "source-mapping", "mapping.json"),
            ["HEXALITH_SOURCE_MAPPING_HASH"] = options.Mapping.ContentHash,
            ["Platform__EnvironmentId"] = environment,
            ["Platform__ActiveModule"] = "parties",
            ["Platform__DeclarationPath"] = options.DeclarationPaths.Single(),
            ["Platform__UsePrebuiltProjects"] = "true",
        };
        string apphost = Path.Combine(root, "apphost.cs");
        bool started = false;
        try
        {
            foreach (var server in PlatformAppModel.Prepare(options))
            {
                var build = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet",
                    ["build", server.ProjectPath!, "-c", "Debug", "-m:1", "-p:GeneratePackageOnBuild=false", "--nologo"], root, launch),
                    TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
                File.WriteAllText(Path.Combine(evidence, server.ResourceId + "-build.txt"), build.Output + build.ErrorOutput);
                build.ExitCode.ShouldBe(0, server.ResourceId + ": " + build.Output + build.ErrorOutput);
            }
            var start = await RunAsync("start", ["--isolated", "--format", "Json"], launch, TimeSpan.FromMinutes(5));
            started = start.ExitCode == 0;
            start.ExitCode.ShouldBe(0, start.Output + start.ErrorOutput);
            foreach (var server in PlatformAppModel.Prepare(options))
            {
                var ready = await RunAsync("wait", [server.ResourceId, "--timeout", "180"], null, TimeSpan.FromMinutes(4));
                ready.ExitCode.ShouldBe(0, server.ResourceId + ": " + ready.Output + ready.ErrorOutput);
            }
            var described = await RunAsync("describe", ["eventstore", "--format", "Json"], null, TimeSpan.FromSeconds(30));
            described.ExitCode.ShouldBe(0, described.Output + described.ErrorOutput);
            string temporary = Path.Combine(Path.GetTempPath(), "hexalith-platform");
            string catalogDirectory = Directory.GetDirectories(temporary).Select(path => Path.Combine(path, environment + "-catalog"))
                .Single(Directory.Exists);
            byte[] canonical = File.ReadAllBytes(Path.Combine(catalogDirectory, "catalog.json"));
            var envelope = RouteCatalogCodec.Decode(canonical);
            envelope.SchemaVersion.ShouldBe(3);
            string digest = RouteCatalogCodec.ComputeRootDigest(envelope);
            var receipts = Directory.GetFiles(catalogDirectory, "*.committed.json");
            var actualHost = JsonSerializer.Deserialize<Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogHostObservation>(
                File.ReadAllBytes(Directory.GetFiles(catalogDirectory, "*.observation.json").First()));
            receipts.Length.ShouldBe(actualHost!.RequiredHostIds.Count);
            foreach (string path in receipts)
            {
                var readback = JsonSerializer.Deserialize<RouteCatalogGenerationReadback>(File.ReadAllBytes(path));
                readback!.Serving.ShouldBeTrue(); readback.RootDigest.ShouldBe(digest);
                File.Copy(path, Path.Combine(evidence, Path.GetFileName(path)), true);
            }
            using var description = JsonDocument.Parse(described.Output);
            var gateway = description.RootElement.GetProperty("resources").EnumerateArray().Single(resource => resource.GetProperty("displayName").GetString() == "eventstore");
            var settings = gateway.GetProperty("environment");
            string message = Encode(new { alg = "HS256", typ = "JWT" }) + "." + Encode(new Dictionary<string, object?>
            {
                ["iss"] = settings.GetProperty("Authentication__JwtBearer__Issuer").GetString(),
                ["aud"] = settings.GetProperty("Authentication__JwtBearer__Audience").GetString(),
                ["sub"] = "catalog-live-probe", ["eventstore:surface"] = "agent",
                ["exp"] = DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds(),
            });
            string signature = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.GetProperty("Authentication__JwtBearer__SigningKey").GetString()!), Encoding.UTF8.GetBytes(message)))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            string endpoint = gateway.GetProperty("urls").EnumerateArray().Single(url => url.GetProperty("name").GetString() == "http").GetProperty("url").GetString()!;
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", message + "." + signature);
            using var response = await client.GetAsync(endpoint + "/api/v1/routing/catalog", TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
            using var metadata = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
            metadata.RootElement.GetProperty("rootDigest").GetString().ShouldBe(digest);
            metadata.RootElement.GetProperty("schemaVersion").GetInt32().ShouldBe(3);
            metadata.RootElement.GetProperty("generation").GetInt64().ShouldBe(envelope.Generation);
            var publicOperations = envelope.Operations!.Where(operation => operation.Ingress == "public").ToArray();
            var discovered = metadata.RootElement.GetProperty("operations").EnumerateArray().ToArray();
            discovered.Select(operation => operation.GetProperty("routeId").GetString()).Order().ShouldBe(publicOperations.Select(operation => operation.RouteId).Order());
            foreach (var operation in publicOperations)
            {
                var found = discovered.Single(found => found.GetProperty("routeId").GetString() == operation.RouteId);
                JsonElement.DeepEquals(operation.RequestSchema, found.GetProperty("requestSchema")).ShouldBeTrue();
                JsonElement.DeepEquals(operation.ResultSchema, found.GetProperty("resultSchema")).ShouldBeTrue();
            }
            File.WriteAllBytes(Path.Combine(evidence, "metadata-http.json"), JsonSerializer.SerializeToUtf8Bytes(new
            {
                method = "GET", path = "/api/v1/routing/catalog", status = (int)response.StatusCode,
                authentication = "signed JWT; eventstore:surface=agent; bearer redacted", body = metadata.RootElement,
            }));
            File.WriteAllBytes(Path.Combine(evidence, "catalog.json"), canonical);
            File.Copy(Path.Combine(catalogDirectory, "receipt.json"), Path.Combine(evidence, "receipt.json"), true);

            static string Encode(object value) => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
        finally
        {
            await StopStartedHostAsync(started, (timeout, cancellation) => RunAsync("stop", [], null, timeout, cancellation));
        }

        async Task<CompositionProcessResult> RunAsync(string command, string[] arguments, IReadOnlyDictionary<string, string>? values, TimeSpan timeout, CancellationToken? cancellationToken = null)
        {
            var result = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("aspire", [command, .. arguments, "--apphost", apphost, "--non-interactive"], root, values), timeout, cancellationToken ?? TestContext.Current.CancellationToken);
            string safe = result.Output + result.ErrorOutput;
            if (command == "describe" && result.ExitCode == 0 && safe.TrimStart().StartsWith('{'))
            {
                var model = JsonNode.Parse(safe)!;
                foreach (var resource in model["resources"]!.AsArray())
                    if (resource!["environment"] is JsonObject settings)
                        foreach (string key in settings.Select(pair => pair.Key).ToArray()) settings[key] = "[redacted]";
                safe = model.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            }
            safe = System.Text.RegularExpressions.Regex.Replace(safe, "([?&]t=)[^\\s\\\"\\\\&]+", "$1[redacted]");
            File.WriteAllText(Path.Combine(evidence, command + ".txt"), safe);
            return result;
        }
    }

    [Fact]
    public async Task FailedAssertionAfterStartupStillStopsWithAnUncancelledBound()
    {
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        bool stopped = false;
        var failure = await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            try { throw new InvalidOperationException("original assertion"); }
            finally
            {
                await StopStartedHostAsync(true, (timeout, cancellation) =>
                {
                    timeout.ShouldBe(TimeSpan.FromSeconds(30));
                    cancellation.CanBeCanceled.ShouldBeFalse();
                    stopped = true;
                    return Task.FromResult(new CompositionProcessResult(true, 0, "stopped", false));
                });
            }
        });
        stopped.ShouldBeTrue();
        failure.Message.ShouldBe("original assertion");
    }

    [Fact]
    public async Task UnstartedHostDoesNotIssueStop()
    {
        await StopStartedHostAsync(false, (_, _) => throw new InvalidOperationException("must not stop"));
    }

    [Fact]
    public async Task CleanupFailureRetainsProcessDiagnostics()
    {
        var failure = await Assert.ThrowsAsync<ShouldAssertException>(() => StopStartedHostAsync(true, (_, _) =>
            Task.FromResult(new CompositionProcessResult(true, 1, "stop stdout", false) { ErrorOutput = "stop stderr" })));
        failure.Message.ShouldContain("stop stdout");
        failure.Message.ShouldContain("stop stderr");
    }

    internal static async Task StopStartedHostAsync(bool started, Func<TimeSpan, CancellationToken, Task<CompositionProcessResult>> stop)
    {
        if (!started) return;
        CompositionProcessResult result = await stop(TimeSpan.FromSeconds(30), CancellationToken.None);
        result.TimedOut.ShouldBeFalse(result.Output + result.ErrorOutput);
        result.ExitCode.ShouldBe(0, result.Output + result.ErrorOutput);
    }
}
