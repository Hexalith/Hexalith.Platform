namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;

using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Workspace;

using Shouldly;

/// <summary>Distinguishes native test control source from package runtime catalog inputs.</summary>
public sealed class PlatformCatalogContractBindingTests
{
    /// <summary>Uses owner projects only when the command selected source mode.</summary>
    /// <param name="module">The enrolled catalog owner.</param>
    /// <param name="mode">The single command-selected mapping mode.</param>
    [Theory]
    [InlineData("tenants", WorkspaceMode.Source)]
    [InlineData("tenants", WorkspaceMode.Package)]
    [InlineData("parties", WorkspaceMode.Source)]
    [InlineData("parties", WorkspaceMode.Package)]
    [InlineData("memories", WorkspaceMode.Source)]
    [InlineData("memories", WorkspaceMode.Package)]
    public void ActiveControlSourceDoesNotReplacePublishedPackageInputs(string module, WorkspaceMode mode)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "catalog-control-source-" + Guid.NewGuid().ToString("N"));
        string repository = PlatformServerBindings.Repository(module);
        string owner = Path.Combine(workspace, repository);
        string contract = repository + (module == "memories" ? ".EventStore" : ".Contracts");
        string adapter = repository + (module == "tenants" ? ".Server" : ".EventStoreExtensions");
        const string version = "9.8.7";
        try
        {
            foreach (string assembly in new[] { contract, adapter })
            {
                string directory = Path.Combine(owner, "src", assembly);
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, assembly + ".csproj"),
                    """<Project><Target Name="SourceControlMustNotBuild" BeforeTargets="Build"><Error Text="Native control source is not a package runtime input." /></Target></Project>""");
            }

            string catalog = Path.Combine(workspace, "references", "Hexalith.Builds", "Props");
            Directory.CreateDirectory(catalog);
            File.WriteAllText(Path.Combine(catalog, "Directory.Packages.props"),
                "<Project><PropertyGroup><Hexalith" + repository["Hexalith.".Length..] + "Version>" + version
                + "</Hexalith" + repository["Hexalith.".Length..] + "Version></PropertyGroup></Project>");
            var mapping = new SourceMapping(mode, workspace, repository,
                [new SourceMappingEntry(repository, "source", owner)], false);
            string mappingHash = mapping.ContentHash;
            var options = new PlatformCompositionOptions { PlatformRoot = workspace, Mapping = mapping };
            var declaration = new PlatformModuleDeclaration(Path.Combine(owner, "module.json"),
                JsonSerializer.SerializeToElement(new { identity = new { moduleId = module } }));
            var server = new PlatformServerBinding(declaration, "domain", module, module,
                null, "registry.example/" + module + ":" + version, "/ready", new Dictionary<string, string>(), []);

            PlatformCatalogContractBinding binding = PlatformCatalogContractBinding.Resolve(options, [server]).Single();

            if (mode == WorkspaceMode.Package)
            {
                binding.ContractReference.ShouldBe(contract);
                binding.AdapterReference.ShouldBe(adapter);
                binding.PackageVersion.ShouldBe(version);
                Path.IsPathFullyQualified(binding.ContractReference).ShouldBeFalse();
                Path.IsPathFullyQualified(binding.AdapterReference).ShouldBeFalse();
            }
            else
            {
                binding.ContractReference.ShouldBe(Path.Combine(owner, "src", contract, contract + ".csproj"));
                binding.AdapterReference.ShouldBe(Path.Combine(owner, "src", adapter, adapter + ".csproj"));
                binding.PackageVersion.ShouldBeNull();
            }

            options.Mapping.ShouldBeSameAs(mapping);
            options.Mapping.ContentHash.ShouldBe(mappingHash);
        }
        finally
        {
            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, recursive: true);
            }
        }
    }
}
