namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.Platform.CatalogArtifacts;
using Shouldly;

/// <summary>Checks package-version selection using component catalogs and snapshots; grants no artifact or runtime authority.</summary>
public sealed class PlatformMcpCliCatalogSelectionTests
{
    /// <summary>Both purposes require the actual catalog's exact measured abstraction pin, including refusal of unreleased major three.</summary>
    [Fact]
    public void ActualCatalogPinSelectsOnlyExistingMeasuredPurposes()
    {
        using var fixture = PlatformQualificationPackageFixture.SelectedSdkTest();
        PlatformCompositionOptions options = fixture.Options with { RunPlan = null };
        string sdk = PlatformPartiesPackageFixture.SelectedSdkVersion;
        string privateCohort = sdk == "4.2.0" ? PublishedCatalogArtifactCohort.CurrentOwnerSdk420Name : PublishedCatalogArtifactCohort.CurrentOwnerName;
        string publicCohort = sdk == "4.2.0" ? PublishedCatalogArtifactCohort.PublishedPartiesSdk420Name : PublishedCatalogArtifactCohort.PublishedPartiesName;
        string catalog = Catalog(fixture.Root);
        string original = File.ReadAllText(catalog).Replace("</PropertyGroup>", "<HexalithPartiesVersion>3.1.0</HexalithPartiesVersion></PropertyGroup>", StringComparison.Ordinal);
        File.WriteAllText(catalog, original);
        PlatformPackageCatalogQualification.SelectCurrentOwnerCohort(options).ShouldBe(privateCohort);
        PlatformPackageCatalogQualification.SelectCohort(options, fullParties: true).ShouldBe(publicCohort);
        foreach (string refused in new[] { "2.0.0", "2.0.2", "2.0.1+metadata", "3.0.0", "3.0.1", "" })
        {
            File.WriteAllText(catalog, original.Replace("Version=\"2.0.1\"", "Version=\"" + refused + "\"", StringComparison.Ordinal));
            PlatformPackageCatalogQualification.SelectCurrentOwnerCohort(options).ShouldBeNull();
            PlatformPackageCatalogQualification.SelectCohort(options, fullParties: true).ShouldBeNull();
        }
        File.WriteAllText(catalog, original.Replace("<PackageVersion Include=\"Hexalith.McpCli.Abstractions\" Version=\"2.0.1\" />", "", StringComparison.Ordinal));
        PlatformPackageCatalogQualification.SelectCurrentOwnerCohort(options).ShouldBeNull();
        PlatformPackageCatalogQualification.SelectCohort(options, fullParties: true).ShouldBeNull();
        using var historical = PlatformQualificationPackageFixture.HistoricalPublic();
        PlatformCompositionOptions historicalOptions = historical.Options with { RunPlan = null };
        PlatformPackageCatalogQualification.SelectCohort(historicalOptions, fullParties: true).ShouldBe(PublishedCatalogArtifactCohort.CurrentFullPartiesName);
        string historicalCatalog = Catalog(historical.Root);
        File.WriteAllText(historicalCatalog, File.ReadAllText(historicalCatalog).Replace("2.0.0", "2.0.1", StringComparison.Ordinal));
        PlatformPackageCatalogQualification.SelectCohort(historicalOptions, fullParties: true).ShouldBeNull();
    }

