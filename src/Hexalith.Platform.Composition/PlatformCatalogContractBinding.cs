namespace Hexalith.Platform.Composition;

using Hexalith.Builds.Tooling.Workspace;

/// <summary>The mapped owner artifacts used by the private reader. This contains no operation inventory.</summary>
internal sealed record PlatformCatalogContractBinding(string Assembly, string AdapterAssembly, string AppId,
    string ContractReference, string AdapterReference, string? PackageVersion)
{
    internal static IReadOnlyList<PlatformCatalogContractBinding> Resolve(PlatformCompositionOptions options, IReadOnlyList<PlatformServerBinding> servers)
    {
        List<PlatformCatalogContractBinding> result = [];
        foreach (var server in servers.Where(server => server.ServerId == "domain"))
        {
            string module = PlatformDeclarationSet.Id(server.Module.Declaration);
            string repository = PlatformServerBindings.Repository(module);
            var selected = options.Mapping.Entries.SingleOrDefault(entry => entry.Identity == repository)
                ?? throw new InvalidDataException($"{module}: mapped Contracts owner is missing.");
            string contract = repository + (module == "memories" ? ".EventStore" : ".Contracts");
            string adapter = repository + (module == "tenants" ? ".Server" : ".EventStoreExtensions");
            string? version = null;
            string Reference(string assembly)
            {
                if (options.Mapping.Mode != WorkspaceMode.Source) return assembly;
                if (selected.Origin != "source" || selected.Path is null) throw new InvalidDataException($"{module}: mapped Contracts source is unavailable.");
                string project = Path.GetFullPath(Path.Combine(selected.Path, "src", assembly, assembly + ".csproj"));
                if (!(options.Mapping with { Entries = [selected] }).ContainsProject(project))
                    throw new InvalidDataException($"{module}: Contracts project {assembly} is absent or outside its mapped owner.");
                return project;
            }
            if (options.Mapping.Mode == WorkspaceMode.Package) version = PlatformServerBindings.Version(options, module, server.Module.Source);
            result.Add(new(contract, adapter, server.AppId, Reference(contract), Reference(adapter), version));
        }
        if (result.Count == 0) throw new InvalidDataException("The selected closure has no compiled catalog owners.");
        return result;
    }
}
