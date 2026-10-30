namespace Hexalith.Platform.Protection;

/// <summary>Exact immutable preparation bound to the complete manifest digest, original reservation and both native epochs before trim.</summary>
internal sealed record ErasureRootPreparedSurvivors(string RequestDigest, string ReservationReceiptId, int PredecessorEpoch, int SuccessorEpoch,
    SortedDictionary<string, ErasureRootTarget> Targets);
