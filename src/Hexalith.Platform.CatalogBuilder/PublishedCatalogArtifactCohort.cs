namespace Hexalith.Platform.CatalogArtifacts;

using System.Collections.Frozen;
using System.Reflection;
using System.Security.Cryptography;

/// <summary>The finite signed public artifacts measured for the SDK4.0.4/Tenants7 reader, distinct from image owner binaries.</summary>
internal static class PublishedCatalogArtifactCohort
{
    /// <summary>The sole measured public package cohort identifier.</summary>
    internal const string Name = "eventstore-4.0.4-tenants-7.0.0";
    /// <summary>The independently measured complete ordinary-Parties public reader cohort.</summary>
    internal const string FullPartiesName = "parties-3.0.0-eventstore-4.0.4-tenants-7.0.0-memories-3.0.23";
    private static readonly FrozenDictionary<string, string> _hashes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Hexalith.EventStore.Contracts"] = "5680933ce8003475a3c323ddd48f299be0eeac52810424814ca6e39bab763d53",
        ["Hexalith.EventStore.ServiceDefaults"] = "860b843eb81e5e1b1e6176b8865e79a116b62182c28cf5a39230f00bfac2115d",
        ["Hexalith.EventStore.Client"] = "0bca680b2fc475720a113e05a73d328f01f7a7a37e98fb2babc94a35532e0e9c",
        ["Hexalith.EventStore.DomainService"] = "f35786f3df58263d34e60ddfa83844cb43b2e7ac345420cb901f9ef49b7e3eb8",
        ["Hexalith.Tenants.Contracts"] = "5676f656752395e149296ffbe9bc9d5e5e699e4bd6d4265df7de0f3442f05a40",
        ["Hexalith.Tenants.Server"] = "4c03251c6378d131287a02b5b3871387874afcfff6f059f8bd52f9b640e15c93",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> _fullPartiesHashes = _hashes.Concat(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Hexalith.Memories.Contracts"] = "28243237fdf46f125cb71deaa2248dc66c632976f80afeaf9fdb6f46dcedc6f7",
        ["Hexalith.Memories.EventStore"] = "325fbd74ba6d08414effeadd3df585ce258c558f432803664c39fd32fe04b1ec",
        ["Hexalith.Memories.EventStoreExtensions"] = "c423380cefd082d996e059fc9a649669eb16992b914ecc54d7d093c2265f2d25",
        ["Hexalith.Parties.Contracts"] = "b10c65c9b15aaf2b07ed1cd3eececb5d8685a62d87efa1465a777adba335eff9",
        ["Hexalith.Parties.EventStoreExtensions"] = "8713e5e44391b000a2e3cfde89eb64dbff20feadea41e3fda912a7d14a50f604",
        ["Hexalith.McpCli.Abstractions"] = "fc59fc5b5d170484fb402ef99ed5be42a78d775274a93cbbb66f9ec161df4d06",
    }).ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Checks the actual loaded SDK files; versions and informational strings alone grant no capability.</summary>
    internal static bool MatchesSdk()
    {
        try
        {
            _ = VerifySdk();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
            return false;
        }
    }

    /// <summary>Checks an actual loaded package dependency against the same finite public artifact authority.</summary>
    internal static bool Matches(string name, string cohort = Name)
    {
        try
        {
            _ = Verify(Assembly.Load(name), cohort);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
            return false;
        }
    }

    /// <summary>Verifies every actual SDK dependency before executing the owner reader and returns receipt paths.</summary>
    internal static IReadOnlyList<string> VerifySdk() => _hashes.Keys.Where(name => name.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal))
        .Order(StringComparer.Ordinal).Select(name => Verify(Assembly.Load(name))).ToArray();

    /// <summary>Checks every measured public dependency before any full-owner attribute or factory is consumed.</summary>
    internal static IReadOnlyList<string> VerifyFullParties() => _fullPartiesHashes.Keys.Order(StringComparer.Ordinal)
        .Select(name => Verify(Assembly.Load(name), FullPartiesName)).ToArray();

    /// <summary>Verifies a loaded owner or SDK artifact before reading its attributes or invoking its factory.</summary>
    internal static string Verify(Assembly assembly, string cohort = Name)
    {
        IReadOnlyDictionary<string, string> hashes = cohort switch
        {
            Name => _hashes,
            FullPartiesName => _fullPartiesHashes,
            _ => throw new InvalidDataException("The requested public artifact cohort is unavailable."),
        };
        string? name = assembly.GetName().Name;
        if (name is null || !hashes.TryGetValue(name, out string? expected) || string.IsNullOrWhiteSpace(assembly.Location)
            || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location))) != expected)
            throw new InvalidDataException("The qualified package catalog reader requires the exact signed public artifact bytes.");
        return assembly.Location;
    }
}
