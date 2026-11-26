#!/usr/bin/env python3
"""Run G-1 provider and native Durable Task checks on one private Docker network."""

from argparse import ArgumentParser
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
from hashlib import sha256
import json
import os
from pathlib import Path
from secrets import token_hex
import ssl
import subprocess
import sys
import tempfile
import time
from urllib.error import HTTPError
from urllib.request import HTTPSHandler, ProxyHandler, Request, build_opener
from uuid import uuid4


DAPR = "daprio/daprd@sha256:ed471547b67db50c831d498b8ee861822fb037d9d594c38ac299546b425f575a"
REDIS = "redis@sha256:7f7d30b7fdd09467bac58198a1f166dd69789d348d4fe859cccb20e0cf287fbe"
PYTHON = "python@sha256:4766d8b510c428e595d74b9cc5bbb2fae8e26316fffb4adc89908d79aacd58a2"
DOTNET = "mcr.microsoft.com/dotnet/sdk@sha256:65f3c7c6a25a239ab3e6719f2896147e7b68242ff2d8818dd70498bde6ef8bea"
NATIVE_CHECKS = "Hexalith.EventStore.DomainService.Tests.DurableTasks.DurableTaskAdmissionLiveTests"
EXECUTION_CHECKS = "Hexalith.EventStore.DomainService.Tests.DurableTasks.DurableTaskExecutionLiveTests"
STATE_PATH = "/v1.0/state/g1-task-state"
ROW_PATH = f"{STATE_PATH}/feasibility?consistency=strong"
ISSUER = "https://g1-issuer:8443/workload-authority"
AUDIENCE = "g1-tasks"
LOOKUP_PATH = f"/v1.0/invoke/{AUDIENCE}/method/internal/durable-tasks/v1/steps/lookup"
WORKLOADS = ("sample-owner", "other-owner", "untrusted-owner")


def docker(*arguments: str, timeout: int = 30, environment: dict[str, str] | None = None) -> str:
    result = subprocess.run(
        ["docker", *arguments], capture_output=True, text=True, timeout=timeout, check=False,
        env=os.environ | environment if environment is not None else None,
    )
    if result.returncode:
        details = (result.stdout + result.stderr).strip()
        raise RuntimeError(f"docker {arguments[0]} exited {result.returncode}: {details}")
    return result.stdout.strip()


def request(replica: str, path: str, payload: object = None) -> tuple[int, str | None, bytes]:
    message = Request(
        f"http://{replica}:3500{path}",
        data=None if payload is None else json.dumps(payload).encode(),
        headers={"Content-Type": "application/json"},
    )
    try:
        with build_opener(ProxyHandler({})).open(message, timeout=3) as response:
            return response.status, response.headers.get("ETag"), response.read()
    except HTTPError as error:
        return error.code, error.headers.get("ETag"), error.read()


def save(replica: str, etag: str, value: dict) -> int:
    status, _, _ = request(replica, STATE_PATH, [{
        "key": "feasibility", "etag": etag, "value": value,
        "options": {"concurrency": "first-write", "consistency": "strong"},
    }])
    return status


def wait_for_sidecar(replica: str) -> None:
    expires = time.monotonic() + 15
    while time.monotonic() < expires:
        try:
            if request(replica, "/v1.0/healthz/outbound")[0] == 204:
                return
        except OSError:
            pass
        time.sleep(0.2)
    raise RuntimeError(f"Dapr replica {replica} did not become ready")


def check_concurrency() -> dict:
    first, second = "replica-a", "replica-b"
    wait_for_sidecar(first)
    wait_for_sidecar(second)
    absent, _, _ = request(first, ROW_PATH)
    if absent != 204:
        raise RuntimeError(f"Expected an empty store, got HTTP {absent}")

    with ThreadPoolExecutor(max_workers=2) as workers:
        writes = [workers.submit(save, replica, "", {"writer": replica, "fence": 1})
                  for replica in (first, second)]
        inserts = [write.result() for write in writes]
    if sorted(inserts) != [204, 409]:
        raise RuntimeError(f"First-write concurrency returned HTTP {inserts}")

    status, old_etag, row = request(first, ROW_PATH)
    if status != 200 or not old_etag or request(second, ROW_PATH)[2] != row:
        raise RuntimeError("The two replicas disagree about the committed row")
    amended = {**json.loads(row), "fence": 2}
    if save(second, old_etag, amended) != 204 or save(first, old_etag, {"fence": 1}) != 409:
        raise RuntimeError("The new ETag did not fence the stale writer")
    return {"firstWriteStatuses": inserts, "expectedRow": amended, "oldETag": old_etag}


