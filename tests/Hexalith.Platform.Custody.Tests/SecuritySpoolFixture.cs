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
    internal long Anchor { get; set; }
    internal string AnchorDigest { get; set; }
    internal int PhysicalAppends { get; private set; }
    internal bool CommitBeforeSaveFault { get; set; }
    internal bool FailSave { get; set; }
    internal bool LoseAppendAcknowledgement { get; set; }
    internal bool AcceptWithoutPersistence { get; set; }
    internal Dictionary<string, SecurityEventRecordReceipt> Recorded { get; } = [];
    internal SecuritySpoolFixture()
    {
        Target = new("synthetic-independent-spool", "installed-epoch-1", "qualified-synthetic-binding-1", Clock.Now.AddDays(2));
        AnchorDigest = ReplicatedSecurityObservationSpool.StateDigest(new(Target.InstallationEpoch, 0, []));
        Authority.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(Target);
        Authority.AuthorizeAsync(Target, Arg.Any<string>(), Arg.Any<SecurityObservationIntent?>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.ValidateStateAsync(Target, Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<long>() == Anchor && call.Arg<string>() == AnchorDigest);
        Authority.RecordRevisionAsync(Target, Arg.Any<long>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => {
            if (call.ArgAt<long>(1) != Anchor || call.ArgAt<long>(2) != Anchor + 1) { return false; } Anchor++; AnchorDigest = call.Arg<string>(); return true;
        });
        Client.GetStateAndETagAsync<SecuritySpoolSnapshot>(Target.ComponentName, Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => (Read()!, (Read()?.Revision ?? 0).ToString(CultureInfo.InvariantCulture)));
        Client.TrySaveStateAsync(Target.ComponentName, Arg.Any<string>(), Arg.Any<SecuritySpoolSnapshot>(), Arg.Any<string>(), Arg.Any<StateOptions>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call => {
                if (call.ArgAt<string>(3) != (Read()?.Revision ?? 0).ToString(CultureInfo.InvariantCulture)) { return Task.FromResult(false); }
                if (!FailSave || CommitBeforeSaveFault) { Persisted = JsonSerializer.SerializeToUtf8Bytes(call.Arg<SecuritySpoolSnapshot>()); }
                return FailSave ? Task.FromException<bool>(new HttpRequestException("Controlled component save fault.")) : Task.FromResult(true);
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
        Spool = new(Client, Clock, Authority, Recorder);
    }
    internal SecuritySpoolSnapshot? Read() => Persisted is null ? null : JsonSerializer.Deserialize<SecuritySpoolSnapshot>(Persisted);
    internal static SecurityObservationIntent Intent(string id = "observation-1") => new(id, "tenant-a", "invalid-tag",
        Convert.ToHexString(HMACSHA256.HashData(new byte[32], "synthetic-untrusted-secret"u8)), "system-observation-key-v1");
    internal static SecurityEventRecordReceipt Receipt(SecurityObservationRecord record) => new(record.Intent.ObservationId, record.Intent.RoutingTenantId, record.UtcDay,
        ReplicatedSecurityObservationSpool.IntentDigest(record.Intent), ReplicatedSecurityObservationSpool.SourceStream(record), 1, "persisted-event-" + record.Intent.ObservationId);
}
