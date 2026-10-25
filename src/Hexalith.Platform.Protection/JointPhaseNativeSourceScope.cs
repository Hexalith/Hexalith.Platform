using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.Platform.Protection;

/// <summary>Exact independently enrolled read identities and immutable target/phase binding for native source publication. No expected predicates or result flags are accepted.</summary>
public sealed record JointPhaseNativeSourceScope(string BindingDigest,string TargetCommit,int Phase,string TenantId,string BatchId,IReadOnlyList<AggregateIdentity> NativeStreams,IReadOnlyList<Hexalith.Platform.Custody.Fr34CanaryReference>? Fr34Canaries=null,IReadOnlyList<Hexalith.EventStore.Contracts.Security.DeletionBatchCapabilityV1>? SigningPayloads=null,IReadOnlyList<QualificationGuardStep>? GuardOperations=null);
