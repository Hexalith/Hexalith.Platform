#if !HEXALITH_PLATFORM_SOURCE_CATALOG
namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Platform.CatalogArtifacts;
using Shouldly;

/// <summary>Owner package admission through the generated lock: the I/O matrix of the 2.14 lock slice, with genuine package bodies.</summary>
/// <remarks>Composition checks each loaded owner package against the lock and derives the expected DLL identity from the lock-verified archive (AR-12).</remarks>
public sealed class PlatformOwnerPackageLockTests
{
    // Genuine nuget.org packageHash values of a later owner release; these test inputs grant nothing to the committed lock.
    private const string _newReleaseLock = """
        {
          "schemaVersion": 1,
          "packages": [
            { "id": "Hexalith.EventStore.Client", "version": "5.2.0", "contentHash": "/75VQNzzvd09DkbD6jlLzBCpLB4o/N9u+b4DTRi2NcPjNGvQXCWYXpi+QLL8JjP+g4D0HAiFcdSeaKfd/uYfpw==" },
            { "id": "Hexalith.EventStore.Contracts", "version": "5.2.0", "contentHash": "Ziuou6o21o7ppvdSY/2qAffVOLEcg4Ew1QgjE8BfOvK4smPzVPKReWTAwv8QhLm5624x6wwT+IoDBdLcrU3SCg==" },
            { "id": "Hexalith.Tenants.Contracts", "version": "8.0.1", "contentHash": "Y/CEYTJxwG5wI4P0VfDHi3ecMDunkDCj+840biy5ujuZBu1FoK9hLXxrz+hEO2bD0xv3aA/zZGZWS2tDnNWRZg==" },
            { "id": "Hexalith.Tenants.Server", "version": "8.0.1", "contentHash": "x1umR5hN7sQ+Xcv9N9a/vAsdF7BPW0qHaoTKxwet/c7Q+fhgxl9e/NRKZWrGdl/l1PSwl2MiLRbeDNBUaaCinQ==" }
          ]
        }
        """;

    private static readonly string[] _readerBodies = ["Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults", "Hexalith.EventStore.Client",
        "Hexalith.EventStore.DomainService", "Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server", "Hexalith.Memories.Contracts", "Hexalith.Memories.EventStore",
        "Hexalith.Memories.EventStoreExtensions", "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions", "Hexalith.McpCli.Abstractions"];

    /// <summary>Current pins: the catalog selection is admitted and the evidence lists lock id, version, contentHash, member and loaded SHA-256.</summary>
    [Fact]
    public void CurrentPinsAreAdmittedWithExpectedAndLoadedIdentities()
    {
        using PlatformQualificationPackageFixture fixture = PlatformQualificationPackageFixture.CurrentTest();
        var declarations = PlatformDeclarationSet.Load(fixture.Options);
        var servers = PlatformServerBindings.Resolve(fixture.Options, declarations);
        OwnerPackageLock owners = PlatformPackageCatalogQualification.ValidateTestInputs(fixture.Options, declarations, servers, developmentRun: true);
        owners.Path.ShouldBe(OwnerPackageLock.PathFor(fixture.Root));
        IReadOnlyList<OwnerPackageIdentity> identities = PlatformPackageCatalogQualification.VerifyPackageBytes(fixture.Options, owners);
        string[] selected = ["Hexalith.EventStore.Client", "Hexalith.EventStore.Contracts", "Hexalith.EventStore.DomainService", "Hexalith.EventStore.ServiceDefaults",
             "Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server"];
        selected.ShouldAllBe(name => identities.Any(identity => identity.Assembly == name));
        // Every owner package assembly in the default load context is checked too, not only the selected contracts.
        AssemblyLoadContext.Default.Assemblies.Where(assembly => !assembly.IsDynamic && OwnerPackageLock.IsOwnerPackage(assembly.GetName().Name))
            .ShouldAllBe(assembly => identities.Any(identity => identity.LoadedPath == assembly.Location));
        AssertEvidence(owners, identities);
        foreach (OwnerPackageIdentity identity in identities.Where(identity => selected.Contains(identity.Assembly)))
        {
            identity.Version.ShouldBe(PlatformServerBindings.Version(fixture.Options, identity.PackageId.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal) ? "eventstore" : "tenants", "lock evidence"));
            identity.LoadedPath.ShouldBe(Assembly.Load(identity.Assembly).Location);
        }

