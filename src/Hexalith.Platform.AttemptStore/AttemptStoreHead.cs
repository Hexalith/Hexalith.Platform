using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Builds.Tooling.ReleaseRecords;

namespace Hexalith.Platform.AttemptStore;

internal sealed record AttemptStoreHead(
    string Schema,
    string Environment,
    long Revision,
    string? Previous,
    long Epoch,
    string? Holder,
    ImmutableDictionary<string, StoredAttempt> Attempts,
    StoredRecord? LatestWorking,
    StoredStop? Stop,
    ImmutableDictionary<string, StoredDecision> Decisions,
    ImmutableDictionary<string, CommittedTransition> Commits,
    ImmutableDictionary<string, RecordReference> ActionIds)
{
    internal const int MaximumBytes = 32 * 1024 * 1024;

    private const string SchemaId = "hexalith.attempt-store.head.v1";
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 16,
    };

    internal static AttemptStoreHead Empty(string environment) =>
        new(SchemaId, environment, 0, null, 0, null, [], null, null, [], [], []);

    internal static AttemptStoreHead Decode(byte[] bytes, string environment)
    {
        try
        {
            AttemptStoreHead head = JsonSerializer.Deserialize<AttemptStoreHead>(bytes, _json) ??
                throw new RecordValidationException("invalid-head");
            if (head.Schema != SchemaId || head.Environment != environment || head.Revision < 0 ||
                head.Epoch < 0 || head.Epoch > head.Revision || head.Attempts is null || head.Decisions is null ||
                head.Commits is null || head.ActionIds is null ||
                (head.Revision == 0 ? head.Previous is not null : !StoredRecord.IsDigest(head.Previous)) ||
                head.Commits.Count != head.Revision ||
                (head.Holder is not null && !head.Attempts.ContainsKey(head.Holder)) ||
                head.Attempts.Any(entry => entry.Value?.Record is null || entry.Value.Terminal == (entry.Key == head.Holder)) ||
                head.Decisions.Values.Any(decision => decision?.Origin is null || decision.Binding is null || decision.Release is null) ||
                (head.Stop is { } stop && (stop.Revision <= 0 || stop.ClearedThrough < 0 || stop.ClearedThrough > stop.Revision ||
                    (stop.ClearedThrough == 0) != (stop.LastClear is null))) ||
                (environment != AttemptStore.Staging && head.Decisions.Count != 0))
            {
                throw new RecordValidationException("invalid-head");
            }

            return head;
        }
        catch (JsonException)
        {
            throw new RecordValidationException("invalid-head");
        }
    }

    internal byte[] Encode()
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(this, _json);
        return bytes.Length <= MaximumBytes ? bytes : throw new RecordValidationException("head-too-large");
    }
}

internal sealed record StoredRecord(RecordReference Reference, ReleaseRecordVersion Version, string EnvelopeDigest)
{
    private const int MaximumS3KeyBytes = 1024;

    internal static string Digest(ReadOnlySpan<byte> bytes) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

    internal static bool IsDigest(string? digest) =>
        digest is { Length: 71 } && digest.StartsWith("sha256:", StringComparison.Ordinal) &&
        digest.AsSpan(7).IndexOfAnyExcept("0123456789abcdef") < 0;

    internal static string Key(string environment, string kind, RecordReference reference)
    {
        if (reference?.RecordId is not { Length: > 0 } || !IsDigest(reference.Digest))
        {
            throw new RecordValidationException("invalid-record-reference");
        }

        string prefix = $"environments/{environment}/records/{kind}/";
        string suffix = $"/{reference.Digest}.json";
        string key = prefix + PathSegment(reference.RecordId) + suffix;
        return Encoding.UTF8.GetByteCount(key) <= MaximumS3KeyBytes
            ? key
            : prefix + HashedSegment(reference.RecordId) + suffix;
    }

    private static string PathSegment(string identity)
    {
        string segment = Uri.EscapeDataString(identity);
        // Escape dot segments and reserve "~" for long identities to stay under S3's key byte limit.
        if (segment.Length > 640)
        {
            return HashedSegment(identity);
        }

        return segment switch
        {
            "." => "%2E",
            ".." => "%2E%2E",
            _ => segment.Replace("~", "%7E", StringComparison.Ordinal),
        };
    }

    private static string HashedSegment(string identity) => "~" + Digest(Encoding.UTF8.GetBytes(identity))[7..];
}

internal sealed record StoredAttempt(StoredRecord Record, bool Terminal);

internal sealed record StoredStop(long Revision, StoredRecord Latest, long ClearedThrough, StoredRecord? LastClear);

internal sealed record StoredDecision(
    StoredRecord Origin,
    StagingEvidenceBinding Binding,
    RecordReference Release,
    StoredRecord? Reservation,
    StoredRecord? AdministratorReservation,
    StoredRecord? Terminal);

internal sealed record CommittedTransition(
    string Operation,
    long Revision,
    long Epoch,
    StoredRecord Primary,
    StoredRecord? Companion);
