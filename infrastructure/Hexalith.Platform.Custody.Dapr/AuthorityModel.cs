using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Platform.Custody;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Custody.Dapr;

internal sealed record ObjectVersion(string Key, string VersionId, string Sha256, int Bytes, DateTimeOffset RetainUntil);
internal sealed record AuthorityHead(long Revision, string Digest, ObjectVersion Checkpoint, string EnrollmentDigest);
internal sealed record StoredOriginal(string RequestDigest, string Operation, ObjectVersion? Response, CustodyBindingRequestV1? Manifest);
internal sealed record StoredRegistration(CustodyKeyRegistration Registration, OuterEnvelopeV1 Envelope, byte[] EncryptedOuterKey, string Epoch);
internal sealed record Pin(string HoldId, string Target, long GuardRevision, string GuardReceipt);
internal enum CyclePhase { Reserved, Prepared, Armed, SendCommitted, AwaitingFinalEvidence, Published }
internal sealed record Cycle(string OperationId, CustodyBindingRequestV1 Manifest, string SourceEpoch, string? SuccessorEpoch,
    CyclePhase Phase, string[] AlternateEpochs, string[] TaintedEpochs, string? PreparationDigest, string? CutIntentId);
internal sealed record Anchor(long Revision, string Digest, AnchoredStateTransition? Admitted, string? AdmissionOperationId);

// Only complete immutable snapshots are selected by the single conditional head. No target shards.
internal sealed record Checkpoint(string TenantId, string EnrollmentDigest, long Revision, string PredecessorDigest,
    string CurrentEpoch, Dictionary<string, StoredRegistration> Registrations, HashSet<string> AllocationExclusions,
    Dictionary<string, Pin> Pins, Dictionary<string, StoredOriginal> Originals, Dictionary<string, NativeIntentV1> NativeIntents,
    Dictionary<string, Anchor> Anchors, Cycle? Cycle, HashSet<string> RevokedSignerIds,
    Dictionary<string, ObjectVersion> NativeResults, Dictionary<string, CustodyBindingDeliveryV1> Deliveries)
{
    internal bool Frozen => Cycle?.Phase is CyclePhase.Reserved or CyclePhase.Prepared;
}
internal sealed record CurrentState(AuthorityHead Head, Checkpoint Checkpoint);
internal sealed class AuthorityUnavailableException(string message) : Exception(message);
internal sealed class AuthorityConflictException() : Exception("Current authority comparison failed.");

internal interface IAuthorityStore
{
    Task<CurrentState> ReadAsync(string tenant, CancellationToken cancellationToken);
    Task<ObjectVersion> PutImmutableAsync(ReadOnlyMemory<byte> bytes, string category, CancellationToken cancellationToken);
    Task<byte[]> ReadImmutableAsync(ObjectVersion version, CancellationToken cancellationToken);
    Task<ObjectVersion> PutIntentResultAsync(ReadOnlyMemory<byte> bytes, string locator, CancellationToken cancellationToken);
    Task<ObjectVersion?> FindIntentResultAsync(string locator, CancellationToken cancellationToken);
    Task<CurrentState> CommitAsync(CurrentState expected, Checkpoint next, CancellationToken cancellationToken);
}
internal static class CustodyJson
{
    internal static byte[] Bytes<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, CustodyBindingContract.Json);
    internal static T Read<T>(ReadOnlySpan<byte> bytes)
    {
        CustodyBindingContract.RejectDuplicateProperties(bytes);
        return JsonSerializer.Deserialize<T>(bytes, CustodyBindingContract.Json) ?? throw new AuthorityUnavailableException("Missing immutable object.");
    }
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    internal static string Target(CustodyKeyObjectIdentity i) => CustodyJson.Hash(PlatformCanonicalBytes.Components([i.TenantId, i.ObjectId, i.KeyAlias]));
    internal static string Interaction(CustodyKeyObjectIdentity i) => CustodyJson.Hash(PlatformCanonicalBytes.Components([i.TenantId, i.ObjectId]));
    internal static string IntentLocator(string tenant, string intent) => "journal/native-result/" + Hash(PlatformCanonicalBytes.Components([tenant, intent]));
}