    /// <summary>Physical resolution permits the canonical default assignment and refuses ambiguous, conditional or imported package inputs.</summary>
    [Fact]
    public void PhysicalPinRequiresOneUnambiguousPackageAndDefaultProperty()
    {
        using var fixture = PlatformQualificationPackageFixture.SelectedSdkTest();
        PlatformCompositionOptions options = fixture.Options with { RunPlan = null };
        string path = Catalog(fixture.Root);
        const string Item = "<PackageVersion Include=\"Hexalith.McpCli.Abstractions\" Version=\"2.0.1\" />";
        File.WriteAllText(path, "<Project><ItemGroup>" + Item + "</ItemGroup></Project>");
        PlatformMcpCliCatalogVersion.Resolve(options, "component selection").ShouldBe("2.0.1");
        const string Property = "<HexalithMcpCliVersion Condition=\"'$(HexalithMcpCliVersion)' == ''\">2.0.1</HexalithMcpCliVersion>";
        string defaultCatalog = "<Project><PropertyGroup>" + Property + "</PropertyGroup><ItemGroup>" + Item.Replace("2.0.1", "$(HexalithMcpCliVersion)", StringComparison.Ordinal) + "</ItemGroup></Project>";
        File.WriteAllText(path, defaultCatalog);
        PlatformMcpCliCatalogVersion.Resolve(options, "component selection").ShouldBe("2.0.1");
        foreach (string invalid in new[]
        {
            "<Project><ItemGroup>" + Item + Item + "</ItemGroup></Project>",
            "<Project><ItemGroup>" + Item + Item.Replace("PackageVersion", "packageversion", StringComparison.Ordinal) + "</ItemGroup></Project>",
            "<Project><ItemGroup condition=\"'$(Configuration)' == 'Release'\">" + Item + "</ItemGroup></Project>",
            defaultCatalog.Replace(Property, Property + Property.Replace("HexalithMcpCliVersion", "hexalithmcpcliversion", StringComparison.Ordinal), StringComparison.Ordinal),
            "<Project><ItemGroup>" + Item + Item.Replace("Include", "Update", StringComparison.Ordinal) + "</ItemGroup></Project>",
            "<Project><ItemGroup>" + Item.Replace(" />", " Condition=\"'$(Configuration)' == 'Release'\" />", StringComparison.Ordinal) + "</ItemGroup></Project>",
            "<Project><ItemGroup Condition=\"'$(Configuration)' == 'Release'\">" + Item + "</ItemGroup></Project>",
            "<Project><Import Project=\"other.props\" /><ItemGroup>" + Item + "</ItemGroup></Project>",
            defaultCatalog.Replace("== ''", "== 'unexpected'", StringComparison.Ordinal),
            defaultCatalog.Replace(Property, Property + Property, StringComparison.Ordinal),
            defaultCatalog.Replace("$(HexalithMcpCliVersion)", "$(OtherVersion)", StringComparison.Ordinal),
        })
        {
            File.WriteAllText(path, invalid);
            PlatformMcpCliCatalogVersion.Resolve(options, "component selection").ShouldBeNull();
        }
    }

    /// <summary>The recorded default package view wins over live bytes and refuses changed mapping or content identities.</summary>
    [Fact]
    public void RecordedPinRetainsMappingAndContentValidation()
    {
        using var fixture = PlatformQualificationPackageFixture.SelectedSdkTest();
        // This deliberately unpinned component snapshot tests its own mapping/content hash only.
        // The fixture's invented catalog commit cannot stand in for genuine pinned source bytes.
        PlatformCompositionOptions options = fixture.Options with
        {
            Mapping = fixture.Options.Mapping with { PlatformIdentity = fixture.Options.Mapping.PlatformIdentity! with { CatalogCommit = null } },
        };
        string path = Path.Combine(options.RunPlan!.Workspace, "source-mapping", "catalog-versions.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Dictionary<string, string> versions = new() { ["Hexalith.McpCli.Abstractions"] = "2.0.1" };
        Dictionary<string, string> pinnedFiles = new() { ["Directory.Packages.props"] = new string('b', 64) };
        Dictionary<string, IReadOnlyDictionary<string, string>> projects = new()
        {
            ["Hexalith.Platform.EventStoreHost"] = new Dictionary<string, string> { ["Hexalith.McpCli.Abstractions"] = "3.0.0" },
        };
        string hash = CatalogVersionSnapshot.ComputeHash(options.Mapping.ContentHash, new string('a', 40), new string('b', 64), pinnedFiles, versions, projects);
        var snapshot = new CatalogVersionSnapshot(options.Mapping.ContentHash, new string('a', 40), new string('b', 64), pinnedFiles, versions, projects, hash);
        File.WriteAllText(Catalog(fixture.Root), "<Project><ItemGroup><PackageVersion Include=\"Hexalith.McpCli.Abstractions\" Version=\"3.0.0\" /></ItemGroup></Project>");
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot));
        PlatformMcpCliCatalogVersion.Resolve(options, "recorded component selection").ShouldBe("2.0.1");
        File.WriteAllText(path, "{");
        Should.Throw<InvalidDataException>(() => PlatformMcpCliCatalogVersion.Resolve(options, "recorded component selection"));
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot with { MappingHash = "changed" }));
        Should.Throw<InvalidDataException>(() => PlatformMcpCliCatalogVersion.Resolve(options, "recorded component selection"));
        versions["Hexalith.McpCli.Abstractions"] = "3.0.0";
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot));
        Should.Throw<InvalidDataException>(() => PlatformMcpCliCatalogVersion.Resolve(options, "recorded component selection"));
        snapshot = snapshot with { Hash = CatalogVersionSnapshot.ComputeHash(snapshot.MappingHash, snapshot.CatalogCommit, snapshot.CatalogFileHash, pinnedFiles, versions, projects) };
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot));
        PlatformMcpCliCatalogVersion.Resolve(options, "recorded component selection").ShouldBe("3.0.0");
        PublishedCatalogArtifactCohort.ForCurrentOwnerVersions("4.2.0", "7.0.7", "3.0.27", PlatformMcpCliCatalogVersion.Resolve(options, "recorded component selection")).ShouldBeNull();
        PublishedCatalogArtifactCohort.ForPublishedPartiesVersions("4.2.0", "7.0.7", "3.0.27", "3.1.1", PlatformMcpCliCatalogVersion.Resolve(options, "recorded component selection")).ShouldBeNull();
        versions.Clear();
        snapshot = snapshot with { Hash = CatalogVersionSnapshot.ComputeHash(snapshot.MappingHash, snapshot.CatalogCommit, snapshot.CatalogFileHash, pinnedFiles, versions, projects) };
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot));
        PlatformMcpCliCatalogVersion.Resolve(options, "recorded component selection").ShouldBeNull();
