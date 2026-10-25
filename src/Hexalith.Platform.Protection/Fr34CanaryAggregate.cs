using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Shipped platform-owned canary domain. Its one native event follows normal EventStore protection, persistence and occurrence completion.</summary>
[Hexalith.EventStore.Client.Attributes.EventStoreDomain("fr34-canary")]
public sealed class Fr34CanaryAggregate : EventStoreAggregate<Fr34CanaryState>
{
    private static readonly DetachedStateCapture<Fr34CanaryState> Capture = new(512, static (state, token) => { token.ThrowIfCancellationRequested(); return state.Copy(); });
    /// <inheritdoc />
    protected override DetachedStateCapture<Fr34CanaryState> SnapshotCapture => Capture;
    /// <summary>Appends one original canary; identical retry is a no-op and conflicting identity is refused.</summary>
    public static DomainResult Handle(PersistFr34Canary command, Fr34CanaryState? state)
    {
        if (command.ProtectedContent.Value.Length is 0 or > 16384 || string.IsNullOrWhiteSpace(command.ProtectedContent.Target.AgentInteractionId))
            throw new InvalidOperationException("Bounded canary required.");
        if (state?.OriginalSha256 is { } original)
            return original == Fr34CanaryState.Digest(command.ProtectedContent) ? DomainResult.NoOp() : throw new InvalidOperationException("Changed canary original.");
        return DomainResult.Success([new Fr34CanaryPersisted(command.ProtectedContent)]);
    }
}
