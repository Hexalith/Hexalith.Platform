using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Sample.Greeting.Commands;

namespace Hexalith.EventStore.Sample.Greeting;

/// <summary>The existing SendGreeting wire name and its approved target-only Mutation policy.</summary>
public static class GreetingCatalogDeclarations
{
    /// <summary>Gets the owner policy; Greeting has no projection or query declaration.</summary>
    public static CommandIntentDeclaration Command { get; } = new(
        new("greeting-send-greeting", "command", "greeting", nameof(SendGreeting), null, "v1", 1,
            "workload", EventStoreOperationIdentifiers.DomainServiceProcess, null, [],
            new(typeof(SendGreeting).FullName!, "clr-short-name-v1", [nameof(SendGreeting)], "domain-service", null, [])),
        "hexalith:greeting:send-greeting:intent-v1", "greeting:send-greeting", "greeting:send-greeting:policy-v1",
        new("hexalith:greeting:send-greeting:descriptor-v1", 1, [new("target", "string")]), IdempotencyReplayRetentionTier.Mutation, false);
}
