using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Exact independently enrolled owner and native source identities for one qualification case.</summary>
public sealed record QualificationOwnerReadScope(string TenantId,string BatchId,string DeletionRequestId,DeletionBatchCapabilityV1? Signing,IReadOnlyList<AggregateIdentity> NativeStreams);
