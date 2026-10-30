using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Loop-two bounded-head, immutable-original, capacity and actual-issued-holder regressions.</summary>
public sealed class ErasureRootLoop2CustodyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private readonly ErasureRootStoreFixture _store = new();
    private readonly ErasureRootAuthorizationFixture _authorization = new();
    private readonly ErasureRootCustody _custody;
    private readonly ErasureRootInstallation _installation = new("erc", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant"] = "epoch" });
    public ErasureRootLoop2CustodyTests()
    { _store.Enroll(_installation, "tenant"); _custody = new(_store, _installation, _authorization); }
    private Task<CustodyKeyRegistration?> Root(string id) => _custody.WrapAsync(new("tenant", id, PlatformKeyPurpose.InteractionRootDek, "alias", "1", "1", "erc", "1"), "wrap:" + id, 0, Token);
    private static DeletionBatchConsumptionRequest Request(CustodyKeyRegistration root, string batch)
    {
        ProtectionTarget[] targets = [new(root.Identity.TenantId, root.Identity.ObjectId, root.Identity.KeyAlias)];
        return new(new("issuer", "audience", "tenant", "request", "seal", "accepted", 0, batch, DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), "guard", 1, 1, 1, "key"), "component", 2, "dispatch", 3, targets);
    }
    [Fact]
    public async Task CounterfeitAndManuallyClearedLiveHolderCannotRetireActualIssuedInstance()
    {
        var root = (await Root("target"))!; var issued = (await _custody.UnwrapAsync(root, "holder", Token))!;
        using var counterfeit = new OwnedErasureRootKey(new byte[32], "holder"); counterfeit.Dispose();
        (await _custody.CompleteHolderAsync(root, counterfeit, Token)).ShouldBeFalse();
        CryptographicOperations.ZeroMemory(issued.Bytes);
        (await _custody.CompleteHolderAsync(root, issued, Token)).ShouldBeFalse();
        (await _custody.ConsumeAsync(Request(root, "batch"), "reservation", Token)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        issued.Dispose(); _custody.RetainDisposedHolder(root, issued);
        _authorization.Allowed = false; (await _custody.CompleteDisposedHoldersAsync(Token)).ShouldBe(0);
        _authorization.Allowed = true; (await _custody.CompleteDisposedHoldersAsync(Token)).ShouldBe(1);
        var otherOwner = new ErasureRootCustody(_store, _installation, _authorization);
        (await otherOwner.ConsumeAsync(Request(root, "batch"), "reservation", Token)).State.ShouldBe(DeletionManifestProviderState.Consumed);
    }
    [Fact]
    public async Task RepeatedSmallErasuresRetainExactOriginalsWithoutSurvivorPopulationsInHeadOrCompletedArchives()
    {
        var roots = new List<CustodyKeyRegistration>();
        for (int index = 0; index < 60; index++) { roots.Add((await Root("root-" + index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture)))!); }
        int initialBytes = (await _store.ReadHeadAsync("tenant", Token))!.Bytes.Length;
        var results = new List<(DeletionBatchConsumptionRequest Request, DeletionManifestProviderResult Result)>();
        for (int index = 0; index < 20; index++)
        {
            var request = Request(roots[index], "batch-" + index);
            var result = await _custody.ConsumeAsync(request, "reservation-" + index, Token);
            result.State.ShouldBe(DeletionManifestProviderState.Consumed); results.Add((request, result));
            var head = await _store.RetainedHeadAsync("tenant"); head.Pending.ShouldBeNull(); head.Manifests.ShouldBeEmpty(); head.ManifestArchives.Count.ShouldBe(index + 1);
            (await _store.ReadHeadAsync("tenant", Token))!.Bytes.Length.ShouldBeLessThan(initialBytes * 2);
            foreach (var pointer in head.ManifestArchives.Values)
            {
                var bytes = (await ErasureRootArchives.ReadBytesAsync(_store, "tenant", pointer, Token))!;
                var original = JsonSerializer.Deserialize<ErasureRootManifest>(bytes)!;
                original.Survivors.ShouldBeNull(); original.SurvivorArchive.ShouldBeNull(); original.Receipts.Length.ShouldBe(1);
            }
        }
        for (int index = 0; index < results.Count; index++)
        { ErasureRootCustody.Hash(await _custody.LookupAsync(results[index].Request, "reservation-" + index, Token)).ShouldBe(ErasureRootCustody.Hash(results[index].Result)); }
        _store.TrimCount.ShouldBe(20); (await _store.RetainedHeadAsync("tenant")).Targets.Count.ShouldBe(40);
    }
    [Fact]
    public async Task CapacityRefusalPrecedesPendingAdmissionRotationAndTrim()
    {
        var root = (await Root("near-limit"))!; var head = await _store.RetainedHeadAsync("tenant");
        int current = JsonSerializer.SerializeToUtf8Bytes(head).Length;
        head.QualificationAttempts.Add("padding", new string('x', OpenBaoErasureRootStore.MaximumHeadBytes - current - 64));
        _store.ReplaceHead("tenant", head);
        var before = (await _store.ReadHeadAsync("tenant", Token))!.Bytes;
        (await _custody.ConsumeAsync(Request(root, "too-full"), "reservation", Token)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        _store.RotateAttempts.ShouldBe(0); _store.TrimCount.ShouldBe(0);
        (await _store.ReadHeadAsync("tenant", Token))!.Bytes.ShouldBe(before); (await _store.RetainedHeadAsync("tenant")).Pending.ShouldBeNull();
    }
    [Fact]
    public async Task PreparedArchiveSubstitutionCannotAuthorizeTrimAndUnreferencedCompletionCannotAuthorizeLookup()
    {
        var root = (await Root("target"))!; _ = await Root("survivor"); var request = Request(root, "batch");
        var orphan = new ErasureRootManifest(request, "reservation", ErasureRootCustody.Hash(new { request, reservation = "reservation" }), 1, 2, null,
            [new(request.Targets[0], "batch", "orphan-receipt")]);
        _ = await ErasureRootArchives.WriteAsync(_store, "tenant", orphan, Token);
        (await _custody.LookupAsync(request, "reservation", Token)).State.ShouldBe(DeletionManifestProviderState.NotStarted);
        _store.Failure = "trim-before";
        (await _custody.ConsumeAsync(request, "reservation", Token)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        var head = await _store.RetainedHeadAsync("tenant"); var pending = head.Pending!; pending.SurvivorArchive.ShouldNotBeNull();
        var prepared = JsonSerializer.Deserialize<ErasureRootPreparedSurvivors>((await ErasureRootArchives.ReadBytesAsync(_store, "tenant", pending.SurvivorArchive!, Token))!)!;
        string wrong = await ErasureRootArchives.WriteAsync(_store, "tenant", prepared with { ReservationReceiptId = "substituted" }, Token);
        _store.ReplaceHead("tenant", head with { Pending = pending with { SurvivorArchive = wrong } });
        (await _custody.LookupAsync(request, "reservation", Token)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        _store.TrimCount.ShouldBe(0); (await _store.RetainedHeadAsync("tenant")).Erased.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthorizedPendingMetadataCannotConsumeReservedTerminalCapacityEvenAtTrimBoundary(bool prepared)
    {
        var roots = new List<CustodyKeyRegistration>();
        for (int index = 0; index < 80; index++) { roots.Add((await Root("capacity-" + index))!); }
        var request = Request(roots[0], "capacity-batch");
        _store.Failure = prepared ? "trim-before" : "rotate-before";
        (await _custody.ConsumeAsync(request, "reservation", Token)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        var journal = new ProtectionQualificationOperationJournal(_custody, _authorization);
        (await journal.UpdateAsync("tenant", "safe-metadata", "QualificationNativeResult", new string('A', 64),
            head => { head.QualificationAttempts.Add("safe", "retained"); return true; }, Token)).ShouldBeTrue();
        async Task RefuseCapacityStealingMetadata()
        {
            byte[] before = (await _store.ReadHeadAsync("tenant", Token))!.Bytes;
            int cas = _store.CasCount, rotations = _store.RotateCount, trims = _store.TrimCount;
            int proposedBytes = 0;
            await Should.ThrowAsync<InvalidOperationException>(() => journal.UpdateAsync("tenant", "oversized-metadata", "QualificationNativeResult", new string('B', 64), head =>
            {
                head.QualificationAttempts.Add("padding", new string('x', OpenBaoErasureRootStore.MaximumHeadBytes - before.Length - 64));
                var compact = head with { Pending = head.Pending! with { Survivors = null } };
                proposedBytes = JsonSerializer.SerializeToUtf8Bytes(compact).Length;
                return true;
            }, Token));
            proposedBytes.ShouldBeGreaterThan(OpenBaoErasureRootStore.MaximumHeadBytes - 128);
            proposedBytes.ShouldBeLessThanOrEqualTo(OpenBaoErasureRootStore.MaximumHeadBytes);
            (await _store.ReadHeadAsync("tenant", Token))!.Bytes.ShouldBe(before);
            _store.CasCount.ShouldBe(cas); _store.RotateCount.ShouldBe(rotations); _store.TrimCount.ShouldBe(trims);
        }
        await RefuseCapacityStealingMetadata();
        int boundaryChecks = 0;
        _store.BeforeTrim = async () => { boundaryChecks++; await RefuseCapacityStealingMetadata(); };
        (await _custody.LookupAsync(request, "reservation", Token)).State.ShouldBe(DeletionManifestProviderState.Consumed);
        boundaryChecks.ShouldBe(1); _store.TrimCount.ShouldBe(1);
        var final = await _store.RetainedHeadAsync("tenant"); final.Pending.ShouldBeNull(); final.Targets.Count.ShouldBe(79);
        final.QualificationAttempts.ShouldContainKeyAndValue("safe", "retained"); final.QualificationAttempts.ShouldNotContainKey("padding");
    }
}
