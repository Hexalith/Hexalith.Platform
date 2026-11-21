"""Exercise the actual package-built fixture host with a pinned disposable Dapr sidecar."""

import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import signal
import socket
import subprocess
import sys
import tarfile
import tempfile
import time
import urllib.error
import urllib.request
import zipfile


ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "eng"))
from builds_tool_lock import download, load_lock
from owner_packages_lock import archive_path, locked_entry, require_locked

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--evidence", required=True, type=Path)
parser.add_argument("--tool-root", required=True, type=Path)
arguments = parser.parse_args()
EVIDENCE = arguments.evidence.absolute()
EVIDENCE.mkdir(parents=True)
scratch = tempfile.TemporaryDirectory(prefix="platform-signed-extension-")
work = Path(scratch.name)
source = subprocess.check_output(["git", "-C", str(ROOT), "rev-parse", "HEAD"], text=True, timeout=30).strip()
version = "2.2.5"
package = "Hexalith.Platform.TestExtension"
fixture_source = "0aacf13244fa6b1d9e08d627149d72cf12229167"
archive_hash = "7c3b432aba236b9cd102b6fad98a15ed629b3dcdfb9bad9ee6a3d105f63d04b4"
fixture_hash = "650d37d4aa1ae6de6c4633c1ecafa763349ed8c29f1c7e24d257ec0853d3f004"
copy = work / "Platform"
copy.mkdir()
receipt = {"package": package, "version": version, "platformSource": source, "fixtureSource": fixture_source,
           "commands": [], "distributedQualification": False}



def interrupted(signum, _frame):
    raise InterruptedError(f"Signed fixture component interrupted by signal {signum}")


signal.signal(signal.SIGINT, interrupted)
signal.signal(signal.SIGTERM, interrupted)


def run(command, cwd, name, env=None, timeout=240):
    log = EVIDENCE / f"fixture225-host-{name}.log"
    row = {"command": [str(item) for item in command], "cwd": str(cwd), "log": str(log)}
    receipt["commands"].append(row)
    with log.open("wb") as output:
        child = subprocess.Popen(command, cwd=cwd, env=env, stdout=output, stderr=subprocess.STDOUT, start_new_session=True)
        try:
            row["exitCode"] = child.wait(timeout=timeout)
        finally:
            try:
                os.killpg(child.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            child.wait()
    if row["exitCode"]:
        raise ValueError(f"{name} failed, see {log}")
    return log.read_text()


def write(relative, content):
    path = copy / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content)
    return path


def unpack(repository, revision, target, *paths):
    raw = subprocess.check_output(["git", "-C", str(repository), "archive", revision, *paths], timeout=60)
    with tarfile.open(fileobj=io.BytesIO(raw)) as archive:
        archive.extractall(target, filter="data")


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def free_ports():
    with socket.socket() as http, socket.socket() as grpc:
        http.bind(("127.0.0.1", 0))
        grpc.bind(("127.0.0.1", 0))
        return http.getsockname()[1], grpc.getsockname()[1]


