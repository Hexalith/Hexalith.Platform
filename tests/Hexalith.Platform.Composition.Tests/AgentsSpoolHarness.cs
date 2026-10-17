namespace Hexalith.Platform.Composition.Tests;

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using NSubstitute;

/// <summary>In-memory conditional spool used to observe lost acknowledgement without installing storage.</summary>
internal sealed class AgentsSpoolHarness
{
    internal ManualClock Clock { get; } = new();
    internal DaprClient Client { get; } = Substitute.For<DaprClient>();
    internal IReplicatedSecuritySpoolAuthority Authority { get; } = Substitute.For<IReplicatedSecuritySpoolAuthority>();
    internal ISecurityObservationRecorder Recorder { get; } = Substitute.For<ISecurityObservationRecorder>();
    internal ReplicatedSecuritySpoolTarget Target { get; }
    internal ReplicatedSecurityObservationSpool Spool { get; }
    internal byte[]? Persisted { get; set; }
    internal Dictionary<string, byte[]> Archives { get; } = new(StringComparer.Ordinal);
    internal byte[]? PendingBytes { get; set; }
    internal long PendingEtag { get; set; }
    internal long Anchor { get; set; }
    internal string AnchorDigest { get; set; }
    private readonly Dictionary<string, byte[]> _journal = [];

    internal AgentsSpoolHarness()
    {
        Target = new("synthetic-independent-spool", "installed-epoch-1", "qualified-synthetic-binding-1", Clock.Now.AddDays(2));
        AnchorDigest = ReplicatedSecurityObservationSpool.StateDigest(new(Target.InstallationEpoch, 0, []));
        Authority.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(Target);
        Authority.AuthorizeAsync(Target, Arg.Any<string>(), Arg.Any<SecurityObservationIntent?>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.AuthorizeOriginalLookupAsync(Target, Arg.Any<SecurityObservationOriginalLookup>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.ValidateStateAsync(Target, Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<long>() == Anchor && call.Arg<string>() == AnchorDigest);
        Authority.RecordRevisionAsync(Target, Arg.Any<long>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (call.ArgAt<long>(1) != Anchor || call.ArgAt<long>(2) != Anchor + 1)
                return false;
            Anchor++;
            AnchorDigest = call.Arg<string>();
            return true;
        });
        AgentsAnchoredJournal.Attach(Authority, Target.ComponentName + "|system/security-observations/" + Target.InstallationEpoch,
            () => (Anchor, AnchorDigest), (next, digest) => { Anchor = next; AnchorDigest = digest; }, _journal);
        Client.GetStateAndETagAsync<AnchoredStateTransition>(Target.ComponentName, Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => (PendingBytes is null ? null! : JsonSerializer.Deserialize<AnchoredStateTransition>(PendingBytes)!, PendingEtag.ToString(CultureInfo.InvariantCulture)));
        Client.TrySaveStateAsync(Target.ComponentName, Arg.Any<string>(), Arg.Any<AnchoredStateTransition?>(), Arg.Any<string>(), Arg.Any<StateOptions>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (call.ArgAt<string>(3) != PendingEtag.ToString(CultureInfo.InvariantCulture))
                return Task.FromResult(false);
            AnchoredStateTransition? pending = call.Arg<AnchoredStateTransition?>();
            PendingBytes = pending is null ? null : JsonSerializer.SerializeToUtf8Bytes(pending);
            PendingEtag++;
            return Task.FromResult(true);
        });
        Client.GetStateAndETagAsync<SecuritySpoolSnapshot>(Target.ComponentName, Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => (Read()!, (Read()?.Revision ?? 0).ToString(CultureInfo.InvariantCulture)));
        Client.TrySaveStateAsync(Target.ComponentName, Arg.Any<string>(), Arg.Any<SecuritySpoolSnapshot>(), Arg.Any<string>(), Arg.Any<StateOptions>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (call.ArgAt<string>(3) != (Read()?.Revision ?? 0).ToString(CultureInfo.InvariantCulture))
                return Task.FromResult(false);
            Persisted = JsonSerializer.SerializeToUtf8Bytes(call.Arg<SecuritySpoolSnapshot>());
            return Task.FromResult(true);
        });
        Client.GetStateAndETagAsync<SecuritySpoolArchivePage>(Target.ComponentName, Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string key = call.ArgAt<string>(1);
                return (Archives.TryGetValue(key, out byte[]? bytes) ? JsonSerializer.Deserialize<SecuritySpoolArchivePage>(bytes)! : null!, Archives.ContainsKey(key) ? "1" : "0");
            });
        Client.TrySaveStateAsync(Target.ComponentName, Arg.Any<string>(), Arg.Any<SecuritySpoolArchivePage>(), Arg.Any<string>(), Arg.Any<StateOptions>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        Recorder.LookupAsync(Arg.Any<SecurityObservationRecord>(), Arg.Any<CancellationToken>()).Returns(new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.NotRecorded));
        Recorder.AppendAsync(Arg.Any<SecurityObservationRecord>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        Spool = new ReplicatedSecurityObservationSpool(Client, Clock, Authority, Recorder);
    }

    internal SecuritySpoolSnapshot? Read() => Persisted is null ? null : JsonSerializer.Deserialize<SecuritySpoolSnapshot>(Persisted);

    internal static SecurityObservationIntent Intent() => new("observation-1", "tenant-a", "invalid-tag",
        Convert.ToHexString(SHA256.HashData("synthetic-observation"u8)), "system-observation-key-v1");
}

