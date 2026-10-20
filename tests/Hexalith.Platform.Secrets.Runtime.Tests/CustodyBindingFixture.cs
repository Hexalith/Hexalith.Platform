using System.Text.Json;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

internal static class CustodyBindingFixture
{
    internal static readonly string Digest = new('A', 64);
    internal static AuthorityEnrollmentV1 Enrollment => new("authority", "incarnation-1", "opaque-store", "profile", "trust-1", "anchors", Digest);
    internal static CustodyKeyObjectIdentity Identity => new("tenant", "object", PlatformKeyPurpose.InteractionRootDek,
        "alias", "v1", "lifecycle", "opaque-store", "contract");
    internal static CustodyKeyRegistration Registration => new(Identity, "wrap-op", 1, "kek-v1", "opaque-wrap", "wrap-receipt");

    internal static CustodyBindingRequestV1 Bind(CustodyBindingRequestV1 r)
        => r with { Envelope = r.Envelope with { RequestDigest = CustodyBindingContract.OriginalRequestDigest(r) } };

    internal static CustodyBindingEnvelopeV1 Envelope(string operation, string tenant = "tenant", string scope = "owner|purpose", string? target = null)
    {
        var proof = new ControlAdmissionProofV1("issuer", "custody", "proof", Enrollment.AuthorityIncarnation, tenant, target ?? Digest,
            operation, null, null, null, null, null, 0, null, null, "decision", 1, Digest, [1, 2, 3]);
        return new(CustodyBindingContract.Protocol, operation, "operation-1", Digest, tenant, scope, Enrollment, 1, Digest, "attempt-1", "challenge-1", proof);
    }

    // All fixture content is synthetic value-free identity/receipt material. No real credentials/keys/provider are used.
    internal static CustodyBindingRequestV1 Manifest(int count = 1, string? field = null, bool maximal = false, bool structural = false)
    {
        string tenant = structural ? field ?? "tenant" : maximal ? new string('t', 64) : "tenant";
        var targets = Enumerable.Range(0, count).Select(n => new ProtectionTarget(tenant,
            maximal ? (field ?? "x")[..2044] + n.ToString("D4") : $"interaction-{n:D4}", field ?? "alias")).ToArray();
        string digest = DeletionBatchCapabilityIdentity.TargetManifestDigest(targets);
        var capability = new DeletionBatchCapabilityV1("issuer", "audience", tenant, "delete-request", "seal", "kind", 0,
            "batch", digest, "guard", 1, 1, 1, "capability-v1");
        var owner = new DeletionBatchConsumptionRequest(capability, "original-detached-signature", 2, "dispatch", 3, targets);
        var m = new CustodyBindingManifestV1(owner, DeletionBatchCapabilityIdentity.CanonicalPayload(capability), "reservation");
        var e = Envelope("destroy-manifest", tenant, target: digest);
        e = e with { Proof = e.Proof with { ProtectionReservationReceiptId = "reservation", GuardRevision = 2,
            GuardReceiptId = "issued", DispatchRevision = 3, DispatchReceiptId = "dispatch" } };
        return Bind(new(e, Manifest: m));
    }

    internal static CustodyBindingRequestV1 Lifecycle(bool unpin = false, bool lookup = false)
    {
        var operation = unpin ? "unpin" : "pin";
        var e = Envelope(lookup ? "lookup-original" : operation);
        var original = new CustodyKeyLifecycleRequest(Identity, e.OperationId,
            unpin ? CustodyKeyLifecycleAction.Unpin : CustodyKeyLifecycleAction.Pin, e.ExpectedCustodyRevision,
            "hold", "guard-receipt", 2, e.Proof.DecisionVersion);
        e = e with { Proof = e.Proof with { HoldId = "hold", GuardRevision = 2, GuardReceiptId = "guard-receipt",
            ReleaseGuardRevision = unpin ? 3 : null, ReleaseGuardReceiptId = unpin ? "release-receipt" : null } };
        return Bind(new(e, Registration, Lifecycle: original));
    }

