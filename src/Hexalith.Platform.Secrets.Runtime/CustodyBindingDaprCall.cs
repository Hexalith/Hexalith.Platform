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

    internal static CustodyBindingRequestV1? Build(CustodyLifecycleBindingCommand command, PrivateCustodyBindingAuthority? authority = null)
    {
        if (command.Operation is not ("pin" or "unpin" or "destroy-manifest" or "lookup-original")) { return null; }
        if (command.Operation is "pin" or "unpin" || command.Operation == "lookup-original" && command.Manifest is null)
        {
            if (command.Lifecycle is not { } lifecycle || lifecycle.HoldId is null || authority is null) { return null; }
            var registration = new CustodyKeyRegistration(lifecycle.Identity, lifecycle.OperationId, lifecycle.ExpectedRevision, "kek-v1", "opaque-wrap", "wrap-receipt");
            byte[] fingerprint = authority.RequestFingerprint(command.Operation, lifecycle.OperationId, command.RequestDigest);
            var proof = Proof(authority, command.Operation, lifecycle.Identity.TenantId, command.RequestDigest, command.RequestDigest, lifecycle.ExpectedRevision, lifecycle.HoldId,
                lifecycle.FenceRevision, lifecycle.FenceTokenId, lifecycle.DecisionVersion, unpin: lifecycle.Action == CustodyKeyLifecycleAction.Unpin, reservation: null, dispatchRevision: 0, dispatchReceipt: null, fingerprint);
            var envelope = Envelope(authority, command.Operation, lifecycle.OperationId, command.RequestDigest, lifecycle.Identity.TenantId, lifecycle.Identity.StoreTarget, lifecycle.ExpectedRevision, proof);
            return envelope is null ? null : Stamp(authority, new CustodyBindingRequestV1(envelope, registration, Lifecycle: lifecycle));
        }
        if (command.Manifest is not { } manifest) { return null; }
        var targets = manifest.Targets.Select(i => new ProtectionTarget(i.TenantId, i.ObjectId, i.KeyAlias)).ToArray();
        if (!Sorted(targets) || DeletionBatchCapabilityIdentity.TargetManifestDigest(targets) != manifest.ManifestDigest) { return null; }
        if (authority is null) { return null; }
        string operation = command.Operation == "lookup-original" ? "lookup-original" : "destroy-manifest";
        var capability = new DeletionBatchCapabilityV1(authority.Issuer, authority.Audience, targets[0].TenantId, manifest.ReservationId, manifest.BatchId, "manifest", 1,
            manifest.BatchId, manifest.ManifestDigest, manifest.ReservationId, 1, 1, 1, authority.KeyVersion);
        string? signature = authority.Sign(capability);
        if (signature is null) { return null; }
        var owner = new DeletionBatchConsumptionRequest(capability, signature, 2, manifest.ReservationId, 3, targets);
        var body = new CustodyBindingManifestV1(owner, DeletionBatchCapabilityIdentity.CanonicalPayload(capability), manifest.ReservationId);
        byte[] manifestFingerprint = authority.RequestFingerprint(operation, manifest.ReservationId, command.RequestDigest);
        var manifestProof = Proof(authority, operation, targets[0].TenantId, manifest.ManifestDigest,
            command.RequestDigest, authority.Revision, null, 2, manifest.ReservationId, manifest.ReservationId, false, manifest.ReservationId, 3, manifest.ReservationId, manifestFingerprint);
        var manifestEnvelope = Envelope(authority, operation, manifest.ReservationId, command.RequestDigest,
            targets[0].TenantId, manifest.Targets[0].StoreTarget, authority.Revision, manifestProof);
        return manifestEnvelope is null ? null : Stamp(authority, new CustodyBindingRequestV1(manifestEnvelope, Manifest: body));
    }

    private static CustodyBindingRequestV1? Stamp(PrivateCustodyBindingAuthority authority, CustodyBindingRequestV1 request)
    {
        CustodyBindingRequestV1 bound = Bind(request);
        if (!authority.BindRequest(bound.Envelope.Operation, bound.Envelope.OperationId, bound.Envelope.RequestDigest)) { return null; }
        byte[] fingerprint = authority.RequestFingerprint(bound.Envelope.Operation, bound.Envelope.OperationId, bound.Envelope.RequestDigest);
        return bound with { Envelope = bound.Envelope with { Proof = bound.Envelope.Proof with { Signature = fingerprint } } };
    }

    private static CustodyBindingRequestV1 Bind(CustodyBindingRequestV1 request)
        => request with { Envelope = request.Envelope with { RequestDigest = CustodyBindingContract.OriginalRequestDigest(request) } };

    private static CustodyBindingEnvelopeV1? Envelope(PrivateCustodyBindingAuthority authority, string operation, string operationId, string digest, string tenant, string storeTarget, long revision, ControlAdmissionProofV1 proof)
    {
        if (tenant != authority.TenantId || revision != authority.Revision) { return null; }
        return new(CustodyBindingContract.Protocol, operation, operationId, digest, tenant, "owner|purpose",
            new AuthorityEnrollmentV1(authority.AuthorityId, "rev-" + authority.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture), storeTarget,
                authority.PublicAnchorId, authority.PublicAnchorVersion, authority.PublicAnchorId, authority.ArtifactDigest),
            revision, proof.CheckpointDigest, "attempt-1", "challenge-1", proof);
    }

    private static ControlAdmissionProofV1 Proof(PrivateCustodyBindingAuthority authority, string operation, string tenant, string target, string checkpoint, long revision, string? hold, long guardRevision, string? guardReceipt,
        string decision, bool unpin, string? reservation, long dispatchRevision, string? dispatchReceipt, byte[] fingerprint)
        => new(authority.Issuer, authority.Audience, "proof", "rev-" + authority.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture), tenant, target, operation, hold, guardRevision > 0 ? guardRevision : null, guardReceipt,
            unpin ? 3 : null, unpin ? "release-receipt" : null, dispatchRevision, dispatchReceipt, reservation, decision, revision, checkpoint, fingerprint);

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

/// <summary>Current-authority check for one captured credential. App id, binding name, and <see cref="ControlAdmissionProofV1"/> are not authority.</summary>
internal sealed class PrivateCustodyBindingCurrentAuthority(PrivateCustodyBindingAuthority? authority) : ICustodyBindingCurrentAuthority
{
    /// <inheritdoc/>
    public Task<bool> AuthorizeAsync(CustodyBindingRequestV1 exact, string privateMethod, CancellationToken cancellationToken)
    {
        _ = privateMethod;
        if (authority is null || exact.Envelope.Proof is null) { return Task.FromResult(false); }
        byte[] fingerprint = authority.RequestFingerprint(exact.Envelope.Operation, exact.Envelope.OperationId, exact.Envelope.RequestDigest);
        bool authorized = authority.AuthorizesExact(exact.Envelope.Proof.Issuer, exact.Envelope.Proof.Audience, exact.Envelope.TenantId,
            exact.Envelope.Enrollment.AuthorityId, exact.Envelope.ExpectedCustodyRevision, exact.Envelope.Operation, exact.Envelope.OperationId,
            exact.Envelope.RequestDigest, fingerprint);
        if (!authorized || exact.Manifest is not { } manifest) { return Task.FromResult(authorized); }
        return Task.FromResult(authority.Verify(manifest.Original.Capability, manifest.Original.DetachedJws));
    }
}
