namespace Hexalith.Platform.Composition.Tests;

using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Platform.CatalogArtifacts;
using Shouldly;

/// <summary>Replays the admitted production package cohort independently of the test dependency closure; starts no services or image probes.</summary>
/// <param name="output">Retains the actual isolated production build output.</param>
public sealed class PlatformProductionModelClosureTests(ITestOutputHelper output)
{
    /// <summary>The exact admitted package model supplies its signed Server body before the production verifier loads it.</summary>
    [Fact]
    public async Task AdmittedProductionCohortSuppliesTheSignedServerOutsideTheTestClosureAsync()
    {
        string root = PlatformTestRepository.Root;
        string builds = PlatformTestRepository.ReferenceRoot(root, "Hexalith.Builds");
        using JsonDocument tuple = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "eng", "builds-tuple.json")));
        string catalogHash = tuple.RootElement.GetProperty("producer").GetProperty("catalog").GetProperty("sha256").GetString()!;
        string source = tuple.RootElement.GetProperty("producer").GetProperty("sourceRevision").GetString()!;
        var revision = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("git", ["rev-parse", "HEAD"], builds, null),
            TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken).ConfigureAwait(true);
        revision.ExitCode.ShouldBe(0, revision.ErrorOutput);
        revision.Output.Trim().ShouldBe(source);

        string tool = typeof(PlatformProductionModelClosureTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "PlatformTestPublishedToolRoot").Value!;
        string tooling = Path.Combine(tool, "Hexalith.Builds.Tooling.dll");
        Hash(tooling).ShouldBe(tuple.RootElement.GetProperty("publication").GetProperty("toolingSha256").GetString());
        string workspace = Path.Combine(Path.GetTempPath(), "platform-production-model-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            // Preserve the recorded blob's BOM and LF bytes; checkout/export EOL conversion and text capture are not catalog authority.
            string catalog = Path.Combine(workspace, "Directory.Packages.props");
            await ExportCatalogBlobAsync(builds, source, catalog).ConfigureAwait(true);
            Hash(catalog).ShouldBe(catalogHash);
            string project = Path.Combine(root, "src", "Hexalith.Platform.Composition", "Hexalith.Platform.Composition.csproj");
            // Observe this real model's named view; the authenticated catalog can also declare a generic Tenants default.
            var selection = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet",
                ["msbuild", project, "-nologo", "-nodeReuse:false", "-getProperty:HexalithTenantsVersion",
                 "-p:Configuration=Release", "-p:UseHexalithProjectReferences=false", "-p:HexalithBuildsRoot=" + builds,
                 "-p:BuildsToolingAssemblyPath=" + tooling, "-p:CustomBeforeDirectoryBuildProps=" + catalog,
                 "-p:DirectoryPackagesPropsPath=" + catalog], root, null),
                TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken).ConfigureAwait(true);
            output.WriteLine(selection.Output + selection.ErrorOutput);
            selection.ExitCode.ShouldBe(0, selection.ErrorOutput);
            selection.TimedOut.ShouldBeFalse();
            selection.OutputTruncated.ShouldBeFalse();
            string tenantsVersion = selection.Output.Trim();
            (string ExpectedHash, string Cohort) selected = tenantsVersion switch
            {
                "7.0.5" => ("e84f4b22485c5db4866636558488e387132369583483b0334839c1bcf66b95ad", PlatformPartiesPackageFixture.EventStoreTestCohort),
                "7.0.7" => ("1e180b869b693ab50c5522959f52b6b8f1f5637102d18fc9df1f3fa9768ca68d", PublishedCatalogArtifactCohort.EventStorePackageTestCurrentName),
                _ => throw new InvalidDataException("The production closure requires an independently measured Server package."),
            };
            string security = Path.Combine(root, "eng", "HistoricalPackageSecurity.props");
            File.Exists(security).ShouldBeTrue("The isolated build must use the same bounded security patches as the owning Platform build.");
            string model = Path.Combine(workspace, "hosts", "platform-model");
            string artifacts = Path.Combine(workspace, "build", "platform-model");
            // The existing Server707 authority is an SDK4.2 cohort. New catalog defaults remain observed separately.
            string[] admittedProperties = tenantsVersion == "7.0.7"
                ? ["-p:HexalithEventStoreVersion=4.2.0", "-p:HexalithTenantsVersion=7.0.7",
                   "-p:HexalithMemoriesVersion=3.0.27", "-p:HexalithPartiesVersion=3.1.1", "-p:HexalithMcpCliVersion=2.0.1"]
                : [];
            var result = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet",
                ["build", project, "--configuration", "Release", "--output", model, "--artifacts-path", artifacts,
                 "-p:UseHexalithProjectReferences=false", "-p:HexalithBuildsRoot=" + builds, "-p:BuildsToolingAssemblyPath=" + tooling,
                 "-p:CustomBeforeDirectoryBuildProps=" + catalog, "-p:CustomAfterMicrosoftCommonProps=" + security, "-p:ImportDirectoryPackagesProps=false",
                 "-p:CentralPackageVersionsFileImported=true", "-p:CentralPackageTransitivePinningEnabled=true",
                 "-p:NuGetAudit=true", "-p:NuGetAuditMode=all", "-p:NuGetAuditLevel=low", "-p:DirectoryPackagesPropsPath=" + catalog,
                 .. admittedProperties,
                 "-p:CopyLocalLockFileAssemblies=true", "-p:UseSharedCompilation=false", "-nodeReuse:false", "-m:1",
                 "-p:BuildInParallel=false", "-p:RestoreBuildInParallel=false"], root, null),
                TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken).ConfigureAwait(true);
            output.WriteLine(result.Output + result.ErrorOutput);
            result.ExitCode.ShouldBe(0, result.ErrorOutput);
            result.TimedOut.ShouldBeFalse();
            result.OutputTruncated.ShouldBeFalse();
            Hash(catalog).ShouldBe(catalogHash, "Security patches must not rewrite the authenticated exported catalog blob.");
            string server = Path.Combine(model, "Hexalith.Tenants.Server.dll");
            File.Exists(server).ShouldBeTrue("The production model output itself must supply Server; the test project's reference cannot satisfy this prerequisite.");
            Hash(server).ShouldBe(selected.ExpectedHash);
            using JsonDocument assets = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(artifacts, "obj", "Hexalith.Platform.Composition", "project.assets.json")));
            JsonElement audit = assets.RootElement.GetProperty("project").GetProperty("restore").GetProperty("restoreAuditProperties");
            audit.GetProperty("enableAudit").GetString().ShouldBe("true");
            audit.GetProperty("auditMode").GetString().ShouldBe("all");
            audit.GetProperty("auditLevel").GetString().ShouldBe("low");
            assets.RootElement.GetProperty("libraries").GetProperty("Hexalith.Tenants.Server/" + tenantsVersion).GetProperty("type").GetString().ShouldBe("package");
            if (tenantsVersion == "7.0.7")
            {
                foreach (string identity in new[] { "Hexalith.EventStore.DomainService/4.2.0", "Hexalith.Memories.Contracts/3.0.27", "Hexalith.Parties.Contracts/3.1.1" })
                    assets.RootElement.GetProperty("libraries").GetProperty(identity).GetProperty("type").GetString().ShouldBe("package");
                output.WriteLine(JsonSerializer.Serialize(new { Scope = "Admitted SDK4.2 production-model replay; no default SDK5.1 native qualification",
                    CatalogSha256 = catalogHash, AssetsSha256 = Hash(Path.Combine(artifacts, "obj", "Hexalith.Platform.Composition", "project.assets.json")),
                    ServerSha256 = Hash(server), Properties = admittedProperties, CandidateAcceptance = false, RuntimeAcceptance = false }));
            }
            assets.RootElement.GetProperty("targets").EnumerateObject().Single().Value.GetProperty("Hexalith.Tenants.Server/" + tenantsVersion)
                .GetProperty("runtime").TryGetProperty("lib/net10.0/Hexalith.Tenants.Server.dll", out _).ShouldBeTrue();
            var context = new AssemblyLoadContext("production-model-only-" + Guid.NewGuid().ToString("N"), isCollectible: true);
            context.Resolving += (load, name) =>
            {
                string path = Path.Combine(model, name.Name + ".dll");
                return File.Exists(path) ? load.LoadFromAssemblyPath(path) : null;
            };
            try
            {
                // Explicitly load the already-hash-verified production file before any fallback can see the test's copy.
                Assembly loaded = context.LoadFromAssemblyPath(server);
                loaded.Location.ShouldBe(server);
                AssemblyLoadContext.GetLoadContext(loaded).ShouldBeSameAs(context);
                Assembly production = context.LoadFromAssemblyPath(Path.Combine(model, "Hexalith.Platform.Composition.dll"));
                using (context.EnterContextualReflection())
                {
                    Type authority = production.GetType("Hexalith.Platform.CatalogArtifacts.PublishedCatalogArtifactCohort", throwOnError: true)!;
                    bool matched = (bool)authority.GetMethod("Matches", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null,
                        ["Hexalith.Tenants.Server", selected.Cohort])!;
                    matched.ShouldBeTrue("The actual production authority must verify the body from its own isolated model directory.");
                }
                context.Assemblies.Single(assembly => assembly.GetName().Name == "Hexalith.Tenants.Server").Location.ShouldBe(server);
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(workspace, recursive: true); }
    }

    private static async Task ExportCatalogBlobAsync(string builds, string source, string catalog)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var process = new Process
        {
            StartInfo = CompositionProcess.CreateStartInfo("git", ["show", source + ":Props/Directory.Packages.props"], builds, null),
        };
        process.Start().ShouldBeTrue();
        Task<string> error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await using (FileStream blob = File.Create(catalog))
            {
                await Task.WhenAll(process.StandardOutput.BaseStream.CopyToAsync(blob, timeout.Token),
                    process.WaitForExitAsync(timeout.Token), error).ConfigureAwait(true);
            }

            process.ExitCode.ShouldBe(0, await error.ConfigureAwait(true));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(true);
            }
        }
    }

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
