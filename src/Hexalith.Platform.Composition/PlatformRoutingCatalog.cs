namespace Hexalith.Platform.Composition;

using System.Text.Json;
using System.Xml.Linq;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.EventStore.Contracts.Routing;

/// <summary>The exact validated bytes owned by one environment, generated before any resource starts.</summary>
internal sealed record PlatformRoutingCatalog(string InstanceId, string Directory, string CatalogPath, string RootDigest,
    RouteCatalogEnvelope Envelope, IReadOnlyList<string> OwnerAssemblies, IReadOnlyList<string> AdapterAssemblies)
{
    internal static TimeSpan ReaderTimeout(TimeSpan maximum, DateTimeOffset? startupDeadlineUtc, DateTimeOffset now)
    {
        if (startupDeadlineUtc is null) return maximum;
        TimeSpan remaining = CompositionStartupClock.Remaining(startupDeadlineUtc, now);
        if (remaining <= TimeSpan.Zero) throw new InvalidDataException("The routing catalog startup deadline has expired.");
        return remaining < maximum ? remaining : maximum;
    }

    internal static PlatformRoutingCatalog Generate(PlatformCompositionOptions options, IReadOnlyList<PlatformServerBinding> servers, string workspace)
    {
        string environment = options.RunPlan?.EnvironmentId ?? options.EnvironmentId ?? Path.GetFileName(workspace);
        string instance = environment + "-catalog";
        if (environment.Length == 0 || instance.Length > 128 || environment.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')
            || options.RunPlan is not null && options.EnvironmentId is not null && options.EnvironmentId != options.RunPlan.EnvironmentId)
            throw new InvalidDataException("The routing catalog requires this run's exact environment identity and a derived instance identifier of at most 128 characters.");
        _ = ReaderTimeout(TimeSpan.FromMinutes(5), options.RunPlan?.StartupDeadlineUtc, DateTimeOffset.UtcNow);
        var owners = PlatformCatalogContractBinding.Resolve(options, servers);
        string build = Path.Combine(workspace, "catalog-reader");
        CompositionWorkspace.CreatePrivateDirectory(build);
        string template = Path.Combine(options.PlatformRoot, "src", "Hexalith.Platform.CatalogBuilder");
        string project = Path.Combine(build, "Hexalith.Platform.CatalogBuilder.csproj");
        var xml = XDocument.Load(Path.Combine(template, "Hexalith.Platform.CatalogBuilder.csproj"));
        xml.Root!.AddFirst(new XElement("Import", new XAttribute("Project", Path.Combine(options.PlatformRoot, "Directory.Packages.props"))));
        xml.Root.AddFirst(new XElement("Import", new XAttribute("Project", Path.Combine(options.PlatformRoot, "Directory.Build.props"))));
        xml.Save(project);
        File.Copy(Path.Combine(template, "Program.cs"), Path.Combine(build, "Program.cs"), true);
        var references = new XElement("ItemGroup");
        foreach (var owner in owners)
        foreach (string reference in new[] { owner.ContractReference, owner.AdapterReference })
        {
            references.Add(owner.PackageVersion is null ? new XElement("ProjectReference", new XAttribute("Include", reference))
                : new XElement("PackageReference", new XAttribute("Include", reference)));
            if (owner.PackageVersion is not null)
                references.Add(new XElement("PackageVersion", new XAttribute("Update", reference), new XAttribute("Version", owner.PackageVersion)));
        }
        string props = Path.Combine(build, "bindings.props");
        new XDocument(new XElement("Project", references)).Save(props);
        string mappingWorkspace = options.RunPlan?.Workspace ?? workspace;
        string output = Path.Combine(build, "output");
        var start = CompositionProcess.CreateStartInfo("dotnet", ["build", project, "-c", options.Mapping.Mode == WorkspaceMode.Source ? "Debug" : "Release",
            "--output", output, "-m:1", "-p:GeneratePackageOnBuild=false", "-p:CentralPackageVersionsFileImported=true", "-p:PlatformCatalogBindings=" + props, "--nologo"], options.PlatformRoot, SourceMappingMaterializer.Environment(options.Mapping, mappingWorkspace));
        var compiled = CompositionProcess.RunAsync(start, ReaderTimeout(TimeSpan.FromMinutes(5), options.RunPlan?.StartupDeadlineUtc, DateTimeOffset.UtcNow), CancellationToken.None).GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(build, "build.log"), compiled.Output + compiled.ErrorOutput);
        if (compiled.ExitCode != 0 || compiled.TimedOut) throw new InvalidDataException($"Mapped routing catalog owner build failed; see {Path.Combine(build, "build.log")}.");
        string pending = Path.Combine(workspace, "catalog-pending-" + Guid.NewGuid().ToString("N"));
        CompositionWorkspace.CreatePrivateDirectory(pending);
        string request = Path.Combine(build, "request.json");
        File.WriteAllBytes(request, JsonSerializer.SerializeToUtf8Bytes(new
        {
            generation = options.CatalogGeneration, instanceId = instance, outputDirectory = pending,
            gatewayAppId = servers.Single(server => server.ResourceId == "eventstore").AppId,
            committedPredecessor = options.CommittedRoutingCatalogPath,
            committedPredecessorRootDigest = options.CommittedRoutingCatalogRootDigest,
            owners = owners.Select(owner => new { assembly = owner.Assembly, adapterAssembly = owner.AdapterAssembly, appId = owner.AppId }).ToArray(),
        }));
        try
        {
            var run = CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet", [Path.Combine(output, "Hexalith.Platform.CatalogBuilder.dll"), request], build, null),
                ReaderTimeout(TimeSpan.FromMinutes(1), options.RunPlan?.StartupDeadlineUtc, DateTimeOffset.UtcNow), CancellationToken.None).GetAwaiter().GetResult();
            File.WriteAllText(Path.Combine(build, "generation.log"), run.Output + run.ErrorOutput);
            if (run.ExitCode != 0 || run.TimedOut) throw new InvalidDataException($"Owner routing catalog generation refused; see {Path.Combine(build, "generation.log")}.");
            byte[] bytes = File.ReadAllBytes(Path.Combine(pending, "catalog.json"));
            using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(pending, "receipt.json")));
            string root = receipt.RootElement.GetProperty("rootDigest").GetString()!;
            if (receipt.RootElement.GetProperty("instanceId").GetString() != instance) throw new InvalidDataException("Cross-run catalog binding refused.");
            var catalog = RouteCatalogCodec.Decode(bytes, root);
            string target = Path.Combine(workspace, instance);
            if (System.IO.Directory.Exists(target))
            {
                if (!File.ReadAllBytes(Path.Combine(target, "catalog.json")).AsSpan().SequenceEqual(bytes)) throw new InvalidDataException("The run already owns different valid catalog bytes.");
                System.IO.Directory.Delete(pending, true);
            }
            else System.IO.Directory.Move(pending, target);
            return new(instance, target, Path.Combine(target, "catalog.json"), root, catalog,
                receipt.RootElement.GetProperty("ownerAssemblies").EnumerateArray().Select(value => value.GetString()!).ToArray(),
                receipt.RootElement.GetProperty("adapterAssemblies").EnumerateArray().Select(value => value.GetString()!).ToArray());
        }
        catch
        {
            if (System.IO.Directory.Exists(pending)) System.IO.Directory.Delete(pending, true);
            throw;
        }
    }
}
