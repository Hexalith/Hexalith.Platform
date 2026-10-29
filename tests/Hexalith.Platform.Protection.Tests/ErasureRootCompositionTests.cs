using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Security;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Explicit provider composition resolves the actual engine and refuses a missing port before modifying registration.</summary>
public sealed class ErasureRootCompositionTests
{
    [Fact]
    public void ExplicitPortsResolveActualEngineWithoutUnregisteredDependencies()
    {
        var services = new ServiceCollection();
        var registry = Substitute.For<IInteractionOccurrenceRegistry>();
        var keys = Substitute.For<IInteractionOccurrenceKeyProvider>();
        var bindings = Substitute.For<IInteractionProtectionBindingProvider>();
        var manifests = Substitute.For<IAtomicDeletionManifestProvider>();
        var occurrences = Substitute.For<IInteractionOccurrenceAuthority>();
        var consumption = Substitute.For<IDeletionConsumptionAuthority>();
        services.AddInteractionPayloadProtection(registry, keys, bindings, manifests, occurrences, consumption);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IEventPayloadProtectionService>().ShouldBeOfType<InteractionPayloadProtectionService>();
        provider.GetRequiredService<IInteractionOccurrenceKeyProvider>().ShouldBeSameAs(keys);
        provider.GetRequiredService<IAtomicDeletionManifestProvider>().ShouldBeSameAs(manifests);
        provider.GetRequiredService<IDeletionConsumptionAuthority>().ShouldBeSameAs(consumption);
    }

    [Fact]
    public void MissingPortRefusesBeforeAnyRegistrationEffect()
    {
        var services = new ServiceCollection();
        Should.Throw<ArgumentNullException>(() => services.AddInteractionPayloadProtection(
            Substitute.For<IInteractionOccurrenceRegistry>(), Substitute.For<IInteractionOccurrenceKeyProvider>(),
            Substitute.For<IInteractionProtectionBindingProvider>(), null!, Substitute.For<IInteractionOccurrenceAuthority>(),
            Substitute.For<IDeletionConsumptionAuthority>()));
        services.ShouldBeEmpty();
    }
}
