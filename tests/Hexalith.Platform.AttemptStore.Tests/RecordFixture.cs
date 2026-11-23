using System.Collections.Immutable;
using System.Security.Cryptography;

using Hexalith.Builds.Tooling.ReleaseRecords;

namespace Hexalith.Platform.AttemptStore.Tests;

internal sealed class RecordFixture : IDisposable
{
    private const string Digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    internal static readonly DateTimeOffset Timestamp = new(2026, 11, 23, 0, 0, 0, TimeSpan.Zero);

    private readonly Dictionary<string, ECDsa> _keys = new(StringComparer.Ordinal);

    internal RecordFixture()
    {
        ImmutableHashSet<string> kinds = ["release", "attempt", "latest-working", "promotion-stop", "administrator", "promotion-decision"];
        List<RecordWriterKey> anchors = [];
        foreach ((string writer, string role, string[] environments) in new (string, string, string[])[]
        {
            ("publisher", "publication", []),
            ("staging", "executor", ["staging"]),
            ("staging-successor", "executor", ["staging"]),
            ("production", "executor", ["production"]),
            ("production-successor", "executor", ["production"]),
            ("monitor", "monitor", ["staging", "production"]),
            ("evaluator", "promotion-evaluator", ["production"]),
            ("admin", "administrator", ["staging", "production"]),
        })
        {
            ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            _keys.Add(writer, key);
            anchors.Add(new(writer, writer + "-key", key.ExportSubjectPublicKeyInfoPem(),
                [role], kinds, [.. environments], role == "administrator", false));
        }

        Trust = new(anchors);
    }

    internal RecordTrustPolicy Trust { get; }

    internal static StagingEvidenceBinding Binding(string id) =>
        new("hexalith.platform.staging-evidence-policy.v2", id);

    internal SignedBytes<ReleaseRecord> Release(string id = "release-a") =>
        Sign(new ReleaseRecord(id, Digest, Map(("module", Digest)), Digest, Digest, Digest, Digest, "1.19.2",
            "realm-v1", "recovery-v1", Map(("smoke", Digest)), ["postgres"], ["kubernetes"], Map(("module", "compatible")),
            Map(("attestation", Digest))), "publisher", environment: null, "record-" + id, id);

    internal SignedBytes<ReleaseRecord> ResignRelease(SignedBytes<ReleaseRecord> release) =>
        Sign(release.Record, release.Version);

    internal SignedBytes<AttemptRecord> Attempt(
        string id, string environment, long epoch, SignedBytes<ReleaseRecord> release,
        string? writer = null, RecordReference? supersedes = null, StagingEvidenceBinding? binding = null,
        RecordReference? observedStop = null, string kind = "release")
    {
        EnvironmentCurrent current = new(Map(("database", "secret-ref")), Map(("database", "read-write")),
            1, 1, Digest, Digest, "realm-v1", Digest, Digest, ["app.example"], Map(("deploy", "credential-ref")));
        AttemptRecord payload = new(release.Reference, kind, current, "normal", epoch, Timestamp, null, null,
            Timestamp.AddMinutes(5), null, null, Timestamp.AddMinutes(5), 30, 30, kind == "release" ? 1 : 0, 0,
            observedStop, "open", "in-progress", supersedes, null)
        {
            StagingEvidence = binding,
        };
        return Sign(payload, writer ?? environment, environment, id, release.Record.Payload.ReleaseId,
            binding is null ? ReleaseRecordVersion.V1 : ReleaseRecordVersion.V2);
    }

    internal SignedBytes<AttemptRecord> Revise(
        SignedBytes<AttemptRecord> attempt, AttemptRecord payload, string? writer = null, int seconds = 1)
    {
        SignedRecord<AttemptRecord> original = attempt.Record;
        writer ??= original.WriterId;
        return Sign(original with
        {
            Payload = payload,
            WriterId = writer,
            KeyId = writer + "-key",
            RecordedUtc = Timestamp.AddSeconds(seconds),
        }, attempt.Version);
    }

    internal SignedBytes<LatestWorkingPointerRecord> Pointer(SignedBytes<AttemptRecord> attempt, string id = "working-pointer") =>
        Sign(new LatestWorkingPointerRecord(attempt.Record.Payload.Release, attempt.Reference, attempt.Record.Payload.LockEpoch),
            attempt.Record.WriterId, attempt.Record.Environment, id, attempt.Record.Environment!);

