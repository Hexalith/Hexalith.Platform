namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Runtime.Loader;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Microsoft.Extensions.Configuration;
using Shouldly;

public sealed class OwnerOptionBindingTests
{
    [Fact]
    public async Task MemoriesProfileBindsAndValidatesActualOwnerOptionsAsync()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "apphost.cs"))) directory = directory.Parent;
        string root = directory!.FullName;
        string owner = Path.Combine(root, "references", "Hexalith.Memories");
        string workspace = Path.Combine(Path.GetTempPath(), "platform-owner-options-" + Guid.NewGuid().ToString("N"));
        CompositionWorkspace.CreatePrivateDirectory(workspace);
        AssemblyLoadContext context = new("owner-options-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            SourceMapping mapping = await WorkspaceRootResolver.ResolveAsync(Path.Combine(owner, "module", "hexalith.module-manifest.v2.json"), WorkspaceMode.Source, TestContext.Current.CancellationToken);
            await SourceMappingMaterializer.WriteAsync(mapping, workspace, TestContext.Current.CancellationToken);
            string binaries = Path.Combine(workspace, "owner-options");
            string project = Path.Combine(owner, "src", "Hexalith.Memories.Server", "Hexalith.Memories.Server.csproj");
            CompositionProcessResult build = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet",
                ["build", project, "--configuration", "Debug", "--output", binaries, "-m:1", "-nodeReuse:false", "-p:UseSharedCompilation=false", "-p:NuGetAudit=false", "-p:MinVerVersionOverride=1.0.0"], root,
                SourceMappingMaterializer.Environment(mapping, workspace)), TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
            build.TimedOut.ShouldBeFalse(build.Output + build.ErrorOutput);
            build.ExitCode.ShouldBe(0, build.Output + build.ErrorOutput);
            string assemblyPath = Path.Combine(binaries, "Hexalith.Memories.Server.dll");
            File.Exists(assemblyPath).ShouldBeTrue();
            IReadOnlyList<PlatformServerBinding> servers = PlatformAppModel.Prepare(new PlatformCompositionOptions { PlatformRoot = root, Mapping = mapping, ActiveModule = "memories" });
            context.Resolving += (loader, name) => File.Exists(Path.Combine(binaries, name.Name + ".dll")) ? loader.LoadFromAssemblyPath(Path.Combine(binaries, name.Name + ".dll")) : null;
            Assembly serverAssembly = context.LoadFromAssemblyPath(assemblyPath);
            Assembly telemetryAssembly = context.LoadFromAssemblyPath(Path.Combine(binaries, "Hexalith.Memories.AccessTelemetry.Contracts.dll"));
            foreach (PlatformServerBinding server in servers.Where(server => server.Module.Declaration.GetProperty("identity").GetProperty("moduleId").GetString() == "memories"))
            {
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile(Path.Combine(owner, "src", "Hexalith.Memories.Server", "appsettings.json"))
                    .AddInMemoryCollection(server.Environment.Select(pair => new KeyValuePair<string, string?>(pair.Key.Replace("__", ":", StringComparison.Ordinal), pair.Value))).Build();
                Type defaults = serverAssembly.GetType("Hexalith.Memories.Server.Ingestion.EmbeddingProviderDefaultsOptions", throwOnError: true)!;
                object providers = configuration.GetSection("EmbeddingProviders").Get(defaults)!;
                Should.NotThrow(() => serverAssembly.GetType("Hexalith.Memories.Server.Ingestion.EmbeddingProviderDefaults", throwOnError: true)!.GetMethod("Configure")!.Invoke(null, [providers]));
                foreach ((string name, string section) in new[]
                {
                    ("RateLimiting.InboundRateLimitOptions", "InboundRateLimiting"),
                    ("NaturalLanguage.NaturalLanguageDescriptionOptions", "NaturalLanguage"),
                    ("Cases.CaseActivityOptions", "Cases:Activity"),
                    ("Ingestion.IngestionSettings", "Ingestion"),
                }) Should.NotThrow(() => configuration.GetSection(section).Get(serverAssembly.GetType("Hexalith.Memories.Server." + name, throwOnError: true)!));
                Type options = telemetryAssembly.GetType("Hexalith.Memories.AccessTelemetry.Contracts.AccessTelemetryOptions", throwOnError: true)!;
                object lifecycle = configuration.GetSection("AccessTelemetryLifecycle").Get(options)!;
                object result = telemetryAssembly.GetType("Hexalith.Memories.AccessTelemetry.Contracts.AccessTelemetryOptionsValidator", throwOnError: true)!.GetMethod("Validate")!.Invoke(null, [lifecycle, "Development"])!;
                result.GetType().GetProperty("IsValid")!.GetValue(result).ShouldBe(true);
            }
        }
        finally
        {
            context.Unload();
            (await CompositionWorkspace.TryDeleteAsync(workspace, CancellationToken.None)).ShouldBeTrue();
        }
    }
}
