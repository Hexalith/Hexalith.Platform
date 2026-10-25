using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Local concrete case workflow and independent projection. Implementations return only actual owner-derived public facts.</summary>
public interface IQualificationOwnerCaseDriver
{
    /// <summary>Gets the exact compiled case identity.</summary>
    string CaseId { get; }
    /// <summary>Names source/protocol/resource inputs before any effect; no expected outcome is an input.</summary>
    Task<IReadOnlyList<string>> PreflightAsync(JsonObject original,CancellationToken cancellationToken);
    /// <summary>Runs one admitted original operation and observation sequence.</summary>
    Task<JsonObject?> ExecuteAsync(JsonObject original,CancellationToken cancellationToken);
    /// <summary>Looks up only the exact retained original after uncertainty.</summary>
    Task<JsonObject?> LookupAsync(JsonObject original,CancellationToken cancellationToken);
    /// <summary>Reads and projects independently persisted owner originals.</summary>
    Task<JsonObject?> ObserveAsync(JsonObject original,CancellationToken cancellationToken);
}
