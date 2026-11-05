namespace Hexalith.Platform.CatalogArtifacts;

using System.Collections.Frozen;
using System.Reflection;
using System.Security.Cryptography;

/// <summary>The closed signed public reader cohorts, measured separately from released image owner binaries.</summary>
internal static class PublishedCatalogArtifactCohort
{
    /// <summary>The historical SDK4.0.4/Tenants7.0.0 public package cohort identifier.</summary>
    internal const string Name = "eventstore-4.0.4-tenants-7.0.0";
    /// <summary>The independently measured complete ordinary-Parties public reader cohort.</summary>
    internal const string FullPartiesName = "parties-3.0.0-eventstore-4.0.4-tenants-7.0.0-memories-3.0.23";
    /// <summary>The independently authenticated SDK4.1.0/Tenants7.0.2 public reader cohort.</summary>
    internal const string CurrentName = "eventstore-4.1.0-tenants-7.0.2";
    /// <summary>The complete ordinary-Parties public reader with the newly selected SDK/Tenants pair.</summary>
    internal const string CurrentFullPartiesName = "parties-3.0.0-eventstore-4.1.0-tenants-7.0.2-memories-3.0.23";
    internal const string CurrentOwnerName = "current-parties-eventstore-4.1.0-tenants-7.0.4-memories-3.0.23";
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

    private static readonly FrozenDictionary<string, string> _currentHashes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Hexalith.EventStore.Contracts"] = "f3f2d7f6e7edf54e187af15edec8b4f13bf7e255b186b999efa0836a0c86a739",
        ["Hexalith.EventStore.ServiceDefaults"] = "0e3f88ffefb12327d53f865b96bbb774beb71f7c78c9f20a3cbefa4901e9ba2d",
        ["Hexalith.EventStore.Client"] = "702dfb7537e990a2065fe37c0b06301cdb70c26907af42faa6acdae331a7bce6",
        ["Hexalith.EventStore.DomainService"] = "5d8068c8c116551efc3d545f85d41040023fa38217e5b4e73e4d225fe8e8bf78",
        ["Hexalith.Tenants.Contracts"] = "4f07e5529c43f60859c4372fa718dd339c4e18dd0b81db301abe0e4a4e665df9",
        ["Hexalith.Tenants.Server"] = "70eca75b01bc7cc8dc056b6babe97529fffec4f78ce81fecc58fbdb1c482003b",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // M23, P3 and Hexalith.McpCli2.0 remain the same authenticated public owners in both SDK/Tenants cohorts.
    private static readonly FrozenDictionary<string, string> _currentFullPartiesHashes = _currentHashes
        .Concat(_fullPartiesHashes.Where(pair => !_hashes.ContainsKey(pair.Key))).ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> _currentOwnerHashes = _currentFullPartiesHashes
        .Where(pair => !pair.Key.StartsWith("Hexalith.Parties.", StringComparison.Ordinal) && !pair.Key.StartsWith("Hexalith.Tenants.", StringComparison.Ordinal))
        .Concat(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Hexalith.Tenants.Contracts"] = "6758c6691127f0f480d1a84860de621bd610f60d7ef1b6c5fe9e5a768283f983",
            ["Hexalith.Tenants.Server"] = "44187842f060b1c7bd8a4cff3bf7a3c69c1eaebb0aae9267b61a401cc41d27a3",
        }).ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Selects only complete measured version pairs; unknown, mixed and near-match pairs carry no authority.</summary>
    internal static string? ForVersions(string sdk, string tenants, bool fullParties = false) => (sdk, tenants, fullParties) switch
    {
        ("4.0.4", "7.0.0", false) => Name,
        ("4.0.4", "7.0.0", true) => FullPartiesName,
        ("4.1.0", "7.0.2", false) => CurrentName,
        ("4.1.0", "7.0.2", true) => CurrentFullPartiesName,
        _ => null,
    };

    /// <summary>Checks the actual loaded SDK files; versions and informational strings alone grant no capability.</summary>
    internal static bool MatchesSdk(string cohort = Name)
    {
        try
        {
            _ = VerifySdk(cohort);
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or BadImageFormatException)
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
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or BadImageFormatException)
        {
            return false;
        }
    }

    /// <summary>Verifies every actual SDK dependency before executing the owner reader and returns receipt paths.</summary>
    internal static IReadOnlyList<string> VerifySdk(string cohort = Name) => Hashes(cohort).Keys.Where(name => name.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal))
        .Order(StringComparer.Ordinal).Select(name => Verify(Assembly.Load(name), cohort)).ToArray();

    /// <summary>Checks every measured public dependency before any full-owner attribute or factory is consumed.</summary>
    internal static IReadOnlyList<string> VerifyFullParties(string cohort = FullPartiesName) => cohort is FullPartiesName or CurrentFullPartiesName
        ? Hashes(cohort).Keys.Order(StringComparer.Ordinal).Select(name => Verify(Assembly.Load(name), cohort)).ToArray()
        : throw new InvalidDataException("The requested full public artifact cohort is unavailable.");

    internal static IReadOnlyList<string> VerifyCurrentReferences() => _currentOwnerHashes.Keys.Order(StringComparer.Ordinal).Select(name => Verify(Assembly.Load(name), CurrentOwnerName)).ToArray();

    /// <summary>Verifies a loaded owner or SDK artifact before reading its attributes or invoking its factory.</summary>
    internal static string Verify(Assembly assembly, string cohort = Name)
    {
        IReadOnlyDictionary<string, string> hashes = Hashes(cohort);
        string? name = assembly.GetName().Name;
        if (name is null || !hashes.TryGetValue(name, out string? expected) || string.IsNullOrWhiteSpace(assembly.Location)
            || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location))) != expected)
            throw new InvalidDataException("The qualified package catalog reader requires the exact signed public artifact bytes.");
        return assembly.Location;
    }

    internal static void VerifyCurrentSdkFile(string path, string digest)
    {
        string name = AssemblyName.GetAssemblyName(path).Name!;
        if (!name.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal) || !_currentOwnerHashes.TryGetValue(name, out string? expected)
            || digest != expected || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) != expected)
            throw new InvalidDataException("The current owner must load the exact public SDK bodies.");
    }

    internal static void VerifyCurrentReferenceReceipt(IReadOnlyList<(string Path, string Hash)> artifacts)
    {
        foreach (var expected in _currentOwnerHashes)
        {
            var matches = artifacts.Where(item => AssemblyName.GetAssemblyName(item.Path).Name == expected.Key).ToArray();
            if (matches.Length != 1 || matches[0].Hash != expected.Value
                || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(matches[0].Path))) != expected.Value)
                throw new InvalidDataException("The current reader receipt differs from the immutable public reference cohort.");
        }
    }

    private static IReadOnlyDictionary<string, string> Hashes(string cohort) => cohort switch
    {
        Name => _hashes,
        FullPartiesName => _fullPartiesHashes,
        CurrentName => _currentHashes,
        CurrentFullPartiesName => _currentFullPartiesHashes,
        CurrentOwnerName => _currentOwnerHashes,
        _ => throw new InvalidDataException("The requested public artifact cohort is unavailable."),
    };
}
