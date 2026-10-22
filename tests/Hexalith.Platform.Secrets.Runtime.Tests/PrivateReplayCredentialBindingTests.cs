using System.Security.Cryptography;
using System.Text;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Custody.Component;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

/// <summary>Replay stays unregistered when the credential is absent, an endpoint, or stale.</summary>
public sealed class PrivateReplayCredentialBindingTests
{
    [Fact]
    public async Task AbsentEndpointOrStaleCredentialInstallsNoRegistrarAndWritesNothing()
    {
        var services = new ServiceCollection();
        var invoker = new RecordingInvoker();
        SecretsRuntimeConfiguration ready = Fixtures.Ready();
        services.AddPrivateReplay(ready with { ReplayRegistrarCredentialReference = null }, invoker, new FixedClock(Fixtures.Now))
            .ShouldBe("runtime.replayRegistrarCredentialReference");
        services.AddPrivateReplay(ready with { ReplayRegistrarReference = "replay-app", ReplayRegistrarCredentialReference = "replay-app" }, invoker, new FixedClock(Fixtures.Now))
            .ShouldBe("runtime.replayRegistrarCredentialReference");
        services.AddPrivateReplay(ready with { ReplayRegistrarCredentialReference = "https://192.168.1.30:3500" }, invoker, new FixedClock(Fixtures.Now))
            .ShouldBe("runtime.replayRegistrarCredentialReference");
        PrivateReplayCredentialBinding.TryCapture("replay-app", "file:/mounted/replay", 0).ShouldBeNull();
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(ITrustedEnvelopeReplayRegistrar));
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(TrustedEnvelopeReplayVerifier));
        services.AddPrivateReplay(ready, invoker, new FixedClock(Fixtures.Now)).ShouldBeNull();
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(ITrustedEnvelopeReplayRegistrar));
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(TrustedEnvelopeReplayVerifier));
        PrivateReplayCredentialBinding? captured = PrivateReplayCredentialBinding.TryCapture("replay-app", "file:/mounted/replay", 1);
        captured.ShouldNotBeNull();
        var stale = new DaprReplayRegistrarAdapter(ready, invoker, new FixedClock(Fixtures.Now), captured.AsStale());
        var intent = new TrustedEnvelopeReplayIntent("issuer", new string('a', 64), "tenant-a", "command-1", "k1", new string('b', 64));
        (await stale.RegisterAsync(intent, Fixtures.Now, TimeSpan.FromDays(7), TestContext.Current.CancellationToken)).ShouldBeNull();
        (await stale.LookupAsync(intent, TestContext.Current.CancellationToken)).ShouldBeNull();
        invoker.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task SuppliedAuthorityReplacesFabricatedSigningLiteralsAndAMissingOneWritesNothing()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var authority = PrivateCustodyBindingAuthority.Create(key, "story-issuer", "story-audience", "tenant-a", "story-authority", 1);
        authority.ShouldNotBeNull();
        var identity = new CustodyKeyObjectIdentity("tenant-a", "interaction-0001", PlatformKeyPurpose.InteractionRootDek, "alias", "v1", "lifecycle", "store-1", "custody-lifecycle-v1");
        var pin = new CustodyKeyLifecycleRequest(identity, "pin-build", CustodyKeyLifecycleAction.Pin, 1, "hold-a", "fence-token", 2, "decision");
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("pin-build")));
        CustodyBindingDaprCall.Build(new("pin", true, pin, digest, null)).ShouldBeNull();
        CustodyBindingRequestV1? built = CustodyBindingDaprCall.Build(new("pin", true, pin, digest, null), authority);
        built.ShouldNotBeNull();
        built.Envelope.Proof.Issuer.ShouldBe("story-issuer");
        built.Envelope.Proof.Audience.ShouldBe("story-audience");
        built.Envelope.Enrollment.AuthorityId.ShouldBe("story-authority");
        built.Envelope.Proof.Signature.ShouldNotBe(new byte[] { 1, 2, 3 });
        CustodyBindingContract.Encode(built);
        var current = new PrivateCustodyBindingCurrentAuthority(authority);
        (await current.AuthorizeAsync(built, "app-id", TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await current.AuthorizeAsync(built with { Envelope = built.Envelope with { Proof = built.Envelope.Proof with { Signature = [9, 9, 9] } } }, "interaction-custody", TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await new PrivateCustodyBindingCurrentAuthority(null).AuthorizeAsync(built, "custody-lifecycle/pin", TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await current.AuthorizeAsync(built with { Envelope = built.Envelope with { TenantId = "tenant-b" } }, "custody-lifecycle/pin", TestContext.Current.CancellationToken)).ShouldBeFalse();
        var targets = new[] { new ProtectionTarget("tenant-a", "interaction-0009", "alias"), new ProtectionTarget("tenant-a", "interaction-0001", "alias") };
        var reversed = new CustodyOriginalManifest("reservation-order", "batch-order", DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), [identity with { ObjectId = "interaction-0009" }, identity]);
        CustodyBindingDaprCall.Build(new("destroy-manifest", true, null, digest, reversed), authority).ShouldBeNull();
        var orderedTargets = new[] { identity, identity with { ObjectId = "interaction-0009" } };
        var ordered = new CustodyOriginalManifest("reservation-order", "batch-order", DeletionBatchCapabilityIdentity.TargetManifestDigest(orderedTargets.Select(item => new ProtectionTarget(item.TenantId, item.ObjectId, item.KeyAlias)).ToArray()), orderedTargets);
        CustodyBindingRequestV1? manifest = CustodyBindingDaprCall.Build(new("destroy-manifest", true, null, digest, ordered), authority);
        manifest.ShouldNotBeNull();
        manifest.Manifest!.Original.DetachedJws.ShouldNotBe("original-detached-signature");
        manifest.Envelope.Enrollment.AuthorityId.ShouldNotBe("authority");
        authority.Verify(manifest.Manifest.Original.Capability, manifest.Manifest.Original.DetachedJws).ShouldBeTrue();
        authority.Sign(manifest.Manifest.Original.Capability, key).ShouldBe(manifest.Manifest.Original.DetachedJws);
        (await current.AuthorizeAsync(manifest, "custody-lifecycle/destroy-manifest", TestContext.Current.CancellationToken)).ShouldBeTrue();
        using ECDsa other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var changed = PrivateCustodyBindingAuthority.Create(other, "story-issuer", "story-audience", "tenant-a", "story-authority", 1);
        changed.ShouldNotBeNull();
        (await new PrivateCustodyBindingCurrentAuthority(changed).AuthorizeAsync(manifest, "custody-lifecycle/destroy-manifest", TestContext.Current.CancellationToken)).ShouldBeFalse();
        CustodyBindingDaprCall.Build(new("destroy-manifest", true, null, digest, ordered), authority.AsStale()).ShouldBeNull();
    }
}
