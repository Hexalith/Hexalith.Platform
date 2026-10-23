using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>A current, independently authorized exact owner call, carrying public framing and signed private proofs.</summary>
/// <param name="RequestJson">Closed custody-lifecycle-v1 request, with a fresh attempt and challenge.</param>
/// <param name="Metadata">Exact independently issued private-operation and control statements.</param>
/// <param name="MaximumRequestMessageBytes">Installed full request ceiling.</param>
/// <param name="TransportProfileDigest">Verified installed transport profile SHA-256.</param>
public sealed record CustodyOwnerAuthorizedCall(string RequestJson, IReadOnlyDictionary<string, string> Metadata,
    int MaximumRequestMessageBytes, string TransportProfileDigest);

/// <summary>The installed private authorizer; local snapshots cannot issue these permissions or enroll genesis.</summary>
public interface ICustodyOwnerCallSource
{
    /// <summary>Returns a freshly authorized exact phase, or null. Recovery requires its distinct private scope.</summary>
    Task<CustodyOwnerAuthorizedCall?> AuthorizeAsync(string phase, string tenant, string ownerScope,
        string? operationIdentity, string? transitionJson, CancellationToken cancellationToken);
}

/// <summary>Provider-neutral EventStore handback port with authenticated independent owner currentness.</summary>
public interface ICustodyOwnerStateAuthority : IAnchoredStateTransitionAuthority
{
    /// <summary>Validates an exact current owner revision/digest, including independently installed revision-zero genesis.</summary>
    Task<bool> ValidateStateAsync(long ownerRevision, string exactStateDigest, CancellationToken cancellationToken = default);
    /// <summary>Checks current private permission for the exact owner operation identity without advancing an anchor.</summary>
    Task<bool> AuthorizeOperationAsync(string identity, string operation, CancellationToken cancellationToken = default);
}

/// <summary>Concrete Dapr-only owner adapter. Construction never registers DI, installs trust, or enrolls an owner.</summary>
public sealed class CustodyOwnerAuthority : ICustodyOwnerStateAuthority
{
    private readonly DaprClient _client;
    private readonly string _binding, _tenant, _scope, _publicKey;
    private readonly AuthorityEnrollmentV1 _enrollment;
    private readonly ICustodyOwnerCallSource _source;

