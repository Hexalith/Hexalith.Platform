namespace Hexalith.Platform.Composition;

using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;

/// <summary>Produces the local Parties reader and owner outputs before any native run is issued.</summary>
internal static class PlatformNativeBuildProducer
{
    /// <summary>Runs the tool-selected private preparation entry point without composing resources.</summary>
    internal static int Run(string planPath)
    {
        try
        {
            if (!Path.IsPathFullyQualified(planPath)) throw PlatformPreparedNativeBuild.Refusal();
            CompositionRunPlan plan = CompositionDocumentStore.TryReadAsync<CompositionRunPlan>(planPath, CancellationToken.None)
                .GetAwaiter().GetResult() ?? throw PlatformPreparedNativeBuild.Refusal();
            if (planPath != Path.Combine(plan.Workspace, "plan.json") || plan.SelectedTest is not { ModuleId: "parties", Mode: "package" } selection
                || plan.SelectedTests.Count != 1 || plan.SelectedTests[0] != selection || plan.PreparedNativeBuild is not null
                || plan.SourceMapping is not { Mode: WorkspaceMode.Package } mapping || plan.PlatformIdentity is not { } platform)
                throw PlatformPreparedNativeBuild.Refusal();

            string seed = Path.Combine(mapping.EffectiveBuildsRoot, "src", "hosts", "Hexalith.Builds.Module.EventStoreHost", "Hexalith.Builds.Module.EventStoreHost.csproj");
            string signingKey = CompositionSigningKey.Create();
            PlatformCompositionOptions options = new()
            {
                PlatformRoot = platform.PlatformPath, Mapping = mapping, RunPlan = plan, DeclarationPaths = plan.PlatformDeclarationPaths,
                ActiveModule = plan.PlatformActiveModule, Configuration = PlatformAppModel.RunnerConfiguration(signingKey, complete: false), EventStoreSeedProject = seed,
            };
            options = options with { PartiesRunProof = PlatformPartiesRunProof.Create(options, signingKey) };
            _ = PlatformPackageCatalogQualification.RequireLockedPackages(options);
            var declarations = PlatformDeclarationSet.Load(options);
            var servers = PlatformServerBindings.Resolve(options, declarations);
            PlatformServerBinding gateway = servers.Single(server => server.ResourceId == "eventstore");
            if (gateway.EventStoreHost is not null || gateway.ProjectPath != seed)
                throw new InvalidDataException("Prepared Parties companion requires the ordinary package-only EventStore gateway seed.");

            List<object> inputs = [];
            if (PlatformCurrentOwnerProof.SelectsIntegration(options))
            {
                PlatformCurrentOwnerProof proof = PlatformCurrentOwnerProof.Create(options, signingKey, developmentRun: true, audited: true);
                options = options with { CurrentOwnerProof = proof, PartiesRunProof = proof.Parent };
                inputs.Add(Input("owner.parties", Path.Combine(proof.OutputDirectory, "Hexalith.Parties.dll"),
                    Path.Combine(proof.Artifacts, "obj", "Hexalith.Parties", "project.assets.json"), PlatformCurrentOwnerProof.OwnerProject(options)));
            }

            string readerDirectory = options.CurrentOwnerProof is null ? Path.Combine(Path.GetDirectoryName(plan.Workspace)!, "ordinary-reader") : Path.Combine(plan.Workspace, "catalog-reader");
            string reader = Path.Combine(readerDirectory, "output", "Hexalith.Platform.CatalogBuilder.dll");
            if (!File.Exists(reader))
            {
                var participants = PlatformRoutingCatalogBootstrap.ResolveParticipants(options, servers, PlatformDaprBindings.Resolve(declarations, options.RetentionProfile));
                reader = PlatformRoutingCatalog.CompileReader(options, PlatformCatalogContractBinding.Resolve(options, participants), plan.Workspace, readerDirectory, audited: true);
            }
            inputs.Add(Input("catalog-reader", reader, Path.Combine(readerDirectory, "obj", "project.assets.json"), Path.Combine(readerDirectory, "Hexalith.Platform.CatalogBuilder.csproj")));

            string gatewayRoot = Path.Combine(Path.GetDirectoryName(plan.Workspace)!, "eventstore");
            string gatewayOutput = Path.Combine(gatewayRoot, "output");
            string gatewayArtifacts = Path.Combine(gatewayRoot, "artifacts");
            var packageEnvironment = SourceMappingMaterializer.Environment(mapping, plan.Workspace);
            const string gatewayAssembly = "Hexalith.Builds.Module.EventStoreHost";
            string gatewayEntry = Path.Combine(gatewayOutput, gatewayAssembly + ".dll");
            if (!File.Exists(gatewayEntry))
            {
                var built = CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet",
                    ["build", seed, "-c", "Release", "--output", gatewayOutput, "--artifacts-path", gatewayArtifacts, "-m:1", "-nodeReuse:false",
                     "-p:GeneratePackageOnBuild=false", "-p:MinVerVersionOverride=1.0.0", "-p:CopyLocalLockFileAssemblies=true",
                     "-p:UseHexalithProjectReferences=false", "-p:NuGetAudit=true", "-p:NuGetAuditMode=all", "-p:NuGetAuditLevel=low", "-p:RestoreForceEvaluate=true"],
                    Path.GetDirectoryName(seed), packageEnvironment), PlatformRoutingCatalog.ReaderTimeout(TimeSpan.FromMinutes(5), plan.StartupDeadlineUtc, DateTimeOffset.UtcNow), CancellationToken.None)
                    .GetAwaiter().GetResult();
                if (!built.Started || built.TimedOut || built.ExitCode != 0) throw new InvalidDataException("The locked EventStore Release gateway seed could not build.");
            }
            inputs.Add(Input("owner.eventstore", gatewayEntry,
                Path.Combine(gatewayArtifacts, "obj", gatewayAssembly, "project.assets.json"), seed));
            Console.WriteLine(JsonSerializer.Serialize(inputs));
            return 0;

            object Input(string dependency, string entry, string assets, string project) =>
                new { role = PreparedNativeBuild.CompositionRole(selection.Name, dependency), entry, assets, project };
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            Console.Error.WriteLine("The prepared Platform package companion refused an audited owner or reader input.");
            return 1;
        }
    }
}
