using System.Text.Json;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Local receiving implementation bound by the owning process to its actual installed owner ports.</summary>
public interface IIndependentOwnerOperationHandler
{
    /// <summary>Gets the closed set of receiving methods supplied by this owner.</summary>
    IReadOnlySet<string> Methods { get; }
    /// <summary>Executes after current authenticated workload admission; missing local dependencies deny before owner effects.</summary>
    Task<object?> HandleAsync(string method, JsonElement payload, IndependentCallerGrant caller, IndependentAuthorityState current, CancellationToken cancellationToken);
}
