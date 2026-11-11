namespace Hexalith.Platform.Composition;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Platform.CatalogArtifacts;

/// <summary>The finite ordinary-Parties public/image cohort; this does not grant candidate or runtime qualification.</summary>
internal static class PlatformPartiesPackageCatalogQualification
{
    /// <summary>The selected current test declaration, including its actual additive lifecycle suite.</summary>
    internal const string ActiveDeclarationHash = "40cee6ce4b82902d994b8c2a1baa0629dab33e8f2afc5c8687214fc1dab1641b";
    /// <summary>The independently published backend declaration at source639; it has no lifecycle suite.</summary>
    internal const string ReleasedDeclarationHash = "86002eeb916c77bd753a3c618ce394125794baf421315604c9f551b762624504";

    /// <summary>The actual public3.1 declaration from release source50904cc4; separate from active-owner source authority.</summary>
    internal const string PublishedDeclarationHash = "d9c193b0e7b61601b8fcceb906b5dafe4769a1b9d7538a6ce164b4aa0840b348";

    /// <summary>Only these exact old and additive-suite declaration bytes retain ordinary profile compatibility.</summary>
    internal static bool IsOrdinaryDeclaration(string? hash) => string.Equals(hash, ActiveDeclarationHash, StringComparison.OrdinalIgnoreCase)
        || string.Equals(hash, PlatformCurrentOwnerProof.DeclarationHash, StringComparison.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, (string Image, string Digest)> _images = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        ["tenants/domain"] = ("registry.hexalith.com/tenants:7.0.0", "2f4ab155bc4d33e4b16f1bc8995c45de83503bd8ad99d2c7e46ebbef382b6c42"),
        ["tenants/api"] = ("registry.hexalith.com/tenants-api:7.0.0", "2969ad3f75b154566567dbd6a8f55dd8a64364cbec6fed96398e921379dde9b6"),
        ["memories/server"] = ("registry.hexalith.com/memories:3.0.23", "f8684c3d8a084a5bc357ec13e9c7c5294a350d73f570b585f5fb2bfde3d9904b"),
        ["memories/domain"] = ("registry.hexalith.com/memories-domain-service:3.0.23", "fb0190a1fc5ffdbdcf9cf5d4941ef81600bde23d38840058f695b52623870f5e"),
        ["memories/clock"] = ("registry.hexalith.com/memories-access-telemetry-clock:3.0.23", "b64505ee27cfd455f7efe730831a72688ca92ef6b49fd904eb0e68abdd38e54f"),
        ["memories/telemetry"] = ("registry.hexalith.com/memories-access-telemetry:3.0.23", "ee01a024abb17a7d465ba8fe5fbb626114b7fb380fc521a5fa7531ca514d548a"),
        ["parties/domain"] = ("registry.hexalith.com/parties:3.0.0", "25e4a03cfcc932f01fd9e66be647305c572a4c7d7e0a680db710cb9579bf783b"),
    };

