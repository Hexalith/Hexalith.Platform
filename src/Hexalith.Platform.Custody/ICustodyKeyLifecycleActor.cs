using Dapr.Actors;

namespace Hexalith.Platform.Custody;

/// <summary>Private exact wrapped-object metadata/reservation/effect owner; unregistered and deny-default.</summary>
public interface ICustodyKeyLifecycleActor : IActor
{
    /// <summary>Retains independently proved original encrypted wrap/store reference; registration itself never fabricates cryptographic/provider proof.</summary>
    Task<CustodyKeyLifecycleOutcome> RegisterWrappedAsync(CustodyKeyRegistration registration);
    /// <summary>Conditionally pins/unpins/reserves destruction; unknown original resolves only by authenticated exact physical lookup.</summary>
    Task<CustodyKeyLifecycleOutcome> ApplyAsync(CustodyKeyLifecycleRequest request);
    /// <summary>Current private exact operation lookup, without renewing the original physical authority.</summary>
    Task<CustodyKeyLifecycleOutcome> LookupAsync(CustodyKeyLifecycleRequest request);
}