    internal SignedBytes<PromotionStopRecord> Stop(
        string environment, long revision = 1, RecordReference? previous = null, string cause = "health") =>
        Sign(new PromotionStopRecord(revision, previous, cause, [Digest]), "monitor", environment, "stop-" + revision, environment);

    internal SignedBytes<AdministratorRecord> Clear(
        string id, SignedBytes<PromotionStopRecord> stop, SignedBytes<AttemptRecord> attempt,
        params (string Key, string Value)[] dispositions) =>
        Clear(id, stop, attempt, predecessor: null, dispositions);

    internal SignedBytes<AdministratorRecord> Clear(
        string id, SignedBytes<PromotionStopRecord> stop, SignedBytes<AttemptRecord> attempt,
        RecordReference? predecessor, params (string Key, string Value)[] dispositions) =>
        Sign(new AdministratorRecord("clear-promotion-stop", stop.Record.Environment!, attempt.Reference, stop.Reference,
            stop.Record.Payload.Revision, predecessor, "operator-reviewed", Map(dispositions), null),
            "admin", stop.Record.Environment, id, stop.Record.Environment!);

    internal SignedBytes<PromotionDecisionRecord> Decision(
        string id, string action, StagingEvidenceBinding binding, SignedBytes<ReleaseRecord> release,
        SignedBytes<AttemptRecord>? attempt = null, SignedBytes<PromotionDecisionRecord>? reservation = null,
        SignedBytes<LatestWorkingPointerRecord>? pointer = null, string? writer = null, long? epoch = null)
    {
        (string defaultWriter, string environment) = action switch
        {
            "supersede" => ("staging", "staging"),
            "reserve-admission" => ("evaluator", "production"),
            "reserve-administrator" => ("admin", "production"),
            _ => ("production", "production"),
        };
        return Sign(new PromotionDecisionRecord(action, binding, release.Reference, attempt?.Reference,
            epoch ?? attempt?.Record.Payload.LockEpoch, reservation?.Reference, pointer?.Reference),
            writer ?? defaultWriter, environment, id, release.Record.Payload.ReleaseId, ReleaseRecordVersion.V2);
    }

    internal byte[] UntrustedProductionStop() =>
        ReleaseRecordCodec.Encode(ReleaseRecordCodec.Sign(
            Envelope(new PromotionStopRecord(1, null, "forged", [Digest]), "staging", "production", "bad-stop", "production",
                ReleaseRecordVersion.V1), _keys["staging"]));

    public void Dispose()
    {
        foreach (ECDsa key in _keys.Values)
        {
            key.Dispose();
        }
    }

    private static ImmutableDictionary<string, string> Map(params (string Key, string Value)[] pairs) =>
        pairs.ToImmutableDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private SignedBytes<T> Sign<T>(T payload, string writer, string? environment, string id, string subject,
        ReleaseRecordVersion version = ReleaseRecordVersion.V1) where T : class =>
        Sign(Envelope(payload, writer, environment, id, subject, version), version);

    private SignedBytes<T> Sign<T>(SignedRecord<T> record, ReleaseRecordVersion version) where T : class
    {
        SignedRecord<T> signed = ReleaseRecordCodec.Sign(record, _keys[record.WriterId], version);
        byte[] bytes = ReleaseRecordCodec.Encode(signed, version);
        RecordVerificationResult<T> verified = ReleaseRecordCodec.Verify<T>(bytes, Trust, version);
        Assert.NotNull(verified.Verified);
        return new(bytes, verified.Verified, version);
    }

    private static SignedRecord<T> Envelope<T>(T payload, string writer, string? environment, string id, string subject,
        ReleaseRecordVersion version) where T : class =>
        new(ReleaseRecordCodec.SchemaFor<T>(version), ReleaseRecordCodec.KindFor<T>(), id, writer, writer + "-key",
            subject, environment, Timestamp, Timestamp, payload, "ES256", string.Empty);
}

internal sealed record SignedBytes<T>(byte[] Bytes, VerifiedRecord<T> Verified, ReleaseRecordVersion Version) where T : class
{
    internal SignedRecord<T> Record => Verified.Record;
    internal RecordReference Reference => Verified.Reference;
}
