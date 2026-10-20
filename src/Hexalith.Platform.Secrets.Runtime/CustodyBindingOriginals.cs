using System.Text.Json;
using System.Diagnostics;

using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Secrets.Runtime;

internal sealed record CustodyBindingOriginalHeaderV1(AuthorityEnrollmentV1 Authority, string OriginalResultReference,
    string TenantId, string OperationId, string RequestDigest, string BatchId, string ReservationReceiptId,
    string ManifestDigest, int TotalBytes, int TargetCount, string OriginalSha256)
{
    internal bool Valid() => CustodyBindingContract.ValidEnrollment(Authority) && CustodyBindingContract.Text(OriginalResultReference)
        && CustodyBindingContract.Text(TenantId) && CustodyBindingContract.Text(OperationId) && CustodyBindingContract.Digest(RequestDigest)
        && CustodyBindingContract.Text(BatchId) && CustodyBindingContract.Text(ReservationReceiptId) && CustodyBindingContract.Digest(ManifestDigest)
        && TotalBytes is > 0 and <= CustodyBindingContract.MaximumOriginalBytes && TargetCount is >= 1 and <= 1000
        && CustodyBindingContract.Digest(OriginalSha256);
}

internal sealed record CustodyBindingOriginalPageV1(CustodyBindingOriginalHeaderV1 Header, int Offset, int Length,
    string PageSha256, byte[] Data);

// This is exclusively value-free original metadata. There is deliberately no key/opaque payload extension field.
internal sealed record CustodyBindingOriginalV1(DeletionManifestProviderResult Result, string RequestDigest,
    string ManifestDigest, string RestoreBarrierReceiptId, byte[] Signature);

internal interface ICustodyBindingOriginalPageSource
{
    Task<ReadOnlyMemory<byte>?> ReadAsync(CustodyBindingPageReadV1 exactRead, CancellationToken cancellationToken);
}

internal sealed record CustodyBindingAssemblyResult(DeletionManifestProviderState State, CustodyBindingOriginalV1? Original,
    byte[]? ExactOriginalBytes)
{
    internal static CustodyBindingAssemblyResult Unknown { get; } = new(DeletionManifestProviderState.Unknown, null, null);
}

internal static class CustodyBindingOriginalAssembler
{
    internal static async Task<CustodyBindingAssemblyResult> AssembleAsync(CustodyBindingRequestV1 originalRequest,
        CustodyBindingOriginalHeaderV1 header, ICustodyBindingOriginalPageSource source,
        ICustodyBindingCurrentAuthority authority, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30)) { return CustodyBindingAssemblyResult.Unknown; }
        budget.CancelAfter(timeout);
        long started = Stopwatch.GetTimestamp();
        void CheckBudget()
        {
            budget.Token.ThrowIfCancellationRequested();
            if (Stopwatch.GetElapsedTime(started) >= timeout) { throw new OperationCanceledException(budget.Token); }
        }
        try
        {
            CheckBudget();
            // Snapshot all mutable arrays/vectors before an external await. Never change signed original bytes.
            var request = CustodyBindingContract.Decode(CustodyBindingContract.Encode(originalRequest));
            if (request?.Manifest is not { } manifest || !header.Valid() || header.Authority != request.Envelope.Enrollment
                || header.TenantId != request.Envelope.TenantId || header.OperationId != request.Envelope.OperationId
                || header.RequestDigest != request.Envelope.RequestDigest || header.BatchId != manifest.Original.Capability.BatchId
                || header.ReservationReceiptId != manifest.ProtectionReservationReceiptId
                || header.ManifestDigest != manifest.Original.Capability.ManifestDigest || header.TargetCount != manifest.Original.Targets.Count
                || !await authority.VerifyHeaderAsync(request, header, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false))
            { return CustodyBindingAssemblyResult.Unknown; }
            CheckBudget();
            byte[] assembled = new byte[header.TotalBytes];
            int offset = 0;
            while (offset < assembled.Length)
            {
                CheckBudget();
                int count = Math.Min(CustodyBindingContract.MaximumPageBytes, assembled.Length - offset);
                var read = new CustodyBindingPageReadV1(header, offset, count);
                // Authorization is independently current for every exact page, not inferred from a historical header.
                if (!await authority.AuthorizePageAsync(request, read, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false))
                { return CustodyBindingAssemblyResult.Unknown; }
                CheckBudget();
                ReadOnlyMemory<byte>? frame = await source.ReadAsync(read, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
                CheckBudget();
                if (frame is null || frame.Value.Length is 0 or >= CustodyBindingContract.MaximumPageFrameBytes)
                { return CustodyBindingAssemblyResult.Unknown; }
                CustodyBindingContract.RejectDuplicateProperties(frame.Value.Span);
                var page = JsonSerializer.Deserialize<CustodyBindingOriginalPageV1>(frame.Value.Span, CustodyBindingContract.Json);
                if (page is null || page.Header != header || page.Offset != offset || page.Length != count || page.Data is null
                    || page.Data.Length != count || page.Data.Length > CustodyBindingContract.MaximumPageBytes
                    || page.PageSha256 != CustodyBindingContract.Hash(page.Data)) { return CustodyBindingAssemblyResult.Unknown; }
                page.Data.CopyTo(assembled, offset);
                offset += count;
            }
            CheckBudget();
            if (CustodyBindingContract.Hash(assembled) != header.OriginalSha256) { return CustodyBindingAssemblyResult.Unknown; }
            CustodyBindingContract.RejectDuplicateProperties(assembled);
            var original = JsonSerializer.Deserialize<CustodyBindingOriginalV1>(assembled, CustodyBindingContract.Json);
            var result = original?.Result;
            if (original is null || result is null || result.State != DeletionManifestProviderState.Consumed
                || result.TenantId != header.TenantId || result.BatchId != header.BatchId || result.ReservationReceiptId != header.ReservationReceiptId
                || original.RequestDigest != header.RequestDigest || original.ManifestDigest != header.ManifestDigest
                || !CustodyBindingContract.CustodyText(original.RestoreBarrierReceiptId) || original.Signature is not { Length: > 0 and <= 8192 }
                || result.TargetReceipts is null || result.TargetReceipts.Count != header.TargetCount
                || !result.TargetReceipts.Select(r => r.ReceiptId).Distinct(StringComparer.Ordinal).Count().Equals(header.TargetCount))
            { return CustodyBindingAssemblyResult.Unknown; }
            for (int i = 0; i < header.TargetCount; i++)
            {
                var receipt = result.TargetReceipts[i];
                if (receipt.Target != manifest.Original.Targets[i] || receipt.OriginalBatchId != header.BatchId
                    || !CustodyBindingContract.CustodyText(receipt.ReceiptId)) { return CustodyBindingAssemblyResult.Unknown; }
            }
            // Only independently verified full vector/signature/restore proof can expose a terminal original.
            if (!await authority.VerifyOriginalAsync(header, original, assembled, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false))
            { return CustodyBindingAssemblyResult.Unknown; }
            CheckBudget();
            return new(DeletionManifestProviderState.Consumed, original, assembled);
        }
        catch (Exception) { return CustodyBindingAssemblyResult.Unknown; }
    }
}
