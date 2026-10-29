using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Occurrence registry authority backed by its actual independently enrolled ERC scope, exact source originals and writer proofs; revision zero requires enrolled genesis.</summary>
public sealed class ErasureRootOccurrenceAuthority(ErasureRootCustody custody, ErasureRootOccurrenceBindings bindings,
    IErasureRootOccurrenceSource source, IErasureRootAuthorization authorization, IReadOnlyDictionary<string, string> registryEpochs) : IInteractionOccurrenceAuthority
{
    /// <inheritdoc />
    public Task<AnchoredStateTransition?> ReadCurrentOriginalAsync(string scopeId, CancellationToken cancellationToken = default)
    {
        var tenants = registryEpochs.Keys.Where(tenant => Scope(tenant) == scopeId).ToArray();
        return tenants.Length == 1 ? Journal(tenants[0]).ReadCurrentOriginalAsync(scopeId, cancellationToken) : Task.FromResult<AnchoredStateTransition?>(null);
    }
    /// <summary>Gets the exact scope used by the shipped registry actor's recoverable state journal.</summary>
    public static string Scope(string tenant) => InteractionOccurrenceRegistryActor.GetActorId(tenant) + "|candidate-interaction-occurrences-v1";
    private ErasureRootTransitionAuthority Journal(string tenant) => new(custody, authorization, tenant, Scope(tenant));
    /// <inheritdoc />
    public async Task<bool> AuthorizeOperationAsync(InteractionOccurrenceIdentity identity, string operation, CancellationToken cancellationToken = default)
        => operation is "Reserve" or "RetainSealed" or "CompleteWriter" or "Lookup" && registryEpochs.ContainsKey(identity.Target.TenantId)
            && await authorization.AuthorizeAsync(identity.Target.TenantId, ErasureRootCustody.Hash(identity), operation, ErasureRootCustody.Hash(new { identity, operation }), cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    public Task<bool> ValidateStateAsync(string tenantId, string epochId, long revision, string exactStateDigest, CancellationToken cancellationToken = default)
        => registryEpochs.TryGetValue(tenantId, out string? enrolled) && enrolled == epochId
            ? Journal(tenantId).ValidateStateAsync(revision, exactStateDigest, cancellationToken) : Task.FromResult(false);
    /// <inheritdoc />
    public async Task<string?> GetInstalledEpochAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (!registryEpochs.TryGetValue(tenantId, out var epoch)) { return null; }
        var read = await custody.ReadAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return read is { } current && current.Head.Anchors.ContainsKey(Scope(tenantId))
            && await authorization.AuthorizeAsync(tenantId, Scope(tenantId), "ReadInstalledRegistryEpoch", ErasureRootCustody.Hash(epoch), cancellationToken).ConfigureAwait(false) ? epoch : null;
    }
    /// <inheritdoc />
    public async Task<bool> AuthorizeReservationAsync(InteractionOccurrenceRequest request, CancellationToken cancellationToken = default)
    {
        var retained = await bindings.FindAsync(request.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(request.Identity), cancellationToken).ConfigureAwait(false);
        return retained?.Enrollment.Binding.Identity == request.Identity && retained.Enrollment.Binding.KeyReference == request.ProposedKeyReference
            && retained.Enrollment.Binding.DigestKeyVersion == request.DigestKeyVersion && retained.Enrollment.Binding.ReservationAttemptOrdinal == request.ReservationAttemptOrdinal
            && await source.VerifyReservationAsync(request, cancellationToken).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public async Task<bool> VerifySealedAsync(InteractionOccurrenceRecord reservation, InteractionOccurrenceSealedResult sealedResult, CancellationToken cancellationToken = default)
    {
        if (reservation.WriterState != InteractionOccurrenceWriterState.Reserved || sealedResult.KeyReference != reservation.KeyReference
            || sealedResult.SerializationFormat != "json+pdenc-v2" || sealedResult.PayloadBytes is not { Length: > 0 and <= 16777216 }
            || sealedResult.ProtectedPathCount <= 0) { return false; }
        var retained = await bindings.FindAsync(reservation.Request.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(reservation.Request.Identity), cancellationToken).ConfigureAwait(false);
        return retained?.EncryptionRequestDigest == ErasureRootCustody.Hash(reservation.Request)
            && await source.VerifySealedAsync(reservation, sealedResult, cancellationToken).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public async Task<bool> VerifyWriterAsync(InteractionOccurrenceRecord record, string proofId, bool persisted, CancellationToken cancellationToken = default)
    {
        var retained = await bindings.FindAsync(record.Request.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(record.Request.Identity), cancellationToken).ConfigureAwait(false);
        return retained is { CompletionLease: true } && retained.Enrollment.Binding.OperationId == proofId
            && retained.Enrollment.Binding.Identity == record.Request.Identity && await source.VerifyWriterAsync(record, proofId, persisted, cancellationToken).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public Task<bool> RecordRevisionAsync(string tenantId, string epochId, long expectedRevision, long nextRevision, string exactStateDigest, CancellationToken cancellationToken = default)
        => nextRevision == expectedRevision + 1 ? ValidateStateAsync(tenantId, epochId, nextRevision, exactStateDigest, cancellationToken) : Task.FromResult(false);
    private Task<bool> Transition(AnchoredStateTransition transition, string operation, CancellationToken token)
    {
        var tenants = registryEpochs.Keys.Where(tenant => Scope(tenant) == transition.ScopeId).ToArray();
        if (tenants.Length != 1) { return Task.FromResult(false); }
        var journal = Journal(tenants[0]);
        return operation switch { "admit" => journal.AdmitTransitionAsync(transition, token), "record" => journal.RecordTransitionAsync(transition, token),
            "recover" => journal.RecoverTransitionAsync(transition, token), _ => journal.VerifyTransitionAsync(transition, token) };
    }
    /// <inheritdoc />
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => Transition(transition, "admit", cancellationToken);
    /// <inheritdoc />
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => Transition(transition, "record", cancellationToken);
    /// <inheritdoc />
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => Transition(transition, "recover", cancellationToken);
    /// <inheritdoc />
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => Transition(transition, "verify", cancellationToken);
}