def run_with_sidecar(command):
    pin = "1.19.2"
    asset = "daprd_linux_amd64.tar.gz"
    with urllib.request.urlopen(f"https://github.com/dapr/dapr/releases/download/v{pin}/{asset}", timeout=100) as response:
        dapr_bytes = response.read(100_000_001)
    if len(dapr_bytes) > 100_000_000:
        raise ValueError("The pinned Dapr download exceeded its size bound")
    pins = json.loads((copy / "references/Hexalith.Builds/Props/dapr-toolchain-sha256.json").read_text())
    if sha(dapr_bytes) != pins["runtime"][pin][asset]:
        raise ValueError("Downloaded Dapr runtime differs from the Builds archive pin")
    with tarfile.open(fileobj=io.BytesIO(dapr_bytes)) as tgz:
        tgz.extract("daprd", work, filter="data")
    daprd = work / "daprd"
    resources = work / "dapr-resources"
    resources.mkdir()
    (resources / "statestore.yaml").write_text("apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: statestore\nspec:\n  type: state.in-memory\n  version: v1\n  metadata: []\n")
    http_port, grpc_port = free_ports()
    sidecar_command = [str(daprd), "--app-id", "story32-signed-host-component", "--dapr-http-port", str(http_port), "--dapr-grpc-port", str(grpc_port),
                       "--dapr-listen-addresses", "127.0.0.1", "--resources-path", str(resources), "--enable-metrics=false"]
    row = {"command": sidecar_command, "archiveSha256": sha(dapr_bytes), "binarySha256": sha(daprd.read_bytes()), "version": pin,
           "state": "disposable in-memory component", "log": str(EVIDENCE / "fixture225-host-dapr.log")}
    receipt["sidecar"] = row
    with Path(row["log"]).open("wb") as log:
        sidecar = subprocess.Popen(sidecar_command, cwd=work, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
        try:
            deadline = time.monotonic() + 20
            while sidecar.poll() is None and time.monotonic() < deadline:
                try:
                    with urllib.request.urlopen(f"http://127.0.0.1:{http_port}/v1.0/healthz/outbound", timeout=1) as response:
                        if response.status == 204:
                            break
                except (OSError, urllib.error.URLError):
                    pass
                time.sleep(0.2)
            else:
                raise ValueError(f"Pinned Dapr is unavailable, see {row['log']}")
            return run(command, copy, "startup", env={**os.environ, "DAPR_HTTP_PORT": str(http_port), "DAPR_GRPC_PORT": str(grpc_port)}, timeout=65)
        finally:
            if sidecar.poll() is None:
                sidecar.terminate()
            try:
                row["exitCode"] = sidecar.wait(timeout=8)
            except subprocess.TimeoutExpired:
                os.killpg(sidecar.pid, signal.SIGKILL)
                row["exitCode"] = sidecar.wait()


try:
    unpack(ROOT, source, copy, "src", "tests/Hexalith.Platform.Composition.Tests/GatewaySeedProtectionInstallation.cs", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "eng", "global.json", "DaprSelfHostedMtls.cs", "DaprSelfHostedMtlsSubscriber.cs")
    builds = subprocess.check_output(["git", "-C", str(ROOT), "rev-parse", source + ":references/Hexalith.Builds"], text=True, timeout=30).strip()
    eventstore = subprocess.check_output(["git", "-C", str(ROOT), "rev-parse", source + ":references/Hexalith.EventStore"], text=True, timeout=30).strip()
    unpack(ROOT / "references/Hexalith.Builds", builds, copy / "references/Hexalith.Builds", "Props")
    unpack(ROOT / "references/Hexalith.EventStore", eventstore, copy / "references/Hexalith.EventStore", "tools/ext-protection-1/plan.json")
    tool_package = "Hexalith.Builds.Module.Cli"
    tool_lock = load_lock(copy / "eng/builds-tool.lock.json")
    tool_version = next(item["version"] for item in tool_lock["packages"] if item["id"] == tool_package)
    tool_root = arguments.tool_root.resolve(strict=True)
    source_archive = tool_root.parents[3] / f"{tool_package.lower()}.{tool_version}.nupkg"
    tool_bytes = source_archive.read_bytes()
    require_locked(locked_entry(tool_lock, tool_package, tool_version), tool_bytes)
    with zipfile.ZipFile(io.BytesIO(tool_bytes)) as archive:
        if (tool_root / "Hexalith.Builds.Tooling.dll").read_bytes() != archive.read("tools/net11.0/any/Hexalith.Builds.Tooling.dll"):
            raise ValueError("The component used another Builds Tooling assembly")

    package_bytes = download(version, package)
    if sha(package_bytes) != archive_hash:
        raise ValueError("The public fixture differs from the reviewed signed archive")
    signed = work / f"{package}.{version}.nupkg"
    signed.write_bytes(package_bytes)
    run(["dotnet", "nuget", "verify", str(signed), "--all"], work, "fixture-signature", timeout=90)
    fixture_dll_member = "lib/net11.0/" + package + ".dll"
    with zipfile.ZipFile(io.BytesIO(package_bytes)) as archive:
        fixture_dll = archive.read(fixture_dll_member)
    if sha(fixture_dll) != fixture_hash:
        raise ValueError("The fixture archive carries another runtime assembly")

    host = copy / "src/Hexalith.Platform.EventStoreHost/Hexalith.Platform.EventStoreHost.csproj"
    generator = write("probe/FixtureProbe.csproj", '''<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup><IsPackable>false</IsPackable><OutputType>Exe</OutputType></PropertyGroup>
 <ItemGroup><ProjectReference Include="../src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj" />
 <ProjectReference Include="../src/Hexalith.Platform.Protection/Hexalith.Platform.Protection.csproj" />
 <Compile Include="../tests/Hexalith.Platform.Composition.Tests/GatewaySeedProtectionInstallation.cs" />
 <Reference Include="Hexalith.Builds.Tooling" HintPath="$(BuildsToolingAssemblyPath)" /></ItemGroup>
</Project>
''')
    write("probe/Program.cs", '''using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Hexalith.Platform.Composition;
using Hexalith.Platform.Composition.Tests;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;

if (args[0] == "generate")
{
    using var sourceDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
    var declaration = Path.Combine(args[1], "tests/fixtures/Hexalith.Platform.TestExtension/hexalith.module-manifest.v2.json");
    var mapping = await WorkspaceRootResolver.ResolveAsync(declaration, WorkspaceMode.Package, sourceDeadline.Token);
    await SourceMappingMaterializer.WriteAsync(mapping, args[5], sourceDeadline.Token);
    var catalog = new CompositionDeclarationOptions(mapping, []) { Workspace = args[5] };
    string? version = catalog.CatalogPackageVersion("Hexalith.Platform.TestExtension", declaration, "Hexalith.Platform.EventStoreHost");
    if (version != args[2]) throw new InvalidOperationException("The exact named fixture pin is unavailable");
    var extension = new PlatformEventStoreExtensionBinding("platformtestextension", declaration, "Hexalith.Platform.TestExtension", "platform-test", 1, "Hexalith.Platform.TestExtension", version, []);
    var writer = typeof(PlatformEventStoreHostBinding).Assembly.GetType("Hexalith.Platform.Composition.PlatformEventStoreExtensionBindings", true)!;
    Console.WriteLine(writer.GetMethod("Write", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [new PlatformEventStoreHostBinding(args[3], [extension]), args[4]]));
    return;
}

using var install = new GatewaySeedProtectionInstallation();
using var host = new Process();
host.StartInfo = new ProcessStartInfo("dotnet", [args[1]]) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = args[2] };
void Set(string name, string value) => host.StartInfo.Environment[name] = value;
Set("DOTNET_STARTUP_HOOKS", typeof(StartupHook).Assembly.Location);
Set("STORY32_LOADED_FIXTURE", args[3]);
Set("ASPNETCORE_ENVIRONMENT", "Development");
Set("ASPNETCORE_URLS", "http://127.0.0.1:0");
Set("ASPNETCORE_CONTENTROOT", args[2]);
Set("APP_API_TOKEN", "story32-component-example-channel");
Set("Dapr__DeclarativeSubscriptions", "true");
Set("EventStore__CommandStatus__StateStoreName", "statestore");
Set("Authentication__JwtBearer__Issuer", "story32-component");
Set("Authentication__JwtBearer__Audience", "hexalith-eventstore");
Set("Authentication__JwtBearer__SigningKey", "story32-component-signing-key-for-local-probe");
Set("Authentication__JwtBearer__AllowedAlgorithms__0", "HS256");
Set("Authentication__JwtBearer__RequireHttpsMetadata", "false");
Set("EventStore__Extensions__Packages__0__PackageId", "Hexalith.Platform.TestExtension");
Set("EventStore__Extensions__Packages__0__ExtensionId", "platform-test");
Set("EventStore__Extensions__Packages__0__ApiMajor", "1");
Set("EventStore__Extensions__Packages__0__ModuleId", "platformtestextension");
Set("PlatformTestExtension__Marker", "story32-signed-probe");
Set("HEXALITH_G4_RUN_ID", args[4]);
Set("Platform__SignedFixtureQualification__RunId", args[4]);
Set("Platform__SignedFixtureQualification__Workspace", args[5]);
Set("Platform__SignedFixtureQualification__Version", args[6]);
Set("Platform__SignedFixtureQualification__ArchiveSha256", args[7]);
Set("Platform__SignedFixtureQualification__LibrarySha256", args[8]);
var endpoint = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
host.OutputDataReceived += (_, line) => {
    Console.WriteLine(line.Data);
    const string prefix = "Now listening on: ";
    if (line.Data is { } message && message.Contains(prefix, StringComparison.Ordinal))
    {
        using var log = JsonDocument.Parse(message);
        var address = log.RootElement.GetProperty("State").GetProperty("address").GetString();
        if (Uri.TryCreate(address, UriKind.Absolute, out var uri)) endpoint.TrySetResult(uri);
    }
};
host.ErrorDataReceived += (_, line) => Console.Error.WriteLine(line.Data);
host.EnableRaisingEvents = true;
host.Exited += (_, _) => endpoint.TrySetException(new InvalidOperationException($"Host exited {host.ExitCode} before listening"));
if (!host.Start()) throw new InvalidOperationException("Host was not launched");
try {
    host.BeginOutputReadLine();
    host.BeginErrorReadLine();
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(40));
    using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
    Uri address = await endpoint.Task.WaitAsync(deadline.Token);
    if (!address.IsLoopback) throw new InvalidOperationException("Host did not bind loopback");
    using var response = await client.GetAsync(new Uri(address, "/ready"), deadline.Token);
    if ((int)response.StatusCode is not (200 or 503)) throw new InvalidOperationException($"Unexpected readiness HTTP status {(int)response.StatusCode}");
    using var report = JsonDocument.Parse(await response.Content.ReadAsStringAsync(deadline.Token));
    var check = report.RootElement.GetProperty("results").GetProperty("eventstore-extensions");
    if (check.GetProperty("status").GetString() != "Healthy" || check.GetProperty("data").GetProperty("platform-test").GetInt32() != 1)
        throw new InvalidOperationException(check.GetRawText());
    using var loaded = JsonDocument.Parse(await File.ReadAllTextAsync(args[3], deadline.Token));
    Console.WriteLine("SIGNED_PROBE=" + JsonSerializer.Serialize(new { httpStatus = (int)response.StatusCode, extensionReadiness = check, loaded = loaded.RootElement, hostProcess = host.Id }));
} finally {
    if (!host.HasExited) host.Kill(entireProcessTree: true);
    if (!host.WaitForExit(5000)) throw new TimeoutException("Host did not terminate");
}

public static class StartupHook
{
    public static void Initialize() => AppDomain.CurrentDomain.AssemblyLoad += (_, evt) => {
        if (evt.LoadedAssembly.GetName().Name == "Hexalith.Platform.TestExtension")
        {
            var location = evt.LoadedAssembly.Location;
            var sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(location)));
            File.WriteAllText(Environment.GetEnvironmentVariable("STORY32_LOADED_FIXTURE")!, JsonSerializer.Serialize(new { location, sha256, process = Environment.ProcessId }));
        }
    };
}
''')
    artifacts = work / "artifacts"
    build = ["dotnet", "build", "-c", "Release", "--artifacts-path", str(artifacts), "-p:UseHexalithProjectReferences=false", "-nodeReuse:false", "--disable-build-servers", "--nologo", f"-p:HexalithBuildsPublishedToolRoot={tool_root}"]
    run([*build, str(generator)], copy, "generator-build")
    driver = artifacts / "bin/FixtureProbe/release/FixtureProbe.dll"
    generated = work / "binding"
    run(["dotnet", str(driver), "generate", str(ROOT), version, str(host), str(generated), str(work / "catalog")], copy, "bindings")
    receipt["bindingXml"] = (generated / "bindings.props").read_text()
    host_artifacts = work / "project-builds/eventstore"
    host_build = [str(host_artifacts) if argument == str(artifacts) else argument for argument in build]
    run([*host_build, str(host), f"-p:PlatformEventStoreExtensionBindings={generated / 'bindings.props'}"], copy, "host-build")

    output = host_artifacts / "bin/Hexalith.Platform.EventStoreHost/release"
    assets = json.loads((host_artifacts / "obj/Hexalith.Platform.EventStoreHost/project.assets.json").read_bytes())
    deps = json.loads((output / "Hexalith.Platform.EventStoreHost.deps.json").read_bytes())
    lock = json.loads((copy / "eng/owner-packages.lock.json").read_text())
    libraries = [key for key in deps["libraries"] if key.startswith(("Hexalith.EventStore.", package + "/"))]
    if package + "/" + version not in libraries:
        raise ValueError("The actual host dependency graph has no published fixture library")
    restored = {}
    for key in libraries:
        package_id, selected = key.split("/")
        if deps["libraries"][key]["type"] != "package" or assets["libraries"][key]["type"] != "package":
            raise ValueError(f"Host loaded a project library for {key}")
        found = [path for folder in assets["packageFolders"] if (path := archive_path(folder, package_id, selected)).is_file()]
        if len(found) != 1:
            raise ValueError(f"Missing or ambiguous restored archive for {key}")
        raw = found[0].read_bytes()
        if package_id == package:
            if raw != package_bytes or (output / (package + ".dll")).read_bytes() != fixture_dll:
                raise ValueError("Host output or restored fixture differs from the signed archive")
        else:
            require_locked(locked_entry(lock, package_id, selected), raw)
        restored[key] = sha(raw)
    receipt.update(restoredArchives=restored, buildCatalog=builds, fixtureDllSha256=sha(fixture_dll), hostProgramSha256=sha(host.with_name("Program.cs").read_bytes()))
    from uuid import uuid4
    run_id = uuid4().hex
    logs = run_with_sidecar(["dotnet", str(driver), "start", str(output / "Hexalith.Platform.EventStoreHost.dll"),
                            str(host.parent), str(work / "loaded.json"), run_id, str(work), version, archive_hash, fixture_hash])
    result = json.loads(next(line.removeprefix("SIGNED_PROBE=") for line in logs.splitlines() if line.startswith("SIGNED_PROBE=")))
    if result["loaded"]["sha256"] != sha(fixture_dll) or result["loaded"]["process"] != result["hostProcess"]:
        raise ValueError("The actual host loaded a different DLL or the observation came from another process")
    observation = json.loads((work / "fixture-host-loaded.json").read_bytes())
    if observation["runId"] != run_id or observation["processId"] != result["hostProcess"] or observation["librarySha256"] != fixture_hash:
        raise ValueError("Runner and host observed different fixture identities")
    receipt.update(passed=True, nativeTestCases=0, startup=result, hostObservation=observation)
except BaseException as problem:
    receipt.update(passed=False, failure=str(problem))
    raise
finally:
    (EVIDENCE / "fixture225-signed-host-component.json").write_text(json.dumps(receipt, indent=2) + "\n")
    scratch.cleanup()