        // Every reader body that the current pins select verifies under the same committed lock, with no version tuple.
        AssertEvidence(PlatformPartiesPackageFixture.Lock, PlatformPartiesPackageFixture.Lock.VerifyLoaded(_readerBodies, PlatformPartiesPackageFixture.PackageFolders));
        PlatformPackageCatalogQualification.Bind(fixture.Options, declarations, servers, developmentRun: true).ShouldAllBe(server => server.ImageDigest == null);
        Directory.Exists(fixture.Plan.Workspace).ShouldBeFalse();
    }

    /// <summary>New release: real later owner packages with lock entries are admitted through the reader's own code path, with no code edit.</summary>
    [Fact]
    public void NewOwnerReleaseWithLockEntriesIsAdmittedWithoutCodeChanges()
    {
        OwnerPackageLock owners = OwnerPackageLock.Parse(Encoding.UTF8.GetBytes(_newReleaseLock), "new-release.lock.json");
        string directory = Path.Combine(Path.GetTempPath(), "owner-lock-new-release-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            JsonObject targets = [];
            JsonObject libraries = [];
            foreach (OwnerPackageLockEntry entry in owners.Entries)
            {
                string member = "lib/net11.0/" + entry.Id + ".dll";
                File.Copy(Path.Combine(PackageRoot(entry.Id, entry.Version), member), Path.Combine(directory, entry.Id + ".dll"));
                targets[entry.Id + "/" + entry.Version] = new JsonObject { ["runtime"] = new JsonObject { [member] = new JsonObject() } };
                libraries[entry.Id + "/" + entry.Version] = new JsonObject { ["type"] = "package", ["path"] = entry.Id.ToLowerInvariant() + "/" + entry.Version };
            }

            using JsonDocument graph = JsonDocument.Parse(new JsonObject
            {
                ["targets"] = new JsonObject { [".NETCoreApp,Version=v11.0"] = targets }, ["libraries"] = libraries,
            }.ToJsonString());
            IReadOnlyList<OwnerPackageIdentity> admitted = owners.VerifyDirectory(graph.RootElement, PlatformPartiesPackageFixture.PackageFolders, directory, new HashSet<string>());
            admitted.Select(identity => identity.PackageId + "/" + identity.Version).ShouldBe(
                ["Hexalith.EventStore.Client/5.2.0", "Hexalith.EventStore.Contracts/5.2.0", "Hexalith.Tenants.Contracts/8.0.1", "Hexalith.Tenants.Server/8.0.1"]);
            AssertEvidence(owners, admitted);
            // The version rule admits the release by its catalog selection; the committed lock names other versions and refuses it.
            owners.RequireFamily("Hexalith.Tenants.", "8.0.1");
            owners.RequireFamily("Hexalith.EventStore.", "5.2.0");
            string lockedTenants = PlatformPartiesPackageFixture.LockedOwnerVersion("tenants");
            Should.Throw<InvalidDataException>(() => PlatformPartiesPackageFixture.Lock.Require("Hexalith.Tenants.Contracts", "8.0.1"))
                .Message.ShouldContain($"names Hexalith.Tenants.Contracts {lockedTenants}, but 8.0.1 is selected");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>Unknown version: a catalog version without a lock entry refuses before any run effect, naming the package and version.</summary>
    [Fact]
    public void CatalogVersionWithoutALockEntryRefusesNamingThePackage()
    {
        using PlatformQualificationPackageFixture fixture = PlatformQualificationPackageFixture.CurrentTest();
        string tenants = PlatformServerBindings.Version(fixture.Options, "tenants", "lock control");
        RewriteLock(fixture.Root, packages => packages.Where(entry => !entry!["id"]!.GetValue<string>().StartsWith("Hexalith.Tenants.", StringComparison.Ordinal)));
        Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.RequireLockedPackages(fixture.Options))
            .Message.ShouldContain("no entry for Hexalith.Tenants.Contracts " + tenants);
        var declarations = PlatformDeclarationSet.Load(fixture.Options);
        Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.Bind(fixture.Options, declarations,
            PlatformServerBindings.Resolve(fixture.Options, declarations), developmentRun: true));
        File.Delete(OwnerPackageLock.PathFor(fixture.Root));
        Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.RequireLockedPackages(fixture.Options)).Message.ShouldContain("missing or unreadable");
        Directory.Exists(fixture.Plan.Workspace).ShouldBeFalse();
    }

    /// <summary>Stale lock: an entry naming another version refuses; the genuine historical declared graph is refused the same way.</summary>
    [Fact]
    public void LockEntryNamingAnotherVersionRefuses()
    {
        using PlatformQualificationPackageFixture fixture = PlatformQualificationPackageFixture.CurrentTest();
        string tenants = PlatformServerBindings.Version(fixture.Options, "tenants", "lock control");
        RewriteLock(fixture.Root, packages => packages.Select(entry => entry!["id"]!.GetValue<string>() == "Hexalith.Tenants.Server"
            ? new JsonObject { ["id"] = "Hexalith.Tenants.Server", ["version"] = "7.0.5", ["contentHash"] = entry["contentHash"]!.GetValue<string>() } : entry!.DeepClone()));
        Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.RequireLockedPackages(fixture.Options))
            .Message.ShouldContain("names Hexalith.Tenants.Server 7.0.5, but " + tenants + " is selected");
        using PlatformQualificationPackageFixture historical = PlatformQualificationPackageFixture.HistoricalPublic();
        string sdk = PlatformServerBindings.Version(historical.Options, "eventstore", "historical lock control");
        sdk.ShouldNotBe(PlatformPartiesPackageFixture.SelectedSdkVersion);
        var declarations = PlatformDeclarationSet.Load(historical.Options);
        Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.ValidateTestInputs(historical.Options, declarations,
            PlatformServerBindings.Resolve(historical.Options, declarations), developmentRun: true)).Message.ShouldContain(", but " + sdk + " is selected");
    }

    /// <summary>Composition's own admission reaches the archive check: a lock contentHash that differs refuses <c>Bind</c> before any effect.</summary>
    [Fact]
    public void BindRefusesALoadedPackageWhoseArchiveDiffersFromTheLock()
    {
        using PlatformQualificationPackageFixture fixture = PlatformQualificationPackageFixture.CurrentTest();
        var declarations = PlatformDeclarationSet.Load(fixture.Options);
        var servers = PlatformServerBindings.Resolve(fixture.Options, declarations);
        RewriteLock(fixture.Root, packages => packages.Select(entry => entry!["id"]!.GetValue<string>() == "Hexalith.EventStore.Contracts"
            ? new JsonObject { ["id"] = "Hexalith.EventStore.Contracts", ["version"] = entry["version"]!.GetValue<string>(), ["contentHash"] = Convert.ToBase64String(new byte[64]) }
            : entry!.DeepClone()));
        Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.Bind(fixture.Options, declarations, servers, developmentRun: true))
            .Message.ShouldContain("Hexalith.EventStore.Contracts " + PlatformServerBindings.Version(fixture.Options, "eventstore", "lock control") + " archive has contentHash");
        Directory.Exists(fixture.Plan.Workspace).ShouldBeFalse();
    }

    /// <summary>Published Parties bodies are locked outside the integration profile; run-built Parties bodies of that profile are not.</summary>
    /// <param name="profile">The selected Parties package profile.</param>
    [Theory]
    [InlineData("lifecycle-package")]
    [InlineData(PlatformCurrentOwnerProof.Profile)]
    public async Task PublishedPartiesBodiesAreLockedOnlyOutsideTheIntegrationProfileAsync(string profile)
    {
        using PlatformPartiesPackageFixture fixture = await PlatformPartiesPackageFixture.CreateFromCandidateAsync(profile);
        CompositionRunPlan plan = fixture.Plan;
        PlatformCompositionOptions options = new()
        {
            PlatformRoot = plan.SourceMapping!.PlatformIdentity!.PlatformPath, Mapping = plan.SourceMapping, ActiveModule = "parties",
            DeclarationPaths = plan.PlatformDeclarationPaths, RunPlan = plan, EventStoreSeedProject = fixture.Seed,
        };
        bool integration = profile == PlatformCurrentOwnerProof.Profile;
        PlatformCurrentOwnerProof.SelectsIntegration(options).ShouldBe(integration);
        OwnerPackageLock owners = PlatformPackageCatalogQualification.RequireLockedPackages(options);
        IReadOnlyList<OwnerPackageIdentity> identities = PlatformPackageCatalogQualification.VerifyPackageBytes(options, owners);
        AssertEvidence(owners, identities);
        identities.Any(identity => identity.Assembly == "Hexalith.Parties.Contracts").ShouldBe(!integration);
        if (integration) identities.ShouldNotContain(identity => identity.Assembly.StartsWith("Hexalith.Parties.", StringComparison.Ordinal));
        string parties = PlatformServerBindings.Version(options, "parties", "lock control");
        JsonObject document = JsonNode.Parse(File.ReadAllText(owners.Path))!.AsObject();
        foreach (JsonNode? entry in document["packages"]!.AsArray())
            if (entry!["id"]!.GetValue<string>() == "Hexalith.Parties.Contracts") entry["version"] = "3.1.0";
        OwnerPackageLock stale = OwnerPackageLock.Parse(Encoding.UTF8.GetBytes(document.ToJsonString()), "stale-parties.lock.json");
        if (integration) PlatformPackageCatalogQualification.RequireLockedPackages(options, stale).ShouldBeSameAs(stale);
        else
            Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.RequireLockedPackages(options, stale))
                .Message.ShouldContain("names Hexalith.Parties.Contracts 3.1.0, but " + parties + " is selected");
    }

    /// <summary>Tampered archive: package bytes that differ from the lock contentHash refuse, naming the package.</summary>
    /// <param name="name">The genuine owner package to tamper in an isolated package folder.</param>
    [Theory]
    [InlineData("Hexalith.EventStore.Contracts")]
    [InlineData("Hexalith.Tenants.Server")]
    [InlineData("Hexalith.Parties.Contracts")]
    public void TamperedArchiveRefusesNamingThePackage(string name)
    {
        OwnerPackageLock owners = PlatformPartiesPackageFixture.Lock;
        OwnerPackageAsset asset = LoadedAsset(name);
        string folder = Path.Combine(Path.GetTempPath(), "owner-lock-tampered-" + Guid.NewGuid().ToString("N"));
        try
        {
            string archive = Path.Combine(folder, asset.LibraryPath, asset.PackageId.ToLowerInvariant() + "." + asset.Version + ".nupkg");
            Directory.CreateDirectory(Path.GetDirectoryName(archive)!);
            byte[] original = File.ReadAllBytes(owners.Verify(asset, PlatformPartiesPackageFixture.PackageFolders, Assembly.Load(name).Location).Archive);
            File.WriteAllBytes(archive, [.. original, 0]);
            Should.Throw<InvalidDataException>(() => owners.Verify(asset, [folder, .. PlatformPartiesPackageFixture.PackageFolders], Assembly.Load(name).Location))
                .Message.ShouldContain("owner package " + name + " " + asset.Version + " archive has contentHash");
            Should.Throw<InvalidDataException>(() => owners.Verify(asset, [Path.Combine(folder, "absent")], Assembly.Load(name).Location))
                .Message.ShouldContain(name + " " + asset.Version + " archive is absent");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
    }

    /// <summary>Substituted DLL: a loaded body that differs from its archive member refuses, naming the assembly, for every reader body.</summary>
    /// <param name="name">The genuine body to alter in an isolated copy.</param>
    [Theory]
    [InlineData("Hexalith.EventStore.Contracts")]
    [InlineData("Hexalith.EventStore.ServiceDefaults")]
    [InlineData("Hexalith.EventStore.Client")]
    [InlineData("Hexalith.EventStore.DomainService")]
    [InlineData("Hexalith.Tenants.Contracts")]
    [InlineData("Hexalith.Tenants.Server")]
    [InlineData("Hexalith.Memories.Contracts")]
    [InlineData("Hexalith.Memories.EventStore")]
    [InlineData("Hexalith.Memories.EventStoreExtensions")]
    [InlineData("Hexalith.Parties.Contracts")]
    [InlineData("Hexalith.Parties.EventStoreExtensions")]
    [InlineData("Hexalith.McpCli.Abstractions")]
    public void SubstitutedLoadedBodyRefusesNamingTheAssembly(string name)
    {
        OwnerPackageLock owners = PlatformPartiesPackageFixture.Lock;
        string source = Assembly.Load(name).Location;
        byte[] original = File.ReadAllBytes(source);
        string directory = Path.Combine(Path.GetTempPath(), "owner-lock-substituted-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var context = new AssemblyLoadContext("owner-lock-substituted-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            string path = Path.Combine(directory, name + ".dll");
            File.WriteAllBytes(path, [.. original, 0]);
            Assembly changed = context.LoadFromAssemblyPath(path);
            changed.GetName().Name.ShouldBe(name, "The appended body remains a loadable assembly of the same name.");
            Should.Throw<InvalidDataException>(() => owners.Verify(LoadedAsset(name), PlatformPartiesPackageFixture.PackageFolders, changed.Location))
                .Message.ShouldContain("loaded assembly '" + name + "' differs from its lock-verified package member");
        }
        finally { context.Unload(); Directory.Delete(directory, recursive: true); }
        File.ReadAllBytes(source).ShouldBe(original);
    }

    /// <summary>The released OCI image body of Parties Contracts is genuine for its image role but is not the package member, so it refuses.</summary>
    [Fact]
    public void PublishedImageOwnerBodyCannotBecomeAPackageReaderBody()
    {
        string directory = Path.Combine(Path.GetTempPath(), "owner-lock-image-role-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "Hexalith.Parties.Contracts.dll");
            using (Stream source = typeof(PlatformOwnerPackageLockTests).Assembly.GetManifestResourceStream("Hexalith.Platform.Composition.Tests.Fixtures.public-parties310-image-contracts.dll")!)
            using (FileStream target = File.Create(path)) source.CopyTo(target);
            AssemblyName.GetAssemblyName(path).Name.ShouldBe("Hexalith.Parties.Contracts");
            Should.Throw<InvalidDataException>(() => PlatformPartiesPackageFixture.Lock.Verify(LoadedAsset("Hexalith.Parties.Contracts"), PlatformPartiesPackageFixture.PackageFolders, path))
                .Message.ShouldContain("'Hexalith.Parties.Contracts' differs from its lock-verified package member");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>Assemblies outside every owner package carry no package identity and cannot be admitted.</summary>
    [Fact]
    public void ForeignAssembliesHaveNoOwnerPackageIdentity()
    {
        OwnerPackageLock owners = PlatformPartiesPackageFixture.Lock;
        foreach (string name in new[] { typeof(PlatformOwnerPackageLockTests).Assembly.GetName().Name!, typeof(PlatformAppModel).Assembly.GetName().Name! })
            Should.Throw<InvalidDataException>(() => owners.VerifyLoaded([name], PlatformPartiesPackageFixture.PackageFolders)).Message.ShouldContain("no owner package identity");
        Should.Throw<InvalidDataException>(() => owners.VerifyLoaded(["Hexalith.Tenants.Absent"], PlatformPartiesPackageFixture.PackageFolders)).Message.ShouldContain("not loadable");
        OwnerPackageLock.IsOwnerPackage("Hexalith.Platform.Composition").ShouldBeFalse();
        OwnerPackageLock.IsOwnerPackage("Hexalith.Commons.Abstractions").ShouldBeFalse();
    }

    /// <summary>Reader receipts must carry one complete AR-12 record per loaded owner body, under the same lock, with unchanged bytes.</summary>
    [Fact]
    public void ReaderReceiptRequiresCompleteUnchangedLockEvidence()
    {
        OwnerPackageLock owners = PlatformPartiesPackageFixture.Lock;
        IReadOnlyList<OwnerPackageIdentity> identities = owners.VerifyLoaded(_readerBodies, PlatformPartiesPackageFixture.PackageFolders);
        JsonObject receipt = Receipt(owners, identities);
        owners.VerifyReceipt(JsonSerializer.SerializeToElement(receipt), new HashSet<string>()).Count.ShouldBe(_readerBodies.Length);
        var runBound = new HashSet<string>(["Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions"], StringComparer.Ordinal);
        JsonObject withoutParties = Receipt(owners, identities.Where(identity => !runBound.Contains(identity.Assembly)).ToArray(), identities);
        owners.VerifyReceipt(JsonSerializer.SerializeToElement(withoutParties), runBound).Count.ShouldBe(_readerBodies.Length - 2);
        Should.Throw<InvalidDataException>(() => owners.VerifyReceipt(JsonSerializer.SerializeToElement(withoutParties), new HashSet<string>()));
        string directory = Path.Combine(Path.GetTempPath(), "owner-lock-receipt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string changed = Path.Combine(directory, "Hexalith.Tenants.Contracts.dll");
            File.WriteAllBytes(changed, [.. File.ReadAllBytes(Assembly.Load("Hexalith.Tenants.Contracts").Location), 0]);
            foreach (string fault in new[] { "absent", "duplicate", "foreign-lock", "content-hash", "expected", "changed-bytes", "no-records", "stale-version" })
            {
                JsonObject mutated = Receipt(owners, identities);
                JsonArray records = mutated["ownerPackages"]!.AsArray();
                JsonObject first = records.Select(record => record!.AsObject()).Single(record => record["assembly"]!.GetValue<string>() == "Hexalith.Tenants.Contracts");
                switch (fault)
                {
                    case "absent": records.Remove(first); break;
                    case "duplicate": records.Add(first.DeepClone()); break;
                    case "foreign-lock": mutated["ownerPackageLock"]!["sha256"] = new string('0', 64); break;
                    case "content-hash": first["contentHash"] = Convert.ToBase64String(new byte[64]); break;
                    case "expected": first["expectedSha256"] = new string('0', 64); break;
                    case "changed-bytes": first["loadedPath"] = changed; break;
                    case "no-records": records.Clear(); mutated["loadedArtifacts"] = new JsonArray(); break;
                    case "stale-version": first["version"] = "7.0.5"; break;
                }

                Should.Throw<InvalidDataException>(() => owners.VerifyReceipt(JsonSerializer.SerializeToElement(mutated), new HashSet<string>()), fault);
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>The C# reader applies the generator's schema: malformed, foreign or ambiguous locks refuse before any package is read.</summary>
    [Fact]
    public void MalformedLocksRefuse()
    {
        OwnerPackageLock committed = PlatformPartiesPackageFixture.Lock;
        committed.Entries.ShouldNotBeEmpty();
        committed.Entries.ShouldAllBe(entry => OwnerPackageLock.IsOwnerPackage(entry.Id));
        committed.Sha256.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(committed.Path))));
        string valid = "{\"id\":\"Hexalith.Tenants.Contracts\",\"version\":\"7.0.7\",\"contentHash\":\"" + Convert.ToBase64String(new byte[64]) + "\"}";
        foreach (string document in new[]
        {
            "{", "[]", "{\"schemaVersion\":2,\"packages\":[" + valid + "]}", "{\"schemaVersion\":1,\"packages\":[" + valid + "],\"extra\":1}",
            "{\"schemaVersion\":1,\"packages\":[]}", "{\"schemaVersion\":1,\"packages\":{}}", "{\"schemaVersion\":1,\"packages\":[" + valid + "," + valid + "]}",
            "{\"schemaVersion\":1,\"packages\":[" + valid.Replace("Hexalith.Tenants.Contracts", "Newtonsoft.Json", StringComparison.Ordinal) + "]}",
            "{\"schemaVersion\":1,\"packages\":[" + valid.Replace("\"7.0.7\"", "\"\"", StringComparison.Ordinal) + "]}",
            "{\"schemaVersion\":1,\"packages\":[" + valid.Replace(Convert.ToBase64String(new byte[64]), "not base64!", StringComparison.Ordinal) + "]}",
            "{\"schemaVersion\":1,\"packages\":[" + valid.Replace(Convert.ToBase64String(new byte[64]), Convert.ToBase64String(new byte[32]), StringComparison.Ordinal) + "]}",
            "{\"schemaVersion\":1,\"packages\":[" + valid.Replace("}", ",\"sha256\":\"a\"}", StringComparison.Ordinal) + "]}",
        })
            Should.Throw<InvalidDataException>(() => OwnerPackageLock.Parse(Encoding.UTF8.GetBytes(document), "malformed.lock.json"), document);
        Should.Throw<InvalidDataException>(() => OwnerPackageLock.Load(Path.Combine(Path.GetTempPath(), "absent-" + Guid.NewGuid().ToString("N") + ".json")));
    }

    private static void AssertEvidence(OwnerPackageLock owners, IReadOnlyList<OwnerPackageIdentity> identities)
    {
        identities.ShouldNotBeEmpty();
        foreach (OwnerPackageIdentity identity in identities)
        {
            OwnerPackageLockEntry entry = owners.Require(identity.PackageId, identity.Version);
            identity.ContentHash.ShouldBe(entry.ContentHash);
            Convert.ToBase64String(SHA512.HashData(File.ReadAllBytes(identity.Archive))).ShouldBe(entry.ContentHash);
            identity.ArchiveMember.ShouldStartWith("lib/");
            identity.ArchiveMember.ShouldEndWith("/" + identity.Assembly + ".dll");
            identity.ExpectedSha256.ShouldBe(identity.LoadedSha256);
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(identity.LoadedPath))).ShouldBe(identity.LoadedSha256);
        }
    }

    private static OwnerPackageAsset LoadedAsset(string name)
    {
        string location = Assembly.Load(name).Location;
        using JsonDocument graph = JsonDocument.Parse(File.ReadAllBytes(Path.ChangeExtension(typeof(PlatformOwnerPackageLockTests).Assembly.Location, ".deps.json")));
        OwnerPackageAsset asset = OwnerPackageLock.ReadAssets(graph.RootElement)[name];
        Path.GetFileName(asset.Member).ShouldBe(Path.GetFileName(location));
        return asset;
    }

    private static JsonObject Receipt(OwnerPackageLock owners, IReadOnlyList<OwnerPackageIdentity> records, IReadOnlyList<OwnerPackageIdentity>? loaded = null)
        => new()
        {
            ["ownerPackageLock"] = new JsonObject { ["path"] = owners.Path, ["sha256"] = owners.Sha256 },
            ["ownerPackages"] = new JsonArray(records.Select(identity => (JsonNode)new JsonObject
            {
                ["assembly"] = identity.Assembly, ["packageId"] = identity.PackageId, ["version"] = identity.Version, ["contentHash"] = identity.ContentHash,
                ["archive"] = identity.Archive, ["archiveMember"] = identity.ArchiveMember, ["expectedSha256"] = identity.ExpectedSha256,
                ["loadedPath"] = identity.LoadedPath, ["loadedSha256"] = identity.LoadedSha256,
            }).ToArray()),
            ["loadedArtifacts"] = new JsonArray((loaded ?? records).Select(identity => (JsonNode)new JsonObject
            {
                ["path"] = identity.LoadedPath, ["sha256"] = identity.LoadedSha256, ["assemblyIdentity"] = AssemblyName.GetAssemblyName(identity.LoadedPath).FullName,
            }).ToArray()),
        };

    private static void RewriteLock(string root, Func<IEnumerable<JsonNode?>, IEnumerable<JsonNode?>> change)
    {
        string path = OwnerPackageLock.PathFor(root);
        JsonObject document = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        document["packages"] = new JsonArray(change(document["packages"]!.AsArray()).Select(entry => entry?.DeepClone()).ToArray());
        File.WriteAllText(path, document.ToJsonString());
        _ = OwnerPackageLock.Load(path);
    }

    private static string PackageRoot(string id, string version)
    {
        string root = PlatformPartiesPackageFixture.PackageFolders.Select(folder => Path.Combine(folder, id.ToLowerInvariant(), version)).FirstOrDefault(Directory.Exists)
            ?? throw new InvalidDataException("The genuine " + id + " " + version + " package is a required restore input; there is no synthetic fallback.");
        return root;
    }
}
#endif
