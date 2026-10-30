using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Actors.Client;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Hexalith.EventStore.Contracts.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Configuration admission happens before any service graph or native operation can fall back to an unprotected provider.</summary>
public sealed class ErasureRootHostCompositionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PinnedConfiguredEnginePersistsSealedEventsAndMissingNativeDigestHasZeroEffects(bool digestAvailable)
    {
        string directory = Path.Combine(Path.GetTempPath(), "erc-host-graph-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); using var fixture = new ErasureRootEngineFixture();
        fixture.DigestAvailable = digestAvailable; var original = fixture.Enroll();
        try
        {
            (string Path, string Hash) Write(string name, byte[] bytes)
            { string path = System.IO.Path.Combine(directory, name); File.WriteAllBytes(path, bytes); return (path, Convert.ToHexString(SHA256.HashData(bytes))); }
            var inventory = Write("inventory.json", "{\"runtime\":{\"currentInventoryAuthorityReference\":\"inventory-authority\"}}"u8.ToArray());
            var copy = Write("copy.json", "{}"u8.ToArray());
            string credential = Path.Combine(directory, "private-bearer"); File.WriteAllText(credential, "component-private-bearer");
            var authority = new IndependentDaprAuthorityBinding("private-authority", "file:" + credential, key.ExportSubjectPublicKeyInfoPem(), "component", "tenant-a", "1");
            var host = new ErasureRootHostInstallation(new("erc-test", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant-a"] = "epoch" }),
                "https://erc.invalid/", "env:EXT_PROTECTION_LOOP4_COMPONENT_NATIVE", new() { ["tenant-a"] = "registry-1" }, authority, authority, authority, authority, authority,
                [new("tenant-a", "DeletionBatchCapabilitySigningKey", "key-1", "issuer", "audience", "anchor", "1", key.ExportSubjectPublicKeyInfo(), 1, false)], inventory.Path, inventory.Hash, copy.Path, copy.Hash);
            var pinned = Write("host.json", JsonSerializer.SerializeToUtf8Bytes(host));
            var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<IEventPayloadProtectionService, NoOpEventPayloadProtectionService>();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["EventStore:PayloadProtection:Enabled"] = "true", ["EventStore:PayloadProtection:InstallationFile"] = pinned.Path, ["EventStore:PayloadProtection:InstallationSha256"] = pinned.Hash }).Build();
            var absentServices = new ServiceCollection(); var absentHost = host with { CredentialReference = "env:LOOP4_MISSING_NATIVE_" + Guid.NewGuid().ToString("N").ToUpperInvariant() };
            var absentPin = Write("absent.json", JsonSerializer.SerializeToUtf8Bytes(absentHost));
            var absentConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["EventStore:PayloadProtection:Enabled"] = "true", ["EventStore:PayloadProtection:InstallationFile"] = absentPin.Path, ["EventStore:PayloadProtection:InstallationSha256"] = absentPin.Hash }).Build();
            Should.Throw<InvalidOperationException>(() => absentServices.AddPlatformErasureRootProtection(absentConfiguration)); absentServices.ShouldBeEmpty();
            Environment.SetEnvironmentVariable("EXT_PROTECTION_LOOP4_COMPONENT_NATIVE", "synthetic-component-native");
            ErasureRootHostComposition.AddPlatformErasureRootProtection(services, configuration, () => File.WriteAllText(inventory.Path, "{\"runtime\":{\"currentInventoryAuthorityReference\":\"substituted-authority\"}}"));
            services.Single(entry => entry.ServiceType == typeof(SecretsRuntimeConfiguration)).ImplementationInstance.ShouldBeOfType<SecretsRuntimeConfiguration>()
                .CurrentInventoryAuthorityReference.ShouldBe("inventory-authority");
            // Substitute only independently enrolled infrastructure boundaries; configured activation and persistence stay concrete.
            services.Replace(ServiceDescriptor.Singleton<IInteractionOccurrenceRegistry>(fixture.Registry));
            services.Replace(ServiceDescriptor.Singleton<IInteractionOccurrenceKeyProvider>(fixture.Keys));
            services.Replace(ServiceDescriptor.Singleton<IInteractionProtectionBindingProvider>(fixture.Bindings));
            await using var provider = services.BuildServiceProvider();
            var engine = provider.GetRequiredService<IEventPayloadProtectionService>(); engine.ShouldBeOfType<InteractionPayloadProtectionService>();
            var state = new InMemoryStateManager(); var persister = new EventPersister(state, provider.GetRequiredService<ILogger<EventPersister>>(), engine);
            var payload = Substitute.For<ISerializedEventPayload>(); payload.EventTypeName.Returns("Event.Type"); payload.SerializationFormat.Returns("json"); payload.EventContractType.Returns((string?)null);
            payload.PayloadBytes.Returns(JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Target = original.Binding.Identity.Target, Value = "configured-secret" } }));
            var owner = original.Binding.Identity.Owner; var command = new CommandEnvelope("01ARZ3NDEKTSV4RRFFQ69G5FAX", owner.TenantId, owner.Domain, owner.AggregateId, "Command.Type", "{}"u8.ToArray(), "correlation", null, "private", null);
            string before = ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a"));
            if (!digestAvailable)
            {
                await Should.ThrowAsync<InvalidOperationException>(() => persister.PersistEventsAsync(owner, owner.Domain, command, DomainResult.Success([payload]), "v1", TestContext.Current.CancellationToken));
                state.CommittedState.ShouldBeEmpty(); ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a")).ShouldBe(before);
            }
            else
            {
                var result = await persister.PersistEventsAsync(owner, owner.Domain, command, DomainResult.Success([payload]), "v1", TestContext.Current.CancellationToken);
                var envelope = result.PersistedEnvelopes.Single();
                System.Text.Encoding.UTF8.GetString(envelope.Payload).ShouldNotContain("configured-secret");
                envelope.SerializationFormat.ShouldBe("json+pdenc-v2");
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    [InlineData("true")]
    public void MissingOrDisabledInstallationRefusesStartupBeforeChangingServices(string? enabled)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IEventPayloadProtectionService>());
        var original = services.ToArray();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["EventStore:PayloadProtection:Enabled"] = enabled }).Build();
        var failure = Should.Throw<InvalidOperationException>(() => services.AddPlatformErasureRootProtection(configuration));
        failure.Message.ShouldStartWith("DependencyNotAvailable:EXT-PROTECTION-1:");
        services.ToArray().ShouldBe(original);
    }

    [Fact]
    public void UnpinnedInstallationCannotActivateDefaultProtection()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventStore:PayloadProtection:Enabled"] = "true",
            ["EventStore:PayloadProtection:InstallationFile"] = "/missing/protection-installation.json",
            ["EventStore:PayloadProtection:InstallationSha256"] = new string('0', 64),
        }).Build();
        Should.Throw<InvalidOperationException>(() => services.AddPlatformErasureRootProtection(configuration));
        services.ShouldBeEmpty();
    }
}