#if NET11_0_OR_GREATER
        foreach (int generation in new[] { 4, 5, 6 })
        {
            PlatformCompositionOptions selected = options with
            {
                Mapping = options.Mapping with { PlatformIdentity = options.Mapping.PlatformIdentity! with
                {
                    ToolVersionRange = generation == 6 ? "[6.0.3,7.0.0-0)" : $"[{generation}.0.0,{generation + 1}.0.0-0)",
                    CatalogCommit = generation == 6 ? "99f7c86072ca7b35e3fbbaee4606a559a847805a" : options.Mapping.PlatformIdentity!.CatalogCommit,
                } },
            };
            PlatformDeclarationSet.DeclarationOptions(selected).ConsumerProjectName.ShouldBe(generation == 4
                ? null : Hexalith.Builds.Tooling.Runtime.CompositionDeclarationOptions.PlatformCompositionProjectName);
        }
        PlatformCompositionOptions future = options with
        {
            Mapping = options.Mapping with { PlatformIdentity = options.Mapping.PlatformIdentity! with { ToolVersionRange = "[7.0.0,8.0.0-0)" } },
        };
        Should.Throw<InvalidDataException>(() => PlatformDeclarationSet.DeclarationOptions(future));
#endif
#if NET11_0_OR_GREATER && !HEXALITH_PLATFORM_EXTENSION_SDK
        // NET11's package API selects the named view even when the loaded SDK has no extension API.
        // These deliberately unpinned component values establish selection only, never artifact authority.
        PlatformCompositionOptions current = options with
        {
            Mapping = options.Mapping with { PlatformIdentity = options.Mapping.PlatformIdentity! with { ToolVersionRange = "[5.0.0,6.0.0-0)" } },
        };
        versions["Hexalith.McpCli.Abstractions"] = "2.0.1";
        projects[Hexalith.Builds.Tooling.Runtime.CompositionDeclarationOptions.PlatformCompositionProjectName] =
            new Dictionary<string, string> { ["Hexalith.McpCli.Abstractions"] = "2.0.0" };
        snapshot = snapshot with
        {
            MappingHash = current.Mapping.ContentHash,
            Hash = CatalogVersionSnapshot.ComputeHash(current.Mapping.ContentHash, snapshot.CatalogCommit,
                snapshot.CatalogFileHash, pinnedFiles, versions, projects),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot));
        PlatformMcpCliCatalogVersion.Resolve(current, "NET11 without extension component selection").ShouldBe("2.0.0");
#endif
    }

    private static string Catalog(string root) => Path.Combine(root, "references", "Hexalith.Builds", "Props", "Directory.Packages.props");
}
