using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Independent current permission reads bind real credential/workload identities and the single allowed read-only control invocation.</summary>
public sealed class ErasureRootObserverPermissionTests
{
    [Fact]
    public void PermissionFingerprintUsesTheActualCapturedDaprCredential()
    {
        using var client = new DaprClientBuilder().UseDaprApiToken("captured-read-only-channel").Build();
        ProtectionQualificationObserverPermissions.DaprCredential(client, "permission-owner").ShouldBe("captured-read-only-channel");
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("writable")]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("credential")]
    [InlineData("workload")]
    [InlineData("mutation-api")]
    public async Task ActualSignedObserverPermissionPathCannotRelabelWritableOrMismatchedCredentials(string mode)
    {
        var token = TestContext.Current.CancellationToken; using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>();
        var handler = new ErasureRootAuthorityOperationHandler(fixture.Custody, new(fixture.Custody, fixture.Permissions, native, fixture.Digests), Substitute.For<IGovernanceScopeGuard>(), Substitute.For<IDeletionCapabilityRevocationAuthenticator>(), new string('A', 64), TimeProvider.System);
        using var receiver = new ReceivingAuthorityFixture("erc-observer", handlers: [handler]);
        receiver.State = receiver.State with { Callers = [receiver.State.Callers[0] with { ScopeReference = "tenant-a|erc", Methods = ["erc-read-observer-permissions"] }] };
        var client = new DaprErasureRootAuthorization(receiver.Authority, fixture.Store, fixture.Custody.Installation);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var binding = receiver.Binding("permission-owner");
        var host = new ErasureRootHostInstallation(fixture.Custody.Installation, "https://native.invalid/", "env:NATIVE_OBSERVER", new() { ["tenant-a"] = "registry-1" }, binding, binding, binding, binding, binding, [], "inventory", new string('A', 64), "copy", new string('B', 64));
        var scope = new ProtectionQualificationServiceScope("observer", "installed-observer", "tenant-a", new string('c', 64), new("proof", "proof", new()), new(), new());
        string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        string tenant = Convert.ToHexStringLower(SHA256.HashData("tenant-a"u8));
        var proof = new ProtectionQualificationObserverPermission("tenant-a", "erc-test", "enrollment", "workload", "installed-observer", scope.BindingDigest,
            Hash("native-read-only"), Hash("dapr-read-only"), Hash("role-read-only"), "ReadPersistedStateOnly", ["heads/data/tenants/" + tenant, "heads/data/archives/" + tenant + "/*"], ["source"],
            ["state.get", "invoke:permission-owner/platform-independent/v1/erc-read-observer-permissions"],
            ["erc-head-write", "erc-archive-write", "transit-encrypt", "transit-decrypt", "transit-rewrap", "transit-rotate", "transit-trim", "dapr-state-write", "dapr-state-transaction", "dapr-invoke-mutation", "dapr-secret-read"],
            DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(20), "independently-installed-current-proof");
        proof = mode switch
        {
            "writable" => proof with { CredentialScope = "ExecutorWrites", DeniedCapabilities = [] },
            "expired" => proof with { ValidUntil = DateTimeOffset.UtcNow.AddSeconds(-1) },
            "credential" => proof with { NativeCredentialSha256 = Hash("executor-credential") },
            "workload" => proof with { WorkloadIdentity = "other-workload" },
            "mutation-api" => proof with { DaprApiScopes = [.. proof.DaprApiScopes, "state.transaction"] },
            _ => proof,
        };
        if (mode != "missing") { receiver.State = receiver.State with { QualificationCases = new() { ["erc.observer-permissions:workload"] = JsonSerializer.SerializeToElement(proof) } }; }
        string before = ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a"));
        var observed = await client.ReadObserverPermissionAsync("tenant-a", "workload", token);
        bool accepted = observed is not null && ProtectionQualificationObserverPermissions.Matches(observed, scope, host, "workload", "native-read-only", "dapr-read-only", "role-read-only", new HashSet<string> { "source" }, DateTimeOffset.UtcNow);
        accepted.ShouldBe(mode == "valid");
        ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a")).ShouldBe(before);
        receiver.Methods.Where(method => !method.StartsWith("status:", StringComparison.Ordinal)).ShouldAllBe(method => method == "erc-read-observer-permissions");
        (await receiver.Authority.InvokeAsync<ErasureRootPermissionEvidence>("erc-permission", new { tenantId = "tenant-a", identity = "no-mutation", operation = "WrapDek", requestDigest = new string('A', 64), original = new { tenantId = "tenant-a" } }, token)).ShouldBeNull();
    }
}
