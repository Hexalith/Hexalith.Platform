using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Hexalith.Platform.Secrets.Custody.Tests;

[CollectionDefinition("Permitted reference acquisition", DisableParallelization = true)]
public sealed class PermittedReferenceAcquisitionCollection;

[Collection("Permitted reference acquisition")]
public sealed class PermittedReferencesStartupTests
{
    private static string Valid(string tenant = "tenant-a") => new JsonObject
    {
        ["permittedReferences"] = new JsonArray(
            Row(tenant, "TrustedEnvelope"), Row(tenant, "ContentDigest"),
            Row("tenant-b", "TrustedEnvelope"), Row("tenant-b", "ContentDigest")),
    }.ToJsonString();

    private static JsonObject Row(string tenant, string purpose) => new()
    {
        ["tenantId"] = tenant, ["purpose"] = purpose,
        ["secretReference"] = "tenants/" + tenant + "/" + purpose + "/operational", ["secretField"] = "hmacKey",
    };

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ValidBoundedSnapshotPreservesExactScopesAndMetadata(bool bom, bool maximum)
    {
        string text = Valid();
        byte[] body = Encoding.UTF8.GetBytes(text + (maximum ? new string(' ', PermittedReferencesFileReader.MaximumBytes - Encoding.UTF8.GetByteCount(text)) : ""));
        if (bom) { body = [.. Encoding.UTF8.Preamble, .. body]; }
        var reader = new PermittedReferencesFileReader(_ => new ShortStream(body), TimeSpan.FromSeconds(2));
        JsonArray snapshot = await reader.ReadAsync("/fixture/permitted.json", TestContext.Current.CancellationToken);
        snapshot.Count.ShouldBe(4);
        snapshot[0]!["tenantId"]!.GetValue<string>().ShouldBe("tenant-a");
        snapshot[0]!["purpose"]!.GetValue<string>().ShouldBe("TrustedEnvelope");
        snapshot[0]!["secretField"]!.GetValue<string>().ShouldBe("hmacKey");
        snapshot[0]!["secretReference"]!.GetValue<string>().ShouldBe("tenants/tenant-a/TrustedEnvelope/operational");
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("oversized")]
    [InlineData("malformed")]
    [InlineData("utf8")]
    [InlineData("root-array")]
    [InlineData("duplicate-root")]
    [InlineData("duplicate-nested")]
    [InlineData("depth")]
    [InlineData("missing-array")]
    [InlineData("wrong-array")]
    [InlineData("three")]
    [InlineData("five")]
    [InlineData("duplicate-scope")]
    [InlineData("nonobject")]
    [InlineData("tenant-number")]
    [InlineData("tenant-empty")]
    [InlineData("purpose-case")]
    [InlineData("purpose-number")]
    [InlineData("purpose-unknown")]
    public async Task InvalidInputNeverPublishesPermittedReferences(string scenario)
    {
        byte[] bytes = Invalid(scenario);
        var reader = new PermittedReferencesFileReader(_ => new ShortStream(bytes), TimeSpan.FromSeconds(2));
        JsonArray published = [];
        var startup = new PermittedReferencesStartup("/fixture/permitted.json", published, reader);
        IOException error = await Should.ThrowAsync<IOException>(() => startup.StartingAsync(TestContext.Current.CancellationToken));
        error.InnerException.ShouldBeNull();
        published.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative.json")]
    [InlineData("/fixture/valid.json\0suffix")]
    public async Task InvalidLocatorNeverStartsAcquisition(string? path)
    {
        int opens = 0;
        var reader = new PermittedReferencesFileReader(_ => { opens++; throw new IOException(); }, TimeSpan.FromSeconds(2));
        await Should.ThrowAsync<IOException>(() => reader.ReadAsync(path, TestContext.Current.CancellationToken));
        opens.ShouldBe(0);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("oversized")]
    [InlineData("directory")]
    [InlineData("fifo")]
    [InlineData("device")]
    [InlineData("socket")]
    public async Task RealDescriptorRefusesUnavailableOrNonregularInput(string scenario)
    {
        using var files = new Files();
        string path = files.PathFor("input");
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        switch (scenario)
        {
            case "oversized": File.WriteAllBytes(path, new byte[PermittedReferencesFileReader.MaximumBytes + 1]); break;
            case "directory": Directory.CreateDirectory(path); break;
            case "fifo": MakeFifo(path, 0x180).ShouldBe(0); break;
            case "device": path = "/dev/zero"; break;
            case "socket": socket.Bind(new UnixDomainSocketEndPoint(path)); break;
        }
        var elapsed = Stopwatch.StartNew();
        await Should.ThrowAsync<IOException>(() => new PermittedReferencesFileReader().ReadAsync(path, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken));
        elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(4));
    }

