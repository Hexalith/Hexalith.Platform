using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independently provisioned atomic, durable, nonrollback original store; not an in-memory substitute.</summary>
public interface IQualificationOriginalStore
{
    /// <summary>Checks the actual current independent observer original writer/read capabilities and enrolled archive, without creating a future case original.</summary>
    Task<IReadOnlyList<string>> PreflightObservationAsync(CancellationToken cancellationToken)
        =>Task.FromResult<IReadOnlyList<string>>(["InstalledIndependentObservationOriginalReadWriteAndArchiveCapabilities"]);
    /// <summary>Atomically retains the complete request before an effect. Existing identities return false; changed bytes must throw.</summary>
    Task<bool> BeginAsync(string role, string requestId, JsonObject original, CancellationToken cancellationToken);
    /// <summary>Admits a closed once-only lease/release whose protocol is terminal at admission; no fictitious later result is reserved.</summary>
    Task<bool> BeginFinalAsync(string role,string requestId,JsonObject original,CancellationToken cancellationToken)
        =>Task.FromException<bool>(new InvalidOperationException("Explicit terminal-at-admission original protocol required."));

    /// <summary>Retains a signed observation or resolved original. Existing changed bytes must throw.</summary>
    Task RetainAsync(string role, string requestId, JsonObject result, CancellationToken cancellationToken);

    /// <summary>Distinguishes independently signed exact absence from an admitted original and transport uncertainty. Omitted implementations cannot prove absence.</summary>
    Task<QualificationOriginalAdmissionState> ReadAdmissionAsync(string role,string requestId,JsonObject exactOriginal,CancellationToken cancellationToken)
        =>Task.FromResult(QualificationOriginalAdmissionState.Unknown);

    /// <summary>Reads the immutable admitted request for metadata-only recovery; a missing implementation cannot prove absence.</summary>
    Task<JsonObject?> LookupRequestAsync(string role,string requestId,CancellationToken cancellationToken) => Task.FromResult<JsonObject?>(null);
    /// <summary>Distinguishes authenticated absent requests from an unavailable original reader without inventing a request body.</summary>
    Task<QualificationOriginalLookup> ReadOriginalAsync(string role,string requestId,CancellationToken cancellationToken)=>Task.FromResult(new QualificationOriginalLookup(false));

    /// <summary>Reads an exact retained result after current private admission; omitted implementations remain unresolved.</summary>
    Task<JsonObject?> LookupResultAsync(string role,string requestId,CancellationToken cancellationToken) => Task.FromResult<JsonObject?>(null);
}
