namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Net;
using System.Net.Http.Headers;
using System.Globalization;
using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Tenants.Contracts.Identity;
using Hexalith.Tenants.Contracts.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

/// <summary>Exercises native owner source/refusal checks and controlled readiness transport without live runtime qualification.</summary>
public sealed class PlatformNativeTestPreparationTests
{
    private const string ScopeRefusal = "The native consumer scope differs from this owning run.";

    private static readonly Lazy<(byte[] Bytes, string Root)> TenantsCatalog = new(() =>
    {
        // Mirrors the Platform catalog builder for the actual Tenants owner contracts, in process.
        const long generation = 1;
        IReadOnlyDictionary<string, string> methods = TenantCatalogDeclarations.ReceiverMethods;
        RouteCatalogEnvelope envelope = VersionedRouteCatalogBuilder.Build(new(generation, "Development",
            TenantCatalogDeclarations.Routes, TenantCatalogDeclarations.Operations,
            [.. TenantCatalogDeclarations.Routes.Select(route => new RouteCompositionBinding(route.RouteId, "tenants", methods[route.RouteId], route.ContractVersion, false, "*"))],
            [.. TenantCatalogDeclarations.Commands.Select(command => DevelopmentIdempotencyBindings.Bind(command, generation))]));
        return (RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope));
    });

    /// <summary>Refuses each foreign or broadened consumer scope before any state, token or credential channel exists.</summary>
    [Theory]
    [InlineData("foreign-run")]
    [InlineData("foreign-module")]
    [InlineData("global-administrators-domain")]
    [InlineData("missing-synthetic-tenant")]
    public async Task RefusesANativeScopeThatDiffersFromTheOwningRunAsync(string violation)
    {
        using var workspace = new PreparationWorkspace(DateTimeOffset.UtcNow.AddMinutes(10));
        CompositionNativeTestScope valid = workspace.Scope;
        workspace.WriteSource(violation switch
        {
            "foreign-run" => valid with { RunId = CompositionRunPlanFactory.NewRunId() },
            "foreign-module" => valid with { ModuleId = "memories" },
            "global-administrators-domain" => valid with { Domains = [.. valid.Domains, TenantIdentity.GlobalAdministratorsDomain] },
            "missing-synthetic-tenant" => valid with { Tenants = [workspace.Plan.ForeignTenantNamespace] },
            _ => throw new ArgumentOutOfRangeException(nameof(violation)),
        });
        workspace.WriteStaleCredentials();
        PlatformNativeTestPreparation preparation = workspace.Preparation();
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        using CancellationTokenSource bound = Bound();

        Exception failure = await CaptureAsync(preparation.PrepareAsync(services, bound.Token));

        failure.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe(ScopeRefusal);
        workspace.ShouldBeUnprepared(preparation);
#if HEXALITH_PLATFORM_CANONICAL_SCOPES
        if (violation == "missing-synthetic-tenant")
        {
            // Keeping the owning tenant must not make an additional valid foreign grant eligible.
            workspace.Plan.ForeignTenantNamespace.ShouldNotBe(workspace.Plan.TenantNamespace);
            Hexalith.EventStore.Contracts.Identity.TenantCanonicalizer.TryCanonicalize(workspace.Plan.TenantNamespace, out _).ShouldBeTrue();
            Hexalith.EventStore.Contracts.Identity.TenantCanonicalizer.TryCanonicalize(workspace.Plan.ForeignTenantNamespace, out _).ShouldBeTrue();
            File.Exists(Path.Combine(workspace.Catalog, "preparation-progress.json")).ShouldBeFalse();
            File.WriteAllBytes(Path.Combine(workspace.Catalog, "native-scope.json"), JsonSerializer.SerializeToUtf8Bytes(
                valid with { Tenants = [workspace.Plan.TenantNamespace, workspace.Plan.ForeignTenantNamespace] }));
            workspace.WriteStaleCredentials();
            PlatformNativeTestPreparation broadened = workspace.Preparation();

            Exception broaderFailure = await CaptureAsync(broadened.PrepareAsync(services, bound.Token));

            broaderFailure.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe(ScopeRefusal);
            File.Exists(Path.Combine(workspace.Catalog, "preparation-progress.json")).ShouldBeFalse();
            File.Exists(Path.Combine(workspace.Root, "readiness.json")).ShouldBeFalse();
            workspace.ShouldBeUnprepared(broadened);
        }
#endif
    }

    /// <summary>Removes a credential channel that appears after entry cleanup when preparation is refused or cancelled.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedPreparationRemovesCredentialFilesWrittenWhileItRunsAsync(bool cancel)
    {
        using var workspace = new PreparationWorkspace(DateTimeOffset.UtcNow.AddMinutes(10));
        workspace.WriteStaleCredentials();
        PlatformNativeTestPreparation preparation = workspace.Preparation();
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        using CancellationTokenSource bound = Bound();

        // Entry cleanup runs synchronously; the owner then waits for its scope, which is not yet written.
        Task preparing = preparation.PrepareAsync(services, bound.Token);
        File.Exists(workspace.CredentialPath).ShouldBeFalse();
        File.Exists(workspace.CredentialPath + ".tmp").ShouldBeFalse();
        preparing.IsCompleted.ShouldBeFalse();
        workspace.WriteStaleCredentials();
        if (cancel) await bound.CancelAsync();
        else workspace.WriteSource(workspace.Scope with { RunId = CompositionRunPlanFactory.NewRunId() });

        Exception failure = await CaptureAsync(preparing);

        if (cancel) failure.ShouldBeAssignableTo<OperationCanceledException>();
        else failure.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe(ScopeRefusal);
        workspace.ShouldBeUnprepared(preparation);
    }

    /// <summary>Cancels, rather than faults, once the startup deadline elapsed before the bound token fired.</summary>
    [Fact]
    public async Task ElapsedStartupDeadlineCancelsBeforeMintingCredentialsAsync()
    {
        using var workspace = new PreparationWorkspace(DateTimeOffset.UtcNow.AddSeconds(-1));
        workspace.WriteSource(workspace.Scope);
        workspace.WriteStaleCredentials();
        PlatformNativeTestPreparation preparation = workspace.Preparation();
        // No application model: minting tokens and resolving the gateway would fault instead of cancelling.
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        using CancellationTokenSource bound = Bound();

        Task preparing = preparation.PrepareAsync(services, bound.Token);
        Exception failure = await CaptureAsync(preparing);

        bound.IsCancellationRequested.ShouldBeFalse();
        failure.ShouldBeOfType<OperationCanceledException>().Message.ShouldContain("startup deadline");
        // The Builds AppHost runner reports a cancelled (not faulted) owner preparation as the startup stall HXR021.
        preparing.IsCanceled.ShouldBeTrue();
        // The valid scope, catalog and closure passed every source check first.
        using (JsonDocument progress = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(workspace.Catalog, "preparation-progress.json"))))
            progress.RootElement.GetProperty("step").GetString().ShouldBe("source-validated");
        workspace.ShouldBeUnprepared(preparation);
    }

    /// <summary>Validates complete source proofs before requiring the network-backed application model.</summary>
    [Fact]
    public async Task FutureStartupDeadlineValidatesSourceBeforeRequiringApplicationModelAsync()
    {
        using var workspace = new PreparationWorkspace(DateTimeOffset.UtcNow.AddMinutes(10));
        workspace.WriteSource(workspace.Scope);
        workspace.WriteStaleCredentials();
        PlatformNativeTestPreparation preparation = workspace.Preparation();
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        using CancellationTokenSource bound = Bound();

        Task preparing = preparation.PrepareAsync(services, bound.Token);
        Exception failure = await CaptureAsync(preparing);

        bound.IsCancellationRequested.ShouldBeFalse();
        preparing.IsFaulted.ShouldBeTrue();
        failure.ShouldBeOfType<InvalidOperationException>().Message.ShouldContain(
            "Aspire.Hosting.ApplicationModel.DistributedApplicationModel");
        using (JsonDocument progress = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(workspace.Catalog, "preparation-progress.json"))))
            progress.RootElement.GetProperty("step").GetString().ShouldBe("source-validated");
        workspace.ShouldBeUnprepared(preparation);
        await AssertReadinessPollingAsync(workspace);
