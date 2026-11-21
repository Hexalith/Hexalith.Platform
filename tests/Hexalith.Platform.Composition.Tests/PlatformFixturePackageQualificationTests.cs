namespace Hexalith.Platform.Composition.Tests;

using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

/// <summary>Exercises the signed fixture's separate host source and package admission boundaries.</summary>
public sealed class PlatformFixturePackageQualificationTests
{
    [Fact]
    public void CommittedGatewaySourcesAndVerifiedSeedSettingsAreRetained()
    {
        using HostWorkspace workspace = new();
        Directory.CreateDirectory(Path.Combine(workspace.Host, "bin"));
        Directory.CreateDirectory(Path.Combine(workspace.Host, "obj"));
        PlatformFixturePackageQualification.HostSources(workspace.Options).Count.ShouldBe(3);

        string shim = Path.Combine(workspace.Root, "shim");
        Directory.CreateDirectory(shim);
        File.WriteAllText(Path.Combine(shim, "appsettings.json"), "\"unverified overlay\"");
        IReadOnlyDictionary<string, string> settings = new Dictionary<string, string> { ["appsettings.json"] = "{\"Approved\":true}" };
        string content = PlatformGatewayConfiguration.Create(Path.Combine(shim, "shim.csproj"), workspace.Root, [], settings);
        File.ReadAllText(Path.Combine(content, "appsettings.json")).ShouldBe(settings["appsettings.json"]);
        File.Exists(Path.Combine(content, "appsettings.Development.json")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("extra-source")]
    [InlineData("host-import")]
    [InlineData("ancestor-import")]
    [InlineData("changed-observation")]
    [InlineData("missing-observation")]
    [InlineData("committed-project-substitution")]
    [InlineData("extra-directory")]
    public void UnapprovedCompilerInputsRefuse(string fault)
    {
        using HostWorkspace workspace = new();
        string observer = Path.Combine(workspace.Host, "SignedFixtureObservation.cs");
        switch (fault)
        {
            case "extra-source":
                File.WriteAllText(Path.Combine(workspace.Host, "Extra.cs"), "// unapproved source");
                break;
            case "host-import":
                File.WriteAllText(Path.Combine(workspace.Host, "Directory.Build.props"), "<Project />");
                break;
            case "ancestor-import":
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(workspace.Host)!, "Directory.Build.targets"), "<Project />");
                break;
            case "changed-observation":
                File.AppendAllText(observer, "\n// changed observer");
                break;
            case "missing-observation":
                File.Delete(observer);
                break;
            case "committed-project-substitution":
                File.AppendAllText(PlatformEventStoreExtensionBindings.HostProjectPath(workspace.Options), "\n<!-- replaced -->");
                PlatformPartiesProofFixture.Commit(workspace.Root);
                break;
            case "extra-directory":
                Directory.CreateDirectory(Path.Combine(workspace.Host, "Unexpected"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fault));
        }

        _ = Should.Throw<InvalidDataException>(() => PlatformFixturePackageQualification.HostSources(workspace.Options));
    }

    [Fact]
    public void DeclaredFixtureCannotBorrowTheExistingPackageCohort()
    {
        using HostWorkspace workspace = new();
        _ = Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.Bind(workspace.Options, [], [], developmentRun: true));
        _ = Should.Throw<InvalidDataException>(() => PlatformFixturePackageQualification.RequireOwners(workspace.Options));
    }

    private sealed class HostWorkspace : IDisposable
    {
        internal HostWorkspace()
        {
            string relative = Path.GetDirectoryName(PlatformEventStoreExtensionBindings.HostProject)!;
            Host = Path.Combine(Root, relative);
            Directory.CreateDirectory(Host);
            foreach (string name in new[] { "Hexalith.Platform.EventStoreHost.csproj", "Program.cs", "SignedFixtureObservation.cs" })
                File.Copy(Path.Combine(PlatformTestRepository.Root, relative, name), Path.Combine(Host, name));
            PlatformPartiesProofFixture.Git(Root, "init", "-q");
            PlatformPartiesProofFixture.Commit(Root);
            Options = new PlatformCompositionOptions
            {
                PlatformRoot = Root,
                ActiveModule = PlatformFixturePackageQualification.Module,
                Mapping = new SourceMapping(WorkspaceMode.Package, Root, "Hexalith.Platform", [], true),
            };
        }

        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "platform-fixture-host-source-" + Guid.NewGuid().ToString("N"));

        internal string Host { get; }

        internal PlatformCompositionOptions Options { get; }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
