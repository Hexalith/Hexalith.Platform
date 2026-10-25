using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>An independently installed role provider. Implementations must use actual owner state and a separately held signing key.</summary>
public interface IQualificationRoleProvider
{
    /// <summary>Gets the exact enrolled role: executor, observer, or enrollment.</summary>
    string Role { get; }

    /// <summary>Checks current accepted target/configuration/plan/anchors, authenticated caller, isolated scope and all required owner ACLs.</summary>
    Task<bool> AuthorizeAsync(JsonObject request, QualificationCallerContext caller, CancellationToken cancellationToken);

    /// <summary>Reads actual owner results and returns the independently signed public protocol envelope. Expected outcomes are never supplied.</summary>
    Task<JsonObject?> InvokeAsync(string operation, JsonObject original, CancellationToken cancellationToken);
}
