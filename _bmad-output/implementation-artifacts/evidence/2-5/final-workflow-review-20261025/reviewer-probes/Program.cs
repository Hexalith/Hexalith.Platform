using Hexalith.Builds.Tooling.Runtime;
using System.Diagnostics;
if (args.Length > 0) {
  System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (_, name) => {
    string candidate = Path.Combine("/home/administrator/projects/hexalith/platform/references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0", name.Name + ".dll");
    return File.Exists(candidate) ? System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate) : null;
  };
  using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
  Console.WriteLine("Reading result with 200ms cancellation allowance");
  var result = Hexalith.Builds.Tooling.TestEnvironment.TestEnvironmentContractReader.ReadResult(args[0], deadline.Token);
  Console.WriteLine("Read completed: " + result.IsValid);
  return;
}
var root = Path.Combine(Path.GetTempPath(), "story25-proof-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var runId = CompositionRunPlanFactory.NewRunId();
var claims = new CompositionResourceClaimStore(root);
claims.Acquire(runId, ["exclusive-proof"]);
var claim = Directory.GetFiles(Path.Combine(root, "claims"), "*.json").Single();
File.Delete(claim);
Directory.CreateDirectory(claim);
claims.ReleaseOwned(runId);
Console.WriteLine($"Claim directory still exists={Directory.Exists(claim)}, FindOwned count={claims.FindOwned(runId).Count}");
var docker = Path.Combine(root, "docker");
File.WriteAllText(docker, "#!/bin/sh\nexit 0\n");
File.SetUnixFileMode(docker, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
using var owned = new Process { StartInfo = CompositionProcess.CreateStartInfo("python3", ["-c", "import ctypes,time; ctypes.CDLL(None).prctl(4,0,0,0,0); print('ready',flush=True); time.sleep(60)"], root, new Dictionary<string,string> { [CompositionEnvironment.RunId] = runId }) };
owned.Start();
await owned.StandardOutput.ReadLineAsync();
try {
  try { File.ReadAllBytes($"/proc/{owned.Id}/environ"); Console.WriteLine("Owned environment readable"); }
  catch (Exception e) { Console.WriteLine("Owned environment read failure=" + e.GetType().Name); }
  var found = await new CompositionRunResourceScanner(docker).FindAsync(runId, CancellationToken.None);
  Console.WriteLine($"Owned process alive={!owned.HasExited}, Found={found.ProcessIds.Contains(owned.Id)}, Unverified={found.Unverified}, IsEmpty={found.IsEmpty}");
} finally { owned.Kill(true); await owned.WaitForExitAsync(); Directory.Delete(root,true); }
