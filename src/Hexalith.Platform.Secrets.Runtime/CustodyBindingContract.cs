using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

// Unbound source contract. No implementation here supplies current authority or physical evidence.
internal sealed record AuthorityEnrollmentV1(string AuthorityId, string AuthorityIncarnation, string StoreTarget,
    string ProviderProfileId, string TrustProfileRevision, string PublishedAnchorSetId, string ArtifactProfileDigest);

internal sealed record ControlAdmissionProofV1(string Issuer, string Audience, string ProofId, string AuthorityIncarnation,
    string TenantId, string TargetVectorDigest, string Action, string? HoldId, long? GuardRevision, string? GuardReceiptId,
    long? ReleaseGuardRevision, string? ReleaseGuardReceiptId, long DispatchRevision, string? DispatchReceiptId,
    string? ProtectionReservationReceiptId, string DecisionVersion, long ExpectedCustodyRevision, string CheckpointDigest,
    byte[] Signature);

internal sealed record CustodyBindingEnvelopeV1(string ProtocolVersion, string Operation, string OperationId,
    string RequestDigest, string TenantId, string OwnerPurposeScope, AuthorityEnrollmentV1 Enrollment,
    long ExpectedCustodyRevision, string CheckpointDigest, string RequestAttemptId, string Challenge,
    ControlAdmissionProofV1 Proof);

internal sealed record CustodyBindingManifestV1(DeletionBatchConsumptionRequest Original, byte[] SignedCanonicalPayload,
    string ProtectionReservationReceiptId);

internal sealed record CustodyBindingPageReadV1(CustodyBindingOriginalHeaderV1 Header, int Offset, int Length);

internal sealed record CustodyBindingKeyUseV1(string KeyUseLeaseId, string DeliveryAttemptId, string RecipientProcessIncarnation,
    string TransportIncarnation, string OriginalRootReference);

// A closed tagged union: validation admits exactly one operation-specific branch.
internal sealed record CustodyBindingRequestV1(CustodyBindingEnvelopeV1 Envelope, CustodyKeyRegistration? Registration = null,
    CustodyKeyLifecycleRequest? Lifecycle = null, CustodyBindingManifestV1? Manifest = null,
    AnchoredStateTransition? Transition = null, CustodyBindingPageReadV1? Page = null, CustodyBindingKeyUseV1? KeyUse = null);

internal static class CustodyBindingContract
{
    internal const string Protocol = "custody-lifecycle-v1";
    internal const string LogicalRole = "interaction-custody";
    internal const int DefaultMessageBytes = 4 * 1024 * 1024;
    internal const int MaximumPageBytes = 256 * 1024;
    internal const int MaximumPageFrameBytes = 1024 * 1024;
    // A complete value-free original has <=1000 receipts, each with five <=2048 UTF-16 strings
    // (three target components, batch, receipt), at most six JSON bytes per UTF-16 code unit.
    // 1000*(5*2048*6+256) + 16*2048*6 + 16384 < 64 MiB, including wrapper/signature overhead.
    // This assembly cap is separate from the unchanged 32 MiB actor pending-state cap.
    internal const int MaximumOriginalBytes = 64 * 1024 * 1024;
    internal static readonly string[] Operations = ["wrap", "unwrap", "pin", "unpin", "destroy-manifest", "lookup-original",
        "read-current", "admit-transition", "record-transition", "recover-transition", "verify-transition", "lookup-transition",
        "read-original-page", "complete-key-use"];
    internal static readonly JsonSerializerOptions Json = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    // Existing owner digest projections remain byte-for-byte System.Text.Json defaults. In particular,
    // signed JCS bytes are carried separately; transport escaping never changes their signing identity.
    internal static string OriginalRequestDigest(CustodyBindingRequestV1 request)
    {
        if (request.Manifest is { } manifest) { return Hash(JsonSerializer.SerializeToUtf8Bytes(manifest.Original)); }
        if (request.Lifecycle is { } lifecycle) { return Hash(JsonSerializer.SerializeToUtf8Bytes(lifecycle)); }
        if (request.Transition is { } transition) { return Hash(JsonSerializer.SerializeToUtf8Bytes(transition)); }
        if (request.Page is { } page) { return page.Header.RequestDigest; }
        if (request.Registration is { } registration) { return Hash(JsonSerializer.SerializeToUtf8Bytes(registration)); }
        var e = request.Envelope;
        return Hash(PlatformCanonicalBytes.Components([Protocol, e.TenantId, e.OwnerPurposeScope,
            e.Enrollment.AuthorityId, e.Enrollment.AuthorityIncarnation]));
    }
    internal static bool Digest(string? value) => value is { Length: 64 } && value.All(c => c is >= 'A' and <= 'F' or >= '0' and <= '9');
    internal static bool Text(string? value, int limit = 2048)
    {
        try { return !string.IsNullOrWhiteSpace(value) && value.Length <= limit && new UTF8Encoding(false, true).GetByteCount(value) > 0; }
        catch (EncoderFallbackException) { return false; }
    }
    internal static bool CustodyText(string? value)
        => Text(value) && new UTF8Encoding(false, true).GetByteCount(value!) <= 2048;

