#!/usr/bin/env python3
"""Disposable local package/HTTP probe. Never publishes or grants released-cohort admission."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import tempfile
import time
import xml.etree.ElementTree as ET
import zipfile
import uuid

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
VERSION_PREFIX = "0.0.0-story32.local.20261109"
PACKAGE = "Hexalith.Platform.TestExtension"
SDK = ["Admin.Abstractions", "Contracts", "ServiceDefaults", "Client", "DomainService", "Server", "Gateway"]


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def run(command, cwd, log, timeout=600):
    started = time.monotonic()
    commands.append({"command": command, "cwd": str(cwd), "timeoutSeconds": timeout, "log": str(log)})
    with open(log, "wb") as output:
        child = subprocess.Popen(command, cwd=cwd, stdout=output, stderr=subprocess.STDOUT, start_new_session=True)
        try:
            code = child.wait(timeout=timeout)
        except BaseException:
            os.killpg(child.pid, signal.SIGKILL)
            child.wait()
            raise
    commands[-1].update(exitCode=code, elapsedSeconds=round(time.monotonic() - started, 3))
    if code:
        raise RuntimeError(f"Exit {code}: {command}; see {log}")


def write(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content)


def project_overlay(path, xml):
    document = ET.parse(path)
    document.getroot().append(ET.fromstring(xml))
    document.write(path, encoding="unicode")


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--root", type=Path, default=ROOT)
parser.add_argument("--work", type=Path)
parser.add_argument("--version", help="Unique nonpublic candidate version; omit to generate one")
args = parser.parse_args()
VERSION = args.version or VERSION_PREFIX + "." + uuid.uuid4().hex[:12]
if not VERSION.startswith(VERSION_PREFIX + "."):
    parser.error("Candidate version must use the unmistakable nonpublic prefix")
root = args.root.resolve()
work = args.work.resolve() if args.work else Path(tempfile.mkdtemp(prefix="story32-candidate-package-"))
work.mkdir(parents=True, exist_ok=True)
commands = []
receipt = {"schema": "story32-local-candidate-probe.v1", "qualificationCredit": False,
           "candidateOnly": True, "packageVersion": VERSION, "work": str(work), "commands": commands,
           "scope": "Actual Platform Program over loopback Kestrel HTTP; no sidecar/distributed readiness or production cohort admission.",
           "limits": ["The existing seed metadata fixture removes only AdminOperationalIndexHostedService's hosted alias; no Dapr sidecar is provisioned.",
                      "The locally packaged SDK is deliberately unsigned and nonpublic. No signed-body, image, provenance or finite-host admission check is invoked or changed."],
           "source": {"platform": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip(),
                      "bindingWriterSha256": sha(root / "src/Hexalith.Platform.Composition/PlatformEventStoreExtensionBindings.cs"),
                      "fixtureSourceSha256": {path.name: sha(path) for path in sorted((root / "tests/fixtures" / PACKAGE).glob("*.cs"))},
                      "eventstore": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root / "references/Hexalith.EventStore", text=True).strip()}}
print(f"Candidate work directory: {work}", flush=True)
try:
    feed = work / "feed"
    feed.mkdir(exist_ok=True)
    es = root / "references/Hexalith.EventStore"
    for name in SDK:
        package = "Hexalith.EventStore." + name
        print("Packing " + package, flush=True)
        run(["dotnet", "pack", str(es / "src" / package / (package + ".csproj")), "-c", "Release",
             "--artifacts-path", str(work / "sdk-artifacts"), "-o", str(feed), "-nodeReuse:false", "--disable-build-servers",
             "-p:UseHexalithProjectReferences=false", "-p:Version=" + VERSION, "-p:PackageVersion=" + VERSION,
             "-p:GeneratePackageOnBuild=false", "--nologo"], root, work / ("pack-" + name + ".log"))
    # Copy tracked Platform inputs only, never an existing bin/obj or fixture output.
    platform = work / "platform"
    files = subprocess.check_output(["git", "ls-files", "--", ":!:references"], cwd=root, text=True).splitlines()
    for relative in files:
        source = root / relative
        if source.is_file() and not relative.startswith("_bmad-output/"):
            target = platform / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
    write(platform / "nuget.config", '<configuration><packageSources><clear/><add key="candidate" value="' + str(feed) + '"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources><packageSourceMapping><clear/><packageSource key="candidate">' + ''.join('<package pattern="Hexalith.EventStore.' + name + '"/>' for name in SDK) + '<package pattern="' + PACKAGE + '"/></packageSource><packageSource key="nuget.org"><package pattern="*"/></packageSource></packageSourceMapping></configuration>')
    # Imported catalog remains the selected Builds source. The local SDK override is finite and test-only.
    copied_builds = platform / "references/Hexalith.Builds/Props"
    shutil.copytree(root / "references/Hexalith.Builds/Props", copied_builds, dirs_exist_ok=True)
    protection_plan = platform / "references/Hexalith.EventStore/tools/ext-protection-1/plan.json"
    protection_plan.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(es / "tools/ext-protection-1/plan.json", protection_plan)
    central = platform / "Directory.Packages.props"
    project_overlay(central, "<ItemGroup>" + "".join(f'<PackageVersion Remove="Hexalith.EventStore.{name}"/><PackageVersion Include="Hexalith.EventStore.{name}" Version="{VERSION}"/>' for name in SDK) + f'<PackageVersion Include="{PACKAGE}" Version="{VERSION}"/></ItemGroup>')
    host = platform / "src/Hexalith.Platform.EventStoreHost/Hexalith.Platform.EventStoreHost.csproj"
    fixture = platform / "tests/fixtures" / PACKAGE / (PACKAGE + ".csproj")
    for project in [host, fixture]:
        project_overlay(project, '<PropertyGroup><DefineConstants>$(DefineConstants);HEXALITH_PLATFORM_EXTENSION_SDK</DefineConstants></PropertyGroup>')
    fixture_props = ["-p:IsPackable=true", "-p:PackageVersion=" + VERSION, "-p:Version=" + VERSION,
                     "-p:Description=Disposable local Story 3.2 extension probe", "-p:Authors=Local candidate probe"]
    run(["dotnet", "pack", str(fixture), "-c", "Release", "--artifacts-path", str(work / "fixture-artifacts"),
         "-o", str(feed), "-p:UseHexalithProjectReferences=false", "-p:RestoreAdditionalProjectSources=" + str(feed),
         "-nodeReuse:false", "--disable-build-servers", "--nologo", *fixture_props], platform, work / "pack-fixture.log")
    # Execute the production generated-reference writer from the freshly built source assembly.
    generator = work / "generator"
    assembly = root / "src/Hexalith.Platform.Composition/bin/Debug/net10.0/Hexalith.Platform.Composition.dll"
    dependency_root = root / "tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0"
    write(generator / "Generator.csproj", f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><Reference Include="Hexalith.Platform.Composition" HintPath="{assembly}"/></ItemGroup></Project>')
    write(generator / "Program.cs", '''using System.Reflection;
using System.Runtime.Loader;
using Hexalith.Platform.Composition;
AssemblyLoadContext.Default.Resolving += (_, name) => AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(args[0], name.Name + ".dll"));
var extension = new PlatformEventStoreExtensionBinding("extensionprobe", "local-candidate-only", "Hexalith.Platform.TestExtension", "platform-test", 1, "Hexalith.Platform.TestExtension", args[1], []);
var type = typeof(PlatformEventStoreHostBinding).Assembly.GetType("Hexalith.Platform.Composition.PlatformEventStoreExtensionBindings", true)!;
Console.WriteLine(type.GetMethod("Write", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [new PlatformEventStoreHostBinding(args[2], [extension]), args[3]]));
''')
    bindings = work / "bindings"
    run(["dotnet", "run", "--project", str(generator / "Generator.csproj"), "--", str(dependency_root), VERSION, str(host), str(bindings)], work, work / "generate-bindings.log", 120)
    # Actual host HTTP fixture: references only the host, whose extension arrives through generated PackageReference.
    driver = platform / "candidate-driver"
    driver.mkdir()
    (driver / "GatewaySeedProtectionInstallation.cs").write_bytes((root / "tests/Hexalith.Platform.Composition.Tests/GatewaySeedProtectionInstallation.cs").read_bytes())
    write(driver / "Candidate.csproj", f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include="{host}" Aliases="platformseed"/><PackageReference Include="Microsoft.AspNetCore.Mvc.Testing"/></ItemGroup></Project>')
    write(driver / "Program.cs", '''extern alias platformseed;
using System.Text.Json;
using Hexalith.Platform.Composition.Tests;
using Hexalith.EventStore.Server.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using var installation = new GatewaySeedProtectionInstallation();
foreach (var pair in new Dictionary<string,string> { ["PackageId"]="Hexalith.Platform.TestExtension", ["ExtensionId"]="platform-test", ["ApiMajor"]="1", ["ModuleId"]="extensionprobe" })
    Environment.SetEnvironmentVariable("EventStore__Extensions__Packages__0__" + pair.Key, pair.Value);
await using var factory = new WebApplicationFactory<platformseed::Program>().WithWebHostBuilder(builder => {
    builder.UseEnvironment("Development");
    builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(installation.Configuration).AddInMemoryCollection(new Dictionary<string,string?> {
      ["APP_API_TOKEN"]="candidate-extension-channel", ["Dapr:DeclarativeSubscriptions"]="true",
      ["Authentication:JwtBearer:Issuer"]="hexalith-test", ["Authentication:JwtBearer:Audience"]="hexalith-eventstore",
      ["Authentication:JwtBearer:SigningKey"]="test-only-extension-readiness-signing-key-at-least-32-bytes",
      ["Authentication:JwtBearer:AllowedAlgorithms:0"]="HS256", ["Authentication:JwtBearer:RequireHttpsMetadata"]="false" }));
    builder.ConfigureServices(services => {
      int index = services.ToList().FindIndex(service => service.ServiceType == typeof(Hexalith.EventStore.Indexes.AdminOperationalIndexHostedService));
      if (index < 0 || services[index+2].ServiceType != typeof(IHostedService) || services[index+2].ImplementationFactory is null) throw new InvalidOperationException("Seed hosted-alias prerequisite changed");
      services.Remove(services[index+2]);
    });
});
factory.UseKestrel(0);
using var client = factory.CreateClient();
using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
using var response = await client.GetAsync("/ready", cancellation.Token);
string body = await response.Content.ReadAsStringAsync(cancellation.Token);
using var document = JsonDocument.Parse(body);
var check = document.RootElement.GetProperty("results").GetProperty("eventstore-extensions");
if (check.GetProperty("status").GetString() != "Healthy" || check.GetProperty("data").GetProperty("platform-test").GetInt32() != 1) throw new InvalidOperationException(body);
var loaded = factory.Services.GetRequiredService<EventStoreExtensionCatalog>().Extensions.Single();
var extensionAssembly = System.Reflection.Assembly.Load("Hexalith.Platform.TestExtension");
Console.WriteLine("CANDIDATE_RESULT=" + JsonSerializer.Serialize(new { Extension = loaded, Readiness = check.Clone(), ExtensionAssembly = extensionAssembly.Location,
  HttpStatus = (int)response.StatusCode, HttpTransport = "Kestrel loopback dynamic port", DistributedReadiness = false }));
''')
    build_args = ["-p:UseHexalithProjectReferences=false", "-p:RestoreAdditionalProjectSources=" + str(feed),
                  "-p:PlatformEventStoreExtensionBindings=" + str(bindings / "bindings.props")]
    print("Building actual host through generated package references", flush=True)
    run(["dotnet", "build", str(driver / "Candidate.csproj"), "-c", "Release", "--artifacts-path", str(work / "host-artifacts"),
         "-nodeReuse:false", "--disable-build-servers", "--nologo", *build_args], platform, work / "build-host.log")
    output = work / "host-artifacts/bin/Hexalith.Platform.EventStoreHost/release"
    deps = json.loads((output / "Hexalith.Platform.EventStoreHost.deps.json").read_text())
    assets_path = work / "host-artifacts/obj/Hexalith.Platform.EventStoreHost/project.assets.json"
    assets = json.loads(assets_path.read_text())
    ids = [PACKAGE] + ["Hexalith.EventStore." + name for name in SDK]
    project_libraries = {key: value for key, value in assets["libraries"].items() if value["type"] == "project"}
    assert all(key.startswith("Hexalith.Platform.") and (host.parent / value["path"]).resolve().is_relative_to(platform) for key, value in project_libraries.items()), project_libraries
    receipt["consumerProjectLibraries"] = project_libraries
    receipt["foreignProjectReferences"] = []
    receipt["resolvedLibraries"] = {}
    for package in ids:
        identity = package + "/" + VERSION
        assert deps["libraries"][identity]["type"] == "package", identity
        assert assets["libraries"][identity]["type"] == "package", identity
        receipt["resolvedLibraries"][identity] = {"depsType": "package", "assetsType": "package"}
    host_fixture = output / (PACKAGE + ".dll")
    archive = feed / (PACKAGE + "." + VERSION + ".nupkg")
    with zipfile.ZipFile(archive) as package_zip:
        archived = package_zip.read("lib/net10.0/" + PACKAGE + ".dll")
    assert hashlib.sha256(archived).hexdigest() == sha(host_fixture)
    receipt["fixtureAssemblySha256"] = sha(host_fixture)
    receipt["hostProgramSha256"] = sha(platform / "src/Hexalith.Platform.EventStoreHost/Program.cs")
    assert receipt["hostProgramSha256"] == sha(root / "src/Hexalith.Platform.EventStoreHost/Program.cs")
    receipt["packageArchives"] = {path.name: sha(path) for path in sorted(feed.glob("*.nupkg"))}
    print("Starting actual Platform Program and requesting HTTP extension readiness", flush=True)
    run(["dotnet", str(work / "host-artifacts/bin/Candidate/release/Candidate.dll")], work / "host-artifacts/bin/Candidate/release", work / "http-host.log", 90)
    result_line = next(line for line in (work / "http-host.log").read_text().splitlines() if line.startswith("CANDIDATE_RESULT="))
    receipt["http"] = json.loads(result_line.split("=", 1)[1])
    assert sha(receipt["http"]["ExtensionAssembly"]) == receipt["fixtureAssemblySha256"]
    receipt["http"]["ActualProgramAssembly"] = "Hexalith.Platform.EventStoreHost"
    receipt["passed"] = True
except Exception as error:
    receipt.update(passed=False, failure=str(error))
    raise
finally:
    (work / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n")
    print(f"Receipt: {work / 'receipt.json'}", flush=True)
