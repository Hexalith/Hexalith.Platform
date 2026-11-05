namespace Hexalith.Platform.Composition;

using System.Reflection;
using System.Security.Cryptography;
using Hexalith.Builds.Tooling.Runtime;

/// <summary>Recognizes only the existing published AppHost process that exits before Build/Run during validation.</summary>
internal static class PlatformPartiesValidationContext
{
    // Authenticated members of the accepted signed 4.44.3/fc212 module archive. A different producer requires qualification.
    private const string _appHostHash = "23fbcd1b7e495a85920a44b1d7fd690cee69548dadd9ae02768d8d29b355577b";
    private const string _toolingHash = "da4c0db5b9746609629f8ba20aadb06c5c792521149ce985b1fa56c8f4efb000";

    /// <summary>Requires the genuine validation-only entry bytes, exact existing arguments and consumed parent environment handoff.</summary>
    internal static bool IsCurrentPublishedValidation(CompositionRunPlan plan)
    {
        Assembly? entry = Assembly.GetEntryAssembly();
        string[] arguments = Environment.GetCommandLineArgs();
        string document = Path.Combine(plan.Workspace, "plan.json");
        return entry?.GetName().Name == "Hexalith.Builds.Module.AppHost"
            && arguments.Length == 2 && arguments[0] == entry.Location && arguments[1] == "--validate-platform"
            && Environment.GetEnvironmentVariable(CompositionEnvironment.PlanPath) == document
            && Environment.GetEnvironmentVariable(CompositionEnvironment.RunId) == plan.RunId
            && Environment.GetEnvironmentVariable(CompositionEnvironment.SigningKey) is null
            && HasHash(entry.Location, _appHostHash) && HasHash(typeof(CompositionRunPlan).Assembly.Location, _toolingHash);
    }

    /// <summary>Compares the complete canonical document after projecting only its nonempty absolute claim directory to null.</summary>
    internal static bool IsExactClaimProjection(CompositionRunPlan recorded, CompositionRunPlan supplied)
        => recorded.ResourceClaimStateDirectory is not null && Path.IsPathFullyQualified(recorded.ResourceClaimStateDirectory)
            && supplied.ResourceClaimStateDirectory is null
            && CompositionDocumentStore.Serialize(recorded with { ResourceClaimStateDirectory = null }) == CompositionDocumentStore.Serialize(supplied);

    private static bool HasHash(string path, string expected)
    {
        FileInfo file = new(path);
        if (!file.Exists || file.LinkTarget is not null) return false;
        for (DirectoryInfo? parent = file.Directory; parent is not null; parent = parent.Parent)
            if (parent.LinkTarget is not null) return false;
        return Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) == expected;
    }
}
