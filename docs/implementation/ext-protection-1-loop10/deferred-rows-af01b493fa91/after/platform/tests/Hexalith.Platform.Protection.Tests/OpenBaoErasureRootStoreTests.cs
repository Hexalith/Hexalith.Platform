using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Native HTTP shape, strict CAS classification and missing-input component checks.</summary>
public sealed class OpenBaoErasureRootStoreTests
{
    private readonly ErasureRootInstallation _installation = new("erc", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant"] = "epoch" });
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("success")]
    [InlineData("native-refusal")]
    [InlineData("invalid-json")]
    public async Task DecryptRequestBodyIsZeroedAfterSuccessAndFailure(string outcome)
    {
        string variable = "ERC_DECRYPT_CLEAR_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "component-test-credential");
        MemoryStream? retainedBody = null;
        byte[] expected = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        try
        {
            using var handler = new ErasureRootTransportHandler((request, _) =>
            {
                request.RequestUri!.AbsolutePath.ShouldBe("/v1/transit/decrypt/epoch");
                retainedBody = (MemoryStream)request.Content!.ReadAsStream(Token);
                using var document = JsonDocument.Parse(retainedBody.ToArray());
                document.RootElement.GetProperty("ciphertext").GetString().ShouldBe("vault:v1:private-ciphertext");
                document.RootElement.GetProperty("context").GetString().ShouldBe("YQ==");
                // ByteArrayContent's read stream retains its backing body after the request
                // is disposed. ToArray works on a closed MemoryStream, so this observes the
                // actual transport buffer after DecryptAsync's finally, without a copy seam.
                retainedBody.ToArray().Any(value => value != 0).ShouldBeTrue();
                return outcome switch
                {
                    "native-refusal" => ErasureRootTransportHandler.Json("{\"errors\":[\"permission denied\"]}", HttpStatusCode.Forbidden),
                    "invalid-json" => ErasureRootTransportHandler.Json("not-json"),
                    _ => ErasureRootTransportHandler.Json(JsonSerializer.Serialize(new { data = new { plaintext = Convert.ToBase64String(expected) } })),
                };
            }, readBodyAsString: false);
            using var http = new HttpClient(handler) { BaseAddress = new("https://erc.invalid/"), Timeout = TimeSpan.FromSeconds(5) };
            using var store = new OpenBaoErasureRootStore(http, _installation, "env:" + variable);
            if (outcome == "success")
            {
                byte[] actual = await store.DecryptAsync("tenant", new("vault:v1:private-ciphertext", "YQ=="), Token);
                try { actual.ShouldBe(expected); }
                finally { CryptographicOperations.ZeroMemory(actual); }
            }
            else if (outcome == "native-refusal")
            {
                await Should.ThrowAsync<InvalidOperationException>(() => store.DecryptAsync("tenant", new("vault:v1:private-ciphertext", "YQ=="), Token));
            }
            else
            {
                await Should.ThrowAsync<JsonException>(() => store.DecryptAsync("tenant", new("vault:v1:private-ciphertext", "YQ=="), Token));
            }
            handler.Calls.ShouldBe(1);
            retainedBody.ShouldNotBeNull().ToArray().ShouldAllBe(value => value == 0);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public async Task NativeDeadlineStillBoundsStalledBodyAfterHeadersComplete()
    {
        string variable = "ERC_BODY_TEST_" + Guid.NewGuid().ToString("N"); Environment.SetEnvironmentVariable(variable, "component");
        try
        {
            using var handler = new ErasureRootTransportHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StalledErasureRootBody()) });
            using var http = new HttpClient(handler) { BaseAddress = new("https://erc.invalid/"), Timeout = TimeSpan.FromMilliseconds(100) };
            var store = new OpenBaoErasureRootStore(http, _installation, "env:" + variable);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadHeadAsync("tenant", Token));
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Fact]
    public async Task MaximumAdmittedHeadReservesNativeReadFramingOverhead()
    {
        string variable = "ERC_FRAMING_TEST_" + Guid.NewGuid().ToString("N"); Environment.SetEnvironmentVariable(variable, "component");
        try
        {
            byte[] head = JsonSerializer.SerializeToUtf8Bytes(new { cell = new string('x', OpenBaoErasureRootStore.MaximumHeadBytes - 11) });
            using var handler = new ErasureRootTransportHandler((request, _) => request.Method == HttpMethod.Post ? ErasureRootTransportHandler.Json("{}")
                : ErasureRootTransportHandler.Json("{\"data\":{\"metadata\":{\"version\":2,\"created_time\":\"2026-10-29T00:00:00Z\",\"custom_metadata\":{}},\"data\":" + System.Text.Encoding.UTF8.GetString(head) + "}}"));
            using var http = new HttpClient(handler) { BaseAddress = new("https://erc.invalid/"), Timeout = TimeSpan.FromSeconds(5) };
            var store = new OpenBaoErasureRootStore(http, _installation, "env:" + variable);
            (await store.CompareExchangeHeadAsync("tenant", 1, head, Token)).ShouldBeTrue();
            (await store.ReadHeadAsync("tenant", Token))!.Bytes.ShouldBe(head);
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Fact]
    public async Task MissingCredentialRefusesBeforeAnyTransportEffect()
    {
        string reference = "ERC_MISSING_TEST_" + Guid.NewGuid().ToString("N");
        using var handler = new ErasureRootTransportHandler((_, _) => throw new InvalidOperationException());
        using var http = new HttpClient(handler) { BaseAddress = new("https://erc.invalid/"), Timeout = TimeSpan.FromSeconds(5) };
        var store = new OpenBaoErasureRootStore(http, _installation, "env:" + reference);
        await Should.ThrowAsync<InvalidOperationException>(() => store.ReadHeadAsync("tenant", Token));
        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task TransitUsesAuthenticatedContextExplicitEpochAndOrderedBatchWithoutAssociatedData()
    {
        string variable = "ERC_PROTOCOL_TEST_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "component-test-credential");
        try
        {
            using var handler = new ErasureRootTransportHandler((request, body) =>
            {
                request.Headers.GetValues("X-Vault-Token").Single().ShouldBe("component-test-credential");
                using var json = JsonDocument.Parse(body!);
                json.RootElement.TryGetProperty("associated_data", out _).ShouldBeFalse();
                json.RootElement.GetProperty("key_version").GetInt32().ShouldBe(2);
                if (request.RequestUri!.AbsolutePath.EndsWith("/rewrap/epoch", StringComparison.Ordinal))
                {
                    var batch = json.RootElement.GetProperty("batch_input");
                    batch.GetArrayLength().ShouldBe(2);
                    batch[0].GetProperty("context").GetString().ShouldBe("YQ==");
                    batch[1].GetProperty("context").GetString().ShouldBe("Yg==");
                    return ErasureRootTransportHandler.Json("{\"data\":{\"batch_results\":[{\"ciphertext\":\"vault:v2:a\"},{\"ciphertext\":\"vault:v2:b\"}]}}");
                }
                json.RootElement.GetProperty("plaintext").GetBytesFromBase64().Length.ShouldBe(32);
                json.RootElement.GetProperty("context").GetString().ShouldBe("YQ==");
                return ErasureRootTransportHandler.Json("{\"data\":{\"ciphertext\":\"vault:v2:a\"}}");
            });
            using var http = new HttpClient(handler) { BaseAddress = new("https://erc.invalid/"), Timeout = TimeSpan.FromSeconds(5) };
            var store = new OpenBaoErasureRootStore(http, _installation, "env:" + variable);
            (await store.EncryptAsync("tenant", 2, new byte[32], "YQ==", Token)).ShouldBe("vault:v2:a");
            (await store.RewrapAsync("tenant", 2, [new("vault:v1:a", "YQ=="), new("vault:v1:b", "Yg==")], Token))
                .ShouldBe(["vault:v2:a", "vault:v2:b"]);
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Theory]
    [InlineData("check-and-set parameter did not match the current version", true)]
    [InlineData("permission denied", false)]
    public async Task OnlyExactNativeCasConflictIsRetryable(string error, bool conflict)
    {
        string variable = "ERC_CAS_TEST_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "component-test-credential");
        try
        {
            using var handler = new ErasureRootTransportHandler((_, _) => ErasureRootTransportHandler.Json(JsonSerializer.Serialize(new { errors = new[] { error } }), HttpStatusCode.BadRequest));
            using var http = new HttpClient(handler) { BaseAddress = new("https://erc.invalid/"), Timeout = TimeSpan.FromSeconds(5) };
            var store = new OpenBaoErasureRootStore(http, _installation, "env:" + variable);
            if (conflict) { (await store.CompareExchangeHeadAsync("tenant", 1, "{}"u8.ToArray(), Token)).ShouldBeFalse(); }
            else { await Should.ThrowAsync<InvalidOperationException>(() => store.CompareExchangeHeadAsync("tenant", 1, "{}"u8.ToArray(), Token)); }
            handler.Calls.ShouldBe(1);
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(64)]
    public async Task TrimMinimumVersionsUseExactStringValuesAcceptedByTheBoundedNativePolicy(int epoch)
    {
        string variable = "ERC_TRIM_ENCODING_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "component-test-credential");
        var paths = new List<string>();
        try
        {
            using var handler = new ErasureRootTransportHandler((request, body) =>
            {
                paths.Add(request.RequestUri!.AbsolutePath);
                using var json = JsonDocument.Parse(body!);
                if (paths.Count == 1)
                {
                    json.RootElement.GetProperty("min_decryption_version").GetString().ShouldBe(epoch.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    json.RootElement.GetProperty("min_encryption_version").GetString().ShouldBe(epoch.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    json.RootElement.EnumerateObject().Count().ShouldBe(2);
                }
                else
                {
                    json.RootElement.GetProperty("min_available_version").GetInt32().ShouldBe(epoch);
                    json.RootElement.EnumerateObject().Count().ShouldBe(1);
                }
                return ErasureRootTransportHandler.Json("{}");
            });
            using var http = new HttpClient(handler) { BaseAddress = new("https://erc.invalid/"), Timeout = TimeSpan.FromSeconds(5) };
            using var store = new OpenBaoErasureRootStore(http, _installation, "env:" + variable);
            await store.TrimAsync("tenant", epoch, Token);
            paths.ShouldBe(["/v1/transit/keys/epoch/config", "/v1/transit/keys/epoch/trim"]);
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task TrimVerificationAllowsUnusedNewerVersionsButRequiresExactIrreversibleFloor(int latest)
    {
        string variable = "ERC_TRIM_TEST_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "component-test-credential");
        try
        {
            using var handler = new ErasureRootTransportHandler((request, _) =>
            {
                if (request.RequestUri!.AbsolutePath == "/v1/sys/health")
                { return ErasureRootTransportHandler.Json("{\"version\":\"2.6.2\",\"initialized\":true,\"sealed\":false}"); }
                return ErasureRootTransportHandler.Json(JsonSerializer.Serialize(new { data = new {
                    type = "aes256-gcm96", derived = true, exportable = false, allow_plaintext_backup = false, deletion_allowed = false,
                    latest_version = latest, min_available_version = 2, min_decryption_version = 2, min_encryption_version = 2,
                    keys = latest == 2 ? new Dictionary<string, int> { ["2"] = 123 } : new Dictionary<string, int> { ["2"] = 123, ["3"] = 456 } } }));
            });
            using var http = new HttpClient(handler) { BaseAddress = new("https://erc.invalid/"), Timeout = TimeSpan.FromSeconds(5) };
            var store = new OpenBaoErasureRootStore(http, _installation, "env:" + variable);
            (await store.VerifyTrimAsync("tenant", 2, Token)).ShouldBeTrue();
            (await store.VerifyTrimAsync("tenant", 3, Token)).ShouldBeFalse();
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Theory]
    [InlineData("http://erc.invalid/")]
    [InlineData("https://erc.invalid/not-a-root/")]
    public void UnsafeTransportEnrollmentCannotBeConstructed(string endpoint)
    {
        using var http = new HttpClient { BaseAddress = new(endpoint), Timeout = TimeSpan.FromSeconds(5) };
        Should.Throw<ArgumentException>(() => new OpenBaoErasureRootStore(http, _installation, "env:ERC_TEST"));
    }

    [Fact]
    public async Task NonIntegerKeyVersionNameRefusesTrimVerification()
    {
        string variable = "ERC_TRIM_NAME_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "component-test-credential");
        try
        {
            using var handler = new ErasureRootTransportHandler((request, _) =>
            {
                if (request.RequestUri!.AbsolutePath == "/v1/sys/health")
                { return ErasureRootTransportHandler.Json("{\"version\":\"2.6.2\",\"initialized\":true,\"sealed\":false}"); }
                return ErasureRootTransportHandler.Json(JsonSerializer.Serialize(new { data = new {
                    type = "aes256-gcm96", derived = true, exportable = false, allow_plaintext_backup = false, deletion_allowed = false,
                    latest_version = 2, min_available_version = 2, min_decryption_version = 2, min_encryption_version = 2,
                    keys = new Dictionary<string, int> { ["beta"] = 1 } } }));
            });
            using var http = new HttpClient(handler) { BaseAddress = new("https://erc.invalid/"), Timeout = TimeSpan.FromSeconds(5) };
            var store = new OpenBaoErasureRootStore(http, _installation, "env:" + variable);
            (await store.VerifyTrimAsync("tenant", 2, Token)).ShouldBeFalse();
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Fact]
    public async Task NullRewrapCiphertextRefusesBeforePrefixComparison()
    {
        string variable = "ERC_REWRAP_NULL_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "component-test-credential");
        try
        {
            using var handler = new ErasureRootTransportHandler((_, _) => ErasureRootTransportHandler.Json("{\"data\":{\"batch_results\":[{\"ciphertext\":null}]}}"));
            using var http = new HttpClient(handler) { BaseAddress = new("https://erc.invalid/"), Timeout = TimeSpan.FromSeconds(5) };
            var store = new OpenBaoErasureRootStore(http, _installation, "env:" + variable);
            await Should.ThrowAsync<InvalidOperationException>(() => store.RewrapAsync("tenant", 2, [new("vault:v1:a", "YQ==")], Token));
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Fact]
    public void MissingCaPinRefusesBeforeASocket()
    {
        Should.Throw<ArgumentException>(() => new OpenBaoErasureRootStore(new Uri("https://127.0.0.1/"), _installation, "env:ERC_TEST"));
    }

    [Fact]
    public async Task MatchingPrivateCaReceivesTheTokenAndADifferentCaDoesNot()
    {
        var (leaf, matching) = IssueCa("matching-ca");
        var (otherLeaf, different) = IssueCa("different-ca");
        using (otherLeaf)
        using (leaf)
        {
            string token = "erc-pin-token-" + Guid.NewGuid().ToString("N");
            string seen = await ExchangeAsync(leaf, matching, token);
            seen.ShouldContain(token);
            string refused = await ExchangeAsync(leaf, different, token);
            refused.ShouldNotContain(token);
        }
    }

    /// <summary>R11-BH-9: TrimAsync zeroes both native response buffers after success and after a refused trim.</summary>
    [Theory]
    [InlineData("success")]
    [InlineData("trim-refused")]
    public async Task TrimResponseBuffersAreNonzeroInUseAndZeroAfterward(string outcome)
    {
        string variable = "ERC_TRIM_CLEAR_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "component-test-credential");
        var observed = new List<(string Name, byte[] Buffer, bool NonzeroInUse)>();
        try
        {
            using var handler = new ErasureRootTransportHandler((request, _) => request.RequestUri!.AbsolutePath.EndsWith("/trim", StringComparison.Ordinal) && outcome == "trim-refused"
                ? ErasureRootTransportHandler.Json("{\"errors\":[\"permission denied\"]}", HttpStatusCode.Forbidden)
                : ErasureRootTransportHandler.Json("{\"data\":{\"name\":\"epoch\",\"latest_version\":2}}"));
            using var http = new HttpClient(handler) { BaseAddress = new("https://erc.invalid/"), Timeout = TimeSpan.FromSeconds(5) };
            using var store = new OpenBaoErasureRootStore(http, _installation, "env:" + variable)
            { ResponseBufferObserver = (name, buffer) => observed.Add((name, buffer, buffer.Any(value => value != 0))) };
            if (outcome == "success") { await store.TrimAsync("tenant", 2, Token); }
            else { await Should.ThrowAsync<InvalidOperationException>(() => store.TrimAsync("tenant", 2, Token)); }
            observed.Select(item => item.Name).ShouldBe(outcome == "success" ? new[] { "trim:config", "trim:trim" } : new[] { "trim:config" });
            observed.ShouldAllBe(item => item.NonzeroInUse);
            observed.ShouldAllBe(item => item.Buffer.All(value => value == 0));
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    /// <summary>R11-BH-9: key-metadata reads zero every native response buffer after success and after each refusal.</summary>
    [Theory]
    [InlineData("epoch-success")]
    [InlineData("verify-trim-success")]
    [InlineData("metadata-refused")]
    [InlineData("health-refused")]
    public async Task KeyMetadataResponseBuffersAreNonzeroInUseAndZeroAfterward(string outcome)
    {
        string variable = "ERC_KEY_CLEAR_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "component-test-credential");
        var observed = new List<(string Name, byte[] Buffer, bool NonzeroInUse)>();
        try
        {
            using var handler = new ErasureRootTransportHandler((request, _) =>
            {
                if (request.RequestUri!.AbsolutePath == "/v1/sys/health")
                { return ErasureRootTransportHandler.Json("{\"version\":\"2.6.2\",\"initialized\":true,\"sealed\":" + (outcome == "health-refused" ? "true" : "false") + "}"); }
                return ErasureRootTransportHandler.Json(JsonSerializer.Serialize(new { data = new {
                    type = "aes256-gcm96", derived = true, exportable = false, allow_plaintext_backup = false, deletion_allowed = outcome == "metadata-refused",
                    latest_version = 2, min_available_version = 2, min_decryption_version = 2, min_encryption_version = 2,
                    keys = new Dictionary<string, int> { ["2"] = 123 } } }));
            });
            using var http = new HttpClient(handler) { BaseAddress = new("https://erc.invalid/"), Timeout = TimeSpan.FromSeconds(5) };
            using var store = new OpenBaoErasureRootStore(http, _installation, "env:" + variable)
            { ResponseBufferObserver = (name, buffer) => observed.Add((name, buffer, buffer.Any(value => value != 0))) };
            switch (outcome)
            {
                case "epoch-success": (await store.ReadEpochAsync("tenant", Token)).ShouldBe(2); break;
                case "verify-trim-success": (await store.VerifyTrimAsync("tenant", 2, Token)).ShouldBeTrue(); break;
                default: await Should.ThrowAsync<InvalidOperationException>(() => store.ReadEpochAsync("tenant", Token)); break;
            }
            observed.Select(item => item.Name).ShouldContain("read-key:health");
            if (outcome == "health-refused") { observed.Select(item => item.Name).ShouldNotContain("read-key:metadata"); }
            else { observed.Select(item => item.Name).ShouldContain("read-key:metadata"); }
            observed.ShouldAllBe(item => item.NonzeroInUse);
            foreach (var item in observed) { item.Buffer.ShouldAllBe(value => value == 0, item.Name); }
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    /// <summary>R11-EC-7: production stores built sequentially or concurrently keep their own pinned CA; a later store never adopts or disposes another store's pin.</summary>
    [Fact]
    public async Task SequentialAndConcurrentStoresKeepOnlyTheirOwnPinnedCa()
    {
        var (leafA, caA) = IssueCa("erc-ca-a");
        var (leafB, caB) = IssueCa("erc-ca-b");
        using (leafA)
        using (leafB)
        {
            string token = "erc-ec7-token-" + Guid.NewGuid().ToString("N");
            string variable = "ERC_EC7_" + Guid.NewGuid().ToString("N");
            Environment.SetEnvironmentVariable(variable, token);
            try
            {
                // One flow: A, then a synthetic internal-handler store, then B. Disposing the later stores leaves A's pin intact.
                var serverA = ServeOnce(leafA);
                var serverB = ServeOnce(leafB);
                using var storeA = new OpenBaoErasureRootStore(new Uri($"https://127.0.0.1:{serverA.Port}/"), _installation with { TrustedCaCertificatePem = caA }, "env:" + variable, TimeSpan.FromSeconds(5));
                var synthetic = new OpenBaoErasureRootStore(new HttpClient(new ErasureRootTransportHandler((_, _) => ErasureRootTransportHandler.Json("{}")))
                    { BaseAddress = new("https://erc.invalid/"), Timeout = TimeSpan.FromSeconds(5) }, _installation, "env:" + variable, ownsHttp: true);
                var storeB = new OpenBaoErasureRootStore(new Uri($"https://127.0.0.1:{serverB.Port}/"), _installation with { TrustedCaCertificatePem = caB }, "env:" + variable, TimeSpan.FromSeconds(5));
                await ReadIgnoringTransportAsync(storeB);
                (await serverB.Received).ShouldContain(token);
                storeB.Dispose();
                synthetic.Dispose();
                await ReadIgnoringTransportAsync(storeA);
                (await serverA.Received).ShouldContain(token);

                // Concurrent construction: each store accepts only a server issued by its own CA.
                var runs = Enumerable.Range(0, 8).Select(index => Task.Run(async () =>
                {
                    bool useA = index % 2 == 0, matching = index % 4 < 2;
                    var server = ServeOnce(matching == useA ? leafA : leafB);
                    using var store = new OpenBaoErasureRootStore(new Uri($"https://127.0.0.1:{server.Port}/"),
                        _installation with { TrustedCaCertificatePem = useA ? caA : caB }, "env:" + variable, TimeSpan.FromSeconds(5));
                    await ReadIgnoringTransportAsync(store);
                    return (Matching: matching, Seen: (await server.Received).Contains(token, StringComparison.Ordinal));
                }, Token)).ToArray();
                foreach (var (matching, seen) in await Task.WhenAll(runs)) { seen.ShouldBe(matching); }
            }
            finally { Environment.SetEnvironmentVariable(variable, null); }
        }
    }

    private static async Task ReadIgnoringTransportAsync(OpenBaoErasureRootStore store)
    {
        try { await store.ReadHeadAsync("tenant", Token); }
        catch (HttpRequestException) { }
    }

    private static (int Port, Task<string> Received) ServeOnce(X509Certificate2 leaf)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = Task.Run(async () =>
        {
            try
            {
                using var tcp = await listener.AcceptTcpClientAsync(Token);
                await using var ssl = new SslStream(tcp.GetStream(), false);
                await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = leaf,
                    ClientCertificateRequired = false,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                }, Token);
                var buffer = new byte[8192];
                int count = await ssl.ReadAsync(buffer, Token);
                await ssl.WriteAsync("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray(), Token);
                return Encoding.ASCII.GetString(buffer, 0, count);
            }
            catch (Exception) { return ""; }
            finally { listener.Stop(); }
        });
        return (port, received.WaitAsync(TimeSpan.FromSeconds(10), Token));
    }

    private async Task<string> ExchangeAsync(X509Certificate2 leaf, string pem, string token)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            using var tcp = await listener.AcceptTcpClientAsync(Token);
            await using var ssl = new SslStream(tcp.GetStream(), false);
            try
            {
                await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = leaf,
                    ClientCertificateRequired = false,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                }, Token);
                var buffer = new byte[8192];
                int count = await ssl.ReadAsync(buffer, Token);
                await ssl.WriteAsync("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray(), Token);
                return Encoding.ASCII.GetString(buffer, 0, count);
            }
            catch (Exception) { return ""; }
        });
        string variable = "ERC_PIN_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, token);
        try
        {
            using var store = new OpenBaoErasureRootStore(new Uri($"https://127.0.0.1:{port}/"), _installation with { TrustedCaCertificatePem = pem }, "env:" + variable, TimeSpan.FromSeconds(5));
            try { await store.ReadHeadAsync("tenant", Token); }
            catch (HttpRequestException) { }
        }
        finally { Environment.SetEnvironmentVariable(variable, null); listener.Stop(); }
        return await server.WaitAsync(TimeSpan.FromSeconds(8), Token);
    }

    private static (X509Certificate2 Leaf, string CaPem) IssueCa(string name)
    {
        using var caKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var caRequest = new CertificateRequest("CN=" + name, caKey, HashAlgorithmName.SHA256);
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        caRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(caRequest.PublicKey, false));
        using var ca = caRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
        using var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var leafRequest = new CertificateRequest("CN=127.0.0.1", leafKey, HashAlgorithmName.SHA256);
        var san = new SubjectAlternativeNameBuilder();
        san.AddIpAddress(IPAddress.Loopback);
        leafRequest.CertificateExtensions.Add(san.Build());
        leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        using var issued = leafRequest.Create(ca, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), [1, 2, 3, 4, 5, 6, 7, 8]);
        return (issued.CopyWithPrivateKey(leafKey), ca.ExportCertificatePem());
    }
}
