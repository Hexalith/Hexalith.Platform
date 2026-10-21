#!/usr/bin/env python3
"""Run only owned local synthetic F1 processes; retain all attempts externally."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import tempfile
import time
import urllib.request

from protocol import verify as verify_protocol

ROOT = Path(__file__).resolve().parents[3]
DOCS = ROOT / "docs/implementation/ext-secrets-1-f1-2026-10-20"


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write(path, value):
    Path(path).write_text(json.dumps(value, indent=2, sort_keys=True) + "\n")


def rows(path):
    return [json.loads(line) for line in Path(path).read_text().splitlines() if line]


def port():
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        return sock.getsockname()[1]


def launch_bound(argv, environment, cwd, stream):
    executable = Path(argv[0]) if Path(argv[0]).is_absolute() else Path(shutil.which(str(argv[0])))
    # The kernel executes this opened inode, so even a process that exits before
    # /proc/PID/exe can be opened is bound to the measured executable bytes.
    with executable.open("rb") as image:
        digest = hashlib.file_digest(image, "sha256").hexdigest()
        image.seek(0)
        stat = os.fstat(image.fileno())
        process = subprocess.Popen(list(map(str, argv)), executable=f"/proc/self/fd/{image.fileno()}", pass_fds=(image.fileno(),),
                                   env=environment, cwd=cwd, stdout=stream, stderr=subprocess.STDOUT, start_new_session=True)
        process._f1_image_fd = os.dup(image.fileno())
        binding = {"pid": process.pid, "startTicks": Path(f"/proc/{process.pid}/stat").read_text().split()[21],
                   "executionImageSha256": digest, "imageBinding": "opened-executable-fd",
                   "executableFile": str(executable), "executableDevice": stat.st_dev, "executableInode": stat.st_ino}
    return process, binding


def terminal_binding(process, binding):
    # wait()/poll() is the OS terminal observation for this owned PID. The same
    # opened image inode remains available even after /proc/PID/exe disappears.
    with os.fdopen(process._f1_image_fd, "rb") as image:
        image.seek(0)
        digest = hashlib.file_digest(image, "sha256").hexdigest()
        stat = os.fstat(image.fileno())
    return {"terminalPid": process.pid, "terminalStartTicks": binding["startTicks"],
            "terminalImageSha256": digest, "terminalExecutableDevice": stat.st_dev, "terminalExecutableInode": stat.st_ino,
            "terminalObservation": "waitpid-owned-child-and-opened-image-fd"}


def run(build, assembly, output):
    build, assembly, output = map(lambda p: Path(p).resolve(), (build, assembly, output))
    if output.exists() or output.is_relative_to(ROOT):
        raise ValueError("Fresh detached output required; prior failed attempts must be retained")
    output.mkdir(parents=True, mode=0o700)
    sockets = Path(tempfile.mkdtemp(prefix="f1-local-"))
    sockets.chmod(0o700)
    discovery = sockets / "discovery"
    discovery.mkdir(mode=0o700)
    resources = output / "resources"
    resources.mkdir()
    (resources / "binding.yaml").write_text("""apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: synthetic-configured-role
spec:
  type: bindings.f1fixture
  version: v1
  metadata: []
""")
    (resources / "resiliency.yaml").write_text("""apiVersion: dapr.io/v1alpha1
kind: Resiliency
metadata:
  name: f1-local-no-retry
spec:
  policies:
    retries:
      f1NoRetry:
        policy: constant
        duration: 0s
        maxRetries: 0
  targets:
    components:
      synthetic-configured-role:
        outbound:
          retry: f1NoRetry
""")
    config = output / "configuration.yaml"
    config.write_text("""apiVersion: dapr.io/v1alpha1
kind: Configuration
metadata:
  name: f1-local-synthetic
spec:
  tracing:
    samplingRate: "0"
  metric:
    enabled: false
