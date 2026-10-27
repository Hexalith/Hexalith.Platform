using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Actual content-free independent canary measurement, retaining native source proof and exact production identity. Booleans come from byte comparisons in the collector, never request flags.</summary>
public sealed record Fr34MeasuredCanaryOriginal(Fr34CanaryReference Reference,string EngineName,string EngineRevision,NativeSourceProof NativeProof,
    long SourceRevision,string NativeEventDigest,int PersistedByteCount,int OriginalPlaintextByteCount,bool PersistedPlaintextAbsent,bool UnsealEqual,string? CommandOriginalProtocol=null);
