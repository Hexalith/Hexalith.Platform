using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Custody.Dapr;

// Native current owner framing runs beside its actual surviving authority store, with an independently retained control original.
internal sealed class CustodyOwnerOperationHandler(ComponentProfile profile, IAuthorityStore store, IndependentDaprAuthority originals,
    string signerId, ECDsa signer, TimeProvider clock) : IIndependentOwnerOperationHandler
{
    public IReadOnlySet<string> Methods { get; } = new HashSet<string>(["custody-owner-call"], StringComparer.Ordinal);
    public async Task<object?> HandleAsync(string method, JsonElement payload, IndependentCallerGrant caller, IndependentAuthorityState enrolled, CancellationToken cancellationToken)
    {
        var input = payload.Deserialize<OwnerCall>(CustodyBindingContract.Json) ?? throw new AuthorityUnavailableException("Exact owner-call required.");
        if (method != "custody-owner-call" || caller.Role != "custody-owner" || caller.ScopeReference != input.tenant + "|" + input.ownerScope
            || !caller.Methods.Contains("custody-owner-phase:" + input.phase, StringComparer.Ordinal)
            || input.phase is not ("read-current" or "admit-transition" or "record-transition" or "recover-transition" or "verify-transition"))
            throw new AuthorityUnavailableException("Current exact owner phase grant required.");
        AnchoredStateTransition? transition = input.transitionJson is null ? null : CustodyJson.Read<AnchoredStateTransition>(System.Text.Encoding.UTF8.GetBytes(input.transitionJson));
        if (input.phase == "read-current" ? transition is not null : transition is null || transition.ScopeId != input.ownerScope)
            throw new AuthorityUnavailableException("Owner transition substitution denied.");
        var current = await store.ReadAsync(input.tenant, cancellationToken);
        string requestDigest = transition is not null ? CustodyJson.Hash(JsonSerializer.SerializeToUtf8Bytes(transition))
            : CustodyBindingContract.Hash(PlatformCanonicalBytes.Components([CustodyBindingContract.Protocol, input.tenant, input.ownerScope,
                profile.Enrollment.AuthorityId, profile.Enrollment.AuthorityIncarnation]));
        string operationId = input.operationIdentity ?? requestDigest;
        if (!CustodyBindingContract.Digest(operationId) || input.operationIdentity is not null && input.phase != "read-current")
            throw new AuthorityUnavailableException("Exact owner operation identity required.");
        var retained = await originals.InvokeAsync<RetainedControlOriginal>("custody-control-original", new { tenant = input.tenant, scope = input.ownerScope,
            operationId, revision = current.Head.Revision, digest = current.Head.Digest }, cancellationToken);
        var verifier = new StatementVerifier(profile, clock);
        if (retained is null || retained.ControlStatement is null) throw new AuthorityUnavailableException("Independent original owner control unavailable.");
        string proofHash = CustodyJson.Hash(CustodyJson.Bytes(retained.Proof));
        if (!verifier.Verify(retained.GuardAndProtectionOriginal, "guard-control-original", current, proofHash, TimeSpan.FromSeconds(30))
            || !verifier.Verify(retained.ControlStatement, "control", current, proofHash, TimeSpan.FromSeconds(30))
            || retained.Proof.TenantId != input.tenant || retained.Proof.Action != input.phase
            || retained.Proof.ExpectedCustodyRevision != current.Head.Revision || retained.Proof.CheckpointDigest != current.Head.Digest)
            throw new AuthorityUnavailableException("Fresh exact original owner control required.");
        var envelope = new CustodyBindingEnvelopeV1(CustodyBindingContract.Protocol, input.phase, operationId, requestDigest,
            input.tenant, input.ownerScope, profile.Enrollment, current.Head.Revision, current.Head.Digest,
            Guid.NewGuid().ToString("N"), Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), retained.Proof);
        var request = new CustodyBindingRequestV1(envelope, Transition: transition);
        byte[] wire = CustodyBindingContract.Encode(request);
        var permission = new OperationPermission(input.phase, operationId, envelope.RequestAttemptId, envelope.Challenge, input.ownerScope, profile.ProfileDigest);
        if (!profile.Trust.Any(key => key.KeyId == signerId && key.Purpose == "private-operation"))
            throw new AuthorityUnavailableException("Independent private-operation signer required.");
        var statement = new SignedStatement("private-operation", signerId, profile.Enrollment.AuthorityId, profile.Enrollment.AuthorityIncarnation,
            input.tenant, current.Head.Revision, current.Head.Digest, CustodyJson.Hash(wire), clock.GetUtcNow(), clock.GetUtcNow().AddSeconds(30),
            JsonSerializer.SerializeToElement(permission, CustodyBindingContract.Json), []);
        lock (signer) statement = statement with { Signature = signer.SignData(CustodyJson.Bytes(statement), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence) };
        return new CustodyOwnerAuthorizedCall(System.Text.Encoding.UTF8.GetString(wire), new Dictionary<string, string>(StringComparer.Ordinal) {
            ["custody-operation-proof"] = Convert.ToBase64String(CustodyJson.Bytes(statement)),
            ["custody-control-proof"] = Convert.ToBase64String(CustodyJson.Bytes(retained.ControlStatement)) },
            CustodyBindingContract.CandidateRequestMessageBytes, profile.Enrollment.ArtifactProfileDigest);
    }
    private sealed record OwnerCall(string phase, string tenant, string ownerScope, string? operationIdentity, string? transitionJson);
}