    [Fact]
    public async Task ProjectedConfigMapFollowsParentAndDataLinksAndKeepsStartupSnapshotUntilRestart()
    {
        using var files = new Files();
        string first = files.PathFor("..first"), second = files.PathFor("..second");
        Directory.CreateDirectory(first); Directory.CreateDirectory(second);
        File.WriteAllText(Path.Combine(first, "permitted.json"), Valid());
        File.WriteAllText(Path.Combine(second, "permitted.json"), Valid("replacement"));
        Directory.CreateSymbolicLink(files.PathFor("..data"), first);
        File.CreateSymbolicLink(files.PathFor("permitted.json"), "..data/permitted.json");
        string parentLink = files.PathFor("var-run");
        Directory.CreateSymbolicLink(parentLink, files.Root);
        string path = Path.Combine(parentLink, "permitted.json");
        JsonArray published = [];
        await new PermittedReferencesStartup(path, published, new()).StartingAsync(TestContext.Current.CancellationToken);
        Directory.CreateSymbolicLink(files.PathFor("..next"), second);
        Rename(files.PathFor("..next"), files.PathFor("..data")).ShouldBe(0);
        published[0]!["tenantId"]!.GetValue<string>().ShouldBe("tenant-a");
        JsonArray restarted = [];
        await new PermittedReferencesStartup(path, restarted, new()).StartingAsync(TestContext.Current.CancellationToken);
        restarted[0]!["tenantId"]!.GetValue<string>().ShouldBe("replacement");
        Directory.Delete(parentLink);
    }

