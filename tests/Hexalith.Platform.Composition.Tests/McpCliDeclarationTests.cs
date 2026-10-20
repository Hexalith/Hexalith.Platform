namespace Hexalith.Platform.Composition.Tests;

using System.Reflection.Metadata;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Runtime;
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

        string workspace = Path.Combine(Path.GetTempPath(), "platform-mcpcli-symbols-" + Guid.NewGuid().ToString("N"));
        try
        {
            CompositionWorkspace.CreatePrivateDirectory(workspace);
            await SourceMappingMaterializer.WriteAsync(mapping, workspace, TestContext.Current.CancellationToken);
            string tool = await PlatformManifestCommand.BuildMcpCliAsync(mapping, workspace, null, TestContext.Current.CancellationToken);
            File.Exists(tool).ShouldBeTrue();
            string checkout = mapping.Entries.Single(entry => entry.Identity == "Hexalith.McpCli").Path!;
            foreach (string assembly in new[] { "Hexalith.McpCli", "Hexalith.McpCli.Core", "Hexalith.McpCli.Abstractions", "Hexalith.McpCli.Mcp" })
            {
                string pdb = Path.Combine(Path.GetDirectoryName(tool)!, assembly + ".pdb");
                File.Exists(pdb).ShouldBeTrue(pdb);
                using FileStream stream = File.OpenRead(pdb);
                using MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbStream(stream);
                MetadataReader reader = provider.GetMetadataReader();
                string sources = Path.Combine(checkout, "src", assembly) + Path.DirectorySeparatorChar;
                Dictionary<DocumentHandle, int> documents = reader.Documents
                    .Where(handle => reader.GetString(reader.GetDocument(handle).Name).StartsWith(sources, StringComparison.Ordinal))
                    .ToDictionary(handle => handle, handle => File.ReadAllLines(reader.GetString(reader.GetDocument(handle).Name)).Length);
                documents.ShouldNotBeEmpty($"{pdb} must name sources in {sources}");
                reader.MethodDebugInformation.Any(handle => reader.GetMethodDebugInformation(handle).GetSequencePoints().Any(point =>
                    !point.IsHidden && documents.TryGetValue(point.Document, out int lines)
                    && point.StartLine > 0 && point.EndLine >= point.StartLine && point.EndLine <= lines
                    && point.StartColumn > 0 && (point.EndLine > point.StartLine || point.EndColumn > point.StartColumn)))
                    .ShouldBeTrue($"{pdb} must contain usable sequence points in the mapped checkout sources");
            }
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        }
    }
}
