using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;
using NSubstitute;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Synthetic independently durable exact transition journal; backend restoration cannot advance or forge it.</summary>
internal static class AnchoredFixtureJournal
{
    private static readonly ConditionalWeakTable<IAnchoredStateTransitionAuthority, Dictionary<string, string>> Admissions = new();
    private static readonly ConditionalWeakTable<IAnchoredStateTransitionAuthority, StrongBox<bool>> Availability = new();
    private static readonly ConditionalWeakTable<IAnchoredStateTransitionAuthority, StrongBox<(Func<Task> Before, Action Finished)?>> RecordHooks = new();
    internal static void SuspendRecord(IAnchoredStateTransitionAuthority authority, Func<Task> before, Action finished)
        => RecordHooks.GetValue(authority, _ => new(null)).Value = (before, finished);
    /// <summary>Controls the independently retained synthetic journal without changing original admission or current anchor.</summary>
    internal static void SetAvailable(IAnchoredStateTransitionAuthority authority, bool available) => Availability.GetValue(authority, _ => new(true)).Value = available;
    internal static void Attach(IAnchoredStateTransitionAuthority authority, string scope, Func<(long Revision, string Digest)> current, Action<long, string> advance, Dictionary<string, byte[]>? retained = null)
    {
        var admissions = Admissions.GetValue(authority, _ => new(StringComparer.Ordinal));
        authority.AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var transition = call.Arg<AnchoredStateTransition>(); string exact = JsonSerializer.Serialize(transition);
            if (admissions.TryGetValue(transition.TargetDigest, out var admitted)) { return admitted == exact; }
            var original = current();
            if (transition.ScopeId != scope || transition.ExpectedRevision != original.Revision || transition.TargetRevision != original.Revision + 1
                || transition.PredecessorDigest != original.Digest) { return false; }
            admissions.Add(transition.TargetDigest, exact); return true;
        });
        authority.RecoverTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var transition = call.Arg<AnchoredStateTransition>(); return admissions.TryGetValue(transition.TargetDigest, out var admitted) && admitted == JsonSerializer.Serialize(transition)
                ? authority.RecordTransitionAsync(transition, call.Arg<CancellationToken>()) : Task.FromResult(false);
        });
        var proofs = retained ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);
        authority.RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var hook = RecordHooks.GetValue(authority, _ => new(null)).Value;
            try
            {
                if (hook is { } suspended) { await suspended.Before(); }
            var transition = call.Arg<AnchoredStateTransition>(); var original = current();
            if (!Availability.GetValue(authority, _ => new(true)).Value) { return false; }
            if (transition.ScopeId != scope || transition.ExpectedRevision != original.Revision || transition.TargetRevision != original.Revision + 1
                || transition.PredecessorDigest != original.Digest || Convert.ToHexString(SHA256.HashData(transition.TargetBytes)) != transition.TargetDigest) { return false; }
            proofs[transition.TargetDigest] = JsonSerializer.SerializeToUtf8Bytes(transition); advance(transition.TargetRevision, transition.TargetDigest); return true;
            }
            finally { hook?.Finished(); }
        });
        authority.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var transition = call.Arg<AnchoredStateTransition>(); return transition.ScopeId == scope && proofs.TryGetValue(transition.TargetDigest, out var proof)
                && proof.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(transition));
        });
    }
}
