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
        var lostCompletion = new OpenBaoLostHeadAcknowledgement(fixture.Native);
        var owningCustody = new ErasureRootCustody(lostCompletion, fixture.Installation, new ErasureRootAuthorizationFixture());
        using var completedHolder = (await owningCustody.UnwrapAsync(erased, "native-lost-completion-holder", Token))!;
        completedHolder.Dispose(); owningCustody.RetainDisposedHolder(erased, completedHolder); lostCompletion.Armed = true;
        (await owningCustody.CompleteDisposedHoldersAsync(Token)).ShouldBe(0);
        byte[] before = await fixture.Native.DecryptAsync("tenant-a", old.Targets[ErasureRootCustody.TargetId(survivor.Identity)].Wrapped, Token);
        try
        {
            var request = OpenBaoNativeCustodyFixture.Manifest([erased]);
            (await fixture.Custody.ConsumeAsync(request, "native-reservation", Token)).State.ShouldBe(DeletionManifestProviderState.Consumed);
            (await owningCustody.CompleteDisposedHoldersAsync(Token)).ShouldBe(1);
            owningCustody.CompletionDiagnostics.Read().OutstandingCount.ShouldBe(0);
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

        await fixture.AssertForbiddenPolicyOperationsAsync(Token);
        // Substantial completed guard history uses immutable native archives; normal reads and holder completion stay bounded.
        var historyHead = (await fixture.Custody.ReadAsync("tenant-a", Token))!.Value;
        var targets = Array.Empty<GuardedStateMutation>();
        string lastIntent = "";
        for (int index = 0; index < 128; index++)
        {
            var transition = new GovernanceGuardTransition("tenant-a", "native-completed-guard-" + index, GovernanceGuardOperation.RegisterHold, index, "epoch", "request", null, null, null, null, null, null, "", "");
            string intent = ErasureRootCustody.Hash(new { Transition = transition, TargetMutationDigest = ErasureRootCustody.Hash(targets) });
            var receipt = new GovernanceProtocolReceipt(transition.OperationId, intent, "Stale", index + 1, 0, "", "native-guard-refusal-" + index);
            historyHead.Head.GuardAdmissions.Add(intent, new(transition, targets, index));
            historyHead.Head.GuardAttempts.Add(intent, new(transition, ErasureRootCustody.Hash(targets), index, index + 1, receipt));
            historyHead.Head.ConditionalHoldOriginals.Add("native-refusal-" + index, (System.Text.Json.Nodes.JsonObject)JsonSerializer.SerializeToNode(receipt)!);
            lastIntent = intent;
        }
        (await fixture.Custody.SaveAsync("tenant-a", historyHead.Version, historyHead.Head, Token)).ShouldBeTrue();
        int callsBefore = fixture.Transport.PublicStages.Count;
        var bounded = (await fixture.Custody.ReadAsync("tenant-a", Token))!.Value;
        (fixture.Transport.PublicStages.Count - callsBefore).ShouldBe(1);
        bounded.Head.GuardAttempts.ShouldBeEmpty(); bounded.Head.GuardAdmissions.ShouldBeEmpty(); bounded.Head.ConditionalHoldOriginals.ShouldBeEmpty();
        callsBefore = fixture.Transport.PublicStages.Count;
        (await fixture.Custody.ReadGuardAttemptAsync("tenant-a", bounded.Head, lastIntent, Token))!.Receipt.ReceiptId.ShouldBe("native-guard-refusal-127");
        (fixture.Transport.PublicStages.Count - callsBefore).ShouldBe(1);
        using (var historyHolder = await fixture.Custody.UnwrapAsync(root, "native-bounded-holder", Token))
        {
            historyHolder.ShouldNotBeNull(); historyHolder.Dispose();
            callsBefore = fixture.Transport.PublicStages.Count;
            (await fixture.Custody.CompleteHolderAsync(root, historyHolder, Token)).ShouldBeTrue();
            (fixture.Transport.PublicStages.Count - callsBefore).ShouldBeLessThan(12);
        }
        // Actual immutable archive transport runs in this same owned native case, with no synthetic archive provider.
        var legacyHead = (await fixture.Custody.ReadAsync("tenant-a", Token))!.Value;
        string legacyId = ErasureRootCustody.TargetId(root.Identity);
        legacyHead.Head.Targets[legacyId] = legacyHead.Head.Targets[legacyId] with { CompletedHolders = ["native-legacy-retired"] };
        (await fixture.Custody.SaveAsync("tenant-a", legacyHead.Version, legacyHead.Head, Token)).ShouldBeTrue();
        (await fixture.Custody.UnwrapAsync(root, "native-legacy-retired", Token)).ShouldBeNull();
        using var holder = await fixture.Custody.UnwrapAsync(root, "native-archive-holder", Token); holder.ShouldNotBeNull(); holder.Dispose();
        (await fixture.Custody.CompleteHolderAsync(root, holder, Token)).ShouldBeTrue();
        var held = (await fixture.Custody.ReadAsync("tenant-a", Token))!.Value;
        var retainedRoot = held.Head.Targets[ErasureRootCustody.TargetId(root.Identity)];
        retainedRoot.CompletedHolders.ShouldBeEmpty(); retainedRoot.CompletedHolderArchive.ShouldNotBeNull();
        byte[] holderBytes = (await fixture.Native.ReadArchiveAsync("tenant-a", retainedRoot.CompletedHolderArchive, Token))!;
        JsonSerializer.Deserialize<ErasureRootHolderArchive>(holderBytes)!.HolderId.ShouldBe(holder.HolderId);
        (await fixture.Custody.CompleteHolderAsync(root, holder, Token)).ShouldBeTrue();
        (await fixture.Custody.UnwrapAsync(root, holder.HolderId, Token)).ShouldBeNull();
        (await fixture.Custody.UnwrapAsync(root, "native-legacy-retired", Token)).ShouldBeNull();
        (await fixture.Native.ReadArchiveAsync("tenant-b", retainedRoot.CompletedHolderArchive, Token)).ShouldBeNull();

        const string scope = "tenant-a:native-archive-owner";
        string genesis = Convert.ToHexString(SHA256.HashData("native-genesis"u8));
        held.Head.Anchors.Add(scope, new(0, genesis, [], []));
        (await fixture.Custody.SaveAsync("tenant-a", held.Version, held.Head, Token)).ShouldBeTrue();
        var authority = new ErasureRootTransitionAuthority(fixture.Custody, new ErasureRootAuthorizationFixture(), "tenant-a", scope);
        byte[] firstBytes = "native-owner-state-one"u8.ToArray();
        var first = new AnchoredStateTransition(scope, 0, 1, genesis, Convert.ToHexString(SHA256.HashData(firstBytes)), firstBytes);
        (await authority.AdmitTransitionAsync(first, Token)).ShouldBeTrue(); (await authority.RecordTransitionAsync(first, Token)).ShouldBeTrue();
        byte[] secondBytes = "native-owner-state-two"u8.ToArray();
        var second = new AnchoredStateTransition(scope, 1, 2, first.TargetDigest, Convert.ToHexString(SHA256.HashData(secondBytes)), secondBytes);
        (await authority.AdmitTransitionAsync(second, Token)).ShouldBeTrue(); (await authority.RecordTransitionAsync(second, Token)).ShouldBeTrue();
        (await authority.VerifyTransitionAsync(first, Token)).ShouldBeTrue();
        (await authority.ReadCurrentOriginalAsync(scope, Token))!.TargetBytes.ShouldBe(secondBytes);
        var archived = (await fixture.Custody.ReadAsync("tenant-a", Token))!.Value.Head.Anchors[scope];
        var rawHead = JsonSerializer.Deserialize<ErasureRootHead>((await fixture.Native.ReadHeadAsync("tenant-a", Token))!.Bytes, ErasureRootCustody.Json)!;
        rawHead.Anchors[scope].Completed.ShouldBeEmpty(); rawHead.Anchors[scope].Admissions.ShouldBeEmpty();
        byte[] originalArchive = (await fixture.Native.ReadArchiveAsync("tenant-a", archived.ArchiveHead!, Token))!;
        (await fixture.Native.CreateArchiveAsync("tenant-a", archived.ArchiveHead!, originalArchive, Token)).ShouldBeTrue();
        (await fixture.Native.CreateArchiveAsync("tenant-a", archived.ArchiveHead!, "changed"u8.ToArray(), Token)).ShouldBeFalse();
        (await fixture.Native.ReadArchiveAsync("tenant-a", archived.ArchiveHead!, Token))!.ShouldBe(originalArchive);
        (await fixture.Native.ReadArchiveAsync("tenant-b", archived.ArchiveHead!, Token)).ShouldBeNull();
        // Current states above the former 8 MiB cliff use real bounded immutable native chunks, including exclusive admission discovery.
        byte[] largeBytes = JsonSerializer.SerializeToUtf8Bytes(new { Records = new string('x', 9 * 1024 * 1024) });
        var large = new AnchoredStateTransition(scope, 2, 3, second.TargetDigest, Convert.ToHexString(SHA256.HashData(largeBytes)), largeBytes);
        (await authority.AdmitTransitionAsync(large, Token)).ShouldBeTrue();
        (await authority.ReadAdmittedOriginalAsync(scope, Token))!.TargetBytes.ShouldBe(largeBytes);
        (await authority.VerifyTransitionAsync(large, Token)).ShouldBeFalse();
        (await authority.RecordTransitionAsync(large, Token)).ShouldBeTrue();
        (await authority.ReadCurrentOriginalAsync(scope, Token))!.TargetBytes.ShouldBe(largeBytes);
        (await authority.VerifyTransitionAsync(first, Token)).ShouldBeTrue();
        (await fixture.Native.ReadHeadAsync("tenant-a", Token))!.Bytes.Length.ShouldBeLessThan(65536);
        byte[] uncommittedBytes = "uncommitted-native-original"u8.ToArray();
        var uncommitted = new AnchoredStateTransition(scope, 2, 3, second.TargetDigest, Convert.ToHexString(SHA256.HashData(uncommittedBytes)), uncommittedBytes);
        _ = await ErasureRootArchives.WriteAsync(fixture.Native, "tenant-a", new ErasureRootTransitionArchive(scope, archived.ArchiveHead, uncommitted), Token);
        (await authority.VerifyTransitionAsync(uncommitted, Token)).ShouldBeFalse();

        // Exercise the independent Raft transaction bound with the actual native KV head and shipped restricted ACL.
        var headBefore = (await fixture.Native.ReadHeadAsync("tenant-a", Token))!;
        var largeHead = JsonSerializer.Deserialize<ErasureRootHead>(headBefore.Bytes, ErasureRootCustody.Json)!;
        largeHead.QualificationAttempts.Add("bounded-native-head-proof", new string('h', 9 * 1024 * 1024));
        byte[] headBytes = JsonSerializer.SerializeToUtf8Bytes(largeHead, ErasureRootCustody.Json);
        headBytes.Length.ShouldBeGreaterThan(8 * 1024 * 1024);
        (await fixture.Native.CompareExchangeHeadAsync("tenant-a", headBefore.Version, headBytes, Token)).ShouldBeTrue();
        var largeRead = (await fixture.Native.ReadHeadAsync("tenant-a", Token))!;
        ErasureRootCustody.Hash(JsonSerializer.Deserialize<ErasureRootHead>(largeRead.Bytes, ErasureRootCustody.Json)).ShouldBe(ErasureRootCustody.Hash(largeHead));
        largeRead.Bytes.Length.ShouldBeGreaterThan(8 * 1024 * 1024); largeRead.Version.ShouldBe(headBefore.Version + 1);
        (await fixture.Native.CompareExchangeHeadAsync("tenant-a", headBefore.Version, headBefore.Bytes, Token)).ShouldBeFalse();
        (await fixture.Native.CompareExchangeHeadAsync("tenant-a", largeRead.Version, headBefore.Bytes, Token)).ShouldBeTrue();
        foreach (int initial in new[] { 128 * 1024, 5 * 1024 * 1024 })
        {
            string growthScope = "tenant-a:native:history-" + initial + "|state";
            var enrolled = (await fixture.Custody.ReadAsync("tenant-a", Token))!.Value;
            enrolled.Head.Anchors.Add(growthScope, new(0, genesis, [], []));
            (await fixture.Custody.SaveAsync("tenant-a", enrolled.Version, enrolled.Head, Token)).ShouldBeTrue();
            var growing = new ErasureRootTransitionAuthority(fixture.Custody, new ErasureRootAuthorizationFixture(), "tenant-a", growthScope);
            long writeBytes = fixture.Transport.ArchiveWriteBytes; int writes = fixture.Transport.ArchiveWriteCount;
            var originals = new List<AnchoredStateTransition>(); string payload = Convert.ToHexString(RandomNumberGenerator.GetBytes(initial / 2)); string priorDigest = genesis;
            for (int index = 0; index < 8; index++)
            {
                if (index > 0) { payload = payload.Insert(index % 2 == 0 ? payload.Length / 2 : 0, Convert.ToHexString(RandomNumberGenerator.GetBytes(16 * 1024))); }
                byte[] stateBytes = JsonSerializer.SerializeToUtf8Bytes(new { Prefix = new string('p', index), Records = payload });
                var transition = new AnchoredStateTransition(growthScope, index, index + 1, priorDigest, Convert.ToHexString(SHA256.HashData(stateBytes)), stateBytes);
                (await growing.AdmitTransitionAsync(transition, Token)).ShouldBeTrue(); (await growing.RecordTransitionAsync(transition, Token)).ShouldBeTrue();
                originals.Add(transition); priorDigest = transition.TargetDigest;
            }
            long actualBytes = fixture.Transport.ArchiveWriteBytes - writeBytes;
            // Counts native JSON/base64 framing, chunk frames and logarithmic ancestry metadata as well as payload bytes.
            long supportedBytes = (initial + 7L * 32 * 1024) * 3 + 8L * 768 * 1024;
            actualBytes.ShouldBeLessThan(supportedBytes);
            foreach (int index in new[] { 0, 3, 7 }) { (await growing.VerifyTransitionAsync(originals[index], Token)).ShouldBeTrue(); }
            (await growing.ReadCurrentOriginalAsync(growthScope, Token))!.TargetBytes.ShouldBe(originals[^1].TargetBytes);
            Console.WriteLine(JsonSerializer.Serialize(new { nativeHistoryGeometry = initial, transitions = 8, archiveWriteBytes = actualBytes,
                archiveWrites = fixture.Transport.ArchiveWriteCount - writes, supportedBytes, nativeHeadBytes = headBytes.Length, restrictedRuntimePolicy = true }));
        }
    }
}
