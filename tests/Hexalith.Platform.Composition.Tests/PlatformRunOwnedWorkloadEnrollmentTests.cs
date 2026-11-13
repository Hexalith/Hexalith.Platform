using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.Platform.Composition;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Shouldly;

namespace Hexalith.Platform.Composition.Tests;

/// <summary>Checks final-cohort construction and fail-closed disposable owner selection.</summary>
[Collection("Platform file entry")]
public sealed class PlatformRunOwnedWorkloadEnrollmentTests
{
    /// <summary>Physical replicas stay present, and proof workers retain independent actual caller identities.</summary>
    [Fact]
    public void CompleteInventoryUsesActualIdentitiesAndNormalizedStorageTenants()
    {
        IDistributedApplicationBuilder builder = Builder();
        JsonElement inventory = PlatformWorkloadEnrollmentInventory.Create(builder, "selected-run-tenant");
        JsonElement[] clients = [.. inventory.GetProperty("clients").EnumerateArray()];
        clients.Length.ShouldBe(8);
        clients.Select(client => client.GetProperty("workload").GetString()).Distinct().Count().ShouldBe(6);
        string Caller(string name) => clients.Single(client => client.GetProperty("resource").GetString() == name).GetProperty("workload").GetString()!;
        Caller("folders").ShouldBe(Caller("folders-replica"));
        Caller("folders-workers").ShouldBe(Caller("folders-workers-replica"));
        new[] { Caller("folders-workers"), Caller("folders-workers-proof-a"), Caller("folders-workers-proof-b") }.Distinct().Count().ShouldBe(3);
        foreach (JsonElement client in clients)
            foreach (JsonElement probe in client.GetProperty("probes").EnumerateArray())
            {
                string operation = probe.GetProperty("operation").GetString()!;
                string audience = probe.GetProperty("audience").GetString()!;
                if (operation.StartsWith("eventstore:", StringComparison.Ordinal)) audience.ShouldBe(Caller("eventstore"));
                else if (probe.GetProperty("bindings").TryGetProperty("eventstore:bound-domain", out JsonElement domain))
                    audience.ShouldBe(Caller(domain.GetString() == "folders" ? "folders" : "tenants"));
                else new[] { Caller("folders"), Caller("tenants") }.ShouldContain(audience);
            }
        JsonElement[] queries = [.. clients.Single(client => client.GetProperty("resource").GetString() == "eventstore").GetProperty("probes").EnumerateArray()
            .Where(probe => probe.GetProperty("operation").GetString() == "domain-service:query")];
        queries.Length.ShouldBe(3);
        foreach (JsonElement query in queries)
        {
            JsonElement binding = query.GetProperty("bindings");
            binding.GetProperty("eventstore:bound-tenant").GetString().ShouldBe(binding.GetProperty("eventstore:bound-domain").GetString() == "folders" ? "selected-run-tenant" : "system");
            binding.GetProperty("eventstore:bound-query-authority-v1").GetString()!.Length.ShouldBe(64);
        }
    }

    /// <summary>Missing or changed source, bootstrap, callback, query and tenant entries refuse.</summary>
    /// <param name="fault">The isolated inventory change.</param>
    [Theory]
    [InlineData("source")]
    [InlineData("callback")]
    [InlineData("bootstrap")]
    [InlineData("query")]
    [InlineData("storage-tenant")]
    [InlineData("global-administrators-storage-tenant")]
    [InlineData("selected-tenant")]
    [InlineData("audience")]
    public void ChangedInventoryRefuses(string fault)
    {
        IDistributedApplicationBuilder builder = Builder();
        JsonObject inventory = JsonNode.Parse(PlatformWorkloadEnrollmentInventory.Create(builder, "selected-run-tenant").GetRawText())!.AsObject();
        JsonArray clients = inventory["clients"]!.AsArray();
        JsonArray Probes(string name) => clients.Single(client => client!["resource"]!.GetValue<string>() == name)!["probes"]!.AsArray();
        if (fault == "source") Probes("folders-workers-proof-a").Clear();
        if (fault == "callback") Probes("eventstore").RemoveAt(0);
        if (fault == "bootstrap") Probes("tenants").RemoveAt(0);
        if (fault == "query") Probes("eventstore").RemoveAt(8);
        if (fault == "audience") Probes("eventstore")[0]!["audience"] = "folders";
        if (fault == "storage-tenant") Probes("eventstore")[9]!["bindings"]!["eventstore:bound-tenant"] = "selected-run-tenant";
        if (fault == "global-administrators-storage-tenant") Probes("eventstore")[10]!["bindings"]!["eventstore:bound-tenant"] = "selected-run-tenant";
        using JsonDocument document = JsonDocument.Parse(inventory.ToJsonString());
        Should.Throw<InvalidOperationException>(() => PlatformWorkloadEnrollmentInventory.Validate(PlatformWorkloadEnrollmentInventory.Cohort(builder),
            [.. document.RootElement.GetProperty("clients").EnumerateArray()], fault == "selected-tenant" ? "other" : "selected-run-tenant"));
    }