    [Fact]
    public async Task StartupSnapshotDrivesExactPermissionsAndCurrentMetadataReadiness()
    {
        using var files = new Files();
        string path = files.PathFor("permitted.json"); File.WriteAllText(path, Valid());
        PlatformSigningProfile profile = PlatformAcceptedEnvelopeTiming.Bind("fixture-current", "https://offline.example/authority", "ext-secrets-1-custody",
            DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5));
        var runtime = new SecretsRuntimeConfiguration { ProviderBinding = "store", HmacCredentialReference = "credential",
            CurrentInventoryAuthorityReference = "inventory", ProfileReference = "profile", ExpectedProfile = profile };
        JsonArray permitted = [];
        var authority = new CurrentProofs(profile);
        var binding = new CustodyBinding(runtime, authority, permitted, null, () => true);
        binding.Permits("tenant-a", PlatformHmacPurpose.ContentDigest).ShouldBeFalse();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.Configure<HostOptions>(options => options.ServicesStartConcurrently = true);
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Configuration["Custody:CallerIssuer"] = "https://offline.example/authority";
        builder.Services.AddPlatformCustody();
        CustodyHttp.AddAuthentication(builder.Services, builder.Configuration);
        builder.Services.AddSingleton<IHostedService>(new PermittedReferencesStartup(path, permitted, new()));
        await using WebApplication app = builder.Build();
        CustodyHttp.Map(app, binding);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using HttpResponseMessage ready = await client.GetAsync("/ready", TestContext.Current.CancellationToken);
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
        authority.Resolutions.ShouldBe(4);
        binding.Permits("tenant-a", PlatformHmacPurpose.ContentDigest).ShouldBeTrue();
        binding.Permits("Tenant-a", PlatformHmacPurpose.ContentDigest).ShouldBeFalse();
        binding.Permits("foreign", PlatformHmacPurpose.ContentDigest).ShouldBeFalse();
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RotationAfterProductionOpenKeepsTheHeldDescriptorSnapshot()
    {
        using var files = new Files();
        string first = files.PathFor("..first"), second = files.PathFor("..second");
        Directory.CreateDirectory(first); Directory.CreateDirectory(second);
        File.WriteAllText(Path.Combine(first, "permitted.json"), Valid());
        File.WriteAllText(Path.Combine(second, "permitted.json"), Valid("replacement"));
        Directory.CreateSymbolicLink(files.PathFor("..data"), first);
        File.CreateSymbolicLink(files.PathFor("permitted.json"), "..data/permitted.json");
        string path = files.PathFor("permitted.json");
        using var release = new ManualResetEventSlim();
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new PermittedReferencesFileReader(locator =>
        {
            Stream descriptor = PermittedReferencesFileReader.Open(locator);
            opened.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10))) { descriptor.Dispose(); throw new IOException("Fixture release unavailable."); }
            return descriptor;
        }, TimeSpan.FromSeconds(5));
        Task<JsonArray> reading = reader.ReadAsync(path, TestContext.Current.CancellationToken);
        try
        {
            await opened.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            Directory.CreateSymbolicLink(files.PathFor("..next"), second);
            Rename(files.PathFor("..next"), files.PathFor("..data")).ShouldBe(0);
            File.Delete(Path.Combine(first, "permitted.json")); // The unlinked old inode must remain readable through its descriptor.
        }
        finally { release.Set(); }
        (await reading)[0]!["tenantId"]!.GetValue<string>().ShouldBe("tenant-a");
        (await new PermittedReferencesFileReader().ReadAsync(path, TestContext.Current.CancellationToken))[0]!["tenantId"]!
            .GetValue<string>().ShouldBe("replacement");
    }

    [Fact]
    public async Task AcquisitionUsesOneTotalDeadlineAcrossOpenReadAndDisposal()
    {
        using var mount = new PhasedMount();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new PermittedReferencesFileReader(mount.Open, TimeSpan.FromSeconds(1), () => finished.TrySetResult());
        JsonArray published = [];
        Task startup = new PermittedReferencesStartup("/fixture/permitted.json", published, reader).StartingAsync(TestContext.Current.CancellationToken);
        try
        {
            for (int phase = 0; phase < 3; phase++)
            {
                await mount.Entered[phase].Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
                await Task.Delay(400, TestContext.Current.CancellationToken);
                mount.Release[phase].Set();
            }
            await Should.ThrowAsync<IOException>(() => startup.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            published.ShouldBeEmpty();
        }
        finally { foreach (ManualResetEventSlim release in mount.Release) { release.Set(); } }
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        published.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("open", false)]
    [InlineData("read", false)]
    [InlineData("dispose", false)]
    [InlineData("open", true)]
    [InlineData("read", true)]
    [InlineData("dispose", true)]
    public async Task StalledAcquisitionRetainsProcessWorkerThroughActualCompletion(string phase, bool cancel)
    {
        if (Environment.GetEnvironmentVariable("DOTNET_ThreadPool_ForceMaxWorkerThreads") == "2")
        { ThreadPool.GetMaxThreads(out int maximum, out _); maximum.ShouldBe(2); }
        using var mount = new StalledMount(phase);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new PermittedReferencesFileReader(mount.Open, TimeSpan.FromSeconds(cancel ? 15 : 1),
            () => finished.TrySetResult(), () => observed.TrySetResult());
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        JsonArray published = [];
        var startup = new PermittedReferencesStartup("/fixture/permitted.json", published, reader);
        await using WebApplication app = TestHost(startup);
        Task starting = app.StartAsync(cancellation.Token);
        try
        {
            await mount.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (cancel)
            {
                cancellation.Cancel();
                await Should.ThrowAsync<OperationCanceledException>(() => starting.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
            }
            else { await Should.ThrowAsync<IOException>(() => starting.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken)); }
            app.Urls.ShouldBeEmpty();
            published.ShouldBeEmpty();
            finished.Task.IsCompleted.ShouldBeFalse();
            for (int index = 0; index < 12; index++)
            {
                // Even a different reader instance must refuse without opening or queueing work.
                var another = new PermittedReferencesFileReader(_ => { mount.ExtraOpens++; return new ShortStream(Encoding.UTF8.GetBytes(Valid())); }, TimeSpan.FromSeconds(1));
                Task<JsonArray> refused = another.ReadAsync("/fixture/another.json", TestContext.Current.CancellationToken);
                refused.IsCompleted.ShouldBeTrue();
                await Should.ThrowAsync<IOException>(() => refused);
            }
            mount.ExtraOpens.ShouldBe(0);
        }
        finally { mount.Release.Set(); }
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        published.ShouldBeEmpty(); // Late successful I/O cannot resume the failed startup.
        (await new PermittedReferencesFileReader(_ => new ShortStream(Encoding.UTF8.GetBytes(Valid())), TimeSpan.FromSeconds(2))
            .ReadAsync("/fixture/recovery.json", TestContext.Current.CancellationToken)).Count.ShouldBe(4);
    }

    [Fact]
    public async Task PrecancelledStartupNeverAcquiresOrPublishes()
    {
        int opens = 0;
        var reader = new PermittedReferencesFileReader(_ => { opens++; throw new IOException(); }, TimeSpan.FromSeconds(2));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        JsonArray published = [];
        await Should.ThrowAsync<OperationCanceledException>(() => new PermittedReferencesStartup("/fixture/permitted.json", published, reader).StartingAsync(cancellation.Token));
        opens.ShouldBe(0); published.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("open", "immediate")]
    [InlineData("read", "immediate")]
    [InlineData("dispose", "immediate")]
    [InlineData("open", "timeout")]
    [InlineData("read", "timeout")]
    [InlineData("dispose", "timeout")]
    [InlineData("open", "cancel")]
    [InlineData("read", "cancel")]
    [InlineData("dispose", "cancel")]
    public async Task AcquisitionFaultIsObservedWithoutInputInDiagnostics(string phase, string mode)
    {
        string sentinel = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Fault(string stage)
        {
            if (stage != phase) { return; }
            entered.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(15))) { throw new IOException("Fixture release unavailable."); }
            throw new IOException(sentinel);
        }
        if (mode == "immediate") { release.Set(); }
        var reader = new PermittedReferencesFileReader(_ =>
        {
            Fault("open");
            return new FaultStream(Fault);
        }, TimeSpan.FromSeconds(mode == "cancel" ? 15 : 1), () => finished.TrySetResult(), () => observed.TrySetResult());
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        JsonArray published = [];
        Task pending = new PermittedReferencesStartup("/fixture/" + sentinel, published, reader).StartingAsync(cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (mode == "cancel") { cancellation.Cancel(); }
            Exception error = await Should.ThrowAsync<Exception>(() => pending.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
            error.ToString().Contains(sentinel, StringComparison.Ordinal).ShouldBeFalse();
            error.InnerException.ShouldBeNull();
            published.ShouldBeEmpty();
        }
        finally { release.Set(); }
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        (await new PermittedReferencesFileReader(_ => new MemoryStream(Encoding.UTF8.GetBytes(Valid())), TimeSpan.FromSeconds(2))
            .ReadAsync("/fixture/recovery.json", TestContext.Current.CancellationToken)).Count.ShouldBe(4);
        published.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("missing")]
    [InlineData("oversized")]
    [InlineData("malformed")]
    [InlineData("directory")]
    [InlineData("duplicate-scope")]
    public async Task ActualEntryPointGatesListenerAndReadinessOnAllowlist(string scenario)
    {
        using var files = new Files();
        string runtime = files.PathFor("runtime.json"), permitted = files.PathFor("permitted.json");
        File.WriteAllText(runtime, "{}"); // Incomplete runtime may start but must retain unavailable readiness.
        if (scenario == "valid") { File.WriteAllText(permitted, Valid()); }
        else if (scenario == "directory") { Directory.CreateDirectory(permitted); }
        else if (scenario != "missing") { File.WriteAllBytes(permitted, Invalid(scenario)); }
        using var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = files.Root };
        info.Environment.Clear();
        info.Environment["DOTNET_EnableDiagnostics"] = "0";
        info.Environment["Logging__LogLevel__Default"] = "Warning";
        info.ArgumentList.Add(typeof(CustodyBinding).Assembly.Location);
        info.ArgumentList.Add("--servicesStartConcurrently"); info.ArgumentList.Add("true");
        foreach (string argument in new[] { "--Custody:RuntimeReferenceFile", runtime, "--Custody:PermittedReferencesFile", permitted,
            "--Custody:CallerIssuer", "https://offline.example/authority", "--urls", "http://127.0.0.1:" + port }) { info.ArgumentList.Add(argument); }
        using Process process = Process.Start(info)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> errors = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:" + port), Timeout = TimeSpan.FromSeconds(1) };
        try
        {
            if (scenario == "valid")
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
                deadline.CancelAfter(TimeSpan.FromSeconds(8));
                bool alive = false;
                while (!process.HasExited && !alive)
                {
                    try { using HttpResponseMessage health = await client.GetAsync("/alive", deadline.Token); alive = health.StatusCode == HttpStatusCode.OK; }
                    catch (HttpRequestException) { await Task.Delay(25, deadline.Token); }
                }
                alive.ShouldBeTrue();
                using HttpResponseMessage ready = await client.GetAsync("/ready", TestContext.Current.CancellationToken);
                ready.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            }
            else
            {
                await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(8), TestContext.Current.CancellationToken);
                process.ExitCode.ShouldNotBe(0);
                await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync("/ready", TestContext.Current.CancellationToken));
                (await errors + await output).Contains("A valid bounded public permitted-reference file is required.", StringComparison.Ordinal).ShouldBeTrue();
            }
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); }
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            await Task.WhenAll(output, errors);
        }
    }

    private static WebApplication TestHost(IHostedService startup)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.Configure<HostOptions>(options => options.ServicesStartConcurrently = true);
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddSingleton(startup);
        WebApplication app = builder.Build();
        app.MapGet("/ready", () => "ready");
        return app;
    }

    private static byte[] Invalid(string scenario)
    {
        JsonObject document = JsonNode.Parse(Valid())!.AsObject();
        JsonArray rows = document["permittedReferences"]!.AsArray();
        switch (scenario)
        {
            case "empty": return [];
            case "oversized": return Encoding.UTF8.GetBytes(new string(' ', PermittedReferencesFileReader.MaximumBytes + 1));
            case "malformed": return "{"u8.ToArray();
            case "utf8": return [0xff, 0xfe, 0xff];
            case "root-array": return "[]"u8.ToArray();
            case "duplicate-root": return Encoding.UTF8.GetBytes(Valid().Insert(1, "\"permittedReferences\":[],"));
            case "duplicate-nested": return Encoding.UTF8.GetBytes(Valid().Replace("\"hmacKey\"", "\"hmacKey\",\"secretField\":\"other\"", StringComparison.Ordinal));
            case "depth": return Encoding.UTF8.GetBytes("{\"extra\":" + new string('[', 33) + "0" + new string(']', 33) + "}");
            case "missing-array": return "{}"u8.ToArray();
            case "wrong-array": document["permittedReferences"] = new JsonObject(); break;
            case "three": rows.RemoveAt(3); break;
            case "five": rows.Add(Row("tenant-c", "TrustedEnvelope")); break;
            case "duplicate-scope": rows[3] = rows[0]!.DeepClone(); break;
            case "nonobject": rows[0] = "bad"; break;
            case "tenant-number": rows[0]!["tenantId"] = 2; break;
            case "tenant-empty": rows[0]!["tenantId"] = ""; break;
            case "purpose-case": rows[0]!["purpose"] = "trustedenvelope"; break;
            case "purpose-number": rows[0]!["purpose"] = "0"; break;
            case "purpose-unknown": rows[0]!["purpose"] = "Signing"; break;
            default: throw new InvalidOperationException("Unknown fixture scenario.");
        }
        return Encoding.UTF8.GetBytes(document.ToJsonString());
    }

    private sealed class ShortStream(byte[] bytes) : MemoryStream(bytes)
    { public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, 17)]); }

    private sealed class CurrentProofs(PlatformSigningProfile profile) : IDaprPlatformHmacAuthority
    {
        internal int Resolutions;
        public PlatformSigningProfile? GetCurrentProfile(string reference) => profile;
        public ValueTask<DaprPlatformHmacInventory?> ResolveAsync(string reference, PlatformHmacScope scope, string? version, CancellationToken token)
        {
            Resolutions++;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return ValueTask.FromResult<DaprPlatformHmacInventory?>(new(reference, "fixture-revision", scope,
                new("fixture-current", PlatformHmacKeyState.Active, now.AddMinutes(-1), now.AddMinutes(1), null), "store", "credential",
                "exact-reference", "1", "hmacKey", now.AddSeconds(-1), now.AddSeconds(20)));
        }
    }

    private sealed class FaultStream(Action<string> fault) : MemoryStream(Encoding.UTF8.GetBytes(Valid()))
    {
        public override int Read(Span<byte> buffer) { fault("read"); return base.Read(buffer); }
        protected override void Dispose(bool disposing) { base.Dispose(disposing); fault("dispose"); }
    }

    private sealed class PhasedMount : IDisposable
    {
        internal readonly ManualResetEventSlim[] Release = [new(), new(), new()];
        internal readonly TaskCompletionSource[] Entered = Enumerable.Range(0, 3).Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
        private int _read;
        internal Stream Open(string path) { Hold(0); return new HeldStream(this); }
        private void Hold(int phase)
        {
            Entered[phase].TrySetResult();
            if (!Release[phase].Wait(TimeSpan.FromSeconds(10))) { throw new IOException("Fixture release unavailable."); }
        }
        private sealed class HeldStream(PhasedMount owner) : MemoryStream(Encoding.UTF8.GetBytes(Valid()))
        {
            public override int Read(Span<byte> buffer) { if (Interlocked.Increment(ref owner._read) == 1) { owner.Hold(1); } return base.Read(buffer); }
            protected override void Dispose(bool disposing) { owner.Hold(2); base.Dispose(disposing); }
        }
        public void Dispose() { foreach (ManualResetEventSlim release in Release) { release.Set(); release.Dispose(); } }
    }

    private sealed class StalledMount(string phase) : IDisposable
    {
        internal readonly ManualResetEventSlim Release = new();
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int ExtraOpens;
        internal Stream Open(string path) { Stall("open"); return new HeldStream(this); }
        private void Stall(string stage)
        {
            if (stage != phase) { return; }
            Entered.TrySetResult();
            if (!Release.Wait(TimeSpan.FromSeconds(15))) { throw new IOException("Fixture release unavailable."); }
        }
        private sealed class HeldStream(StalledMount owner) : MemoryStream(Encoding.UTF8.GetBytes(Valid()))
        {
            public override int Read(Span<byte> buffer) { owner.Stall("read"); return base.Read(buffer); }
            protected override void Dispose(bool disposing) { owner.Stall("dispose"); base.Dispose(disposing); }
        }
        public void Dispose() { Release.Set(); Release.Dispose(); }
    }

    private sealed class Files : IDisposable
    {
        internal string Root { get; } = Directory.CreateTempSubdirectory("custody-startup-").FullName;
        internal string PathFor(string name) => Path.Combine(Root, name);
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int MakeFifo([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint mode);
    [DllImport("libc", EntryPoint = "rename", SetLastError = true)]
    private static extern int Rename([MarshalAs(UnmanagedType.LPUTF8Str)] string source, [MarshalAs(UnmanagedType.LPUTF8Str)] string destination);
}
