using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

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
