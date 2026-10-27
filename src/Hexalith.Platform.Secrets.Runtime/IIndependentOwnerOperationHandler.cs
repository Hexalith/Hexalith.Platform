using System.Text.Json;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Local receiving implementation bound by the owning process to its actual installed owner ports.</summary>
public interface IIndependentOwnerOperationHandler
{
    /// <summary>Gets the closed set of receiving methods supplied by this owner.</summary>
    IReadOnlySet<string> Methods { get; }
    /// <summary>Gets whether this exact method performs only current/original reads, without allocating or releasing resources.</summary>
    bool IsReadOnly(string method)=>false;
    /// <summary>Requires the completed read to be ordered against current caller revocation before its proof is returned.</summary>
    bool RequiresCurrentResultAdmission(string method)=>false;
    /// <summary>Captures only the semantic policy used by this result; unrelated journal progress is excluded.</summary>
    string? ResultPolicy(string method,JsonElement payload,IndependentAuthorityState current)=>null;
    /// <summary>Clears receiving-owned sensitive buffers after serialization or any failed result path.</summary>
    void ReleaseResult(string method,object? result) { }
    /// <summary>Recovers an already admitted unknown operation. Unspecified handlers cannot dispatch its effect again.</summary>
    Task<object?> RecoverAsync(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState current,CancellationToken cancellationToken)
        =>Task.FromException<object?>(new InvalidOperationException("Exact admitted owner original recovery protocol required."));
    /// <summary>Executes after current authenticated workload admission; missing local dependencies deny before owner effects.</summary>
    Task<object?> HandleAsync(string method, JsonElement payload, IndependentCallerGrant caller, IndependentAuthorityState current, CancellationToken cancellationToken);
}