#if HEXALITH_PLATFORM_CANONICAL_SCOPES
        await PlatformNativeGatewayContractChecks.AssertAsync(workspace.Catalog, workspace.Plan.TenantNamespace);
#endif
    }

#if HEXALITH_PLATFORM_V2_CATALOG
    /// <summary>The preparation entry point must reject complete activation/readback drift before credentials or source-validated progress.</summary>
    [Theory]
    [InlineData("activation-attempt")]
    [InlineData("activation-binding")]
    [InlineData("readback-attempt")]
    [InlineData("readback-binding")]
    public async Task CompleteActivationValidationRunsBeforeSourceProgressAndCredentialsAsync(string mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        using var workspace = new PreparationWorkspace(DateTimeOffset.UtcNow.AddMinutes(10));
        workspace.WriteSource(workspace.Scope);
        string activationPath = Path.Combine(workspace.Catalog, "activation.json");
        if (mutation.StartsWith("activation-", StringComparison.Ordinal))
        {
            var activation = JsonSerializer.Deserialize<RouteCatalogActivation>(File.ReadAllBytes(activationPath))!;
            activation = mutation == "activation-attempt" ? activation with { AttemptId = "foreign-attempt" }
                : activation with { TrustProof = new string('f', 64) };
            File.WriteAllBytes(activationPath, JsonSerializer.SerializeToUtf8Bytes(activation));
        }
        else
        {
            string readbackPath = Path.Combine(workspace.Catalog, "eventstore.committed.json");
            var readback = JsonSerializer.Deserialize<RouteCatalogGenerationReadback>(File.ReadAllBytes(readbackPath))!;
            readback = mutation == "readback-attempt" ? readback with { AttemptId = "foreign-attempt" }
                : readback with { ActivationBindingDigest = new string('f', 64) };
            File.WriteAllBytes(readbackPath, JsonSerializer.SerializeToUtf8Bytes(readback));
        }
        workspace.WriteStaleCredentials();
        PlatformNativeTestPreparation preparation = workspace.Preparation();
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        using CancellationTokenSource bound = Bound();

        Exception failure = await CaptureAsync(preparation.PrepareAsync(services, bound.Token));

        failure.ShouldBeOfType<InvalidOperationException>();
        File.Exists(Path.Combine(workspace.Catalog, "preparation-progress.json")).ShouldBeFalse();
        workspace.ShouldBeUnprepared(preparation);
    }
