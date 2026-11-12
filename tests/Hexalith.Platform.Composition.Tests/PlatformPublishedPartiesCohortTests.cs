namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using Hexalith.Platform.CatalogArtifacts;
using Shouldly;

/// <summary>Authenticates the finite public3.1 reader without granting private-purpose, image-body or native acceptance.</summary>
public sealed class PlatformPublishedPartiesCohortTests
{
    private static readonly string[] _names = ["Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults", "Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService",
        "Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server", "Hexalith.Memories.Contracts", "Hexalith.Memories.EventStore", "Hexalith.Memories.EventStoreExtensions",
        "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions", "Hexalith.McpCli.Abstractions"];

    /// <summary>All twelve exact signed public bodies authenticate, while private reference authority still excludes Parties.</summary>
    [Fact]
    public void GenuinePublicReaderBodiesHaveTheirOwnCompleteAuthority()
    {
        AssemblyLoadContext context = new("published-parties-bodies-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            foreach (string name in _names)
            {
                string path = Body(name);
                Assembly assembly = context.LoadFromAssemblyPath(path);
                PublishedCatalogArtifactCohort.Verify(assembly, PublishedCatalogArtifactCohort.PublishedPartiesName).ShouldBe(path);
                if (name.StartsWith("Hexalith.Parties.", StringComparison.Ordinal))
                {
                    Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(assembly, PublishedCatalogArtifactCohort.CurrentOwnerName));
                    Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(assembly, PublishedCatalogArtifactCohort.CurrentFullPartiesName));
                }
            }
        }
        finally { context.Unload(); }
        PublishedCatalogArtifactCohort.ForVersions("4.1.0", "7.0.5").ShouldBeNull();
        PublishedCatalogArtifactCohort.ForVersions("4.1.0", "7.0.5", fullParties: true).ShouldBeNull();
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyFullParties(PublishedCatalogArtifactCohort.CurrentOwnerName));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyFullParties(PublishedCatalogArtifactCohort.EventStorePackageTestName));
    }

    /// <summary>Every selected body is checked before reflection; changing even appended bytes cannot retain public authority.</summary>
    /// <param name="name">The actual public assembly to copy and alter privately.</param>
    [Theory]
    [InlineData("Hexalith.EventStore.Contracts")]
    [InlineData("Hexalith.Tenants.Contracts")]
    [InlineData("Hexalith.Memories.Contracts")]
    [InlineData("Hexalith.Parties.Contracts")]
    [InlineData("Hexalith.Parties.EventStoreExtensions")]
    [InlineData("Hexalith.McpCli.Abstractions")]
    public void AlteredPublicReaderBodyRefusesBeforeOwnerReflection(string name)
    {
        string source = Body(name);
        byte[] original = File.ReadAllBytes(source);
        string directory = Path.Combine(Path.GetTempPath(), "published-parties-tamper-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        AssemblyLoadContext context = new("published-parties-tamper-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            string copy = Path.Combine(directory, name + ".dll");
            File.WriteAllBytes(copy, [.. original, 0]);
            Assembly changed = context.LoadFromAssemblyPath(copy);
            Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(changed, PublishedCatalogArtifactCohort.PublishedPartiesName));
        }
        finally { context.Unload(); Directory.Delete(directory, recursive: true); }
        File.ReadAllBytes(source).ShouldBe(original);
    }

    /// <summary>Authentic historical packages remain authentic under their own authority and cannot be mixed into the new reader.</summary>
    /// <param name="name">The actual old public owner assembly.</param><param name="version">Its measured historical version.</param>
    /// <param name="cohort">Its retained historical authority.</param>
    [Theory]
    [InlineData("Hexalith.Tenants.Contracts", "7.0.2", PublishedCatalogArtifactCohort.CurrentFullPartiesName)]
    [InlineData("Hexalith.Tenants.Contracts", "7.0.4", PublishedCatalogArtifactCohort.Tenants704CurrentOwnerName)]
    [InlineData("Hexalith.Memories.Contracts", "3.0.23", PublishedCatalogArtifactCohort.CurrentFullPartiesName)]
    [InlineData("Hexalith.Memories.Contracts", "3.0.24", PublishedCatalogArtifactCohort.PreviousCurrentOwnerName)]
    [InlineData("Hexalith.Parties.Contracts", "3.0.0", PublishedCatalogArtifactCohort.CurrentFullPartiesName)]
    [InlineData("Hexalith.Parties.EventStoreExtensions", "3.0.0", PublishedCatalogArtifactCohort.CurrentFullPartiesName)]
    [InlineData("Hexalith.McpCli.Abstractions", "2.0.0", PublishedCatalogArtifactCohort.CurrentFullPartiesName)]
    public void GenuineHistoricalBodiesCannotBeMixedIntoPublicParties(string name, string version, string cohort)
    {
        string path = Body(name, version);
        AssemblyLoadContext context = new("published-parties-mixed-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            Assembly assembly = context.LoadFromAssemblyPath(path);
            PublishedCatalogArtifactCohort.Verify(assembly, cohort).ShouldBe(path);
            Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(assembly, PublishedCatalogArtifactCohort.PublishedPartiesName));
        }
        finally { context.Unload(); }
    }

    /// <summary>The complete public version selection is finite; partial, future, private and near-match tuples remain unavailable.</summary>
    /// <param name="sdk">The SDK version.</param><param name="tenants">The tenant version.</param>
    /// <param name="memories">The memories version.</param><param name="parties">The Parties version.</param>
    /// <param name="accepted">Whether this is the sole measured public tuple.</param>
    [Theory]
    [InlineData("4.1.0", "7.0.5", "3.0.25", "3.1.0", true)]
    [InlineData("4.0.4", "7.0.5", "3.0.25", "3.1.0", false)]
    [InlineData("4.1.1", "7.0.5", "3.0.25", "3.1.0", false)]
    [InlineData("4.1.0", "7.0.4", "3.0.25", "3.1.0", false)]
    [InlineData("4.1.0", "7.0.2", "3.0.25", "3.1.0", false)]
    [InlineData("4.1.0", "7.0.5", "3.0.23", "3.1.0", false)]
    [InlineData("4.1.0", "7.0.5", "3.0.24", "3.1.0", false)]
    [InlineData("4.1.0", "7.0.5", "3.0.26", "3.1.0", false)]
    [InlineData("4.1.0", "7.0.5", "3.0.25", "3.0.0", false)]
    [InlineData("4.1.0", "7.0.5", "3.0.25", "3.1.1", false)]
    [InlineData("4.1.0", "7.0.5", "3.0.25", "3.1.0+metadata", false)]
    [InlineData("4.1.0", "7.0.5.0", "3.0.25", "3.1.0", false)]
    [InlineData("unknown", "unknown", "unknown", "unknown", false)]
    public void OnlyTheCompleteMeasuredVersionsSelectPublicAuthority(string sdk, string tenants, string memories, string parties, bool accepted)
        => PublishedCatalogArtifactCohort.ForPublishedPartiesVersions(sdk, tenants, memories, parties, "2.0.1")
            .ShouldBe(accepted ? PublishedCatalogArtifactCohort.PublishedPartiesName : null);

    /// <summary>The measured NuGet Contracts body is distinct from the measured OCI owner body; neither label grants cross-role equality.</summary>
    [Fact]
    public void PublicNuGetContractsRetainTheirMeasuredPackageRole()
    {
        string path = Body("Hexalith.Parties.Contracts");
        string digest = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        digest.ShouldBe("99230b764de6769a803702170c174f9dbef9b0b5b0c2931b07e85045478cd654");
        digest.ShouldNotBe("b2ddfb18a7719b171ab9cf8b846675c4556d4d683ba0b5ebe8a503a6ae73d2f6");
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(typeof(PlatformPublishedPartiesCohortTests).Assembly, PublishedCatalogArtifactCohort.PublishedPartiesName));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(Assembly.Load("Hexalith.EventStore.Contracts"), "unknown"));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyFullParties("unknown"));
        PlatformPartiesPackageCatalogQualification.PublishedDeclarationHash.ShouldBe("d9c193b0e7b61601b8fcceb906b5dafe4769a1b9d7538a6ce164b4aa0840b348");
    }

    /// <summary>The actual published OCI owner Contracts body is valid for its measured image role and cannot become a public package reader.</summary>
    [Fact]
    public void ActualPublishedImageOwnerBodyCannotAcquirePackageReaderAuthority()
    {
        string directory = Path.Combine(Path.GetTempPath(), "published-parties-image-role-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        AssemblyLoadContext context = new("published-parties-image-role-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            string path = Path.Combine(directory, "Hexalith.Parties.Contracts.dll");
            using (Stream source = typeof(PlatformPublishedPartiesCohortTests).Assembly.GetManifestResourceStream("Hexalith.Platform.Composition.Tests.Fixtures.public-parties310-image-contracts.dll")!)
            using (FileStream target = File.Create(path)) source.CopyTo(target);
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))).ShouldBe("b2ddfb18a7719b171ab9cf8b846675c4556d4d683ba0b5ebe8a503a6ae73d2f6");
            Assembly image = context.LoadFromAssemblyPath(path);
            image.GetName().Name.ShouldBe("Hexalith.Parties.Contracts");
            foreach (string cohort in new[] { PublishedCatalogArtifactCohort.PublishedPartiesName, PublishedCatalogArtifactCohort.CurrentFullPartiesName, PublishedCatalogArtifactCohort.CurrentOwnerName })
                Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(image, cohort));
        }
        finally { context.Unload(); Directory.Delete(directory, recursive: true); }
    }

    /// <summary>The released declaration retains the complete ordinary parent/source proof and grants no private current profile.</summary>
    [Fact]
    public void ReleasedDeclarationPreservesTheOrdinarySourceAndPurposeBoundary()
    {
        using PlatformPartiesProofFixture fixture = new(publishedDeclaration: true);
        PlatformCompositionOptions options = fixture.Options;
        options.RunPlan!.PlatformDeclarationHash.ShouldBe(PlatformPartiesPackageCatalogQualification.PublishedDeclarationHash);
        options.PartiesRunProof!.Matches(options).ShouldBeTrue();
        Should.Throw<InvalidDataException>(() => PlatformPartiesRunProof.CreateCurrent(options, fixture.Key));
        PlatformCompositionOptions replay = options with { RunPlan = options.RunPlan with { AttemptId = Hexalith.Builds.Tooling.Runtime.CompositionRunPlanFactory.NewRunId() } };
        options.PartiesRunProof.Matches(replay).ShouldBeFalse();
        File.AppendAllText(options.DeclarationPaths.Single(), " ");
        options.PartiesRunProof.Matches(options).ShouldBeFalse();
    }

    /// <summary>Static fixture selection requires the actual public Parties reader, preserving the historical image set for partial or old loaded owners.</summary>
    [Fact]
    public void OrdinaryFixtureUsesOnlyItsActualMeasuredOwnerAndImageCohort()
    {
        PlatformPartiesPackageFixture.PublishedImages.Count.ShouldBe(7);
        PlatformPartiesPackageFixture.PublishedImages.ShouldContain("registry.hexalith.com/parties@sha256:48d0e8db506e4ce574e8c7bd97e528a302d3fbf97754b007176904cbc1f3abfb");
        PlatformPartiesPackageFixture.PublishedImages.Except(PlatformPartiesPackageFixture.CurrentImages, StringComparer.Ordinal).Count().ShouldBe(1);
        bool published = PublishedCatalogArtifactCohort.Matches("Hexalith.Parties.Contracts", PublishedCatalogArtifactCohort.PublishedPartiesName);
        PlatformPartiesPackageFixture.Images.Count.ShouldBe(7);
        PlatformPartiesPackageFixture.Images.ShouldContain(published
            ? "registry.hexalith.com/parties@sha256:48d0e8db506e4ce574e8c7bd97e528a302d3fbf97754b007176904cbc1f3abfb"
            : "registry.hexalith.com/parties@sha256:25e4a03cfcc932f01fd9e66be647305c572a4c7d7e0a680db710cb9579bf783b");
        PlatformPartiesPackageFixture.Images.ShouldContain(published
            ? "registry.hexalith.com/tenants@sha256:1ba6ff9dd0623817d667bd46229dd0ba34a23ffc51d48c889d3e67f474bec22a"
            : "registry.hexalith.com/tenants@sha256:adfcad2eea7c0b59e2936a62131c7925af518f1331a4e9fec0071da55874caed");
    }

    /// <summary>Private source owners retain exact old/new catalog versions without acquiring public or unknown purpose authority.</summary>
    /// <param name="version">The catalog version.</param><param name="current">Whether the independently proven private purpose is selected.</param>
    /// <param name="cohort">The exact reader authority.</param><param name="accepted">The finite expected version compatibility.</param>
    [Theory]
    [InlineData("3.1.0", false, PublishedCatalogArtifactCohort.PublishedPartiesName, true)]
    [InlineData("3.0.0", false, PublishedCatalogArtifactCohort.FullPartiesName, true)]
    [InlineData("3.0.0", false, PublishedCatalogArtifactCohort.CurrentFullPartiesName, true)]
    [InlineData("3.0.0", true, PublishedCatalogArtifactCohort.CurrentOwnerName, true)]
    [InlineData("3.1.0", true, PublishedCatalogArtifactCohort.CurrentOwnerName, true)]
    [InlineData("3.0.0", false, PublishedCatalogArtifactCohort.PublishedPartiesName, false)]
    [InlineData("3.1.0", false, PublishedCatalogArtifactCohort.FullPartiesName, false)]
    [InlineData("3.1.0", false, PublishedCatalogArtifactCohort.CurrentFullPartiesName, false)]
    [InlineData("3.1.0+metadata", true, PublishedCatalogArtifactCohort.CurrentOwnerName, false)]
    [InlineData("3.1.1", true, PublishedCatalogArtifactCohort.CurrentOwnerName, false)]
    [InlineData("4.0.0", true, PublishedCatalogArtifactCohort.CurrentOwnerName, false)]
    [InlineData("unknown", true, PublishedCatalogArtifactCohort.CurrentOwnerName, false)]
    [InlineData("3.1.0", true, PublishedCatalogArtifactCohort.PublishedPartiesName, false)]
    [InlineData("3.1.0", false, PublishedCatalogArtifactCohort.CurrentOwnerName, false)]
    [InlineData("3.0.0", false, "", false)]
    [InlineData("3.0.0", false, "unknown", false)]
    [InlineData("3.1.0.0", true, PublishedCatalogArtifactCohort.CurrentOwnerName, false)]
    public void VersionCompatibilityPreservesEveryExactPurposeBoundary(string version, bool current, string cohort, bool accepted)
        => PlatformPartiesPackageCatalogQualification.MatchesPartiesVersion(version, current, cohort).ShouldBe(accepted);

    /// <summary>An unproven, partial or mixed private purpose cannot select ordinary public authority, even before reading catalog data.</summary>
    /// <param name="scope">Which private-purpose representation is present.</param>
    [Theory]
    [InlineData("primary")]
    [InlineData("batch")]
    [InlineData("both")]
    public void PrivatePurposeCannotSelectOrdinaryPublicAuthority(string scope)
    {
        using PlatformPartiesProofFixture fixture = new(publishedDeclaration: true);
        PlatformCompositionOptions options = fixture.Options;
        Hexalith.Builds.Tooling.Runtime.ModuleTestSelection ordinary = options.RunPlan!.SelectedTest!;
        Hexalith.Builds.Tooling.Runtime.ModuleTestSelection current = ordinary with { Name = PlatformCurrentOwnerProof.Profile };
        Hexalith.Builds.Tooling.Runtime.CompositionRunPlan plan = options.RunPlan with
        {
            SelectedTest = scope == "batch" ? ordinary : current,
            SelectedTests = scope == "primary" ? [ordinary] : scope == "batch" ? [ordinary, current] : [current],
        };
        PlatformPackageCatalogQualification.SelectCohort(options with { RunPlan = plan, PartiesRunProof = null }, fullParties: true).ShouldBeNull();
    }

    private static string Body(string name, string? version = null)
    {
        string root = typeof(PlatformPublishedPartiesCohortTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "PlatformHistoricalPackageRoot").Value!;
        version ??= name.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal) ? "4.1.0"
            : name.StartsWith("Hexalith.Tenants.", StringComparison.Ordinal) ? "7.0.5"
            : name.StartsWith("Hexalith.Memories.", StringComparison.Ordinal) ? "3.0.25"
            : name.StartsWith("Hexalith.Parties.", StringComparison.Ordinal) ? "3.1.0" : "2.0.1";
        string path = Path.Combine(root, name.ToLowerInvariant(), version, "lib", "net10.0", name + ".dll");
        File.Exists(path).ShouldBeTrue("Genuine signed public bodies are mandatory; no synthetic or source-built fallback.");
        return path;
    }
}