using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Public Dapr-only custody operations. Construction does not install an authorizer, native provider, trust or DI.</summary>
public sealed class CustodyLifecycleClient
{
    /// <summary>Configured logical binding role.</summary>
    public const string LogicalRole = "interaction-custody";
    private readonly DaprClient _client;
    private readonly IReadOnlyDictionary<string, string> _bindings;
    private readonly AuthorityEnrollmentV1 _enrollment;
    private readonly string _publicKey, _scope;
    private readonly ICustodyLifecycleCallSource _source;

    /// <summary>Uses independently installed public enrollment/P-256 trust and an authenticated current authorizer.</summary>
    public CustodyLifecycleClient(DaprClient client, IReadOnlyDictionary<string, string> logicalBindings,
        string ownerPurposeScope, string enrollmentJson, string publicKeyPem, ICustodyLifecycleCallSource source)
    {
        _client = client; _bindings = new Dictionary<string, string>(logicalBindings); _scope = ownerPurposeScope;
        _source = source; _publicKey = publicKeyPem;
        CustodyBindingContract.RejectDuplicateProperties(Encoding.UTF8.GetBytes(enrollmentJson));
        _enrollment = JsonSerializer.Deserialize<AuthorityEnrollmentV1>(enrollmentJson, CustodyBindingContract.Json)
            ?? throw new ArgumentException("Independent enrollment required.", nameof(enrollmentJson));
        if (!CustodyBindingContract.ValidEnrollment(_enrollment) || !CustodyBindingContract.Text(_scope, 4096))
            throw new ArgumentException("Invalid public enrollment/scope.");
        using var key = ECDsa.Create(); key.ImportFromPem(publicKeyPem);
        if (key.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7") throw new ArgumentException("P-256 verifier required.");
    }

    internal TimeProvider OperationClock { get; init; } = TimeProvider.System;

    internal async Task<T> InBudgetAsync<T>(CancellationToken caller, Func<CustodyOperationBudget, Task<T>> operation, Func<T> unknown)
    {
        using var budget = new CustodyOperationBudget(caller, OperationClock);
        T? result = default; bool transferred = false;
        try
        {
            budget.Check(); result = await operation(budget).ConfigureAwait(false); budget.Check();
            transferred = true; return result;
        }
        catch (OperationCanceledException) when (caller.IsCancellationRequested) { throw new OperationCanceledException(caller); }
        catch (Exception) { caller.ThrowIfCancellationRequested(); return unknown(); }
        finally { if (!transferred && result is CustodyKeyUseResult { Key: { } key }) key.Dispose(); }
    }

    /// <summary>Wraps the supplied original 32-byte root. The caller retains ownership of its input; internal copies are cleared.</summary>
    public Task<CustodyKeyRegistration?> WrapAsync(CustodyKeyObjectIdentity identity, string operationId, long expectedRevision,
        string kekVersion, string opaqueReference, CustodyKeyUse keyUse, ReadOnlyMemory<byte> rootKey, CancellationToken cancellationToken = default)
        => InBudgetAsync<CustodyKeyRegistration?>(cancellationToken, async budget =>
        {
            if (rootKey.Length != 32) return null;
            byte[] owned = rootKey.ToArray();
            try
            {
                var input = new CustodyBindingWrapInputV1(identity, operationId, expectedRevision, kekVersion, opaqueReference, owned, Use(keyUse));
                var body = new { input.Identity, input.OperationId, input.ExpectedRevision, input.KekVersion, input.OpaqueWrappedKeyReference, input.KeyUse };
                var result = await CallAsync("wrap", identity.TenantId, operationId, body, e => new(e, WrapInput: input), budget);
                return result?.Response.WrappedRegistration;
            }
            finally { CryptographicOperations.ZeroMemory(owned); }
        }, () => null);

    /// <summary>Releases one original root to an enrolled recipient. A duplicate or lost delivery returns value-free status.</summary>
    public Task<CustodyKeyUseResult> UnwrapAsync(CustodyKeyRegistration registration, string operationId,
        CustodyKeyUse keyUse, CancellationToken cancellationToken = default)
        => InBudgetAsync(cancellationToken, async budget =>
        {
            var result = await CallAsync("unwrap", registration.Identity.TenantId, operationId, new { Registration = registration, KeyUse = Use(keyUse) },
                e => new(e, Registration: registration, KeyUse: Use(keyUse)), budget);
            if (result?.Response.KeyDelivery is { } delivery) return new CustodyKeyUseResult("Released", new(delivery.RootKey));
            return Delivery(result?.Response.Delivery);
        }, () => new CustodyKeyUseResult("Unknown"));

    /// <summary>Records independent completion of every originally admitted holder. Local disposal alone cannot close a delivery.</summary>
    public Task<CustodyKeyUseResult> CompleteKeyUseAsync(CustodyKeyRegistration registration, string operationId,
        CustodyKeyUse keyUse, CancellationToken cancellationToken = default)
        => InBudgetAsync(cancellationToken, async budget =>
        {
            var result = await CallAsync("complete-key-use", registration.Identity.TenantId, operationId,
                new { Registration = registration, KeyUse = Use(keyUse) }, e => new(e, Registration: registration, KeyUse: Use(keyUse)), budget);
            return Delivery(result?.Response.Delivery);
        }, () => new CustodyKeyUseResult("Unknown"));

    /// <summary>Executes an exact pin/unpin. The independent control owner supplies hold/dispatch ordering.</summary>
    public Task<CustodyKeyLifecycleOutcome> ExecuteLifecycleAsync(CustodyKeyRegistration registration,
        CustodyKeyLifecycleRequest request, CancellationToken cancellationToken = default)
        => InBudgetAsync(cancellationToken, budget => LifecycleAsync(registration, request, false, budget), () => Missing(registration, request));

    /// <summary>Recovers a value-free registration or pin/unpin original; this never releases a key or starts native work.</summary>
    public Task<CustodyKeyLifecycleOutcome> LookupOriginalAsync(CustodyKeyRegistration registration,
        CustodyKeyLifecycleRequest? request = null, CancellationToken cancellationToken = default)
        => InBudgetAsync(cancellationToken, budget => LifecycleAsync(registration, request, true, budget), () => Missing(registration, request));

    /// <summary>Recovers the complete enrolled registration only after the native receiver verifies its retained wrap response and exact registration digest/receipt.</summary>
    public async Task<CustodyKeyRegistration?> LookupRegistrationOriginalAsync(CustodyKeyRegistration registration,CancellationToken token=default)
    {
        var original=await LookupOriginalAsync(registration,null,token).ConfigureAwait(false);
        return original.Status==CustodyKeyLifecycleStatus.Wrapped && original.Identity==registration.Identity && original.OperationId==registration.OperationId
            && original.ReceiptId==registration.WrapReceiptId && original.RequestDigest==CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(registration))
            ?registration:null;
    }

