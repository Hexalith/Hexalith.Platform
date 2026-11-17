using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.Builds.Tooling.Manifest;

// Metadata/build-only compatibility check. No builder, AppHost, probe or resource is executed.
string mappingPath = args[0], output = args[1], toolRoot = args[2];
string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
SourceMapping mapping = SourceMapping.LoadVerified(mappingPath, JsonDocument.Parse(File.ReadAllBytes(mappingPath)).RootElement.GetProperty("ContentHash").GetString()!);
string workspace = Path.Combine(Path.GetTempPath(), "ordinary-guard-workspace");
Directory.CreateDirectory(workspace);
await SourceMappingMaterializer.WriteAsync(mapping, workspace, CancellationToken.None);
string model = await PlatformManifestCommand.BuildModelAsync(mapping, workspace, CancellationToken.None);
AssemblyLoadContext.Default.Resolving += (_, name) => {
    foreach (string root in new[] { Path.GetDirectoryName(model)!, toolRoot }) {
        string file = Path.Combine(root, name.Name + ".dll");
        if (File.Exists(file)) return AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
    }
    return null;
};
Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(model);
Type T(string name) => assembly.GetType("Hexalith.Platform.Composition." + name, throwOnError: true)!;
string root = mapping.Root, declaration = Path.Combine(root, "module", "hexalith.module-manifest.v2.json");
string seed = Path.Combine(toolRoot, "g4-host", "projects", "EventStore", "Host.csproj");
if (!File.Exists(seed)) throw new Exception("Exact packaged seed metadata is absent");
List<object> outcomes = [];
foreach (string profile in new[] { "platform-package", "lifecycle-package" }) {
    using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(declaration));
    JsonElement test = document.RootElement.GetProperty("modules")[0].GetProperty("tests").EnumerateArray().Single(test => test.GetProperty("name").GetString() == profile);
    string entry = test.GetProperty("entry").GetString()!;
    DateTimeOffset now = DateTimeOffset.UtcNow;
    ModuleTestSelection selected = new("parties", profile, test.GetProperty("executor").GetString()!, entry,
        Hash(declaration), Hash(Path.Combine(root, entry)), test.GetProperty("executionTimeoutSeconds").GetInt32(),
        test.GetProperty("stopTimeoutSeconds").GetInt32(), now, now.AddSeconds(300), now.AddSeconds(300), "package");
    CompositionRunPlan plan = CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), workspace,
        Path.Combine(workspace, "unstarted-dapr"),
        new CompositionRunPorts(20001,20002,20003,20004,20005,20006,20007,20008,20009,20010,20011,20012,20013,[20014],20015,20016,20017,[new(20018,20019,20020,20021)]),
        [new("parties", "parties", "parties", Path.Combine(root, entry))], [], mapping, [declaration], "parties", model) with {
        AttemptId = CompositionRunPlanFactory.NewRunId(), EnvironmentId = CompositionRunPlanFactory.NewRunId(),
        SelectedTest = selected, SelectedTests = [selected], PlatformDeclarationHash = Hash(declaration),
    };
    var closure = CompositionDeclarationClosure.Load(new CompositionDeclarationOptions(mapping, [declaration]) {
        PlatformRoot = mapping.PlatformIdentity!.PlatformPath, ActiveModule = "parties", Workspace = workspace,
    });
    Type writer = typeof(CompositionRunPlan).Assembly.GetType("Hexalith.Builds.Tooling.TestEnvironment.TestEnvironmentDescriptorWriter")!;
    string composition = (string)writer.GetMethod("CompositionIdentity", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [closure, selected, null])!;
    plan = plan with { CompositionContentIdentity = composition };
    await CompositionDocumentStore.WriteAsync(Path.Combine(workspace, "plan.json"), plan, CancellationToken.None);
    Type optionType = T("PlatformCompositionOptions");
    object options = Activator.CreateInstance(optionType)!;
    void Set(string property, object value) => optionType.GetProperty(property, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(options, value);
    Set("PlatformRoot", mapping.PlatformIdentity.PlatformPath); Set("Mapping", mapping); Set("ActiveModule", "parties");
    Set("DeclarationPaths", new[] { declaration }); Set("EventStoreSeedProject", seed); Set("RunPlan", plan);
    string key = CompositionSigningKey.Create();
    object proof = T("PlatformPartiesRunProof").GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
        .Single(method => method.Name == "Create" && method.GetParameters().Length == 2).Invoke(null, [options, key])!;
    Set("PartiesRunProof", proof);
    object declarations = T("PlatformDeclarationSet").GetMethod("Load")!.Invoke(null, [options])!;
    object servers = T("PlatformAppModel").GetMethod("Prepare")!.Invoke(null, [options])!;
    object bound = T("PlatformPartiesPackageCatalogQualification").GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
        .Single(method => method.Name == "Bind" && method.GetParameters().Length == 6)
        .Invoke(null, [options, declarations, servers, true, true, Architecture.X64])!;
    int count = ((System.Collections.IEnumerable)bound).Cast<object>().Count();
    outcomes.Add(new { profile, ordinaryRunProof = true, fullPackageCohortGuard = true, serverCount = count,
        declarationSha256 = Hash(declaration), executionTimeoutSeconds = selected.ExecutionTimeoutSeconds,
        stopTimeoutSeconds = selected.StopTimeoutSeconds });
}
using var git = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = root, ArgumentList = { "rev-parse", "HEAD" }, RedirectStandardOutput = true })!;
string source = await git.StandardOutput.ReadToEndAsync();
await git.WaitForExitAsync();
if (git.ExitCode != 0) throw new Exception("Source identity query failed");
File.WriteAllText(output, JsonSerializer.Serialize(new {
    callerSource = source.Trim(),
    catalogSource = mapping.PlatformIdentity!.CatalogCommit, platformSource = mapping.PlatformIdentity.Commit,
    modelSha256 = Hash(model), toolingSha256 = Hash(typeof(CompositionRunPlan).Assembly.Location),
    toolchain = PlatformVersionCatalog.Current, outcomes,
    appHostExecuted = false, builderCreated = false, servicesStarted = false, probesExecuted = false,
    runtimeExecutionClaimed = false, candidateAcceptance = false,
}, new JsonSerializerOptions { WriteIndented = true }) + "\n");
