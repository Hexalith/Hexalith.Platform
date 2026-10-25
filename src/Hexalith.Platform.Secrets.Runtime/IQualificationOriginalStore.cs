using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independently provisioned atomic, durable, nonrollback original store; not an in-memory substitute.</summary>
public interface IQualificationOriginalStore
{
    /// <summary>Atomically retains the complete request before an effect. Existing identities return false; changed bytes must throw.</summary>
    Task<bool> BeginAsync(string role, string requestId, JsonObject original, CancellationToken cancellationToken);

    /// <summary>Retains a signed observation or resolved original. Existing changed bytes must throw.</summary>
    Task RetainAsync(string role, string requestId, JsonObject result, CancellationToken cancellationToken);

    /// <summary>Reads an exact retained result after current private admission; omitted implementations remain unresolved.</summary>
    Task<JsonObject?> LookupResultAsync(string role,string requestId,CancellationToken cancellationToken) => Task.FromResult<JsonObject?>(null);
}
