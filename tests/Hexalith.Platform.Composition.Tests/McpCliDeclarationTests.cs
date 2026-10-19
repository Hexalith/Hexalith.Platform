namespace Hexalith.Platform.Composition.Tests;

using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

public sealed class McpCliDeclarationTests
{
    [Fact]
    public async Task ActualMcpCliDeclarationValidatesAndSelectsOnlyTheEventStoreGatewayAsync()
    {
        PlatformServerBindings.Repository("mcpcli").ShouldBe("Hexalith.McpCli");
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "apphost.cs"))) directory = directory.Parent;
        string root = directory!.FullName;
        string manifest = Path.Combine(root, "references", PlatformServerBindings.Repository("mcpcli"), "module", "hexalith.module-manifest.v2.json");

        PlatformManifestValidationResult validation = PlatformManifestValidator.Validate([manifest], TestContext.Current.CancellationToken);
        validation.IsValid.ShouldBeTrue(string.Join("; ", validation.Diagnostics.Select(diagnostic => $"{diagnostic.RuleId} {diagnostic.Field}: {diagnostic.Message}")));

        SourceMapping mapping = await WorkspaceRootResolver.ResolveAsync(manifest, WorkspaceMode.Source, TestContext.Current.CancellationToken);
        IReadOnlyList<PlatformServerBinding> servers = PlatformAppModel.Prepare(new PlatformCompositionOptions { PlatformRoot = root, Mapping = mapping, ActiveModule = "mcpcli", DeclarationPaths = [manifest] });
        servers.Select(server => server.ResourceId).ShouldBe(["eventstore"]);
    }
}
