using Hexalith.EventStore.Contracts.Security;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using NSubstitute;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Actual DAPR spool source with synthetic conditional serialized component/backend and independent monotonic authority; no replication/live qualification.</summary>
internal sealed class SecuritySpoolFixture
{
    internal CustodyFixtureClock Clock { get; } = new();
    internal DaprClient Client { get; } = Substitute.For<DaprClient>();
    internal IReplicatedSecuritySpoolAuthority Authority { get; } = Substitute.For<IReplicatedSecuritySpoolAuthority>();
    internal ISecurityObservationRecorder Recorder { get; } = Substitute.For<ISecurityObservationRecorder>();
    internal ReplicatedSecuritySpoolTarget Target { get; }
    internal ReplicatedSecurityObservationSpool Spool { get; }
    internal byte[]? Persisted { get; set; }
    internal Dictionary<long, byte[]> Archives { get; } = [];
    internal byte[]? PendingBytes { get; set; }
    internal long PendingEtag { get; set; }
    internal int FailSaveStage { get; set; } = 1;
    private int _saves;
    internal long Anchor { get; set; }
    internal string AnchorDigest { get; set; }
    internal int PhysicalAppends { get; private set; }
    internal bool CommitBeforeSaveFault { get; set; }
    internal bool FailSave { get; set; }
    internal bool LoseAppendAcknowledgement { get; set; }
    internal bool AcceptWithoutPersistence { get; set; }
    internal bool RecoveryPermission { get; set; }
    internal Func<SecurityObservationRecoveryGrant, SecurityObservationRecoveryGrant>? AlterRecoveryGrant { get; set; }
    internal int RecoveryRequests { get; private set; }
    internal Dictionary<string, byte[]> Journal { get; } = [];
    internal Dictionary<string, SecurityEventRecordReceipt> Recorded { get; } = [];
    internal SecuritySpoolFixture()
    {
        Target = new("synthetic-independent-spool", "installed-epoch-1", "qualified-synthetic-binding-1", Clock.Now.AddDays(2));
        AnchorDigest = ReplicatedSecurityObservationSpool.StateDigest(new(Target.InstallationEpoch, 0, []));
        Authority.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(Target);
        Authority.AuthorizeAsync(Target, Arg.Any<string>(), Arg.Any<SecurityObservationIntent?>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.AuthorizeOriginalLookupAsync(Target, Arg.Any<SecurityObservationOriginalLookup>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.ValidateStateAsync(Target, Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<long>() == Anchor && call.Arg<string>() == AnchorDigest);
        Authority.RecordRevisionAsync(Target, Arg.Any<long>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => {
            if (call.ArgAt<long>(1) != Anchor || call.ArgAt<long>(2) != Anchor + 1) { return false; } Anchor++; AnchorDigest = call.Arg<string>(); return true;
        });
        InstallJournal();
        Client.GetStateAndETagAsync<AnchoredStateTransition>(Target.ComponentName, Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => (PendingBytes is null ? null! : JsonSerializer.Deserialize<AnchoredStateTransition>(PendingBytes)!, PendingEtag.ToString(CultureInfo.InvariantCulture)));
        Client.TrySaveStateAsync(Target.ComponentName, Arg.Any<string>(), Arg.Any<AnchoredStateTransition?>(), Arg.Any<string>(), Arg.Any<StateOptions>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (call.ArgAt<string>(3) != PendingEtag.ToString(CultureInfo.InvariantCulture)) { return Task.FromResult(false); }
            bool fail = FailSave && ++_saves == FailSaveStage;
            if (!fail || CommitBeforeSaveFault) { var pending = call.Arg<AnchoredStateTransition?>(); PendingBytes = pending is null ? null : JsonSerializer.SerializeToUtf8Bytes(pending); PendingEtag++; }
            return fail ? Task.FromException<bool>(new HttpRequestException("Controlled pending CAS failure.")) : Task.FromResult(true);
        });
        Client.GetStateAndETagAsync<SecuritySpoolSnapshot>(Target.ComponentName, Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => (Read()!, (Read()?.Revision ?? 0).ToString(CultureInfo.InvariantCulture)));
        Client.TrySaveStateAsync(Target.ComponentName, Arg.Any<string>(), Arg.Any<SecuritySpoolSnapshot>(), Arg.Any<string>(), Arg.Any<StateOptions>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call => {
                if (call.ArgAt<string>(3) != (Read()?.Revision ?? 0).ToString(CultureInfo.InvariantCulture)) { return Task.FromResult(false); }
                bool fail = FailSave && ++_saves == FailSaveStage;
                if (!fail || CommitBeforeSaveFault) { Persisted = JsonSerializer.SerializeToUtf8Bytes(call.Arg<SecuritySpoolSnapshot>()); }
                return fail ? Task.FromException<bool>(new HttpRequestException("Controlled component save fault.")) : Task.FromResult(true);
            });
        Client.GetStateAndETagAsync<SecuritySpoolArchivePage>(Target.ComponentName, Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                long index = ArchiveIndex(call.ArgAt<string>(1));
                return (Archives.TryGetValue(index, out var bytes) ? JsonSerializer.Deserialize<SecuritySpoolArchivePage>(bytes)! : null!, Archives.ContainsKey(index) ? "1" : "0");
            });
        Client.TrySaveStateAsync(Target.ComponentName, Arg.Any<string>(), Arg.Any<SecuritySpoolArchivePage>(), Arg.Any<string>(), Arg.Any<StateOptions>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                long index = ArchiveIndex(call.ArgAt<string>(1));
                if (call.ArgAt<string>(3) != "0" || Archives.ContainsKey(index)) { return Task.FromResult(false); }
                Archives[index] = JsonSerializer.SerializeToUtf8Bytes(call.Arg<SecuritySpoolArchivePage>());
                return Task.FromResult(true);
            });
        Recorder.LookupAsync(Arg.Any<SecurityObservationRecord>(), Arg.Any<CancellationToken>()).Returns(call => {
            var record = call.Arg<SecurityObservationRecord>(); return Recorded.TryGetValue(record.Intent.ObservationId, out var receipt)
                ? new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.Recorded, receipt) : new(SecurityEventRecorderLookupState.NotRecorded);
        });
        Recorder.AppendAsync(Arg.Any<SecurityObservationRecord>(), Arg.Any<CancellationToken>()).Returns(call => {
            var record = call.Arg<SecurityObservationRecord>(); PhysicalAppends++;
            if (!AcceptWithoutPersistence) { Recorded[record.Intent.ObservationId] = Receipt(record); }
            return LoseAppendAcknowledgement ? Task.FromException(new HttpRequestException("Controlled recorder response loss.")) : Task.CompletedTask;
        });
        var recoveryGrants = new HashSet<string>(StringComparer.Ordinal);
        Authority.AuthorizeAgedOriginalRecoveryAsync(Target, Arg.Any<SecurityObservationRecord>(), Arg.Any<SecurityEventRecorderLookup>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var original = call.Arg<SecurityObservationRecord>(); var proof = call.Arg<SecurityEventRecorderLookup>();
            if (!RecoveryPermission || Read()?.Records.SingleOrDefault(record => record.Sequence == original.Sequence) is not { } retained
                || retained with { Receipt = null } != original || !AuthenticProof(original, proof)) { return (SecurityObservationRecoveryGrant?)null; }
            var grant = new SecurityObservationRecoveryGrant(Target, original, proof, "separately-installed-original-recovery", Clock.Now, Clock.Now.AddMinutes(5));
            recoveryGrants.Add(JsonSerializer.Serialize(grant)); return AlterRecoveryGrant is null ? grant : AlterRecoveryGrant(grant);
        });
        Authority.VerifyAgedOriginalRecoveryAsync(Arg.Any<SecurityObservationRecoveryGrant>(), Arg.Any<SecurityEventRecorderLookup>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var grant = call.Arg<SecurityObservationRecoveryGrant>();
            return RecoveryPermission && grant.Target == Target && grant.ValidUntil > Clock.Now && recoveryGrants.Contains(JsonSerializer.Serialize(grant))
                && AuthenticProof(grant.Original, call.Arg<SecurityEventRecorderLookup>());
        });
        Recorder.RecoverOriginalAsync(Arg.Any<SecurityObservationRecord>(), Arg.Any<SecurityObservationRecoveryGrant>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            RecoveryRequests++; var original = call.Arg<SecurityObservationRecord>(); var grant = call.Arg<SecurityObservationRecoveryGrant>();
            if (!RecoveryPermission || grant.Original != original || !recoveryGrants.Contains(JsonSerializer.Serialize(grant)) || grant.ValidUntil <= Clock.Now)
            { return Task.FromException(new InvalidOperationException("Independent original recovery denied.")); }
            if (!Recorded.ContainsKey(original.Intent.ObservationId))
            { PhysicalAppends++; if (!AcceptWithoutPersistence) { Recorded[original.Intent.ObservationId] = Receipt(original); } }
            return LoseAppendAcknowledgement ? Task.FromException(new HttpRequestException("Controlled original recovery response loss.")) : Task.CompletedTask;
        });
        bool AuthenticProof(SecurityObservationRecord original, SecurityEventRecorderLookup proof) => proof.State == SecurityEventRecorderLookupState.NotRecorded
            ? proof.Receipt is null && !Recorded.ContainsKey(original.Intent.ObservationId)
            : proof.State == SecurityEventRecorderLookupState.Recorded && Recorded.TryGetValue(original.Intent.ObservationId, out var exact) && proof.Receipt == exact && exact == Receipt(original);
        Spool = new(Client, Clock, Authority, Recorder);
    }
    internal void InstallJournal() => AnchoredFixtureJournal.Attach(Authority, Target.ComponentName + "|system/security-observations/" + Target.InstallationEpoch,
        () => (Anchor, AnchorDigest), (next, digest) => { Anchor = next; AnchorDigest = digest; }, Journal);
    internal SecuritySpoolSnapshot? Read() => Persisted is null ? null : JsonSerializer.Deserialize<SecuritySpoolSnapshot>(Persisted);
    private static long ArchiveIndex(string key) => long.Parse(key[(key.LastIndexOf('/') + 1)..], CultureInfo.InvariantCulture);
    internal static SecurityObservationIntent Intent(string id = "observation-1") => new(id, "tenant-a", "invalid-tag",
        Convert.ToHexString(HMACSHA256.HashData(new byte[32], "synthetic-untrusted-secret"u8)), "system-observation-key-v1");
    internal static SecurityEventRecordReceipt Receipt(SecurityObservationRecord record) => new(record.Intent.ObservationId, record.Intent.RoutingTenantId, record.UtcDay,
        ReplicatedSecurityObservationSpool.IntentDigest(record.Intent), ReplicatedSecurityObservationSpool.SourceStream(record), 1, "persisted-event-" + record.Intent.ObservationId);
}
