using System.Security.Cryptography;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Custody.Dapr;

internal sealed class SemanticProvider(ComponentProfile profile, IAuthorityStore store, StatementVerifier verifier, ECDsa responseSigner, KeyExchange? keyExchange = null)
{
    internal async Task<CustodyBindingResponseV1> InvokeAsync(byte[] exactBytes, SignedStatement operation, SignedStatement control, CancellationToken token, SignedStatement? keyAdmission = null)
    {
        var request = CustodyBindingContract.Decode(exactBytes) ?? throw new AuthorityUnavailableException("Invalid closed request.");
        try { return await InvokeDecodedAsync(request, exactBytes, operation, control, token, keyAdmission); }
        finally { if (request.WrapInput is { } input) CryptographicOperations.ZeroMemory(input.RootKey); }
    }
    private async Task<CustodyBindingResponseV1> InvokeDecodedAsync(CustodyBindingRequestV1 request, byte[] exactBytes,
        SignedStatement operation, SignedStatement control, CancellationToken token, SignedStatement? keyAdmission)
    {
        var e = request.Envelope;
        CurrentState current = await store.ReadAsync(e.TenantId, token); // Every call, including duplicate read-current, reads fresh.
        if (!verifier.Authorize(request, exactBytes, operation, control, current)) throw new AuthorityUnavailableException("Current exact authorization missing.");
        CustodyBindingResponseV1 Reply() => new(CustodyBindingContract.Protocol, e.OperationId, e.RequestDigest);
        if (e.Operation == "read-current")
        {
            current.Checkpoint.Anchors.TryGetValue(e.OwnerPurposeScope, out var owner);
            var read = new CustodyBindingCurrentReadV1(e.RequestAttemptId, e.Challenge, profile.Enrollment, current.Head.Revision, current.Head.Digest, [], owner?.Revision, owner?.Digest, e.TenantId, e.OwnerPurposeScope);
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
        if (request.Transition is not null) return await TransitionAsync(request, current, token);
        if (e.Operation == "lookup-original" && request.Registration is { } requestedRegistration && request.Lifecycle is null)
        {
            // This lookup has the registration projection's digest, not the ephemeral wrap-input
            // digest. Authenticate both against the independently retained original before returning
            // its unchanged receipt. Lookup never reconstructs a root or invokes a native method.
            if (!current.Checkpoint.Registrations.TryGetValue(requestedRegistration.OpaqueWrappedKeyReference, out var stored)
                || stored.Registration != requestedRegistration
                || !current.Checkpoint.Originals.TryGetValue(requestedRegistration.OperationId, out var wrapOriginal)
                || wrapOriginal.Operation != "wrap" || wrapOriginal.Manifest is not null || wrapOriginal.Response is null)
                throw new AuthorityUnavailableException("Exact original wrapped registration unavailable.");
            var original = CustodyJson.Read<CustodyBindingResponseV1>(await store.ReadImmutableAsync(wrapOriginal.Response, token));
            if (original.ProtocolVersion != CustodyBindingContract.Protocol || original.OperationId != requestedRegistration.OperationId
                || original.RequestDigest != wrapOriginal.RequestDigest || original.WrappedRegistration != requestedRegistration
                || original.OriginalHeader is not null || original.Lifecycle is not null || original.Current is not null
                || original.TransitionVerified is not null || original.Page is not null || original.ManifestStatus is not null
                || original.KeyDelivery is not null || original.Delivery is not null || original.TransitionProof is not null)
                throw new AuthorityUnavailableException("Retained wrap original binding mismatch.");
            return Reply() with { Lifecycle = new(requestedRegistration.Identity, e.OperationId, e.RequestDigest,
                CustodyKeyLifecycleStatus.Wrapped, requestedRegistration.WrapReceiptId, null) };
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
        if (e.Operation == "complete-key-use")
        {
            if (keyExchange is null || keyAdmission is null) throw new AuthorityUnavailableException("Independent completion missing.");
            return await keyExchange.CompleteAsync(request, current, keyAdmission, token);
        }
        if (e.Operation is "lookup-original" or "lookup-transition" or "verify-transition")
            return Pending(request); // No synthetic NotStarted proof.
        // Key effects and B1 require qualified current installation, measured completion reserve and closure.
        if (!verifier.Qualified(current)) throw new AuthorityUnavailableException("Installed qualification and capacity are missing.");
        if (e.ExpectedCustodyRevision != current.Head.Revision || e.CheckpointDigest != current.Head.Digest)
            throw new AuthorityConflictException();
        if (current.Checkpoint.Originals.Count >= 10000) throw new AuthorityUnavailableException("Lifecycle original capacity exhausted.");
        if (request.Lifecycle is { } lifecycleTarget && e.Proof.TargetVectorDigest != Hexalith.EventStore.Contracts.Security.DeletionBatchCapabilityIdentity.TargetManifestDigest(
            [new(e.TenantId, lifecycleTarget.Identity.ObjectId, lifecycleTarget.Identity.KeyAlias)]))
            throw new AuthorityUnavailableException("Lifecycle target-vector digest mismatch.");
        var next = Clone(current);
        switch (e.Operation)
        {
            case "pin":
                var holding = request.Lifecycle!;
                if (next.Frozen || !next.Registrations.TryGetValue(request.Registration!.OpaqueWrappedKeyReference, out var held)
                    || held.Registration != request.Registration || held.Epoch != next.CurrentEpoch
                    || next.Cycle is { } active && active.Manifest.Manifest!.Original.Targets.Any(t => t.AgentInteractionId == holding.Identity.ObjectId
                        && t.TargetProtectionKeyAlias == holding.Identity.KeyAlias))
                    throw new AuthorityUnavailableException("Frozen, reserved, erased or unknown target cannot acquire a pin.");
                string heldId = CustodyJson.Target(holding.Identity) + ":" + holding.HoldId;
                if (!next.Pins.TryAdd(heldId, new(holding.HoldId!, CustodyJson.Target(holding.Identity), e.Proof.GuardRevision!.Value, e.Proof.GuardReceiptId!)))
                    throw new AuthorityUnavailableException("Hold already has a different original.");
                var pinned = Reply() with { Lifecycle = new(holding.Identity, e.OperationId, e.RequestDigest,
                    CustodyKeyLifecycleStatus.Pinned, "pin-" + Guid.NewGuid().ToString("N"), null) };
                var pinOriginal = await store.PutImmutableAsync(CustodyJson.Bytes(pinned), "original", token);
                next.Originals.Add(e.OperationId, new(e.RequestDigest, e.Operation, pinOriginal, null));
                await store.CommitAsync(current, next, token);
                return pinned;
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
                next = next with { Cycle = new(e.OperationId, request, next.CurrentEpoch, null, CyclePhase.Reserved,
                    profile.Epochs.Where(epoch => epoch.TenantId == e.TenantId && epoch.OpaqueId != next.CurrentEpoch).Select(epoch => epoch.OpaqueId).ToArray(), [], null, null) };
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
                // No unsupported effect is inferred from a successful transport/authentication check.
                throw new AuthorityUnavailableException("This effect has no qualified implementation and remains closed.");
        }
    }
    private async Task<CustodyBindingResponseV1> TransitionAsync(CustodyBindingRequestV1 request, CurrentState current, CancellationToken token)
    {
        var e = request.Envelope; var t = request.Transition!;
        // Every owner scope, including revision-zero genesis, must be independently enrolled ahead of startup.
        // Absent/restored local state is never permission to create this entry.
        if (!current.Checkpoint.Anchors.TryGetValue(t.ScopeId, out var anchor))
            throw new AuthorityUnavailableException("Owner scope has no authenticated installation/genesis enrollment.");
        bool same = anchor.Admitted is { } a && CustodyJson.Bytes(a).AsSpan().SequenceEqual(CustodyJson.Bytes(t));
        bool atTarget = anchor.Revision == t.TargetRevision && anchor.Digest == t.TargetDigest;
        bool predecessor = anchor.Revision == t.ExpectedRevision && anchor.Digest == t.PredecessorDigest;
        bool read = e.Operation is "verify-transition" or "lookup-transition";
        bool accepted = atTarget && same;
        string originalId = "anchor:" + CustodyJson.Hash(PlatformCanonicalBytes.Components([t.ScopeId, t.TargetRevision.ToString(System.Globalization.CultureInfo.InvariantCulture), t.TargetDigest]));
        bool historical = false;
        if (anchor.Revision >= t.TargetRevision && current.Checkpoint.Originals.TryGetValue(originalId, out var original)
            && original.Operation == "admit-transition" && original.RequestDigest == e.RequestDigest && original.Response is not null)
        {
            var retained = CustodyBindingContract.Decode(await store.ReadImmutableAsync(original.Response, token));
            historical = retained?.Transition is { } exact && CustodyJson.Bytes(exact).AsSpan().SequenceEqual(CustodyJson.Bytes(t));
            if (!historical) throw new AuthorityUnavailableException("Retained owner transition substitution.");
            accepted = true;
        }
        if (!read && !accepted)
        {
            if (!verifier.Qualified(current) || e.ExpectedCustodyRevision != current.Head.Revision || e.CheckpointDigest != current.Head.Digest
                || !predecessor || anchor.Admitted is { } pending && anchor.Revision < pending.TargetRevision && !same)
                throw new AuthorityUnavailableException("Owner predecessor/admitted original comparison failed.");
            var next = Clone(current);
            if (e.Operation == "admit-transition")
            {
                if (next.Originals.Count >= 10000) throw new AuthorityUnavailableException("Owner original capacity exhausted.");
                if (!next.Originals.ContainsKey(originalId))
                {
                    var retained = await store.PutImmutableAsync(CustodyJson.Bytes(request), "journal", token);
                    next.Originals.Add(originalId, new(e.RequestDigest, e.Operation, retained, null));
                }
                next.Anchors[t.ScopeId] = anchor with { Admitted = t, AdmissionOperationId = e.OperationId };
            }
            else if (e.Operation is "record-transition" or "recover-transition" && same)
                next.Anchors[t.ScopeId] = anchor with { Revision = t.TargetRevision, Digest = t.TargetDigest };
            else throw new AuthorityUnavailableException("Record/recover requires the retained admitted original.");
            await store.CommitAsync(current, next, token); accepted = true;
        }
        var observed = historical ? anchor with { Revision = t.TargetRevision, Digest = t.TargetDigest }
            : read || !accepted ? anchor : e.Operation == "admit-transition" ? anchor : anchor with { Revision = t.TargetRevision, Digest = t.TargetDigest };
        var proof = new CustodyBindingTransitionProofV1(e.RequestAttemptId, e.Challenge, profile.Enrollment, e.Operation, t.ScopeId,
            observed.Revision, observed.Digest, CustodyJson.Hash(CustodyJson.Bytes(t)),
            accepted ? observed.Revision == t.TargetRevision ? "Recorded" : "Admitted" : "Unknown", []);
        return new(CustodyBindingContract.Protocol, e.OperationId, e.RequestDigest, TransitionVerified: accepted,
            TransitionProof: proof with { Signature = Sign(CustodyJson.Bytes(proof)) });
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
