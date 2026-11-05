namespace Hexalith.Platform.Composition;

using System.Reflection;
using System.Security.Cryptography;
using Hexalith.Builds.Tooling.Runtime;

/// <summary>Recognizes only the existing published AppHost process that exits before Build/Run during validation.</summary>
internal static class PlatformPartiesValidationContext
{
    /// <summary>Recognizes complete paired members of the authenticated signed 4.44.3/fc212, 4.44.4/5fa2 and 4.44.5/afb2 archives.</summary>
    /// <param name="appHostHash">The measured executing entry body.</param>
    /// <param name="toolingHash">The measured loaded Tooling body.</param>
    internal static bool IsPublishedPair(string? appHostHash, string? toolingHash)
        => (appHostHash, toolingHash) is
            ("23fbcd1b7e495a85920a44b1d7fd690cee69548dadd9ae02768d8d29b355577b", "da4c0db5b9746609629f8ba20aadb06c5c792521149ce985b1fa56c8f4efb000")
            or ("e85148b3809600a8f6e81c1163618c0250b179f8f9e5435a46c2ad5fe119dc10", "57eff14f810059878e1c43eafe2b1ee04382ea01cb8fd860cfc6810e9d2505a2")
            or ("acac3d8ee71977f2f70fc5099510d69be695e2ff466f78e06ee9086d86c45066", "445d9219c0670abff9550fa3c028c3ee00ffc4d9b3a4c07e5e14b078f035cbb4");

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
            && IsPublishedPair(Hash(entry.Location), Hash(typeof(CompositionRunPlan).Assembly.Location));
    }

    /// <summary>Compares the complete canonical document after projecting only its nonempty absolute claim directory to null.</summary>
    internal static bool IsExactClaimProjection(CompositionRunPlan recorded, CompositionRunPlan supplied)
        => recorded.ResourceClaimStateDirectory is not null && Path.IsPathFullyQualified(recorded.ResourceClaimStateDirectory)
            && supplied.ResourceClaimStateDirectory is null
            && CompositionDocumentStore.Serialize(recorded with { ResourceClaimStateDirectory = null }) == CompositionDocumentStore.Serialize(supplied);

    private static string? Hash(string path)
    {
        FileInfo file = new(path);
        if (!file.Exists || file.LinkTarget is not null) return null;
        for (DirectoryInfo? parent = file.Directory; parent is not null; parent = parent.Parent)
            if (parent.LinkTarget is not null) return null;
        return Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    }
}
