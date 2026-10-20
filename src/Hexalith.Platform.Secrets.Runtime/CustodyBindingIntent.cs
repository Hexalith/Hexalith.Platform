namespace Hexalith.Platform.Secrets.Runtime;

internal enum CustodyBindingIntentPhase { Prepared, SendCommitted, ResultDurable, Closed }

internal sealed record NativeIntentV1(string TenantId, string OperationId, string IntentId, string RequestDigest,
    string ProducerIncarnation, string SourceGeneration, string DestinationGeneration, string TargetDigest,
    long ExpectedHeadRevision, CustodyBindingIntentPhase Phase);

internal sealed record CustodyBindingDeliveryV1(CustodyBindingKeyUseV1 KeyUse, CustodyBindingIntentPhase Phase,
    bool FrozenOrErased, bool IoComplete, bool BuffersComplete, bool HolderInventoryComplete);

// Pure validation of persisted owner-supplied facts. These functions do not persist, send, zero buffers or prove closure.
internal static class CustodyBindingIntentRules
{
    internal static bool SameEnrollment(AuthorityEnrollmentV1 original, AuthorityEnrollmentV1 incoming)
        => CustodyBindingContract.ValidEnrollment(original) && original == incoming;

    internal static bool Valid(NativeIntentV1 i) => CustodyBindingContract.Text(i.TenantId) && CustodyBindingContract.Text(i.OperationId)
        && CustodyBindingContract.Text(i.IntentId) && CustodyBindingContract.Digest(i.RequestDigest)
        && CustodyBindingContract.Text(i.ProducerIncarnation) && CustodyBindingContract.Text(i.SourceGeneration)
        && CustodyBindingContract.Text(i.DestinationGeneration) && CustodyBindingContract.Digest(i.TargetDigest)
        && i.ExpectedHeadRevision >= 0 && Enum.IsDefined(i.Phase);

    internal static bool CanAdvance(NativeIntentV1 retained, NativeIntentV1 next, bool valueFreeResultDurable,
        bool ioComplete, bool buffersComplete, bool holdersComplete)
    {
        if (!Valid(retained) || !Valid(next) || retained with { Phase = next.Phase } != next) { return false; }
        if (retained.Phase == next.Phase) { return true; } // status-only exact duplicate
        return (retained.Phase, next.Phase) switch
        {
            (CustodyBindingIntentPhase.Prepared, CustodyBindingIntentPhase.SendCommitted) => true,
            (CustodyBindingIntentPhase.SendCommitted, CustodyBindingIntentPhase.ResultDurable) => valueFreeResultDurable,
            (CustodyBindingIntentPhase.ResultDurable, CustodyBindingIntentPhase.Closed) => valueFreeResultDurable && ioComplete && buffersComplete && holdersComplete,
            _ => false,
        };
    }

    internal static bool DuplicateIsLookupOnly(NativeIntentV1 retained) => Valid(retained) && retained.Phase != CustodyBindingIntentPhase.Prepared;
    private static bool ValidDelivery(CustodyBindingDeliveryV1 d) => d.KeyUse is { } k && Enum.IsDefined(d.Phase)
        && CustodyBindingContract.Text(k.KeyUseLeaseId) && CustodyBindingContract.Text(k.DeliveryAttemptId)
        && CustodyBindingContract.Text(k.RecipientProcessIncarnation) && CustodyBindingContract.Text(k.TransportIncarnation)
        && CustodyBindingContract.Text(k.OriginalRootReference);
    internal static bool CanRelease(CustodyBindingDeliveryV1 d) => ValidDelivery(d) && d.Phase == CustodyBindingIntentPhase.Prepared && !d.FrozenOrErased;
    internal static bool CanClose(CustodyBindingDeliveryV1 d) => ValidDelivery(d) && d.Phase == CustodyBindingIntentPhase.ResultDurable
        && d.IoComplete && d.BuffersComplete && d.HolderInventoryComplete;
}
