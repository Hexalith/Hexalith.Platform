using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Current large registry-state framing and independently discoverable exclusive admission regressions.</summary>
public sealed class ErasureRootLoop2TransitionTests
{
    [Fact]
    public async Task CumulativeStatesCrossEightMiBAndSupportedBoundaryWithoutEmbeddingBytesInHead()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new ErasureRootStoreFixture(); var permissions = new ErasureRootAuthorizationFixture();
        var installation = new ErasureRootInstallation("erc", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant"] = "epoch" });
        string genesis = Convert.ToHexString(SHA256.HashData("genesis"u8)); store.Enroll(installation, "tenant", "scope", genesis);
        var authority = new ErasureRootTransitionAuthority(new(store, installation, permissions), permissions, "tenant", "scope");
        string digest = genesis; long revision = 0;
        foreach (int size in new[] { 4 * 1024 * 1024, 9 * 1024 * 1024, RecoverableAnchoredState.MaximumPendingBytes - 64 })
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { Records = new string('x', size) });
            var transition = new AnchoredStateTransition("scope", revision, revision + 1, digest, Convert.ToHexString(SHA256.HashData(bytes)), bytes);
            (await authority.AdmitTransitionAsync(transition, token)).ShouldBeTrue();
            (await store.ReadHeadAsync("tenant", token))!.Bytes.Length.ShouldBeLessThan(4096);
            var admitted = (await authority.ReadAdmittedOriginalAsync("scope", token))!; admitted.TargetBytes.ShouldBe(bytes);
            (await authority.ReadCurrentOriginalAsync("scope", token) is null).ShouldBe(revision == 0);
            (await authority.RecordTransitionAsync(transition, token)).ShouldBeTrue();
            (await authority.ReadCurrentOriginalAsync("scope", token))!.TargetBytes.ShouldBe(bytes);
            revision++; digest = transition.TargetDigest;
        }
        byte[] oversized = new byte[RecoverableAnchoredState.MaximumPendingBytes + 1];
        var rejected = new AnchoredStateTransition("scope", revision, revision + 1, digest, Convert.ToHexString(SHA256.HashData(oversized)), oversized);
        var before = (await store.ReadHeadAsync("tenant", token))!.Bytes;
        (await authority.AdmitTransitionAsync(rejected, token)).ShouldBeFalse();
        (await store.ReadHeadAsync("tenant", token))!.Bytes.ShouldBe(before);
    }
    [Fact]
    public async Task AdmissionLostBeforePrimaryPendingSaveIsDiscoverableReadOnlyAndOnlyExactRecoveryCanAdvance()
    {
        var token = TestContext.Current.CancellationToken; var store = new ErasureRootStoreFixture(); var permissions = new ErasureRootAuthorizationFixture();
        var installation = new ErasureRootInstallation("erc", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant"] = "epoch" });
        string genesis = RecoverableAnchoredState.Digest(1); store.Enroll(installation, "tenant", "scope", genesis);
        var authority = new ErasureRootTransitionAuthority(new(store, installation, permissions), permissions, "tenant", "scope");
        var original = RecoverableAnchoredState.Prepare("scope", 0, 1, 1, 2);
        await Should.ThrowAsync<IOException>(() => RecoverableAnchoredState.CommitAsync(original, authority, () => Task.FromResult<AnchoredStateTransition?>(null), _ => throw new IOException("lost primary pending write")));
        (await authority.ReadCurrentOriginalAsync("scope", token)).ShouldBeNull();
        var before = (await store.ReadHeadAsync("tenant", token))!.Bytes;
        (await authority.ReadAdmittedOriginalAsync("scope", token))!.TargetBytes.ShouldBe(original.TargetBytes);
        (await store.ReadHeadAsync("tenant", token))!.Bytes.ShouldBe(before);
        (await authority.AdmitTransitionAsync(RecoverableAnchoredState.Prepare("scope", 0, 1, 1, 3), token)).ShouldBeFalse();
        (await RecoverableAnchoredState.ReconcileAsync("scope", 1, null, value => value,
            value => authority.ValidateStateAsync(value == 1 ? 0 : 1, RecoverableAnchoredState.Digest(value), token), authority, Task.FromResult, true)).ShouldBe(2);
        (await authority.ValidateStateAsync(1, RecoverableAnchoredState.Digest(2), token)).ShouldBeTrue();
    }
}
