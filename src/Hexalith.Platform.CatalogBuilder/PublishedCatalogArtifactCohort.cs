namespace Hexalith.Platform.CatalogArtifacts;

using System.Collections.Frozen;
using System.Reflection;
using System.Security.Cryptography;

/// <summary>The finite signed public artifacts measured for the SDK4.0.4/Tenants7 reader, distinct from image owner binaries.</summary>
internal static class PublishedCatalogArtifactCohort
{
    /// <summary>The sole measured public package cohort identifier.</summary>
    internal const string Name = "eventstore-4.0.4-tenants-7.0.0";
    private static readonly FrozenDictionary<string, string> _hashes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Hexalith.EventStore.Contracts"] = "5680933ce8003475a3c323ddd48f299be0eeac52810424814ca6e39bab763d53",
        ["Hexalith.EventStore.ServiceDefaults"] = "860b843eb81e5e1b1e6176b8865e79a116b62182c28cf5a39230f00bfac2115d",
        ["Hexalith.EventStore.Client"] = "0bca680b2fc475720a113e05a73d328f01f7a7a37e98fb2babc94a35532e0e9c",
        ["Hexalith.EventStore.DomainService"] = "f35786f3df58263d34e60ddfa83844cb43b2e7ac345420cb901f9ef49b7e3eb8",
        ["Hexalith.Tenants.Contracts"] = "5676f656752395e149296ffbe9bc9d5e5e699e4bd6d4265df7de0f3442f05a40",
        ["Hexalith.Tenants.Server"] = "4c03251c6378d131287a02b5b3871387874afcfff6f059f8bd52f9b640e15c93",
    }.ToFrozenDictionary(StringComparer.Ordinal);

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
    internal static bool Matches(string name)
    {
        try
        {
            _ = Verify(Assembly.Load(name));
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

    /// <summary>Verifies a loaded owner or SDK artifact before reading its attributes or invoking its factory.</summary>
    internal static string Verify(Assembly assembly)
    {
        string? name = assembly.GetName().Name;
        if (name is null || !_hashes.TryGetValue(name, out string? expected) || string.IsNullOrWhiteSpace(assembly.Location)
            || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location))) != expected)
            throw new InvalidDataException("The qualified package catalog reader requires the exact signed public artifact bytes.");
        return assembly.Location;
    }
}
