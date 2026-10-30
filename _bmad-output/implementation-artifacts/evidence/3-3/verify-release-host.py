"""Probe the actual Builds Release/package gateway against the run-owned package catalog; never manufacture missing host attestations."""
from pathlib import Path
import base64
import hashlib
import hmac
import json
import os
import secrets
import socket
import subprocess
import time
import urllib.error
import urllib.request

root = Path(__file__).resolve().parents[4]
evidence = Path(__file__).resolve().parent / "verification"
receipt = json.loads((evidence / "package-receipt.json").read_text())
workspace = Path(receipt["workspace"])
configuration = workspace / "nuget.config"
version = next(package["version"] for package in receipt["packages"] if package["id"] == "Hexalith.EventStore.Gateway")
environment = {key: value for key, value in os.environ.items() if not key.startswith(("CustomBefore", "CustomAfter", "AfterMicrosoft", "HEXALITH_SOURCE_MAPPING"))}
environment.update({"NUGET_PACKAGES": receipt["packageCache"], "DOTNET_ENVIRONMENT": "Development", "ASPNETCORE_ENVIRONMENT": "Development"})
project = root / "references/Hexalith.Builds/src/hosts/Hexalith.Builds.Module.EventStoreHost/Hexalith.Builds.Module.EventStoreHost.csproj"
artifacts = workspace / "release-host"
args = ["dotnet", "build", str(project), "-c", "Release", "-m:1", "--artifacts-path", str(artifacts), "--nologo",
        "-p:GeneratePackageOnBuild=false", "-p:UseHexalithProjectReferences=false", "-p:UseNuGetDeps=true", "-p:HexalithEventStoreFromSource=false",
        "-p:HexalithEventStoreVersion=" + version, "-p:HexalithBuildsRoot=" + str(root / "references/Hexalith.Builds"), "-p:RestoreConfigFile=" + str(configuration)]