""")
    binary = build / "bin/f1fixture"
    daprd = build / "bin/daprd-stable"
    environment = dict(os.environ, DAPR_COMPONENTS_SOCKETS_FOLDER=str(discovery), GOMAXPROCS="4")
    for name in ("DAPR_API_TOKEN", "APP_API_TOKEN", "EXT_SECRETS_1_F1_EVIDENCE", "EXT_SECRETS_1_LOCAL_TRANSPORT"):
        environment.pop(name, None)
    processes, commands = [], []

    def start(name, argv, extra=None):
        stream = (output / (name + ".log")).open("wb")
        process, binding = launch_bound(argv, dict(environment, **(extra or {})), ROOT, stream)
        processes.append((name, process, stream, binding))
        commands.append({"name": name, "argv": list(map(str, argv)), **binding,
                         "sdkDriverSha256": sha(assembly) if name.startswith("sdk-") else None})
        write(output / "commands.json", commands)
        return process

    def execute(name, argv, extra=None):
        process = start(name, argv, extra)
        code = process.wait(timeout=180)
        if code:
            raise ValueError(f"{name} failed ({code}); logs retained at {output}")

    def ready(path, process):
        for _ in range(300):
            if process.poll() is not None:
                raise ValueError(f"Owned process failed before {path}")
            if Path(path).exists():
                return
            time.sleep(0.1)
        raise ValueError(f"Socket readiness timeout: {path}")

    probe_index = 0
    probes = []

    def probe(name, hop="api", data=0, metadata=None, operation="fixture-send", protobuf=0, raw_headers=None, compressed=False, nested=False):
        nonlocal probe_index
        probe_index += 1
        target = f"127.0.0.1:{api_proxy}" if hop == "api" else str(discovery / "f1fixture.sock")
        log = output / f"probe-{probe_index:03d}-{name}.jsonl"
        argv = [binary, "--mode", "raw-probe" if raw_headers is not None or compressed else "probe", "--target", target,
                "--hop", hop, "--log", log, "--incarnation", name]
        if raw_headers is not None:
            argv += ["--header-list-bytes", str(raw_headers)]
        if compressed:
            argv += ["--compressed"]
        if nested:
            argv += ["--nested-unknown"]
        if raw_headers is None and not compressed:
            argv += ["--data-bytes", str(data), "--metadata", json.dumps(metadata or {}, separators=(",", ":")), "--operation", operation]
            if protobuf:
                argv += ["--protobuf-bytes", str(protobuf)]
        execute(f"probe-{probe_index:03d}-{name}", argv)
        result = rows(log)[-1]
        probes.append({"name": name, "hop": hop, "result": result, "log": log.name})
        write(output / "probes.json", probes)
        return result

    def sdk(name, cases):
        configuration = output / (name + "-configuration.json")
        write(configuration, {"Endpoint": f"http://127.0.0.1:{api_proxy}", "Output": str(output / (name + ".json")), "SinkObservations": str(output / "sink.jsonl"), "Cases": cases})
        execute(name, ["dotnet", assembly, "-method", "*EmitDeterministicF1Evidence", "-result-xml", output / (name + ".xml")],
                {"EXT_SECRETS_1_LOCAL_TRANSPORT": str(configuration)})

    api, api_proxy, http, internal, metrics = [port() for _ in range(5)]
    sink_socket, implementation = sockets / "sink.sock", sockets / "implementation.sock"
    try:
        execute("descriptors", [binary, "--mode", "descriptors", "--target", output / "imported-descriptors.json"])
        write(output / "protocol-comparison.json", verify_protocol(DOCS / "protocol", output / "imported-descriptors.json"))
        sink = start("sink", [binary, "--mode", "sink", "--listen", sink_socket, "--log", output / "sink.jsonl", "--incarnation", "sink-1"])
        ready(sink_socket, sink)
        component = start("component-1", [binary, "--mode", "component", "--listen", implementation, "--target", sink_socket,
                                            "--log", output / "component.jsonl", "--incarnation", "component-1"])
        ready(implementation, component)
        socket_incarnations = [{"incarnation": "component-1", "pid": component.pid, "inode": implementation.stat().st_ino,
                                "device": implementation.stat().st_dev, "mode": oct(implementation.stat().st_mode & 0o777)}]
        component_proxy = start("component-observer", [binary, "--mode", "proxy", "--listen", discovery / "f1fixture.sock", "--target", implementation,
                                                        "--hop", "component", "--log", output / "component-wire.jsonl"])
        ready(discovery / "f1fixture.sock", component_proxy)
        sidecar = start("daprd", [daprd, "--app-id", "f1synthetic", "--mode", "standalone", "--dapr-grpc-port", str(api),
                                   "--dapr-http-port", str(http), "--dapr-internal-grpc-port", str(internal), "--metrics-port", str(metrics),
                                   "--resources-path", resources, "--config", config, "--max-body-size", "128Mi", "--read-buffer-size", "64Ki",
                                   "--dapr-listen-addresses", "127.0.0.1", "--enable-metrics=false", "--log-level", "warn"])
        for _ in range(600):
            if sidecar.poll() is not None:
                raise ValueError("daprd failed before readiness")
            try:
                with urllib.request.urlopen(f"http://127.0.0.1:{http}/v1.0/healthz/outbound", timeout=1) as response:
                    if response.status == 204:
                        break
            except OSError:
                pass
            time.sleep(0.1)
        else:
            raise ValueError("daprd readiness timeout")
        api_observer = start("api-observer", [binary, "--mode", "proxy", "--listen", f"127.0.0.1:{api_proxy}", "--target", f"127.0.0.1:{api}",
                                              "--hop", "api", "--log", output / "api-wire.jsonl"])
        time.sleep(0.2)
        write(output / "isolation.json", {"socketRoot": str(sockets), "uid": os.getuid(), "sameUidIsolation": False,
              "appExcludedMountProven": False, "productionIsolationQualified": False,
              "paths": {str(p): oct(p.stat().st_mode & 0o777) for p in (sockets, discovery, sink_socket, implementation, discovery / "f1fixture.sock")},
              "environment": {"DAPR_COMPONENTS_SOCKETS_FOLDER": str(discovery), "GOMAXPROCS": "4"},
              "loopbackPorts": {"api": api, "apiObserver": api_proxy, "http": http, "internalGrpc": internal, "metricsUnused": metrics}})
        sdk("sdk-witnesses", ["manifest", "transition", "data-45508608", "data-48933548", "data-67108863", "data-67108864", "data-67108865",
                              "api-protobuf-134217727", "api-protobuf-134217728", "api-protobuf-134217729"])
        for n in (45_508_608, 48_933_548, 67_108_863, 67_108_864, 67_108_865):
            probe(f"data-{n}", data=n)
        for hop in ("api", "component"):
            for n in (134_217_727, 134_217_728, 134_217_729):
                probe(f"protobuf-{hop}-{n}", hop=hop, protobuf=n)
            for n in (65535, 65536, 65537):
                probe(f"header-{hop}-{n}", hop=hop, raw_headers=n)
            probe(f"compression-{hop}", hop=hop, compressed=True)
            probe(f"nested-request-{hop}", hop=hop, nested=True)
        # Bound the exact received component map. Then exercise the Dapr path:
        # its generated/forwarded entries are measured, never assumed absent.
        probe("metadata-baseline")
        api_baseline = [r for r in rows(output / "component.jsonl") if r.get("kind") == "componentAdmission"][-1]["metadata"]
        write(output / "forwarded-metadata-baseline.json", api_baseline)
        for hop in ("component", "api"):
            for n in (63, 64, 65):
                supplied = n - (api_baseline["entries"] if hop == "api" else 0)
                probe(f"entries-{hop}-{n}", hop=hop, metadata={f"m{i:02d}": "v" for i in range(supplied)})
            for n in (256, 257):
                probe(f"key-{hop}-{n}", hop=hop, metadata={"k" * n: "v"})
            for n in (4096, 4097):
                probe(f"value-{hop}-{n}", hop=hop, metadata={"m": "v" * n})
            for total in (65535, 65536, 65537):
                supplied = total - (api_baseline["aggregateUtf8Bytes"] if hop == "api" else 0)
                metadata = {f"m{i:02d}": "v" * 3900 for i in range(16)}
                metadata["m16"] = "v" * (supplied - sum(len(k) + len(v) for k, v in metadata.items()) - 3)
                # Use 17 entries to keep every value <=4096 including exact cap.
                if len(metadata["m16"]) > 4096:
                    raise ValueError("Invalid aggregate witness")
                probe(f"aggregate-{hop}-{total}", hop=hop, metadata=metadata)
        for hop in ("component", "api"):
            for n in (1048575, 1048576, 1048577):
                probe(f"response-{hop}-{n}", hop=hop, operation="fixture-response", metadata={"response-wire-bytes": str(n)})
            probe(f"response-unknown-{hop}", hop=hop, operation="fixture-response", metadata={"response-unknown": "true"})
            probe(f"response-nested-unknown-{hop}", hop=hop, operation="fixture-response", metadata={"response-nested-unknown": "true"})
            probe(f"response-compression-{hop}", hop=hop, operation="fixture-response", metadata={"response-compression": "true"})
        sdk("sdk-faults", ["cancellation", "status-cancellation", "late-duplicate", "status-late-duplicate", "lost-result", "status-lost-result", "reconnect"])
        component.terminate(); component.wait(timeout=10)
        # Remove only this stopped process's owned stale socket before the exact
        # replacement. Neither old incarnation nor uncertainty is erased.
        implementation.unlink()
        replacement = start("component-2", [binary, "--mode", "component", "--listen", implementation, "--target", sink_socket,
                                             "--log", output / "component.jsonl", "--incarnation", "component-2"])
        ready(implementation, replacement)
        socket_incarnations.append({"incarnation": "component-2", "pid": replacement.pid, "inode": implementation.stat().st_ino,
                                    "device": implementation.stat().st_dev, "mode": oct(implementation.stat().st_mode & 0o777)})
        write(output / "socket-incarnations.json", socket_incarnations)
        time.sleep(2)
        sdk("sdk-reconnect-lookup", ["status-reconnect"])
        write(output / "execution.json", {"kind": "ExecutedSyntheticLocalF1Transport", "status": "MeasuredLocalTransportOnly",
              "DependencyAvailable": False, "LiveReady": False, "KeyDeliveryEnabled": False, "CompletionPermission": False,
              "PhysicalEvidence": False, "SemanticComponentQualified": False, "ProductionInvokerActivated": False,
              "buildRoot": str(build), "assembly": str(assembly), "binarySha256": {"daprd": sha(daprd), "fixture": sha(binary), "sdkDriver": sha(assembly)},
              "configuredRetries": 0, "transparentDuplicatesExcluded": False,
              "responseCompression": "No component compressor registered; actual component gzip requests refused, stock API gzip requests accepted/decompressed; fixture refuses compressed response selection before send",
              "incarnations": ["component-1", "component-2"], "originalUncertaintyRetained": True})
    finally:
        cleanup = []
        for name, process, stream, binding in reversed(processes):
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill(); process.wait(timeout=10)
            cleanup.append({"name": name, **binding, **terminal_binding(process, binding), "returncode": process.returncode, "stopped": process.poll() is not None, "terminalObserved": process.returncode is not None})
            stream.close()
        write(output / "cleanup.json", cleanup)
        write(output / "sha256.json", {str(p.relative_to(output)): sha(p) for p in sorted(output.rglob("*")) if p.is_file() and p.name != "sha256.json"})
    return output


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-root", required=True)
    parser.add_argument("--assembly", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    print(run(args.build_root, args.assembly, args.output))
