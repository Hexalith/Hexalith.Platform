using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact private recorder transport. The independent receiver owns stream/event ACL enforcement and authoritative lookup.</summary>
public sealed class DaprSecurityObservationRecorder(IndependentDaprAuthority recorder) : ISecurityObservationRecorder
{
    /// <inheritdoc />
    public async Task<SecurityEventRecorderLookup> LookupAsync(SecurityObservationRecord record, CancellationToken cancellationToken = default)
    {
        var result = await recorder.InvokeAsync<OriginalResult>("security-observation-lookup", new { record }, cancellationToken).ConfigureAwait(false);
        return result is not null && result.OriginalSha256 == Digest(record) ? result.Lookup : new(SecurityEventRecorderLookupState.Unknown);
    }
    /// <inheritdoc />
    public async Task AppendAsync(SecurityObservationRecord record, CancellationToken cancellationToken = default)
    {
        var result = await recorder.InvokeAsync<OriginalResult>("security-observation-append", new { record }, cancellationToken).ConfigureAwait(false);
        if (result is null || result.OriginalSha256 != Digest(record) || result.Lookup.State == SecurityEventRecorderLookupState.Unknown)
            throw new InvalidOperationException("Exact recorder outcome unknown; lookup original before recovery.");
    }
    /// <inheritdoc />
    public async Task RecoverOriginalAsync(SecurityObservationRecord original, SecurityObservationRecoveryGrant grant, CancellationToken cancellationToken = default)
    {
        if (grant.Original != original) throw new InvalidOperationException("Changed original recovery denied.");
        var result = await recorder.InvokeAsync<OriginalResult>("security-observation-recover-original", new { original, grant }, cancellationToken).ConfigureAwait(false);
        if (result is null || result.OriginalSha256 != Digest(original) || result.Lookup.State == SecurityEventRecorderLookupState.Unknown)
            throw new InvalidOperationException("Exact original recovery remains unknown.");
    }
    private static string Digest(SecurityObservationRecord record) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(record, CustodyBindingContract.Json)));
    private sealed record OriginalResult(string OriginalSha256, SecurityEventRecorderLookup Lookup);
}
