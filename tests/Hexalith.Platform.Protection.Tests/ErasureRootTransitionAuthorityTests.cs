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
        (await _store.HeadAsync("tenant")).Anchors["owner-scope"].Completed.Count.ShouldBe(1);
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
        (await _store.HeadAsync("tenant")).Anchors["owner-scope"].Revision.ShouldBe(0);
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
        (await _store.HeadAsync("tenant")).Anchors["owner-scope"].Revision.ShouldBe(0);
    }
}
