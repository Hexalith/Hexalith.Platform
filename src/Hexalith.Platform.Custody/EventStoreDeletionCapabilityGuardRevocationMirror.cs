using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Private exact protection-block mirror into the actual conditional tenant guard owner; no signing or consuming authority is exposed.</summary>
/// <param name="guard">Installed private EventStore guard with independent receipt authentication and current lookup authority.</param>
/// <param name="clock">Optional operational deadline clock.</param>
public sealed class EventStoreDeletionCapabilityGuardRevocationMirror(IGovernanceScopeGuard guard, TimeProvider? clock = null) : IDeletionCapabilityGuardRevocationMirror
{
    /// <inheritdoc/>
    public async Task<bool> RecordAsync(DeletionCapabilityRevocationReceipt receipt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var deadline = new PrivateOwnerOperationDeadline(clock ?? TimeProvider.System, cancellationToken);
        receipt = await deadline.ReadAsync(() => Task.FromResult(Capture(receipt))).ConfigureAwait(false);
        var state = await deadline.ReadAsync(() => guard.ReadAsync(receipt.Envelope.TenantId, CancellationToken.None)).ConfigureAwait(false);
        if (state is null || state.TenantId != receipt.Envelope.TenantId) { return false; }
        var original = await deadline.ReadAsync(() => Task.FromResult(state.Revocations.SingleOrDefault(value => value.Envelope == receipt.Envelope))).ConfigureAwait(false);
        if (original is not null) { return Hash(original) == Hash(receipt); }
        var batch = new GovernanceBatchCommand("", "", 0, [], "", 0, receipt.Envelope.KeyVersion, "", "", "", [], receipt.KeyBlockSetRevision);
        var command = new GovernanceGuardTransition(receipt.Envelope.TenantId, "revocation-" + Hash(receipt.Envelope), GovernanceGuardOperation.RecordKeyCompromise,
            state.Revision, "", "", null, null, null, null, null, batch, "", "") { RevocationReceipt = receipt };
        var result = await deadline.ReadAsync(() => guard.ExecuteAsync(command, [], CancellationToken.None)).ConfigureAwait(false);
        return result?.Status == "Committed";
    }

    /// <inheritdoc/>
    public async Task<DeletionCapabilityRevocationReceipt?> LookupAsync(DeletionCapabilityRevocationEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        Validate(envelope);
        var deadline = new PrivateOwnerOperationDeadline(clock ?? TimeProvider.System, cancellationToken);
        var state = await deadline.ReadAsync(() => guard.ReadAsync(envelope.TenantId, CancellationToken.None)).ConfigureAwait(false);
        return state is not null && state.TenantId == envelope.TenantId
            ? await deadline.ReadAsync(() => Task.FromResult(state.Revocations.SingleOrDefault(value => value.Envelope == envelope))).ConfigureAwait(false) : null;
    }

    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static DeletionCapabilityRevocationReceipt Capture(DeletionCapabilityRevocationReceipt value)
    {
        Validate(value.Envelope);
        if (!Text(value.ReceiptId) || value.OwnerRevision <= 0 || value.KeyBlockSetRevision <= 0) { throw new ArgumentException("Malformed exact block receipt."); }
        var ids = new List<string>();
        foreach (string id in value.AffectedBatchIds)
        {
            if (ids.Count >= 1000 || !Text(id) || ids.Count > 0 && string.CompareOrdinal(ids[^1], id) >= 0) { throw new ArgumentException("Malformed complete block coverage."); }
            ids.Add(id);
        }
        return value with { AffectedBatchIds = ids.AsReadOnly() };
    }
    private static void Validate(DeletionCapabilityRevocationEnvelope value)
    {
        if (value.KeyFamily != "DeletionBatchCapabilitySigningKey" || value.RevocationRevision <= 0 || value.TrustProfileRevision <= 0
            || !new[] { value.Issuer, value.Audience, value.TenantId, value.KeyVersion, value.EventIdentity }.All(Text)
            || value.SignatureDigest is not { Length: 64 } || !value.SignatureDigest.All(char.IsAsciiHexDigit)) { throw new ArgumentException("Malformed exact revocation."); }
    }
    private static bool Text(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048) { return false; }
        _ = new UTF8Encoding(false, true).GetByteCount(value); return true;
    }
}
