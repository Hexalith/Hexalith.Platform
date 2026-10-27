using System.Text.Json;

namespace Hexalith.Platform.Protection;

/// <summary>Shipped closed-semantic capability inventory. Enrolled configuration and native read flags cannot replace missing invariant implementations or actual proof protocols.</summary>
public static class JointPhaseSemanticReadiness
{
    /// <summary>Returns the exact unsupported native facts before phase effect admission.</summary>
    public static IReadOnlyList<string> MissingInputs(string caseId)
    {
        using var stream=typeof(JointPhaseSemanticReadiness).Assembly.GetManifestResourceStream("NativePhaseInvariantInventory")!;
        using var document=JsonDocument.Parse(stream);
        foreach(var row in document.RootElement.GetProperty("cases").EnumerateArray())
            if(row.GetProperty("caseId").GetString()==caseId)
                return row.GetProperty("invariants").EnumerateArray().Where(value=>value.GetProperty("missingInput").ValueKind==JsonValueKind.String)
                    .Select(value=>value.GetProperty("missingInput").GetString()!).Distinct(StringComparer.Ordinal).ToArray();
        return ["ClosedNativePhaseCase."+caseId];
    }
}