def check_restart(expected: dict) -> None:
    wait_for_sidecar("replica-a")
    wait_for_sidecar("replica-b")
    for replica in ("replica-a", "replica-b"):
        status, etag, row = request(replica, ROW_PATH)
        if status != 200 or not etag or etag == expected["oldETag"] or json.loads(row) != expected["expectedRow"]:
            raise RuntimeError(f"{replica} did not recover the persisted value after Redis restarted")


def check_lookup_readiness() -> dict:
    wait_for_sidecar("replica-a")
    wait_for_sidecar("replica-b")
    tls = ssl.create_default_context(cafile="/g1-ca.pem")
    opener = build_opener(ProxyHandler({}), HTTPSHandler(context=tls))
    until = time.monotonic() + 15
    while time.monotonic() < until:
        try:
            with opener.open(ISSUER + "/.well-known/openid-configuration", timeout=3) as response:
                metadata = json.load(response)
            if metadata.get("issuer") != ISSUER or metadata.get("jwks_uri") != ISSUER + "/jwks":
                raise RuntimeError("Unexpected G-1 workload issuer discovery metadata")
            with opener.open(metadata["jwks_uri"], timeout=3) as response:
                keys = json.load(response)["keys"]
            if len(keys) != 1 or keys[0].get("alg") != "RS256":
                raise RuntimeError("The G-1 issuer did not publish the expected RS256 key")
            status, _, _ = request("replica-b", f"/v1.0/invoke/{AUDIENCE}/method/alive")
            if status == 200:
                return {"issuer": ISSUER, "signingAlgorithm": "RS256", "invocationHealthStatus": status}
        except OSError:
            pass
        time.sleep(0.2)
    raise RuntimeError("G-1 lookup host or its HTTPS workload issuer did not become ready")


def create_issuer_certificate(directory: Path, password: str) -> None:
    (directory / "tls.ext").write_text(
        "basicConstraints=critical,CA:FALSE\nkeyUsage=critical,digitalSignature,keyEncipherment\n"
        "extendedKeyUsage=serverAuth\nsubjectAltName=DNS:g1-issuer\n", encoding="utf-8",
    )
    commands = (
        ("req", "-x509", "-newkey", "rsa:2048", "-nodes", "-days", "1", "-subj", "/CN=G-1 Fixture CA",
         "-addext", "basicConstraints=critical,CA:TRUE", "-addext", "keyUsage=critical,keyCertSign",
         "-keyout", "ca.key", "-out", "ca.pem"),
        ("req", "-newkey", "rsa:2048", "-nodes", "-subj", "/CN=g1-issuer", "-keyout", "issuer.key", "-out", "issuer.csr"),
        ("x509", "-req", "-in", "issuer.csr", "-CA", "ca.pem", "-CAkey", "ca.key", "-CAcreateserial",
         "-days", "1", "-extfile", "tls.ext", "-out", "issuer.pem"),
        ("pkcs12", "-export", "-in", "issuer.pem", "-inkey", "issuer.key", "-out", "issuer.pfx",
         "-passout", "env:HEXALITH_G1_PFX_PASSWORD"),
    )
    for command in commands:
        result = subprocess.run(["openssl", *command], cwd=directory, capture_output=True, text=True,
                                env=os.environ | {"HEXALITH_G1_PFX_PASSWORD": password}, timeout=30)
        if result.returncode:
            raise RuntimeError(f"openssl {command[0]} failed: {result.stderr.strip()}")


