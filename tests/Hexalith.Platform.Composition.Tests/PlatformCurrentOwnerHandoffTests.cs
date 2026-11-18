namespace Hexalith.Platform.Composition.Tests;

using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

/// <summary>Executes the signed entry/Tooling transport with its bounded test-only security overlay and a fresh reader before application resources start.</summary>
public sealed class PlatformCurrentOwnerHandoffTests
{
    /// <summary>The genuine pinned pair is lock-verified and admitted by its version; crossed, split or out-of-range versions refuse.</summary>
    [Fact]
    public void CurrentSignedHostAndToolingPairCannotBeCrossed()
    {
        string tool = typeof(PlatformCurrentOwnerHandoffTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "PlatformTestPublishedToolRoot").Value!;
        string host = Path.Combine(tool, "g4-host", "Hexalith.Builds.Module.AppHost.dll");
        string tooling = Path.Combine(tool, "g4-host", "Hexalith.Builds.Tooling.dll");
        // AR-12 expected identities come from the lock-verified archive, never from literals.
        Hash(host).ShouldBe(PlatformTestPins.LockedArchiveMemberHash(tool, "g4-host/Hexalith.Builds.Module.AppHost.dll"));
        Hash(tooling).ShouldBe(PlatformTestPins.LockedArchiveMemberHash(tool, "g4-host/Hexalith.Builds.Tooling.dll"));
        string? hostVersion = FileVersionInfo.GetVersionInfo(host).ProductVersion;
        string? toolingVersion = FileVersionInfo.GetVersionInfo(tooling).ProductVersion;
        hostVersion.ShouldBe(PlatformTestPins.ToolVersion + "+" + PlatformTestPins.BuildsGitlink);
        toolingVersion.ShouldBe(hostVersion);
        string range = PlatformTestPins.ToolVersionRange;
        PlatformPartiesValidationContext.IsPublishedPair(hostVersion, toolingVersion, range).ShouldBeTrue();
        string foreign = PlatformTestPins.ToolVersion + "+" + new string('0', 40);
        PlatformPartiesValidationContext.IsPublishedPair(hostVersion, foreign, range).ShouldBeFalse();
        PlatformPartiesValidationContext.IsPublishedPair(foreign, toolingVersion, range).ShouldBeFalse();
        PlatformPartiesValidationContext.IsPublishedPair(hostVersion, null, range).ShouldBeFalse();
        // A range whose floor is the next major generation refuses the current pair.
        int major = int.Parse(PlatformTestPins.ToolVersion.Split('.')[0], System.Globalization.CultureInfo.InvariantCulture);
        PlatformPartiesValidationContext.IsPublishedPair(hostVersion, toolingVersion, $"[{major + 1}.0.0, {major + 2}.0.0-0)").ShouldBeFalse();
    }

    /// <summary>Preflight can enrich only derived consumer cache; a fresh ordinary proof and reader preserve the same signed command inputs.</summary>
    [Fact]
    public async Task PublishedCurrentValidationThenFreshOrdinaryGeneratePreservesMeasuredAuthorityAsync()
    {
        await AssertPublishedCurrentValidationThenFreshOrdinaryGenerateAsync();
    }

