using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Executable narrow actual OpenBao 2.6.2 integration. Enabled only by the separately owned disposable harness; absent inputs never become passing evidence.</summary>
public sealed class OpenBaoErasureRootIntegrationTests
{
    /// <summary>Gets whether the explicit owned-container native harness selected this assembly run.</summary>
    public static bool Enabled => Environment.GetEnvironmentVariable("ERC_NATIVE_DISPOSABLE_CONTAINER") is { Length: 64 }
        && Environment.GetEnvironmentVariable("ERC_NATIVE_DISPOSABLE_NONCE") is { Length: 32 }
        && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ERC_NATIVE_DISPOSABLE_TOKEN"));
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact(Skip = "Requires the owned disposable OpenBao native harness.", SkipUnless = nameof(Enabled))]
    public async Task TrimRefusesLeakedOldCiphertextAndRetainsUnrelatedRoot()
    {
        using var fixture = await OpenBaoNativeCustodyFixture.CreateAsync(Token);
        var erased = (await fixture.RootAsync("erased", token: Token))!; var survivor = (await fixture.RootAsync("survivor", token: Token))!;
        var old = JsonSerializer.Deserialize<ErasureRootHead>((await fixture.Native.ReadHeadAsync("tenant-a", Token))!.Bytes, ErasureRootCustody.Json)!;
        var oldWrapped = old.Targets[ErasureRootCustody.TargetId(erased.Identity)].Wrapped;
        byte[] before = await fixture.Native.DecryptAsync("tenant-a", old.Targets[ErasureRootCustody.TargetId(survivor.Identity)].Wrapped, Token);
        try
        {
            var request = OpenBaoNativeCustodyFixture.Manifest([erased]);
            (await fixture.Custody.ConsumeAsync(request, "native-reservation", Token)).State.ShouldBe(DeletionManifestProviderState.Consumed);
            await Should.ThrowAsync<InvalidOperationException>(() => fixture.Native.DecryptAsync("tenant-a", oldWrapped, Token));
            var current = JsonSerializer.Deserialize<ErasureRootHead>((await fixture.Native.ReadHeadAsync("tenant-a", Token))!.Bytes, ErasureRootCustody.Json)!;
            byte[] after = await fixture.Native.DecryptAsync("tenant-a", current.Targets[ErasureRootCustody.TargetId(survivor.Identity)].Wrapped, Token);
            try { CryptographicOperations.FixedTimeEquals(before, after).ShouldBeTrue(); } finally { CryptographicOperations.ZeroMemory(after); }
            (await fixture.Custody.WrapAsync(erased.Identity, "forbidden-reissue", 0, Token)).ShouldBeNull();
        }
        finally { CryptographicOperations.ZeroMemory(before); }
    }

    [Fact(Skip = "Requires the owned disposable OpenBao native harness.", SkipUnless = nameof(Enabled))]
    public async Task ThousandTargetManifestReturnsCompleteOrderedOriginalVector()
    {
        using var fixture = await OpenBaoNativeCustodyFixture.CreateAsync(Token); var roots = new List<CustodyKeyRegistration>();
        for (int index = 0; index < 1000; index++)
        {
            var root = await fixture.RootAsync("target-" + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), token: Token);
            if (root is null)
            {
                string stage = "RootWrapIndex" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                OpenBaoNativeTransportDiagnostics.Record(stage, null, fixture.Transport.PublicStages);
                throw new InvalidOperationException("ERC_NATIVE_STAGE:" + stage);
            }
            roots.Add(root);
        }
        var request = OpenBaoNativeCustodyFixture.Manifest(roots);
        var original = await fixture.Custody.ConsumeAsync(request, "thousand-native-reservation", Token);
        original.State.ShouldBe(DeletionManifestProviderState.Consumed); original.TargetReceipts.Count.ShouldBe(1000);
        original.TargetReceipts.Select(receipt => receipt.Target).ShouldBe(request.Targets);
        var looked = await fixture.Custody.LookupAsync(request, "thousand-native-reservation", Token);
        JsonSerializer.Serialize(looked).ShouldBe(JsonSerializer.Serialize(original));
        var head = JsonSerializer.Deserialize<ErasureRootHead>((await fixture.Native.ReadHeadAsync("tenant-a", Token))!.Bytes, ErasureRootCustody.Json)!;
        head.Targets.ShouldBeEmpty(); head.Erased.Count.ShouldBe(1000); head.Pending.ShouldBeNull();
    }

    [Fact(Skip = "Requires the owned disposable OpenBao native harness.", SkipUnless = nameof(Enabled))]
    public async Task PinRaceOrdersBeforeManifestOrLosesToTheCompleteManifest()
    {
        using var fixture = await OpenBaoNativeCustodyFixture.CreateAsync(Token);
        var root = (await fixture.RootAsync("held", token: Token))!; var other = (await fixture.RootAsync("other", token: Token))!;
        var pin = new CustodyKeyLifecycleRequest(root.Identity, "native-pin", CustodyKeyLifecycleAction.Pin, 0, "hold", "fence", 1, "fixture-decision");
        var request = OpenBaoNativeCustodyFixture.Manifest([root, other]);
        var pinning = fixture.Custody.ExecuteAsync(root, pin, ErasureRootCustody.Hash(pin), Token);
        var consuming = fixture.Custody.ConsumeAsync(request, "pin-race-reservation", Token);
        await Task.WhenAll(pinning, consuming);
        var pinResult = await pinning; var consumed = await consuming;
        var head = JsonSerializer.Deserialize<ErasureRootHead>((await fixture.Native.ReadHeadAsync("tenant-a", Token))!.Bytes, ErasureRootCustody.Json)!;
        if (pinResult.Status == CustodyKeyLifecycleStatus.Pinned)
        { consumed.State.ShouldBe(DeletionManifestProviderState.Unknown); head.Epoch.ShouldBe(1); head.Targets.Count.ShouldBe(2); head.Erased.ShouldBeEmpty(); }
        else { consumed.State.ShouldBe(DeletionManifestProviderState.Consumed); head.Epoch.ShouldBe(2); head.Targets.ShouldBeEmpty(); head.Erased.Count.ShouldBe(2); }
    }

    [Fact(Skip = "Requires the owned disposable OpenBao native harness.", SkipUnless = nameof(Enabled))]
    public async Task LostTrimAcknowledgementRecoversOnlyOriginalNativeEpochAndReceipt()
    {
        using var fixture = await OpenBaoNativeCustodyFixture.CreateAsync(Token); var root = (await fixture.RootAsync("lost", token: Token))!;
        var loss = new OpenBaoLostTrimAcknowledgement(fixture.Native);
        var custody = new ErasureRootCustody(loss, fixture.Installation, new ErasureRootAuthorizationFixture());
        var request = OpenBaoNativeCustodyFixture.Manifest([root]);
        (await custody.ConsumeAsync(request, "lost-native-reservation", Token)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        var recovered = await custody.LookupAsync(request, "lost-native-reservation", Token); recovered.State.ShouldBe(DeletionManifestProviderState.Consumed);
        (await fixture.Native.ReadEpochAsync("tenant-a", Token)).ShouldBe(2);
        JsonSerializer.Serialize(await custody.LookupAsync(request, "lost-native-reservation", Token)).ShouldBe(JsonSerializer.Serialize(recovered));
    }

    [Fact(Skip = "Requires the owned disposable OpenBao native harness.", SkipUnless = nameof(Enabled))]
    public async Task NativeTenantAndCompleteContextSubstitutionCannotDecrypt()
    {
        using var fixture = await OpenBaoNativeCustodyFixture.CreateAsync(Token); var root = (await fixture.RootAsync("isolated", token: Token))!;
        var head = JsonSerializer.Deserialize<ErasureRootHead>((await fixture.Native.ReadHeadAsync("tenant-a", Token))!.Bytes, ErasureRootCustody.Json)!;
        var wrapped = head.Targets[ErasureRootCustody.TargetId(root.Identity)].Wrapped;
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Native.DecryptAsync("tenant-b", wrapped, Token));
        var changed = wrapped with { Context = Convert.ToBase64String("wrong-complete-context"u8.ToArray()) };
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Native.DecryptAsync("tenant-a", changed, Token));
    }
}
