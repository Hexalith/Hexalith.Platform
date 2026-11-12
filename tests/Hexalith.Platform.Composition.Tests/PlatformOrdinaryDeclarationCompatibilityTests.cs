namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;
using Shouldly;
using Xunit;

/// <summary>Checks additive declaration compatibility without granting any public package, image or runtime authority.</summary>
public sealed class PlatformOrdinaryDeclarationCompatibilityTests
{
    /// <summary>The exact updated declaration retains each ordinary profile's existing parent proof.</summary>
    [Theory]
    [InlineData("platform-package")]
    [InlineData("lifecycle-package")]
    public void UpdatedDeclarationRetainsOrdinaryPurpose(string profile)
    {
        using PlatformPartiesProofFixture fixture = new();
        PlatformCompositionOptions options = Updated(fixture, profile);
        PlatformPartiesRunProof proof = PlatformPartiesRunProof.Create(options, fixture.Key);
        proof.Matches(options).ShouldBeTrue();
        proof.MatchesCurrent(options).ShouldBeFalse();
        PlatformPartiesPackageCatalogQualification.IsOrdinaryDeclaration(options.RunPlan!.PlatformDeclarationHash).ShouldBeTrue();
    }

    /// <summary>The actual published producer uses uppercase hex without changing the measured declaration bytes.</summary>
    [Fact]
    public async Task ProducerUppercaseHashesRetainTheSameFiniteDeclaration()
    {
        using PlatformPartiesProofFixture fixture = new();
        PlatformCompositionOptions options = Updated(fixture, "platform-package");
        ModuleTestSelection selected = options.RunPlan!.SelectedTest! with { ManifestHash = PlatformCurrentOwnerProof.DeclarationHash.ToUpperInvariant() };
        CompositionRunPlan plan = options.RunPlan with { PlatformDeclarationHash = selected.ManifestHash, SelectedTest = selected, SelectedTests = [selected] };
        await CompositionDocumentStore.WriteAsync(Path.Combine(plan.Workspace, "plan.json"), plan, CancellationToken.None).ConfigureAwait(true);
        options = options with { RunPlan = plan };
        PlatformPartiesRunProof.Create(options, fixture.Key).Matches(options).ShouldBeTrue();
    }

