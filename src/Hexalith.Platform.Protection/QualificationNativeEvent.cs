using System.Security.Cryptography;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.Platform.Protection;

/// <summary>Content-free independently measured exact persisted native event fact.</summary>
public sealed record QualificationNativeEvent(long SourceRevision,string EventId,string EventTypeName,string PayloadSha256,string NativeEventDigest,string? ProtectionScheme,string? ProtectionKeyAlias,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? OccurredAt=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? PrincipalId=null);
