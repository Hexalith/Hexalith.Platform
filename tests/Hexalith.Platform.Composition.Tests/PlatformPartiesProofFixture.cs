namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

/// <summary>Real local Git repositories and a measured declaration for proof-only controls; never a published tuple or CI identity.</summary>
internal sealed class PlatformPartiesProofFixture : IDisposable
{
    /// <summary>Creates clean local histories and private parent handoff files without invoking a service or granting cohort authority.</summary>
    /// <param name="platformOwned">Whether the actual owner is a direct Git child of the outer Platform root, with a package-mode Builds entry.</param>
    internal PlatformPartiesProofFixture(bool platformOwned = false)
    {
        Root = Path.Combine(Path.GetTempPath(), "platform-parties-proof-" + Guid.NewGuid().ToString("N"));
        string active = platformOwned ? Path.Combine(Root, "platform", "references", "Hexalith.Parties") : Path.Combine(Root, "parties");
        string platform = Path.Combine(Root, "platform");
        string builds = platformOwned ? Path.Combine(platform, "references", "Hexalith.Builds") : Path.Combine(Root, "builds");
        string workspace = Path.Combine(Root, "run");
        foreach (string root in new[] { active, platform, builds }) Directory.CreateDirectory(root);
        string declaration = Path.Combine(active, "module", "hexalith.module-manifest.v2.json");
        Directory.CreateDirectory(Path.GetDirectoryName(declaration)!);
        using (Stream source = typeof(PlatformPartiesProofFixture).Assembly.GetManifestResourceStream("Hexalith.Platform.Composition.Tests.Fixtures.parties-active-declaration.json")!)
        using (FileStream target = File.Create(declaration)) source.CopyTo(target);
        Hash(declaration).ShouldBe(PlatformPartiesPackageCatalogQualification.ActiveDeclarationHash);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(declaration));
        JsonElement suite = document.RootElement.GetProperty("modules")[0].GetProperty("tests")
            .EnumerateArray().Single(test => test.GetProperty("name").GetString() == "platform-package");
        string entry = suite.GetProperty("entry").GetString()!;
        string project = Path.Combine(active, entry);
        Directory.CreateDirectory(Path.GetDirectoryName(project)!);
        File.WriteAllText(project, "<Project />");
        File.WriteAllText(Path.Combine(active, "Native.cs"), "// Explicit synthetic native source for clean revision-drift control.\n");
        string record = Path.Combine(platform, "hexalith-platform.json");
        File.WriteAllText(record, "{}");
        string catalog = Path.Combine(builds, "Props", "Directory.Packages.props");
        Directory.CreateDirectory(Path.GetDirectoryName(catalog)!);
        File.WriteAllText(catalog, "<Project />");
        foreach (string root in new[] { active, builds, platform })
        {
            Git(root, "init", "-q");
            Commit(root);
        }
        SourceMapping mapping = new(WorkspaceMode.Package, platformOwned ? platform : active, "Hexalith.Parties",
            [new("Hexalith.Parties", "source", active), platformOwned ? new("Hexalith.Builds", "package", null) : new("Hexalith.Builds", "source", builds)], true)
        {
            PlatformIdentity = new PlatformIdentity(Git(platform, "rev-parse", "HEAD"), Git(platform, "rev-parse", "HEAD"),
                Git(platform, "rev-parse", "HEAD"), platform, null, Git(builds, "rev-parse", "HEAD"), "[4.44.0,5.0.0-0)", record, true),
        };
        string privateMapping = Path.Combine(workspace, "source-mapping");
        Directory.CreateDirectory(Path.Combine(privateMapping, "pinned-tree", "Props"));
        File.WriteAllText(Path.Combine(privateMapping, "mapping.json"), JsonSerializer.Serialize(mapping));
        File.WriteAllText(Path.Combine(privateMapping, "catalog-versions.json"), "{}");
        File.Copy(catalog, Path.Combine(privateMapping, "pinned-tree", "Props", "Directory.Packages.props"));
        string seed = Path.Combine(Root, "seed.csproj");
        File.WriteAllText(seed, "<Project />");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ModuleTestSelection selected = new("parties", "platform-package", suite.GetProperty("executor").GetString()!, entry,
            Hash(declaration), Hash(project), 300, 90, now, now.AddSeconds(300), now.AddSeconds(300), "package");
        CompositionRunPlan plan = CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), workspace, Path.Combine(Root, "dapr"),
            new CompositionRunPorts(20001, 20002, 20003, 20004, 20005, 20006, 20007, 20008, 20009, 20010, 20011, 20012, 20013, [20014], 20015, 20016, 20017, [new(20018, 20019, 20020, 20021)]), [new CompositionRunModule("parties", "parties", "parties", project)], []) with
        {
            SourceMapping = mapping, PlatformActiveModule = "parties", PlatformDeclarationPaths = [declaration], PlatformDeclarationHash = Hash(declaration),
            PlatformModelAssemblyPath = typeof(PlatformAppModel).Assembly.Location, SelectedTest = selected, SelectedTests = [selected],
        };
        CompositionDocumentStore.WriteAsync(Path.Combine(workspace, "plan.json"), plan, CancellationToken.None).GetAwaiter().GetResult();
        Key = CompositionSigningKey.Create();
        Options = new PlatformCompositionOptions
        {
            PlatformRoot = platform, Mapping = mapping, ActiveModule = "parties", DeclarationPaths = [declaration], EventStoreSeedProject = seed, RunPlan = plan,
        };
        Options = Options with { PartiesRunProof = PlatformPartiesRunProof.Create(Options, Key) };
    }

    /// <summary>Gets the parent directory of this disposable proof-only fixture.</summary>
    internal string Root { get; }
    /// <summary>Gets the locally issued secret, never retained in diagnostic output.</summary>
    internal string Key { get; }
    /// <summary>Gets proof-only options; the fixture has no admitted public catalog or released image authority.</summary>
    internal PlatformCompositionOptions Options { get; }

    /// <summary>Records an actual clean local revision without rewriting history.</summary>
    internal static void Commit(string root)
    {
        Git(root, "add", ".");
        Git(root, "-c", "user.name=Local proof fixture", "-c", "user.email=fixture@invalid", "-c", "commit.gpgsign=false", "commit", "-q", "-m", "test(proof): record local boundary inputs");
    }

    /// <summary>Executes real local Git and requires a successful bounded result.</summary>
    internal static string Git(string root, params string[] arguments)
    {
        var result = CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("git", arguments, root, null), TimeSpan.FromSeconds(10), CancellationToken.None).GetAwaiter().GetResult();
        result.ExitCode.ShouldBe(0, result.ErrorOutput);
        result.TimedOut.ShouldBeFalse();
        return result.Output.Trim();
    }

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(Root, true);
}