    internal static byte[] Encode(CustodyBindingRequestV1 request)
    {
        if (!Validate(request)) { throw new ArgumentException("Invalid closed custody binding request."); }
        return JsonSerializer.SerializeToUtf8Bytes(request, Json);
    }

    internal static CustodyBindingRequestV1? Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            if (bytes.Length is 0 or > MaximumOriginalBytes) { return null; }
            RejectDuplicateProperties(bytes);
            var request = JsonSerializer.Deserialize<CustodyBindingRequestV1>(bytes, Json);
            return request is not null && Validate(request) ? request : null;
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or OverflowException) { return null; }
    }

    internal static void RejectDuplicateProperties(ReadOnlySpan<byte> bytes)
    {
        var reader = new Utf8JsonReader(bytes);
        var objects = new Stack<HashSet<string>>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject) { objects.Push(new(StringComparer.Ordinal)); }
            else if (reader.TokenType == JsonTokenType.EndObject) { objects.Pop(); }
            else if (reader.TokenType == JsonTokenType.PropertyName && !objects.Peek().Add(reader.GetString()!))
            { throw new JsonException("Duplicate custody field."); }
        }
    }

    internal static bool ValidEnrollment(AuthorityEnrollmentV1 e) => e is not null && Text(e.AuthorityId) && Text(e.AuthorityIncarnation)
        && Text(e.StoreTarget) && Text(e.ProviderProfileId) && Text(e.TrustProfileRevision) && Text(e.PublishedAnchorSetId) && Digest(e.ArtifactProfileDigest);

    internal static bool Validate(CustodyBindingRequestV1 r)
    {
        try
        {
            var e = r.Envelope; var p = e.Proof;
            if (e.ProtocolVersion != Protocol || !Operations.Contains(e.Operation, StringComparer.Ordinal)
                || !Text(e.OperationId) || !Digest(e.RequestDigest) || !Text(e.TenantId) || !Text(e.OwnerPurposeScope, 4096)
                || !ValidEnrollment(e.Enrollment) || e.ExpectedCustodyRevision < 0 || !Digest(e.CheckpointDigest)
                || !Text(e.RequestAttemptId) || !Text(e.Challenge) || p is null
                || !Text(p.Issuer) || !Text(p.Audience) || !Text(p.ProofId) || !Text(p.DecisionVersion)
                || p.AuthorityIncarnation != e.Enrollment.AuthorityIncarnation || p.TenantId != e.TenantId
                || p.Action != e.Operation || p.ExpectedCustodyRevision != e.ExpectedCustodyRevision || p.CheckpointDigest != e.CheckpointDigest
                || !Digest(p.TargetVectorDigest) || p.Signature is not { Length: > 0 and <= 8192 }
                || p.DispatchRevision < 0 || !Pair(p.GuardRevision, p.GuardReceiptId) || !Pair(p.ReleaseGuardRevision, p.ReleaseGuardReceiptId)
                || e.RequestDigest != OriginalRequestDigest(r)) { return false; }
            int branches = (r.Registration is null ? 0 : 1) + (r.Manifest is null ? 0 : 1) + (r.Transition is null ? 0 : 1) + (r.Page is null ? 0 : 1);
            switch (e.Operation)
            {
                case "admit-transition": case "record-transition": case "recover-transition": case "verify-transition": case "lookup-transition":
                    return branches == 1 && r.Transition is { } t && r.Lifecycle is null && r.KeyUse is null
                        && t.ScopeId == e.OwnerPurposeScope && t.ExpectedRevision == e.ExpectedCustodyRevision
                        && t.ExpectedRevision < long.MaxValue && t.TargetRevision == t.ExpectedRevision + 1
                        && t.PredecessorDigest == e.CheckpointDigest && Digest(t.TargetDigest)
                        && t.TargetBytes is { Length: > 0 and <= RecoverableAnchoredState.MaximumPendingBytes }
                        && Hash(t.TargetBytes) == t.TargetDigest && p.TargetVectorDigest == t.TargetDigest;
                case "read-current":
                    return branches == 0 && r.Lifecycle is null && r.KeyUse is null;
                case "read-original-page":
                    return branches == 1 && r.Page is { } page && r.Lifecycle is null && r.KeyUse is null
                        && page.Header.Valid() && page.Header.Authority == e.Enrollment && page.Header.TenantId == e.TenantId
                        && page.Header.RequestDigest == e.RequestDigest && page.Header.ManifestDigest == p.TargetVectorDigest
                        && page.Header.ReservationReceiptId == p.ProtectionReservationReceiptId
                        && page.Offset >= 0 && page.Length is > 0 and <= MaximumPageBytes
                        && page.Offset <= page.Header.TotalBytes - page.Length;
                case "destroy-manifest": case "lookup-original" when r.Manifest is not null:
                    return branches == 1 && r.Manifest is { } m && r.Lifecycle is null && r.KeyUse is null && ValidManifest(m, e);
                case "wrap": case "unwrap": case "pin": case "unpin": case "lookup-original": case "complete-key-use":
                    if (branches != 1 || r.Registration is not { } registration || registration.Identity.TenantId != e.TenantId
                        || registration.Identity.StoreTarget != e.Enrollment.StoreTarget) { return false; }
                    CustodyKeyLifecycleActor.ValidateIdentity(registration.Identity);
                    if (!CustodyText(registration.OperationId) || registration.ExpectedRevision < 0 || !CustodyText(registration.KekVersion)
                        || !CustodyText(registration.OpaqueWrappedKeyReference) || !CustodyText(registration.WrapReceiptId)) { return false; }
                    if (e.Operation is "wrap" || e.Operation == "lookup-original" && r.Lifecycle is null)
                    {
                        if (e.OperationId != registration.OperationId || e.ExpectedCustodyRevision != registration.ExpectedRevision) { return false; }
                    }
                    if (e.Operation is "pin" or "unpin" || e.Operation == "lookup-original" && r.Lifecycle is not null)
                    {
                        return r.KeyUse is null && r.Lifecycle is { } l && l.Identity == registration.Identity && l.OperationId == e.OperationId
                            && l.ExpectedRevision == e.ExpectedCustodyRevision && l.HoldId == p.HoldId && CustodyText(l.HoldId)
                            && CustodyText(l.OperationId) && CustodyText(l.FenceTokenId) && CustodyText(l.DecisionVersion)
                            && l.FenceTokenId == p.GuardReceiptId && l.FenceRevision == p.GuardRevision && l.DecisionVersion == p.DecisionVersion
                            && l.ProtectionReservationReceiptId is null
                            && l.Action is CustodyKeyLifecycleAction.Pin or CustodyKeyLifecycleAction.Unpin
                            && (e.Operation == "lookup-original" || l.Action == (e.Operation == "pin" ? CustodyKeyLifecycleAction.Pin : CustodyKeyLifecycleAction.Unpin))
                            && (l.Action != CustodyKeyLifecycleAction.Unpin || p.ReleaseGuardRevision is > 0);
                    }
                    if (r.Lifecycle is not null) { return false; }
                    return e.Operation is "unwrap" or "complete-key-use" ? ValidKeyUse(r.KeyUse, registration) : r.KeyUse is null;
                default: return false;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NullReferenceException or OverflowException) { return false; }
    }

    private static bool Pair(long? revision, string? receipt) => revision is null ? receipt is null : revision > 0 && Text(receipt);
    private static bool ValidKeyUse(CustodyBindingKeyUseV1? k, CustodyKeyRegistration r) => k is not null && Text(k.KeyUseLeaseId)
        && Text(k.DeliveryAttemptId) && Text(k.RecipientProcessIncarnation) && Text(k.TransportIncarnation)
        && k.OriginalRootReference == r.OpaqueWrappedKeyReference;

    private static bool ValidManifest(CustodyBindingManifestV1 m, CustodyBindingEnvelopeV1 e)
    {
        var o = m.Original; var c = o.Capability; var targets = o.Targets;
        // Match the existing protection owner's capture rules, without changing the canonical codec's wider Unicode primitive.
        _ = new AggregateIdentity(c.TenantId, "protection", c.BatchId);
        foreach (string value in new[] { c.Issuer, c.Audience, c.TenantId, c.DeletionRequestId, c.DestructionSealId,
            c.BatchKind, c.BatchId, c.ManifestDigest, c.GuardStreamId, c.CapabilityKeyVersion, o.DispatchReceiptId })
        { if (!CustodyText(value)) { return false; } }
        _ = new UTF8Encoding(false, true).GetByteCount(o.DetachedJws);
        if (targets is not { Count: >= 1 and <= 1000 } || c.TenantId != e.TenantId || o.DetachedJws is not { Length: > 0 and <= 16384 }
            || !Text(m.ProtectionReservationReceiptId) || m.ProtectionReservationReceiptId != e.Proof.ProtectionReservationReceiptId
            || o.CommittedIssuedGuardRevision <= 0 || o.DispatchGuardRevision < o.CommittedIssuedGuardRevision || !Text(o.DispatchReceiptId)
            || o.DispatchGuardRevision != e.Proof.DispatchRevision || o.DispatchReceiptId != e.Proof.DispatchReceiptId
            || o.CommittedIssuedGuardRevision != e.Proof.GuardRevision || !Text(e.Proof.GuardReceiptId)
            || c.ManifestDigest != e.Proof.TargetVectorDigest || c.ManifestDigest != DeletionBatchCapabilityIdentity.TargetManifestDigest(targets)
            || !m.SignedCanonicalPayload.AsSpan().SequenceEqual(DeletionBatchCapabilityIdentity.CanonicalPayload(c))) { return false; }
        // Verify supplied owner ordering; never silently sort a signed manifest.
        ProtectionTarget? previous = null;
        foreach (var target in targets)
        {
            if (target.TenantId != e.TenantId || !CustodyText(target.TenantId) || !CustodyText(target.AgentInteractionId)
                || !CustodyText(target.TargetProtectionKeyAlias) || previous is not null && Compare(previous, target) >= 0) { return false; }
            previous = target;
        }
        return true;
    }

    private static int Compare(ProtectionTarget a, ProtectionTarget b)
    {
        int tenant = StringComparer.Ordinal.Compare(a.TenantId, b.TenantId);
        int interaction = StringComparer.Ordinal.Compare(a.AgentInteractionId, b.AgentInteractionId);
        return tenant != 0 ? tenant : interaction != 0 ? interaction : StringComparer.Ordinal.Compare(a.TargetProtectionKeyAlias, b.TargetProtectionKeyAlias);
    }
}

