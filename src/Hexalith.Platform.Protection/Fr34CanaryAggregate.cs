using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Isolated canary content envelope protected by the actual EventStore write pipeline.</summary>
public sealed record Fr34CanaryContent(string Value, ProtectionTarget Target);
/// <summary>Private canary command; no general application content or credentials.</summary>
public sealed record PersistFr34Canary(Fr34CanaryContent ProtectedContent);
/// <summary>Exact canary event, whose content value must be encrypted before native persistence.</summary>
public sealed record Fr34CanaryPersisted(Fr34CanaryContent ProtectedContent) : IEventPayload;
/// <summary>Value-free idempotency state; challenge bytes are never retained in snapshots.</summary>
public sealed class Fr34CanaryState
{
    /// <summary>Gets the original complete command digest.</summary>
    public string? OriginalSha256 { get; private set; }
    /// <summary>Applies the exact canary event.</summary>
    public void Apply(Fr34CanaryPersisted value) => OriginalSha256 = Digest(value.ProtectedContent);
    internal Fr34CanaryState Copy() => new() { OriginalSha256 = OriginalSha256 };
    internal static string Digest(Fr34CanaryContent value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
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
        if (command.ProtectedContent.Value.Length is 0 or > 16384 || command.ProtectedContent.Target.AgentInteractionId.Length != 64)
            throw new InvalidOperationException("Bounded canary required.");
        if (state?.OriginalSha256 is { } original)
            return original == Fr34CanaryState.Digest(command.ProtectedContent) ? DomainResult.NoOp() : throw new InvalidOperationException("Changed canary original.");
        return DomainResult.Success([new Fr34CanaryPersisted(command.ProtectedContent)]);
    }
}