    /// <summary>The compatibility set remains finite; changing only source bytes cannot acquire it.</summary>
    [Fact]
    public void OnlyTheTwoMeasuredDeclarationsAreCompatible()
    {
        PlatformPartiesPackageCatalogQualification.IsOrdinaryDeclaration(PlatformPartiesPackageCatalogQualification.ActiveDeclarationHash).ShouldBeTrue();
        PlatformPartiesPackageCatalogQualification.IsOrdinaryDeclaration(PlatformCurrentOwnerProof.DeclarationHash).ShouldBeTrue();
        PlatformPartiesPackageCatalogQualification.IsOrdinaryDeclaration(new string('0', 64)).ShouldBeFalse();
        PlatformPartiesPackageCatalogQualification.IsOrdinaryDeclaration(null).ShouldBeFalse();
        const string LegacyAppHost = "23fbcd1b7e495a85920a44b1d7fd690cee69548dadd9ae02768d8d29b355577b";
        const string LegacyTooling = "da4c0db5b9746609629f8ba20aadb06c5c792521149ce985b1fa56c8f4efb000";
        const string CurrentAppHost = "e85148b3809600a8f6e81c1163618c0250b179f8f9e5435a46c2ad5fe119dc10";
        const string CurrentTooling = "57eff14f810059878e1c43eafe2b1ee04382ea01cb8fd860cfc6810e9d2505a2";
        PlatformPartiesValidationContext.IsPublishedPair(LegacyAppHost, LegacyTooling).ShouldBeTrue();
        PlatformPartiesValidationContext.IsPublishedPair(CurrentAppHost, CurrentTooling).ShouldBeTrue();
        const string NewAppHost = "acac3d8ee71977f2f70fc5099510d69be695e2ff466f78e06ee9086d86c45066";
        const string NewTooling = "445d9219c0670abff9550fa3c028c3ee00ffc4d9b3a4c07e5e14b078f035cbb4";
        PlatformPartiesValidationContext.IsPublishedPair(NewAppHost, NewTooling).ShouldBeTrue();
        foreach (var previous in new[] { (LegacyAppHost, LegacyTooling), (CurrentAppHost, CurrentTooling) })
        {
            PlatformPartiesValidationContext.IsPublishedPair(previous.Item1, NewTooling).ShouldBeFalse();
            PlatformPartiesValidationContext.IsPublishedPair(NewAppHost, previous.Item2).ShouldBeFalse();
        }
        const string NextAppHost = "2b746f7b8a91e70fe1e2a571dc4ca832c4cb18ca502b60a467347da32a4fe84c";
        const string NextTooling = "23ef9fa3ec8a6c85e91a08da434e0fd1990ff1f613e8f36e0951144b09a37b87";
        PlatformPartiesValidationContext.IsPublishedPair(NextAppHost, NextTooling).ShouldBeTrue();
        foreach (var previous in new[] { (LegacyAppHost, LegacyTooling), (CurrentAppHost, CurrentTooling), (NewAppHost, NewTooling) })
        {
            PlatformPartiesValidationContext.IsPublishedPair(previous.Item1, NextTooling).ShouldBeFalse();
            PlatformPartiesValidationContext.IsPublishedPair(NextAppHost, previous.Item2).ShouldBeFalse();
        }
        const string CanonicalClaimsAppHost = "f353c835fd365703c1e6cff7ee632e9f3e98f53e7acf8033e927772383bef911";
        const string CanonicalClaimsTooling = "6639eb04d7fc99960b0bcd5ed6c978a7213e86f7c9d2b9963d3d3fe1ecf20c90";
        PlatformPartiesValidationContext.IsPublishedPair(CanonicalClaimsAppHost, CanonicalClaimsTooling).ShouldBeTrue();
        foreach (var previous in new[] { (LegacyAppHost, LegacyTooling), (CurrentAppHost, CurrentTooling), (NewAppHost, NewTooling), (NextAppHost, NextTooling) })
        {
            PlatformPartiesValidationContext.IsPublishedPair(previous.Item1, CanonicalClaimsTooling).ShouldBeFalse();
            PlatformPartiesValidationContext.IsPublishedPair(CanonicalClaimsAppHost, previous.Item2).ShouldBeFalse();
        }
        const string SafeStatusAppHost = "ad2ed0e8a9a8e6b86ad21718d8856ced45ea3ce819797cd80cb2a11e09f04764";
        const string SafeStatusTooling = "f37dc97c407b513dfecf99818606c954508c05dcece333d788aa5bc7ff3545e9";
        const string CurrentProducerAppHost = "672f01e4165cd00ea2f6866dd5a2681afe4dc30764e8f31a6f396f03f9dbfef0";
        const string CurrentProducerTooling = "3ce7ca60e805b03efd10f66a31734fc32f8c6568d0d20d9657b6a8af3a0c908a";
        const string LatestProducerAppHost = "0795a6f45a68a78856670327f85549ef4173652780f31533d3342e2ea07529cf";
        const string LatestProducerTooling = "c43e11474ce0d269e1492d0924d826538b3cb871869669ac23744a5f2f687632";
        const string Builds44411AppHost = "f4a9cb4c9cd81579ccdcec7e40d4254219d5afeb7f7c28dcc5d566164bd0df2d";
        const string Builds44411Tooling = "c1d319f7c5a5f33219bbe8437f95ff169318ef5265c5827591c547623171c8db";
        const string Builds44412AppHost = "ac1b7477337c3921a77e1ca527cf3d76c7feeb2f5c8019bd14c1da9222581d8e";
        const string Builds44412Tooling = "1bd228fc084776f48ad9faff8ac0c614e0c6dc0c2f03f309d325e0e90cbc1870";
        const string Builds44413AppHost = "5d3ed600a11c921ad1ca8d98e90430c000681ab2b9d41dfb7d5df3a0dae6f252";
        const string Builds44413Tooling = "39583273ccfbf9fac6b921e1996c109cc66b0d5336ff59fc7ac70923edcf0d2d";
        const string Builds4450AppHost = "0afbfd9bfe10a6d28cce6b09e375e1cfbbd581bcc6d501157eccacd3e3cc2954";
        const string Builds4450Tooling = "0c8c00e5563730f953f24b19b777dedd1887f4d186e92347145e55783d4d4981";
        const string Builds4470AppHost = "6aae774cfcf41e0488cf7b2517d67897f1726c68674219fea164d6998ad60ebe";
        const string Builds4470Tooling = "35d0eb2b0f32ad5e3b1e4353f088800e76084b99e60f46838e6e497105f64f01";
        const string Builds4481AppHost = "adeffaefcbf7207711d7a8290d975edc9a6ffee446d0ad40194ad96b5299df70";
        const string Builds4481Tooling = "592785b5fc3426321fec66538975988a74573df50f11f42bfd82268869bf23ab";
        const string Builds4482AppHost = "f8f7b6e743a798e37784f5e3d44f62bc81c5cef112c8fa9ec643fcb35da825ba";
        const string Builds4482Tooling = "142bd3fdd26ff4e5ba8eadd91d00de92b738b019270fa6742e3a35f41460b1cf";
        const string Builds4483AppHost = "7ccaa52a5fb6144bb570fddc1d98514822c9ef22ceccbdc2c3ae840afa701340";
        const string Builds4483Tooling = "e2248aa6b6978385a369f04668f167360775db72cb3bd773903af51266d77a6f";
        const string Builds500AppHost = "c4bf26c96e72203c1143b92830dc8feeadc8cb88380dd16545a21abb97bbc63b";
        const string Builds500Tooling = "6ceb2bcd28d7b99927b5b6e84b6e4e7ea4b5c485337003bcfdb04b752d9e7261";
        var publishedPairs = new[] { (LegacyAppHost, LegacyTooling), (CurrentAppHost, CurrentTooling),
            (NewAppHost, NewTooling), (NextAppHost, NextTooling), (CanonicalClaimsAppHost, CanonicalClaimsTooling),
            (SafeStatusAppHost, SafeStatusTooling), (CurrentProducerAppHost, CurrentProducerTooling), (LatestProducerAppHost, LatestProducerTooling), (Builds44411AppHost, Builds44411Tooling), (Builds44412AppHost, Builds44412Tooling), (Builds44413AppHost, Builds44413Tooling), (Builds4450AppHost, Builds4450Tooling), (Builds4470AppHost, Builds4470Tooling), (Builds4481AppHost, Builds4481Tooling), (Builds4482AppHost, Builds4482Tooling), (Builds4483AppHost, Builds4483Tooling), (Builds500AppHost, Builds500Tooling) };
        foreach (var appHostPair in publishedPairs)
            foreach (var toolingPair in publishedPairs)
                PlatformPartiesValidationContext.IsPublishedPair(appHostPair.Item1, toolingPair.Item2)
                    .ShouldBe(appHostPair == toolingPair);
        PlatformPartiesValidationContext.IsPublishedPair(LegacyAppHost, CurrentTooling).ShouldBeFalse();
        PlatformPartiesValidationContext.IsPublishedPair(CurrentAppHost, LegacyTooling).ShouldBeFalse();
        PlatformPartiesValidationContext.IsPublishedPair(CurrentAppHost, new string('0', 64)).ShouldBeFalse();
        PlatformPartiesValidationContext.IsPublishedPair(null, CurrentTooling).ShouldBeFalse();
        using PlatformPartiesProofFixture fixture = new();
        PlatformPartiesValidationContext.IsCurrentPublishedValidation(fixture.Options.RunPlan!).ShouldBeFalse();
        PlatformCompositionOptions options = Updated(fixture, "platform-current-package");
        Should.Throw<InvalidDataException>(() => PlatformPartiesRunProof.Create(options, fixture.Key));
    }

