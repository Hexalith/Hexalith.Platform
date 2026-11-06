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
        var publishedPairs = new[] { (LegacyAppHost, LegacyTooling), (CurrentAppHost, CurrentTooling),
            (NewAppHost, NewTooling), (NextAppHost, NextTooling), (CanonicalClaimsAppHost, CanonicalClaimsTooling),
            (SafeStatusAppHost, SafeStatusTooling), (CurrentProducerAppHost, CurrentProducerTooling), (LatestProducerAppHost, LatestProducerTooling), (Builds44411AppHost, Builds44411Tooling) };
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
