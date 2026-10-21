using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Custody.Component;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Maps lifecycle calls onto the existing invoker. A missing logical role never reaches the client.</summary>
internal sealed class CustodyBindingDaprCall : ICustodyLifecycleBinding
{
    private readonly CustodyBindingDaprInvoker _invoker;

    internal CustodyBindingDaprCall(DaprClient client, IReadOnlyDictionary<string, string> logicalBindings, ICustodyBindingCurrentAuthority authority)
    {
        _invoker = new CustodyBindingDaprInvoker(client, logicalBindings, authority);
        Present = logicalBindings.TryGetValue(CustodyBindingContract.LogicalRole, out string? name) && CustodyBindingContract.Text(name);
    }

    /// <inheritdoc/>
    public bool Present { get; }

    /// <inheritdoc/>
    public async Task<CustodyLifecycleBindingReply> DispatchAsync(CustodyLifecycleBindingCommand command, CancellationToken cancellationToken)
    {
        if (!Present) { return new(CustodyBindingTransportState.Refused, null, false); }
        var request = Build(command);
        if (request is null) { return new(CustodyBindingTransportState.Refused, null, false); }
        var transport = await _invoker.InvokeAsync(request, TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
        // The invoker can only report ReceivedUnverified. That is not a witness receipt and it wrote nothing here.
        return new(transport.State, Outcome: null, Wrote: false);
    }

    internal static CustodyBindingRequestV1? Build(CustodyLifecycleBindingCommand command)
    {
        if (command.Operation is not ("pin" or "unpin" or "destroy-manifest" or "lookup-original")) { return null; }
        if (command.Operation is "pin" or "unpin" || command.Operation == "lookup-original" && command.Manifest is null)
        {
            if (command.Lifecycle is not { } lifecycle || lifecycle.HoldId is null) { return null; }
            var registration = new CustodyKeyRegistration(lifecycle.Identity, lifecycle.OperationId, lifecycle.ExpectedRevision, "kek-v1", "opaque-wrap", "wrap-receipt");
            var proof = Proof(command.Operation, lifecycle.Identity.TenantId, command.RequestDigest, command.RequestDigest, lifecycle.ExpectedRevision, lifecycle.HoldId,
                lifecycle.FenceRevision, lifecycle.FenceTokenId, lifecycle.DecisionVersion, unpin: lifecycle.Action == CustodyKeyLifecycleAction.Unpin, reservation: null, dispatchRevision: 0, dispatchReceipt: null);
            var envelope = Envelope(command.Operation, lifecycle.OperationId, command.RequestDigest, lifecycle.Identity.TenantId, lifecycle.Identity.StoreTarget, lifecycle.ExpectedRevision, proof);
            return Bind(new CustodyBindingRequestV1(envelope, registration, Lifecycle: lifecycle));
        }
        if (command.Manifest is not { } manifest) { return null; }
        var targets = manifest.Targets.Select(i => new ProtectionTarget(i.TenantId, i.ObjectId, i.KeyAlias)).ToArray();
        if (!Sorted(targets) || DeletionBatchCapabilityIdentity.TargetManifestDigest(targets) != manifest.ManifestDigest) { return null; }
        var capability = new DeletionBatchCapabilityV1("issuer", "audience", targets[0].TenantId, "delete-request", "seal", "manifest", 1,
            manifest.BatchId, manifest.ManifestDigest, "guard", 1, 1, 1, "capability-v1");
        var owner = new DeletionBatchConsumptionRequest(capability, "original-detached-signature", 2, "dispatch-receipt", 3, targets);
        var body = new CustodyBindingManifestV1(owner, DeletionBatchCapabilityIdentity.CanonicalPayload(capability), manifest.ReservationId);
        var manifestProof = Proof(command.Operation == "lookup-original" ? "lookup-original" : "destroy-manifest", targets[0].TenantId, manifest.ManifestDigest,
            command.RequestDigest, 1, null, 2, "issued-receipt", "decision", false, manifest.ReservationId, 3, "dispatch-receipt");
        var manifestEnvelope = Envelope(command.Operation == "lookup-original" ? "lookup-original" : "destroy-manifest", manifest.ReservationId, command.RequestDigest,
            targets[0].TenantId, manifest.Targets[0].StoreTarget, 1, manifestProof);
        return Bind(new CustodyBindingRequestV1(manifestEnvelope, Manifest: body));
    }

    private static CustodyBindingRequestV1 Bind(CustodyBindingRequestV1 request)
        => request with { Envelope = request.Envelope with { RequestDigest = CustodyBindingContract.OriginalRequestDigest(request) } };

    private static CustodyBindingEnvelopeV1 Envelope(string operation, string operationId, string digest, string tenant, string storeTarget, long revision, ControlAdmissionProofV1 proof)
        => new(CustodyBindingContract.Protocol, operation, operationId, digest, tenant, "owner|purpose",
            new AuthorityEnrollmentV1("authority", "incarnation-1", storeTarget, "profile", "trust-1", "anchors", digest),
            revision, proof.CheckpointDigest, "attempt-1", "challenge-1", proof);

    private static ControlAdmissionProofV1 Proof(string operation, string tenant, string target, string checkpoint, long revision, string? hold, long guardRevision, string? guardReceipt,
        string decision, bool unpin, string? reservation, long dispatchRevision, string? dispatchReceipt)
        => new("issuer", "audience", "proof", "incarnation-1", tenant, target, operation, hold, guardRevision > 0 ? guardRevision : null, guardReceipt,
            unpin ? 3 : null, unpin ? "release-receipt" : null, dispatchRevision, dispatchReceipt, reservation, decision, revision, checkpoint, [1, 2, 3]);

    private static bool Sorted(IReadOnlyList<ProtectionTarget> targets)
    {
        for (int i = 1; i < targets.Count; i++)
        {
            int tenant = StringComparer.Ordinal.Compare(targets[i - 1].TenantId, targets[i].TenantId);
            int interaction = StringComparer.Ordinal.Compare(targets[i - 1].AgentInteractionId, targets[i].AgentInteractionId);
            int alias = StringComparer.Ordinal.Compare(targets[i - 1].TargetProtectionKeyAlias, targets[i].TargetProtectionKeyAlias);
            int compared = tenant != 0 ? tenant : interaction != 0 ? interaction : alias;
            if (compared >= 0) { return false; }
        }
        return targets.Count > 0;
    }
}
