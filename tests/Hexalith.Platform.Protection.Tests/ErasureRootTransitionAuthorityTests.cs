using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Independent enrollment, original admission and rollback component checks.</summary>
public sealed class ErasureRootTransitionAuthorityTests
{
    private readonly ErasureRootInstallation _installation = new("erc-test", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant"] = "epoch" });
    private readonly ErasureRootStoreFixture _store = new();
    private readonly ErasureRootAuthorizationFixture _permissions = new();
    private readonly ErasureRootTransitionAuthority _authority;
    private readonly string _genesis = Convert.ToHexString(SHA256.HashData("genesis"u8));
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ErasureRootTransitionAuthorityTests()
    {
        _store.Enroll(_installation, "tenant", "owner-scope", _genesis);
        _authority = new(new(_store, _installation, _permissions), _permissions, "tenant", "owner-scope");
    }
    private AnchoredStateTransition Transition(string value = "next")
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
        return new("owner-scope", 0, 1, _genesis, Convert.ToHexString(SHA256.HashData(bytes)), bytes);
    }

    [Fact]
    public async Task OnlyEnrolledGenesisAndAdmittedExactOriginalCanAdvance()
    {
        (await _authority.ValidateStateAsync(0, _genesis, Token)).ShouldBeTrue();
        var transition = Transition();
        (await _authority.RecordTransitionAsync(transition, Token)).ShouldBeFalse();
        (await _authority.AdmitTransitionAsync(transition, Token)).ShouldBeTrue();
        (await _authority.ValidateStateAsync(0, _genesis, Token)).ShouldBeTrue();
        (await _authority.RecoverTransitionAsync(Transition("substituted"), Token)).ShouldBeFalse();
        (await _authority.RecordTransitionAsync(transition, Token)).ShouldBeTrue();
        (await _authority.VerifyTransitionAsync(transition, Token)).ShouldBeTrue();
        (await _authority.RecoverTransitionAsync(transition, Token)).ShouldBeTrue();
        (await _authority.ValidateStateAsync(0, _genesis, Token)).ShouldBeFalse();
        (await _authority.ValidateStateAsync(1, transition.TargetDigest, Token)).ShouldBeTrue();
        var retained = (await _store.RetainedHeadAsync("tenant")).Anchors["owner-scope"];
        retained.Completed.ShouldBeEmpty(); retained.Admissions.ShouldBeEmpty(); retained.ArchiveHead.ShouldNotBeNull();
    }

