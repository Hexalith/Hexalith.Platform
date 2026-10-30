using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using Shouldly;

namespace Hexalith.Platform.Secrets.Authority.Tests;

public sealed class AdministratorChannelTests
{
    [Theory]
    [InlineData("missing-bearer", 401)]
    [InlineData("bad-signature", 401)]
    [InlineData("expired", 401)]
    [InlineData("query-audience", 401)]
    [InlineData("foreign-administrator", 403)]
    [InlineData("missing-operation", 403)]
    [InlineData("wrong-operation", 403)]
    public async Task RejectedAdministratorNeverAcquiresPrivateMount(string scenario, int expected)
    {
        int acquisitions = 0;
        var reader = new AdministratorChannelTokenReader(_ =>
        {
            Interlocked.Increment(ref acquisitions);
            throw new IOException("Mount unavailable.");
        }, TimeSpan.FromSeconds(2));
        await using var server = await AuthorityHttpFixture.Start(seeded: false, reader);
        // The old middleware returns 503 here before it ever validates the JWT.
        File.Delete(server.AdministratorFile);
        string? bearer = scenario switch
        {
            "missing-bearer" => null,
            "bad-signature" => server.Token(AuthorityTestState.Administrator, "hmac:administer", "ext-secrets-1-hmac-authority-admin", wrongSignature: true),
            "expired" => server.Token(AuthorityTestState.Administrator, "hmac:administer", "ext-secrets-1-hmac-authority-admin", expired: true),
            "query-audience" => server.Token(),
            "foreign-administrator" => server.Token("foreign", "hmac:administer", "ext-secrets-1-hmac-authority-admin"),
            "missing-operation" => server.Token(AuthorityTestState.Administrator, null, "ext-secrets-1-hmac-authority-admin"),
            _ => server.Token(AuthorityTestState.Administrator, "hmac:query", "ext-secrets-1-hmac-authority-admin"),
        };
        foreach (string path in new[] { "admin/state", "admin/operations" })
        {
            using HttpResponseMessage response = await Send(server, path, bearer);
            ((int)response.StatusCode).ShouldBe(expected);
        }
        acquisitions.ShouldBe(0);
        (await server.State.Repository.ReadAsync(TestContext.Current.CancellationToken)).Audit.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("directory")]
    [InlineData("empty")]
    [InlineData("whitespace")]
    [InlineData("bom-only")]
    [InlineData("oversized")]
    [InlineData("invalid-utf8")]
    [InlineData("fifo")]
    [InlineData("device")]
    public async Task InvalidRealMountDeniesAdministrationWithinBudget(string scenario)
    {
        await using var server = await AuthorityHttpFixture.Start(seeded: false);
        File.Delete(server.AdministratorFile);
        switch (scenario)
        {
            case "directory": Directory.CreateDirectory(server.AdministratorFile); break;
            case "empty": await File.WriteAllBytesAsync(server.AdministratorFile, [], TestContext.Current.CancellationToken); break;
            case "whitespace": await File.WriteAllTextAsync(server.AdministratorFile, " \r\n\t", TestContext.Current.CancellationToken); break;
            case "bom-only": await File.WriteAllBytesAsync(server.AdministratorFile, Encoding.UTF8.GetPreamble(), TestContext.Current.CancellationToken); break;
            case "oversized": await File.WriteAllBytesAsync(server.AdministratorFile, new byte[AdministratorChannelTokenReader.MaximumBytes + 1], TestContext.Current.CancellationToken); break;
            case "invalid-utf8": await File.WriteAllBytesAsync(server.AdministratorFile, [0xff], TestContext.Current.CancellationToken); break;
            case "fifo": MakeFifo(server.AdministratorFile, 0x180).ShouldBe(0); break;
            case "device": File.CreateSymbolicLink(server.AdministratorFile, "/dev/zero"); break;
        }
        using HttpResponseMessage response = await Send(server, "admin/operations", server.AdministratorToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await server.State.Repository.ReadAsync(TestContext.Current.CancellationToken)).Audit.ShouldBeEmpty();
        (await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ProjectedSecretAndParentSymlinksSupportFreshRotationAndAuthenticatedWrites()
    {
        await using var server = await AuthorityHttpFixture.Start(seeded: false);
        string root = server.State.DirectoryPath;
        string first = Path.Combine(root, "projection-1");
        string second = Path.Combine(root, "projection-2");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        await File.WriteAllTextAsync(Path.Combine(first, "token"), "offline-administrator-channel\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(second, "token"), "replacement-fixture-channel", TestContext.Current.CancellationToken);
        Directory.CreateSymbolicLink(Path.Combine(root, "..data"), first);
        string parentLink = Path.Combine(root, "run-link");
        Directory.CreateSymbolicLink(parentLink, root);
        File.Delete(server.AdministratorFile);
        File.CreateSymbolicLink(server.AdministratorFile, Path.Combine(parentLink, "..data", "token"));

        using (HttpResponseMessage state = await Send(server, "admin/state", server.AdministratorToken))
        { state.StatusCode.ShouldBe(HttpStatusCode.OK); }
        Directory.CreateSymbolicLink(Path.Combine(root, "..next"), second);
        Rename(Path.Combine(root, "..next"), Path.Combine(root, "..data")).ShouldBe(0);
        using (HttpResponseMessage stale = await Send(server, "admin/operations", server.AdministratorToken))
        { stale.StatusCode.ShouldBe(HttpStatusCode.Unauthorized); }
        using (HttpResponseMessage applied = await Send(server, "admin/operations", server.AdministratorToken, "replacement-fixture-channel"))
        { applied.StatusCode.ShouldBe(HttpStatusCode.OK); }
        (await server.State.Repository.ReadAsync(TestContext.Current.CancellationToken)).Audit.Length.ShouldBe(1);
        // Remove the parent loop before the fixture's recursive cleanup.
        Directory.Delete(parentLink);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task StalledAcquisitionOrDisposalRetainsAllWorkerSlotsUntilActualCompletion(bool stallOpen, bool stallDispose)
    {
        if (Environment.GetEnvironmentVariable("DOTNET_ThreadPool_ForceMaxWorkerThreads") == "2")
        { ThreadPool.GetMaxThreads(out int maximum, out _); maximum.ShouldBe(2); }
        using var mount = new StalledMount(stallOpen, stallDispose: stallDispose);
        var reader = new AdministratorChannelTokenReader(mount.Open, TimeSpan.FromSeconds(1));
        await using var server = await AuthorityHttpFixture.Start(seeded: true, reader);
        int initialAudit = (await server.State.Repository.ReadAsync(TestContext.Current.CancellationToken)).Audit.Length;
        string[] refusedIds = Enumerable.Range(0, AdministratorChannelTokenReader.MaximumWorkers).Select(index => "timed-out-" + index).ToArray();
        var elapsed = Stopwatch.StartNew();
        Task<HttpResponseMessage>[] pending = refusedIds
            .Select(id => Send(server, "admin/operations", server.AdministratorToken, operationId: id)).ToArray();
        try
        {
            foreach (HttpResponseMessage response in await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken))
            { using (response) { response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable); } }
            // One second acquisition plus at most two seconds for real HTTP/JWT scheduling.
            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(3));
            mount.AllEntered.Task.IsCompletedSuccessfully.ShouldBeTrue();
            // Timed-out kernel work still owns all capacity. Repeated requests must not spawn more work.
            for (int index = 0; index < 12; index++)
            {
                Task<bool> refused = reader.MatchesAsync("/fixture/token", "offline-administrator-channel", TestContext.Current.CancellationToken);
                refused.IsCompleted.ShouldBeTrue(); // Admission refusal is immediate, not another acquisition wait.
                await Should.ThrowAsync<IOException>(() => refused);
                using HttpResponseMessage saturated = await Send(server, "admin/state", server.AdministratorToken).WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
                saturated.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            }
            mount.Entered.ShouldBe(AdministratorChannelTokenReader.MaximumWorkers);
            using HttpResponseMessage unauthenticated = await Send(server, "admin/state", bearer: null);
            unauthenticated.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            using HttpResponseMessage query = await SendQuery(server);
            query.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await server.State.Repository.ReadAsync(TestContext.Current.CancellationToken)).Audit.Length.ShouldBe(initialAudit);
        }
        finally { mount.Release.Set(); }
        await mount.AllClosed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await Task.WhenAll(refusedIds.Select(server.RequestCompleted)).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        using HttpResponseMessage recovered = await Send(server, "admin/operations", server.AdministratorToken, operationId: "recovery-operation");
        recovered.StatusCode.ShouldBe(HttpStatusCode.OK);
        AuthorityState final = await server.State.Repository.ReadAsync(TestContext.Current.CancellationToken);
        final.Audit.Length.ShouldBe(initialAudit + 1);
        final.Audit.Any(receipt => refusedIds.Contains(receipt.OperationId)).ShouldBeFalse();
        final.Audit[^1].OperationId.ShouldBe("recovery-operation");
    }