/// <summary>Fixed UTC clock for the synthetic spool.</summary>
internal sealed class ManualClock : TimeProvider
{
    internal DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch.AddYears(50);
    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Synthetic transition journal copied for the composition lost-acknowledgement case.</summary>
internal static class AgentsAnchoredJournal
{
    private static readonly ConditionalWeakTable<IAnchoredStateTransitionAuthority, Dictionary<string, string>> Admissions = new();

    internal static void Attach(IAnchoredStateTransitionAuthority authority, string scope, Func<(long Revision, string Digest)> current, Action<long, string> advance, Dictionary<string, byte[]> retained)
    {
        Dictionary<string, string> admissions = Admissions.GetValue(authority, _ => new Dictionary<string, string>(StringComparer.Ordinal));
        authority.AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            AnchoredStateTransition transition = call.Arg<AnchoredStateTransition>();
            string exact = JsonSerializer.Serialize(transition);
            if (admissions.TryGetValue(transition.TargetDigest, out string? admitted))
                return admitted == exact;
            (long revision, string digest) = current();
            if (transition.ScopeId != scope || transition.ExpectedRevision != revision || transition.TargetRevision != revision + 1 || transition.PredecessorDigest != digest)
                return false;
            admissions.Add(transition.TargetDigest, exact);
            return true;
        });
        authority.RecoverTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            AnchoredStateTransition transition = call.Arg<AnchoredStateTransition>();
            return admissions.TryGetValue(transition.TargetDigest, out string? admitted) && admitted == JsonSerializer.Serialize(transition)
                ? authority.RecordTransitionAsync(transition, call.Arg<CancellationToken>()) : Task.FromResult(false);
        });
        authority.RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            AnchoredStateTransition transition = call.Arg<AnchoredStateTransition>();
            (long revision, string digest) = current();
            if (transition.ScopeId != scope || transition.ExpectedRevision != revision || transition.TargetRevision != revision + 1
                || transition.PredecessorDigest != digest || Convert.ToHexString(SHA256.HashData(transition.TargetBytes)) != transition.TargetDigest)
                return false;
            retained[transition.TargetDigest] = JsonSerializer.SerializeToUtf8Bytes(transition);
            advance(transition.TargetRevision, transition.TargetDigest);
            return true;
        });
        authority.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            AnchoredStateTransition transition = call.Arg<AnchoredStateTransition>();
            return transition.ScopeId == scope && retained.TryGetValue(transition.TargetDigest, out byte[]? proof)
                && proof.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(transition));
        });
    }
}
