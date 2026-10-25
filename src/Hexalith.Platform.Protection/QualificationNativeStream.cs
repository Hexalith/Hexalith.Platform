using System.Security.Cryptography;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.Platform.Protection;

/// <summary>Complete bounded native stream observation under its actual source-v1 proof; no owner-provided result flag.</summary>
public sealed record QualificationNativeStream(AggregateIdentity Identity,NativeSourceProof Proof,IReadOnlyList<QualificationNativeEvent> Events);
