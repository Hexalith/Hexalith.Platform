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
        using PlatformPartiesProofFixture fixture = new();
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
