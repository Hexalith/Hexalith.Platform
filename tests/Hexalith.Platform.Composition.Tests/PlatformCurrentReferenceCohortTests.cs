namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Security.Cryptography;
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