    private static readonly IReadOnlyDictionary<string, (string Image, string Digest)> _currentImages = _images
        .Where(pair => !pair.Key.StartsWith("tenants/", StringComparison.Ordinal))
        .Concat(new Dictionary<string, (string Image, string Digest)>(StringComparer.Ordinal)
        {
            ["tenants/domain"] = ("registry.hexalith.com/tenants:7.0.2", "adfcad2eea7c0b59e2936a62131c7925af518f1331a4e9fec0071da55874caed"),
            ["tenants/api"] = ("registry.hexalith.com/tenants-api:7.0.2", "2922e0b1eb7b115bac472479213ca46d84c26ac4917b1d53854034618179c2e4"),
        }).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, (string Image, string Digest)> _previousCurrentOwnerImages = _currentImages
        .Where(pair => !pair.Key.StartsWith("tenants/", StringComparison.Ordinal) && !pair.Key.StartsWith("memories/", StringComparison.Ordinal))
        .Concat(new Dictionary<string, (string Image, string Digest)>(StringComparer.Ordinal)
        {
            ["tenants/domain"] = ("registry.hexalith.com/tenants:7.0.4", "712a6975c87d804e93399026c25a668926daaf60b2cb1837729d7a464100166c"),
            ["tenants/api"] = ("registry.hexalith.com/tenants-api:7.0.4", "39e1fab54a79df171b3ac4aa40a3e3d4bb2812697ed80f6dc330b55ae00393d2"),
            ["memories/server"] = ("registry.hexalith.com/memories:3.0.24", "7cbba1867709359bf7a4c5f115e8879bfbb9548cdf209947dc0f8c633ad859d7"),
            ["memories/domain"] = ("registry.hexalith.com/memories-domain-service:3.0.24", "efbd5e1d37e8ac75c78855ae4f22664ea294b8f6e1a65e4a8c6f44c1efb5eacb"),
            ["memories/clock"] = ("registry.hexalith.com/memories-access-telemetry-clock:3.0.24", "7da6f0849a1994513e126f3cbea50754c414be563d499850830e34c10ac411ef"),
            ["memories/telemetry"] = ("registry.hexalith.com/memories-access-telemetry:3.0.24", "7c4f498dc7f7f0a2ab965efd05f76853f6f42a1b2513e3e0d94eeea96971620a"),
        }).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, (string Image, string Digest)> _tenants704CurrentOwnerImages = _previousCurrentOwnerImages
        .Where(pair => !pair.Key.StartsWith("memories/", StringComparison.Ordinal))
        .Concat(new Dictionary<string, (string Image, string Digest)>(StringComparer.Ordinal)
        {
            ["memories/server"] = ("registry.hexalith.com/memories:3.0.25", "a857a0865ff3781027cc392a7d36431fd13db1e7a3522690a8ef05df7036af3a"),
            ["memories/domain"] = ("registry.hexalith.com/memories-domain-service:3.0.25", "90d15d551cb1ada3aafbcab823cbd8e83304605ca383abdbd47573613304e820"),
            ["memories/clock"] = ("registry.hexalith.com/memories-access-telemetry-clock:3.0.25", "e4edd4f62a43fc404f40eac2fd107c17320420b38bbd6514f31f23b19b019d3e"),
            ["memories/telemetry"] = ("registry.hexalith.com/memories-access-telemetry:3.0.25", "d227044d62db58b87c26f99a2ea6264b148ffd0c8d15c452b3bf4b30dd244e0b"),
        }).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, (string Image, string Digest)> _currentOwnerImages = _tenants704CurrentOwnerImages
        .Where(pair => !pair.Key.StartsWith("tenants/", StringComparison.Ordinal))
        .Concat(new Dictionary<string, (string Image, string Digest)>(StringComparer.Ordinal)
        {
            ["tenants/domain"] = ("registry.hexalith.com/tenants:7.0.5", "1ba6ff9dd0623817d667bd46229dd0ba34a23ffc51d48c889d3e67f474bec22a"),
            ["tenants/api"] = ("registry.hexalith.com/tenants-api:7.0.5", "f4707de77c595977d5b7913b4e6119aed407da3c72475ba13b46e9e302d1ef7b"),
        }).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, (string Image, string Digest)> _publishedPartiesImages = _currentOwnerImages
        .Where(pair => pair.Key != "parties/domain")
        .Concat(new Dictionary<string, (string Image, string Digest)>(StringComparer.Ordinal)
        {
            ["parties/domain"] = ("registry.hexalith.com/parties:3.1.0", "48d0e8db506e4ce574e8c7bd97e528a302d3fbf97754b007176904cbc1f3abfb"),
        }).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    /// <summary>Checks the complete finite plan, source, declarations, package versions and topology before image probes or rendering.</summary>
    internal static IReadOnlyList<PlatformServerBinding> Bind(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations,
        IReadOnlyList<PlatformServerBinding> servers, bool developmentRun)
        => Bind(options, declarations, servers, developmentRun, OperatingSystem.IsLinux(), RuntimeInformation.ProcessArchitecture);

