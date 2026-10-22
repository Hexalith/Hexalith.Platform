using System.Security.Cryptography;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Custody.Dapr;

internal sealed class SemanticProvider(ComponentProfile profile, IAuthorityStore store, StatementVerifier verifier, ECDsa responseSigner, KeyExchange? keyExchange = null)
{
    internal async Task<CustodyBindingResponseV1> InvokeAsync(byte[] exactBytes, SignedStatement operation, SignedStatement control, CancellationToken token, SignedStatement? keyAdmission = null)
    {
        var request = CustodyBindingContract.Decode(exactBytes) ?? throw new AuthorityUnavailableException("Invalid closed request.");
        var e = request.Envelope;
        CurrentState current = await store.ReadAsync(e.TenantId, token); // Every call, including duplicate read-current, reads fresh.
        if (!verifier.Authorize(request, exactBytes, operation, control, current)) throw new AuthorityUnavailableException("Current exact authorization missing.");
        CustodyBindingResponseV1 Reply() => new(CustodyBindingContract.Protocol, e.OperationId, e.RequestDigest);
        if (e.Operation == "read-current")
        {
            var read = new CustodyBindingCurrentReadV1(e.RequestAttemptId, e.Challenge, profile.Enrollment, current.Head.Revision, current.Head.Digest, []);
            return Reply() with { Current = read with { Signature = Sign(CustodyJson.Bytes(read)) } };
        }
        if (e.Operation == "read-original-page")
        {
            var page = request.Page!;
            if (!current.Checkpoint.Originals.TryGetValue(page.Header.OperationId, out var retained)
                || retained.Response is null || retained.RequestDigest != e.RequestDigest
                || page.Header.OriginalResultReference != retained.Response.Key || page.Header.OriginalSha256 != retained.Response.Sha256)
                throw new AuthorityUnavailableException("Original page identity unavailable.");
            byte[] original = await store.ReadImmutableAsync(retained.Response, token);
            var header = Header(retained.Manifest!, retained.Response, original.Length);
            if (header != page.Header) throw new AuthorityUnavailableException("Original header substitution.");
            byte[] slice = original.AsSpan(page.Offset, page.Length).ToArray();
            return Reply() with { Page = new(header, page.Offset, slice.Length, CustodyJson.Hash(slice), slice) };
        }
        if (e.Operation != "unwrap" && current.Checkpoint.Originals.TryGetValue(e.OperationId, out var originalOperation))
        {
            if (originalOperation.RequestDigest != e.RequestDigest) throw new AuthorityUnavailableException("Permanent original operation conflict.");
            if (originalOperation.Response is { } reference)
            {
                byte[] original = await store.ReadImmutableAsync(reference, token);
                return originalOperation.Manifest is not null
                    ? Reply() with { OriginalHeader = Header(originalOperation.Manifest, reference, original.Length) }
                    : CustodyJson.Read<CustodyBindingResponseV1>(original);
            }
            return Pending(request); // An admitted uncertain original is never replayed or called NotStarted.
        }
        if (e.Operation is "wrap" or "unwrap")
        {
            if (keyExchange is null || keyAdmission is null) throw new AuthorityUnavailableException("Key execution installation missing.");
            return await keyExchange.ExecuteAsync(request, current, keyAdmission, token);
        }
        if (e.Operation is "lookup-original" or "lookup-transition" or "verify-transition")
            return Pending(request); // No synthetic NotStarted proof.
        // Key effects and B1 require qualified current installation, measured completion reserve and closure.
        if (!verifier.Qualified(current)) throw new AuthorityUnavailableException("Installed qualification and capacity are missing.");
        if (e.ExpectedCustodyRevision != current.Head.Revision || e.CheckpointDigest != current.Head.Digest)
            throw new AuthorityConflictException();
        if (current.Checkpoint.Originals.Count >= 10000) throw new AuthorityUnavailableException("Lifecycle original capacity exhausted.");
        var next = Clone(current);
        switch (e.Operation)
        {
            case "destroy-manifest":
                if (next.Cycle is not null || next.Frozen) throw new AuthorityUnavailableException("One physical cycle per tenant.");
                var manifest = request.Manifest!;
                foreach (var target in manifest.Original.Targets)
                {
                    var matches = next.Registrations.Values.Where(r => r.Registration.Identity.ObjectId == target.AgentInteractionId
                        && r.Registration.Identity.KeyAlias == target.TargetProtectionKeyAlias).ToArray();
                    if (matches.Length != 1 || next.Pins.Values.Any(p => p.Target == CustodyJson.Target(matches[0].Registration.Identity)))
                        throw new AuthorityUnavailableException("Unknown target or effective pin blocks complete reservation.");
                }
                next.Originals.Add(e.OperationId, new(e.RequestDigest, e.Operation, null, request));
                next = next with { Cycle = new(e.OperationId, request, next.CurrentEpoch, null, CyclePhase.Reserved, [], [], null, null) };
                await store.CommitAsync(current, next, token);
                return Pending(request);
            case "unpin":
                var lifecycle = request.Lifecycle!; string pinId = CustodyJson.Target(lifecycle.Identity) + ":" + lifecycle.HoldId;
                if (!next.Pins.TryGetValue(pinId, out var pin) || pin.GuardReceipt != e.Proof.GuardReceiptId
                    || e.Proof.ReleaseGuardRevision <= pin.GuardRevision || next.Frozen)
                    throw new AuthorityUnavailableException("Exact currently authorized hold release missing.");
                next.Pins.Remove(pinId);
                var result = new CustodyKeyLifecycleOutcome(lifecycle.Identity, e.OperationId, e.RequestDigest,
                    CustodyKeyLifecycleStatus.Unpinned, "release-" + Guid.NewGuid().ToString("N"), null);
                var response = Reply() with { Lifecycle = result };
                var reference = await store.PutImmutableAsync(CustodyJson.Bytes(response), "original", token);
                next.Originals.Add(e.OperationId, new(e.RequestDigest, e.Operation, reference, null));
                await store.CommitAsync(current, next, token);
                return response;
            default:
                // Pin awaits cross-owner atomic dispatch handoff. Key paths await real holder admission/execution.
                // Anchor scope revisions cannot be reinterpreted as tenant-head revisions.
                throw new AuthorityUnavailableException("This effect has no qualified implementation and remains closed.");
        }
    }
    internal static Checkpoint Clone(CurrentState current) => CustodyJson.Read<Checkpoint>(CustodyJson.Bytes(current.Checkpoint)) with {
        Revision = checked(current.Head.Revision + 1), PredecessorDigest = current.Head.Digest };
    private byte[] Sign(byte[] bytes) => responseSigner.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
    private CustodyBindingResponseV1 Pending(CustodyBindingRequestV1 request)
    {
        var e = request.Envelope;
        if (request.Manifest is { } manifest)
        {
            var result = new Hexalith.EventStore.Contracts.Security.DeletionManifestProviderResult(
                e.TenantId, manifest.Original.Capability.BatchId, manifest.ProtectionReservationReceiptId,
                Hexalith.EventStore.Contracts.Security.DeletionManifestProviderState.Unknown, []);
            var status = new CustodyBindingManifestStatusV1(result, profile.Enrollment, manifest.Original.Capability.ManifestDigest, []);
            return new(CustodyBindingContract.Protocol, e.OperationId, e.RequestDigest, ManifestStatus: status with { Signature = Sign(CustodyJson.Bytes(status)) });
        }
        return new(CustodyBindingContract.Protocol, e.OperationId, e.RequestDigest, TransitionVerified: request.Transition is null ? null : false);
    }
    internal CustodyBindingOriginalHeaderV1 Header(CustodyBindingRequestV1 request, ObjectVersion reference, int length)
        => new(profile.Enrollment, reference.Key, request.Envelope.TenantId, request.Envelope.OperationId,
            request.Envelope.RequestDigest, request.Manifest!.Original.Capability.BatchId, request.Manifest.ProtectionReservationReceiptId,
            request.Manifest.Original.Capability.ManifestDigest, length, request.Manifest.Original.Targets.Count, reference.Sha256);
}