    /// <summary>Recovers the complete retained native wrap result using only its original pre-effect identity.</summary>
    public Task<CustodyKeyRegistration?> LookupRegistrationOriginalAsync(CustodyWrapOriginalIdentity original,CancellationToken token=default)
        =>InBudgetAsync<CustodyKeyRegistration?>(token,async budget=>
        {
            var result=await CallAsync("lookup-original",original.Identity.TenantId,original.OperationId,new { WrapOriginal=original },e=>new(e,WrapOriginal:original),budget).ConfigureAwait(false);
            return result?.Response.WrappedRegistration;
        },()=>null);

    internal async Task<CustodyKeyLifecycleOutcome> LifecycleAsync(CustodyKeyRegistration registration, CustodyKeyLifecycleRequest? lifecycle,
        bool lookup, CustodyOperationBudget budget)
    {
        budget.Check();
        string operation = lookup ? "lookup-original" : lifecycle?.Action == CustodyKeyLifecycleAction.Pin ? "pin" : "unpin";
        string id = lifecycle?.OperationId ?? registration.OperationId;
        if (!lookup && lifecycle?.Action is not (CustodyKeyLifecycleAction.Pin or CustodyKeyLifecycleAction.Unpin)) return Missing(registration, lifecycle);
        var result = await CallAsync(operation, registration.Identity.TenantId, id, new { Registration = registration, Lifecycle = lifecycle },
            e => new(e, Registration: registration, Lifecycle: lifecycle), budget);
        budget.Check(); return result?.Response.Lifecycle ?? Missing(registration, lifecycle);
    }
    private static CustodyKeyLifecycleOutcome Missing(CustodyKeyRegistration registration, CustodyKeyLifecycleRequest? lifecycle)
        => new(registration.Identity, lifecycle?.OperationId ?? registration.OperationId,
            CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes<object>(lifecycle is null ? registration : lifecycle)), CustodyKeyLifecycleStatus.Unknown);

    /// <summary>Consumes or recovers the exact complete signed manifest. Only a verified full immutable original yields Consumed.</summary>
    public async Task<DeletionManifestProviderResult> ExecuteManifestAsync(DeletionBatchConsumptionRequest original, string reservationReceiptId,
        bool lookupOnly, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteManifestOriginalAsync(original, reservationReceiptId, lookupOnly, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested(); return result.Result;
    }

    /// <summary>Recovers the complete signed vector together with its original restore-barrier evidence.</summary>
    public Task<CustodyManifestOriginal> ExecuteManifestOriginalAsync(DeletionBatchConsumptionRequest original, string reservationReceiptId,
        bool lookupOnly, CancellationToken cancellationToken = default)
        => InBudgetAsync(cancellationToken, budget => ExecuteManifestOriginalAsync(original, reservationReceiptId, lookupOnly, budget),
            () => UnknownManifest(original, reservationReceiptId));

    private static CustodyManifestOriginal UnknownManifest(DeletionBatchConsumptionRequest original, string reservation)
        => new(new(original.Capability.TenantId, original.Capability.BatchId, reservation, DeletionManifestProviderState.Unknown, []), null);

    internal async Task<CustodyManifestOriginal> ExecuteManifestOriginalAsync(DeletionBatchConsumptionRequest original, string reservationReceiptId,
        bool lookupOnly, CustodyOperationBudget budget)
    {
        budget.Check();
        var manifest = new CustodyBindingManifestV1(original, DeletionBatchCapabilityIdentity.CanonicalPayload(original.Capability), reservationReceiptId);
        string id = ManifestOperationId(original, reservationReceiptId);
        var call = await CallAsync(lookupOnly ? "lookup-original" : "destroy-manifest", original.Capability.TenantId, id,
            new { Manifest = manifest }, e => new(e, Manifest: manifest), budget);
        budget.Check();
        if (call is null) return UnknownManifest(original, reservationReceiptId);
        if (call.Response.ManifestStatus is { } status)
            return Verify(JsonSerializer.SerializeToUtf8Bytes(status with { Signature = [] }, CustodyBindingContract.Json), status.Signature)
                ? new(status.Result, null,status.Denial) : UnknownManifest(original, reservationReceiptId);
        if (call.Response.OriginalHeader is not { } header) return UnknownManifest(original, reservationReceiptId);
        var assembled = await CustodyBindingOriginalAssembler.AssembleAsync(call.Request, header,
            new OriginalPages(this, original.Capability.TenantId, id, budget), new OriginalAuthority(this, header), budget.Remaining, budget.Token);
        budget.Check(); return new(assembled.Original?.Result ?? UnknownManifest(original, reservationReceiptId).Result, assembled.Original?.RestoreBarrierReceiptId);
    }

    internal static string ManifestOperationId(DeletionBatchConsumptionRequest original, string reservation)
        => CustodyBindingContract.Hash(PlatformCanonicalBytes.Components(["custody-manifest-original-v1", original.Capability.TenantId,
            original.Capability.BatchId, original.Capability.ManifestDigest, reservation]));

    private async Task<Call?> CallAsync(string operation, string tenant, string id, object publicBody,
        Func<CustodyBindingEnvelopeV1, CustodyBindingRequestV1> create, CustodyOperationBudget budget)
    {
        CustodyBindingResponseV1? response = null; bool transferred = false;
        try
        {
            budget.Check();
            // Derive the exact original digest locally before authorization, without sending a secret to the authorizer.
            var placeholder = new CustodyBindingEnvelopeV1(CustodyBindingContract.Protocol, operation, id, "", tenant, _scope, _enrollment,
                0, "", "", "", null!);
            var draft = create(placeholder);
            string digest = CustodyBindingContract.OriginalRequestDigest(draft);
            var intent = new CustodyCallIntent(operation, tenant, _scope, id, digest, JsonSerializer.Serialize(publicBody, CustodyBindingContract.Json),
                Guid.NewGuid().ToString("N"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            var prepared = await _source.PrepareAsync(intent, budget.Token).WaitAsync(budget.Token);
            budget.Check();
            if (prepared is null) return null;
            byte[] envelopeBytes = Encoding.UTF8.GetBytes(prepared.EnvelopeJson);
            CustodyBindingContract.RejectDuplicateProperties(envelopeBytes);
            var e = JsonSerializer.Deserialize<CustodyBindingEnvelopeV1>(envelopeBytes, CustodyBindingContract.Json);
            if (e is null || e.ProtocolVersion != CustodyBindingContract.Protocol || e.Operation != operation || e.OperationId != id
                || e.TenantId != tenant || e.OwnerPurposeScope != _scope || e.Enrollment != _enrollment || e.RequestDigest != digest
                || e.RequestAttemptId != intent.RequestAttemptId || e.Challenge != intent.Challenge) return null;
            var request = create(e); byte[] exact = CustodyBindingContract.Encode(request); string wireDigest;
            try { wireDigest = CustodyBindingContract.Hash(exact); }
            finally { CryptographicOperations.ZeroMemory(exact); }
            var authorization = await _source.AuthorizeAsync(intent, prepared, wireDigest, budget.Token).WaitAsync(budget.Token);
            budget.Check();
            if (authorization is null || authorization.RequestSha256 != wireDigest) return null;
            var authority = new ExactAuthority(wireDigest, prepared, authorization);
            var invoker = new CustodyBindingDaprInvoker(_client, _bindings, authority);
            var result = await invoker.InvokeAsync(request, budget.Remaining, budget.Token);
            response = result.Response;
            budget.Check();
            if (result.State != CustodyBindingTransportState.ReceivedUnverified || response is null || !Authenticate(request, response)) return null;
            budget.Check(); transferred = true; return new(request, response, invoker);
        }
        catch (OperationCanceledException) when (budget.Caller.IsCancellationRequested) { throw new OperationCanceledException(budget.Caller); }
        catch (Exception) { budget.Caller.ThrowIfCancellationRequested(); return null; }
        finally { if (!transferred && response?.KeyDelivery is { } delivery) CryptographicOperations.ZeroMemory(delivery.RootKey); }
    }
    private bool Authenticate(CustodyBindingRequestV1 request, CustodyBindingResponseV1 response)
    {
        var proof = response.ResultProof;
        if (proof is null || proof.Authority != _enrollment || proof.RequestAttemptId != request.Envelope.RequestAttemptId
            || proof.Challenge != request.Envelope.Challenge) return false;
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(response with { ResultProof = null }, CustodyBindingContract.Json);
        try
        {
            return proof.ResponseSha256 == CustodyBindingContract.Hash(bytes)
                && Verify(JsonSerializer.SerializeToUtf8Bytes(proof with { Signature = [] }, CustodyBindingContract.Json), proof.Signature);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    private bool Verify(byte[] bytes, byte[] signature)
    {
        using var key = ECDsa.Create(); key.ImportFromPem(_publicKey);
        return signature.Length is > 0 and <= 8192 && key.VerifyData(bytes, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
    }
    private static CustodyBindingKeyUseV1 Use(CustodyKeyUse use)
        => new(use.KeyUseLeaseId, use.DeliveryAttemptId, use.RecipientProcessIncarnation, use.TransportIncarnation, use.OriginalRootReference);
    private static CustodyKeyUseResult Delivery(CustodyBindingDeliveryV1? delivery)
        => delivery is null ? new("Unknown") : new(delivery.Phase.ToString(), HoldersCompleted:
            delivery.Phase == CustodyBindingIntentPhase.Closed && delivery.IoComplete && delivery.BuffersComplete && delivery.HolderInventoryComplete);
    private sealed record Call(CustodyBindingRequestV1 Request, CustodyBindingResponseV1 Response, CustodyBindingDaprInvoker Invoker);
    private sealed class ExactAuthority(string digest, CustodyPreparedCall prepared, CustodyCallAuthorization authorization) : ICustodyBindingTransportAuthority
    {
        private bool Same(CustodyBindingRequestV1 request)
        {
            byte[] bytes = CustodyBindingContract.Encode(request);
            try { return CustodyBindingContract.Hash(bytes) == digest; }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        public Task<bool> AuthorizeAsync(CustodyBindingRequestV1 exact, string method, CancellationToken token)
            => Task.FromResult(method == "custody-lifecycle/" + exact.Envelope.Operation && Same(exact));
        public Task<CustodyBindingTransportAuthorization?> PrepareTransportAsync(CustodyBindingRequestV1 exact, CancellationToken token)
            => Task.FromResult<CustodyBindingTransportAuthorization?>(Same(exact)
                ? new(authorization.Metadata, prepared.MaximumRequestMessageBytes, prepared.TransportProfileDigest, prepared.KeyClosureProfileDigest) : null);
    }
    private sealed class OriginalAuthority(CustodyLifecycleClient client, CustodyBindingOriginalHeaderV1 authenticatedHeader) : ICustodyBindingCurrentAuthority
    {
        public Task<bool> VerifyHeaderAsync(CustodyBindingRequestV1 exact, CustodyBindingOriginalHeaderV1 header, CancellationToken token)
            => Task.FromResult(header == authenticatedHeader);
        public Task<bool> AuthorizePageAsync(CustodyBindingRequestV1 exact, CustodyBindingPageReadV1 page, CancellationToken token)
            => Task.FromResult(page.Header == authenticatedHeader); // The actual page call obtains fresh independent current authorization.
        public Task<bool> VerifyOriginalAsync(CustodyBindingOriginalHeaderV1 header, CustodyBindingOriginalV1 original, ReadOnlyMemory<byte> bytes, CancellationToken token)
            => Task.FromResult(header == authenticatedHeader && client.Verify(JsonSerializer.SerializeToUtf8Bytes(original with { Signature = [] }, CustodyBindingContract.Json), original.Signature));
    }
    private sealed class OriginalPages(CustodyLifecycleClient client, string tenant, string id, CustodyOperationBudget budget) : ICustodyBindingOriginalPageSource
    {
        public async Task<ReadOnlyMemory<byte>?> ReadAsync(CustodyBindingPageReadV1 read, CancellationToken token)
        {
            var call = await client.CallAsync("read-original-page", tenant, id, new { Page = read }, e => new(e, Page: read), budget);
            budget.Check();
            return call?.Response.Page is { } page ? JsonSerializer.SerializeToUtf8Bytes(page, CustodyBindingContract.Json) : null;
        }
    }
}
