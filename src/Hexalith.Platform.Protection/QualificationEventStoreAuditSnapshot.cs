using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Actual independently read native originals/statuses/streams/projections. Missing log/trace/physical/control protocols are explicit; this is not a fabricated complete trace.</summary>
public sealed record QualificationEventStoreAuditSnapshot(string RequestId,IReadOnlyDictionary<string,IndependentOriginal> Originals,
    IReadOnlyList<CommandStatusQueryResponse> CommandStatuses,IReadOnlyList<QualificationNativeStream> NativeStreams,IReadOnlyList<QualificationProjectionAudit> Projections,IReadOnlyList<string> MissingInputs);