    [Fact]
    public async Task SustainedGrowingTransitionsKeepBoundedHeadAndRetainEveryExactOriginal()
    {
        var originals = new List<AnchoredStateTransition>(); string digest = _genesis;
        for (int index = 0; index < 160; index++)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(new string('x', (index + 1) * 512));
            var transition = new AnchoredStateTransition("owner-scope", index, index + 1, digest, Convert.ToHexString(SHA256.HashData(bytes)), bytes);
            (await _authority.AdmitTransitionAsync(transition, Token)).ShouldBeTrue();
            (await _authority.RecordTransitionAsync(transition, Token)).ShouldBeTrue();
            originals.Add(transition); digest = transition.TargetDigest;
            var raw = (await _store.ReadHeadAsync("tenant", Token))!;
            raw.Bytes.Length.ShouldBeLessThan(2048);
        }
        foreach (var transition in originals)
        { (await _authority.VerifyTransitionAsync(transition, Token)).ShouldBeTrue(); (await _authority.RecoverTransitionAsync(transition, Token)).ShouldBeTrue(); }
        var current = await _authority.ReadCurrentOriginalAsync("owner-scope", Token);
        current!.TargetBytes.ShouldBe(originals[^1].TargetBytes);
        (await _authority.ValidateStateAsync(0, _genesis, Token)).ShouldBeFalse();
    }

    [Fact]
    public async Task PrewrittenArchiveAndLostCasNeverEstablishCompletionBeforeCommittedAncestry()
    {
        var transition = Transition();
        (await _authority.AdmitTransitionAsync(transition, Token)).ShouldBeTrue();
        _store.Failure = "cas-before";
        (await _authority.RecordTransitionAsync(transition, Token)).ShouldBeFalse();
        (await _authority.VerifyTransitionAsync(transition, Token)).ShouldBeFalse();
        (await _authority.ReadCurrentOriginalAsync("owner-scope", Token)).ShouldBeNull();
        (await _authority.RecordTransitionAsync(transition, Token)).ShouldBeTrue();
        (await _authority.VerifyTransitionAsync(transition, Token)).ShouldBeTrue();
        var forged = Transition("uncommitted");
        var node = new ErasureRootTransitionArchive("owner-scope", null, forged);
        (await _store.CreateArchiveAsync("tenant", ErasureRootCustody.Hash(node), System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(node), Token)).ShouldBeTrue();
        (await _authority.VerifyTransitionAsync(forged, Token)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LegacyInHeadOriginalsMigrateOnlyWithCompleteExactAncestry(bool complete)
    {
        var first = Transition(); byte[] secondBytes = "legacy-second"u8.ToArray();
        var second = new AnchoredStateTransition("owner-scope", 1, 2, first.TargetDigest, Convert.ToHexString(SHA256.HashData(secondBytes)), secondBytes);
        var legacy = await _store.RetainedHeadAsync("tenant");
        var originals = new SortedDictionary<string, AnchoredStateTransition>(StringComparer.Ordinal) { [ErasureRootCustody.Hash(second)] = second };
        if (complete) { originals.Add(ErasureRootCustody.Hash(first), first); }
        legacy.Anchors["owner-scope"] = new(2, second.TargetDigest, new(originals, StringComparer.Ordinal), originals);
        _store.ReplaceHead("tenant", legacy);
        byte[] thirdBytes = "legacy-successor"u8.ToArray();
        var third = new AnchoredStateTransition("owner-scope", 2, 3, second.TargetDigest, Convert.ToHexString(SHA256.HashData(thirdBytes)), thirdBytes);
        (await _authority.AdmitTransitionAsync(third, Token)).ShouldBe(complete);
        if (complete)
        {
            (await _store.RetainedHeadAsync("tenant")).Anchors["owner-scope"].Completed.ShouldBeEmpty();
            (await _authority.VerifyTransitionAsync(first, Token)).ShouldBeTrue();
            (await _authority.RecordTransitionAsync(third, Token)).ShouldBeTrue();
            (await _authority.ReadCurrentOriginalAsync("owner-scope", Token))!.TargetBytes.ShouldBe(thirdBytes);
        }
        else
        {
            var retained = (await _store.RetainedHeadAsync("tenant")).Anchors["owner-scope"];
            retained.ArchiveHead.ShouldBeNull(); retained.Revision.ShouldBe(2);
        }
    }

    [Fact]
    public async Task MissingScopeAndWithdrawnOriginalPermissionCannotCreateAuthority()
    {
        var absent = new ErasureRootTransitionAuthority(new(_store, _installation, _permissions), _permissions, "tenant", "absent-scope");
        (await absent.ValidateStateAsync(0, _genesis, Token)).ShouldBeFalse();
        var transition = Transition();
        (await _authority.AdmitTransitionAsync(transition, Token)).ShouldBeTrue();
        _permissions.TransitionAllowed = false;
        (await _authority.RecoverTransitionAsync(transition, Token)).ShouldBeFalse();
        (await _store.RetainedHeadAsync("tenant")).Anchors["owner-scope"].Revision.ShouldBe(0);
        _permissions.TransitionAllowed = true;
        (await _authority.RecoverTransitionAsync(transition, Token)).ShouldBeTrue();
    }

    [Fact]
    public async Task CallerMutationOfAdmittedBytesAndCrossScopeSubstitutionAreRejected()
    {
        var transition = Transition();
        (await _authority.AdmitTransitionAsync(transition, Token)).ShouldBeTrue();
        transition.TargetBytes[0] ^= 1;
        (await _authority.RecordTransitionAsync(transition, Token)).ShouldBeFalse();
        (await _authority.RecordTransitionAsync(Transition() with { ScopeId = "foreign" }, Token)).ShouldBeFalse();
        (await _authority.RecordTransitionAsync(Transition() with { TargetRevision = 2 }, Token)).ShouldBeFalse();
        (await _store.RetainedHeadAsync("tenant")).Anchors["owner-scope"].Revision.ShouldBe(0);
    }
}
