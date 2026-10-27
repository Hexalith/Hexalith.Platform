namespace Hexalith.Platform.Custody.Dapr;

internal sealed record NativeArchiveCapacityState(string EnrollmentDigest,string ArchiveEnrollmentSha256,long RetainedBytes,
    Dictionary<string,NativeCompletionReservation> Pending,Dictionary<string,NativePublicationReservation> Publications);
