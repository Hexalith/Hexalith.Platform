namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Security.Cryptography;
using System.Runtime.Loader;
using Hexalith.Platform.CatalogArtifacts;
using Shouldly;
using Xunit;

/// <summary>The current owner substitutes only its own bodies; reference packages retain exact signed public authority.</summary>
public sealed class PlatformCurrentReferenceCohortTests
{
    [Fact]
    public void CurrentReferenceCohortAuthenticatesEveryPublicBodyWithoutGrantingPublicPartiesAuthority()
    {
        IReadOnlyList<string> paths = PublishedCatalogArtifactCohort.VerifyCurrentReferences();
        paths.Count.ShouldBe(10);
        var artifacts = paths.Select(path => (path, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))))).ToArray();
        PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt(artifacts);
        foreach (string name in new[] { "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions" })
            Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(Assembly.Load(name), PublishedCatalogArtifactCohort.CurrentOwnerName));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyFullParties(PublishedCatalogArtifactCohort.CurrentOwnerName));
        PublishedCatalogArtifactCohort.ForVersions("4.1.0", "7.0.4", fullParties: true).ShouldBeNull("The private current purpose does not grant a new public-consumer cohort.");
        PublishedCatalogArtifactCohort.ForVersions("4.1.0", "7.0.2", fullParties: true).ShouldBe(PublishedCatalogArtifactCohort.CurrentFullPartiesName);
        string cache = typeof(PlatformCurrentReferenceCohortTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "PlatformHistoricalPackageRoot").Value!;
        var context = new AssemblyLoadContext("old-reference-bodies-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            foreach (string name in new[] { "Hexalith.Memories.Contracts", "Hexalith.Memories.EventStore", "Hexalith.Memories.EventStoreExtensions", "Hexalith.McpCli.Abstractions" })
            {
                string version = name == "Hexalith.McpCli.Abstractions" ? "2.0.0" : "3.0.23";
                string path = Path.Combine(cache, name.ToLowerInvariant(), version, "lib", "net10.0", name + ".dll");
                File.Exists(path).ShouldBeTrue("The original public package is a required restore input; no synthetic or current-body substitution.");
                Assembly old = context.LoadFromAssemblyPath(path);
                PublishedCatalogArtifactCohort.Verify(old, PublishedCatalogArtifactCohort.CurrentFullPartiesName).ShouldBe(path);
                Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(old, PublishedCatalogArtifactCohort.CurrentOwnerName));
            }
        }
        finally { context.Unload(); }
        var previous = new AssemblyLoadContext("previous-reference-bodies-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            foreach (string name in new[] { "Hexalith.Memories.Contracts", "Hexalith.Memories.EventStore", "Hexalith.Memories.EventStoreExtensions" })
            {
                string path = Path.Combine(cache, name.ToLowerInvariant(), "3.0.24", "lib", "net10.0", name + ".dll");
                File.Exists(path).ShouldBeTrue("The previously qualified Memory reference body remains an explicit restore input.");
                Assembly old = previous.LoadFromAssemblyPath(path);
                PublishedCatalogArtifactCohort.Verify(old, PublishedCatalogArtifactCohort.PreviousCurrentOwnerName).ShouldBe(path);
                Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(old, PublishedCatalogArtifactCohort.CurrentOwnerName));
            }
            Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyFullParties(PublishedCatalogArtifactCohort.PreviousCurrentOwnerName));
        }
        finally { previous.Unload(); }
    }

    [Fact]
    public void CurrentReferenceReceiptRefusesAbsentDuplicateAndTamperedPublicBodies()
    {
        var artifacts = PublishedCatalogArtifactCohort.VerifyCurrentReferences()
            .Select(path => (Path: path, Hash: Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))))).ToArray();
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt(artifacts.Skip(1).ToArray()));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt([.. artifacts, artifacts[0]]));
        var changed = artifacts.Select((item, index) => index == 0 ? (item.Path, new string('0', 64)) : item).ToArray();
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt(changed));
        string directory = Path.Combine(Path.GetTempPath(), "current-reference-body-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, Path.GetFileName(artifacts[0].Path));
            File.WriteAllBytes(path, [.. File.ReadAllBytes(artifacts[0].Path), 0]);
            changed = artifacts.Select((item, index) => index == 0 ? (path, item.Hash) : item).ToArray();
            Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyCurrentReferenceReceipt(changed));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
