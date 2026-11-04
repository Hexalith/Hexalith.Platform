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

    /// <summary>Checks the complete finite plan, source, declarations, package versions and topology before image probes or rendering.</summary>
    internal static IReadOnlyList<PlatformServerBinding> Bind(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations,
        IReadOnlyList<PlatformServerBinding> servers, bool developmentRun)
        => Bind(options, declarations, servers, developmentRun, OperatingSystem.IsLinux(), RuntimeInformation.ProcessArchitecture);

    /// <summary>Exercises the same complete guard with an explicit runtime scope; the production entry always supplies the actual OS and architecture.</summary>
    internal static IReadOnlyList<PlatformServerBinding> Bind(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations,
        IReadOnlyList<PlatformServerBinding> servers, bool developmentRun, bool linux, Architecture architecture)
    {
        if (!developmentRun || !linux || architecture != Architecture.X64
            || options.PartiesRunProof?.Matches(options) != true
            || !declarations.Select(module => PlatformDeclarationSet.Id(module.Declaration)).Order(StringComparer.Ordinal)
                .SequenceEqual(["eventstore", "memories", "parties", "tenants"], StringComparer.Ordinal)
            || !servers.Select(Identity).Order(StringComparer.Ordinal).SequenceEqual(_images.Keys.Append("eventstore/gateway").Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || !Snapshot(options, declarations, "eventstore", "21eda6da824d5ae6135ff342dd8a5aae950b02420319b953a836dddeb00ce495")
            || !Snapshot(options, declarations, "tenants", "1588f3428ec29296c452fdd35bf95146142315aad3ad8ceb934e7a791f1fb522")
            || !Snapshot(options, declarations, "memories", "56f6dc8134d3f7cea06910e923a4736a450e00241e8e4a565ac9226897881840")
            || !Snapshot(options, declarations, "parties", ActiveDeclarationHash)
            || !VersionsMatch(options) || !PlatformPackageCatalogQualification.HasQualifiedSdkBytes()
            || !PublishedCatalogArtifactCohort.Matches("Hexalith.Tenants.Contracts")
            || !PublishedCatalogArtifactCohort.Matches("Hexalith.Memories.Contracts", PublishedCatalogArtifactCohort.FullPartiesName)
            || !PublishedCatalogArtifactCohort.Matches("Hexalith.Parties.Contracts", PublishedCatalogArtifactCohort.FullPartiesName)
            || !CompositionMatches(options, declarations)) throw Refusal();
        return servers.Select(server =>
        {
            if (Identity(server) == "eventstore/gateway")
                return server.Image is null && server.ProjectPath == options.EventStoreSeedProject ? server : throw Refusal();
            var expected = _images[Identity(server)];
            return server.Image == expected.Image && server.ImageDigest is null ? server with { ImageDigest = expected.Digest } : throw Refusal();
        }).ToArray();
    }

    private static bool VersionsMatch(PlatformCompositionOptions options) => new[] { ("eventstore", "4.0.4"), ("tenants", "7.0.0"), ("memories", "3.0.23"), ("parties", "3.0.0") }
        .All(item => PlatformServerBindings.Version(options, item.Item1, "full-Parties qualification") == item.Item2);

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