def issuer_environment(secrets: dict[str, str], password: str) -> dict[str, str]:
    authority = "Authentication__WorkloadAuthority__"
    environment = {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ASPNETCORE_URLS": "https://+:8443",
        "Kestrel__Certificates__Default__Path": "/issuer.pfx",
        "Kestrel__Certificates__Default__Password": password,
        f"{authority}Enabled": "true",
        f"{authority}NonconformingDevelopmentFixture": "true",
        f"{authority}Issuer": ISSUER,
        f"{authority}KeyId": "g1-qualification-issuer",
        f"{authority}SigningCertificatePath": "/issuer.pfx",
        f"{authority}SigningCertificatePassword": password,
    }
    now = datetime.now(timezone.utc)
    for ordinal, (workload, secret) in enumerate(secrets.items()):
        grant = f"{authority}Grants__{ordinal}__"
        values = {
            "GrantId": f"g1-qualification-{workload}", "Revision": "1", "Surface": "service",
            "ClientId": workload, "Workload": workload, "Audience": AUDIENCE,
            "ClientSecretSha256": sha256(secret.encode()).hexdigest().upper(),
            "NotBefore": (now - timedelta(minutes=5)).isoformat(), "ExpiresAt": (now + timedelta(hours=1)).isoformat(),
            "Operations__0": "eventstore:durable-task-step-lookup", "Operations__1": "domain-service:query",
            "Bindings__eventstore:bound-tenant__0": "tenant", "Bindings__eventstore:bound-tenant__1": "foreign-tenant",
            "Bindings__eventstore:bound-domain__0": "sample-owner-domain", "Bindings__eventstore:bound-domain__1": "foreign-domain",
        }
        environment.update({grant + key: value for key, value in values.items()})
    return environment


