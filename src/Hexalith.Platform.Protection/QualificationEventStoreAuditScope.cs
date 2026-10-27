using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.Platform.Protection;

/// <summary>Exact independently enrolled native source, command-status and projection read identities for one case.</summary>
public sealed record QualificationEventStoreAuditScope(string TenantId,string EnrolledScopeSha256,IReadOnlyList<string> OriginalSuffixes,
    IReadOnlyList<string> WorkloadMessageIds,IReadOnlyList<AggregateIdentity> NativeStreams,IReadOnlyList<SubmitQueryRequest> ProjectionQueries,
    IReadOnlyList<QualificationDispatchOriginalReference>? DispatchOriginals=null);