    /// <summary>Exercises the same complete guard with an explicit runtime scope; the production entry always supplies the actual OS and architecture.</summary>
    internal static IReadOnlyList<PlatformServerBinding> Bind(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations,
        IReadOnlyList<PlatformServerBinding> servers, bool developmentRun, bool linux, Architecture architecture)
    {
        bool current = options.CurrentOwnerProof?.Matches(options) == true;
        string? cohort = null;
        string? ownerHash = options.RunPlan?.PlatformDeclarationHash;
        if (!current && !IsOrdinaryDeclaration(ownerHash)) throw Refusal();
        if (!developmentRun || !linux || architecture != Architecture.X64
            || (!current && options.PartiesRunProof?.Matches(options) != true)
            || !declarations.Select(module => PlatformDeclarationSet.Id(module.Declaration)).Order(StringComparer.Ordinal)
                .SequenceEqual(["eventstore", "memories", "parties", "tenants"], StringComparer.Ordinal)
            || !servers.Select(Identity).Order(StringComparer.Ordinal).SequenceEqual(_images.Keys.Append("eventstore/gateway").Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || !Snapshot(options, declarations, "eventstore", "21eda6da824d5ae6135ff342dd8a5aae950b02420319b953a836dddeb00ce495")
            || !Snapshot(options, declarations, "tenants", "1588f3428ec29296c452fdd35bf95146142315aad3ad8ceb934e7a791f1fb522")
            || !Snapshot(options, declarations, "memories", "56f6dc8134d3f7cea06910e923a4736a450e00241e8e4a565ac9226897881840")
            || !Snapshot(options, declarations, "parties", current ? options.CurrentOwnerProof!.SelectedDeclarationHash : ownerHash!.ToLowerInvariant())
            || (cohort = current ? PlatformPackageCatalogQualification.SelectCurrentOwnerCohort(options) : PlatformPackageCatalogQualification.SelectCohort(options, fullParties: true)) is null
            // Signed reader artifacts alone cannot authorize new execution image tuples.
            || cohort is PublishedCatalogArtifactCohort.CurrentOwnerCurrentName or PublishedCatalogArtifactCohort.PublishedPartiesCurrentName
            || cohort is PublishedCatalogArtifactCohort.PublishedPartiesName or PublishedCatalogArtifactCohort.PublishedPartiesSdk420Name && !string.Equals(ownerHash, PublishedDeclarationHash, StringComparison.OrdinalIgnoreCase)
            || !VersionsMatch(options, current, cohort) || !PlatformPackageCatalogQualification.HasQualifiedSdkBytes(cohort)
            || !PublishedCatalogArtifactCohort.Matches("Hexalith.Tenants.Contracts", cohort)
            || !PublishedCatalogArtifactCohort.Matches("Hexalith.Memories.Contracts", cohort)
            || !current && !PublishedCatalogArtifactCohort.Matches("Hexalith.Parties.Contracts", cohort)
            || !CompositionMatches(options, declarations)) throw Refusal();
        IReadOnlyDictionary<string, (string Image, string Digest)> images = current ? _currentOwnerImages
            : cohort is PublishedCatalogArtifactCohort.PublishedPartiesName or PublishedCatalogArtifactCohort.PublishedPartiesSdk420Name ? _publishedPartiesImages
            : cohort == PublishedCatalogArtifactCohort.CurrentFullPartiesName ? _currentImages : _images;
        return servers.Select(server =>
        {
            if (Identity(server) == "eventstore/gateway")
                return server.Image is null && server.ProjectPath == options.EventStoreSeedProject ? server : throw Refusal();
            if (current && Identity(server) == "parties/domain")
                return server.Image is null && server.ProjectPath == PlatformCurrentOwnerProof.OwnerProject(options) ? server : throw Refusal();
            var expected = images[Identity(server)];
            return server.Image == expected.Image && server.ImageDigest is null ? server with { ImageDigest = expected.Digest } : throw Refusal();
        }).ToArray();
    }

    /// <summary>Retains exact historical and public versions and only the two measured private source-owner catalog versions.</summary>
    internal static bool MatchesPartiesVersion(string version, bool current, string cohort) => (current, cohort, version) switch
    {
        (true, PublishedCatalogArtifactCohort.CurrentOwnerName, "3.0.0" or "3.1.0") => true,
        (true, PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name, "3.1.0") => true,
        (true, PublishedCatalogArtifactCohort.CurrentOwnerCurrentName, "3.1.1") => true,
        (false, PublishedCatalogArtifactCohort.PublishedPartiesCurrentName, "3.1.1") => true,
        (false, PublishedCatalogArtifactCohort.PublishedPartiesName or PublishedCatalogArtifactCohort.PublishedPartiesSdk420Name, "3.1.0") => true,
        (false, PublishedCatalogArtifactCohort.FullPartiesName or PublishedCatalogArtifactCohort.CurrentFullPartiesName, "3.0.0") => true,
        _ => false,
    };

    private static bool VersionsMatch(PlatformCompositionOptions options, bool current, string cohort)
        => PlatformServerBindings.Version(options, "memories", "full-Parties qualification")
                == (current ? PublishedCatalogArtifactCohort.CurrentOwnerMemoriesVersion(cohort)
                    : cohort == PublishedCatalogArtifactCohort.PublishedPartiesCurrentName ? "3.0.26"
                    : cohort is PublishedCatalogArtifactCohort.PublishedPartiesName or PublishedCatalogArtifactCohort.PublishedPartiesSdk420Name ? "3.0.25" : "3.0.23")
            && MatchesPartiesVersion(PlatformServerBindings.Version(options, "parties", "full-Parties qualification"), current, cohort);

    private static bool Snapshot(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations, string owner, string expected)
    {
        PlatformModuleDeclaration selected = declarations.Single(module => PlatformDeclarationSet.Id(module.Declaration) == owner);
        PlatformModuleDeclaration authority = PlatformDeclarationSet.Load(options).Single(module => PlatformDeclarationSet.Id(module.Declaration) == owner);
        return Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(selected.Source))) == expected
            && selected.Source == authority.Source && JsonElement.DeepEquals(selected.Declaration, authority.Declaration);
    }

    // The existing producer's ordinary closure identity has no additive-owner binding or MAC.
    private static bool CompositionMatches(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations)
    {
        var selected = options.RunPlan!.SelectedTest;
        string digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            modules = declarations.OrderBy(declaration => PlatformDeclarationSet.Id(declaration.Declaration), StringComparer.Ordinal)
                .Select(declaration => new { declaration.Declaration, SourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(declaration.Source))) }),
            selected?.Mode, selected?.DaprRuntimeVersion, retention = false,
        })));
        return options.RunPlan.CompositionContentIdentity == digest;
    }

    private static string Identity(PlatformServerBinding server) => PlatformDeclarationSet.Id(server.Module.Declaration) + "/" + server.ServerId;
    private static InvalidDataException Refusal() => new("The ordinary Parties package profile requires its exact qualified full owner/public/image cohort.");
}