    internal static CustodyBindingOriginalV1 Original(CustodyBindingRequestV1 request, int receiptPadding = 0)
    {
        var m = request.Manifest!;
        var receipts = m.Original.Targets.Select((t, n) => new DeletionTargetReceipt(t, m.Original.Capability.BatchId,
            $"receipt-{n:D4}" + new string('x', receiptPadding))).ToArray();
        return new(new(request.Envelope.TenantId, m.Original.Capability.BatchId, m.ProtectionReservationReceiptId,
            DeletionManifestProviderState.Consumed, receipts), request.Envelope.RequestDigest,
            m.Original.Capability.ManifestDigest, "restore-barrier", [7, 8, 9]);
    }

    internal static (CustodyBindingOriginalHeaderV1 Header, byte[] Bytes) Header(CustodyBindingRequestV1 request, CustodyBindingOriginalV1? original = null)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(original ?? Original(request), CustodyBindingContract.Json);
        return (new(request.Envelope.Enrollment, "original-reference", request.Envelope.TenantId, request.Envelope.OperationId,
            request.Envelope.RequestDigest, request.Manifest!.Original.Capability.BatchId, request.Manifest.ProtectionReservationReceiptId,
            request.Manifest.Original.Capability.ManifestDigest, bytes.Length, request.Manifest.Original.Targets.Count,
            CustodyBindingContract.Hash(bytes)), bytes);
    }

    internal sealed class Authority : ICustodyBindingCurrentAuthority
    {
        internal bool Allowed { get; set; } = true;
        internal bool RecoveryAllowed { get; set; } = true;
        internal bool OriginalAllowed { get; set; } = true;
        internal List<string> Methods { get; } = [];
        internal int PageReads { get; private set; }
        internal Action? OnAuthorize { get; set; }
        internal Action? OnVerifyOriginal { get; set; }
        public Task<bool> AuthorizeAsync(CustodyBindingRequestV1 exact, string privateMethod, CancellationToken cancellationToken)
        { OnAuthorize?.Invoke(); Methods.Add(privateMethod); return Task.FromResult(Allowed && privateMethod == "custody-lifecycle/" + exact.Envelope.Operation); }
        public Task<bool> AuthorizeRecoveryAsync(CustodyBindingRequestV1 exact, CancellationToken cancellationToken) => Task.FromResult(RecoveryAllowed);
        public Task<bool> VerifyHeaderAsync(CustodyBindingRequestV1 exact, CustodyBindingOriginalHeaderV1 header, CancellationToken cancellationToken) => Task.FromResult(Allowed);
        public Task<bool> AuthorizePageAsync(CustodyBindingRequestV1 original, CustodyBindingPageReadV1 exact, CancellationToken cancellationToken)
        { PageReads++; return Task.FromResult(Allowed); }
        public Task<bool> VerifyOriginalAsync(CustodyBindingOriginalHeaderV1 header, CustodyBindingOriginalV1 original,
            ReadOnlyMemory<byte> exactBytes, CancellationToken cancellationToken)
        {
            OnVerifyOriginal?.Invoke();
            return Task.FromResult(Allowed && OriginalAllowed && original.Signature.AsSpan().SequenceEqual(new byte[] { 7, 8, 9 }));
        }
    }

    internal sealed class Pages(CustodyBindingOriginalHeaderV1 header, byte[] original) : ICustodyBindingOriginalPageSource
    {
        internal Func<CustodyBindingOriginalPageV1, CustodyBindingOriginalPageV1> Change { get; set; } = p => p;
        internal Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> Frame { get; set; } = p => p;
        internal int Reads { get; private set; }
        internal bool Unavailable { get; set; }
        public Task<ReadOnlyMemory<byte>?> ReadAsync(CustodyBindingPageReadV1 exactRead, CancellationToken cancellationToken)
        {
            Reads++;
            if (Unavailable) { return Task.FromResult<ReadOnlyMemory<byte>?>(null); }
            byte[] data = original.AsSpan(exactRead.Offset, exactRead.Length).ToArray();
            var page = Change(new(header, exactRead.Offset, data.Length, CustodyBindingContract.Hash(data), data));
            return Task.FromResult<ReadOnlyMemory<byte>?>(Frame(JsonSerializer.SerializeToUtf8Bytes(page, CustodyBindingContract.Json)));
        }
    }
}
