using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Local executable source workflow. Inputs are independently enrolled identities, never expected pass flags.</summary>
public sealed record QualificationSourceWorkflow(string CaseId,Func<JsonObject,CancellationToken,Task<JsonObject?>> Execute,
    Func<JsonObject,CancellationToken,Task<JsonObject?>> Lookup,Func<JsonObject,CancellationToken,Task<IReadOnlyList<string>>> Preflight);