def run_fixture(
    component_dir: Path,
    native_assembly: Path | None,
    verify_execution: bool,
    evidence_directory: Path | None,
    lookup_host: Path | None,
) -> dict:
    network = docker("network", "create", "--internal", f"g1-state-{uuid4().hex}")
    containers = []

    logs = {}
    channel_token = token_hex(32) if lookup_host is not None else ""
    workload_environment = {
        "Authentication__WorkloadJwtBearer__Authority": ISSUER,
        "Authentication__WorkloadJwtBearer__Issuer": ISSUER,
        "Authentication__WorkloadJwtBearer__Audience": AUDIENCE,
        "Authentication__WorkloadJwtBearer__RequireHttpsMetadata": "true",
        "Authentication__WorkloadJwtBearer__AllowedAlgorithms__0": "RS256",
        "SSL_CERT_FILE": "/g1-ca.pem",
    }
    secrets = {workload: token_hex(32) for workload in WORKLOADS} if lookup_host is not None else {}
    trust_mount = ("--volume", f"{component_dir.parent / 'ca.pem'}:/g1-ca.pem:ro")

    def start(*arguments: str, attached: bool = False, environment: dict[str, str] | None = None) -> tuple[str, str]:
        variables = [entry for key in environment or {} for entry in ("--env", key)]
        container = docker("create", "--pull", "never", "--network", network, *variables, *arguments,
                           environment=environment)
        containers.append(container)
        options = ("--attach",) if attached else ()
        output = docker("start", *options, container, timeout=120 if attached else 30)
        return container, output

    def replica(alias: str, generation: str) -> str:
        receiver = alias == "replica-a"
        app_id = AUDIENCE if lookup_host is None or receiver else "sample-owner"
        network_flags = ("--network-alias", f"{app_id}-dapr") if lookup_host is not None else ()
        discovery = ("--config", "/components/discovery.yaml", "--dapr-internal-grpc-port", "50002") if lookup_host is not None else ()
        channel = ("--app-protocol", "http", "--app-port", "8080", "--app-channel-address", "g1-host") if lookup_host is not None and receiver else ()
        container, _ = start(
            "--network-alias", alias, *network_flags, "--volume", f"{component_dir}:/components:ro",
            DAPR, "/daprd", "--app-id", app_id, "--enable-mtls=false",
            "--dapr-listen-addresses", "0.0.0.0", "--resources-path", "/components/state", *discovery, *channel,
            environment={"APP_API_TOKEN": channel_token} if channel else None,
        )
        if lookup_host is not None:
            logs[container] = f"{alias}-{generation}"
        return container

    def observe(*arguments: str) -> str:
        mount = trust_mount if lookup_host is not None else ()
        _, output = start("--volume", f"{Path(__file__).resolve()}:/probe.py:ro",
                          *mount, PYTHON, "python", "/probe.py", *arguments, attached=True)
        return output

    def native(filter_name: str, selector: str, phase: str) -> None:
        assert native_assembly is not None
        result = "/g1-native.xml"
        environment = {"HEXALITH_G1_PRIMARY_GRPC": "http://replica-a:50001", "HEXALITH_G1_REPLICA_GRPC": "http://replica-b:50001"}
        mount = ()
        if lookup_host is not None and phase != "admission":
            mount = trust_mount
            environment.update(workload_environment)
            environment.update({"HEXALITH_G1_OWNER_HTTP": "http://replica-b:3500", "HEXALITH_G1_HOST_HTTP": "http://g1-host:8080"})
            environment.update({f"HEXALITH_G1_{workload.upper().replace('-', '_')}_SECRET": secret
                                for workload, secret in secrets.items()})
        container, output = start(
            "--volume", f"{native_assembly.parent}:/tests:ro", *mount,
            DOTNET, "dotnet", f"/tests/{native_assembly.name}",
            filter_name, selector, "-failSkips", "-result-xml", result, attached=True, environment=environment,
        )
        if evidence_directory is not None:
            docker("cp", f"{container}:{result}", str(evidence_directory / f"{phase}.xml"))
        print(output)

    try:
        if lookup_host is not None:
            certificate_password = token_hex(32)
            create_issuer_certificate(component_dir.parent, certificate_password)
            host_mount = ("--volume", f"{lookup_host.parent}:/host:ro")
            issuer, _ = start(
                "--network-alias", "g1-issuer", *host_mount,
                "--volume", f"{component_dir.parent / 'issuer.pfx'}:/issuer.pfx:ro",
                DOTNET, "dotnet", f"/host/{lookup_host.name}", "authority",
                environment=issuer_environment(secrets, certificate_password),
            )
            host, _ = start(
                "--network-alias", "g1-host", *host_mount, *trust_mount, DOTNET, "dotnet", f"/host/{lookup_host.name}",
                environment=workload_environment | {
                    "ASPNETCORE_ENVIRONMENT": "Development", "ASPNETCORE_URLS": "http://+:8080",
                    "HEXALITH_G1_PRIMARY_GRPC": "http://replica-a:50001", "APP_API_TOKEN": channel_token,
                    "Authentication__DurableTaskStepLookup__Audience": AUDIENCE,
                    "Authentication__DurableTaskStepLookup__AllowedCallers__0": "sample-owner",
                    "Authentication__DurableTaskStepLookup__AllowedCallers__1": "other-owner",
                },
            )
            logs.update({issuer: "workload-issuer", host: "lookup-host"})
        redis, _ = start(
            "--network-alias", "redis", REDIS,
            "redis-server", "--appendonly", "yes", "--appendfsync", "always",
        )
        original = [replica("replica-a", "before-restart"), replica("replica-b", "before-restart")]
        committed = json.loads(observe("--check-concurrency"))
        if lookup_host is not None:
            before = json.loads(observe("--check-lookup-readiness"))
            redis_started_before = docker("inspect", "--format", "{{.State.StartedAt}}", redis)
            logs[redis] = "redis"
        if verify_execution:
            native("-method", f"{EXECUTION_CHECKS}.CommitWithLostOwnerReply", "execution-before-restart")
        for sidecar in original:
            if lookup_host is not None:
                retain_log(sidecar, logs.pop(sidecar), evidence_directory)
            docker("rm", "--force", "--volumes", sidecar)
            containers.remove(sidecar)
        docker("restart", "--time", "3", redis)
        replacement = [replica("replica-a", "after-restart"), replica("replica-b", "after-restart")]
        observe("--check-restart", json.dumps(committed))
        if lookup_host is not None:
            redis_started_after = docker("inspect", "--format", "{{.State.StartedAt}}", redis)
            if redis_started_before == redis_started_after or set(original).intersection(replacement):
                raise RuntimeError("Expected Redis restart and two replacement Dapr sidecars")
            after = json.loads(observe("--check-lookup-readiness"))
        if verify_execution:
            native("-method", f"{EXECUTION_CHECKS}.RecoverCommittedReceiptAfterRestart", "execution-after-restart")
        if native_assembly is not None:
            native("-class", NATIVE_CHECKS, "admission")
        result = {"dapr": DAPR, "redis": REDIS, "firstWriteStatuses": committed["firstWriteStatuses"],
                  "staleWriteRejected": True, "persistedFenceAfterRestart": committed["expectedRow"]["fence"]}
        if lookup_host is not None:
            result.update({
                "lookupPath": LOOKUP_PATH, "ownerSidecarAppId": "sample-owner", "lookupSidecarAppId": AUDIENCE,
                "lookupReadinessBeforeRestart": before, "lookupReadinessAfterRestart": after,
                "sidecarsBeforeRestart": original, "sidecarsAfterRestart": replacement,
                "redisContainer": redis, "redisStartedBefore": redis_started_before, "redisStartedAfter": redis_started_after,
                "daprVersions": [docker("exec", sidecar, "/daprd", "--version") for sidecar in replacement],
                "redisVersion": docker("exec", redis, "redis-server", "--version"),
                "dotnetSdk": docker("exec", host, "dotnet", "--version"),
                "dotnetRuntimes": docker("exec", host, "dotnet", "--list-runtimes").splitlines(),
                "dotnetImage": DOTNET, "probeImage": PYTHON,
            })
            assert evidence_directory is not None
            (evidence_directory / "runtime-topology.json").write_text(
                json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8",
            )
        return result
    finally:
        failures = []
        for container in reversed(containers):
            try:
                if container in logs:
                    retain_log(container, logs[container], evidence_directory)
            except (OSError, RuntimeError, subprocess.TimeoutExpired) as error:
                failures.append(str(error))
            try:
                docker("rm", "--force", "--volumes", container)
            except (RuntimeError, subprocess.TimeoutExpired) as error:
                failures.append(str(error))
        try:
            docker("network", "rm", network)
        except (RuntimeError, subprocess.TimeoutExpired) as error:
            failures.append(str(error))
        if failures:
            raise RuntimeError("; ".join(failures))


