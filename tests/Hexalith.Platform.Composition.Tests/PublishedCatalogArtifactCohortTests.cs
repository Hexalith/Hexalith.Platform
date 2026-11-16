namespace Hexalith.Platform.Composition.Tests;

using Hexalith.Platform.CatalogArtifacts;
using Shouldly;

/// <summary>Checks the closed production cohort selection and refusal rules without loading any historical package body.</summary>
/// <remarks>Only the cohort-independent cases of the retired replay-only class remain; old signed-body checks stay retired.</remarks>
public sealed class PublishedCatalogArtifactCohortTests
{
    private const string _sdk410Base = "eventstore-4.1.0-tenants-7.0.2";
    private const string _sdk410Full = "parties-3.0.0-eventstore-4.1.0-tenants-7.0.2-memories-3.0.23";

    /// <summary>Version selection accepts complete exact pairs and refuses unknown, aliased and cross-cohort pairs.</summary>
    /// <param name="sdk">The selected SDK version.</param><param name="tenants">The selected Tenant version.</param>
    /// <param name="full">Whether the full ordinary owner set is requested.</param><param name="expected">The sole expected authority or null.</param>
    [Theory]
    [InlineData("4.0.4", "7.0.0", false, "eventstore-4.0.4-tenants-7.0.0")]
    [InlineData("4.0.4", "7.0.0", true, "parties-3.0.0-eventstore-4.0.4-tenants-7.0.0-memories-3.0.23")]
    [InlineData("4.1.0", "7.0.2", false, _sdk410Base)]
    [InlineData("4.1.0", "7.0.2", true, _sdk410Full)]
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

    /// <summary>Foreign assemblies and unknown authorities refuse before their attributes or factories are accessed.</summary>
    /// <param name="cohort">The known or unknown requested authority.</param>
    [Theory]
    [InlineData(PublishedCatalogArtifactCohort.Name)]
    [InlineData(PublishedCatalogArtifactCohort.FullPartiesName)]
    [InlineData(_sdk410Base)]
    [InlineData(_sdk410Full)]
    [InlineData("unknown")]
    public void ForeignBodiesAndUnknownAuthoritiesRemainRefused(string cohort)
        => Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.Verify(typeof(PublishedCatalogArtifactCohortTests).Assembly, cohort));

    /// <summary>A base cohort request cannot be relabelled as a full owner verification.</summary>
    [Fact]
    public void FullVerificationRequiresAFullAuthority()
    {
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyFullParties(_sdk410Base));
        Should.Throw<InvalidDataException>(() => PublishedCatalogArtifactCohort.VerifyFullParties(PublishedCatalogArtifactCohort.Name));
    }
}
