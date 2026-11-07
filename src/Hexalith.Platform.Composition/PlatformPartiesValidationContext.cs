namespace Hexalith.Platform.Composition;

using System.Reflection;
using System.Security.Cryptography;
using Hexalith.Builds.Tooling.Runtime;

/// <summary>Recognizes only the existing published AppHost process that exits before Build/Run during validation.</summary>
internal static class PlatformPartiesValidationContext
{
    /// <summary>Recognizes complete paired members of the authenticated signed Builds archives.</summary>
    /// <param name="appHostHash">The measured executing entry body.</param>
    /// <param name="toolingHash">The measured loaded Tooling body.</param>
    internal static bool IsPublishedPair(string? appHostHash, string? toolingHash)
        => (appHostHash, toolingHash) is
            ("23fbcd1b7e495a85920a44b1d7fd690cee69548dadd9ae02768d8d29b355577b", "da4c0db5b9746609629f8ba20aadb06c5c792521149ce985b1fa56c8f4efb000")
            or ("e85148b3809600a8f6e81c1163618c0250b179f8f9e5435a46c2ad5fe119dc10", "57eff14f810059878e1c43eafe2b1ee04382ea01cb8fd860cfc6810e9d2505a2")
            or ("acac3d8ee71977f2f70fc5099510d69be695e2ff466f78e06ee9086d86c45066", "445d9219c0670abff9550fa3c028c3ee00ffc4d9b3a4c07e5e14b078f035cbb4")
            or ("2b746f7b8a91e70fe1e2a571dc4ca832c4cb18ca502b60a467347da32a4fe84c", "23ef9fa3ec8a6c85e91a08da434e0fd1990ff1f613e8f36e0951144b09a37b87")
            or ("f353c835fd365703c1e6cff7ee632e9f3e98f53e7acf8033e927772383bef911", "6639eb04d7fc99960b0bcd5ed6c978a7213e86f7c9d2b9963d3d3fe1ecf20c90")
            or ("ad2ed0e8a9a8e6b86ad21718d8856ced45ea3ce819797cd80cb2a11e09f04764", "f37dc97c407b513dfecf99818606c954508c05dcece333d788aa5bc7ff3545e9")
            or ("672f01e4165cd00ea2f6866dd5a2681afe4dc30764e8f31a6f396f03f9dbfef0", "3ce7ca60e805b03efd10f66a31734fc32f8c6568d0d20d9657b6a8af3a0c908a")
            or ("0795a6f45a68a78856670327f85549ef4173652780f31533d3342e2ea07529cf", "c43e11474ce0d269e1492d0924d826538b3cb871869669ac23744a5f2f687632")
            or ("f4a9cb4c9cd81579ccdcec7e40d4254219d5afeb7f7c28dcc5d566164bd0df2d", "c1d319f7c5a5f33219bbe8437f95ff169318ef5265c5827591c547623171c8db")
            or ("ac1b7477337c3921a77e1ca527cf3d76c7feeb2f5c8019bd14c1da9222581d8e", "1bd228fc084776f48ad9faff8ac0c614e0c6dc0c2f03f309d325e0e90cbc1870");

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