    /// <summary>Creates an adapter from independently installed public enrollment/trust and a private exact-call authorizer.</summary>
    public CustodyOwnerAuthority(DaprClient client, string logicalBindingName, string tenant, string ownerScope,
        string enrollmentJson, string publicKeyPem, ICustodyOwnerCallSource source)
    {
        _client = client; _binding = logicalBindingName; _tenant = tenant; _scope = ownerScope; _publicKey = publicKeyPem; _source = source;
        CustodyBindingContract.RejectDuplicateProperties(System.Text.Encoding.UTF8.GetBytes(enrollmentJson));
        _enrollment = JsonSerializer.Deserialize<AuthorityEnrollmentV1>(enrollmentJson, CustodyBindingContract.Json)
            ?? throw new ArgumentException("Independent public enrollment required.", nameof(enrollmentJson));
        if (!CustodyBindingContract.ValidEnrollment(_enrollment)) throw new ArgumentException("Invalid enrollment.", nameof(enrollmentJson));
        using var key = ECDsa.Create(); key.ImportFromPem(publicKeyPem);
        if (key.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7") throw new ArgumentException("P-256 verifier required.", nameof(publicKeyPem));
    }
    /// <inheritdoc />
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => TransitionAsync("admit-transition", transition, cancellationToken);
    /// <inheritdoc />
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => TransitionAsync("record-transition", transition, cancellationToken);
    /// <inheritdoc />
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => TransitionAsync("recover-transition", transition, cancellationToken);
    /// <inheritdoc />
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => TransitionAsync("verify-transition", transition, cancellationToken);
    /// <inheritdoc />
    public async Task<bool> ValidateStateAsync(long ownerRevision, string exactStateDigest, CancellationToken cancellationToken = default)
    {
        try
        {
            var call = await PrepareAsync("read-current", null, null, cancellationToken);
            return call is not null && await call.Value.Adapter.ValidateOwnerAsync(_tenant, _scope, ownerRevision, exactStateDigest, cancellationToken);
        }
        catch (Exception) { return false; }
    }
    /// <inheritdoc />
    public async Task<bool> AuthorizeOperationAsync(string identity, string operation, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!CustodyBindingContract.Text(identity) || !CustodyBindingContract.Text(operation)) return false;
            string operationIdentity = CustodyBindingContract.Hash(PlatformCanonicalBytes.Components(["custody-owner-operation-v1", _tenant, _scope, identity, operation]));
            var call = await PrepareAsync("read-current", null, operationIdentity, cancellationToken);
            if (call is null || call.Value.Request.Envelope.OperationId != operationIdentity) return false;
            var result = await call.Value.Invoker.InvokeAsync(call.Value.Request, TimeSpan.FromSeconds(30), cancellationToken);
            var current = result.Response?.Current;
            if (result.State != CustodyBindingTransportState.ReceivedUnverified || current is null || current.Authority != _enrollment
                || current.TenantId != _tenant || current.OwnerPurposeScope != _scope || current.OwnerRevision is null || current.OwnerDigest is null) return false;
            using var key = ECDsa.Create(); key.ImportFromPem(_publicKey);
            return key.VerifyData(JsonSerializer.SerializeToUtf8Bytes(current with { Signature = [] }, CustodyBindingContract.Json), current.Signature,
                HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (Exception) { return false; }
    }
    private async Task<bool> TransitionAsync(string phase, AnchoredStateTransition transition, CancellationToken token)
    {
        if (transition.ScopeId != _scope) return false;
        try
        {
            var call = await PrepareAsync(phase, transition, null, token); if (call is null) return false;
            return phase switch {
                "admit-transition" => await call.Value.Adapter.AdmitTransitionAsync(transition, token),
                "record-transition" => await call.Value.Adapter.RecordTransitionAsync(transition, token),
                "recover-transition" => await call.Value.Adapter.RecoverTransitionAsync(transition, token),
                _ => await call.Value.Adapter.VerifyTransitionAsync(transition, token),
            };
        }
        catch (Exception) { return false; }
    }
    private async Task<(CustodyBindingOwnerAdapter Adapter, CustodyBindingDaprInvoker Invoker, CustodyBindingRequestV1 Request)?> PrepareAsync(
        string phase, AnchoredStateTransition? transition, string? operationIdentity, CancellationToken token)
    {
        var supplied = await _source.AuthorizeAsync(phase, _tenant, _scope, operationIdentity,
            transition is null ? null : JsonSerializer.Serialize(transition, CustodyBindingContract.Json), token);
        if (supplied is null) return null;
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(supplied.RequestJson); var request = CustodyBindingContract.Decode(bytes);
        if (request is null || request.Envelope.Operation != phase || request.Envelope.TenantId != _tenant
            || request.Envelope.OwnerPurposeScope != _scope || request.Envelope.Enrollment != _enrollment) return null;
        var authority = new ExactCall(bytes, supplied);
        var invoker = new CustodyBindingDaprInvoker(_client, new Dictionary<string, string> { [CustodyBindingContract.LogicalRole] = _binding }, authority);
        return (new(invoker, _enrollment, _publicKey, (_, _, _) => Task.FromResult<CustodyBindingRequestV1?>(request)), invoker, request);
    }
    private sealed class ExactCall(byte[] bytes, CustodyOwnerAuthorizedCall supplied) : ICustodyBindingTransportAuthority
    {
        private bool Same(CustodyBindingRequestV1 exact) => CustodyBindingContract.Encode(exact).AsSpan().SequenceEqual(bytes);
        public Task<bool> AuthorizeAsync(CustodyBindingRequestV1 exact, string method, CancellationToken token)
            => Task.FromResult(method == "custody-lifecycle/" + exact.Envelope.Operation && Same(exact));
        public Task<bool> AuthorizeRecoveryAsync(CustodyBindingRequestV1 exact, CancellationToken token)
            => Task.FromResult(exact.Envelope.Operation == "recover-transition" && Same(exact));
        public Task<CustodyBindingTransportAuthorization?> PrepareTransportAsync(CustodyBindingRequestV1 exact, CancellationToken token)
            => Task.FromResult<CustodyBindingTransportAuthorization?>(Same(exact)
                ? new(supplied.Metadata, supplied.MaximumRequestMessageBytes, supplied.TransportProfileDigest, null) : null);
    }
}
