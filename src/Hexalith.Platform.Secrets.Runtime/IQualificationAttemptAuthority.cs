using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Fresh independent verification of the retained accepted cohort attempt; never reserves or enrolls an attempt.</summary>
public interface IQualificationAttemptAuthority
{
    /// <summary>Confirms exact target/plan/run binding from the current independently retained cohort.</summary>
    Task<bool> ValidateAsync(JsonObject request,CancellationToken cancellationToken);
}
