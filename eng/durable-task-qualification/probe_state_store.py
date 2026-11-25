#!/usr/bin/env python3
"""Run G-1 provider and native admission checks on one private Docker network."""

from concurrent.futures import ThreadPoolExecutor
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import time
from urllib.error import HTTPError
from urllib.request import ProxyHandler, Request, build_opener
from uuid import uuid4


DAPR = "daprio/daprd@sha256:ed471547b67db50c831d498b8ee861822fb037d9d594c38ac299546b425f575a"
REDIS = "redis@sha256:7f7d30b7fdd09467bac58198a1f166dd69789d348d4fe859cccb20e0cf287fbe"
PYTHON = "python@sha256:4766d8b510c428e595d74b9cc5bbb2fae8e26316fffb4adc89908d79aacd58a2"
DOTNET = "mcr.microsoft.com/dotnet/sdk@sha256:65f3c7c6a25a239ab3e6719f2896147e7b68242ff2d8818dd70498bde6ef8bea"
NATIVE_CHECKS = "Hexalith.EventStore.DomainService.Tests.DurableTasks.DurableTaskAdmissionLiveTests"
STATE_PATH = "/v1.0/state/g1-task-state"
ROW_PATH = f"{STATE_PATH}/feasibility?consistency=strong"


def docker(*arguments: str, timeout: int = 30) -> str:
    result = subprocess.run(
        ["docker", *arguments], capture_output=True, text=True, timeout=timeout, check=False
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


def run_fixture(component_dir: Path, native_assembly: Path | None) -> dict:
    network = docker("network", "create", "--internal", f"g1-state-{uuid4().hex}")
    containers = []

    def start(*arguments: str, attached: bool = False) -> tuple[str, str]:
        container = docker("create", "--pull", "never", "--network", network, *arguments)
        containers.append(container)
        options = ("--attach",) if attached else ()
        output = docker("start", *options, container, timeout=120 if attached else 30)
        return container, output

    def replica(alias: str) -> str:
        container, _ = start(
            "--network-alias", alias, "--volume", f"{component_dir}:/components:ro",
            DAPR, "/daprd", "--app-id", "g1-tasks", "--enable-mtls=false",
            "--dapr-listen-addresses", "0.0.0.0", "--resources-path", "/components",
        )
        return container

    def observe(*arguments: str) -> str:
        _, output = start("--volume", f"{Path(__file__).resolve()}:/probe.py:ro",
                          PYTHON, "python", "/probe.py", *arguments, attached=True)
        return output

    try:
        redis, _ = start(
            "--network-alias", "redis", REDIS,
            "redis-server", "--appendonly", "yes", "--appendfsync", "always",
        )
        original = [replica("replica-a"), replica("replica-b")]
        committed = json.loads(observe("--check-concurrency"))
        for sidecar in original:
            docker("rm", "--force", "--volumes", sidecar)
            containers.remove(sidecar)
        docker("restart", "--time", "3", redis)
        replica("replica-a")
        replica("replica-b")
        observe("--check-restart", json.dumps(committed))
        if native_assembly is not None:
            _, output = start(
                "--volume", f"{native_assembly.parent}:/tests:ro",
                "--env", "HEXALITH_G1_PRIMARY_GRPC=http://replica-a:50001",
                "--env", "HEXALITH_G1_REPLICA_GRPC=http://replica-b:50001",
                DOTNET, "dotnet", f"/tests/{native_assembly.name}",
                "-class", NATIVE_CHECKS, "-failSkips", attached=True,
            )
            print(output)
        return {"dapr": DAPR, "redis": REDIS, "firstWriteStatuses": committed["firstWriteStatuses"],
                "staleWriteRejected": True, "persistedFenceAfterRestart": committed["expectedRow"]["fence"]}
    finally:
        failures = []
        for container in reversed(containers):
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


def main() -> None:
    arguments = sys.argv[1:]
    if arguments == ["--check-concurrency"]:
        print(json.dumps(check_concurrency()))
        return
    if len(arguments) == 2 and arguments[0] == "--check-restart":
        check_restart(json.loads(arguments[1]))
        return
    if arguments and (len(arguments) != 2 or arguments[0] != "--native"):
        sys.exit("Usage: probe_state_store.py [--native EventStore-test-assembly.dll]")
    assembly = Path(arguments[1]).resolve(strict=True) if arguments else None
    with tempfile.TemporaryDirectory() as directory:
        components = Path(directory)
        components.chmod(0o755)
        (components / "task-state.yaml").write_text("""\
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
      value: appid
""", encoding="utf-8")
        print(json.dumps(run_fixture(components, assembly), sort_keys=True))


if __name__ == "__main__":
    main()
