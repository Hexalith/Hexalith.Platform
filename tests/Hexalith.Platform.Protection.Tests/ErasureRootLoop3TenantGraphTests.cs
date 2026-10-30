using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Testing.Fakes;
using Microsoft.Extensions.Options;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Real configured tenant routing with isolated mounted bearers and the actual receiving HTTP boundary.</summary>
public sealed class ErasureRootLoop3TenantGraphTests
{
    [Fact]
    public async Task CompletePinnedGraphRoutesBothTenantsAndRefusesMissingOrSharedGrantBeforeEffects()
    {
        var token = TestContext.Current.CancellationToken;
        using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>();
        string directory = Path.Combine(Path.GetTempPath(), "erc-two-tenant-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var installation = fixture.Custody.Installation with { NativeKeys = new Dictionary<string, string> { ["tenant-a"] = "epoch-a", ["tenant-b"] = "epoch-b" } };
        fixture.Store.Enroll(installation, "tenant-b");
        var custody = new ErasureRootCustody(fixture.Store, installation, fixture.Permissions);
        var guard = Substitute.For<IGovernanceScopeGuard>();
        var handler = new ErasureRootAuthorityOperationHandler(custody, new(custody, fixture.Permissions, native, fixture.Digests), guard, Substitute.For<IDeletionCapabilityRevocationAuthenticator>(), new string('A', 64), TimeProvider.System);
        using var first = new ReceivingAuthorityFixture("erc-owner", handlers: [handler]); using var second = new ReceivingAuthorityFixture("erc-owner", handlers: [handler]);
        string bearerA = "independent-tenant-a", bearerB = "independent-tenant-b";
        first.State = first.State with { Callers = [first.State.Callers[0] with { BearerSha256 = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(bearerA))), ScopeReference = "tenant-a|erc", Methods = [.. handler.Methods] }], QualificationBindings = new() { ["erc.allow.WrapDek"] = "erc-owner" } };
        second.State = second.State with { Callers = [second.State.Callers[0] with { BearerSha256 = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(bearerB))), ScopeReference = "tenant-b|erc", Methods = [.. handler.Methods] }], QualificationBindings = new() { ["erc.allow.WrapDek"] = "erc-owner" } };
        await using var appA = IndependentAuthorityHttpApplication.BuildApplication(first.Receiver); await using var appB = IndependentAuthorityHttpApplication.BuildApplication(second.Receiver);
        appA.Urls.Add("http://127.0.0.1:0"); appB.Urls.Add("http://127.0.0.1:0");
        try
        {
            await appA.StartAsync(token); await appB.StartAsync(token);
            string Address(Microsoft.AspNetCore.Builder.WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            var addresses = new Dictionary<string, string> { ["authority-a"] = Address(appA), ["authority-b"] = Address(appB) };
            native.CreateInvokeMethodRequest(Arg.Any<HttpMethod>(), Arg.Any<string>(), Arg.Any<string>()).Returns(call => new HttpRequestMessage(call.Arg<HttpMethod>(), addresses[call.ArgAt<string>(1)] + "/" + call.ArgAt<string>(2)));
            native.CreateInvokableHttpClient(Arg.Any<string>()).Returns(_ => { var client = new HttpClient(); client.DefaultRequestHeaders.Add("dapr-api-token", "fixture-channel"); return client; });
            string credentialA = Path.Combine(directory, "tenant-a-bearer"), credentialB = Path.Combine(directory, "tenant-b-bearer");
            File.WriteAllText(credentialA, bearerA); File.WriteAllText(credentialB, bearerB);
            var a = first.Binding("authority-a") with { CredentialReference = "file:" + credentialA };
            var b = second.Binding("authority-b") with { CredentialReference = "file:" + credentialB };
            var bindings = new Dictionary<string, ErasureRootTenantAuthorityBindings> { ["tenant-a"] = new(a, a, a, a, a), ["tenant-b"] = new(b, b, b, b, b) };
            using var anchor = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            (string Path, string Hash) Write(string name, byte[] bytes) { string path = Path.Combine(directory, name); File.WriteAllBytes(path, bytes); return (path, Convert.ToHexString(SHA256.HashData(bytes))); }
            var inventory = Write("inventory.json", "{\"runtime\":{\"currentInventoryAuthorityReference\":\"inventory-authority\"}}"u8.ToArray()); var copy = Write("copy.json", "{}"u8.ToArray());
            var host = new ErasureRootHostInstallation(installation, "https://native.invalid/", "env:EXT_PROTECTION_LOOP4_COMPONENT_NATIVE", new() { ["tenant-a"] = "registry-a", ["tenant-b"] = "registry-b" }, a, a, a, a, a,
                installation.NativeKeys.Keys.Select(tenant => new PublishedDeletionCapabilityTrust(tenant, "DeletionBatchCapabilitySigningKey", "key", "issuer", "audience", "anchor", "1", anchor.ExportSubjectPublicKeyInfo(), 1, false)).ToArray(), inventory.Path, inventory.Hash, copy.Path, copy.Hash) { TenantAuthorities = bindings };
            IConfiguration Configuration(ErasureRootHostInstallation value)
            {
                var file = Write("host.json", JsonSerializer.SerializeToUtf8Bytes(value));
                return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["EventStore:PayloadProtection:Enabled"] = "true", ["EventStore:PayloadProtection:InstallationFile"] = file.Path, ["EventStore:PayloadProtection:InstallationSha256"] = file.Hash }).Build();
            }
            var absent = new ServiceCollection(); Should.Throw<InvalidOperationException>(() => absent.AddPlatformErasureRootProtection(Configuration(host with { TenantAuthorities = null }))); absent.ShouldBeEmpty();
            var shared = new ServiceCollection(); var sharedMap = new Dictionary<string, ErasureRootTenantAuthorityBindings>(bindings) { ["tenant-b"] = new(a, a, a, a, a) };
            Should.Throw<InvalidOperationException>(() => shared.AddPlatformErasureRootProtection(Configuration(host with { TenantAuthorities = sharedMap }))); shared.ShouldBeEmpty();
            foreach (var invalid in new[] { host.CapabilityTrust[0] with { Issuer = "" }, host.CapabilityTrust[0] with { PublicAnchorVersion = "" },
                host.CapabilityTrust[0] with { SubjectPublicKeyInfo = [] }, host.CapabilityTrust[0] with { SubjectPublicKeyInfo = new byte[] { 1, 2, 3 } },
                host.CapabilityTrust[0] with { TenantId = "Tenant-A" }, host.CapabilityTrust[0] with { TrustProfileRevision = 0 } })
            {
                var invalidServices = new ServiceCollection(); int effects = fixture.Store.CasCount;
                Should.Throw<InvalidOperationException>(() => invalidServices.AddPlatformErasureRootProtection(Configuration(host with { CapabilityTrust = [invalid, host.CapabilityTrust[1]] })));
                invalidServices.ShouldBeEmpty(); fixture.Store.CasCount.ShouldBe(effects);
            }
            using (var wrongCurve = ECDsa.Create(ECCurve.NamedCurves.nistP384))
            {
                var invalidServices = new ServiceCollection();
                Should.Throw<InvalidOperationException>(() => invalidServices.AddPlatformErasureRootProtection(Configuration(host with { CapabilityTrust = [host.CapabilityTrust[0] with { SubjectPublicKeyInfo = wrongCurve.ExportSubjectPublicKeyInfo() }, host.CapabilityTrust[1]] })));
                invalidServices.ShouldBeEmpty();
            }
            var duplicatedTrust = new ServiceCollection();
            Should.Throw<InvalidOperationException>(() => duplicatedTrust.AddPlatformErasureRootProtection(Configuration(host with { CapabilityTrust = [.. host.CapabilityTrust, host.CapabilityTrust[0]] })));
            duplicatedTrust.ShouldBeEmpty();
            var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(native); Environment.SetEnvironmentVariable("EXT_PROTECTION_LOOP4_COMPONENT_NATIVE", "synthetic-component-native"); services.AddPlatformErasureRootProtection(Configuration(host));
            services.Replace(ServiceDescriptor.Singleton<IErasureRootStore>(fixture.Store));
            await using var provider = services.BuildServiceProvider(); var permission = provider.GetRequiredService<IErasureRootAuthorization>();
            (await permission.AuthorizeAsync("tenant-a", "own-a", "WrapDek", new string('A', 64), token)).ShouldBeTrue();
            (await permission.AuthorizeAsync("tenant-b", "own-b", "WrapDek", new string('A', 64), token)).ShouldBeTrue();
            (await permission.AuthorizeAsync("tenant-c", "foreign", "WrapDek", new string('A', 64), token)).ShouldBeFalse();
            var source = provider.GetRequiredService<IErasureRootOccurrenceSource>();
            var original = fixture.Enroll();
            foreach (string tenant in installation.NativeKeys.Keys)
            {
                var enrollment = original with { Binding = original.Binding with { Identity = original.Binding.Identity with { Owner = new AggregateIdentity(tenant, "domain", "owner"), Target = original.Binding.Identity.Target with { TenantId = tenant } } }, RootIdentity = original.RootIdentity with { TenantId = tenant }, Digest = original.Digest with { InventoryKey = original.Digest.InventoryKey with { TenantId = tenant } } };
                var read = (await custody.ReadAsync(tenant, token))!.Value; read.Head.SourceOriginals.Add(ErasureRootOccurrenceBindings.Id(enrollment.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(enrollment.Binding.Identity)), enrollment);
                (await custody.SaveAsync(tenant, read.Version, read.Head, token)).ShouldBeTrue();
                ErasureRootCustody.Hash(await source.ResolveAsync(enrollment.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(enrollment.Binding.Identity), token)).ShouldBe(ErasureRootCustody.Hash(enrollment));
            }
            // Exercise the registered private actor through Dapr's configured DI activator, with exact tenant authority routing.
            provider.GetRequiredService<IOptions<ActorRuntimeOptions>>().Value.Actors
                .Single(entry => entry.Type.ActorTypeName == CustodyKeyLifecycleActor.ActorTypeName).Type.ImplementationType.ShouldBe(typeof(CustodyKeyLifecycleActor));
            string[] lifecyclePermissions = ["RetainLifetimeIndex", "MigrateLifetimeIndex", "WrapDek", "RegisterWrappedKey", "VerifyWrappedRegistration", "ValidateOwnerState", "ReadCurrentOwnerOriginal", "AdmitTransition", "RecordTransition", "RecoverTransition", "VerifyTransition", "ApplyKeyLifecycle", "LookupKeyLifecycle", "VerifyLifecycle", "VerifyLifecycleOutcome"];
            first.State = first.State with { QualificationBindings = lifecyclePermissions.ToDictionary(value => "erc.allow." + value, _ => "erc-owner") };
            second.State = second.State with { QualificationBindings = lifecyclePermissions.ToDictionary(value => "erc.allow." + value, _ => "erc-owner") };
            foreach (string tenant in installation.NativeKeys.Keys)
            {
                var read = (await custody.ReadAsync(tenant, token))!.Value;
                string lifecycle = ErasureRootTenantLifecycleAuthority.Scope(tenant);
                if (!read.Head.Anchors.ContainsKey(lifecycle))
                { read.Head.Anchors.Add(lifecycle, new(0, ErasureRootCustody.Hash(new CustodyKeyLifecycleLedger(tenant, 0, [])), [], [])); }
                foreach (string scope in new[] { DeletionConsumptionActor.GetOwnerScope(tenant), ErasureRootOccurrenceAuthority.Scope(tenant) })
                {
                    if (!read.Head.Anchors.ContainsKey(scope))
                    { read.Head.Anchors.Add(scope, new(0, ErasureRootCustody.Hash(new { tenant, scope }), [], [])); }
                }
                (await custody.SaveAsync(tenant, read.Version, read.Head, token)).ShouldBeTrue();
                var nativeCustody = provider.GetRequiredService<ErasureRootCustody>();
                var root = (await nativeCustody.WrapAsync(new(tenant, "lifecycle-root", PlatformKeyPurpose.InteractionRootDek, "alias", "root-v1", "lifecycle", installation.StoreId, "custody"), "lifecycle-wrap-" + tenant, 0, token))!;
                root.ShouldNotBeNull();
                var actorHost = ActorHost.CreateForTest<CustodyKeyLifecycleActor>(new ActorTestOptions { ActorId = new(CustodyKeyLifecycleActor.GetActorId(tenant)) });
                var activator = provider.GetRequiredService<ActorActivatorFactory>().CreateActivator(actorHost.ActorTypeInfo);
                var activated = await activator.CreateAsync(actorHost);
                try
                {
                    var actor = activated.Actor.ShouldBeOfType<CustodyKeyLifecycleActor>(); var backend = new InMemoryStateManager();
                    typeof(Actor).GetProperty("StateManager")!.SetValue(actor, backend);
                    (await actor.RegisterWrappedAsync(root)).Status.ShouldBe(CustodyKeyLifecycleStatus.Wrapped);
                    var stale = (CustodyKeyLifecycleLedger)backend.CommittedState["custody-key-lifecycle-candidate-v1"];
                    var pin = new CustodyKeyLifecycleRequest(root.Identity, "actor-pin-" + tenant, CustodyKeyLifecycleAction.Pin, stale.Revision, "held", "pin-fence", 1, "accepted-hold-policy");
                    var receiving = tenant == "tenant-a" ? first : second;
                    var grants = new Dictionary<string, string>(receiving.State.QualificationBindings!) { ["erc.fence.pin-fence"] = ErasureRootCustody.Hash(pin) };
                    receiving.State = receiving.State with { QualificationBindings = grants };
                    var pinnedOutcome = await actor.ApplyAsync(pin); pinnedOutcome.Status.ShouldBe(CustodyKeyLifecycleStatus.Pinned);
                    ErasureRootCustody.Hash(await actor.LookupAsync(pin)).ShouldBe(ErasureRootCustody.Hash(pinnedOutcome));
                    await backend.SetStateAsync("custody-key-lifecycle-candidate-v1", stale, token); await backend.SaveStateAsync(token);
                    int beforeOutage = fixture.Store.CasCount;
                    fixture.Store.ReadObserved = () => throw new IOException("Synthetic unavailable current-original authority.");
                    (await actor.LookupAsync(pin)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unavailable);
                    fixture.Store.CasCount.ShouldBe(beforeOutage); fixture.Store.TrimCount.ShouldBe(0);
                    fixture.Store.ReadObserved = null;
                    ErasureRootCustody.Hash(await actor.LookupAsync(pin)).ShouldBe(ErasureRootCustody.Hash(pinnedOutcome));
                    var active = (CustodyKeyLifecycleLedger)backend.CommittedState["custody-key-lifecycle-candidate-v1"];
                    var unpin = pin with { OperationId = "actor-unpin-" + tenant, Action = CustodyKeyLifecycleAction.Unpin, ExpectedRevision = active.Revision, FenceTokenId = "unpin-fence" };
                    receiving.State = receiving.State with { QualificationBindings = new Dictionary<string, string>(grants) { ["erc.fence.unpin-fence"] = ErasureRootCustody.Hash(unpin) } };
                    (await actor.ApplyAsync(unpin)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unpinned);
                    await Assert.ThrowsAsync<ArgumentException>(() => actor.ApplyAsync(pin with { Identity = pin.Identity with { TenantId = tenant == "tenant-a" ? "tenant-b" : "tenant-a" } }));
                    receiving.State = receiving.State with { QualificationBindings = new() };
                    (await actor.LookupAsync(pin)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unavailable);
                }
                finally { await activator.DeleteAsync(activated); }
            }
            fixture.Store.TrimCount.ShouldBe(0);
            var wrong = new DaprErasureRootAuthorization(new IndependentDaprAuthority(new DaprSecretsInvoker(native), a, TimeProvider.System), fixture.Store, installation);
            (await wrong.AuthorizeAsync("tenant-b", "wrong-grant", "WrapDek", new string('A', 64), token)).ShouldBeFalse();
        }
        finally { await appA.StopAsync(CancellationToken.None); await appB.StopAsync(CancellationToken.None); Directory.Delete(directory, true); }
    }
}
