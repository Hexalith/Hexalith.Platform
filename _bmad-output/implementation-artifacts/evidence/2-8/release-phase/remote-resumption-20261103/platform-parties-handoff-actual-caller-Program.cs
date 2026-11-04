using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.Builds.Tooling.TestEnvironment;
using Hexalith.Builds.Tooling.Manifest;
string caller = "/home/administrator/.cache/hexalith/story-2-8/parties-platform117-adoption-20261103/Hexalith.Parties";
string tool = "/home/administrator/.cache/hexalith/story-2-8/builds-4442-official-20261104/hexalith.builds.module.cli-extracted/tools/net10.0/any";
string workspace = Path.GetFullPath(args[0]);
string manifest = Path.Combine(caller, "module", "hexalith.module-manifest.v2.json");
CompositionWorkspace.CreatePrivateDirectory(workspace);
var mapping = await WorkspaceRootResolver.ResolveAsync(manifest, WorkspaceMode.Package, CancellationToken.None);
if (mapping.Root != caller || mapping.PlatformIdentity!.CheckoutCommit != "338a6afc31850761f49c0de7d399266dd15cfe84") throw new InvalidOperationException("Unexpected caller binding.");
await SourceMappingMaterializer.WriteAsync(mapping, workspace, CancellationToken.None);
var closure = CompositionDeclarationClosure.Load(new(mapping, [manifest]) { ActiveModule = "parties", Workspace = workspace });
var validation = PlatformManifestValidator.Validate([manifest], CancellationToken.None);
if (!validation.IsValid) throw new InvalidOperationException("Actual manifest validation failed.");
var select = typeof(ModuleTestSelection).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(m => m.Name == "SelectBatch" && m.GetParameters().Length == 4);
var selected = (IReadOnlyList<ModuleTestSelection>)select.Invoke(null, [validation.Declarations!, new string[]{"platform-package"}, manifest, DateTimeOffset.UtcNow])!;
if (selected.Count != 1) throw new InvalidOperationException("Actual producer selection failed.");
string model = await PlatformManifestCommand.BuildModelAsync(mapping, workspace, DateTimeOffset.UtcNow.AddSeconds(600), CancellationToken.None);
string identity = (string)typeof(TestEnvironmentDescriptorWriter).GetMethod("CompositionIdentity", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [closure, selected[0], null])!;
var plan = CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), workspace, Path.Combine(workspace, "dapr-home"),
    new CompositionRunPorts(20001,20002,20003,20004,20005,20006,20007,20008,20009,20010,20011,20012,20013,[20014],20015,20016,20017,[new(20018,20019,20020,20021)]), [], [], mapping, [manifest], "parties", model) with
{
    SelectedTest = selected[0], SelectedTests = selected, PlatformDeclarationHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(manifest))),
    ResourceClaimStateDirectory = Path.Combine(workspace, "claim-state"), StartupDeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(600), CompositionContentIdentity = identity,
};
string key = CompositionSigningKey.Create();
string probes = Path.Combine(workspace, "probes"); Directory.CreateDirectory(probes);
string docker = Path.Combine(probes, "docker"); string log = Path.Combine(probes, "calls.txt");
await File.WriteAllTextAsync(docker, "#!/bin/sh\nprintf '%s\\n' \"$*\" >> '"+log+"'\nexit 83\n");
if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(docker, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
var rows = new List<object>();
foreach (bool claims in new[]{false, true})
{
    var original = plan with { ResourceClaimStateDirectory = claims ? plan.ResourceClaimStateDirectory : null };
    await CompositionDocumentStore.WriteAsync(Path.Combine(workspace, "plan.json"), original, CancellationToken.None);
    if (File.Exists(log)) File.Delete(log);
    var start = CompositionProcess.CreateStartInfo("dotnet", ["exec", Path.Combine(tool,"g4-host","Hexalith.Builds.Module.AppHost.dll"), "--validate-platform"], workspace, new Dictionary<string,string>
    {
        [CompositionEnvironment.PlanPath] = Path.Combine(workspace,"plan.json"), [CompositionEnvironment.RunId]=plan.RunId, [CompositionEnvironment.SigningKey]=key,
        [CompositionEnvironment.DaprRuntimePath]=Path.Combine(plan.DaprHome,".dapr","bin"), ["DOTNET_ENVIRONMENT"]="Development", ["PATH"]=probes+Path.PathSeparator+Environment.GetEnvironmentVariable("PATH"),
    });
    var result = await CompositionProcess.RunAsync(start, TimeSpan.FromSeconds(45), CancellationToken.None);
    if (result.Output.Contains(key) || result.ErrorOutput.Contains(key)) throw new InvalidOperationException("Private key escaped.");
    var calls = File.Exists(log) ? File.ReadAllLines(log) : [];
    bool expected = result.Started && !result.TimedOut && !result.OutputTruncated && result.ExitCode==3 && (claims ? result.ErrorOutput.Contains("trusted parent plan/key handoff") && calls.Length==0 : result.ErrorOutput.Contains("unavailable from its released registry") && calls.Length==1);
    rows.Add(new { canonicalClaimsPresent=claims, result.Started, result.ExitCode, result.TimedOut, result.OutputTruncated, stderr=result.ErrorOutput, imageProbeCalls=calls, expected });
    if (!expected) throw new InvalidOperationException(JsonSerializer.Serialize(rows));
}
if (Directory.Exists(Path.Combine(workspace,"platform-components")) || Directory.Exists(plan.ResourceClaimStateDirectory!)) throw new InvalidOperationException("Unexpected effects.");
Console.WriteLine(JsonSerializer.Serialize(new { caller, mapping.ContentHash, mapping.PlatformIdentity, manifestHash=plan.PlatformDeclarationHash, selectedTests=selected.Select(s=>s.Name), modelPath=model, modelSha256=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(model))), toolSha256=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(CompositionRunPlan).Assembly.Location))), rows, componentOnly=true, servicesStarted=false }, new JsonSerializerOptions {WriteIndented=true}));