def retain_log(container: str, name: str, evidence_directory: Path | None) -> None:
    assert evidence_directory is not None
    (evidence_directory / f"{name}.log").write_text(docker("logs", container) + "\n", encoding="utf-8")


def main() -> None:
    arguments = sys.argv[1:]
    if arguments == ["--check-concurrency"]:
        print(json.dumps(check_concurrency()))
        return
    if len(arguments) == 2 and arguments[0] == "--check-restart":
        check_restart(json.loads(arguments[1]))
        return
    if arguments == ["--check-lookup-readiness"]:
        print(json.dumps(check_lookup_readiness()))
        return
    parser = ArgumentParser(description=__doc__)
    tests = parser.add_mutually_exclusive_group()
    tests.add_argument("--native", type=Path, help="Run the EventStore native admission suite from this test assembly.")
    tests.add_argument("--native-execution", type=Path, help="Run native admission and both execution restart phases.")
    tests.add_argument("--authenticated-lookup", type=Path, help="Run native execution phases through a standalone Dapr lookup host.")
    parser.add_argument("--lookup-host", type=Path, help="The standalone G-1 lookup host assembly for --authenticated-lookup.")
    parser.add_argument("--evidence-directory", type=Path, help="Directory for the passing native xUnit receipts.")
    options = parser.parse_args(arguments)
    assembly = options.native or options.native_execution or options.authenticated_lookup
    if options.authenticated_lookup is not None:
        if options.lookup_host is None or options.evidence_directory is None:
            parser.error("--authenticated-lookup requires --lookup-host and --evidence-directory")
    elif options.lookup_host is not None:
        parser.error("--lookup-host requires --authenticated-lookup")
    if options.evidence_directory is not None:
        if assembly is None:
            parser.error("--evidence-directory requires a native test assembly")
        options.evidence_directory.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as directory:
        components = Path(directory) / "components"
        state = components / "state"
        state.mkdir(parents=True)
        key_prefix = "name" if options.authenticated_lookup is not None else "appid"
        (state / "task-state.yaml").write_text(f"""\
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: g1-task-state
spec:
  type: state.redis
  version: v1
  metadata:
    - name: redisHost
      value: redis:6379
    - name: keyPrefix
      value: {key_prefix}
""", encoding="utf-8")
        if options.authenticated_lookup is not None:
            (components / "discovery.yaml").write_text("""\
apiVersion: dapr.io/v1alpha1
kind: Configuration
metadata:
  name: g1-lookup-discovery
spec:
  nameResolution:
    component: kubernetes
    version: v1
    configuration:
      template: '{{.ID}}-dapr:50002'
""", encoding="utf-8")
        print(json.dumps(run_fixture(
            components,
            assembly.resolve(strict=True) if assembly is not None else None,
            options.native_execution is not None or options.authenticated_lookup is not None,
            options.evidence_directory,
            options.lookup_host.resolve(strict=True) if options.lookup_host is not None else None,
        ), sort_keys=True))


if __name__ == "__main__":
    main()
