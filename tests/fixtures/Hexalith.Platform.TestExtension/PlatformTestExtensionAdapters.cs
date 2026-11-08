#if HEXALITH_PLATFORM_EXTENSION_SDK
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;

[assembly: EventStoreExtension(Hexalith.Platform.TestExtension.PlatformTestExtensionAdapters.ExtensionId, Hexalith.Platform.TestExtension.PlatformTestExtensionAdapters.ApiMajor)]
[assembly: RouteCatalogAdapterSource(typeof(Hexalith.Platform.TestExtension.PlatformTestExtensionAdapters))]
#endif

namespace Hexalith.Platform.TestExtension;

/// <summary>The Platform-owned test extension's identity and its one trusted intent adapter.</summary>
public static class PlatformTestExtensionAdapters
{
    /// <summary>The declared extension identity.</summary>
    public const string ExtensionId = "platform-test";

    /// <summary>The implemented EventStore extension-API major.</summary>
    public const int ApiMajor = 1;

    /// <summary>The adapter identity contributed to the gateway's trusted adapter registry.</summary>
    public const string AdapterId = "platform-test:adapter";

#if HEXALITH_PLATFORM_EXTENSION_SDK
    private static int _creationCount;

    /// <summary>Gets how often the fixture's adapter factory was invoked.</summary>
    public static int CreationCount => Volatile.Read(ref _creationCount);

    /// <summary>Creates the complete adapter inventory; this does not register it or enable admission.</summary>
    /// <returns>The single probe adapter.</returns>
    public static IReadOnlyList<IDeclaredIdempotencyIntentAdapter> Create()
    {
        Interlocked.Increment(ref _creationCount);
        RouteContractDeclaration route = new("platform-test-probe", "command", "platform-test", nameof(PlatformTestProbeCommand), null,
            "v1", 1, "workload", EventStoreOperationIdentifiers.DomainServiceProcess, null, [],
            new(typeof(PlatformTestProbeCommand).FullName!, "clr-short-name-v1", [nameof(PlatformTestProbeCommand)], "domain-service", null, []));
        CommandIntentDeclaration intent = new(route, AdapterId, "platform-test:record-probe", "platform-test:policy-v1",
            new("platform-test:descriptor", 1, [new("target", "string"), new("payload-sha256", "string")]),
            IdempotencyReplayRetentionTier.Mutation, false);
        return [new DeclaredIdempotencyIntentAdapter<PlatformTestProbeCommand>(intent,
            payload => new Dictionary<string, object?> { ["payload-sha256"] = CanonicalIntentPayload.Sha256(payload) })];
    }
#endif
}