    /// <summary>The absence of generated context leaves protected external enrollment available.</summary>
    [Fact]
    public async Task ExternalOwnerContextIsPreservedWhenLauncherIsAbsent()
    {
        using EnvironmentScope environment = new(new Dictionary<string, string?>
        {
            [PlatformRunOwnedWorkloadEnrollment.ReceiptEnvironmentVariable] = null,
            [PlatformRunOwnedWorkloadEnrollment.CapabilityEnvironmentVariable] = null,
        });
        IDistributedApplicationBuilder builder = Builder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestPath"] = "/external/owner" });
        (await PlatformRunOwnedWorkloadEnrollment.AdoptAsync(builder, "selected-run-tenant", TestContext.Current.CancellationToken)).ShouldBeNull();
        builder.Configuration[PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestPath"].ShouldBe("/external/owner");
    }

    /// <summary>A generated context cannot be combined with protected external references.</summary>
    [Fact]
    public async Task MixedOwnerContextRefusesBeforeReadingArtifacts()
    {
        using EnvironmentScope environment = new(new Dictionary<string, string?>
        {
            [PlatformRunOwnedWorkloadEnrollment.ReceiptEnvironmentVariable] = "/foreign/receipt.json",
            [PlatformRunOwnedWorkloadEnrollment.CapabilityEnvironmentVariable] = new string('A', 64),
        });
        IDistributedApplicationBuilder builder = Builder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestSha256"] = new string('A', 64) });
        (await Should.ThrowAsync<InvalidOperationException>(() => PlatformRunOwnedWorkloadEnrollment.AdoptAsync(builder, "selected-run-tenant", TestContext.Current.CancellationToken)))
            .Message.ShouldContain("mixed-owner-contexts");
    }

    /// <summary>Hosted execution never adopts a disposable generated authority.</summary>
    [Fact]
    public async Task ProductionModelRefusesGeneratedContext()
    {
        using EnvironmentScope environment = new(new Dictionary<string, string?>
        {
            [PlatformRunOwnedWorkloadEnrollment.ReceiptEnvironmentVariable] = "/foreign/receipt.json",
            [PlatformRunOwnedWorkloadEnrollment.CapabilityEnvironmentVariable] = new string('A', 64),
        });
        IDistributedApplicationBuilder builder = Builder();
        builder.Environment.EnvironmentName = "Production";
        (await Should.ThrowAsync<InvalidOperationException>(() => PlatformRunOwnedWorkloadEnrollment.AdoptAsync(builder, "selected-run-tenant", TestContext.Current.CancellationToken)))
            .Message.ShouldContain("disposable-development-run-required");
    }

    /// <summary>Missing and unreadable expected launch trust refuse by the named prelaunch requirement.</summary>
    /// <param name="fault">The unavailable trust-file state.</param>
    [Theory]
    [InlineData("missing")]
    [InlineData("unreadable")]
    public async Task LaunchTrustReadFailuresHaveNamedPrecondition(string fault)
    {
        string directory = Path.Combine(Path.GetTempPath(), "run-owned-trust-control-" + Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        try
        {
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            string trustFile = Path.Combine(directory, "trust.pem"), receiptFile = Path.Combine(directory, "receipt.json");
            MethodInfo identity = typeof(PlatformRunOwnedWorkloadEnrollment).GetMethod("ProcessIdentity", BindingFlags.NonPublic | BindingFlags.Static)!;
            int ownerPid = (int)typeof(PlatformRunOwnedWorkloadEnrollment).GetMethod("ParentProcess", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [Environment.ProcessId])!;
            string capability = new('B', 64);
            byte[] receipt = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema = "hexalith.run-owned-workload/v1", owner = "disposable-folders-acceptance", directory, capability,
                childPid = Environment.ProcessId, childIdentity = (string)identity.Invoke(null, [Environment.ProcessId])!,
                ownerPid, ownerIdentity = (string)identity.Invoke(null, [ownerPid])!, runId = new string('A', 32),
                deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60, trustFile, trustSha256 = new string('A', 64),
            });
            await File.WriteAllBytesAsync(receiptFile, receipt, TestContext.Current.CancellationToken);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(receiptFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            // Opening a directory as a file is denied even for root, making the read failure deterministic.
            if (fault == "unreadable")
            {
                Directory.CreateDirectory(trustFile);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(trustFile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            using EnvironmentScope environment = new(new Dictionary<string, string?>
            {
                [PlatformRunOwnedWorkloadEnrollment.ReceiptEnvironmentVariable] = receiptFile,
                [PlatformRunOwnedWorkloadEnrollment.CapabilityEnvironmentVariable] = capability,
                ["SSL_CERT_FILE"] = trustFile,
            });
            (await Should.ThrowAsync<InvalidOperationException>(() => PlatformRunOwnedWorkloadEnrollment.AdoptAsync(Builder(), "selected-run-tenant", TestContext.Current.CancellationToken)))
                .Message.ShouldContain("missing-prelaunch-trust");
            File.Exists(Path.Combine(directory, "request.json")).ShouldBeFalse();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>Producer artifacts that differ from the receipt, the final cohort or the private run directory refuse before activation.</summary>
    /// <param name="fault">The isolated trust or producer fault.</param>
    /// <param name="code">The named refusal.</param>
    [Theory]
    [InlineData("trust-digest", "missing-prelaunch-trust")]
    [InlineData("host-trust", "host-trust-must-merge-launch-trust")]
    [InlineData("result", "foreign-result-or-changed-cohort")]
    [InlineData("manifest-digest", "manifest-binding")]
    [InlineData("issuer", "disposable-loopback-authority-required")]
    [InlineData("cohort", "changed-cohort")]
    [InlineData("probes", "changed-cohort-or-tenant")]
    [InlineData("secret", "foreign-secret-reference")]
    [InlineData("final-cohort", "changed-final-cohort")]
    public async Task ForeignProducerArtifactsRefuseBeforeActivation(string fault, string code)
    {
        string directory = Path.Combine(Path.GetTempPath(), "run-owned-producer-control-" + Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        try
        {
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            string trustFile = Path.Combine(directory, "trust.pem"), receiptFile = Path.Combine(directory, "receipt.json");
            byte[] trust = "-----BEGIN CERTIFICATE-----\nrun-owned-control\n-----END CERTIFICATE-----\n"u8.ToArray();
            await WritePrivateAsync(trustFile, trust);
            MethodInfo identity = typeof(PlatformRunOwnedWorkloadEnrollment).GetMethod("ProcessIdentity", BindingFlags.NonPublic | BindingFlags.Static)!;
            int ownerPid = (int)typeof(PlatformRunOwnedWorkloadEnrollment).GetMethod("ParentProcess", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [Environment.ProcessId])!;
            string capability = new('B', 64), runId = new('A', 32);
            long deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60;
            await WritePrivateAsync(receiptFile, JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema = "hexalith.run-owned-workload/v1", owner = "disposable-folders-acceptance", directory, capability,
                childPid = Environment.ProcessId, childIdentity = (string)identity.Invoke(null, [Environment.ProcessId])!,
                ownerPid, ownerIdentity = (string)identity.Invoke(null, [ownerPid])!, runId, deadline, trustFile,
                trustSha256 = fault == "trust-digest" ? new string('A', 64) : Convert.ToHexString(SHA256.HashData(trust)),
            }));
            using EnvironmentScope environment = new(new Dictionary<string, string?>
            {
                [PlatformRunOwnedWorkloadEnrollment.ReceiptEnvironmentVariable] = receiptFile,
                [PlatformRunOwnedWorkloadEnrollment.CapabilityEnvironmentVariable] = capability,
                ["SSL_CERT_FILE"] = trustFile,
            });
            IDistributedApplicationBuilder builder = Builder();
            using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            limit.CancelAfter(TimeSpan.FromSeconds(30));
            Task<PlatformRunOwnedWorkloadEnrollment?> adoption = PlatformRunOwnedWorkloadEnrollment.AdoptAsync(builder, "selected-run-tenant", limit.Token,
                fault == "host-trust" ? Path.Combine(directory, "missing-host-trust.pem") : null);
            string requestPath = Path.Combine(directory, "request.json");
            if (fault is not ("trust-digest" or "host-trust"))
            {
                // Act as the launcher: answer the published request with one faulty producer artifact.
                while (!File.Exists(requestPath) && !adoption.IsCompleted) await Task.Delay(10, limit.Token);
                JsonObject request = JsonNode.Parse(await File.ReadAllBytesAsync(requestPath, limit.Token))!.AsObject();
                string secretFile = Path.Combine(directory, "client-control.secret");
                await WritePrivateAsync(secretFile, "control-secret"u8.ToArray());
                var clients = new JsonArray();
                foreach (JsonNode? client in request["inventory"]!["clients"]!.AsArray())
                {
                    JsonObject entry = client!.DeepClone().AsObject();
                    entry["secretFile"] = fault == "secret" ? Path.Combine(Path.GetTempPath(), "client-foreign.secret") : secretFile;
                    clients.Add(entry);
                }
                if (fault == "cohort") clients.RemoveAt(clients.Count - 1);
                if (fault == "probes") clients[0]!["probes"]![0]!["revision"] = 2;
                string issuer = (fault == "issuer" ? "https://authority.example:8443" : "https://localhost:8443") + "/realms/run-owned";
                var manifestDocument = new JsonObject
                {
                    ["schema"] = "hexalith.workload-enrollment/v1", ["authority"] = issuer, ["issuer"] = issuer, ["tokenEndpoint"] = issuer + "/protocol/openid-connect/token",
                    ["notBefore"] = deadline - 60, ["expiresAt"] = deadline, ["signingKeyId"] = "control", ["signingPublicKeySha256"] = new string('C', 64),
                    ["receivers"] = request["inventory"]!["receivers"]!.DeepClone(), ["clients"] = clients,
                };
                byte[] manifest = System.Text.Encoding.UTF8.GetBytes(manifestDocument.ToJsonString());
                string manifestPath = Path.Combine(directory, "manifest.json");
                await WritePrivateAsync(manifestPath, manifest);
                if (fault == "final-cohort")
                    builder.AddProject("folders-workers-late", Path.Combine(PlatformTestRepository.Root, "src/Hexalith.Platform.EventStoreHost/Hexalith.Platform.EventStoreHost.csproj"))
                        .WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions { AppId = "folders-workers-late-actual-run" }));
                await WritePrivateAsync(Path.Combine(directory, "result.json"), JsonSerializer.SerializeToUtf8Bytes(new
                {
                    runId = fault == "result" ? new string('F', 32) : runId, deadline, inventorySha256 = request["inventorySha256"]!.GetValue<string>(), manifestPath,
                    manifestSha256 = fault == "manifest-digest" ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(manifest)),
                }));
                // A refused adopter releases its lease; acknowledge it as the launcher does, by removing private material.
                string releasePath = Path.Combine(directory, "release.json");
                while (!File.Exists(releasePath) && !adoption.IsCompleted) await Task.Delay(10, limit.Token);
                File.Exists(releasePath).ShouldBeTrue("A refused adopter publishes its owned release.");
                Directory.Delete(directory, recursive: true);
            }
            (await Should.ThrowAsync<InvalidOperationException>(() => adoption)).Message.ShouldContain(code);
            builder.Configuration[PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestPath"].ShouldBeNull();
            if (fault is "trust-digest" or "host-trust") File.Exists(requestPath).ShouldBeFalse();
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    private static async Task WritePrivateAsync(string path, byte[] bytes)
    {
        string temporary = path + ".writing";
        await File.WriteAllBytesAsync(temporary, bytes, TestContext.Current.CancellationToken);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(temporary, path);
    }

    /// <summary>A failed publish or foreign release keeps refusing until this owner's cleanup is acknowledged.</summary>
    /// <param name="fault">The failed disposal stage.</param>
    [Theory]
    [InlineData("publish")]
    [InlineData("foreign-release")]
    public async Task CleanupRetriesRemainIncompleteUntilOwnedDirectoryIsAbsent(string fault)
    {
        string directory = Path.Combine(Path.GetTempPath(), "run-owned-cleanup-retry-" + Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        try
        {
            ConstructorInfo constructor = typeof(PlatformRunOwnedWorkloadEnrollment).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance,
                null, [typeof(string), typeof(string), typeof(string)], null)!;
            var enrollment = (PlatformRunOwnedWorkloadEnrollment)constructor.Invoke([directory, new string('A', 32), new string('B', 64)]);
            string releasePath = Path.Combine(directory, "release.json");
            if (fault == "publish") Directory.CreateDirectory(releasePath);
            else
            {
                await File.WriteAllTextAsync(releasePath, "{\"runId\":\"foreign\",\"capability\":\"foreign\"}", TestContext.Current.CancellationToken);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(releasePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            for (int attempt = 0; attempt < 2; attempt++)
            {
                (await Should.ThrowAsync<InvalidOperationException>(() => enrollment.DisposeAsync().AsTask()))
                    .Message.ShouldContain("owned-cleanup-incomplete");
                Directory.Exists(directory).ShouldBeTrue();
            }
            if (fault == "publish") Directory.Delete(releasePath);
            else File.Delete(releasePath);
            Task cleanup = enrollment.DisposeAsync().AsTask();
            using CancellationTokenSource publication = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            publication.CancelAfter(TimeSpan.FromSeconds(2));
            while (!File.Exists(releasePath) && !cleanup.IsCompleted) await Task.Delay(10, publication.Token);
            File.Exists(releasePath).ShouldBeTrue("A retry publishes or reuses the owner's release request.");
            byte[] published = await File.ReadAllBytesAsync(releasePath, TestContext.Current.CancellationToken);
            Task concurrentCleanup = enrollment.DisposeAsync().AsTask();
            await Task.Delay(150, TestContext.Current.CancellationToken);
            cleanup.IsCompleted.ShouldBeFalse("Cleanup waits until the owner removes private material.");
            (await File.ReadAllBytesAsync(releasePath, TestContext.Current.CancellationToken)).AsSpan().SequenceEqual(published).ShouldBeTrue();
            Directory.Delete(directory, recursive: true);
            await Task.WhenAll(cleanup, concurrentCleanup).WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            await enrollment.DisposeAsync();
            await enrollment.DisposeAsync();
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    /// <summary>The fresh host applies the consumer's complete signed grant, binding and validity checks.</summary>
    /// <param name="fault">The otherwise correctly signed token mutation.</param>
    [Theory]
    [InlineData("none")]
    [InlineData("grant")]
    [InlineData("revision")]
    [InlineData("surface")]
    [InlineData("binding")]
    [InlineData("extra-binding")]
    [InlineData("issued")]
    [InlineData("not-before")]
    [InlineData("lifetime")]
    public void FreshHostUsesCompleteConsumerTokenVerification(string fault)
    {
        using RSA signer = RSA.Create(2048);
        JsonElement inventory = PlatformWorkloadEnrollmentInventory.Create(Builder(), "selected-run-tenant");
        JsonElement client = inventory.GetProperty("clients").EnumerateArray().Single(value => value.GetProperty("resource").GetString() == "folders-workers-proof-a");
        JsonElement probe = client.GetProperty("probes")[0];
        string workload = client.GetProperty("workload").GetString()!;
        const string issuer = "https://localhost/realms/run-owned", kid = "approved-signer";
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        JsonObject payload = new()
        {
            ["iss"] = issuer, ["azp"] = workload, ["aud"] = probe.GetProperty("audience").GetString(),
            ["iat"] = now - 1, ["nbf"] = now - 1, ["exp"] = now + 60,
            ["eventstore:operation"] = probe.GetProperty("operation").GetString(), ["eventstore:surface"] = "service",
            ["eventstore:grant-id"] = probe.GetProperty("grantId").GetString(), ["eventstore:grant-revision"] = probe.GetProperty("revision").GetInt64(),
        };
        foreach (JsonProperty binding in probe.GetProperty("bindings").EnumerateObject()) payload[binding.Name] = binding.Value.GetString();
        if (fault == "grant") payload["eventstore:grant-id"] = "foreign-grant";
        if (fault == "revision") payload["eventstore:grant-revision"] = 2;
        if (fault == "surface") payload["eventstore:surface"] = "human";
        if (fault == "binding") payload["eventstore:bound-tenant"] = "foreign-tenant";
        if (fault == "extra-binding") payload["eventstore:bound-extra"] = "extra";
        if (fault == "issued") payload["iat"] = now + 60;
        if (fault == "not-before") payload["nbf"] = now + 60;
        if (fault == "lifetime") payload["exp"] = now + 121;
        static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string signed = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", kid })) + "." + Encode(JsonSerializer.SerializeToUtf8Bytes(payload));
        string token = signed + "." + Encode(signer.SignData(System.Text.Encoding.ASCII.GetBytes(signed), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        if (fault == "none") VerifyHostToken(token, signer, kid, issuer, workload, probe, now + 300);
        else Should.Throw<TargetInvocationException>(() => VerifyHostToken(token, signer, kid, issuer, workload, probe, now + 300))
            .InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    /// <summary>Exercises the real producer bridge and the unchanged live consumer under prelaunch trust.</summary>
    [Fact(Explicit = true)]
    public async Task RunOwnedAuthorityPassesUnchangedConsumerAndNegativeControls()
    {
        if (Environment.GetEnvironmentVariable("HEXALITH_RUN_OWNED_WORKLOAD_HOST_PROBE") == "true")
        {
            await RunningHostProbeAsync();
            return;
        }
        if (Environment.GetEnvironmentVariable("HEXALITH_RUN_OWNED_WORKLOAD_COMPONENT") != "true")
            throw new InvalidOperationException("Run verify_component.py --run-owned --platform-assembly <Debug assembly>.");
        if (Environment.GetEnvironmentVariable("HEXALITH_RUN_OWNED_WORKLOAD_BRIDGE_CANCELLATION") == "true")
        {
            await CancelBridgeAfterRequestAsync();
            return;
        }
        IDistributedApplicationBuilder builder = Builder();
        const string tenant = "selected-live-tenant";
        string trust = Environment.GetEnvironmentVariable("SSL_CERT_FILE")!;
        string receiptPath = Environment.GetEnvironmentVariable(PlatformRunOwnedWorkloadEnrollment.ReceiptEnvironmentVariable)!;
        var proofs = new Dictionary<string, string>(StringComparer.Ordinal);
        var publicProofs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (ProjectResource resource in PlatformWorkloadEnrollmentInventory.Cohort(builder))
        {
            string workload = PlatformWorkloadEnrollmentInventory.Workload(resource);
            if (!proofs.ContainsKey(workload))
            {
                using RSA proof = RSA.Create(2048);
                proofs[workload] = Convert.ToBase64String(proof.ExportPkcs8PrivateKey());
                publicProofs[workload] = Convert.ToBase64String(proof.ExportSubjectPublicKeyInfo());
            }
            builder.CreateResourceBuilder(resource)
                .WithEnvironment("Authentication__JwtBearer__Authority", "https://unchanged-human.example.invalid")
                .WithEnvironment("Authentication__JwtBearer__Audience", "unchanged-human-audience")
                .WithEnvironment("Authentication__BoundStreamRead__PrivateKey", proofs[workload])
                .WithEnvironment("Authentication__WorkloadIssuer__EnableResourceBindingRequests", "false")
                .WithEnvironment("UNCHANGED_SETTING", "unchanged");
        }
        ProjectResource gateway = PlatformWorkloadEnrollmentInventory.Cohort(builder).Single(resource => resource.Name == "eventstore");
        foreach ((string workload, string publicKey) in publicProofs)
            builder.CreateResourceBuilder(gateway).WithEnvironment("Authentication__BoundStreamRead__PublicKeys__" + workload, publicKey);
        string[] proofCallers = ["folders-workers", "folders-workers-proof-a", "folders-workers-proof-b"];
        proofCallers.Select(name => proofs[PlatformWorkloadEnrollmentInventory.Workload(builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == name))])
            .Distinct(StringComparer.Ordinal).Count().ShouldBe(3, "The two proof callers use independent actual RSA keys.");
        try
        {
            Environment.SetEnvironmentVariable("SSL_CERT_FILE", null);
            (await Should.ThrowAsync<InvalidOperationException>(() => PlatformRunOwnedWorkloadEnrollment.AdoptAsync(builder, tenant, TestContext.Current.CancellationToken)))
                .Message.ShouldContain("missing-prelaunch-trust");
        }
        finally { Environment.SetEnvironmentVariable("SSL_CERT_FILE", trust); }
        string? capability = Environment.GetEnvironmentVariable(PlatformRunOwnedWorkloadEnrollment.CapabilityEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(PlatformRunOwnedWorkloadEnrollment.CapabilityEnvironmentVariable, new string('0', 64));
            (await Should.ThrowAsync<InvalidOperationException>(() => PlatformRunOwnedWorkloadEnrollment.AdoptAsync(builder, tenant, TestContext.Current.CancellationToken)))
                .Message.ShouldContain("foreign-launcher-receipt");
        }
        finally { Environment.SetEnvironmentVariable(PlatformRunOwnedWorkloadEnrollment.CapabilityEnvironmentVariable, capability); }
        byte[] receiptBytes = await File.ReadAllBytesAsync(receiptPath, TestContext.Current.CancellationToken);
        foreach (string fault in new[] { "expiry", "child", "directory" })
        {
            JsonObject changed = JsonNode.Parse(receiptBytes)!.AsObject();
            if (fault == "expiry") changed["deadline"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1;
            if (fault == "child") changed["childPid"] = Environment.ProcessId + 1;
            if (fault == "directory") changed["directory"] = Path.GetTempPath();
            string reason = fault == "expiry" ? "fixed-owner-deadline-expired-or-invalid" : fault == "child" ? "owner-child-process-binding" : "foreign-launcher-receipt";
            try
            {
                await File.WriteAllTextAsync(receiptPath, changed.ToJsonString(), TestContext.Current.CancellationToken);
                (await Should.ThrowAsync<InvalidOperationException>(() => PlatformRunOwnedWorkloadEnrollment.AdoptAsync(builder, tenant, TestContext.Current.CancellationToken)))
                    .Message.ShouldContain(reason);
                File.Exists(Path.Combine(Path.GetDirectoryName(receiptPath)!, "request.json")).ShouldBeFalse();
            }
            finally { await File.WriteAllBytesAsync(receiptPath, receiptBytes, TestContext.Current.CancellationToken); }
        }
        using JsonDocument receipt = JsonDocument.Parse(receiptBytes);
        using RSA providerRoot = RSA.Create(2048);
        CertificateRequest providerRequest = new("CN=Preserved provider trust", providerRoot, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        providerRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        providerRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        using X509Certificate2 providerCertificate = providerRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.FromUnixTimeSeconds(receipt.RootElement.GetProperty("deadline").GetInt64()));
        string providerTrust = providerCertificate.ExportCertificatePem();
        string hostTrust = Path.Combine(Path.GetDirectoryName(receiptPath)!, "consumer-host-trust.pem");
        await File.WriteAllTextAsync(hostTrust, providerTrust, TestContext.Current.CancellationToken);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(hostTrust, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        (await Should.ThrowAsync<InvalidOperationException>(() => PlatformRunOwnedWorkloadEnrollment.AdoptAsync(builder, tenant, TestContext.Current.CancellationToken, hostTrust)))
            .Message.ShouldContain("host-trust-must-merge-launch-trust");
        await File.WriteAllTextAsync(hostTrust, await File.ReadAllTextAsync(trust, TestContext.Current.CancellationToken) + "\n" + providerTrust, TestContext.Current.CancellationToken);
        Task<PlatformRunOwnedWorkloadEnrollment?> firstAdoption = PlatformRunOwnedWorkloadEnrollment.AdoptAsync(builder, tenant, TestContext.Current.CancellationToken, hostTrust);
        string requestPath = Path.Combine(Path.GetDirectoryName(receiptPath)!, "request.json");
        while (!File.Exists(requestPath) && !firstAdoption.IsCompleted)
            await Task.Delay(20, TestContext.Current.CancellationToken);
        (await Should.ThrowAsync<InvalidOperationException>(() => PlatformRunOwnedWorkloadEnrollment.AdoptAsync(Builder(), tenant, TestContext.Current.CancellationToken, hostTrust)))
            .Message.ShouldContain("owner-request-already-adopted");
        File.Exists(Path.Combine(Path.GetDirectoryName(receiptPath)!, "release.json")).ShouldBeFalse("The refused competitor cannot release the first adoption.");
        await using PlatformRunOwnedWorkloadEnrollment? enrollment = await firstAdoption;
        enrollment.ShouldNotBeNull();
        async Task AssertHostsAsync(bool enabled)
        {
            foreach (ProjectResource resource in PlatformWorkloadEnrollmentInventory.Cohort(builder))
            {
                string workload = PlatformWorkloadEnrollmentInventory.Workload(resource);
                Dictionary<string, object> environment = await EnvironmentAsync(builder, resource);
                environment["SSL_CERT_FILE"].ShouldBe(hostTrust);
                environment["Authentication__WorkloadIssuer__EnableResourceBindingRequests"].ShouldBe(enabled ? "true" : "false");
                environment["Authentication__JwtBearer__Authority"].ShouldBe("https://unchanged-human.example.invalid");
                environment["Authentication__JwtBearer__Audience"].ShouldBe("unchanged-human-audience");
                environment["UNCHANGED_SETTING"].ShouldBe("unchanged");
                (environment["Authentication__BoundStreamRead__PrivateKey"] is string actualProof && actualProof == proofs[workload])
                    .ShouldBeTrue("Independent RSA proof remains unchanged.");
                if (resource.Name == "eventstore")
                    foreach ((string caller, string publicKey) in publicProofs)
                        (environment["Authentication__BoundStreamRead__PublicKeys__" + caller] is string actualPublic && actualPublic == publicKey)
                            .ShouldBeTrue("Public source proof remains unchanged.");
                if (enabled)
                {
                    environment["Authentication__WorkloadJwtBearer__Audience"].ShouldBe(workload);
                    environment["Authentication__WorkloadJwtBearer__RequireHttpsMetadata"].ShouldBe("true");
                    environment["Authentication__WorkloadIssuer__ClientSecret"].ShouldBeOfType<ParameterResource>().Secret.ShouldBeTrue();
                }
                else environment.ShouldNotContainKey("Authentication__WorkloadJwtBearer__Authority");
            }
        }
        await AssertHostsAsync(enabled: false);
        string mergedTrust = await File.ReadAllTextAsync(hostTrust, TestContext.Current.CancellationToken);
        mergedTrust.Contains(await File.ReadAllTextAsync(trust, TestContext.Current.CancellationToken), StringComparison.Ordinal).ShouldBeTrue();
        mergedTrust.Contains(providerTrust, StringComparison.Ordinal).ShouldBeTrue();
        string manifestPath = builder.Configuration[PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestPath"]!;
        byte[] original = await File.ReadAllBytesAsync(manifestPath, TestContext.Current.CancellationToken);
        foreach (string fault in new[] { "probe", "signer", "tls", "permissions", "expiry", "cohort", "tenant" })
        {
            JsonObject manifest = JsonNode.Parse(original)!.AsObject();
            if (fault == "probe") manifest["clients"]![0]!["probes"]![0]!["revision"] = 2;
            if (fault == "signer") manifest["signingPublicKeySha256"] = new string('0', 64);
            if (fault == "permissions") manifest["clients"]![0]!["probes"]!.AsArray().RemoveAt(0);
            if (fault == "expiry") manifest["expiresAt"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1;
            if (fault == "cohort") manifest["receivers"]!.AsArray().RemoveAt(0);
            if (fault == "tls")
                foreach (string field in new[] { "authority", "issuer", "tokenEndpoint" })
                    manifest[field] = manifest[field]!.GetValue<string>().Replace("localhost", "127.0.0.1", StringComparison.Ordinal);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(manifest.ToJsonString());
            await File.WriteAllBytesAsync(manifestPath, bytes, TestContext.Current.CancellationToken);
            builder.Configuration[PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestSha256"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
            await Should.ThrowAsync<InvalidOperationException>(() => PlatformWorkloadEnrollment.ConfigureAsync(builder, TestContext.Current.CancellationToken, fault == "tenant" ? "foreign-tenant" : tenant));
            await AssertHostsAsync(enabled: false);
        }
        await File.WriteAllBytesAsync(manifestPath, original, TestContext.Current.CancellationToken);
        builder.Configuration[PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestSha256"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(original));
        (await PlatformWorkloadEnrollment.ConfigureAsync(builder, TestContext.Current.CancellationToken, tenant)).ShouldBeTrue();
        await AssertHostsAsync(enabled: true);
        using JsonDocument finalManifest = JsonDocument.Parse(original);
        ProjectResource runningHost = PlatformWorkloadEnrollmentInventory.Cohort(builder).Single(resource => resource.Name == "folders-workers-proof-a");
        JsonElement runningClient = finalManifest.RootElement.GetProperty("clients").EnumerateArray().Single(client => client.GetProperty("resource").GetString() == runningHost.Name);
        await VerifyFreshHostAsync(await EnvironmentAsync(builder, runningHost), runningClient.GetProperty("probes")[0], finalManifest.RootElement);
        Console.WriteLine("RUN_OWNED_CONSUMER_CONTROLS resources=8 workloads=6 proofCallers=2 controls=missing-trust,foreign-receipt,receipt-expiry,child,directory,host-trust-merge,probe,signer,tls,permissions,expiry,cohort,tenant assertions=host-trust,profile-before-and-after,human-authentication,independent-rsa-proofs,first-adopter-survives-competitor,fresh-running-host-tls-and-exchange");
    }

    private static async Task CancelBridgeAfterRequestAsync()
    {
        string receiptPath = Environment.GetEnvironmentVariable(PlatformRunOwnedWorkloadEnrollment.ReceiptEnvironmentVariable)!;
        string directory = Path.GetDirectoryName(receiptPath)!;
        using JsonDocument receipt = JsonDocument.Parse(await File.ReadAllBytesAsync(receiptPath, TestContext.Current.CancellationToken));
        IDistributedApplicationBuilder builder = Builder();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task<PlatformRunOwnedWorkloadEnrollment?> adoption = PlatformRunOwnedWorkloadEnrollment.AdoptAsync(builder, "selected-cancellation-tenant", cancellation.Token);
        using CancellationTokenSource observation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        observation.CancelAfter(TimeSpan.FromSeconds(30));
        string requestPath = Path.Combine(directory, "request.json");
        while (!File.Exists(requestPath)) await Task.Delay(10, observation.Token);
        while (!File.Exists(Path.Combine(directory, "bridge-ready.json"))) await Task.Delay(20, observation.Token);
        cancellation.Cancel();
        string releasePath = Path.Combine(directory, "release.json");
        while (!File.Exists(releasePath) && Directory.Exists(directory)) await Task.Delay(10, observation.Token);
        File.Exists(releasePath).ShouldBeTrue("Cancellation after exclusive publication must release the producer before returning.");
        using (JsonDocument release = JsonDocument.Parse(await File.ReadAllBytesAsync(releasePath, observation.Token)))
        {
            (release.RootElement.GetProperty("runId").GetString() == receipt.RootElement.GetProperty("runId").GetString()).ShouldBeTrue("Cancellation releases this run.");
            (release.RootElement.GetProperty("capability").GetString() == receipt.RootElement.GetProperty("capability").GetString()).ShouldBeTrue("Cancellation uses this owner's private capability.");
        }
        string observedPath = Path.Combine(directory, "cancellation-observed.json");
        await File.WriteAllTextAsync(observedPath, "{}", observation.Token);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(observedPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        await Should.ThrowAsync<OperationCanceledException>(async () => await adoption.WaitAsync(TimeSpan.FromSeconds(10), observation.Token));
        Directory.Exists(directory).ShouldBeFalse("The cancelled adopter waits for actual authority and private-material cleanup.");
        builder.Configuration[PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestPath"].ShouldBeNull();
        await Task.Delay(150, observation.Token);
        Console.WriteLine("RUN_OWNED_BRIDGE_CANCELLATION requestPublished=true cancellationObserved=true releasePublished=true childAliveAfterCleanup=true");
    }

    private static async Task VerifyFreshHostAsync(Dictionary<string, object> environment, JsonElement probe, JsonElement manifest)
    {
        long deadline = manifest.GetProperty("expiresAt").GetInt64();
        ProcessStartInfo start = new("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { typeof(PlatformRunOwnedWorkloadEnrollmentTests).Assembly.Location, "-explicit", "on", "-method",
            "Hexalith.Platform.Composition.Tests.PlatformRunOwnedWorkloadEnrollmentTests.RunOwnedAuthorityPassesUnchangedConsumerAndNegativeControls" })
            start.ArgumentList.Add(argument);
        start.Environment.Remove(PlatformRunOwnedWorkloadEnrollment.ReceiptEnvironmentVariable);
        start.Environment.Remove(PlatformRunOwnedWorkloadEnrollment.CapabilityEnvironmentVariable);
        foreach ((string name, object value) in environment)
            start.Environment[name] = value is IValueProvider provider ? await provider.GetValueAsync(TestContext.Current.CancellationToken) : value.ToString();
        start.Environment["HEXALITH_RUN_OWNED_WORKLOAD_HOST_PROBE"] = "true";
        start.Environment["HEXALITH_RUN_OWNED_WORKLOAD_HOST_PROBE_JSON"] = probe.GetRawText();
        start.Environment["HEXALITH_RUN_OWNED_WORKLOAD_HOST_DEADLINE"] = deadline.ToString(System.Globalization.CultureInfo.InvariantCulture);
        start.Environment["HEXALITH_RUN_OWNED_WORKLOAD_HOST_SIGNER_KID"] = manifest.GetProperty("signingKeyId").GetString();
        start.Environment["HEXALITH_RUN_OWNED_WORKLOAD_HOST_SIGNER_SHA256"] = manifest.GetProperty("signingPublicKeySha256").GetString();
        using CancellationTokenSource bounded = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bounded.CancelAfter(TimeSpan.FromSeconds(Math.Max(0, Math.Min(30, deadline - DateTimeOffset.UtcNow.ToUnixTimeSeconds()))));
        using Process child = Process.Start(start) ?? throw new InvalidOperationException("Fresh running-host child did not start.");
        Task<string> output = child.StandardOutput.ReadToEndAsync(bounded.Token), errors = child.StandardError.ReadToEndAsync(bounded.Token);
        try
        {
            await child.WaitForExitAsync(bounded.Token);
            _ = await output;
            _ = await errors;
            child.ExitCode.ShouldBe(0, "Fresh running-host TLS/discovery/exchange failed; protected child output is withheld.");
        }
        finally { if (!child.HasExited) child.Kill(entireProcessTree: true); }
    }

    private static async Task RunningHostProbeAsync()
    {
        long deadline = long.Parse(Environment.GetEnvironmentVariable("HEXALITH_RUN_OWNED_WORKLOAD_HOST_DEADLINE")!, System.Globalization.CultureInfo.InvariantCulture);
        using CancellationTokenSource bounded = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bounded.CancelAfter(TimeSpan.FromSeconds(Math.Max(0, Math.Min(30, deadline - DateTimeOffset.UtcNow.ToUnixTimeSeconds()))));
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        await using WebApplication app = builder.Build();
        app.Urls.Clear();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapGet("/health", () => "ready");
        await app.StartAsync(bounded.Token);
        try
        {
            using HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false });
            if (await http.GetStringAsync(app.Urls.Single() + "/health", bounded.Token) != "ready") throw new InvalidOperationException("Running host readiness refused.");
            string authority = app.Configuration["Authentication:WorkloadJwtBearer:Authority"]!;
            string endpoint = app.Configuration["Authentication:WorkloadIssuer:TokenEndpoint"]!;
            using JsonDocument metadata = JsonDocument.Parse(await http.GetStringAsync(authority + "/.well-known/openid-configuration", bounded.Token));
            if (metadata.RootElement.GetProperty("issuer").GetString() != authority || metadata.RootElement.GetProperty("token_endpoint").GetString() != endpoint)
                throw new InvalidOperationException("Running host discovery refused.");
            string jwksUri = metadata.RootElement.GetProperty("jwks_uri").GetString()!;
            if (new Uri(jwksUri).GetLeftPart(UriPartial.Authority) != new Uri(authority).GetLeftPart(UriPartial.Authority)) throw new InvalidOperationException("Running host discovery origin refused.");
            using JsonDocument jwks = JsonDocument.Parse(await http.GetStringAsync(jwksUri, bounded.Token));
            using JsonDocument requested = JsonDocument.Parse(Environment.GetEnvironmentVariable("HEXALITH_RUN_OWNED_WORKLOAD_HOST_PROBE_JSON")!);
            JsonElement probe = requested.RootElement;
            string audience = probe.GetProperty("audience").GetString()!, operation = probe.GetProperty("operation").GetString()!;
            using FormUrlEncodedContent form = new(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials", ["client_id"] = app.Configuration["Authentication:WorkloadIssuer:ClientId"]!,
                ["client_secret"] = app.Configuration["Authentication:WorkloadIssuer:ClientSecret"]!,
                ["scope"] = "eventstore-audience." + audience + " eventstore-operation." + operation.Replace(':', '.'),
                ["authorization_details"] = JsonSerializer.Serialize(new[] { new { type = "https://hexalith.com/security/workload-binding/v1", audience, operation, bindings = probe.GetProperty("bindings") } }),
            });
            using HttpResponseMessage response = await http.PostAsync(endpoint, form, bounded.Token);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Running host workload exchange refused.");
            using JsonDocument issued = JsonDocument.Parse(await response.Content.ReadAsStringAsync(bounded.Token));
            static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
            string kid = Environment.GetEnvironmentVariable("HEXALITH_RUN_OWNED_WORKLOAD_HOST_SIGNER_KID")!;
            JsonElement signer = jwks.RootElement.GetProperty("keys").EnumerateArray().Single(key => key.GetProperty("kid").GetString() == kid);
            if (signer.GetProperty("kty").GetString() != "RSA" || signer.GetProperty("alg").GetString() != "RS256" || signer.GetProperty("use").GetString() != "sig")
                throw new InvalidOperationException("Running host signer refused.");
            using RSA rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters { Modulus = Decode(signer.GetProperty("n").GetString()!), Exponent = Decode(signer.GetProperty("e").GetString()!) });
            if (rsa.KeySize < 2048 || Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo())) != Environment.GetEnvironmentVariable("HEXALITH_RUN_OWNED_WORKLOAD_HOST_SIGNER_SHA256"))
                throw new InvalidOperationException("Running host signer binding refused.");
            using X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(signer.GetProperty("x5c")[0].GetString()!));
            using RSA? certifiedSigner = certificate.GetRSAPublicKey();
            if (certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow
                || certifiedSigner is null || !certifiedSigner.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(rsa.ExportSubjectPublicKeyInfo()))
                throw new InvalidOperationException("Running host signer certificate refused.");
            VerifyHostToken(issued.RootElement.GetProperty("access_token").GetString()!, rsa, kid, authority,
                app.Configuration["Authentication:WorkloadIssuer:Workload"]!, probe,
                Math.Min(deadline, new DateTimeOffset(certificate.NotAfter.ToUniversalTime()).ToUnixTimeSeconds()));
        }
        finally { await app.StopAsync(CancellationToken.None); }
    }

    private static void VerifyHostToken(string token, RSA signer, string kid, string issuer, string workload, JsonElement probe, long maximumExpiry)
        => typeof(PlatformWorkloadEnrollment).GetMethod("VerifyToken", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [token, signer, kid, issuer, workload, probe, maximumExpiry]);

    private static async Task<Dictionary<string, object>> EnvironmentAsync(IDistributedApplicationBuilder builder, ProjectResource resource)
    {
        Dictionary<string, object> environment = new(StringComparer.Ordinal);
        foreach (EnvironmentCallbackAnnotation annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await annotation.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, resource, environment, TestContext.Current.CancellationToken));
        return environment;
    }

    private static IDistributedApplicationBuilder Builder()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--environment=Development"], AssemblyName = typeof(PlatformWorkloadEnrollment).Assembly.GetName().Name,
            DisableDashboard = true, AllowUnsecuredTransport = true,
        });
        foreach (string name in new[] { "eventstore", "tenants", "folders", "folders-replica", "folders-workers", "folders-workers-replica", "folders-workers-proof-a", "folders-workers-proof-b" })
        {
            string workload = (name == "folders-replica" ? "folders" : name == "folders-workers-replica" ? "folders-workers" : name) + "-actual-run";
            builder.AddProject(name, Path.Combine(PlatformTestRepository.Root, "src/Hexalith.Platform.EventStoreHost/Hexalith.Platform.EventStoreHost.csproj"))
                .WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions { AppId = workload }));
        }
        return builder;
    }
}
