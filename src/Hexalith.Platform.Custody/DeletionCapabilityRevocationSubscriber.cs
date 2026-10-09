using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Authenticates revocation, confirms the same-owner protection block, then confirms its exact guard mirror.</summary>
/// <param name="subscription">Exact independent enrolled source/target.</param>
/// <param name="authenticator">Current independent issuer/signature authority.</param>
/// <param name="registrar">Private protection-only capability; inject nowhere else.</param>
/// <param name="mirror">Exact current guard mirror.</param>
/// <param name="clock">Whole-operation budget and exclusive authority clock.</param>
/// <remarks>No default registration or public endpoint is provided. Unknown outcomes perform no signing, dispatch or re-attestation.</remarks>
public sealed class DeletionCapabilityRevocationSubscriber(DeletionCapabilityRevocationSubscription subscription,
    IDeletionCapabilityRevocationAuthenticator authenticator, IDeletionCapabilityCompromiseRegistrar registrar,
    IDeletionCapabilityGuardRevocationMirror mirror, TimeProvider clock)
{
    /// <summary>Returns true only after exact authenticated protection and mirror lookup; retries reuse the original block.</summary>
    /// <param name="envelope">Exact closed revocation.</param>
    /// <param name="signedEvidence">Opaque signed authentication evidence; never logged or retained here.</param>
    /// <param name="cancellationToken">Caller cancellation.</param>
    /// <returns>Whether both durable owners confirm the exact original revocation.</returns>
    public async Task<bool> ReceiveAsync(DeletionCapabilityRevocationEnvelope envelope, string signedEvidence,
        CancellationToken cancellationToken = default)
    {
        var budget = new PrivateOwnerOperationDeadline(clock, cancellationToken);
        try
        {
            budget.Check();
            if (!Valid(envelope) || string.IsNullOrWhiteSpace(signedEvidence) || signedEvidence.Length > 16384) { return false; }
            var authorization = await budget.ReadAsync(() => authenticator.VerifyAsync(envelope, signedEvidence, CancellationToken.None)).ConfigureAwait(false);
            if (!Current(authorization, envelope)) { return false; }
            var block = await budget.ReadAsync(async () => Capture(await registrar.LookupAsync(envelope, CancellationToken.None).ConfigureAwait(false), envelope, budget)).ConfigureAwait(false);
            if (block is null)
            {
                // Register is idempotent at the same owner. A lost result is resolved only by exact lookup.
                try { await budget.ReadAsync(() => registrar.RegisterAsync(envelope, CancellationToken.None)).ConfigureAwait(false); }
                catch (Exception) { budget.Check(); }
                block = await budget.ReadAsync(async () => Capture(await registrar.LookupAsync(envelope, CancellationToken.None).ConfigureAwait(false), envelope, budget)).ConfigureAwait(false);
            }
            if (block is null || !await StillCurrentAsync(authorization!, envelope, signedEvidence, budget).ConfigureAwait(false)) { return false; }
            var original = await budget.ReadAsync(async () => Capture(await mirror.LookupAsync(envelope, CancellationToken.None).ConfigureAwait(false), envelope, budget)).ConfigureAwait(false);
            if (original is not null && !Same(original, block)) { return false; }
            if (original is null)
            {
                try { await budget.ReadAsync(() => mirror.RecordAsync(block, CancellationToken.None)).ConfigureAwait(false); }
                catch (Exception) { budget.Check(); }
                original = await budget.ReadAsync(async () => Capture(await mirror.LookupAsync(envelope, CancellationToken.None).ConfigureAwait(false), envelope, budget)).ConfigureAwait(false);
            }
            budget.Check();
            return original is not null && Same(original, block)
                && await StillCurrentAsync(authorization!, envelope, signedEvidence, budget).ConfigureAwait(false);
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return false; }
    }

    private bool Valid(DeletionCapabilityRevocationEnvelope? envelope) => envelope is not null
        && envelope.Issuer == subscription.Issuer && envelope.Audience == subscription.Audience && envelope.TenantId == subscription.TenantId
        && envelope.KeyFamily == "DeletionBatchCapabilitySigningKey" && envelope.RevocationRevision > 0 && envelope.TrustProfileRevision > 0
        && new[] { envelope.Issuer, envelope.Audience, envelope.TenantId, envelope.KeyFamily, envelope.KeyVersion, envelope.EventIdentity }
            .All(value => !string.IsNullOrWhiteSpace(value) && value.Length <= 2048)
        && envelope.SignatureDigest is { Length: 64 } digest && digest.All(char.IsAsciiHexDigit);

    private bool Current(DeletionCapabilityRevocationAuthorization? authorization, DeletionCapabilityRevocationEnvelope envelope)
        => authorization is not null && authorization.Envelope == envelope && !string.IsNullOrWhiteSpace(authorization.AuthorityRevision)
            && authorization.AuthorityRevision.Length <= 2048 && authorization.ObservedAt != default
            && authorization.ObservedAt <= clock.GetUtcNow() && authorization.ValidUntil > clock.GetUtcNow();

    private async Task<bool> StillCurrentAsync(DeletionCapabilityRevocationAuthorization initial,
        DeletionCapabilityRevocationEnvelope envelope, string signedEvidence, PrivateOwnerOperationDeadline budget)
    {
        var final = await budget.ReadAsync(() => authenticator.VerifyAsync(envelope, signedEvidence, CancellationToken.None)).ConfigureAwait(false);
        budget.Check(); return Current(initial, envelope) && Current(final, envelope)
            && final!.AuthorityRevision == initial.AuthorityRevision && final.ValidUntil == initial.ValidUntil;
    }

    private static DeletionCapabilityRevocationReceipt? Capture(DeletionCapabilityRevocationReceipt? receipt,
        DeletionCapabilityRevocationEnvelope envelope, PrivateOwnerOperationDeadline budget)
    {
        if (receipt is null) { return null; }
        if (receipt.Envelope != envelope || receipt.OwnerRevision <= 0 || receipt.KeyBlockSetRevision <= 0
            || string.IsNullOrWhiteSpace(receipt.ReceiptId) || receipt.ReceiptId.Length > 2048
            || receipt.AffectedBatchIds is null || receipt.AffectedBatchIds.Count is < 0 or > 1000) { throw new InvalidOperationException("Malformed revocation block outcome."); }
        var ids = new List<string>();
        foreach (string id in receipt.AffectedBatchIds)
        {
            budget.Check();
            if (ids.Count >= 1000 || string.IsNullOrWhiteSpace(id) || id.Length > 2048
                || ids.Count > 0 && string.CompareOrdinal(ids[^1], id) >= 0) { throw new InvalidOperationException("Malformed revocation batch coverage."); }
            ids.Add(id);
        }
        budget.Check(); return receipt with { AffectedBatchIds = ids.AsReadOnly() };
    }

    private static bool Same(DeletionCapabilityRevocationReceipt first, DeletionCapabilityRevocationReceipt second)
        => first.Envelope == second.Envelope && first.OwnerRevision == second.OwnerRevision
            && first.KeyBlockSetRevision == second.KeyBlockSetRevision && first.ReceiptId == second.ReceiptId
            && first.AffectedBatchIds.SequenceEqual(second.AffectedBatchIds);
}
