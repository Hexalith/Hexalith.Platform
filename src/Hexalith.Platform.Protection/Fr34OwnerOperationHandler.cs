using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Actual receiving FR-34 engine and independently persisted observation operations. No owner-provided verification flags are accepted.</summary>
public sealed class Fr34OwnerOperationHandler(EventStoreFr34CanaryEngine engine, Fr34EventStoreCanaryReader independentReader,
    IInteractionProtectionBindingProvider independentBindings, TimeProvider clock,IFr34UnsealMeasurement? independentMeasurements=null) : IIndependentOwnerOperationHandler
{
    /// <inheritdoc />
    public bool RequiresCurrentResultAdmission(string method)=>Methods.Contains(method);
    /// <inheritdoc />
    public string? ResultPolicy(string method,JsonElement payload,IndependentAuthorityState current)
        =>QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(new { current.Fr34Target,current.Fr34AuthorizationRevision,current.Fr34RecipeSha256,current.TrustRevision },CustodyBindingContract.Json)!);
    /// <inheritdoc />
    public void ReleaseResult(string method,object? result)
    {
        byte[]? bytes=method=="fr34-unseal"?result as byte[]:method=="fr34-replay"?(result as Hexalith.EventStore.Contracts.Security.PayloadUnprotectionOutcome)?.PayloadBytes:null;
        if(bytes is not null) CryptographicOperations.ZeroMemory(bytes);
    }
    /// <inheritdoc />
    public bool IsReadOnly(string method)=>method is "fr34-read-persisted" or "fr34-observe-current" or "fr34-verify-ownership" or "fr34-verify-carrier" or "fr34-confirm-destroyed";
    /// <inheritdoc />
    public Task<object?> RecoverAsync(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState current,CancellationToken token)
        =>method is "fr34-unseal" or "fr34-replay" ? Task.FromResult<object?>(null) : HandleAsync(method,payload,caller,current,token);
    /// <inheritdoc />
    public IReadOnlySet<string> Methods { get; } = new HashSet<string>(["fr34-seal-persist", "fr34-unseal", "fr34-destroy", "fr34-replay",
        "fr34-read-persisted", "fr34-observe-current", "fr34-verify-ownership", "fr34-verify-carrier", "fr34-confirm-destroyed"], StringComparer.Ordinal);
    /// <inheritdoc />
    public async Task<object?> HandleAsync(string method, JsonElement payload, IndependentCallerGrant caller, IndependentAuthorityState current, CancellationToken cancellationToken)
    {
        var target = current.Fr34Target;
        if (target is null || current.ValidUntil <= clock.GetUtcNow() || !engine.IsActualTarget(target)
            || caller.ScopeReference != target.TenantId + "|fr34" || string.IsNullOrWhiteSpace(current.Fr34AuthorizationRevision)) throw new InvalidOperationException("Exact current FR-34 enrollment and common authorization revision required.");
        bool engineMethod = method is "fr34-seal-persist" or "fr34-unseal" or "fr34-destroy" or "fr34-replay";
        if (caller.Role != (engineMethod ? "fr34-engine" : "fr34-observer")) throw new InvalidOperationException("Independent FR-34 role required.");
        T Read<T>() => payload.Deserialize<T>(CustodyBindingContract.Json) ?? throw new InvalidOperationException("Closed FR-34 request required.");
        object Decision(bool allowed) => new { Allowed = allowed };
        // Independently enrolled policy/target/trust/recipe revision, shared by engine and observer. Journal progress is never policy authority.
        string authorizationRevision=QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(new { target,current.Fr34AuthorizationRevision,current.TrustRevision,current.Fr34RecipeSha256 },CustodyBindingContract.Json)!);
        var authorization = new Fr34CanaryAuthorization(target, authorizationRevision, clock.GetUtcNow(), current.ValidUntil);
        bool Current(Fr34CanaryAuthorization admitted) => admitted.Target == target && admitted.AuthorityRevision == authorization.AuthorityRevision
            && admitted.ObservedAt <= clock.GetUtcNow() && admitted.ValidUntil <= current.ValidUntil && admitted.ValidUntil > clock.GetUtcNow();
        async Task<Fr34StoredCanary?> Owned(Fr34CanaryReference reference)
        {
            if (reference.Target != target) return null;
            var stored = await independentReader.ReadOriginalAsync(reference, cancellationToken).ConfigureAwait(false);
            if (stored is null) return null;
            var binding = await independentBindings.ResolveAsync(stored.Owner, stored.Occurrence, null, false, cancellationToken).ConfigureAwait(false);
            return binding?.KeyReference == reference.KeyReference && binding.Identity.Target == stored.ProtectionTarget ? stored : null;
        }
        switch (method)
        {
            case "fr34-observe-current": return Read<Observe>().expected == target ? authorization : null;
            case "fr34-seal-persist":
                var seal = Read<Seal>();
                try { return seal.target == target ? await engine.SealAndPersistAsync(target, seal.canaryId, seal.plaintext, cancellationToken).ConfigureAwait(false) : null; }
                finally { CryptographicOperations.ZeroMemory(seal.plaintext); }
            case "fr34-verify-ownership":
                var ownership = Read<Ownership>();
                return Decision(ownership.expected == target && ownership.originalCanaryId == ownership.reference.CanaryId
                    && Current(ownership.admitted) && await Owned(ownership.reference).ConfigureAwait(false) is not null);
            case "fr34-verify-carrier":
                var carrier = Read<Carrier>();
                var storedCarrier = await Owned(carrier.reference).ConfigureAwait(false);
                bool exactCarrier=carrier.expected == target && carrier.originalCanaryId == carrier.reference.CanaryId && Current(carrier.admitted)
                    && storedCarrier is not null && carrier.observation.Reference == carrier.reference
                    && storedCarrier.Protected.PayloadBytes.AsSpan().SequenceEqual(carrier.observation.Bytes)
                    && JsonSerializer.Serialize(storedCarrier.Protected.Metadata) == JsonSerializer.Serialize(carrier.observation.Metadata);
                return Decision(exactCarrier);
            case "fr34-read-persisted":
                var read = Read<Reference>().reference;
                return await Owned(read).ConfigureAwait(false) is not null ? await independentReader.ReadAsync(read, cancellationToken).ConfigureAwait(false) : null;
            case "fr34-confirm-destroyed":
                var reference = Read<Reference>().reference;
                var original = await Owned(reference).ConfigureAwait(false);
                if (original is null) return Decision(false);
                var binding = await independentBindings.ResolveAsync(original.Owner, original.Occurrence, null, false, cancellationToken).ConfigureAwait(false);
                return Decision(binding is not null && await independentBindings.IsIrreversiblyErasedAsync(binding, cancellationToken).ConfigureAwait(false));
            case "fr34-unseal":
                var unseal = Read<Reference>().reference;
                if(await Owned(unseal).ConfigureAwait(false) is null) return null;
                var opened=await engine.UnsealAsync(unseal,cancellationToken).ConfigureAwait(false);
                bool transferred=false;
                try
                {
                    if(opened is not null && independentMeasurements is not null)
                    {
                        var measured=await independentMeasurements.RecordUnsealAsync(unseal,opened,cancellationToken).ConfigureAwait(false);
                        if(measured is not { PersistedPlaintextAbsent:true,UnsealEqual:true }) return null;
                    }
                    cancellationToken.ThrowIfCancellationRequested();transferred=true;return opened;
                }
                finally { if(!transferred && opened is not null) CryptographicOperations.ZeroMemory(opened); }
            case "fr34-destroy":
                var destroy = Read<Reference>().reference;
                return Decision(await Owned(destroy).ConfigureAwait(false) is not null && await engine.DestroyAsync(destroy, cancellationToken).ConfigureAwait(false));
            case "fr34-replay":
                var replay = Read<Reference>().reference;
                return await Owned(replay).ConfigureAwait(false) is not null ? await engine.ReplayAsync(replay, cancellationToken).ConfigureAwait(false) : null;
            default: throw new InvalidOperationException("Closed FR-34 method required.");
        }
    }
    private sealed record Reference(Fr34CanaryReference reference);
    private sealed record Observe(Fr34ProtectionTarget expected);
    private sealed record Seal(Fr34ProtectionTarget target, string canaryId, byte[] plaintext);
    private sealed record Ownership(Fr34ProtectionTarget expected, string originalCanaryId, Fr34CanaryReference reference, Fr34CanaryAuthorization admitted);
    private sealed record Carrier(Fr34ProtectionTarget expected, string originalCanaryId, Fr34CanaryReference reference, Fr34PersistedCanary observation, Fr34CanaryAuthorization admitted);
}
