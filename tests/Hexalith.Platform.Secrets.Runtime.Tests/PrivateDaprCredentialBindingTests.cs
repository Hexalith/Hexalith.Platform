using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class PrivateDaprCredentialBindingTests
{
    [Fact]
    public async Task MountedBearerIsSeparateFromDaprTokenAndRotationIsReadBeforeEveryCall()
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string path = Path.Combine(Path.GetTempPath(), $"ext-secrets-credential-test-{Guid.NewGuid():N}");
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task<List<(string Headers, string Body)>> server = Task.Run(async () =>
        {
            var requests = new List<(string Headers, string Body)>();
            for (int index = 0; index < 2; index++)
            {
                using TcpClient socket = await listener.AcceptTcpClientAsync(cancellation);
                using NetworkStream stream = socket.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var headers = new StringBuilder();
                int length = 0;
                while (await reader.ReadLineAsync(cancellation) is { Length: > 0 } line)
                {
                    headers.AppendLine(line);
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) { length = int.Parse(line[15..]); }
                }
                char[] body = new char[length];
                (await reader.ReadBlockAsync(body.AsMemory(), cancellation)).ShouldBe(length);
                requests.Add((headers.ToString(), new string(body)));
                await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}"), cancellation);
            }
            return requests;
        }, cancellation);
        try
        {
            using DaprClient client = new DaprClientBuilder().UseHttpEndpoint($"http://127.0.0.1:{port}").UseDaprApiToken("dapr-channel-credential").Build();
            var invoker = new DaprSecretsInvoker(client);
            await File.WriteAllTextAsync(path, "synthetic-workload-token-one\n", cancellation);
            (await invoker.InvokeCredentialBoundAsync("private-owner", "file:" + path, "query", "{\"requestId\":\"exact\"}", cancellation)).ShouldBe("{}");
            await File.WriteAllTextAsync(path, "synthetic-workload-token-two\n", cancellation);
            (await invoker.InvokeCredentialBoundAsync("private-owner", "file:" + path, "query", "{\"requestId\":\"exact\"}", cancellation)).ShouldBe("{}");
            List<(string Headers, string Body)> requests = await server.WaitAsync(TimeSpan.FromSeconds(5), cancellation);
            requests[0].Headers.ShouldContain("Authorization: Bearer synthetic-workload-token-one");
            requests[1].Headers.ShouldContain("Authorization: Bearer synthetic-workload-token-two");
            requests.ShouldAllBe(request => request.Headers.Contains("dapr-api-token: dapr-channel-credential", StringComparison.OrdinalIgnoreCase));
            requests.ShouldAllBe(request => request.Body == "{\"requestId\":\"exact\"}");
            requests.ShouldAllBe(request => !request.Headers.Contains(path, StringComparison.Ordinal));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("oversize")]
    [InlineData("header-injection")]
    [InlineData("non-file-reference")]
    [InlineData("credential-as-app-id")]
    public async Task InvalidMountedCredentialsAndDestinationsSendNoRequest(string scenario)
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string path = Path.Combine(Path.GetTempPath(), $"ext-secrets-credential-test-{Guid.NewGuid():N}");
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using DaprClient client = new DaprClientBuilder().UseHttpEndpoint($"http://127.0.0.1:{port}").Build();
        var invoker = new DaprSecretsInvoker(client);
        try
        {
            if (scenario != "missing")
            {
                string token = scenario switch { "empty" => "", "oversize" => new string('a', 8193), "header-injection" => "token\r\nAuthorization: substitute", _ => "synthetic-token" };
                await File.WriteAllTextAsync(path, token, cancellation);
            }
            string reference = scenario == "non-file-reference" ? "namespace/Secret/private#token" : "file:" + path;
            string destination = scenario == "credential-as-app-id" ? reference : "private-owner";
            (await invoker.InvokeCredentialBoundAsync(destination, reference, "effect", "{}", cancellation)).ShouldBeNull();
            listener.Pending().ShouldBeFalse();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task PrivateAdaptersKeepCredentialReferencesOutOfDaprDestinationsAndNeverUseApprovalCredentials()
    {
        var invoker = new RecordingInvoker();
        SecretsRuntimeConfiguration configuration = Fixtures.Ready();
        var clock = new FixedClock(Fixtures.Now);
        var payload = new DeletionBatchCapabilityV1("issuer", "audience", "tenant", "request", "seal", "batch", 1, "batch-1", new string('a', 64), "guard", 1, 1, 1, "k1");
        await new DaprSigningProviderAdapter(configuration, invoker, clock).LookupAsync(payload, "original", TestContext.Current.CancellationToken);
        await new DaprReplayRegistrarAdapter(configuration, invoker, clock).LookupAsync(new("issuer", "nonce", "tenant", "command", "k1", new string('a', 64)), TestContext.Current.CancellationToken);
        await new DaprCompromiseRegistrarAdapter(configuration, invoker, clock).LookupAsync(Envelope(), TestContext.Current.CancellationToken);
        invoker.PrivateBindings.ShouldBe(new[]
        {
            ("signer-app", "capability-signer-credential"),
            ("replay-app", "replay-credential"),
            ("compromise-sub", "compromise-credential"),
        });
        invoker.Calls.ShouldAllBe(call => !call.Body.Contains("outside-approval-issuer-credential", StringComparison.Ordinal));
    }

    [Fact]
    public void ApprovalCredentialAloneCannotComposeCapabilitySignerAndLegacyCompromiseRegistrationRemainsClosed()
    {
        var services = new ServiceCollection();
        SecretsRuntimeConfiguration configuration = Fixtures.Ready() with { CapabilitySignerReference = null, CapabilitySignerCredentialReference = null };
        services.AddExplicitSecretsRuntime(configuration, new RecordingInvoker(), new FixedClock(Fixtures.Now));
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(IDeletionCapabilitySigningProvider));
        services.AddPrivateCompromise(configuration, new RecordingInvoker()).ShouldBe("runtime.compromiseSubscriberBinding");
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(DeletionCapabilityRevocationSubscriber));
    }

    [Theory]
    [InlineData("signer-endpoint", "runtime.capabilitySignerReference")]
    [InlineData("signer-credential", "runtime.capabilitySignerCredentialReference")]
    [InlineData("replay-endpoint", "runtime.replayRegistrarReference")]
    [InlineData("compromise-credential", "runtime.compromiseSubscriberCredentialReference")]
    [InlineData("unbound-transport", "runtime.privateCredentialBoundTransport")]
    public async Task MissingPrivateBindingsNameTheExactInputBeforeAnyNetworkCall(string scenario, string expected)
    {
        var recording = new RecordingInvoker();
        ISecretsDaprInvoker invoker = scenario == "unbound-transport" ? Substitute.For<ISecretsDaprInvoker>() : recording;
        SecretsRuntimeConfiguration configuration = scenario switch
        {
            "signer-endpoint" => Fixtures.Ready() with { CapabilitySignerReference = null },
            "signer-credential" => Fixtures.Ready() with { CapabilitySignerCredentialReference = null },
            "replay-endpoint" => Fixtures.Ready() with { ReplayRegistrarReference = null },
            "compromise-credential" => Fixtures.Ready() with { CompromiseSubscriberCredentialReference = null },
            _ => Fixtures.Ready(),
        };
        SecretsDependencyNotAvailableException exception = await Should.ThrowAsync<SecretsDependencyNotAvailableException>(async () =>
        {
            if (scenario == "replay-endpoint")
            {
                await new DaprReplayRegistrarAdapter(configuration, invoker).LookupAsync(new("issuer", "nonce", "tenant", "command", "k1", new string('a', 64)), TestContext.Current.CancellationToken);
            }
            else if (scenario == "compromise-credential")
            {
                await new DaprCompromiseRegistrarAdapter(configuration, invoker).LookupAsync(Envelope(), TestContext.Current.CancellationToken);
            }
            else
            {
                var payload = new DeletionBatchCapabilityV1("issuer", "audience", "tenant", "request", "seal", "batch", 1, "batch-1", new string('a', 64), "guard", 1, 1, 1, "k1");
                await new DaprSigningProviderAdapter(configuration, invoker).LookupAsync(payload, "original", TestContext.Current.CancellationToken);
            }
        });
        exception.Input.ShouldBe(expected);
        recording.Calls.ShouldBeEmpty();
        if (scenario == "unbound-transport")
        { await invoker.DidNotReceive().InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()); }
    }

    [Fact]
    public void ReferenceParserKeepsIndependentApprovalAndCapabilitySignerFieldsSeparate()
    {
        SecretsRuntimeConfiguration? configuration = SecretsRuntimeConfiguration.Parse("""{"runtime":{"independentSignerCredentialReference":"approval-authority","capabilitySignerReference":"capability-owner","capabilitySignerCredentialReference":"file:/mounted/capability","replayRegistrarReference":"private-replay","replayRegistrarCredentialReference":"file:/mounted/replay"}}""");
        configuration.ShouldNotBeNull();
        configuration.IndependentSignerCredentialReference.ShouldBe("approval-authority");
        configuration.CapabilitySignerReference.ShouldBe("capability-owner");
        configuration.CapabilitySignerCredentialReference.ShouldBe("file:/mounted/capability");
        configuration.ReplayRegistrarReference.ShouldBe("private-replay");
        configuration.ReplayRegistrarCredentialReference.ShouldBe("file:/mounted/replay");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitPrivateCompromiseCompositionUsesOneClockAndExactMirrorRecovery(bool explicitClock)
    {
        var services = new ServiceCollection();
        var invoker = new RecordingInvoker();
        var mirror = Substitute.For<IDeletionCapabilityGuardRevocationMirror>();
        var envelope = Envelope();
        var block = new DeletionCapabilityRevocationReceipt(envelope, 3, 2, "original-block", ["batch-1"]);
        invoker.Reply = (_, method, body, _) =>
        {
            var proof = System.Text.Json.Nodes.JsonNode.Parse(body)!;
            if (method == "query-revocation")
            {
                proof["authorized"] = true;
                proof["authorityRevision"] = "rev-2";
                proof["observedAt"] = Fixtures.Now.ToString("O");
                proof["validUntil"] = Fixtures.Now.AddSeconds(20).ToString("O");
            }
            else
            {
                proof["ownerRevision"] = 3;
                proof["keyBlockSetRevision"] = 2;
                proof["receiptId"] = "original-block";
                proof["affectedBatchIds"] = new System.Text.Json.Nodes.JsonArray("batch-1");
            }
            return proof.ToJsonString();
        };
        mirror.LookupAsync(envelope, Arg.Any<CancellationToken>()).Returns(block);
        TimeProvider existingClock = new FixedClock(explicitClock ? Fixtures.Now.AddYears(1) : Fixtures.Now);
        services.AddSingleton(existingClock);
        services.AddPrivateCompromise(Fixtures.Ready(), invoker, new("issuer", "audience", "tenant-a"), mirror,
            explicitClock ? new FixedClock(Fixtures.Now) : null).ShouldBeNull();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(existingClock);
        (await provider.GetRequiredService<DeletionCapabilityRevocationSubscriber>().ReceiveAsync(envelope, "synthetic-signed-evidence", TestContext.Current.CancellationToken)).ShouldBeTrue();
        invoker.Calls.Where(call => call.Method.Contains("compromise", StringComparison.Ordinal)).ShouldAllBe(call => call.Method == "query-compromise");
        await mirror.DidNotReceive().RecordAsync(Arg.Any<DeletionCapabilityRevocationReceipt>(), Arg.Any<CancellationToken>());
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(IDeletionCapabilityCompromiseRegistrar));
    }

    [Theory]
    [InlineData("other-issuer", "audience")]
    [InlineData("issuer", "other-audience")]
    public void MismatchedSubscriptionRegistersNoSubscriberOrPrivateDependencies(string issuer, string audience)
    {
        var services = new ServiceCollection();
        services.AddPrivateCompromise(Fixtures.Ready(), new RecordingInvoker(), new(issuer, audience, "tenant-a"),
            Substitute.For<IDeletionCapabilityGuardRevocationMirror>()).ShouldBe("runtime.compromiseSubscriberBinding");
        services.ShouldBeEmpty();
    }

    [Fact]
    public async Task OpenedFifoIsRejectedWithinBudgetWithoutAnHttpRequest()
    {
        if (!OperatingSystem.IsLinux()) { return; }
        string path = Path.Combine(Path.GetTempPath(), $"ext-secrets-fifo-test-{Guid.NewGuid():N}");
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using DaprClient client = new DaprClientBuilder().UseHttpEndpoint($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}").Build();
        try
        {
            CreateFifo(path, 0x180).ShouldBe(0); // 0600, with no writer: an ordinary open would block.
            var elapsed = Stopwatch.StartNew();
            string? result = await Task.Run(() => new DaprSecretsInvoker(client).InvokeCredentialBoundAsync("private-owner", "file:" + path, "effect", "{}", TestContext.Current.CancellationToken))
                .WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            result.ShouldBeNull();
            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(3));
            listener.Pending().ShouldBeFalse();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ProjectedSecretSymlinkResolvesTheOpenedRegularFile()
    {
        if (!OperatingSystem.IsLinux()) { return; }
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string directory = Path.Combine(Path.GetTempPath(), $"ext-secrets-projection-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(directory, ".revision-1"));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, ".revision-1", "token"), "synthetic-projected-token", cancellation);
            Directory.CreateSymbolicLink(Path.Combine(directory, "..data"), ".revision-1");
            File.CreateSymbolicLink(Path.Combine(directory, "token"), "..data/token");
            Task<List<CapturedRequest>> server = ServeAsync(listener, 1, (_, _) => (200, "{}", null), cancellation);
            using DaprClient client = new DaprClientBuilder().UseHttpEndpoint($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}").Build();
            (await new DaprSecretsInvoker(client).InvokeCredentialBoundAsync("private-owner", "file:" + Path.Combine(directory, "token"), "query", "{}", cancellation)).ShouldBe("{}");
            (await server.WaitAsync(TimeSpan.FromSeconds(3), cancellation)).Single().Headers.ShouldContain("Authorization: Bearer synthetic-projected-token");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task RedirectCannotSendEitherCredentialToAnotherEndpoint()
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        budget.CancelAfter(TimeSpan.FromSeconds(3));
        string path = Path.Combine(Path.GetTempPath(), $"ext-secrets-redirect-test-{Guid.NewGuid():N}");
        using var origin = new TcpListener(IPAddress.Loopback, 0);
        using var redirected = new TcpListener(IPAddress.Loopback, 0);
        origin.Start();
        redirected.Start();
        try
        {
            await File.WriteAllTextAsync(path, "synthetic-private-token", cancellation);
            string location = $"http://127.0.0.1:{((IPEndPoint)redirected.LocalEndpoint).Port}/untrusted";
            Task<List<CapturedRequest>> server = ServeAsync(origin, 1, (_, _) => (307, "", location), budget.Token);
            using DaprClient client = new DaprClientBuilder().UseHttpEndpoint($"http://127.0.0.1:{((IPEndPoint)origin.LocalEndpoint).Port}").UseDaprApiToken("synthetic-dapr-token").Build();
            (await new DaprSecretsInvoker(client).InvokeCredentialBoundAsync("private-owner", "file:" + path, "effect", "{}", budget.Token)).ShouldBeNull();
            CapturedRequest request = (await server.WaitAsync(TimeSpan.FromSeconds(3), cancellation)).Single();
            request.Headers.ShouldContain("Authorization: Bearer synthetic-private-token");
            request.Headers.ShouldContain("dapr-api-token: synthetic-dapr-token", Case.Insensitive);
            redirected.Pending().ShouldBeFalse();
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("signer", "signer-app", "query-sign", "effect-sign")]
    [InlineData("replay", "replay-app", "query-replay", "effect-replay")]
    [InlineData("compromise", "compromise-sub", "query-compromise", "effect-compromise")]
    public async Task PrivateAdapterQueriesAndEffectsAuthenticateTheExactOwnerOverHttp(string kind, string appId, string query, string effect)
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string path = Path.Combine(Path.GetTempPath(), $"ext-secrets-effect-test-{Guid.NewGuid():N}");
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = new DeletionBatchCapabilityV1("issuer", "audience", "tenant-a", "request", "seal", "batch", 1, "batch-1", new string('a', 64), "guard", 1, 1, 1, "k1");
        var intent = new TrustedEnvelopeReplayIntent("issuer", "nonce", "tenant-a", "command", "k1", new string('c', 64));
        string protectedHeader = Base64Url(Encoding.UTF8.GetBytes("""{"alg":"ES256","anchorVersion":"1","keyVersion":"k1","kid":"exact-public-anchor","typ":"DeletionBatchCapabilityV1"}"""));
        byte[] signedBytes = Encoding.ASCII.GetBytes(protectedHeader + "." + Base64Url(DeletionBatchCapabilityIdentity.CanonicalPayload(payload)));
        byte[] tag = key.SignData(signedBytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        string signature = protectedHeader + ".." + Base64Url(tag);
        try
        {
            await File.WriteAllTextAsync(path, "synthetic-effect-token", cancellation);
            Task<List<CapturedRequest>> server = ServeAsync(listener, 2, (index, request) =>
            {
                request.Target.ShouldBe($"/v1.0/invoke/{appId}/method/{(index == 0 ? query : effect)}");
                var proof = (JsonObject)JsonNode.Parse(request.Body)!;
                if (index == 0)
                {
                    proof["authorized"] = true;
                    proof["authorityRevision"] = "owner-revision-1";
                    proof["status"] = "absent";
                    proof["proofType"] = "absence";
                    proof["authorityReference"] = appId;
                    proof["queryMethod"] = query;
                }
                else if (kind == "signer")
                {
                    proof["state"] = "Signed";
                    proof["detachedJws"] = signature;
                    proof["publicAnchorId"] = "exact-public-anchor";
                    proof["publicAnchorVersion"] = "1";
                    proof["originalIssuanceReceiptId"] = "exact-issuance-receipt";
                    proof["subjectPublicKeyInfo"] = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
                }
                else if (kind == "replay")
                {
                    proof["sourceRevision"] = 1;
                    proof["receiptId"] = "exact-replay-receipt";
                    proof["firstSeenAt"] = Fixtures.Now.ToString("O");
                    proof["retainUntil"] = Fixtures.Now.AddDays(7).ToString("O");
                }
                else
                {
                    proof["ownerRevision"] = 3;
                    proof["keyBlockSetRevision"] = 2;
                    proof["receiptId"] = "exact-compromise-receipt";
                    proof["affectedBatchIds"] = new JsonArray("batch-1");
                }
                proof["observedAt"] = Fixtures.Now.ToString("O");
                proof["validUntil"] = Fixtures.Now.AddSeconds(20).ToString("O");
                return (200, proof.ToJsonString(), null);
            }, cancellation);
            using DaprClient client = new DaprClientBuilder().UseHttpEndpoint($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}").UseDaprApiToken("synthetic-dapr-token").Build();
            var transport = new DaprSecretsInvoker(client);
            SecretsRuntimeConfiguration configuration = Fixtures.Ready() with
            {
                CapabilitySignerCredentialReference = "file:" + path,
                ReplayRegistrarCredentialReference = "file:" + path,
                CompromiseSubscriberCredentialReference = "file:" + path,
            };
            var clock = new FixedClock(Fixtures.Now);
            if (kind == "signer")
            {
                DeletionCapabilitySigningResult result = await new DaprSigningProviderAdapter(configuration, transport, clock).SignAsync(payload, "exact-signing-request", cancellation);
                result.Outcome.State.ShouldBe(DeletionCapabilitySigningState.Signed);
                result.Outcome.Payload.ShouldBe(payload);
                result.Outcome.DetachedJws.ShouldBe(signature);
                key.VerifyData(signedBytes, tag, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation).ShouldBeTrue();
                result.OriginalIssuanceReceiptId.ShouldBe("exact-issuance-receipt");
                result.PublicAnchorSubjectPublicKeyInfo.ShouldBe(key.ExportSubjectPublicKeyInfo());
            }
            else if (kind == "replay")
            {
                TrustedEnvelopeReplayReceipt? result = await new DaprReplayRegistrarAdapter(configuration, transport, clock).RegisterAsync(intent, Fixtures.Now, TimeSpan.FromDays(7), cancellation);
                result.ShouldNotBeNull();
                result.Intent.ShouldBe(intent);
                result.ReceiptId.ShouldBe("exact-replay-receipt");
            }
            else
            {
                DeletionCapabilityRevocationReceipt? result = await new DaprCompromiseRegistrarAdapter(configuration, transport, clock).RegisterAsync(Envelope(), cancellation);
                result.ShouldNotBeNull();
                result.Envelope.ShouldBe(Envelope());
                result.ReceiptId.ShouldBe("exact-compromise-receipt");
                result.AffectedBatchIds.ShouldBe(["batch-1"]);
            }
            List<CapturedRequest> requests = await server.WaitAsync(TimeSpan.FromSeconds(3), cancellation);
            requests.Count.ShouldBe(2);
            requests.ShouldAllBe(request => request.Headers.Contains("Authorization: Bearer synthetic-effect-token", StringComparison.Ordinal));
            requests.ShouldAllBe(request => request.Headers.Contains("dapr-api-token: synthetic-dapr-token", StringComparison.OrdinalIgnoreCase));
            requests.ShouldAllBe(request => !request.Body.Contains("synthetic-effect-token", StringComparison.Ordinal));
        }
        finally { File.Delete(path); }
    }

    private sealed record CapturedRequest(string Target, string Headers, string Body);

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static async Task<List<CapturedRequest>> ServeAsync(TcpListener listener, int count,
        Func<int, CapturedRequest, (int Status, string Body, string? Location)> respond, CancellationToken cancellation)
    {
        var requests = new List<CapturedRequest>();
        for (int index = 0; index < count; index++)
        {
            using TcpClient socket = await listener.AcceptTcpClientAsync(cancellation);
            using NetworkStream stream = socket.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            string target = (await reader.ReadLineAsync(cancellation))!.Split(' ')[1];
            var headers = new StringBuilder();
            int length = 0;
            while (await reader.ReadLineAsync(cancellation) is { Length: > 0 } line)
            {
                headers.AppendLine(line);
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) { length = int.Parse(line[15..]); }
            }
            char[] body = new char[length];
            (await reader.ReadBlockAsync(body.AsMemory(), cancellation)).ShouldBe(length);
            var request = new CapturedRequest(target, headers.ToString(), new string(body));
            requests.Add(request);
            var response = respond(index, request);
            string location = response.Location is null ? "" : $"Location: {response.Location}\r\n";
            string wire = $"HTTP/1.1 {response.Status} Result\r\nContent-Length: {Encoding.UTF8.GetByteCount(response.Body)}\r\n{location}Connection: close\r\n\r\n{response.Body}";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(wire), cancellation);
        }
        return requests;
    }

    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int CreateFifo([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint mode);

    private static DeletionCapabilityRevocationEnvelope Envelope()
        => new("issuer", "audience", "tenant-a", "DeletionBatchCapabilitySigningKey", "k1", 2, 4, "event-1", new string('a', 64));
}