    [Fact]
    public async Task CallerCancellationRetainsStalledWorkUntilActualCompletion()
    {
        using var mount = new StalledMount(stallOpen: false);
        var reader = new AdministratorChannelTokenReader(mount.Open, TimeSpan.FromSeconds(15));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task<bool>[] pending = Enumerable.Range(0, AdministratorChannelTokenReader.MaximumWorkers)
            .Select(_ => reader.MatchesAsync("/fixture/token", "offline-administrator-channel", cancellation.Token)).ToArray();
        try
        {
            await mount.AllEntered.Task.WaitAsync(TimeSpan.FromSeconds(8), TestContext.Current.CancellationToken);
            cancellation.Cancel();
            foreach (Task<bool> request in pending)
            { await Should.ThrowAsync<OperationCanceledException>(async () => await request.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)); }
            await Should.ThrowAsync<IOException>(() => reader.MatchesAsync("/fixture/token", "offline-administrator-channel", TestContext.Current.CancellationToken));
            mount.Entered.ShouldBe(AdministratorChannelTokenReader.MaximumWorkers);
        }
        finally { mount.Release.Set(); }
        await mount.AllClosed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        (await reader.MatchesAsync("/fixture/token", "offline-administrator-channel", TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task CancelledHttpAdministrationLeavesNoAuditAfterStalledOpenReturns()
    {
        using var mount = new StalledMount(stallOpen: true, expectedWorkers: 1);
        var reader = new AdministratorChannelTokenReader(mount.Open, TimeSpan.FromSeconds(15));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var server = await AuthorityHttpFixture.StartWithCancellation(seeded: false, reader, cancellation.Token);
        Task<HttpResponseMessage> pending = Send(server, "admin/operations", server.AdministratorToken, token: cancellation.Token, operationId: "cancelled-operation");
        try
        {
            await mount.AllEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            cancellation.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(async () => await pending.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        }
        finally { mount.Release.Set(); }
        await mount.AllClosed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await server.RequestCompleted("cancelled-operation").WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        (await server.State.Repository.ReadAsync(TestContext.Current.CancellationToken)).Audit.ShouldBeEmpty();
    }

    [Fact]
    public async Task PrecancelledAcquisitionNeverOpensMount()
    {
        int acquisitions = 0;
        var reader = new AdministratorChannelTokenReader(_ => { Interlocked.Increment(ref acquisitions); throw new IOException(); }, TimeSpan.FromSeconds(2));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => reader.MatchesAsync("/fixture/token", "offline-administrator-channel", cancellation.Token));
        acquisitions.ShouldBe(0);
    }

    [Fact]
    public async Task ByteLimitIncludesActualReadsAndAllowsExactlyTheMaximum()
    {
        var valid = new AdministratorChannelTokenReader(_ => new MemoryStream(Enumerable.Repeat((byte)'x', AdministratorChannelTokenReader.MaximumBytes).ToArray()), TimeSpan.FromSeconds(2));
        (await valid.MatchesAsync("/fixture/token", new string('x', AdministratorChannelTokenReader.MaximumBytes), TestContext.Current.CancellationToken)).ShouldBeTrue();
        var growing = new AdministratorChannelTokenReader(_ => new MemoryStream(new byte[AdministratorChannelTokenReader.MaximumBytes + 1]), TimeSpan.FromSeconds(2));
        await Should.ThrowAsync<IOException>(() => growing.MatchesAsync("/fixture/token", "offline-administrator-channel", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateMountFaultAfterTimeoutOrCancellationNeverReachesResponsesOrDiagnostics(bool cancel)
    {
        // Synthetic sensitive content is generated only in memory and never printed by assertions.
        string sentinel = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var faultObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int opens = 0;
        var reader = new AdministratorChannelTokenReader(_ =>
        {
            if (Interlocked.Increment(ref opens) == 1)
            {
                entered.TrySetResult();
                try
                {
                    if (!release.Wait(TimeSpan.FromSeconds(10))) { throw new IOException("Fixture release unavailable."); }
                    throw new IOException(sentinel);
                }
                finally { exited.TrySetResult(); }
            }
            return new MemoryStream(Encoding.UTF8.GetBytes("offline-administrator-channel"));
        }, TimeSpan.FromSeconds(cancel ? 15 : 1), () => faultObserved.TrySetResult());
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var server = await AuthorityHttpFixture.StartWithCancellation(seeded: false, reader, cancellation.Token);
        Task<HttpResponseMessage> pending = Send(server, "admin/operations", server.AdministratorToken,
            token: cancel ? cancellation.Token : null, operationId: "late-refused-operation");
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (cancel)
            {
                cancellation.Cancel();
                await Should.ThrowAsync<OperationCanceledException>(async () => await pending.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            }
            else
            {
                using HttpResponseMessage refused = await pending.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
                refused.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
                (await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Contains(sentinel, StringComparison.Ordinal).ShouldBeFalse();
            }
            await server.RequestCompleted("late-refused-operation").WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally { release.Set(); }
        await exited.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await faultObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        using HttpResponseMessage recovered = await Send(server, "admin/operations", server.AdministratorToken, operationId: "late-fault-recovery");
        recovered.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await recovered.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Contains(sentinel, StringComparison.Ordinal).ShouldBeFalse();
        AuthorityState final = await server.State.Repository.ReadAsync(TestContext.Current.CancellationToken);
        final.Audit.Length.ShouldBe(1);
        final.Audit[0].OperationId.ShouldBe("late-fault-recovery");
        server.Diagnostics.Contains(sentinel, StringComparison.Ordinal).ShouldBeFalse();
    }

    private static async Task<HttpResponseMessage> Send(AuthorityHttpFixture server, string path, string? bearer,
        string channel = "offline-administrator-channel", CancellationToken? token = null, string? operationId = null)
    {
        string id = operationId ?? Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(path == "admin/state" ? HttpMethod.Get : HttpMethod.Post, path);
        if (bearer is not null) { request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer); }
        request.Headers.Add("authority-admin-token", channel);
        request.Headers.Add("fixture-request-id", id);
        if (token is not null) { request.Headers.Add("fixture-cancellable", "true"); }
        if (request.Method == HttpMethod.Post)
        {
            AuthorityState state = await server.State.Repository.ReadAsync(TestContext.Current.CancellationToken);
            request.Content = JsonContent.Create(new AuthorityAdministration(id, state.Revision, "PublishProfile", Profile: server.State.Profile), options: AuthorityJson.Options);
        }
        return await server.Client.SendAsync(request, token ?? TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> SendQuery(AuthorityHttpFixture server)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "query-profile") { Content = JsonContent.Create(new { profileReference = AuthorityTestState.AppId }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", server.Token());
        request.Headers.Add("dapr-api-token", "offline-receiver-channel");
        return await server.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private sealed class StalledMount(bool stallOpen, int expectedWorkers = AdministratorChannelTokenReader.MaximumWorkers, bool stallDispose = false) : IDisposable
    {
        internal readonly ManualResetEventSlim Release = new(false);
        internal readonly TaskCompletionSource AllEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource AllClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _entered;
        private int _closed;
        internal int Entered => Volatile.Read(ref _entered);
        internal Stream Open(string path)
        {
            if (Interlocked.Increment(ref _entered) == expectedWorkers) { AllEntered.TrySetResult(); }
            if (stallOpen) { Wait(); }
            return new HeldStream(this);
        }
        private void BeforeRead() { if (!stallOpen && !stallDispose) { Wait(); } }
        private void BeforeDispose() { if (stallDispose) { Wait(); } }
        private void Closed()
        { if (Interlocked.Increment(ref _closed) == expectedWorkers) { AllClosed.TrySetResult(); } }
        private void Wait()
        { if (!Release.Wait(TimeSpan.FromSeconds(20))) { throw new IOException("Fixture mount did not release."); } }
        private sealed class HeldStream(StalledMount owner) : MemoryStream(Encoding.UTF8.GetBytes("offline-administrator-channel"))
        {
            public override int Read(Span<byte> buffer)
            { owner.BeforeRead(); return base.Read(buffer[..Math.Min(buffer.Length, 3)]); }
            protected override void Dispose(bool disposing)
            { owner.BeforeDispose(); base.Dispose(disposing); owner.Closed(); }
        }
        public void Dispose() { Release.Set(); Release.Dispose(); }
    }

    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int MakeFifo([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint mode);
    [DllImport("libc", EntryPoint = "rename", SetLastError = true)]
    private static extern int Rename([MarshalAs(UnmanagedType.LPUTF8Str)] string source, [MarshalAs(UnmanagedType.LPUTF8Str)] string destination);
}
