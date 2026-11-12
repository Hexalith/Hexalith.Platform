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
    /// <summary>The prior private-purpose public references remain independently byte-authenticated.</summary>
    internal const string PreviousCurrentOwnerName = "current-parties-eventstore-4.1.0-tenants-7.0.4-memories-3.0.24-mcpcli-2.0.1";
    /// <summary>The frozen Tenants7.0.4/Memory25 private reference cohort; retained only for historical byte authentication.</summary>
    internal const string Tenants704CurrentOwnerName = "current-parties-eventstore-4.1.0-tenants-7.0.4-memories-3.0.25-mcpcli-2.0.1";
    /// <summary>The private current-Parties reference cohort; confers no full public Parties authority.</summary>
    internal const string CurrentOwnerName = "current-parties-eventstore-4.1.0-tenants-7.0.5-memories-3.0.25-mcpcli-2.0.1";
    /// <summary>The separate private EventStore package-test reader cohort; confers no general launch authority.</summary>
    internal const string EventStorePackageTestName = "private-eventstore-package-test-4.1.0-tenants-7.0.5";
    /// <summary>The complete authenticated public Parties3.1 reader; independent from every private purpose.</summary>
    internal const string PublishedPartiesName = "parties-3.1.0-eventstore-4.1.0-tenants-7.0.5-memories-3.0.25-mcpcli-2.0.1";
    /// <summary>The independently measured SDK4.2 public reader; released owner images retain their original provenance.</summary>
    internal const string PublishedPartiesSdk420Name = "parties-3.1.0-eventstore-4.2.0-tenants-7.0.5-memories-3.0.25-mcpcli-2.0.1";
    /// <summary>The separately authenticated SDK4.2 private current-owner references; never full public owner authority.</summary>
    internal const string CurrentOwnerSdk420Name = "current-parties-eventstore-4.2.0-tenants-7.0.5-memories-3.0.25-mcpcli-2.0.1";
    /// <summary>The SDK4.2 private EventStore package-test purpose; never general launch authority.</summary>
    internal const string EventStorePackageTestSdk420Name = "private-eventstore-package-test-4.2.0-tenants-7.0.5";
    /// <summary>The independently authenticated current public reader; carries no image or native runtime authority.</summary>
    internal const string PublishedPartiesCurrentName = "parties-3.1.1-eventstore-4.2.0-tenants-7.0.7-memories-3.0.26-mcpcli-2.0.1";
    /// <summary>The independently authenticated current private references; carries no image or native runtime authority.</summary>
    internal const string CurrentOwnerCurrentName = "current-parties-eventstore-4.2.0-tenants-7.0.7-memories-3.0.26-mcpcli-2.0.1";
    /// <summary>The separately authenticated Memories3.0.27 public reader; carries no image or native runtime authority.</summary>
    internal const string PublishedPartiesMemories327Name = "parties-3.1.1-eventstore-4.2.0-tenants-7.0.7-memories-3.0.27-mcpcli-2.0.1";
    /// <summary>The separately authenticated Memories3.0.27 private references; carries no image or native runtime authority.</summary>
    internal const string CurrentOwnerMemories327Name = "current-parties-eventstore-4.2.0-tenants-7.0.7-memories-3.0.27-mcpcli-2.0.1";
    /// <summary>The current private EventStore reader purpose; carries no image or native runtime authority.</summary>
    internal const string EventStorePackageTestCurrentName = "private-eventstore-package-test-4.2.0-tenants-7.0.7";
    /// <summary>The signed extension SDK bodies only; confers no owner or composed-host cohort admission.</summary>
    internal const string ExtensionSdkName = "extension-sdk-eventstore-4.2.0";
    private static readonly FrozenDictionary<string, string> _extensionSdkHashes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Hexalith.EventStore.Admin.Abstractions"] = "9b1edf00cc5e31ace475c851587876a4dc0321fab9d1e2af0aa771b114cfb584",
        ["Hexalith.EventStore.Contracts"] = "7e4c8823479a6c59456c5169c95d9082ccbb5d199f403f7cb7c3267c9cf9a44a",
        ["Hexalith.EventStore.ServiceDefaults"] = "3b618387d1adc3b98cef9823d629edace174c2bc9a8272a501e979f972fcad12",
        ["Hexalith.EventStore.Client"] = "5b184b6c7bc7e83251d4f48edfb6ee446a28ac4fae7c04c381c388ed8cb9cd92",
        ["Hexalith.EventStore.DomainService"] = "d1593c5a2b93e0a6cb8d84d946a94223403bf38c02bb7e30987f1f188ff13646",
        ["Hexalith.EventStore.Server"] = "95038655c275192a4971ea8f74a1a3748be15793736ed5914813614b84cf6cf1",
        ["Hexalith.EventStore.Gateway"] = "a7038a4e7858aca19c78cf2489fbffd5bd78925b956a96f2473d854127ed0b46",
        ["Hexalith.EventStore.Aspire"] = "a68d402265448ae6fb75cd7a8422b4e8581c606aee215914f52a989e758e472a",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The exact canonical SDK4.2 private test identity, retained for the additive extension controls.</summary>
    internal const string Sdk42EventStorePackageTestName = EventStorePackageTestSdk420Name;
    /// <summary>The exact canonical SDK4.2 source-owner identity, retained for the additive extension controls.</summary>
    internal const string Sdk42CurrentOwnerName = CurrentOwnerSdk420Name;
    /// <summary>The exact canonical SDK4.2 public reader identity, retained for the additive extension controls.</summary>
    internal const string Sdk42PublishedPartiesName = PublishedPartiesSdk420Name;

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

    private static readonly FrozenDictionary<string, string> _eventStorePackageTestHashes = _currentHashes
        .Where(pair => !pair.Key.StartsWith("Hexalith.Tenants.", StringComparison.Ordinal))
        .Concat(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Hexalith.Tenants.Contracts"] = "adedcc49d81f2ceb6855f365dddb05e8f1f550e303c5f0e92c4d3153ff672d45",
            ["Hexalith.Tenants.Server"] = "e84f4b22485c5db4866636558488e387132369583483b0334839c1bcf66b95ad",
        }).ToFrozenDictionary(StringComparer.Ordinal);

    // M23, P3 and Hexalith.McpCli2.0 remain the same authenticated public owners in both SDK/Tenants cohorts.
    private static readonly FrozenDictionary<string, string> _currentFullPartiesHashes = _currentHashes
        .Concat(_fullPartiesHashes.Where(pair => !_hashes.ContainsKey(pair.Key))).ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> _previousCurrentOwnerHashes = _currentFullPartiesHashes
        .Where(pair => !pair.Key.StartsWith("Hexalith.Parties.", StringComparison.Ordinal) && !pair.Key.StartsWith("Hexalith.Tenants.", StringComparison.Ordinal)
            && !pair.Key.StartsWith("Hexalith.Memories.", StringComparison.Ordinal) && pair.Key != "Hexalith.McpCli.Abstractions")
        .Concat(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Hexalith.Tenants.Contracts"] = "6758c6691127f0f480d1a84860de621bd610f60d7ef1b6c5fe9e5a768283f983",
            ["Hexalith.Tenants.Server"] = "44187842f060b1c7bd8a4cff3bf7a3c69c1eaebb0aae9267b61a401cc41d27a3",
            ["Hexalith.Memories.Contracts"] = "e64ecb7787da0b81644e3d644bb768f7c06c036079a4439206fb71fe840819dc",
            ["Hexalith.Memories.EventStore"] = "f0762fd414e7d8b317f02668739212c9c68266addf24f77d3b93383b9c8d304d",
            ["Hexalith.Memories.EventStoreExtensions"] = "920d206be7f0428cd7b32ceed8c06dea68907cf0a9075790901eff10e796c6aa",
            ["Hexalith.McpCli.Abstractions"] = "aeb390050b08451827b147a958ba355e1e5c96de526834221645028b85c26c2e",
        }).ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> _tenants704CurrentOwnerHashes = _previousCurrentOwnerHashes
        .Where(pair => !pair.Key.StartsWith("Hexalith.Memories.", StringComparison.Ordinal))
        .Concat(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Hexalith.Memories.Contracts"] = "44b629fb573f0fcb161129c0e353dc5efdf72447550fa46352d773275a2954ca",
            ["Hexalith.Memories.EventStore"] = "b496cd070564f0377f5c01e7436326f1753fdb1790847dce719e6d875fa1daf4",
            ["Hexalith.Memories.EventStoreExtensions"] = "333b469a1a40cc35bf6104e41ff49e84d6f9d4acd1e2a8e15b7cae8b4ebffcdb",
        }).ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> _currentOwnerHashes = _tenants704CurrentOwnerHashes
        .Where(pair => !pair.Key.StartsWith("Hexalith.Tenants.", StringComparison.Ordinal))
        .Concat(_eventStorePackageTestHashes.Where(pair => pair.Key.StartsWith("Hexalith.Tenants.", StringComparison.Ordinal)))
        .ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> _publishedPartiesHashes = _currentOwnerHashes
        .Concat(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Hexalith.Parties.Contracts"] = "99230b764de6769a803702170c174f9dbef9b0b5b0c2931b07e85045478cd654",
            ["Hexalith.Parties.EventStoreExtensions"] = "a48612597603ce5b8d743e28e91236a51e561369c6bcd2d9fcca092dd78f1c06",
        }).ToFrozenDictionary(StringComparer.Ordinal);

    // Only the four SDK reader bodies change. The independently signed public owner packages remain byte-identical.
    private static readonly FrozenDictionary<string, string> _publishedPartiesSdk420Hashes = _publishedPartiesHashes
        .Where(pair => !pair.Key.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal))
        .Concat(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Hexalith.EventStore.Contracts"] = "7e4c8823479a6c59456c5169c95d9082ccbb5d199f403f7cb7c3267c9cf9a44a",
            ["Hexalith.EventStore.ServiceDefaults"] = "3b618387d1adc3b98cef9823d629edace174c2bc9a8272a501e979f972fcad12",
            ["Hexalith.EventStore.Client"] = "5b184b6c7bc7e83251d4f48edfb6ee446a28ac4fae7c04c381c388ed8cb9cd92",
            ["Hexalith.EventStore.DomainService"] = "d1593c5a2b93e0a6cb8d84d946a94223403bf38c02bb7e30987f1f188ff13646",
        }).ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> _currentOwnerSdk420Hashes = _publishedPartiesSdk420Hashes
        .Where(pair => !pair.Key.StartsWith("Hexalith.Parties.", StringComparison.Ordinal)).ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> _eventStorePackageTestSdk420Hashes = _currentOwnerSdk420Hashes
        .Where(pair => pair.Key.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal) || pair.Key.StartsWith("Hexalith.Tenants.", StringComparison.Ordinal))
        .ToFrozenDictionary(StringComparer.Ordinal);

    // Independently repository-signed package measurements; all retained historical dictionaries stay unchanged.
    private static readonly FrozenDictionary<string, string> _publishedPartiesCurrentHashes = _publishedPartiesSdk420Hashes
        .Where(pair => !pair.Key.StartsWith("Hexalith.Tenants.", StringComparison.Ordinal)
            && !pair.Key.StartsWith("Hexalith.Memories.", StringComparison.Ordinal) && !pair.Key.StartsWith("Hexalith.Parties.", StringComparison.Ordinal))
        .Concat(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Hexalith.Tenants.Contracts"] = "680d324c2371838c40d6e9446476cbcb0b10074b860a8ee79114b054290c8624",
            ["Hexalith.Tenants.Server"] = "1e180b869b693ab50c5522959f52b6b8f1f5637102d18fc9df1f3fa9768ca68d",
            ["Hexalith.Memories.Contracts"] = "20787624b423232e58819b05dfaff31e36447241d7f885c3b9429ccf155064c0",
            ["Hexalith.Memories.EventStore"] = "f0e6d172f4e9e02793d8e253ea942c08a527d7adb60048a2a9471f70759debaa",
            ["Hexalith.Memories.EventStoreExtensions"] = "4edaa9d9b18db8bc3738ed22238e8764131ba00698839c9249bceaa753e65f80",
            ["Hexalith.Parties.Contracts"] = "3de20e490c7e1f42209d067caae4749369cf2c22ff982d29d698d97bf838cab5",
            ["Hexalith.Parties.EventStoreExtensions"] = "d5e8963120f881ca0b812d3f69b023a00e3676dd8e289160b2b870f8d36b4b55",
        }).ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> _currentOwnerCurrentHashes = _publishedPartiesCurrentHashes
        .Where(pair => !pair.Key.StartsWith("Hexalith.Parties.", StringComparison.Ordinal)).ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> _publishedPartiesMemories327Hashes = _publishedPartiesCurrentHashes
        .Where(pair => !pair.Key.StartsWith("Hexalith.Memories.", StringComparison.Ordinal))
        .Concat(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Hexalith.Memories.Contracts"] = "212eb1ee03fc4359c8fbcbe6a4199822e93cab1cc0774e3a1ca318a054cfc7b3",
            ["Hexalith.Memories.EventStore"] = "c0f85c6d3d96f72afe9fd628727df5195cdc710cd826d7e919b4db422850ab13",
            ["Hexalith.Memories.EventStoreExtensions"] = "76a461cc91d71dbf3b1d14874348eed1f03f196f25edef6eef9edf8dba63e42c",
        }).ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> _currentOwnerMemories327Hashes = _publishedPartiesMemories327Hashes
        .Where(pair => !pair.Key.StartsWith("Hexalith.Parties.", StringComparison.Ordinal)).ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> _eventStorePackageTestCurrentHashes = _currentOwnerCurrentHashes
        .Where(pair => pair.Key.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal) || pair.Key.StartsWith("Hexalith.Tenants.", StringComparison.Ordinal))
        .ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Selects only the exact independently measured private reference tuples.</summary>
    internal static string? ForCurrentOwnerVersions(string sdk, string tenants, string memories) => (sdk, tenants, memories) switch
    {
        ("4.1.0", "7.0.5", "3.0.25") => CurrentOwnerName,
        ("4.2.0", "7.0.5", "3.0.25") => CurrentOwnerSdk420Name,
        ("4.2.0", "7.0.7", "3.0.26") => CurrentOwnerCurrentName,
        ("4.2.0", "7.0.7", "3.0.27") => CurrentOwnerMemories327Name,
        _ => null,
    };

    /// <summary>Selects only exact independently measured private EventStore package readers.</summary>
    internal static string? ForEventStorePackageTestVersions(string sdk, string tenants) => (sdk, tenants) switch
    {
        ("4.1.0", "7.0.5") => EventStorePackageTestName,
        ("4.2.0", "7.0.5") => EventStorePackageTestSdk420Name,
        ("4.2.0", "7.0.7") => EventStorePackageTestCurrentName,
        _ => null,
    };

    /// <summary>Recognizes only the finite private reference authorities.</summary>
    internal static bool IsCurrentOwnerCohort(string? cohort) => cohort is CurrentOwnerName or CurrentOwnerSdk420Name or CurrentOwnerCurrentName or CurrentOwnerMemories327Name;

    /// <summary>Returns the SDK bound to the exact private reference authority.</summary>
    internal static string CurrentOwnerSdkVersion(string cohort) => cohort switch
    {
        CurrentOwnerName => "4.1.0",
        CurrentOwnerSdk420Name or CurrentOwnerCurrentName or CurrentOwnerMemories327Name => "4.2.0",
        _ => throw new InvalidDataException("The requested private current-owner cohort is unavailable."),
    };

    /// <summary>Returns the exact tenant dependency for a private reference cohort.</summary>
    internal static string CurrentOwnerTenantsVersion(string cohort) => cohort switch
    {
        CurrentOwnerName or CurrentOwnerSdk420Name => "7.0.5",
        CurrentOwnerCurrentName or CurrentOwnerMemories327Name => "7.0.7",
        _ => throw new InvalidDataException("The requested private current-owner cohort is unavailable."),
    };

    /// <summary>Returns the exact Memories dependency for a private reference cohort.</summary>
    internal static string CurrentOwnerMemoriesVersion(string cohort) => cohort switch
    {
        CurrentOwnerName or CurrentOwnerSdk420Name => "3.0.25",
        CurrentOwnerCurrentName => "3.0.26",
        CurrentOwnerMemories327Name => "3.0.27",
        _ => throw new InvalidDataException("The requested private current-owner cohort is unavailable."),
    };

    /// <summary>Selects only complete measured public Parties3.1 readers, without granting a general SDK/Tenants pair.</summary>
    internal static string? ForPublishedPartiesVersions(string sdk, string tenants, string memories, string parties)
        => (sdk, tenants, memories, parties) switch
        {
            ("4.1.0", "7.0.5", "3.0.25", "3.1.0") => PublishedPartiesName,
            ("4.2.0", "7.0.5", "3.0.25", "3.1.0") => PublishedPartiesSdk420Name,
            ("4.2.0", "7.0.7", "3.0.26", "3.1.1") => PublishedPartiesCurrentName,
            ("4.2.0", "7.0.7", "3.0.27", "3.1.1") => PublishedPartiesMemories327Name,
            _ => null,
        };

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
    internal static IReadOnlyList<string> VerifyFullParties(string cohort = FullPartiesName) => cohort is FullPartiesName or CurrentFullPartiesName or PublishedPartiesName or PublishedPartiesSdk420Name or PublishedPartiesCurrentName or PublishedPartiesMemories327Name
        ? Hashes(cohort).Keys.Order(StringComparer.Ordinal).Select(name => Verify(Assembly.Load(name), cohort)).ToArray()
        : throw new InvalidDataException("The requested full public artifact cohort is unavailable.");

    internal static IReadOnlyList<string> VerifyCurrentReferences(string cohort = CurrentOwnerName) => CurrentReferenceHashes(cohort).Keys.Order(StringComparer.Ordinal).Select(name => Verify(Assembly.Load(name), cohort)).ToArray();

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

    internal static void VerifyCurrentSdkFile(string path, string digest, string cohort = CurrentOwnerName)
    {
        string name = AssemblyName.GetAssemblyName(path).Name!;
        if (!name.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal) || !CurrentReferenceHashes(cohort).TryGetValue(name, out string? expected)
            || digest != expected || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) != expected)
            throw new InvalidDataException("The current owner must load the exact public SDK bodies.");
    }

    internal static void VerifyCurrentReferenceReceipt(IReadOnlyList<(string Path, string Hash)> artifacts, string cohort = CurrentOwnerName)
    {
        foreach (var expected in CurrentReferenceHashes(cohort))
        {
            var matches = artifacts.Where(item => AssemblyName.GetAssemblyName(item.Path).Name == expected.Key).ToArray();
            if (matches.Length != 1 || matches[0].Hash != expected.Value
                || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(matches[0].Path))) != expected.Value)
                throw new InvalidDataException("The current reader receipt differs from the immutable public reference cohort.");
        }
    }

    private static IReadOnlyDictionary<string, string> CurrentReferenceHashes(string cohort) => IsCurrentOwnerCohort(cohort)
        ? Hashes(cohort) : throw new InvalidDataException("The requested private current-owner cohort is unavailable.");

    private static IReadOnlyDictionary<string, string> Hashes(string cohort) => cohort switch
    {
        Name => _hashes,
        FullPartiesName => _fullPartiesHashes,
        CurrentName => _currentHashes,
        CurrentFullPartiesName => _currentFullPartiesHashes,
        PreviousCurrentOwnerName => _previousCurrentOwnerHashes,
        Tenants704CurrentOwnerName => _tenants704CurrentOwnerHashes,
        CurrentOwnerName => _currentOwnerHashes,
        CurrentOwnerSdk420Name => _currentOwnerSdk420Hashes,
        PublishedPartiesName => _publishedPartiesHashes,
        PublishedPartiesSdk420Name => _publishedPartiesSdk420Hashes,
        EventStorePackageTestName => _eventStorePackageTestHashes,
        EventStorePackageTestSdk420Name => _eventStorePackageTestSdk420Hashes,
        ExtensionSdkName => _extensionSdkHashes,
        PublishedPartiesCurrentName => _publishedPartiesCurrentHashes,
        CurrentOwnerCurrentName => _currentOwnerCurrentHashes,
        PublishedPartiesMemories327Name => _publishedPartiesMemories327Hashes,
        CurrentOwnerMemories327Name => _currentOwnerMemories327Hashes,
        EventStorePackageTestCurrentName => _eventStorePackageTestCurrentHashes,
        _ => throw new InvalidDataException("The requested public artifact cohort is unavailable."),
    };
}
