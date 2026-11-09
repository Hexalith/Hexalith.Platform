#if HEXALITH_PLATFORM_EXTENSION_SDK && !HEXALITH_PLATFORM_SOURCE_CATALOG
namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using Hexalith.Platform.CatalogArtifacts;
using Shouldly;

/// <summary>Authenticates the exact SDK4.2 reader and private references independently from retained SDK4.1 owner images.</summary>
public sealed class PlatformSdk420ArtifactCohortTests
{
    private static readonly string[] _names = ["Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults", "Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService",
        "Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server", "Hexalith.Memories.Contracts", "Hexalith.Memories.EventStore", "Hexalith.Memories.EventStoreExtensions",
        "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions", "Hexalith.McpCli.Abstractions"];

    /// <summary>Every genuine public reader body authenticates under its independently measured authority.</summary>
    [Fact]
    public void ExactPublicReaderAuthenticatesAllTwelveBodies()
    {
        var context = new AssemblyLoadContext("sdk420-public-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            foreach (string name in _names)
            {
                string path = Body(name);
                Assembly body = context.LoadFromAssemblyPath(path);
                PublishedCatalogArtifactCohort.Verify(body, PublishedCatalogArtifactCohort.PublishedPartiesSdk420Name).ShouldBe(path);
                if (name.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal))
                    Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(body, PublishedCatalogArtifactCohort.PublishedPartiesName));
                else
                    PublishedCatalogArtifactCohort.Verify(body, PublishedCatalogArtifactCohort.PublishedPartiesName).ShouldBe(path);
            }
        }
        finally { context.Unload(); }
        PublishedCatalogArtifactCohort.ForPublishedPartiesVersions("4.2.0", "7.0.5", "3.0.25", "3.1.0")
            .ShouldBe(PublishedCatalogArtifactCohort.PublishedPartiesSdk420Name);
        PublishedCatalogArtifactCohort.ForVersions("4.2.0", "7.0.5").ShouldBeNull();
        PublishedCatalogArtifactCohort.ForVersions("4.2.0", "7.0.5", fullParties: true).ShouldBeNull();
    }

    /// <summary>Even loadable appended bytes lose the finite authority before owner reflection.</summary>
    /// <param name="name">The genuine body to alter in an isolated copy.</param>
    [Theory]
    [InlineData("Hexalith.EventStore.Contracts")]
    [InlineData("Hexalith.EventStore.ServiceDefaults")]
    [InlineData("Hexalith.EventStore.Client")]
    [InlineData("Hexalith.EventStore.DomainService")]
    [InlineData("Hexalith.Tenants.Contracts")]
    [InlineData("Hexalith.Tenants.Server")]
    [InlineData("Hexalith.Memories.Contracts")]
    [InlineData("Hexalith.Memories.EventStore")]
    [InlineData("Hexalith.Memories.EventStoreExtensions")]
    [InlineData("Hexalith.Parties.Contracts")]
    [InlineData("Hexalith.Parties.EventStoreExtensions")]
    [InlineData("Hexalith.McpCli.Abstractions")]
    public void ChangedPublicBodyRefuses(string name)
    {
        string source = Body(name);
        byte[] original = File.ReadAllBytes(source);
        string directory = Path.Combine(Path.GetTempPath(), "sdk420-changed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var context = new AssemblyLoadContext("sdk420-changed-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            string path = Path.Combine(directory, name + ".dll");
            File.WriteAllBytes(path, [.. original, 0]);
            Assembly body = context.LoadFromAssemblyPath(path);
            Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(body, PublishedCatalogArtifactCohort.PublishedPartiesSdk420Name));
        }
        finally { context.Unload(); Directory.Delete(directory, recursive: true); }
        File.ReadAllBytes(source).ShouldBe(original);
    }

    /// <summary>Aliases, future versions and mixed owner tuples cannot acquire SDK4.2 reader authority.</summary>
    /// <param name="sdk">The selected SDK.</param><param name="tenants">The selected Tenants package.</param>
    /// <param name="memories">The selected Memories package.</param><param name="parties">The selected Parties package.</param>
    [Theory]
    [InlineData("4.2.0-preview.1", "7.0.5", "3.0.25", "3.1.0")]
    [InlineData("4.2.0+metadata", "7.0.5", "3.0.25", "3.1.0")]
    [InlineData("4.2.0.0", "7.0.5", "3.0.25", "3.1.0")]
    [InlineData("04.2.0", "7.0.5", "3.0.25", "3.1.0")]
    [InlineData("4.2.1", "7.0.5", "3.0.25", "3.1.0")]
    [InlineData("4.3.0", "7.0.5", "3.0.25", "3.1.0")]
    [InlineData("4.2.0", "7.0.2", "3.0.25", "3.1.0")]
    [InlineData("4.2.0", "7.0.4", "3.0.25", "3.1.0")]
    [InlineData("4.2.0", "7.0.5+metadata", "3.0.25", "3.1.0")]
    [InlineData("4.2.0", "7.0.5", "3.0.24", "3.1.0")]
    [InlineData("4.2.0", "7.0.5", "3.0.25", "3.0.0")]
    [InlineData("4.2.0", "7.0.5", "3.0.25", "3.1.1")]
    public void UnmeasuredPublicTupleRefuses(string sdk, string tenants, string memories, string parties)
        => PublishedCatalogArtifactCohort.ForPublishedPartiesVersions(sdk, tenants, memories, parties).ShouldBeNull();

    /// <summary>The private source-owner reference selector retains exact historical and new tuples only.</summary>
    /// <param name="sdk">The selected SDK.</param><param name="tenants">The selected Tenants package.</param>
    /// <param name="memories">The selected Memories package.</param><param name="expected">The exact expected authority.</param>
    [Theory]
    [InlineData("4.1.0", "7.0.5", "3.0.25", PublishedCatalogArtifactCohort.CurrentOwnerName)]
    [InlineData("4.2.0", "7.0.5", "3.0.25", PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name)]
    [InlineData("4.2.0-preview.1", "7.0.5", "3.0.25", null)]
    [InlineData("4.2.0+metadata", "7.0.5", "3.0.25", null)]
    [InlineData("4.2.0.0", "7.0.5", "3.0.25", null)]
    [InlineData("4.2.1", "7.0.5", "3.0.25", null)]
    [InlineData("4.3.0", "7.0.5", "3.0.25", null)]
    [InlineData("4.2.0", "7.0.4", "3.0.25", null)]
    [InlineData("4.2.0", "7.0.5+metadata", "3.0.25", null)]
    [InlineData("4.2.0", "7.0.5", "3.0.24", null)]
    [InlineData("4.2.0", "7.0.5", "3.0.25+metadata", null)]
    public void PrivateReferenceSelectionRetainsFiniteVersions(string sdk, string tenants, string memories, string? expected)
        => PublishedCatalogArtifactCohort.ForCurrentOwnerVersions(sdk, tenants, memories).ShouldBe(expected);

    /// <summary>Complete private receipts bind all actual reference files and cannot mix old SDK bodies or substitute public purpose.</summary>
    [Fact]
    public void PrivateReceiptsAuthenticateAllReferencesAndRejectCrossCohortReplay()
    {
        var references = _names.Where(name => !name.StartsWith("Hexalith.Parties.", StringComparison.Ordinal))
            .Select(name => (Path: Body(name), Hash: Digest(Body(name)))).ToArray();
        PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt(references, PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name);
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt(references));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt(references, PublishedCatalogArtifactCohort.PublishedPartiesSdk420Name));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt(references[1..], PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt([.. references, references[0]], PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name));
        foreach (string name in _names.Where(name => name.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal)))
        {
            PublishedCatalogArtifactCohort.VerifyCurrentSdkFile(Body(name), Digest(Body(name)), PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name);
            Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyCurrentSdkFile(Body(name), Digest(Body(name))));
            PublishedCatalogArtifactCohort.VerifyCurrentSdkFile(Body(name, "4.1.0"), Digest(Body(name, "4.1.0")));
            Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyCurrentSdkFile(Body(name, "4.1.0"), Digest(Body(name, "4.1.0")), PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name));
        }
    }

    /// <summary>Private references exclude public Parties and cannot become full public or ordinary owner authority.</summary>
    [Fact]
    public void PrivatePurposesCannotAcquireFullPublicOwnerAuthority()
    {
        PublishedCatalogArtifactCohort.CurrentOwnerSdkVersion(PublishedCatalogArtifactCohort.CurrentOwnerName).ShouldBe("4.1.0");
        PublishedCatalogArtifactCohort.CurrentOwnerSdkVersion(PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name).ShouldBe("4.2.0");
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.CurrentOwnerSdkVersion(PublishedCatalogArtifactCohort.PublishedPartiesSdk420Name));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyFullParties(PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyFullParties(PublishedCatalogArtifactCohort.EventStorePackageTestSdk420Name));
        PlatformPartiesPackageCatalogQualification.MatchesPartiesVersion("3.1.0", false, PublishedCatalogArtifactCohort.PublishedPartiesSdk420Name).ShouldBeTrue();
        PlatformPartiesPackageCatalogQualification.MatchesPartiesVersion("3.0.0", false, PublishedCatalogArtifactCohort.PublishedPartiesSdk420Name).ShouldBeFalse();
        PlatformPartiesPackageCatalogQualification.MatchesPartiesVersion("3.1.0", true, PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name).ShouldBeTrue();
        PlatformPartiesPackageCatalogQualification.MatchesPartiesVersion("3.0.0", true, PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name).ShouldBeFalse();
        PlatformPartiesPackageCatalogQualification.MatchesPartiesVersion("3.1.0+metadata", true, PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name).ShouldBeFalse();
        PlatformPartiesPackageCatalogQualification.MatchesPartiesVersion("3.1.0", true, PublishedCatalogArtifactCohort.PublishedPartiesSdk420Name).ShouldBeFalse();
    }

    private static string Digest(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static string Body(string name, string? version = null)
    {
        string root = typeof(PlatformSdk420ArtifactCohortTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "PlatformHistoricalPackageRoot").Value!;
        version ??= name.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal) ? "4.2.0"
            : name.StartsWith("Hexalith.Tenants.", StringComparison.Ordinal) ? "7.0.5"
            : name.StartsWith("Hexalith.Memories.", StringComparison.Ordinal) ? "3.0.25"
            : name.StartsWith("Hexalith.Parties.", StringComparison.Ordinal) ? "3.1.0" : "2.0.1";
        string path = Path.Combine(root, name.ToLowerInvariant(), version, "lib", "net10.0", name + ".dll");
        File.Exists(path).ShouldBeTrue("Genuine signed public bodies are mandatory.");
        return path;
    }
}

#endif
