namespace Hexalith.Platform.Protection.Tests;

/// <summary>One bounded owned-disposable transport diagnosis, separate from the five-case integration acceptance count.</summary>
public sealed class OpenBaoNativeTransportDiagnostics
{
    /// <summary>Gets whether the owned native harness explicitly enabled diagnostics.</summary>
    public static bool Enabled => OpenBaoErasureRootIntegrationTests.Enabled;
    [Fact(Skip = "Requires the owned disposable OpenBao native harness.", SkipUnless = nameof(Enabled))]
    public async Task BootstrapAndWrapOneRoot()
    {
        OpenBaoNativeCustodyFixture? fixture = null;
        try
        {
            fixture = await OpenBaoNativeCustodyFixture.CreateAsync(TestContext.Current.CancellationToken);
            var root = await fixture.RootAsync("diagnostic-root", token: TestContext.Current.CancellationToken);
            if (root is null) { throw new InvalidOperationException("ERC_NATIVE_STAGE:RootWrap"); }
            var original = await fixture.Native.ReadHeadAsync("tenant-a", TestContext.Current.CancellationToken)
                ?? throw new InvalidOperationException("ERC_NATIVE_STAGE:HeadRead");
            var token = TestContext.Current.CancellationToken;
            await fixture.AssertForbiddenPolicyOperationsAsync(token);
            var head = System.Text.Json.JsonSerializer.Deserialize<ErasureRootHead>(original.Bytes, ErasureRootCustody.Json)!;
            var wrapped = head.Targets[ErasureRootCustody.TargetId(root.Identity)].Wrapped;
            byte[] clear = await fixture.Native.DecryptAsync("tenant-a", wrapped, token);
            try { if (clear.Length != 32) { throw new InvalidOperationException("Exact native decrypt output required."); } }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(clear); }
            await Shouldly.Should.ThrowAsync<InvalidOperationException>(() => fixture.Native.DecryptAsync("tenant-a", wrapped with { Context = Convert.ToBase64String("wrong-complete-context"u8.ToArray()) }, token));
            if (await fixture.Native.ReadArchiveAsync("tenant-a", new string('F', 64), token) is not null)
            { throw new InvalidOperationException("Exact native absence response required."); }
            using (var invalidToken = new HttpRequestMessage(HttpMethod.Get, "v1/" + fixture.Installation.TransitMount + "/keys/epoch-a"))
            {
                invalidToken.Headers.Add("X-Vault-Token", "invalid-owned-token");
                using var denied = await fixture.Http.SendAsync(invalidToken, token);
                if (denied.StatusCode != System.Net.HttpStatusCode.Forbidden) { throw new InvalidOperationException("Per-request native token substitution was not denied."); }
            }
            if (await fixture.Native.ReadHeadAsync("tenant-a", token) is null)
            { throw new InvalidOperationException("Valid credential must remain usable after a genuine native denial."); }
            byte[] capacity = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { padding = new string('x', 2 * 1024 * 1024) });
            if (!await fixture.Native.CompareExchangeHeadAsync("tenant-a", original.Version, capacity, TestContext.Current.CancellationToken))
            { throw new InvalidOperationException("ERC_NATIVE_STAGE:HeadCapacity"); }
            Record("RootWrapped", null, fixture.Transport.PublicStages);
        }
        catch (Exception error) { Record("NativeFailure", error, fixture?.Transport.PublicStages ?? []); throw; }
        finally { fixture?.Dispose(); }
    }
    internal static void Record(string stage, Exception? error, IReadOnlyList<string> stages)
    {
        string? directory = Environment.GetEnvironmentVariable("ERC_NATIVE_DIAGNOSTIC_DIRECTORY");
        if (directory is null || !Path.IsPathFullyQualified(directory) || !directory.StartsWith("/tmp/erc-native-diagnostic-", StringComparison.Ordinal)
            || new DirectoryInfo(directory).LinkTarget is not null) { return; }
        var frame = error is null ? null : new System.Diagnostics.StackTrace(error, true).GetFrames()?.FirstOrDefault(value =>
            Path.GetFileName(value.GetFileName()) is "OpenBaoDisposableNativeHandler.cs" or "OpenBaoNativeCustodyFixture.cs" or "OpenBaoNativeTransportDiagnostics.cs");
        string? location = frame is null ? null : Path.GetFileName(frame.GetFileName()) + ":" + frame.GetFileLineNumber().ToString(System.Globalization.CultureInfo.InvariantCulture);
        using var output = new FileStream(Path.Combine(directory, "native-stage.json"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        System.Text.Json.JsonSerializer.Serialize(output, new { stage, exceptionType = error?.GetType().FullName, location, stages = stages.TakeLast(30).ToArray() });
    }
}