// Exact private-operation authenticator seam: AuthorizeAsync MUST independently verify the current
// PrivateOwnerOperationCredential/scope and retained-key fingerprint (or equivalent enrolled exact
// private authenticator) over Encode(exact), including OperationId, phase, complete KeyUse recipient/
// transport incarnation, current enrollment, control proof and attempt/challenge. The original owner
// RequestDigest intentionally excludes delivery fields and is NOT an authentication substitute.
// The component repeats current authorization and native expected-head comparison at each effect/release.
// A signed ControlAdmissionProof, app id, binding role or transport identity alone grants nothing.
internal interface ICustodyBindingCurrentAuthority
{
    Task<bool> AuthorizeAsync(CustodyBindingRequestV1 exact, string privateMethod, CancellationToken cancellationToken) => Task.FromResult(false);
    Task<bool> AuthorizeRecoveryAsync(CustodyBindingRequestV1 exact, CancellationToken cancellationToken) => Task.FromResult(false);
    Task<bool> VerifyHeaderAsync(CustodyBindingRequestV1 exact, CustodyBindingOriginalHeaderV1 header,
        CancellationToken cancellationToken) => Task.FromResult(false);
    Task<bool> AuthorizePageAsync(CustodyBindingRequestV1 original, CustodyBindingPageReadV1 exact,
        CancellationToken cancellationToken) => Task.FromResult(false);
    Task<bool> VerifyOriginalAsync(CustodyBindingOriginalHeaderV1 header, CustodyBindingOriginalV1 original,
        ReadOnlyMemory<byte> exactBytes, CancellationToken cancellationToken) => Task.FromResult(false);
}