(evidence / "release-host-command.json").write_text(json.dumps({"argv": args, "feed": receipt["feed"], "packageCache": receipt["packageCache"], "runtimeEnvironment": "Development"}, indent=2))
build = subprocess.run(args, cwd=root, env=environment, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
(evidence / "release-host-build.txt").write_text(build.stdout)
print("Release package host build:", build.returncode, flush=True)
if build.returncode:
    print(build.stdout[-10000:])
    raise SystemExit(build.returncode)
assets = json.loads((artifacts / "obj/Hexalith.Builds.Module.EventStoreHost/project.assets.json").read_text())
assert all(library["type"] == "package" for library in assets["libraries"].values())
catalog_directory = workspace / "catalog"
catalog = json.loads((catalog_directory / "catalog.json").read_text())
catalog_receipt = json.loads((catalog_directory / "receipt.json").read_text())
with socket.socket() as reservation:
    reservation.bind(("127.0.0.1", 0))
    port = reservation.getsockname()[1]
with socket.socket() as reservation:
    reservation.bind(("127.0.0.1", 0))
    absent_sidecar_port = reservation.getsockname()[1]
with socket.socket() as reservation:
    reservation.bind(("127.0.0.1", 0))
    absent_grpc_port = reservation.getsockname()[1]
hosts = sorted({"eventstore", "tenants-api", "memories", *(route["appId"] for route in catalog["routes"])})
component = catalog_directory / "authority-component.json"
component.write_text(json.dumps({"apiVersion": "dapr.io/v1alpha1", "kind": "Component", "metadata": {"name": "routing-package-probe"},
    "spec": {"type": "state.redis", "version": "v1", "metadata": [{"name": "keyPrefix", "value": "none"}]}, "scopes": hosts}))
acl = catalog_directory / "receiver-acl.json"
acl.write_text(json.dumps({"apiVersion": "dapr.io/v1alpha1", "kind": "Configuration", "metadata": {"name": "routing-package-probe"},
    "spec": {"accessControl": {"defaultAction": "deny", "trustDomain": "public", "policies": []}}}))
signing = secrets.token_urlsafe(48)
environment.update({"ASPNETCORE_URLS": f"http://127.0.0.1:{port}", "DAPR_HTTP_ENDPOINT": f"http://127.0.0.1:{absent_sidecar_port}",
    "DAPR_GRPC_ENDPOINT": f"http://127.0.0.1:{absent_grpc_port}", "DAPR_HTTP_PORT": str(absent_sidecar_port), "DAPR_GRPC_PORT": str(absent_grpc_port),
    "APP_API_TOKEN": secrets.token_urlsafe(32), "DAPR_API_TOKEN": secrets.token_urlsafe(32),
    "Authentication__JwtBearer__Issuer": "catalog-package-probe", "Authentication__JwtBearer__Audience": "catalog-package-probe",
    "Authentication__JwtBearer__SigningKey": signing, "EventStore__IdempotencyAdmission__Enabled": "true",
    "EventStore__IdempotencyAdmission__DigestKeySource": "Configuration", "EventStore__IdempotencyAdmission__ActiveDigestKeyVersion": "development-v1",
    "EventStore__IdempotencyAdmission__DigestKeys__development-v1": base64.b64encode(secrets.token_bytes(32)).decode(),
    "EventStore__IdempotencyAdmission__DigestKeySecretStoreName": "secretstore", "EventStore__IdempotencyAdmission__DigestKeySecretName": "eventstore-idempotency-ring",
    "EventStore__IdempotencyAdmission__DigestKeySecretGeneration": "development-v1", "EventStore__RouteCatalog__StateStore": "routing-package-probe",
    "EventStore__RouteCatalog__StateKey": catalog_receipt["instanceId"] + "-authority"})
prefix = "EventStore__RouteCatalog__DevelopmentBootstrap__"
for key, value in {"Directory": str(catalog_directory), "HostId": "eventstore", "CoordinatorHostId": "eventstore", "AttemptId": workspace.name,
    "InstanceId": catalog_receipt["instanceId"], "CatalogPath": str(catalog_directory / "catalog.json"), "RootDigest": catalog_receipt["rootDigest"],
    "ComponentPath": str(component), "AccessControlPath": str(acl)}.items():
    environment[prefix + key] = value
for index, host in enumerate(hosts):
    environment[prefix + "RequiredHostIds__" + str(index)] = host
for key in ["ownerAssemblies", "adapterAssemblies"]:
    for index, path in enumerate(catalog_receipt[key]):
        environment[prefix + key[0].upper() + key[1:] + "__" + str(index)] = path


def encode(value):
    return base64.urlsafe_b64encode(json.dumps(value, separators=(",", ":")).encode()).decode().rstrip("=")


message = encode({"alg": "HS256", "typ": "JWT"}) + "." + encode({"iss": "catalog-package-probe", "aud": "catalog-package-probe", "sub": "probe", "eventstore:surface": "agent", "exp": int(time.time()) + 120})
token = message + "." + base64.urlsafe_b64encode(hmac.new(signing.encode(), message.encode(), hashlib.sha256).digest()).decode().rstrip("=")
assembly = artifacts / "bin/Hexalith.Builds.Module.EventStoreHost/release/Hexalith.Builds.Module.EventStoreHost.dll"
log = workspace / "release-host-runtime.txt"
with log.open("w") as output:
    process = subprocess.Popen(["dotnet", str(assembly)], cwd=assembly.parent, env=environment, stdout=output, stderr=subprocess.STDOUT)
    try:
        status = None
        for _ in range(120):
            if process.poll() is not None:
                break
            try:
                request = urllib.request.Request(f"http://127.0.0.1:{port}/api/v1/routing/catalog", headers={"Authorization": "Bearer " + token})
                with urllib.request.urlopen(request, timeout=2) as response:
                    status = response.status
            except urllib.error.HTTPError as failure:
                status = failure.code
            except (OSError, TimeoutError):
                time.sleep(0.25)
                continue
            break
        result = {"hostBuiltFromPackagesOnly": True, "hostAssembly": str(assembly), "hostSha256": hashlib.sha256(assembly.read_bytes()).hexdigest(),
                  "runtimeEnvironment": "Development", "instanceId": catalog_receipt["instanceId"], "rootDigest": catalog_receipt["rootDigest"],
                  "requiredHosts": hosts, "httpMetadataStatus": status, "processExit": process.poll(),
                  "gatewayObservationPresent": (catalog_directory / "eventstore.observation.json").exists(),
                  "committed": (catalog_directory / "eventstore.committed.json").exists(),
                  "qualification": "Release/package gateway binding and fail-closed probe only; real owner cohort and sidecars are absent, so package activation/readiness is not established"}
        (evidence / "release-host-receipt.json").write_text(json.dumps(result, indent=2) + "\n")
        print(json.dumps(result), flush=True)
    finally:
        if process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=15)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)
safe = log.read_text().replace(token, "[redacted]").replace(signing, "[redacted]")
(evidence / "release-host-runtime.txt").write_text(safe)