    /// <summary>Wrong key encoding and mixed/stale command, entry-body or source bindings refuse through the actual public host.</summary>
    /// <param name="fault">The single persisted command or transport mutation.</param>
    [Theory]
    [InlineData("wrong-key-encoding")]
    [InlineData("mixed-plan")]
    [InlineData("stale-run")]
    [InlineData("entry-body")]
    [InlineData("source-binding")]
    public async Task PublishedCurrentValidationRefusesChangedAuthorityBeforeEffectsAsync(string fault)
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(PlatformCurrentOwnerProof.Profile);
        await fixture.PersistClaimStateAsync();
        CompositionRunPlan changed = fixture.Plan;
        if (fault == "mixed-plan") changed = changed with { SelectedTests = [changed.SelectedTest!, changed.SelectedTest! with { Name = "platform-package" }] };
        if (fault == "stale-run") changed = changed with { RunId = CompositionRunPlanFactory.NewRunId() };
        if (fault == "entry-body") changed = changed with { SelectedTest = changed.SelectedTest! with { EntryHash = new string('a', 64) } };
        if (fault == "entry-body") changed = changed with { SelectedTests = [changed.SelectedTest!] };
        if (fault == "source-binding") changed = changed with { SourceMapping = changed.SourceMapping! with
        { PlatformIdentity = changed.SourceMapping!.PlatformIdentity! with { CheckoutCommit = new string('a', 40) } } };
        await CompositionDocumentStore.WriteAsync(Path.Combine(fixture.Workspace, "plan.json"), changed, TestContext.Current.CancellationToken);
        var result = await fixture.RunPublishedValidationAsync(completeCatalog: true, signingKey: fault == "wrong-key-encoding" ? "not-a-key" : null);
        result.ExitCode.ShouldNotBe(0);
        Directory.Exists(Path.Combine(fixture.Workspace, "current-owner")).ShouldBeFalse();
        Directory.Exists(Path.Combine(fixture.Workspace, "platform-components")).ShouldBeFalse();
        Directory.Exists(fixture.Plan.ResourceClaimStateDirectory!).ShouldBeFalse();
        File.Exists(Path.Combine(fixture.Workspace, "validation-probes", "images.txt")).ShouldBeFalse();
        File.Exists(Path.Combine(fixture.Workspace, NativeTestHandoff.EndpointCredentialsFileName)).ShouldBeFalse();
    }

    /// <summary>An ordinary plan has no prior key anchor; a fresh well-formed initial key retains the complete measured handoff.</summary>
    [Fact]
    public async Task PublishedCurrentValidationAcceptsFreshWellFormedInitialKeyWithoutPriorAnchorAsync()
    {
        string initialSigningKey = CompositionSigningKey.Create();
        Convert.FromBase64String(initialSigningKey).Length.ShouldBe(48);
        await AssertPublishedCurrentValidationThenFreshOrdinaryGenerateAsync(initialSigningKey);
    }

    private static async Task AssertPublishedCurrentValidationThenFreshOrdinaryGenerateAsync(string? initialSigningKey = null)
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(PlatformCurrentOwnerProof.Profile);
        DateTimeOffset? declaredStartupDeadline = fixture.Plan.StartupDeadlineUtc;
        declaredStartupDeadline.ShouldNotBeNull();
        if (initialSigningKey is not null) fixture.UsesSigningKey(initialSigningKey).ShouldBeFalse();
        await fixture.PersistClaimStateAsync();
        await CompositionDaprComponentRenderer.WriteAsync(fixture.Plan, TestContext.Current.CancellationToken);
        string mapping = Path.Combine(fixture.Workspace, "source-mapping");
        string snapshot = Path.Combine(mapping, "catalog-versions.json");
        string[] authority = [Path.Combine(fixture.Workspace, "plan.json"), .. Directory.GetFiles(mapping, "*", SearchOption.AllDirectories)];
        var original = authority.ToDictionary(path => path, Hash, StringComparer.Ordinal);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        var validation = await fixture.RunPublishedValidationAsync(completeCatalog: true, signingKey: initialSigningKey);
        validation.ExitCode.ShouldBe(0, validation.ErrorOutput);
        validation.ErrorOutput.ShouldNotContain("requires its exact qualified full owner/public/image cohort");
        validation.ErrorOutput.ShouldNotContain("trusted parent plan/key handoff");
        byte[] validationSnapshot = File.ReadAllBytes(snapshot);
        await PlatformPartiesCatalogHandoffTests.AssertDerivedConsumerEnrichmentAsync(originalSnapshot, validationSnapshot, snapshot, fixture.Seed);
        fixture.AssertValidationRuntimeIntegrity();
        Directory.Exists(Path.Combine(fixture.Workspace, "current-owner")).ShouldBeTrue(
            "The genuine current host and Tooling consumed the authenticated parent plan/key and prepared the measured owner.");
        File.ReadAllLines(Path.Combine(fixture.Workspace, "validation-probes", "images.txt"))
            .ToHashSet(StringComparer.Ordinal).SetEquals(PlatformPartiesPackageFixture.CurrentImages).ShouldBeTrue();
        Directory.Exists(fixture.Plan.ResourceClaimStateDirectory!).ShouldBeFalse();
        foreach (var input in original.Where(input => input.Key != snapshot)) Hash(input.Key).ShouldBe(input.Value);
        fixture.Plan.StartupDeadlineUtc.ShouldBe(declaredStartupDeadline);
        var builder = PlatformPartiesPackageFixture.Builder();
        Should.Throw<InvalidOperationException>(() => fixture.Compose(builder, options =>
        {
            PlatformCurrentOwnerProof proof = options.CurrentOwnerProof!;
            proof.Matches(options).ShouldBeTrue();
            options.PartiesRunProof.ShouldBeNull();
            options.RunPlan!.ResourceClaimStateDirectory.ShouldBe(fixture.Plan.ResourceClaimStateDirectory);
            PlatformAppModel.MaterializeSourceMapping(options, fixture.Workspace);
            var catalog = PlatformRoutingCatalog.Generate(options, PlatformAppModel.Prepare(options), Path.Combine(fixture.Workspace, "platform-components"));
            using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.Directory, "current-reader-receipt.json")));
            proof.VerifyReader(receipt.RootElement);
            TestContext.Current.TestOutputHelper!.WriteLine(JsonSerializer.Serialize(new
            {
                phase = "signed-entry-current-validation-and-fresh-ordinary-reader", proof.SourceRevision,
                initialKeySource = initialSigningKey is null ? "original-fixture-key" : "fresh-independent-key",
                priorPlanKeyAnchor = false,
                qualificationCredit = false, runtimeBodies = proof.Bodies, packageArchives = proof.PackageArchives,
                loadedArtifacts = receipt.RootElement.GetProperty("loadedArtifacts"),
            }));
            proof.Matches(options).ShouldBeTrue();
            foreach (var input in original.Where(input => input.Key != snapshot)) Hash(input.Key).ShouldBe(input.Value);
            throw new InvalidOperationException("Expected real signed current validation and ordinary catalog; stop before resources.");
        }, signingKey: initialSigningKey)).Message.ShouldBe("Expected real signed current validation and ordinary catalog; stop before resources.");
        await PlatformPartiesCatalogHandoffTests.AssertDerivedConsumerEnrichmentAsync(validationSnapshot, File.ReadAllBytes(snapshot), snapshot, fixture.Seed);
        fixture.Plan.StartupDeadlineUtc.ShouldBe(declaredStartupDeadline);
        builder.Resources.ShouldBeEmpty();
        Directory.Exists(fixture.Plan.ResourceClaimStateDirectory!).ShouldBeFalse();
        File.Exists(Path.Combine(fixture.Workspace, NativeTestHandoff.EndpointCredentialsFileName)).ShouldBeFalse();
    }

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