#endif

    /// <summary>Required owner inputs stay inside the real owned workspace even when their containing directory is linked.</summary>
    [Theory]
    [InlineData("parent-link")]
    [InlineData("outside-root")]
    public async Task RequiredInputsRefuseLinkedParentsAndOutsideRootsAsync(string mutation)
    {
        if (OperatingSystem.IsWindows() && mutation == "parent-link") return;
        using var workspace = new PreparationWorkspace(DateTimeOffset.UtcNow.AddMinutes(10));
        workspace.WriteSource(workspace.Scope);
        workspace.WriteStaleCredentials();
        string outside = Path.Combine(Path.GetTempPath(), "native-proof-outside-" + Guid.NewGuid().ToString("N"));
        Directory.Move(workspace.Catalog, outside);
        if (mutation == "parent-link") Directory.CreateSymbolicLink(workspace.Catalog, outside);
        byte[] originalScope = File.ReadAllBytes(Path.Combine(outside, "native-scope.json"));
        try
        {
            PlatformNativeTestPreparation preparation = workspace.Preparation(mutation == "parent-link" ? workspace.Catalog : outside);
            using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
            using CancellationTokenSource bound = Bound();

            Exception failure = await CaptureAsync(preparation.PrepareAsync(services, bound.Token));

            failure.ShouldBeOfType<InvalidOperationException>().Message.ShouldContain("owned workspace");
            File.Exists(Path.Combine(outside, "preparation-progress.json")).ShouldBeFalse();
            File.ReadAllBytes(Path.Combine(outside, "native-scope.json")).ShouldBe(originalScope);
            workspace.ShouldBeUnprepared(preparation);
        }
        finally
        {
            if (new DirectoryInfo(workspace.Catalog).LinkTarget is not null) Directory.Delete(workspace.Catalog);
            Directory.Delete(outside, recursive: true);
        }
    }

    /// <summary>Writes the private endpoint channel exactly as the independent Builds consumer reads it.</summary>
    [Fact]
    public async Task EndpointCredentialWriterProducesTheChannelTheBuildsReaderAcceptsAsync()
    {
        using var workspace = new PreparationWorkspace(null);
        const string apiToken = "synthetic-dapr-api-token.v1~/+=";
        var sidecar = new Uri("http://127.0.0.1:3500/");
        var endpoint = new CompositionNativeHttpEndpoint("memories", "memories", "dapr-invocation",
            new Uri("http://127.0.0.1:3500/v1.0/invoke/memories/method/api/"), "memories-dapr-cli");
        CompositionNativeHttpEndpoints.IsInvocationUri(endpoint.Uri).ShouldBeTrue();

        await PlatformNativeTestPreparation.WriteEndpointCredentialsAsync(workspace.CredentialPath, workspace.Plan, endpoint, apiToken, TestContext.Current.CancellationToken);

        File.Exists(workspace.CredentialPath + ".tmp").ShouldBeFalse();
        if (!OperatingSystem.IsWindows()) File.GetUnixFileMode(workspace.CredentialPath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        using (JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(workspace.CredentialPath)))
        {
            JsonElement root = document.RootElement;
            root.EnumerateObject().Select(property => property.Name).ShouldBe(["schema", "runId", "syntheticTenantId", "dataIsolationId", "endpoints"]);
            root.GetProperty("schema").GetString().ShouldBe(NativeTestHandoff.EndpointCredentialsSchema);
            root.GetProperty("runId").GetString().ShouldBe(workspace.Plan.RunId);
            root.GetProperty("syntheticTenantId").GetString().ShouldBe(workspace.Plan.TenantNamespace);
            root.GetProperty("dataIsolationId").GetString().ShouldBe(workspace.Plan.ResourceNamespace);
            JsonElement entry = root.GetProperty("endpoints").EnumerateArray().ShouldHaveSingleItem();
            entry.EnumerateObject().Select(property => (property.Name, property.Value.GetString())).ShouldBe(
            [
                ("resourceId", "memories"), ("moduleId", "memories"), ("endpointName", "dapr-invocation"),
                ("uri", endpoint.Uri.AbsoluteUri), ("headerName", "dapr-api-token"), ("value", apiToken),
            ]);
        }

        // The runner's readiness binds the prepared metadata to the observed owner and sidecar allocation.
        CompositionResourceReadiness[] observed =
        [
            new("memories", "Container", "Healthy") { ModuleId = "memories" },
            new("memories-dapr-cli", "Executable", "Healthy") { ModuleId = "memories", Endpoints = [new("http", "http", sidecar)] },
        ];
        var readiness = new CompositionReadiness(CompositionReadiness.SupportedSchema, workspace.Plan.RunId, new Uri("http://127.0.0.1:3501/"), null,
            CompositionNativeHttpEndpoints.Bind(observed, [endpoint]))
        {
            NativeHttpEndpoints = [endpoint], NativeEndpointCredentialsPath = workspace.CredentialPath,
        };
        (await ReadBuildsSecretsAsync(workspace.Plan, readiness)).ShouldBe([apiToken]);
        // The independent reader binds the exact run isolation, so its acceptance above is not vacuous.
        (await ReadBuildsSecretsAsync(workspace.Plan with { TenantNamespace = workspace.Plan.ForeignTenantNamespace }, readiness)).ShouldBeNull();
    }

    private static async Task AssertReadinessPollingAsync(PreparationWorkspace workspace)
    {
        using CancellationTokenSource bound = Bound();
        PlatformNativeTestPreparation preparation = workspace.Preparation();
        foreach (string header in new[] { "delta", "date", "absent", "invalid", "past", "zero" })
        {
            int requests = 0, predicates = 0;
            DateTimeOffset notBefore = default;
            using var client = new HttpClient(new NativeReadinessResponseHandler(request =>
            {
                request.Method.ShouldBe(HttpMethod.Get);
                request.RequestUri!.AbsolutePath.ShouldBe("/tenants/synthetic");
                requests++;
                if (requests == 1)
                {
                    var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                        { Content = new StringContent("{\"id\":\"synthetic\",\"status\":\"active\"}") };
                    DateTimeOffset now = DateTimeOffset.UtcNow;
                    if (header == "delta") response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(2));
                    else if (header == "date") response.Headers.TryAddWithoutValidation("Retry-After", now.AddSeconds(2).ToString("R", CultureInfo.InvariantCulture));
                    else if (header == "invalid") response.Headers.TryAddWithoutValidation("Retry-After", "invalid-component-header");
                    else if (header == "past") response.Headers.RetryAfter = new RetryConditionHeaderValue(now.AddMinutes(-1));
                    else if (header == "zero") response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                    notBefore = header == "date" ? response.Headers.RetryAfter!.Date!.Value
                        : header == "delta" ? now + response.Headers.RetryAfter!.Delta!.Value : now.AddSeconds(1);
                    return response;
                }
                if (requests == 2)
                {
                    // Allow only timer precision; the old100ms loop and immediate retry cannot satisfy the server wait.
                    DateTimeOffset.UtcNow.ShouldBeGreaterThanOrEqualTo(notBefore.AddMilliseconds(-30));
                    predicates.ShouldBe(0, "A429 body must never reach the readiness predicate.");
                }
                string json = requests switch
                {
                    2 => "{\"id\":\"synthetic\",\"status\":\"provisioning\"}",
                    3 => "{\"id\":\"foreign\",\"status\":\"active\"}",
                    4 => "{\"id\":\"synthetic\",\"status\":\"active\"}",
                    _ => throw new InvalidOperationException("Unexpected readiness component request."),
                };
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            })) { BaseAddress = new Uri("http://localhost/") };
            await preparation.PollAsync("memories-active-observing", client, "tenants/synthetic", element =>
            {
                predicates++;
                return element.GetProperty("id").GetString() == "synthetic" && element.GetProperty("status").GetString() == "active";
            }, bound.Token);
            requests.ShouldBe(4); predicates.ShouldBe(3);
            workspace.ShouldBeUnprepared(preparation);
        }
        int transientRequests = 0;
        HttpStatusCode[] transient = [HttpStatusCode.NotFound, HttpStatusCode.Forbidden, HttpStatusCode.Conflict, HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK];
        using (var client = new HttpClient(new NativeReadinessResponseHandler(_ =>
        {
            HttpStatusCode status = transient[transientRequests++];
            return new HttpResponseMessage(status) { Content = new StringContent("{\"ready\":true}") };
        })) { BaseAddress = new Uri("http://localhost/") })
        {
            await preparation.PollAsync("memories-active-observing", client, "tenant", element => element.GetProperty("ready").GetBoolean(), bound.Token);
            transientRequests.ShouldBe(5); workspace.ShouldBeUnprepared(preparation);
        }
        foreach (HttpStatusCode terminal in new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError })
        {
            int requests = 0;
            using var client = new HttpClient(new NativeReadinessResponseHandler(_ =>
            { requests++; return new HttpResponseMessage(terminal); })) { BaseAddress = new Uri("http://localhost/") };
            var failure = await Should.ThrowAsync<InvalidOperationException>(() => preparation.PollAsync("memories-active-observing", client, "tenant", _ => false, bound.Token));
            failure.Message.ShouldBe("The real owner readiness observation failed: " + (int)terminal);
            requests.ShouldBe(1); workspace.ShouldBeUnprepared(preparation);
        }
        int cancelledRequests = 0;
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(bound.Token);
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string progressPath = Path.Combine(workspace.Catalog, "preparation-progress.json");
        File.Delete(progressPath);
        using (var client = new HttpClient(new NativeReadinessResponseHandler(_ =>
        {
            cancelledRequests++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(1));
            observed.TrySetResult(); return response;
        })) { BaseAddress = new Uri("http://localhost/") })
        {
            Task polling = preparation.PollAsync("memories-active-observing", client, "tenant", _ => false, cancelled.Token);
            await observed.Task.WaitAsync(bound.Token);
            while (!File.Exists(progressPath)) await Task.Delay(1, bound.Token);
            using (JsonDocument progress = JsonDocument.Parse(File.ReadAllBytes(progressPath)))
                progress.RootElement.GetProperty("httpStatus").GetInt32().ShouldBe(429);
            polling.IsCompleted.ShouldBeFalse();
            await cancelled.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => polling.WaitAsync(TimeSpan.FromSeconds(2), bound.Token));
            polling.IsCanceled.ShouldBeTrue();
            cancelledRequests.ShouldBe(1); workspace.ShouldBeUnprepared(preparation);
        }
        using var deadline = new PreparationWorkspace(null);
        int deadlineRequests = 0;
        using (var client = new HttpClient(new NativeReadinessResponseHandler(_ =>
        {
            deadlineRequests++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(1));
            return response;
        })) { BaseAddress = new Uri("http://localhost/") })
        {
            // All workspace, ports, signing key and client setup precedes this one authoritative test budget.
            PlatformNativeTestPreparation deadlinePreparation = deadline.PreparationWithDeadline(DateTimeOffset.UtcNow.AddSeconds(1));
            Task polling = deadlinePreparation.PollAsync("memories-active-observing", client, "tenant", _ => false, bound.Token);
            Exception failure = await CaptureAsync(polling);
            failure.ShouldBeAssignableTo<OperationCanceledException>().Message.ShouldContain("startup deadline");
            polling.IsCanceled.ShouldBeTrue();
            bound.IsCancellationRequested.ShouldBeFalse();
            deadlineRequests.ShouldBe(1); deadline.ShouldBeUnprepared(deadlinePreparation);
        }
        using var expired = new PreparationWorkspace(null);
        int expiredRequests = 0;
        using (var client = new HttpClient(new NativeReadinessResponseHandler(_ =>
        { expiredRequests++; throw new InvalidOperationException("An elapsed startup must not send another request."); })) { BaseAddress = new Uri("http://localhost/") })
        {
            PlatformNativeTestPreparation expiredPreparation = expired.PreparationWithDeadline(DateTimeOffset.UtcNow.AddSeconds(-1));
            Task polling = expiredPreparation.PollAsync("memories-active-observing", client, "tenant", _ => false, bound.Token);
            Exception failure = await CaptureAsync(polling);
            failure.ShouldBeAssignableTo<OperationCanceledException>().Message.ShouldContain("startup deadline");
            polling.IsCanceled.ShouldBeTrue(); expiredRequests.ShouldBe(0);
            expired.ShouldBeUnprepared(expiredPreparation);
        }
        using var nullDeadline = new PreparationWorkspace(null);
        using var nullCancellation = CancellationTokenSource.CreateLinkedTokenSource(bound.Token);
        int nullRequests = 0;
        string nullProgress = Path.Combine(nullDeadline.Catalog, "preparation-progress.json");
        var nullObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var client = new HttpClient(new NativeReadinessResponseHandler(_ =>
        {
            nullRequests++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            // A valid HTTP delta that exceeds Task.Delay's supported range, not malformed input.
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromDays(60));
            nullObserved.TrySetResult(); return response;
        })) { BaseAddress = new Uri("http://localhost/") })
        {
            PlatformNativeTestPreparation nullPreparation = nullDeadline.PreparationWithDeadline(null);
            Task polling = nullPreparation.PollAsync("memories-active-observing", client, "tenant", _ => false, nullCancellation.Token);
            await nullObserved.Task.WaitAsync(bound.Token);
            while (!File.Exists(nullProgress)) await Task.Delay(1, bound.Token);
            polling.IsCompleted.ShouldBeFalse("The clock's default600 budget must cap a valid huge delay without a range fault.");
            await nullCancellation.CancelAsync();
            Exception failure = await CaptureAsync(polling.WaitAsync(TimeSpan.FromSeconds(2), bound.Token));
            failure.ShouldBeAssignableTo<OperationCanceledException>();
            polling.IsCanceled.ShouldBeTrue(); nullRequests.ShouldBe(1);
            nullDeadline.ShouldBeUnprepared(nullPreparation);
        }
        using var late = new PreparationWorkspace(null);
        int lateRequests = 0, latePredicates = 0;
        var lateObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var client = new HttpClient(new NativeReadinessResponseHandler((_, cancellation) =>
        {
            lateRequests++; lateObserved.TrySetResult();
            return held.Task.WaitAsync(cancellation);
        })) { BaseAddress = new Uri("http://localhost/") })
        {
            DateTimeOffset limit = DateTimeOffset.UtcNow.AddSeconds(1);
            PlatformNativeTestPreparation latePreparation = late.PreparationWithDeadline(limit);
            Task polling = latePreparation.PollAsync("memories-active-observing", client, "tenant", _ => { latePredicates++; return true; }, bound.Token);
            try
            {
                await lateObserved.Task.WaitAsync(bound.Token);
                await Task.Delay(CompositionStartupClock.Remaining(limit, DateTimeOffset.UtcNow) + TimeSpan.FromMilliseconds(30), bound.Token);
                held.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ready\":true}") });
                Exception failure = await CaptureAsync(polling);
                failure.ShouldBeAssignableTo<OperationCanceledException>().Message.ShouldContain("startup deadline");
                bound.IsCancellationRequested.ShouldBeFalse(); polling.IsCanceled.ShouldBeTrue();
                lateRequests.ShouldBe(1); latePredicates.ShouldBe(0);
                late.ShouldBeUnprepared(latePreparation);
            }
            finally { held.TrySetCanceled(); }
        }
    }

    private static CancellationTokenSource Bound()
    {
        // ReadRequiredAsync waits for missing owner files; a regression must fail, not hang.
        var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        return bound;
    }

    private static async Task<Exception> CaptureAsync(Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch (Exception exception) { return exception; }
        throw new ShouldAssertException("Native owner preparation completed without a network-backed application model.");
    }

    private static async Task<IReadOnlyList<string>?> ReadBuildsSecretsAsync(CompositionRunPlan plan, CompositionReadiness readiness)
    {
        // The Builds consumer is internal to its tooling assembly; bind it by name so a signature change fails here.
        MethodInfo reader = typeof(CompositionReadiness).Assembly
            .GetType("Hexalith.Builds.Tooling.Runtime.CompositionEndpointCredentials", throwOnError: true)!
            .GetMethod("ReadSecretsAsync", BindingFlags.NonPublic | BindingFlags.Static, [typeof(CompositionRunPlan), typeof(CompositionReadiness), typeof(CancellationToken)])
            ?? throw new MissingMethodException("CompositionEndpointCredentials", "ReadSecretsAsync");
        return await ((Task<IReadOnlyList<string>?>)reader.Invoke(null, [plan, readiness, TestContext.Current.CancellationToken])!).ConfigureAwait(false);
    }

    /// <summary>Owns one temporary run workspace with the actual owner source files native preparation reads first.</summary>
    private sealed class PreparationWorkspace : IDisposable
    {
        private readonly string _signingKey = CompositionSigningKey.Create();

        public PreparationWorkspace(DateTimeOffset? startupDeadlineUtc)
        {
            Root = Path.Combine(Path.GetTempPath(), "platform-native-preparation-" + Guid.NewGuid().ToString("N"));
            CompositionWorkspace.CreatePrivateDirectory(Root);
            Catalog = Path.Combine(Root, "platform-components", "catalog");
            CompositionWorkspace.CreatePrivateDirectory(Catalog);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Plan = CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), Root, Root, CompositionPortAllocator.Allocate(1), [], [],
                platformDeclarationPaths: [Path.Combine(Root, "parties.json")], platformActiveModule: "parties") with
            {
                SelectedTest = new("parties", "platform-source", "dotnet", "tests/native", new string('a', 64), new string('b', 64), 120, 10, now, now.AddMinutes(2), now.AddMinutes(2), "source"),
                StartupDeadlineUtc = startupDeadlineUtc,
            };
            CredentialPath = Path.Combine(Root, NativeTestHandoff.EndpointCredentialsFileName);
            Scope = new(Plan.RunId, "parties", TenantsCatalog.Value.Root, ["party"], [Plan.TenantNamespace]);
        }

        public string Root { get; }

        public string Catalog { get; }

        public CompositionRunPlan Plan { get; }

        public string CredentialPath { get; }

        /// <summary>Gets a scope every source check accepts.</summary>
        public CompositionNativeTestScope Scope { get; }

        // Neither delegated Tenants transport nor the Memories binding is reached before the network boundary.
        public PlatformNativeTestPreparation Preparation(string? catalogDirectory = null) => CreatePreparation(Plan, catalogDirectory ?? Catalog);

        /// <summary>Latches only the test's immutable deadline after workspace/key/client setup, without a production clock or timer seam.</summary>
        public PlatformNativeTestPreparation PreparationWithDeadline(DateTimeOffset? deadline) => CreatePreparation(Plan with { StartupDeadlineUtc = deadline }, Catalog);

        private PlatformNativeTestPreparation CreatePreparation(CompositionRunPlan selected, string directory) => new(selected, directory, _signingKey,
            new PlatformTenantBootstrapAccess("tenants", "tenants", "unused-bootstrap-key", null, "unused-issuer"), null!, new Dictionary<string, string> { ["fixture-tenants"] = "tenants", ["eventstore"] = "eventstore" });

        public void WriteSource(CompositionNativeTestScope scope)
        {
            File.WriteAllBytes(Path.Combine(Catalog, "catalog.json"), TenantsCatalog.Value.Bytes);
            var envelope = RouteCatalogCodec.Decode(TenantsCatalog.Value.Bytes, TenantsCatalog.Value.Root);
            var identity = new RouteCatalogHostIdentity("fixture-tenants", "tenants", new string('a', 64), new string('b', 64), new string('c', 64));
            var gateway = identity with { HostId = "eventstore", AppId = "eventstore" };
            var readback = new RouteCatalogGenerationReadback(1, envelope.Generation, TenantsCatalog.Value.Root,
                envelope.RouteFacetDigest, envelope.IdempotencyFacetDigest, "Development", [gateway, identity], true);
#if HEXALITH_PLATFORM_V2_CATALOG
            // This is structural unit evidence only; it never establishes an actual observed receiver or active runtime.
            var activation = new RouteCatalogActivation(Plan.AttemptId, TenantsCatalog.Value.Bytes, TenantsCatalog.Value.Root, null,
                [gateway, identity], "Development-fixture", "")
            {
                TechnicalHttpReceivers = [new("eventstore", RouteCatalogTechnicalHttpPolicy.FenceVersion,
                    [new("/unit/structural", ["GET"], "unit-file-proof-not-runtime-acceptance", new string('a', 64), new string('a', 64),
                        ["EventStoreHuman"], new string('a', 64), new string('a', 64))])],
            };
            string binding = Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogActivationBinding.ComputeDigest(activation);
            activation = activation with { TrustProof = binding };
            readback = readback with { AttemptId = Plan.AttemptId, ActivationBindingDigest = binding };
            File.WriteAllBytes(Path.Combine(Catalog, "activation.json"), JsonSerializer.SerializeToUtf8Bytes(activation));
#endif
            File.WriteAllBytes(Path.Combine(Catalog, "fixture-tenants.committed.json"), JsonSerializer.SerializeToUtf8Bytes(readback));
            File.WriteAllBytes(Path.Combine(Catalog, "eventstore.committed.json"), JsonSerializer.SerializeToUtf8Bytes(readback));
            File.WriteAllText(Path.Combine(Catalog, "selected-closure.json"), """{"hosts":[{"ResourceId":"eventstore","AppId":"eventstore"},{"ResourceId":"fixture-tenants","AppId":"tenants"}]}""");
            // Publish the scope last and atomically: a waiting owner may poll for it concurrently.
            string staged = Path.Combine(Catalog, "native-scope.staged");
            File.WriteAllBytes(staged, JsonSerializer.SerializeToUtf8Bytes(scope));
            File.Move(staged, Path.Combine(Catalog, "native-scope.json"));
        }

        public void WriteStaleCredentials()
        {
            File.WriteAllText(CredentialPath, "{}");
            File.WriteAllText(CredentialPath + ".tmp", "{}");
        }

        public void ShouldBeUnprepared(PlatformNativeTestPreparation preparation)
        {
            preparation.NativeTestScope.ShouldBeNull();
            preparation.NativeHttpEndpoints.ShouldBeEmpty();
            preparation.NativeEndpointCredentialsPath.ShouldBeNull();
            File.Exists(CredentialPath).ShouldBeFalse();
            File.Exists(CredentialPath + ".tmp").ShouldBeFalse();
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
