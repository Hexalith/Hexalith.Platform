namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Security.Cryptography;
using System.Runtime.Loader;
using Hexalith.Platform.CatalogArtifacts;
using Shouldly;

/// <summary>Checks actual signed public owner files independently of runner proof issuance and service allocation.</summary>
public sealed class PublishedCatalogArtifactCohortTests(ITestOutputHelper output)
{
    private const string _currentBase = "eventstore-4.1.0-tenants-7.0.2";
    private const string _currentFull = "parties-3.0.0-eventstore-4.1.0-tenants-7.0.2-memories-3.0.23";
    private static readonly string[] _names = ["Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults", "Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService",
        "Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server", "Hexalith.Memories.Contracts", "Hexalith.Memories.EventStore", "Hexalith.Memories.EventStoreExtensions",
        "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions", "Hexalith.McpCli.Abstractions"];

    /// <summary>The newly selected SDK and owners require their own exact public-body authority before any factory is read.</summary>
    [Fact]
    public void CurrentFullCohortAcceptsGenuineSignedPublicSdkAndOwners()
    {
        foreach (string name in _names)
        {
            Assembly assembly = Assembly.Load(name);
            PublishedCatalogArtifactCohort.Verify(assembly, _currentFull).ShouldBe(assembly.Location);
            output.WriteLine(name + " sha256=" + Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location))));
        }
    }

    /// <summary>The base cohort verifies the actual SDK and Tenant files without acquiring the full owner authority.</summary>
    [Fact]
    public void CurrentBaseCohortAcceptsOnlyItsPublicOwnerSet()
    {
        PublishedCatalogArtifactCohort.VerifySdk(_currentBase).Count.ShouldBe(4);
        PublishedCatalogArtifactCohort.VerifyFullParties(_currentFull).Count.ShouldBe(12);
        foreach (string name in _names.Take(6))
            PublishedCatalogArtifactCohort.Verify(Assembly.Load(name), _currentBase).ShouldNotBeNullOrWhiteSpace();
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(Assembly.Load("Hexalith.Parties.Contracts"), _currentBase));
    }

    /// <summary>Both historical authorities still require their original signed SDK/Tenant files and unchanged public M23/P3 owners.</summary>
    [Fact]
    public void HistoricalCohortsStillAcceptOriginalSignedPublicBodies()
    {
        var context = new AssemblyLoadContext("historical-public-bodies-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            foreach (string name in _names)
            {
                Assembly assembly = _names.Take(6).Contains(name, StringComparer.Ordinal) ? context.LoadFromAssemblyPath(HistoricalBody(name)) : Assembly.Load(name);
                PublishedCatalogArtifactCohort.Verify(assembly, PublishedCatalogArtifactCohort.FullPartiesName).ShouldBe(assembly.Location);
                if (_names.Take(6).Contains(name, StringComparer.Ordinal))
                    PublishedCatalogArtifactCohort.Verify(assembly, PublishedCatalogArtifactCohort.Name).ShouldBe(assembly.Location);
                output.WriteLine("historical " + name + " sha256=" + Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location))));
            }
        }
        finally { context.Unload(); }
    }

    /// <summary>Version selection accepts complete exact pairs and refuses unknown, aliased and cross-cohort pairs.</summary>
    /// <param name="sdk">The selected SDK version.</param><param name="tenants">The selected Tenant version.</param>
    /// <param name="full">Whether the full ordinary owner set is requested.</param><param name="expected">The sole expected authority or null.</param>
    [Theory]
    [InlineData("4.0.4", "7.0.0", false, "eventstore-4.0.4-tenants-7.0.0")]
    [InlineData("4.0.4", "7.0.0", true, "parties-3.0.0-eventstore-4.0.4-tenants-7.0.0-memories-3.0.23")]
    [InlineData("4.1.0", "7.0.2", false, _currentBase)]
    [InlineData("4.1.0", "7.0.2", true, _currentFull)]
    [InlineData("4.0.4", "7.0.2", false, null)]
    [InlineData("4.0.4", "7.0.2", true, null)]
    [InlineData("4.1.0", "7.0.0", false, null)]
    [InlineData("4.1.0", "7.0.0", true, null)]
    [InlineData("4.1.1", "7.0.2", false, null)]
    [InlineData("4.1.1", "7.0.2", true, null)]
    [InlineData("4.1.0+metadata", "7.0.2", false, null)]
    [InlineData("4.1.0", "7.0.2.0", false, null)]
    [InlineData("4.0.5", "7.0.0", false, null)]
    [InlineData("unknown", "unknown", false, null)]
    public void ExactVersionPairsDetermineOneClosedAuthority(string sdk, string tenants, bool full, string? expected)
        => PublishedCatalogArtifactCohort.ForVersions(sdk, tenants, full).ShouldBe(expected);

    /// <summary>A genuine released body from the other cohort is still the wrong body; equal owner names do not grant authority.</summary>
    /// <param name="historical">Whether the original published body is loaded.</param><param name="name">The actual selected public assembly.</param>
    /// <param name="cohort">The incompatible requested authority.</param>
    [Theory]
    [InlineData(true, "Hexalith.EventStore.Contracts", _currentBase)]
    [InlineData(true, "Hexalith.Tenants.Contracts", _currentBase)]
    [InlineData(true, "Hexalith.EventStore.Contracts", _currentFull)]
    [InlineData(true, "Hexalith.Tenants.Contracts", _currentFull)]
    [InlineData(false, "Hexalith.EventStore.Contracts", PublishedCatalogArtifactCohort.Name)]
    [InlineData(false, "Hexalith.Tenants.Contracts", PublishedCatalogArtifactCohort.Name)]
    [InlineData(false, "Hexalith.EventStore.Contracts", PublishedCatalogArtifactCohort.FullPartiesName)]
    [InlineData(false, "Hexalith.Tenants.Contracts", PublishedCatalogArtifactCohort.FullPartiesName)]
    public void MixedReleasedBodiesCannotAcquireOtherCohortAuthority(bool historical, string name, string cohort)
    {
        var context = new AssemblyLoadContext("mixed-public-body-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            Assembly assembly = historical ? context.LoadFromAssemblyPath(HistoricalBody(name)) : Assembly.Load(name);
            Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(assembly, cohort));
        }
        finally { context.Unload(); }
    }

    /// <summary>Only a disposable child copy is changed; both original and new signed body authorities refuse the changed bytes.</summary>
    /// <param name="historical">Whether the original released source is copied.</param><param name="name">The real public assembly.</param>
    /// <param name="cohort">The requested exact authority.</param>
    [Theory]
    [InlineData(false, "Hexalith.EventStore.Contracts", _currentFull)]
    [InlineData(false, "Hexalith.Tenants.Contracts", _currentFull)]
    [InlineData(true, "Hexalith.EventStore.Contracts", PublishedCatalogArtifactCohort.FullPartiesName)]
    [InlineData(true, "Hexalith.Tenants.Contracts", PublishedCatalogArtifactCohort.FullPartiesName)]
    public void AlteredPrivateCopyIsRefusedBeforeOwnerReflection(bool historical, string name, string cohort)
    {
        string directory = Path.Combine(Path.GetTempPath(), "changed-public-copy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string source = historical ? HistoricalBody(name) : Assembly.Load(name).Location;
        byte[] original = File.ReadAllBytes(source);
        string copy = Path.Combine(directory, name + ".dll");
        File.WriteAllBytes(copy, [.. original, 0]);
        var context = new AssemblyLoadContext("changed-public-body-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try { Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(context.LoadFromAssemblyPath(copy), cohort)); }
        finally { context.Unload(); Directory.Delete(directory, true); }
        File.ReadAllBytes(source).ShouldBe(original);
    }

    /// <summary>Foreign assemblies and unknown authorities refuse before their attributes or factories are accessed.</summary>
    /// <param name="cohort">The known or unknown requested authority.</param>
    [Theory]
    [InlineData(PublishedCatalogArtifactCohort.Name)]
    [InlineData(PublishedCatalogArtifactCohort.FullPartiesName)]
    [InlineData(_currentBase)]
    [InlineData(_currentFull)]
    [InlineData("unknown")]
    public void ForeignBodiesAndUnknownAuthoritiesRemainRefused(string cohort)
        => Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(typeof(PublishedCatalogArtifactCohortTests).Assembly, cohort));

    /// <summary>A base cohort request cannot be relabelled as a full owner verification.</summary>
    [Fact]
    public void FullVerificationRequiresAFullAuthority()
    {
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyFullParties(_currentBase));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyFullParties(PublishedCatalogArtifactCohort.Name));
    }

    /// <summary>The production old-first expression must reach the independently authenticated new cohort after an exact old-body mismatch.</summary>
    [Fact]
    public void OldFirstSdkPredicateReachesTheAuthenticatedCurrentCohort()
    {
        PublishedCatalogArtifactCohort.MatchesSdk().ShouldBeFalse();
        (PublishedCatalogArtifactCohort.MatchesSdk() || PublishedCatalogArtifactCohort.MatchesSdk(PublishedCatalogArtifactCohort.CurrentName)).ShouldBeTrue();
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifySdk());
        PublishedCatalogArtifactCohort.VerifySdk(PublishedCatalogArtifactCohort.CurrentName).Count.ShouldBe(4);
    }

    /// <summary>The boolean predicate distinguishes exact matching bytes from an incompatible or unknown cohort.</summary>
    /// <param name="cohort">The selected complete public authority.</param><param name="expected">Whether the genuine loaded SDK belongs to it.</param>
    [Theory]
    [InlineData(PublishedCatalogArtifactCohort.Name, false)]
    [InlineData(PublishedCatalogArtifactCohort.CurrentName, true)]
    [InlineData("unknown", false)]
    public void SdkPredicateReturnsFalseForTypedAuthorityRefusals(string cohort, bool expected)
        => PublishedCatalogArtifactCohort.MatchesSdk(cohort).ShouldBe(expected);

    /// <summary>The sibling owner predicate must return false for the same exact typed body/cohort refusal without granting mixed authority.</summary>
    /// <param name="name">The actual loaded owner or SDK assembly.</param><param name="cohort">The requested authority.</param>
    /// <param name="expected">Whether that body belongs to the selected authority.</param>
    [Theory]
    [InlineData("Hexalith.Tenants.Contracts", PublishedCatalogArtifactCohort.Name, false)]
    [InlineData("Hexalith.Tenants.Contracts", PublishedCatalogArtifactCohort.CurrentName, true)]
    [InlineData("Hexalith.Tenants.Contracts", PublishedCatalogArtifactCohort.FullPartiesName, false)]
    [InlineData("Hexalith.Tenants.Contracts", PublishedCatalogArtifactCohort.CurrentFullPartiesName, true)]
    [InlineData("Hexalith.Tenants.Contracts", "unknown", false)]
    [InlineData("Hexalith.EventStore.Contracts", PublishedCatalogArtifactCohort.Name, false)]
    [InlineData("Hexalith.EventStore.Contracts", PublishedCatalogArtifactCohort.CurrentName, true)]
    public void OwnerPredicatePreservesExactBodyAndCohortBoundaries(string name, string cohort, bool expected)
        => PublishedCatalogArtifactCohort.Matches(name, cohort).ShouldBe(expected);

    /// <summary>A loaded foreign body is a typed predicate refusal while strict verification still throws before owner reflection.</summary>
    [Fact]
    public void ForeignLoadedBodyRemainsRefusedByBothPredicateAndVerifier()
    {
        Assembly foreign = typeof(PublishedCatalogArtifactCohortTests).Assembly;
        PublishedCatalogArtifactCohort.Matches(foreign.GetName().Name!, PublishedCatalogArtifactCohort.CurrentFullPartiesName).ShouldBeFalse();
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(foreign, PublishedCatalogArtifactCohort.CurrentFullPartiesName));
    }

    private static string HistoricalBody(string name)
    {
        string? root = typeof(PublishedCatalogArtifactCohortTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "PlatformHistoricalPackageRoot").Value;
        root.ShouldNotBeNullOrWhiteSpace();
        string version = name.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal) ? "4.0.4" : "7.0.0";
        string path = Path.Combine(root!, name.ToLowerInvariant(), version, "lib", "net10.0", name + ".dll");
        File.Exists(path).ShouldBeTrue("The original signed public package download is mandatory; no fixture or current-body fallback.");
        return path;
    }

}
