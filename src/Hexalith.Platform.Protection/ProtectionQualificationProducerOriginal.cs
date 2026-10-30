using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Immutable ciphertext-bound source producer original, retained under private native permission after actual source/writer completion.</summary>
/// <param name="SourceIdentity">Exact original source cell/partition.</param><param name="Bindings">Complete original source bindings.</param><param name="SealedPayloadSha256">Exact persisted event or snapshot carrier bytes.</param>
/// <param name="CompletionDigest">Actual original engine completion identity.</param><param name="Completion">Exact native completion original.</param><param name="Producer">Actual producing executor engine and installed role.</param>
public sealed record ProtectionQualificationProducerOriginal(string SourceIdentity, InteractionProtectionBinding[] Bindings, string SealedPayloadSha256, string CompletionDigest, PayloadProtectionCompletionContext Completion, ProtectionQualificationProducerIdentity Producer)
{
    /// <summary>Actual source-owned physical writes and purposes, retained in the atomic producer original.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? CopyPurposes { get; init; }
    /// <summary>Exact executed role operation, complete wire-request digest and admitted native call; persisted atomically with source/writer/completion.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ProtectionQualificationNativeCall? Call { get; init; }
}
