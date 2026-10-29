using Dapr.Client;

namespace Hexalith.Platform.Protection;

/// <summary>Isolated native call-boundary faults. They throw around actual operations and cannot fabricate provider results or successful observations.</summary>
public sealed class ProtectionQualificationFaults(DaprClient nativeControls, ProtectionQualificationNativeCell control, string enrollmentId,
    IReadOnlySet<string> allowedOperations)
{
    /// <summary>Arms one exact before/after acknowledgement fault at an already enrolled fixture operation.</summary>
    public Task ArmAsync(string stepId, string boundary, CancellationToken token)
    {
        if (!allowedOperations.Contains(stepId) || boundary is not ("before" or "after")) { throw new ArgumentException("Exact independently enrolled native fault boundary required."); }
        return ChangeAsync(state =>
        {
            if (state.Armed.ContainsKey(stepId) || state.Consumed.ContainsKey(stepId)) { throw new InvalidOperationException("Fault original already armed."); }
            state.Armed.Add(stepId, boundary); return true;
        }, token);
    }
    /// <summary>Applies the once-only fault without changing any native return value.</summary>
    public async Task CheckAsync(string stepId, string boundary, CancellationToken token)
    {
        if (!allowedOperations.Contains(stepId)) { return; }
        bool applied = false;
        await ChangeAsync(state =>
        {
            applied = false;
            if (!state.Armed.TryGetValue(stepId, out var original) || original != boundary) { return false; }
            state.Armed.Remove(stepId); state.Consumed.Add(stepId, boundary); applied = true; return true;
        }, token).ConfigureAwait(false);
        if (applied) { throw new IOException("Isolated native acknowledgement boundary."); }
    }
    private async Task ChangeAsync(Func<ProtectionQualificationFaultState, bool> change, CancellationToken token)
    {
        for (int attempt = 0; attempt < 16; attempt++)
        {
            var (state, tag) = await nativeControls.GetStateAndETagAsync<ProtectionQualificationFaultState>(control.Component, control.Key, ConsistencyMode.Strong, control.Metadata, token).ConfigureAwait(false);
            if (state is null || state.EnrollmentId != enrollmentId || string.IsNullOrWhiteSpace(tag)) { throw new InvalidOperationException("Independent native fault-control enrollment unavailable."); }
            if (!change(state)) { return; }
            if (await nativeControls.TrySaveStateAsync(control.Component, control.Key, state, tag, new StateOptions { Concurrency = ConcurrencyMode.FirstWrite, Consistency = ConsistencyMode.Strong }, control.Metadata, token).ConfigureAwait(false)) { return; }
        }
        throw new InvalidOperationException("Native fault-control outcome unresolved.");
    }
}
