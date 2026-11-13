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
            or ("ac1b7477337c3921a77e1ca527cf3d76c7feeb2f5c8019bd14c1da9222581d8e", "1bd228fc084776f48ad9faff8ac0c614e0c6dc0c2f03f309d325e0e90cbc1870")
            or ("5d3ed600a11c921ad1ca8d98e90430c000681ab2b9d41dfb7d5df3a0dae6f252", "39583273ccfbf9fac6b921e1996c109cc66b0d5336ff59fc7ac70923edcf0d2d")
            or ("0afbfd9bfe10a6d28cce6b09e375e1cfbbd581bcc6d501157eccacd3e3cc2954", "0c8c00e5563730f953f24b19b777dedd1887f4d186e92347145e55783d4d4981")
            or ("6aae774cfcf41e0488cf7b2517d67897f1726c68674219fea164d6998ad60ebe", "35d0eb2b0f32ad5e3b1e4353f088800e76084b99e60f46838e6e497105f64f01")
            or ("adeffaefcbf7207711d7a8290d975edc9a6ffee446d0ad40194ad96b5299df70", "592785b5fc3426321fec66538975988a74573df50f11f42bfd82268869bf23ab")
            or ("f8f7b6e743a798e37784f5e3d44f62bc81c5cef112c8fa9ec643fcb35da825ba", "142bd3fdd26ff4e5ba8eadd91d00de92b738b019270fa6742e3a35f41460b1cf")
            or ("7ccaa52a5fb6144bb570fddc1d98514822c9ef22ceccbdc2c3ae840afa701340", "e2248aa6b6978385a369f04668f167360775db72cb3bd773903af51266d77a6f")
            or ("c4bf26c96e72203c1143b92830dc8feeadc8cb88380dd16545a21abb97bbc63b", "6ceb2bcd28d7b99927b5b6e84b6e4e7ea4b5c485337003bcfdb04b752d9e7261")
            or ("a0fbad5e3639f9682eceb910c7b3250bd61c7ab2d0a6a9c8290140412a010aef", "550bfe2b2841e65dc97452d9f995709bcb27dba6e650109ffab6f02be825e6bc")
            or ("c5d2804f71532125f5f53dfc1ddeef8acbd2ed757201615d77a5471c28296a1b", "d75fb09296700b793ecbcf437836597878705dfd7eefe3314c80171427ff38ff")
            or ("a229438e01238f7e0ff2063eefb9595f37b072c893f3f3d138a348c1b970305f", "2c1fd7d2647e1460a438119e39d8e137f98d48857a6bdc028ab69925e5b09088");

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