    private static PlatformCompositionOptions Updated(PlatformPartiesProofFixture fixture, string profile)
    {
        PlatformCompositionOptions options = fixture.Options;
        string declaration = options.DeclarationPaths.Single();
        using Stream original = typeof(PlatformOrdinaryDeclarationCompatibilityTests).Assembly.GetManifestResourceStream(
            "Hexalith.Platform.Composition.Tests.Fixtures.parties-current-declaration.json")!;
        using (FileStream target = File.Create(declaration)) original.CopyTo(target);
        string hash = Hash(declaration);
        hash.ShouldBe(PlatformCurrentOwnerProof.DeclarationHash);
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(declaration));
        var suite = manifest.RootElement.GetProperty("modules")[0].GetProperty("tests").EnumerateArray().Single(test => test.GetProperty("name").GetString() == profile);
        string entry = suite.GetProperty("entry").GetString()!;
        string project = Path.Combine(options.Mapping.Root, entry);
        Directory.CreateDirectory(Path.GetDirectoryName(project)!);
        File.WriteAllText(project, "<Project />");
        PlatformPartiesProofFixture.Commit(options.Mapping.Root);
        ModuleTestSelection selected = options.RunPlan!.SelectedTest! with { Name = profile, Entry = entry, EntryHash = Hash(project), ManifestHash = hash };
        CompositionRunPlan plan = options.RunPlan with { PlatformDeclarationHash = hash, SelectedTest = selected, SelectedTests = [selected] };
        CompositionDocumentStore.WriteAsync(Path.Combine(plan.Workspace, "plan.json"), plan, CancellationToken.None).GetAwaiter().GetResult();
        return options with { RunPlan = plan, PartiesRunProof = null };
    }

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
