using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Routes exact signing requests to the published durable actor address; no general signing or approval interface is exposed.</summary>
public sealed class QualificationSigningActorRouter:IDeletionCapabilitySigningActor
{
    /// <inheritdoc />
    public Task<DeletionCapabilitySigningAvailability?> ReadAvailabilityAsync(DeletionCapabilitySigningReadinessRequest request)
        => ActorProxy.Create<IDeletionCapabilitySigningActor>(new ActorId(DeletionCapabilitySigningActor.GetReadinessActorId(request)), DeletionCapabilitySigningActor.ActorTypeName).ReadAvailabilityAsync(request);
    private static IDeletionCapabilitySigningActor Actor(DeletionBatchCapabilityV1 payload)=>ActorProxy.Create<IDeletionCapabilitySigningActor>(new ActorId(DeletionCapabilitySigningActor.GetActorId(payload)),DeletionCapabilitySigningActor.ActorTypeName);
    /// <inheritdoc />
    public Task<DeletionCapabilitySigningOutcome> SignAsync(DeletionBatchCapabilityV1 payload)=>Actor(payload).SignAsync(payload);
    /// <inheritdoc />
    public Task<DeletionCapabilitySigningOutcome> LookupAsync(DeletionBatchCapabilityV1 payload)=>Actor(payload).LookupAsync(payload);
    /// <inheritdoc />
    public Task<DeletionCapabilitySigningOutcome> ObsoleteUnissuedAsync(DeletionBatchCapabilityV1 payload)=>Actor(payload).ObsoleteUnissuedAsync(payload);
    /// <inheritdoc />
    public Task<DeletionCapabilityNoIssueProof?> ReadSuccessorProofAsync(DeletionBatchCapabilityV1 payload)=>Actor(payload).ReadSuccessorProofAsync(payload);
}
