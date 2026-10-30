using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Production admission refuses a missing transit key, a denied encrypt, and missing installed owner genesis before any source or head write.</summary>
public sealed class ErasureRootLoop8AdmissionTests
{
    [Fact]
    public async Task HealthyInstalledGenesisStillAdmitsAfterPositiveEncrypt()
    {
        var token = TestContext.Current.CancellationToken;
        using var fixture = new ErasureRootEngineFixture();
        using var independent = new ErasureRootIndependentSourceFixture();
        var source = Source(fixture, independent);
        var enrollment = Enrollment(fixture);
        (await fixture.Custody.PreflightWrapAsync(enrollment.RootIdentity, enrollment.RootOperationId, enrollment.ExpectedLifecycleRevision, token)).ShouldBeTrue();
        (await source.RegisterOriginalAsync(enrollment, token)).ShouldBeTrue();
        independent.Revision.ShouldBeGreaterThan(0);
        (await fixture.Custody.WrapAsync(enrollment.RootIdentity, enrollment.RootOperationId, enrollment.ExpectedLifecycleRevision, token)).ShouldNotBeNull();
        fixture.Store.CasCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task AbsentTransitKeyRefusesBeforeSourceOrHeadWrites()
    {
        var token = TestContext.Current.CancellationToken;
        using var fixture = new ErasureRootEngineFixture();
        fixture.Store.RemoveTransitKey("tenant-a");
        (await fixture.Store.ReadHeadAsync("tenant-a", token)).ShouldNotBeNull();
        await Refused(fixture, token);
    }

    [Fact]
    public async Task DeniedEncryptRefusesBeforeSourceOrHeadWrites()
    {
        var token = TestContext.Current.CancellationToken;
        using var fixture = new ErasureRootEngineFixture();
        var denied = new ErasureRootCustody(new ErasureRootEncryptDeniedStore(fixture.Store), fixture.Custody.Installation, fixture.Permissions);
        denied.RequireInstalledOwners([ErasureRootOccurrenceAuthority.Scope("tenant-a")]);
        (await fixture.Store.ReadPolicyAsync("tenant-a", token)).Derived.ShouldBeTrue();
        (await fixture.Store.ReadEpochAsync("tenant-a", token)).ShouldBeGreaterThan(0);
        await Refused(fixture, token, denied);
    }

    [Fact]
    public async Task MissingInstalledOwnerGenesisRefusesPreflightAndWrap()
    {
        var token = TestContext.Current.CancellationToken;
        using var fixture = new ErasureRootEngineFixture();
        string scope = ErasureRootOccurrenceAuthority.Scope("tenant-a");
        var head = await fixture.Store.HeadAsync("tenant-a");
        head.Anchors.Clear();
        fixture.Store.ReplaceHead("tenant-a", head);
        var enrollment = Enrollment(fixture);
        (await fixture.Custody.PreflightWrapAsync(enrollment.RootIdentity, enrollment.RootOperationId, enrollment.ExpectedLifecycleRevision, token)).ShouldBeFalse();
        (await fixture.Custody.WrapAsync(enrollment.RootIdentity, enrollment.RootOperationId, enrollment.ExpectedLifecycleRevision, token)).ShouldBeNull();
        (await fixture.Store.HeadAsync("tenant-a")).Anchors.ShouldBeEmpty();
        fixture.Store.CasCount.ShouldBe(0);
        var authority = new ErasureRootTransitionAuthority(fixture.Custody, fixture.Permissions, "tenant-a", scope);
        (await authority.ValidateStateAsync(0, ErasureRootCustody.Hash(new InteractionOccurrenceRegistrySnapshot("tenant-a", "registry-1", 0, [])), token)).ShouldBeFalse();
    }

    [Fact]
    public async Task StandaloneComponentWithoutRequiredOwnersStillWraps()
    {
        var token = TestContext.Current.CancellationToken;
        var installation = new ErasureRootInstallation("erc-test", "independent-enrollment", "erc-transit", "erc-heads", new Dictionary<string, string> { ["tenant-a"] = "epoch-a" });
        var store = new ErasureRootStoreFixture();
        store.Enroll(installation, "tenant-a");
        var custody = new ErasureRootCustody(store, installation, new ErasureRootAuthorizationFixture());
        var identity = new CustodyKeyObjectIdentity("tenant-a", "interaction-001", PlatformKeyPurpose.InteractionRootDek, "explicit-alias", "root-1", "lifecycle-1", installation.StoreId, "custody-1");
        (await custody.PreflightWrapAsync(identity, "wrap:interaction-001", 0, token)).ShouldBeTrue();
        (await custody.WrapAsync(identity, "wrap:interaction-001", 0, token)).ShouldNotBeNull();
    }

    private static ErasureRootNativeOccurrenceSource Source(ErasureRootEngineFixture fixture, ErasureRootIndependentSourceFixture independent, ErasureRootCustody? custody = null)
        => new(custody ?? fixture.Custody, fixture.Permissions, independent.Native, fixture.Digests, new Dictionary<string, ProtectionQualificationNativeCell> { ["tenant-a"] = new("independent", "root", new()) });

    private static ErasureRootOccurrenceEnrollment Enrollment(ErasureRootEngineFixture fixture)
        => fixture.Enroll() with { Writer = new("independent", "writer", new()) };

    private static async Task Refused(ErasureRootEngineFixture fixture, CancellationToken token, ErasureRootCustody? custody = null)
    {
        using var independent = new ErasureRootIndependentSourceFixture();
        var active = custody ?? fixture.Custody;
        var source = Source(fixture, independent, active);
        var enrollment = Enrollment(fixture);
        int cas = fixture.Store.CasCount;
        (await active.PreflightWrapAsync(enrollment.RootIdentity, enrollment.RootOperationId, enrollment.ExpectedLifecycleRevision, token)).ShouldBeFalse();
        (await source.RegisterOriginalAsync(enrollment, token)).ShouldBeFalse();
        var handler = new ErasureRootAuthorityOperationHandler(active, source, Substitute.For<IGovernanceScopeGuard>(), Substitute.For<IDeletionCapabilityRevocationAuthenticator>(), new string('A', 64), TimeProvider.System);
        using var receiver = new ReceivingAuthorityFixture("erc-source", handlers: [handler]);
        receiver.State = receiver.State with { Callers = [receiver.State.Callers[0] with { ScopeReference = "tenant-a|erc", Methods = [.. handler.Methods] }] };
        (await receiver.Authority.InvokeAsync<bool>("erc-register-source-original", new { tenantId = "tenant-a", enrollment }, token)).ShouldBeFalse();
        (await active.WrapAsync(enrollment.RootIdentity, enrollment.RootOperationId, enrollment.ExpectedLifecycleRevision, token)).ShouldBeNull();
        independent.Revision.ShouldBe(0);
        fixture.Store.CasCount.ShouldBe(cas);
        (await fixture.Store.HeadAsync("tenant-a")).SourceOriginals.ShouldBeEmpty();
    }
}
