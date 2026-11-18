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
    }

    /// <summary>The actual published producer uses uppercase hex without changing the measured declaration bytes.</summary>
    [Fact]
    public async Task ProducerUppercaseHashesRetainTheSameFiniteDeclaration()
    {
        using PlatformPartiesProofFixture fixture = new();
        PlatformCompositionOptions options = Updated(fixture, "platform-package");
        ModuleTestSelection selected = options.RunPlan!.SelectedTest! with { ManifestHash = options.RunPlan.PlatformDeclarationHash!.ToUpperInvariant() };
        CompositionRunPlan plan = options.RunPlan with { PlatformDeclarationHash = selected.ManifestHash, SelectedTest = selected, SelectedTests = [selected] };
        await CompositionDocumentStore.WriteAsync(Path.Combine(plan.Workspace, "plan.json"), plan, CancellationToken.None).ConfigureAwait(true);
        options = options with { RunPlan = plan };
        PlatformPartiesRunProof.Create(options, fixture.Key).Matches(options).ShouldBeTrue();
    }

    /// <summary>The compatibility set remains finite; changing only source bytes cannot acquire it.</summary>
    [Fact]
    public void UncommittedDeclarationCannotAcquireParentProof()
    {
        using PlatformPartiesProofFixture fixture = new();
        PlatformPartiesValidationContext.IsCurrentPublishedValidation(fixture.Options.RunPlan!, PlatformTestPins.ToolVersionRange).ShouldBeFalse();
        PlatformCompositionOptions options = Updated(fixture, "platform-package");
        File.AppendAllText(options.DeclarationPaths.Single(), " ");
        Should.Throw<InvalidDataException>(() => PlatformPartiesRunProof.Create(options, fixture.Key));
    }

    /// <summary>The AppHost/Tooling pair is admitted by its shared released version inside <c>[pin, next major)</c>, never by a hash list.</summary>
    [Fact]
    public void BuildsToolPairFollowsThePinnedVersionRule()
    {
        // Rule-relative examples against an explicit range; the current pins are read from the pin files below.
        const string Range = "[6.0.5, 7.0.0-0)";
        const string Commit = "+0123456789abcdef0123456789abcdef01234567";
        foreach (string admitted in new[] { "6.0.5", "6.1.0", "6.1.0-rc.1" })
            PlatformPartiesValidationContext.IsPublishedPair(admitted + Commit, admitted + Commit, Range).ShouldBeTrue(admitted);
        // Prereleases follow NuGet ordering: below the floor and the next major's prereleases refuse.
        foreach (string refused in new[] { "6.0.3", "5.2.2", "7.0.0", "6.0.5-rc.1", "7.0.0-alpha" })
        {
            PlatformPartiesValidationContext.IsPublishedPair(refused + Commit, refused + Commit, Range).ShouldBeFalse(refused);
            PlatformPartiesValidationContext.PairRefusal(refused + Commit, refused + Commit, Range).ShouldBe($"The Builds tool version {refused}{Commit} is outside {Range}.");
        }
        PlatformPartiesValidationContext.PairRefusal("6.0.5" + Commit, "6.1.0" + Commit, Range).ShouldBe($"The Builds AppHost 6.0.5{Commit} and Tooling 6.1.0{Commit} are a split pair.");
        PlatformPartiesValidationContext.PairRefusal("not-a-version", "not-a-version", Range).ShouldBe("The Builds tool version 'not-a-version' is unparseable.");
        // A split pair, a missing version and unparseable versions refuse.
        PlatformPartiesValidationContext.IsPublishedPair("6.0.5" + Commit, "6.1.0" + Commit, Range).ShouldBeFalse();
        PlatformPartiesValidationContext.IsPublishedPair("6.1.0" + Commit, "6.1.0+fedcba9876543210fedcba9876543210fedcba98", Range).ShouldBeFalse();
        foreach (string? missing in new string?[] { null, "", " " })
        {
            PlatformPartiesValidationContext.IsPublishedPair(missing, "6.1.0" + Commit, Range).ShouldBeFalse();
            PlatformPartiesValidationContext.IsPublishedPair("6.1.0" + Commit, missing, Range).ShouldBeFalse();
            PlatformPartiesValidationContext.IsPublishedPair(missing, missing, Range).ShouldBeFalse();
        }
        foreach (string unparseable in new[] { "6.1", "6.1.0.0", "v6.1.0", "6.1.0+", "06.1.0", "not-a-version" })
            PlatformPartiesValidationContext.IsPublishedPair(unparseable, unparseable, Range).ShouldBeFalse(unparseable);
        // Only the exact [pin, next major) range shape admits a pair.
        foreach (string? range in new string?[] { null, "", "invalid", "6.*", "6.0.5", "[6.0.5, 7.0.0)", "[6.0.5, 7.0.0-0]",
            "(6.0.5, 7.0.0-0)", "[6.0.5, 8.0.0-0)", "[6.0.5-preview, 7.0.0-0)", "[6.0.5+metadata, 7.0.0-0)", "[6.0.5, )" })
            PlatformPartiesValidationContext.IsPublishedPair("6.1.0" + Commit, "6.1.0" + Commit, range).ShouldBeFalse(range ?? "null");

        // The current pins come from the pin files; the published DLLs embed <version>+<gitlink>.
        string pinned = PlatformTestPins.ToolVersion;
        PlatformTestPins.BindingVersion.ShouldBe(pinned);
        PlatformTestPins.ToolVersionRange.ShouldBe("[" + pinned + ", " + (int.Parse(pinned.Split('.')[0], System.Globalization.CultureInfo.InvariantCulture) + 1) + ".0.0-0)");
        string current = pinned + "+" + PlatformTestPins.BuildsGitlink;
        PlatformPartiesValidationContext.IsPublishedPair(current, current, PlatformTestPins.ToolVersionRange).ShouldBeTrue();
        string[] parts = pinned.Split('.');
        string older = parts[2] != "0" ? parts[0] + "." + parts[1] + "." + (int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture) - 1)
            : parts[1] != "0" ? parts[0] + "." + (int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) - 1) + ".99"
            : (int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) - 1) + ".99.99";
        string newer = parts[0] + "." + parts[1] + "." + (int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture) + 1);
        string nextMajor = (int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) + 1) + ".0.0";
        PlatformPartiesValidationContext.IsPublishedPair(newer + Commit, newer + Commit, PlatformTestPins.ToolVersionRange).ShouldBeTrue();
        PlatformPartiesValidationContext.IsPublishedPair(older + Commit, older + Commit, PlatformTestPins.ToolVersionRange).ShouldBeFalse();
        PlatformPartiesValidationContext.IsPublishedPair(nextMajor + Commit, nextMajor + Commit, PlatformTestPins.ToolVersionRange).ShouldBeFalse();
        PlatformPartiesValidationContext.IsPublishedPair(current, newer + Commit, PlatformTestPins.ToolVersionRange).ShouldBeFalse();
    }

    private static PlatformCompositionOptions Updated(PlatformPartiesProofFixture fixture, string profile)
    {
        PlatformCompositionOptions options = fixture.Options;
        string declaration = options.DeclarationPaths.Single();
        using Stream original = typeof(PlatformOrdinaryDeclarationCompatibilityTests).Assembly.GetManifestResourceStream(
            "Hexalith.Platform.Composition.Tests.Fixtures.parties-current-declaration.json")!;
        using (FileStream target = File.Create(declaration)) original.CopyTo(target);
        string hash = Hash(declaration);
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
