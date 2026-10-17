namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using System.Diagnostics;
using Aspire.Hosting;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Microsoft.Extensions.Hosting;
using Shouldly;

/// <summary>Exercises file-entry mapping selection without depending on nested owner checkouts.</summary>
[Collection("Platform file entry")]
public sealed class PlatformFileEntryMappingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "platform-file-mapping-" + Guid.NewGuid().ToString("N"));
    private readonly string? _originalMappingPath = Environment.GetEnvironmentVariable("HEXALITH_SOURCE_MAPPING_PATH");
    private readonly string? _originalMappingHash = Environment.GetEnvironmentVariable("HEXALITH_SOURCE_MAPPING_HASH");

    /// <summary>Isolates the file entry from command-resolved mappings in the test process.</summary>
    public PlatformFileEntryMappingTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "gitconfig"), string.Empty);
        Directory.CreateDirectory(Path.Combine(_directory, "git-hooks"));
        Environment.SetEnvironmentVariable("HEXALITH_SOURCE_MAPPING_PATH", null);
        Environment.SetEnvironmentVariable("HEXALITH_SOURCE_MAPPING_HASH", null);
    }

    /// <summary>Restores the process environment and removes only disposable fixture files.</summary>
    public void Dispose()
    {
        Environment.SetEnvironmentVariable("HEXALITH_SOURCE_MAPPING_PATH", _originalMappingPath);
        Environment.SetEnvironmentVariable("HEXALITH_SOURCE_MAPPING_HASH", _originalMappingHash);
        foreach (string file in Directory.EnumerateFiles(_directory, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_directory, true);
    }

    /// <summary>Resolves sibling owners through the umbrella mapping before checking their projects.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("eventstore")]
    public async Task DefaultAnchorUsesUmbrellaOwnerMappingBeforeResourcesAsync(string? activeModule)
    {
        string workspace = await CreateUmbrellaWorkspaceAsync().ConfigureAwait(true);
        string platform = Path.Combine(workspace, "references", "Hexalith.Platform");
        SourceMapping mapping = await WorkspaceRootResolver.ResolveAsync(Path.Combine(platform, "hexalith-platform.json"), WorkspaceMode.Source, TestContext.Current.CancellationToken).ConfigureAwait(true);
        mapping.Root.ShouldBe(workspace);
        mapping.ActiveModule.ShouldBe("Hexalith.Platform");
        mapping.Entries.Single(entry => entry.Identity == "Hexalith.EventStore").Path.ShouldBe(Path.Combine(workspace, "references", "Hexalith.EventStore"));
        // This fixture has no release tag, catalog pin or accepted tool range. Source resolution
        // retains those findings; the fixture never claims package admission.
        mapping.IdentityFindings.ShouldNotBeEmpty();
        Directory.Exists(Path.Combine(platform, "references")).ShouldBeFalse();
        IDistributedApplicationBuilder builder = Builder(platform);
        builder.Configuration["Platform:ActiveModule"] = activeModule;

        InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformAppModel.ComposeEntry(builder));

        refusal.Message.ShouldContain(Path.Combine(workspace, "references", "Hexalith.EventStore", PlatformServerBindings.ReferenceProjects["eventstore/gateway"]));
        refusal.Message.ShouldContain("mapped project is missing");
        builder.Resources.ShouldBeEmpty();
        Directory.Exists(Path.Combine(platform, "references")).ShouldBeFalse();
    }

    /// <summary>Keeps an explicit declaration as the resolver anchor and validates its contents.</summary>
    [Fact]
    public void ExplicitDeclarationKeepsItsWorkspaceAnchorAndValidation()
    {
        string platform = Path.Combine(_directory, "platform");
        string owner = Path.Combine(_directory, "owner");
        Directory.CreateDirectory(platform);
        Directory.CreateDirectory(owner);
        string declaration = Path.Combine(owner, "declaration.json");
        File.WriteAllText(declaration, "{}");
        IDistributedApplicationBuilder builder = Builder(platform);
        builder.Configuration["Platform:DeclarationPath"] = declaration;
        File.Exists(Path.Combine(platform, "hexalith-platform.json")).ShouldBeFalse();

        InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder));

        refusal.Message.ShouldContain(declaration);
        refusal.Message.ShouldContain("schema: Platform enrollment requires major 2");
        builder.Resources.ShouldBeEmpty();
    }

    /// <summary>Uses a content-bound explicit mapping without probing an absent default anchor.</summary>
    [Fact]
    public void VerifiedMappingRefusesMissingOwnerByIdentityBeforeResources()
    {
        SourceMapping mapping = new(WorkspaceMode.Source, _directory, "fixture", [], false);
        IDistributedApplicationBuilder builder = Builder(_directory);
        SelectMapping(builder, mapping);
        File.Exists(Path.Combine(_directory, "hexalith-platform.json")).ShouldBeFalse();

        InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder));

        refusal.Message.ShouldBe("Hexalith.EventStore: identity.dependencies: mapped source declaration is unavailable.");
        builder.Resources.ShouldBeEmpty();
    }

    /// <summary>Rejects changed mapping bytes before any owner loading or resource creation.</summary>
    [Fact]
    public void ChangedExplicitMappingHashRefusesBeforeResources()
    {
        SourceMapping mapping = new(WorkspaceMode.Source, _directory, "fixture", [], false);
        IDistributedApplicationBuilder builder = Builder(_directory);
        string path = SelectMapping(builder, mapping);
        File.WriteAllText(path, JsonSerializer.Serialize(mapping with { ActiveModule = "changed" }));

        InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder));

        refusal.Message.ShouldContain(path);
        refusal.Message.ShouldContain("differs from the command-resolved hash");
        refusal.Message.ShouldContain(mapping.ContentHash);
        builder.Resources.ShouldBeEmpty();
    }

    private string SelectMapping(IDistributedApplicationBuilder builder, SourceMapping mapping)
    {
        string path = Path.Combine(_directory, "mapping.json");
        File.WriteAllText(path, JsonSerializer.Serialize(mapping));
        builder.Configuration["Platform:SourceMapping:Path"] = path;
        builder.Configuration["Platform:SourceMapping:Hash"] = mapping.ContentHash;
        return path;
    }

    private async Task<string> CreateUmbrellaWorkspaceAsync()
    {
        string workspace = Path.Combine(_directory, "workspace");
        Directory.CreateDirectory(workspace);
        await GitAsync(workspace, "init", "-b", "main").ConfigureAwait(true);
        string platformRoot = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(platformRoot, "apphost.cs"))) platformRoot = Path.GetDirectoryName(platformRoot)!;
        foreach (string owner in new[] { "platform", "eventstore", "tenants", "memories" })
        {
            string source = Path.Combine(_directory, owner);
            Directory.CreateDirectory(source);
            await GitAsync(source, "init", "-b", "main").ConfigureAwait(true);
            if (owner == "platform") File.WriteAllText(Path.Combine(source, "hexalith-platform.json"), "{}");
            else
            {
                Directory.CreateDirectory(Path.Combine(source, "module"));
                File.Copy(Path.Combine(platformRoot, "src", "Hexalith.Platform.Composition", "Declarations", owner + ".json"), Path.Combine(source, "module", "hexalith.module-manifest.v2.json"));
            }
            await GitAsync(source, "add", ".").ConfigureAwait(true);
            await GitAsync(source, "commit", "-m", "test: create disposable owner fixture").ConfigureAwait(true);
            string identity = owner == "platform" ? "Hexalith.Platform" : PlatformServerBindings.Repository(owner);
            await GitAsync(workspace, "-c", "protocol.file.allow=always", "submodule", "add", source, "references/" + identity).ConfigureAwait(true);
        }
        await GitAsync(workspace, "commit", "-am", "test: create disposable umbrella fixture").ConfigureAwait(true);
        return workspace;
    }

    private async Task GitAsync(string directory, params string[] arguments)
    {
        ProcessStartInfo process = CompositionProcess.CreateStartInfo("git",
            ["-c", "user.name=Platform Fixture", "-c", "user.email=platform-fixture@example.invalid", "-c", "commit.gpgsign=false", "-c", "core.hooksPath=" + Path.Combine(_directory, "git-hooks"), .. arguments],
            directory, new Dictionary<string, string>
            {
                ["GIT_CONFIG_NOSYSTEM"] = "1",
                ["GIT_CONFIG_GLOBAL"] = Path.Combine(_directory, "gitconfig"),
                ["GIT_CONFIG_COUNT"] = "0",
            });
        process.Environment.Remove("GIT_CONFIG_PARAMETERS");
        CompositionProcessResult result = await CompositionProcess.RunAsync(
            process,
            TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken).ConfigureAwait(true);
        result.ExitCode.ShouldBe(0, result.Output + result.ErrorOutput);
        result.TimedOut.ShouldBeFalse();
    }

    private static IDistributedApplicationBuilder Builder(string platform)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = [], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name,
            ProjectDirectory = platform, DisableDashboard = true, AllowUnsecuredTransport = true,
        });
        builder.Environment.EnvironmentName = Environments.Development;
        foreach (string key in new[] { "Platform:ActiveModule", "Platform:DeclarationPath", "Platform:SourceMapping:Path", "Platform:SourceMapping:Hash", "Platform:Works:Enabled", "Platform:Agents:Enabled", "Platform:Timesheets:Enabled" })
            builder.Configuration[key] = null;
        return builder;
    }
}
