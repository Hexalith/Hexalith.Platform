#!/usr/bin/env python3
"""Fail-closed audit of executed synthetic local F1 evidence, never qualification."""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import importlib.util
import tarfile
import shutil
import re
import unittest

_protocol_spec = importlib.util.spec_from_file_location("f1_local_protocol", Path(__file__).resolve().parent / "local-transport/protocol.py")
_protocol_module = importlib.util.module_from_spec(_protocol_spec)
_protocol_spec.loader.exec_module(_protocol_module)
verify_protocol = _protocol_module.verify

FLAGS = ("DependencyAvailable", "LiveReady", "KeyDeliveryEnabled", "CompletionPermission", "PhysicalEvidence",
         "SemanticComponentQualified", "ProductionInvokerActivated")
WITNESSES = {
    "manifest": (24692881, "72d99146b808b66dba54de3d336e284e5f37e5e9d8605a8abe85640f70ec5dbf", 24692931,
                 "50ab9b289ab78f2a6e2b70c77d6153e46d50a15a6f115de685afe377419caccb"),
    "transition": (44740912, "0eff8b317b90e1ed456df4dde1c8f7d229d8186639f47ebee9defc0e50490d6c", 44740962,
                   "b37d39dfdc6e5f9fb0cce40b89cb9ee814f1e8d97124271067eff5dbd9ddbc38"),
}


def read(path):
    return json.loads(Path(path).read_text())


def rows(path):
    return [json.loads(line) for line in Path(path).read_text().splitlines() if line]


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def require(condition, reason):
    if not condition:
        raise ValueError(reason)


def check_authority(execution):
    expected = set(FLAGS) | {"kind", "status", "buildRoot", "assembly", "binarySha256", "configuredRetries",
                             "transparentDuplicatesExcluded", "responseCompression", "incarnations", "originalUncertaintyRetained"}
    require(set(execution) == expected, "Closed executed-packet fields required")
    require(execution.get("kind") == "ExecutedSyntheticLocalF1Transport" and execution.get("status") == "MeasuredLocalTransportOnly", "Closed local packet kind/status required")
    require(all(execution.get(flag) is False for flag in FLAGS), "Synthetic fixtures cannot claim qualification/authority")
    require(type(execution.get("configuredRetries")) is int and execution["configuredRetries"] == 0
            and execution.get("transparentDuplicatesExcluded") is False, "No configured retries; no exactly-once claim")
    require(execution.get("incarnations") == ["component-1", "component-2"] and execution.get("originalUncertaintyRetained") is True, "Both original/replacement incarnations and uncertainty required")


def check_inventory(evidence):
    evidence = Path(evidence).resolve()
    hashes = read(evidence / "sha256.json")
    present = {str(p.relative_to(evidence)) for p in evidence.rglob("*") if p.is_file() and str(p.relative_to(evidence)) not in ("sha256.json", "audit.json")}
    require(set(hashes) == present, "Evidence inventory must be complete")
    for name, digest in hashes.items():
        path = (evidence / name).resolve()
        require(path.is_relative_to(evidence) and path.is_file() and sha(path) == digest, "Missing/changed evidence file: " + name)


def check_probe_matrix(probes):
    expected = {f"data-{n}" for n in (45508608, 48933548, 67108863, 67108864, 67108865)} | {"metadata-baseline"}
    for hop in ("api", "component"):
        expected |= {f"protobuf-{hop}-{n}" for n in (134217727, 134217728, 134217729)}
        expected |= {f"header-{hop}-{n}" for n in (65535, 65536, 65537)} | {f"compression-{hop}"}
        for field, numbers in (("entries", (63, 64, 65)), ("key", (256, 257)), ("value", (4096, 4097)), ("aggregate", (65535, 65536, 65537)), ("response", (1048575, 1048576, 1048577))):
            expected |= {f"{field}-{hop}-{n}" for n in numbers}
        expected |= {f"response-unknown-{hop}", f"response-compression-{hop}", f"nested-request-{hop}", f"response-nested-unknown-{hop}"}
    names = [probe["name"] for probe in probes]
    require(len(names) == len(set(names)) and set(names) == expected, "Missing, duplicate or unexpected boundary probe")


def check_frame(frame, data_bytes, data_hash, protobuf_bytes=None, protobuf_hash=None):
    require(all(type(frame.get(field)) is int for field in ("dataBytes", "protobufBytes", "wireBytes", "unknownBytes")), "Frame counts must be exact integers")
    require(frame.get("dataBytes") == data_bytes and frame.get("dataSha256") == data_hash
            and frame.get("wireBytes") == frame.get("protobufBytes", -5) + 5
            and frame.get("compressed") is False and frame.get("unknownBytes") == 0
            and frame.get("rawWireValid") is True and frame.get("nestedUnknownBytes") == 0, "Exact complete uncompressed closed frame required")
    if protobuf_bytes is not None:
        require(frame["protobufBytes"] == protobuf_bytes and frame["sha256"] == protobuf_hash, "Exact retained API bytes/hash required")
    metadata = frame["metadata"]
    require(set(metadata) == {"entries", "maximumKeyUtf8Bytes", "maximumValueUtf8Bytes", "aggregateUtf8Bytes"}
            and all(type(value) is int and value >= 0 for value in metadata.values()), "Closed integer metadata summary required")
    require(metadata["entries"] <= 64 and metadata["maximumKeyUtf8Bytes"] <= 256 and metadata["maximumValueUtf8Bytes"] <= 4096
            and metadata["aggregateUtf8Bytes"] <= 65536, "Complete measured metadata exceeds profile")


def check_fault_history(faults, sink, wire):
    expected_names = {name for fault in ("cancellation", "late-duplicate", "lost-result", "reconnect") for name in (fault, "status-" + fault)}
    require(len(faults) == 8 and {row["Name"] for row in faults} == expected_names, "Exact fault/recovery schedule required")
    for fault, expected in (("cancellation", 1), ("late-duplicate", 2), ("lost-result", 1), ("reconnect", 1)):
        identity = "synthetic-original-" + fault
        attempts = [row for row in sink if row.get("kind") == "sink" and row.get("path") == "/send" and row.get("identity") == identity]
        lookups = [row for row in sink if row.get("kind") == "sink" and row.get("path") == "/status" and row.get("identity") == identity]
        require(len(attempts) == expected and len(lookups) == 1 and lookups[0]["attempts"] == expected, "Original attempt counts and status-only recovery required: " + fault)
        require([row["attempts"] for row in attempts] == list(range(1, expected + 1)), "Original attempt sequence changed")
        require(all(row["dataSha256"] == attempts[0]["dataSha256"] for row in attempts + lookups), "Recovery identity/payload replaced")
        send = next(row for row in faults if row["Name"] == fault)
        lookup = next(row for row in faults if row["Name"] == "status-" + fault)
        for row, operation in ((send, "fixture-send"), (lookup, "fixture-status")):
            require(row.get("Operation") == operation and type(row.get("DataBytes")) is int
                    and row["DataBytes"] == attempts[0]["dataBytes"]
                    and row.get("DataSha256", "").lower() == attempts[0]["dataSha256"], "SDK fault original payload/operation must match sink")
            for hop in ("api", "component"):
                frames = [r for r in wire[hop] if r.get("kind") == "grpcFrame" and r.get("direction") == "request" and r.get("syntheticCase") == row["Name"]]
                require(len(frames) == 1 and frames[0].get("operation") == operation, "Each SDK fault/status requires its own request at both hops")
                check_frame(frames[0], row["DataBytes"], row["DataSha256"].lower())
        require(lookup["SdkCalls"] == 1 and lookup["Result"] == "ReceivedSyntheticUnverified", "Successful one-call SDK status lookup required")
        require(lookup.get("LookupAttempts") == expected and lookup.get("LookupIdentity") == identity
                and lookup.get("LookupOriginalDataSha256") == attempts[0]["dataSha256"]
                and lookup.get("LookupOriginalProducerIncarnation") == "component-1", "SDK must receive the exact original synthetic sink status")
        require(send["SdkCalls"] == 1 and (fault == "reconnect" or send["Result"].startswith("Unknown:")), "One SDK original send and honest uncertainty required")
        if fault == "reconnect":
            require(attempts[0]["producerIncarnation"] == "component-1" and lookups[0]["producerIncarnation"] == "component-2", "Original and replacement incarnation observations required")
        if fault in ("cancellation", "late-duplicate"):
            cancelled = send.get("CancellationUnixMilliseconds")
            require(type(cancelled) is int and attempts[0]["timeUnixNano"] < (cancelled + 1) * 1000000,
                    "Cancellation must follow independently observed original sink admission")
            if fault == "late-duplicate":
                require(attempts[1]["timeUnixNano"] > cancelled * 1000000, "Duplicate must arrive after cancellation under the same original")


def check_packet(path, result, build, evidence):
    path, build, evidence = Path(path), Path(build), Path(evidence)
    packet = read(path)
    expected_keys = set(FLAGS) | {"kind", "status", "specBaselineCommit", "candidateSourceHead", "sourceState", "historicalProposedPacketsChanged",
        "build", "evidence", "witnesses", "boundaryProbes", "fullResponses", "headers", "forwardedMetadata", "sufficiency", "compression", "retry", "faults",
        "configurationSha256", "isolation", "retainedAttempts", "verification", "limitations", "nextQualificationTemplate"}
    require(set(packet) == expected_keys and packet["kind"] == "ExecutedSyntheticLocalF1TransportPacket" and packet["status"] == "MeasuredLocalTransportOnly"
            and all(packet.get(flag) is False for flag in FLAGS), "Closed synthetic executed packet cannot confer authority")
    require(packet["historicalProposedPacketsChanged"] is False and packet["specBaselineCommit"] == "e9a142dc5c56cdb3c187f5eec0d3b3a7178a0058", "Original historical packets/baseline must be preserved")
    require(packet["witnesses"] == result["measuredWitnesses"] and packet["boundaryProbes"] == read(evidence / "probes.json"), "Published measurements differ from independently audited observations")
    component, wire = rows(evidence / "component.jsonl"), rows(evidence / "component-wire.jsonl")
    responses = []
    for probe in packet["boundaryProbes"]:
        if not probe["name"].startswith("response-"):
            continue
        observations = rows(evidence / probe["log"])
        start, end = observations[0]["timeUnixNano"], observations[-1]["timeUnixNano"]
        responses.append({"name": probe["name"], "result": probe["result"],
            "fullResponseGuards": [r for r in component if r.get("kind") == "responseGuard" and start <= r["timeUnixNano"] <= end],
            "independentFullFrames": [r for r in wire if r.get("kind") == "grpcFrame" and r.get("direction") == "response" and start <= r["timeUnixNano"] <= end]})
    require(packet["fullResponses"] == responses, "Published full response frames/guards changed")
    require(packet["evidence"]["manifestSha256"] == result["evidenceManifestSha256"] and packet["evidence"]["auditSha256"] == sha(evidence / "audit.json"), "Published evidence bindings changed")
    require(packet["build"]["sealedManifestSha256"] == sha(build / "sealed-build.json") and packet["build"]["runtimeTreeManifestSha256"] == sha(build / "runtime-source-tree-sha256.json"), "Published source/artifact bindings changed")
    sealed = read(build / "sealed-build.json")
    require(Path(packet["build"]["root"]).resolve() == build and Path(packet["evidence"]["root"]).resolve() == evidence
            and packet["build"]["sourceArchiveSha256"] == sealed["archiveSha256"]
            and packet["build"]["goToolchainArchiveSha256"] == sealed["goToolchainArchiveSha256"]
            and packet["build"]["dotnetSourceDependencies"] == read(build / "source-dependencies.json"), "Published archive/root/source dependency bindings required")
    require(all(packet["build"][field] == sealed[field] for field in ("daprCommit", "runtimeFileCount", "runtimeChangedFiles", "binaries", "fixtureSources", "generatedSources", "retainedProtocols", "dependencyOrigins", "sdkDriver", "goBuildEnvironmentSha256")), "Published pinned build identities changed")
    require(packet["isolation"] == read(evidence / "isolation.json") and all(sha(evidence / name) == digest for name, digest in packet["configurationSha256"].items()), "Published process isolation/configuration changed")
    require(packet["retry"]["configuredRetries"] == 0 and packet["retry"]["exactlyOnceClaim"] is False
            and packet["sufficiency"]["finiteWitnessIsExhaustive"] is False and packet["sufficiency"]["backendCapacityProven"] is False, "Transport witnesses cannot imply exactly-once, exhaustive witnesses or backend capacity")
    require(packet["faults"]["sdkObservations"] == read(evidence / "sdk-faults.json") + read(evidence / "sdk-reconnect-lookup.json")
            and packet["faults"]["durableAdmissionImplemented"] is False and packet["faults"]["oldIncarnationSanitized"] is False
            and packet["faults"]["uncertaintyClosed"] is False, "Published synthetic fault history cannot imply durable or physical closure")
    require(packet["faults"]["sinkObservations"] == [r for r in rows(evidence / "sink.jsonl") if r.get("identity", "").startswith("synthetic-original-")]
            and packet["faults"]["socketIncarnations"] == read(evidence / "socket-incarnations.json"), "Published original sink/incarnation observations changed")
    check_published_limits(packet, evidence)
    check_independent_reports(packet, build)
    check_detached_verification(packet["verification"], packet, build)
    require(packet["nextQualificationTemplate"] == "next-qualification.template.json", "Exact next-template path required")
    template = read(path.parent / packet["nextQualificationTemplate"])
    check_template(template, packet, sha(path), result["evidenceManifestSha256"])


def check_source_inputs(source):
    expected = {"daprCommit": "4cef924a0b1360c69d810deec8cd59bf853f6721",
                "archiveSha256": "321eabbc9124a516ec00fd8350db3e5950ceb2da66c35a223b55a32fddb7ab4e",
                "goToolchainArchiveSha256": "0221cfe82f9c88c717677cb5c1f59f598d177cd7eac18c11f90ae29432d356f9",
                "patchSha256": "c19d126445132b1b08894a98f75a653f8d1f6ce53a9386fd4d3d8743d28efbfd",
                "patchedSourceSha256": "41d6010a43956eda01ae0d5c4bc94daa95e61e8523f5031c417e81cd1879be4a"}
    require(source == expected, "Exact builder-produced pinned source-input record required")


def check_binary_bindings(build, execution, sealed):
    require(set(sealed["binaries"]) == {"daprd-stable", "f1fixture"}, "Complete sealed executable inventory required")
    for label, name in (("daprd", "daprd-stable"), ("fixture", "f1fixture")):
        require(sealed["binaries"][name] == sha(Path(build) / "bin" / name) == execution["binarySha256"][label],
                "Sealed, retained and executed image hashes must agree: " + label)


def check_sdk_closure(assembly, sealed_files):
    assembly = Path(assembly).resolve()
    current = {}
    for path in assembly.parent.rglob("*"):
        if path.is_file():
            require(path.resolve().is_relative_to(assembly.parent), "SDK dependency escapes app-local closure")
            current[str(path.resolve())] = sha(path)
    require(current == sealed_files and str(assembly.with_suffix(".runtimeconfig.json")) in current
            and str(assembly.with_suffix(".deps.json")) in current and str(assembly) in current,
            "Complete app-local executable dependency closure/runtimeconfig required")


def check_process_inventory(commands, cleanup, probes, execution, build):
    base = {"descriptors", "sink", "component-1", "component-2", "component-observer", "daprd", "api-observer",
            "sdk-witnesses", "sdk-faults", "sdk-reconnect-lookup"}
    expected = base | {f"probe-{i:03d}-{probe['name']}" for i, probe in enumerate(probes, 1)}
    require(len(commands) == len(cleanup) == len(expected) and {r["name"] for r in commands} == expected
            and {r["name"] for r in cleanup} == expected, "Exact complete named process/terminal inventories required")
    terminal = {r["name"]: r for r in cleanup}
    require(len({(r["pid"], r["startTicks"]) for r in commands}) == len(commands), "Distinct process incarnations required")
    for row in commands:
        done = terminal[row["name"]]
        fields = ("pid", "startTicks", "executionImageSha256", "imageBinding", "executableFile", "executableDevice", "executableInode")
        require(all(row.get(k) is not None and row[k] == done.get(k) for k in fields)
                and row["imageBinding"] == "opened-executable-fd" and type(row["pid"]) is int
                and done.get("terminalObserved") is True and done.get("stopped") is True and type(done.get("returncode")) is int,
                "Matching launch and terminal PID/start/image observations required")
        require(isinstance(row.get("argv"), list) and len(row["argv"]) >= 2, "Complete launch command required")
        require(done.get("terminalPid") == row["pid"] and done.get("terminalStartTicks") == row["startTicks"]
                and done.get("terminalImageSha256") == row["executionImageSha256"]
                and done.get("terminalExecutableDevice") == row["executableDevice"] and done.get("terminalExecutableInode") == row["executableInode"]
                and done.get("terminalObservation") == "waitpid-owned-child-and-opened-image-fd", "Owned waitpid and retained opened-image terminal observation required")
        executable = row["argv"][0]
        actual_path = Path(executable) if Path(executable).is_absolute() else Path(shutil.which(executable) or "/missing-executable")
        require(actual_path.resolve() == Path(row["executableFile"]).resolve() and sha(actual_path) == row["executionImageSha256"], "Launch argv/opened executable image binding differs")
        expected_image = Path(build) / "bin" / ("daprd-stable" if row["name"] == "daprd" else "f1fixture")
        if row["name"].startswith("sdk-"):
            require(actual_path.resolve() == Path(shutil.which("dotnet")).resolve() and Path(row["argv"][1]).resolve() == Path(execution["assembly"]).resolve(), "SDK process must run the sealed assembly under the observed dotnet host")
        else:
            require(actual_path.resolve() == expected_image.resolve(), "Named process must execute its expected runtime/fixture image")
        expected_mode = {"descriptors":"descriptors", "sink":"sink", "component-1":"component", "component-2":"component", "component-observer":"proxy", "api-observer":"proxy"}.get(row["name"])
        if expected_mode:
            require("--mode" in row["argv"] and row["argv"][row["argv"].index("--mode") + 1] == expected_mode, "Wrong fixture process mode")
        if row["name"].startswith("probe-"):
            require("--mode" in row["argv"] and row["argv"][row["argv"].index("--mode") + 1] in ("probe", "raw-probe"), "Wrong probe execution mode")
        if executable in (str(Path(build) / "bin/daprd-stable"), str(Path(build) / "bin/f1fixture")):
            label = "daprd" if executable.endswith("daprd-stable") else "fixture"
            require(row["executionImageSha256"] == execution["binarySha256"][label], "Actual opened execution image differs from retained artifact")
        else:
            require(row["name"].startswith("sdk-") and row["sdkDriverSha256"] == execution["binarySha256"]["sdkDriver"], "Unknown executable or changed SDK driver")
        if row["name"] not in {"sink", "component-1", "component-2", "component-observer", "daprd", "api-observer"}:
            require(done["returncode"] == 0, "Short-lived descriptor/probe/SDK process must finish successfully")


def check_send_delivery(admissions, arrivals, data_bytes, data_hash):
    require(len(admissions) == len(arrivals) == 1, "Accepted send requires its own one complete component/sink delivery")
    require(all(r.get("dataBytes") == data_bytes and r.get("dataSha256") == data_hash for r in admissions + arrivals),
            "Accepted send payload bytes/digest differ at component or sink")


def check_runtime_arguments(commands, evidence, build):
    evidence, build = Path(evidence), Path(build)
    isolation = read(evidence / "isolation.json")
    sockets = Path(isolation["socketRoot"])
    ports = isolation["loopbackPorts"]
    fixture, daprd = str(build / "bin/f1fixture"), str(build / "bin/daprd-stable")
    expected = {
        "descriptors": [fixture,"--mode","descriptors","--target",str(evidence / "imported-descriptors.json")],
        "sink": [fixture,"--mode","sink","--listen",str(sockets / "sink.sock"),"--log",str(evidence / "sink.jsonl"),"--incarnation","sink-1"],
        "component-observer": [fixture,"--mode","proxy","--listen",str(sockets / "discovery/f1fixture.sock"),"--target",str(sockets / "implementation.sock"),"--hop","component","--log",str(evidence / "component-wire.jsonl")],
        "api-observer": [fixture,"--mode","proxy","--listen",f"127.0.0.1:{ports['apiObserver']}","--target",f"127.0.0.1:{ports['api']}","--hop","api","--log",str(evidence / "api-wire.jsonl")],
        "daprd": [daprd,"--app-id","f1synthetic","--mode","standalone","--dapr-grpc-port",str(ports["api"]),
                  "--dapr-http-port",str(ports["http"]),"--dapr-internal-grpc-port",str(ports["internalGrpc"]),"--metrics-port",str(ports["metricsUnused"]),
                  "--resources-path",str(evidence / "resources"),"--config",str(evidence / "configuration.yaml"),"--max-body-size","128Mi","--read-buffer-size","64Ki",
                  "--dapr-listen-addresses","127.0.0.1","--enable-metrics=false","--log-level","warn"],
    }
    for name in ("component-1", "component-2"):
        expected[name] = [fixture,"--mode","component","--listen",str(sockets / "implementation.sock"),"--target",str(sockets / "sink.sock"),"--log",str(evidence / "component.jsonl"),"--incarnation",name]
    actual = {row["name"]: row["argv"] for row in commands}
    require(all(actual.get(name) == argv for name, argv in expected.items()), "Exact loaded runtime/component/observer configuration arguments required")


def check_probe_observations(probe, observations, component, sink, wire):
    result, name, hop = probe["result"], probe["name"], probe["hop"]
    require(result == observations[-1], "Probe summary differs from original observation")
    start, end = observations[0]["timeUnixNano"], result["timeUnixNano"]
    def selected(values, kind):
        return [r for r in values if r.get("kind") == kind and start <= r["timeUnixNano"] <= end]
    admissions = selected(component, "componentAdmission")
    arrivals = [r for r in selected(sink, "sink") if r.get("path") == "/send"]
    refusals = selected(component, "componentRefusal")
    validations = selected(component, "wireValidation")
    frames = [r for r in selected(wire[hop], "grpcFrame") if r.get("direction") == "request"]
    def refused(stage):
        require(len([r for r in refusals if r.get("stage") == stage]) == 1 and not arrivals, "Expected component refusal boundary missing: " + stage)
    def delivered():
        data_bytes = result.get("dataBytes", 0)
        data_hash = result.get("dataSha256", hashlib.sha256(b"").hexdigest())
        check_send_delivery(admissions, arrivals, data_bytes, data_hash)
    if name.startswith("header-"):
        size = int(name.rsplit("-", 1)[1])
        headers = [r for r in selected(wire[hop], "decodedHeaders") if r.get("direction") == "request"]
        require(any(r["decodedListBytes"] == size for r in headers), "Independent exact decoded header-list observation required")
        if size == 65537:
            resets = [r for r in selected(wire[hop], "streamReset") if r.get("direction") == "response" and r.get("code") == "FRAME_SIZE_ERROR"]
            require(result.get("result") == "RST_STREAM:FRAME_SIZE_ERROR" and result.get("grpcStatus") == "" and resets and not admissions and not arrivals,
                    "Over-limit header must receive observed FRAME_SIZE_ERROR at header boundary")
        elif hop == "api":
            require(result.get("result") == "Trailers" and result.get("grpcStatus") == "13" and len(admissions) == 1, "Legal API header transport must reach metadata guard")
            refused("metadata")
        else:
            require(result.get("result") == "Trailers" and result.get("grpcStatus") == "0", "Legal component header transport must succeed")
            delivered()
        return
    if name.startswith("compression-"):
        require(len(frames) == 1 and frames[0].get("compressed") is True and result.get("result") == "Trailers", "Real compressed wire frame/trailers required")
        if hop == "api":
            require(result.get("grpcStatus") == "0", "Stock API gzip request acceptance must stay explicit"); delivered()
        else:
            require(result.get("grpcStatus") == "12" and not admissions and not arrivals, "Component gzip must fail with Unimplemented before decode")
        return
    code = result["code"]
    expected = "OK"
    if name.startswith("nested-request-"):
        require(len(frames) == 1 and frames[0].get("nestedUnknownBytes", 0) > 0, "Real nested map unknown request must be independently observed")
        if hop == "api":
            delivered()  # Pinned stock API discards map-entry unknowns before forwarding.
        else:
            expected = "Internal"
            require(not admissions and not arrivals and any(r.get("direction") == "request" and r.get("accepted") is False and r.get("nestedUnknownBytes", 0) > 0 for r in validations), "Nested request must fail in raw component codec before lossy decode")
    elif name.startswith("data-"):
        n = int(name[5:])
        require(result.get("dataBytes") == n, "Named data boundary differs from actual probe")
        for measured_hop in ("api", "component"):
            measured = [r for r in selected(wire[measured_hop], "grpcFrame") if r.get("direction") == "request"]
            require(len(measured) == 1, "Data boundary requires each independent complete request frame")
            check_frame(measured[0], n, result["dataSha256"])
        if n > 67108864:
            expected = "Internal"; require(len(admissions) == 1, "Over-bound data must reach data guard"); refused("data")
        else: delivered()
    elif name.startswith("protobuf-"):
        n = int(name.rsplit("-", 1)[1])
        require(result.get("protobufBytes") == n, "Named protobuf boundary differs from actual probe")
        if n <= 134217728:
            require(len(frames) == 1 and frames[0].get("protobufBytes") == n and frames[0].get("wireBytes") == n + 5
                    and frames[0].get("compressed") is False
                    and frames[0].get("rawUnknownBytes", 0) > 0, "Complete independent exact protobuf boundary frame required")
        if n > 134217728:
            expected = "ResourceExhausted"
            require(not admissions and not arrivals and any(r.get("bytes") == n and r.get("direction") == "request" for r in selected(wire[hop], "declaredOverCap")), "Over-limit protobuf must fail at selected receive boundary")
        elif hop == "component":
            expected = "Internal"
            require(not admissions and not arrivals and any(r.get("direction") == "request" and r.get("accepted") is False and r.get("rawUnknownBytes", 0) > 0 for r in validations), "At-limit component transport must reach raw unknown-field codec refusal")
        else: delivered()
    elif name.startswith(("entries-", "key-", "value-", "aggregate-")):
        require(len(admissions) == 1, "Complete metadata guard must observe probe")
        field = name.split("-", 1)[0]
        key, limit = {"entries": ("entries", 64), "key": ("maximumKeyUtf8Bytes", 256), "value": ("maximumValueUtf8Bytes", 4096), "aggregate": ("aggregateUtf8Bytes", 65536)}[field]
        n = int(name.rsplit("-", 1)[1]); require(admissions[0]["metadata"][key] == n, "Complete post-sanitization metadata boundary differs from target")
        if n > limit:
            expected = "Internal" if hop == "api" else "ResourceExhausted"; refused("metadata")
        else: delivered()
    elif name.startswith("response-"):
        require(len(admissions) == 1 and not arrivals, "Response probes must reach component without sink send")
        guards = selected(component, "responseGuard")
        if "nested-unknown" in name:
            expected = "Internal"
            require(len(guards) == 1 and any(r.get("direction") == "response" and r.get("accepted") is False and r.get("nestedUnknownBytes", 0) > 0 for r in validations), "Encoded nested response must fail raw codec validation")
        elif "compression" in name:
            expected = "Internal" if hop == "api" else "InvalidArgument"; refused("response-compression")
        else:
            require(len(guards) == 1 and guards[0]["contentTypeBytes"] > 0 and guards[0]["metadata"]["entries"] > 0, "Complete response guard includes content type/metadata")
            if "unknown" in name:
                require(guards[0]["unknownBytes"] > 0, "Actual unknown response field required")
                expected = "Internal" if hop == "api" else "ResourceExhausted"; refused("response-size-or-unknown")
            else:
                n = int(name.rsplit("-", 1)[1]); require(guards[0]["wireBytes"] == n, "Exact full response boundary required")
                if n >= 1048576:
                    expected = "Internal" if hop == "api" else "ResourceExhausted"; refused("response-size-or-unknown")
                else:
                    full = [r for r in selected(wire["component"], "grpcFrame") if r.get("direction") == "response"]
                    require(len(full) == 1 and full[0]["wireBytes"] == 1048575 and full[0]["unknownBytes"] == 0 and full[0]["compressed"] is False, "Independent complete response below cap required")
    elif name == "metadata-baseline":
        delivered(); require(admissions[0]["metadata"]["entries"] > 0, "Forwarded/generated metadata cannot be assumed absent")
    else: raise ValueError("Unhandled probe")
    require(code == expected, "Wrong status/enforcement stage for " + name + ": " + code)


def check_go_environment(record, build):
    # Independently state the accepted effective profile; do not trust its declaration.
    expected = {"GOENV": "off", "GOWORK": "off", "GOFLAGS": "", "GOTOOLCHAIN": "local", "CGO_ENABLED": "0",
                "GOOS": "linux", "GOARCH": "amd64", "GOAMD64": "v1", "GOEXPERIMENT": "", "GOINSECURE": "",
                "GOPRIVATE": "", "GONOPROXY": "none", "GONOSUMDB": "none", "GOVCS": "*:off",
                "GOPROXY": "https://proxy.golang.org", "GOSUMDB": "sum.golang.org https://proxy.golang.org/sumdb/sum.golang.org"}
    require(set(record) == {"required", "effective"} and record["required"] == expected
            and all(record["effective"].get(k) == ("" if k == "GOENV" else v) for k, v in expected.items()), "Isolated effective Go build settings required")
    for key, directory in (("GOCACHE", "go-cache"), ("GOMODCACHE", "go-modules")):
        require(Path(record["effective"][key]).resolve() == (Path(build) / directory).resolve(), "Owned Go cache binding required")


def check_hashed_files(root, hashes):
    root = Path(root).resolve()
    require(bool(hashes), "Nonempty retained hash manifest required")
    for name, digest in hashes.items():
        path = (root / name).resolve()
        require(path.is_relative_to(root) and path.is_file() and sha(path) == digest, "Changed retained report/input: " + name)


def check_independent_reports(packet, build):
    root = Path(packet["evidence"]["independentRoot"]).resolve()
    manifest = Path(build) / "independent-sha256.json"
    require(root == (Path(build) / "independent").resolve() and sha(manifest) == packet["evidence"]["independentManifestSha256"], "Independent manifest binding required")
    hashes = read(manifest)
    require(set(hashes) == {str(p.relative_to(root)) for p in root.rglob("*") if p.is_file()}, "Complete independent retained inventory required")
    check_hashed_files(root, hashes)
    source = root / "source-and-header"
    check = read(source / "verification.json")
    require(check.get("kind") == "IndependentDisposableHeaderPatchCheck" and check.get("sourceCommit") == packet["build"]["daprCommit"]
            and check.get("patchSha256") == "c19d126445132b1b08894a98f75a653f8d1f6ce53a9386fd4d3d8743d28efbfd"
            and check.get("resultSha256") == "41d6010a43956eda01ae0d5c4bc94daa95e61e8523f5031c417e81cd1879be4a"
            and check.get("parsedReadBufferBytes") == check.get("patchedHeaderListBytes") == 65536
            and check.get("productionArtifactQualified") is False, "Independent patch result/false authority required")
    tree = read(source / "candidate-source-archive-verification.json")
    require(tree.get("filesCompared") == 4185 and tree.get("sourceArchiveSha256") == packet["build"]["sourceArchiveSha256"]
            and tree.get("modifiedFiles") == [{"path": "pkg/api/grpc/server.go", "base": "1b4f56b441e92822a0b42c63d54b4450cfe45a94fe2a4736ad14ff54dc7dfbe3", "candidate": check["resultSha256"]}]
            and read(source / "candidate-source-tree-sha256.json") == read(Path(build) / "runtime-source-tree-sha256.json"), "Independent exact source tree required")
    require(read(source / "retained-protocol-source-comparison.json") == packet["build"]["retainedProtocols"], "Independent retained protocol identity required")
    toolchain = read(source / "go-toolchain-hash-verification.json")
    require(toolchain == {"kind":"IndependentGoToolchainModuleHash", "module":"golang.org/toolchain", "version":"v0.0.1-go1.26.4.linux-amd64",
            "archiveSha256":"0221cfe82f9c88c717677cb5c1f59f598d177cd7eac18c11f90ae29432d356f9",
            "moduleContentHash":"h1:wAoHYF9gHXZdyCyZT4yuWhbjdIC/Iv5dpQsE+H3FgGU=", "publicSumdbLookupMatched":True,"sumdbSignatureIndependentlyVerified":False}, "Independent toolchain content hash/pass and authority limit required")
    binary = read(source / "stable-candidate-binary-verification.json")
    require(binary.get("sha256") == packet["build"]["binaries"]["daprd-stable"] and binary.get("bytes") == (Path(build) / "bin/daprd-stable").stat().st_size
            and binary.get("goVersion") == "go1.26.4" and all(binary.get(k) is True for k in ("stablecomponents", "grpcGo1_80_0", "componentsContrib1_18_0")), "Independent runtime artifact identity required")
    report_path = Path(packet["headers"]["independentDifferentImplementationReport"]).resolve()
    require(report_path == source / "independent-runtime-header-boundaries.json", "Independent header report must be in retained manifest")
    headers = read(report_path)
    observations = headers.get("observations", [])
    require(headers.get("kind") == "IndependentRawHpackPatchedDaprHeaderBoundary" and headers.get("binarySha256") == binary["sha256"]
            and headers.get("CompletionPermission") is False and headers.get("configurationSha256") == sha(root / "runtime-headers/config.yaml")
            and len(observations) == 3 and [r.get("decodedHeaderListBytes") for r in observations] == [65535, 65536, 65537]
            and all(r.get("advertisedHeaderListBytes") == [65536] for r in observations)
            and [r.get("headerTransportAccepted") for r in observations] == [True, True, False]
            and any(f.get("type") == 3 and f.get("rstCode") == 6 for f in observations[-1]["frames"]), "Independent header limits/status/runtime binding required")
    cleanup = read(root / "runtime-headers/cleanup.json")
    require(cleanup.get("stopped") is True and cleanup.get("returncode") == 0, "Independent runtime cleanup required")
    arithmetic = read(source / "bound-arithmetic.json")
    require(all(arithmetic.get(k) == v for k, v in {"manifestDataUpperBound":45508608, "transitionDataUpperBound":48933548, "dataCap":67108864,
            "completeApiProtobufLooseUpperBoundAtDataCap":67181835,"completeComponentProtobufLooseUpperBoundAtDataCap":67175688,"protobufCap":134217728}.items())
            and arithmetic.get("finiteWitnessIsExhaustive") is False and arithmetic.get("semanticOrCompletionAuthority") is False, "Independent bound arithmetic/false authority required")


def check_detached_verification(verification, packet, build):
    expected = {"authority", "f1Artifacts", "f1Directory", "pythonSuiteLog", "requiredRuntimeTests", "f1ReportSha256", "pythonSuiteSha256", "f1AssemblySha256"}
    require(set(verification) == expected and verification["requiredRuntimeTests"] == 238, "Complete detached verifier report bindings required")
    root = Path(verification["f1Directory"]).resolve()
    report = read(root / "verification.json")
    require(sha(root / "verification.json") == verification["f1ReportSha256"], "Changed detached verifier report")
    check_hashed_files(root, report["sha256"])
    require(set(report["sha256"]) == {"commands.json", "custody-binding-f1.json", "f1-packet-check.json", "owner-capture-source.json", "runtime-build.log", "runtime-tests.log", "runtime-tests.xml", "source-sha256.json"}, "Complete detached report inventory required")
    from verify_f1 import audit as audit_source_contract
    try:
        counts, _ = audit_source_contract(root / "runtime-tests.xml", root / "custody-binding-f1.json")
    except Exception as error:
        raise ValueError("Retained Runtime test results must pass the complete behavioral audit") from error
    require(counts == report.get("counts") and counts["total"] >= 238 and counts["f1Passed"] >= 101, "All retained Runtime tests and original F1 coverage must execute and pass")
    require(report.get("kind") == "SyntheticF1SourceContractOnly" and report.get("candidateBaseCommit") == packet["candidateSourceHead"]
            and report.get("dependencyAvailable") is False and report.get("runtimeQualificationCompleted") is False and report.get("qualificationEffects") == 0
            and report.get("sourceInputs") == packet["build"]["dotnetSourceDependencies"], "Passing pinned detached source contract/false authority required")
    checks = read(root / "f1-packet-check.json")
    require(checks == {"CompletionPermission":False,"DependencyAvailable":False,"KeyDeliveryEnabled":False,"LiveReady":False,
                      "installedPathProved":False,"physicalCompletionProved":False,"terminationPacketConforms":True,"transportPacketConforms":True}, "Detached packet checks/false authority required")
    fixture = read(root / "custody-binding-f1.json")
    require(fixture.get("Kind") == "SyntheticF1SourceContractOnly" and fixture.get("DaprClient") == "1.18.10"
            and all(fixture.get(k) is False for k in ("DependencyAvailable", "LiveReady", "QualifiedComponentInstalled", "PhysicalEvidence", "RealSidecarPathExecuted"))
            and fixture.get("NativeEffects") == 0, "Detached fixture cannot confer live/physical authority")
    require("0 Warning(s)" in (root / "runtime-build.log").read_text() and "0 Error(s)" in (root / "runtime-build.log").read_text(), "Warning/error-free detached build required")
    commands = read(root / "commands.json")
    assembly = Path(verification["f1Artifacts"]) / "bin/Hexalith.Platform.Secrets.Runtime.Tests/debug/Hexalith.Platform.Secrets.Runtime.Tests.dll"
    require(len(commands) == 2 and commands[0][:2] == ["dotnet", "build"] and commands[1][0] == "dotnet"
            and Path(commands[1][1]).resolve() == assembly.resolve() and "--artifacts-path" in commands[0]
            and Path(commands[0][commands[0].index("--artifacts-path") + 1]).resolve() == Path(verification["f1Artifacts"]).resolve(), "Detached executable/artifacts command bindings required")
    require(sha(assembly) == verification["f1AssemblySha256"], "Detached verifier executable identity changed")
    sources = read(root / "source-sha256.json")
    repo = Path(__file__).resolve().parents[2]
    gate_sources = {str(p.relative_to(repo)) for p in (repo / "eng/ext-secrets-1/local-transport").rglob("*") if p.is_file() and "__pycache__" not in p.parts}
    gate_sources |= {str(p.relative_to(repo)) for p in (repo / "eng/ext-secrets-1").glob("*.py")}
    gate_sources |= {"eng/ext-secrets-1/audit_local_transport.py", "eng/ext-secrets-1/test_audit_local_transport.py", "eng/ext-secrets-1/verify_f1.py", "eng/ext-secrets-1/check_f1_packets.py",
                     "tests/Hexalith.Platform.Secrets.Runtime.Tests/CustodyBindingLocalTransport.cs", "tests/Hexalith.Platform.Secrets.Runtime.Tests/CustodyBindingFeasibilityTests.cs"}
    for name in gate_sources:
        require(sources.get(name) == sha(repo / name), "Detached source snapshot differs from current reviewed gate: " + name)
    python_log = Path(verification["pythonSuiteLog"])
    require(sha(python_log) == verification["pythonSuiteSha256"], "Retained Python verification hash required")
    check_python_suite(python_log.read_text(), repo)


def python_test_inventory(repo):
    suite = unittest.TestLoader().discover(str(Path(repo) / "eng/ext-secrets-1"), pattern="test_*.py")
    def flatten(group):
        for item in group:
            if isinstance(item, unittest.TestSuite):
                yield from flatten(item)
            else:
                yield str(item)
    return list(flatten(suite))


def check_python_suite(log, repo):
    expected = python_test_inventory(repo)
    summaries = re.findall(r"^Ran (\d+) tests? in [\d.]+s$", log, re.M)
    require(log.startswith("command: python3 -m unittest discover -s eng/ext-secrets-1 -p test_*.py -v\n")
            and log.rstrip().endswith("exitCode: 0") and summaries == [str(len(expected))]
            and all(log.count(test + "\n") + log.count(test + " ...") == 1 for test in expected)
            and "\nOK\n" in log and "FAILED" not in log, "Complete discovered Python suite, command, passing inventory/count and successful exit required")


TEMPLATE_INPUTS = {
    "semanticComponent": {"currentAuthorityAndOriginalLookupVerifier", "immutableArtifact", "independentlyEnrolledRolesAndPublicTrust", "operationImplementationAndCanonicalContracts"},
    "backend": {"actualProviderAndIndependentPlacement", "currentAuthorityExpectedHeadComparisons", "holdReservationAndSharedPhysicalCut", "itemActionTransactionAndConcurrencyLimits", "permanentOriginalAndAllocationExclusionRetention", "prewrittenImmutableObjectAndOriginalLimits", "survivorAndRestoreIndependence", "wholeManifestLinearizableHeadOrIndexPrimitive"},
    "productionTransport": {"acceptedSemanticComponentBoundEnforcement", "applicationExcludedSocketMountAndRestrictedUidIdentity", "effectiveSdkSidecarComponentRetryPolicies", "installedFullRequestMetadataHeaderAndResponseMeasurements", "supportedRuntimeArtifactAcceptance"},
    "termination": {"acceptedExactTerminationAndSanitationProfile", "allProducerQueueSealsAndAdmittedSealedObservedWatermarks", "completeOldAndReplacementProcessTransportHostInventories", "durableOriginalAdmissionSendCommittedAndCopyLineage", "independentlyObservedAllHolderAndDelayedCopyProof", "installedIndependentVerifierArtifactAndEnrollment", "measuredCapacityAndCompletionReservations"},
    "physicalProof": {"allCopyErasureAndNativeSharedCut", "holdAndSurvivorSuccess", "oldCopyRestoreFailureAndIndependentCurrentAuthority", "originalVectorSignatureAndReceiptRecovery"},
    "nativeAuthorization": {"approvedStartAndEvidenceWaitSchedule", "exactResourcesIsolationAndAccountableOwners", "monetaryCeilingAndCurrency", "permittedNativeActionsAndBoundedSchedule", "retentionAndIndependentCleanupAuthorization"},
    "ownerQualification": {"acceptedJointSharedPlatformTargetHostProfileAndRunner", "completeStory54InstallationInputs", "freshOwnerAcceptancePacketAndPublicAnchors"},
}


def check_template(template, packet, packet_hash, manifest_hash):
    require(set(template) == set(FLAGS) | set(TEMPLATE_INPUTS) | {"instruction", "kind", "status", "localTransport"}
            and template["kind"] == "F1NextQualificationInputsTemplate" and template["status"] == "NotAuthorizedUnprovedInputs"
            and all(template.get(flag) is False for flag in FLAGS), "Closed unproved next-input template required")
    require(template["localTransport"] == {"acceptedProductionTransportProfile":None, "evidenceManifestSha256":manifest_hash,
            "fixtureArtifactSha256":packet["build"]["binaries"]["f1fixture"], "runtimeArtifactSha256":packet["build"]["binaries"]["daprd-stable"],
            "localSyntheticOnly":True, "packet":"local-transport.json", "packetSha256":packet_hash}, "Exact next-template runtime/fixture/evidence/packet binding required")
    for section, keys in TEMPLATE_INPUTS.items():
        require(set(template.get(section, {})) == keys and all(value is None for value in template[section].values()), "Exact explicit null prerequisites required: " + section)


def check_published_limits(packet, evidence):
    expected_configs = {"configuration.yaml", "isolation.json", "resources/binding.yaml", "resources/resiliency.yaml"}
    require(set(packet["configurationSha256"]) == expected_configs
            and all(sha(Path(evidence) / name) == digest for name, digest in packet["configurationSha256"].items()), "Exact complete published configuration inventory required")
    header = packet["headers"]
    require(set(header) == {"acceptedAtHeaderTransport", "advertisedLimitBytes", "boundaryBytes", "decodedListEntryOverheadBytes", "independentDifferentImplementationReport", "rejectedBeforeComponent"}
            and all(header[k] == v for k, v in {"acceptedAtHeaderTransport":[65535,65536],"advertisedLimitBytes":65536,"boundaryBytes":[65535,65536,65537],"decodedListEntryOverheadBytes":32,"rejectedBeforeComponent":[65537]}.items()), "Exact published decoded header profile required")
    metadata = packet["forwardedMetadata"]
    require(set(metadata) == {"aggregateUtf8Bytes", "completePostSanitizationMapEnforcedAt", "goApiBaseline", "maximumEntries", "maximumKeyUtf8Bytes", "maximumValueUtf8Bytes", "protobufReserveBytes"}
            and all(metadata[k] == v for k, v in {"aggregateUtf8Bytes":65536,"maximumEntries":64,"maximumKeyUtf8Bytes":256,"maximumValueUtf8Bytes":4096,"protobufReserveBytes":66560}.items()), "Exact published complete metadata limits required")
    baseline = next(p for p in read(Path(evidence) / "probes.json") if p["name"] == "metadata-baseline")
    times = rows(Path(evidence) / baseline["log"])
    admissions = [r for r in rows(Path(evidence) / "component.jsonl") if r.get("kind") == "componentAdmission" and times[0]["timeUnixNano"] <= r["timeUnixNano"] <= times[-1]["timeUnixNano"]]
    require(len(admissions) == 1 and metadata["goApiBaseline"] == admissions[0]["metadata"], "Published baseline must match complete measured metadata")
    sufficiency = packet["sufficiency"]
    numbers = {"apiProtobufLooseUpperBoundAtDataCap":67181835,"componentProtobufLooseUpperBoundAtDataCap":67175688,"dataCapBytes":67108864,
               "manifestDataUpperBoundBytes":45508608,"protobufCapBytes":134217728,"transitionDataUpperBoundBytes":48933548}
    require(set(sufficiency) == set(numbers) | {"assumptions", "backendCapacityProven", "finiteWitnessIsExhaustive", "proof"}
            and all(sufficiency[k] == v for k, v in numbers.items()) and sufficiency["backendCapacityProven"] is False
            and sufficiency["finiteWitnessIsExhaustive"] is False and sufficiency["assumptions"] == ["Complete closed-DTO field counts and private bounds in unchanged retained transport profile",
                "Binding name<=2048UTF16 units and operation<=256UTF8 bytes", "Complete post-sanitization metadata map<=66560 protobuf bytes",
                "Uncompressed configured SDK messages; one complete request per manifest; no target splitting"] and bool(sufficiency["proof"]), "Exact derived sufficient bounds/conditional authority required")

def check_sdk_data_delivery(n, sdk, wire, component, sink):
    matches = [r for r in sdk if r["Name"] == f"data-{n}"]
    require(len(matches) == 1 and matches[0]["SdkCalls"] == 1 and matches[0]["DataBytes"] == n and (matches[0]["Result"] == "ReceivedSyntheticUnverified") == (n <= 67108864), "SDK data-bound probe missing or wrong")
    digest = matches[0]["DataSha256"].lower()
    for hop in ("api", "component"):
        frames = [r for r in wire[hop] if r.get("kind") == "grpcFrame" and r.get("direction") == "request" and r.get("syntheticCase") == f"data-{n}"]
        require(len(frames) == 1, "Opaque SDK data bytes must be observed at every transport hop")
        for frame in frames:
            check_frame(frame, n, digest)
    arrivals = [r for r in sink if r.get("kind") == "sink" and r.get("path") == "/send" and r.get("syntheticCase") == f"data-{n}"]
    require(len(arrivals) == (1 if n <= 67108864 else 0) and bool(arrivals) == (n <= 67108864) and all(r["dataBytes"] == n and r["dataSha256"] == digest for r in arrivals), "Opaque SDK data-bound sink bytes/refusal must be observed")
    admissions = [r for r in component if r.get("kind") == "componentAdmission" and r.get("syntheticCase") == f"data-{n}"]
    require(len(admissions) == 1 and admissions[0]["dataBytes"] == n and admissions[0]["dataSha256"] == digest, "Each SDK opaque invocation requires its own component admission")
    if n > 67108864:
        require(matches[0]["Result"].startswith("Unknown:") and matches[0].get("GrpcStatus") == "Internal" and len([r for r in component if r.get("kind") == "componentRefusal" and r.get("syntheticCase") == f"data-{n}" and r.get("stage") == "data"]) == 1, "SDK over-data case must reach its own data guard")


def audit(evidence, build):
    evidence, build = Path(evidence).resolve(), Path(build).resolve()
    check_inventory(evidence)
    execution = read(evidence / "execution.json")
    check_authority(execution)
    resource_names = {str(p.relative_to(evidence / "resources")) for p in (evidence / "resources").rglob("*") if p.is_file()}
    require(resource_names == {"binding.yaml", "resiliency.yaml"}, "Only the synthetic binding and explicit zero-retry policy may be loaded")
    for name, digest in {"resources/binding.yaml": "e92f5d2220e2de7d0816afa018127f7433ba8ef676b215c6bc199fcd35d8de49",
                         "resources/resiliency.yaml": "8a0c871952c2db9702a7d8d526d264da1f5a3a3855805f995b0d1297929c4efa",
                         "configuration.yaml": "ace52a322c89c5109b060c052289f2399c7edccd3de39df83266d1c030b473e4"}.items():
        require(sha(evidence / name) == digest, "Exact isolated synthetic configuration required: " + name)
    for label, path in (("daprd", build / "bin/daprd-stable"), ("fixture", build / "bin/f1fixture"), ("sdkDriver", Path(execution["assembly"]))):
        require(sha(path) == execution["binarySha256"][label], "Changed executed binary: " + label)
    check_go_environment(read(build / "go-build-environment.json"), build)
    source = read(build / "source/source-inputs.json")
    check_source_inputs(source)
    require(Path(execution["buildRoot"]).resolve() == build, "Executed build root must match audited artifact root")
    require(source.get("daprCommit") == "4cef924a0b1360c69d810deec8cd59bf853f6721", "Pinned runtime commit required")
    require(source.get("patchSha256") == "c19d126445132b1b08894a98f75a653f8d1f6ce53a9386fd4d3d8743d28efbfd"
            and source.get("patchedSourceSha256") == "41d6010a43956eda01ae0d5c4bc94daa95e61e8523f5031c417e81cd1879be4a", "Exact retained header patch required")
    require(source["archiveSha256"] == sha(build / "source/dapr.tar.gz") == "321eabbc9124a516ec00fd8350db3e5950ceb2da66c35a223b55a32fddb7ab4e", "Changed pinned source archive")
    require(sha(build / "tools/go-toolchain.zip") == "0221cfe82f9c88c717677cb5c1f59f598d177cd7eac18c11f90ae29432d356f9", "Changed pinned toolchain archive")
    runtime = build / "source/dapr-4cef924a0b1360c69d810deec8cd59bf853f6721"
    names, actual_tree = set(), {}
    with tarfile.open(build / "source/dapr.tar.gz") as archive:
        for member in archive.getmembers():
            if not member.isfile():
                continue
            name = str(Path(member.name).relative_to(runtime.name))
            names.add(name)
            expected = ("41d6010a43956eda01ae0d5c4bc94daa95e61e8523f5031c417e81cd1879be4a" if name == "pkg/api/grpc/server.go"
                        else hashlib.sha256(archive.extractfile(member).read()).hexdigest())
            actual_tree[name] = expected
            require(sha(runtime / name) == expected, "Runtime source differs from exact archive plus retained header correction: " + name)
    require(len(names) == 4185 and names == {str(p.relative_to(runtime)) for p in runtime.rglob("*") if p.is_file()}, "Complete exact runtime source tree required")
    require(read(build / "runtime-source-tree-sha256.json") == actual_tree, "Retained source manifest must describe every exact runtime source byte")
    sealed = read(build / "sealed-build.json")
    require(sealed["goBuildEnvironmentSha256"] == sha(build / "go-build-environment.json"), "Sealed effective Go build settings changed")
    check_binary_bindings(build, execution, sealed)
    check_sdk_closure(execution["assembly"], sealed["sdkDriver"])
    for name, digest in sealed["fixtureSources"].items():
        require(sha(build / "source/fixture" / name) == digest, "Changed built fixture source/lockfile")
    reviewed = Path(__file__).resolve().parent / "local-transport"
    fixture = build / "source/fixture"
    file_inventory = lambda root: {str(p.relative_to(root)) for p in root.rglob("*") if p.is_file() and "__pycache__" not in p.parts}
    require(file_inventory(fixture) == file_inventory(reviewed), "Exact fixture file inventory required; extra assembler/linker inputs are refused")
    reviewed_go = {str(p.relative_to(reviewed)) for p in reviewed.rglob("*.go")}
    require(set(sealed["fixtureSources"]) == reviewed_go | {"go.mod", "go.sum"}
            and {str(p.relative_to(fixture)) for p in fixture.rglob("*.go")} == reviewed_go
            and all(sha(reviewed / name) == sealed["fixtureSources"][name] for name in reviewed_go | {"go.sum"}), "Exact reviewed fixture source closure required")
    require((fixture / "go.mod").read_text() == (reviewed / "go.mod").read_text().rstrip() + "\n\nreplace github.com/dapr/dapr => ../dapr-" + source["daprCommit"] + "\n", "Only the exact pinned runtime module replacement is allowed")
    for name, digest in sealed["retainedProtocols"].items():
        require(sha(Path(__file__).resolve().parents[2] / "docs/implementation/ext-secrets-1-f1-2026-10-20/protocol" / name) == digest,
                "Retained protocol bytes differ from pinned imported source")
    for name, digest in sealed["sdkDriver"].items():
        require(sha(Path(name)) == digest, "Changed SDK driver dependency artifact")
    deps = read(Path(execution["assembly"]).with_suffix(".deps.json"))
    require("Dapr.Client/1.18.10" in deps["libraries"], "Actual SDK package must be pinned Dapr.Client1.18.10")
    build_info = (build / "daprd-stable-build-info.log").read_text()
    require("go1.26.4" in build_info and "stablecomponents" in build_info and "github.com/dapr/components-contrib\tv1.18.0" in build_info
            and "google.golang.org/grpc\tv1.80.0" in build_info, "Pinned effective toolchain, components and grpc-go build graph required")
    protocol_results = verify_protocol(Path(__file__).resolve().parents[2] / "docs/implementation/ext-secrets-1-f1-2026-10-20/protocol", evidence / "imported-descriptors.json")
    require(read(evidence / "protocol-comparison.json") == protocol_results == [
        {"name": "dapr/proto/components/v1/bindings.proto", "messages": 11, "services": 2, "schemaEqual": True},
        {"name": "dapr/proto/components/v1/common.proto", "messages": 5, "services": 0, "schemaEqual": True}], "Complete exact imported protocol/source equivalence required")
    wire = {hop: rows(evidence / (hop + "-wire.jsonl")) for hop in ("api", "component")}
    component = rows(evidence / "component.jsonl")
    sink = rows(evidence / "sink.jsonl")
    sdk = read(evidence / "sdk-witnesses.json")
    require(len(sdk) == 11 and {row["Name"] for row in sdk} == {"manifest", "transition", "response-1048575"} | {f"data-{n}" for n in (45508608,48933548,67108863,67108864,67108865)} | {f"api-protobuf-{n}" for n in (134217727,134217728,134217729)}, "Exact SDK invocation inventory required")
    require(all(row.get("MaxSendMessageBytes") == 134217728 and row.get("MaxReceiveMessageBytes") == 1048575
                and row.get("ConfiguredRetries") == 0 and row.get("CompletionPermission") is False for row in sdk), "Effective SDK profile must stay bounded and synthetic")
    measured = {}
    for name, (data_bytes, data_hash, api_bytes, api_hash) in WITNESSES.items():
        source_rows = [row for row in sdk if row["Name"] == name]
        require(len(source_rows) == 1 and source_rows[0]["SdkCalls"] == 1 and source_rows[0]["Result"] == "ReceivedSyntheticUnverified", "One actual SDK witness call required: " + name)
        require(source_rows[0]["DataBytes"] == data_bytes and source_rows[0]["DataSha256"].lower() == data_hash, "Exact retained SDK witness bytes required: " + name)
        hops = {}
        for hop in ("api", "component"):
            frames = [row for row in wire[hop] if row.get("kind") == "grpcFrame" and row.get("direction") == "request" and row.get("dataSha256") == data_hash and row.get("syntheticCase") == name]
            require(len(frames) == 1, "One complete unsplit witness at " + hop)
            frame = frames[0]
            check_frame(frame, data_bytes, data_hash, api_bytes if hop == "api" else None, api_hash if hop == "api" else None)
            if hop == "api":
                require(frame["protobufBytes"] == api_bytes and frame["sha256"] == api_hash, "Exact retained API bytes/hash required")
            metadata = frame["metadata"]
            require(metadata["entries"] <= 64 and metadata["maximumKeyUtf8Bytes"] <= 256 and metadata["maximumValueUtf8Bytes"] <= 4096 and metadata["aggregateUtf8Bytes"] <= 65536, "Complete measured metadata exceeds profile")
            hops[hop] = frame
        arrivals = [row for row in sink if row.get("kind") == "sink" and row.get("path") == "/send" and row.get("dataSha256") == data_hash and row.get("syntheticCase") == name]
        require(len(arrivals) == 1 and arrivals[0]["dataBytes"] == data_bytes and arrivals[0]["attempts"] == 1, "Exact complete sink witness once required")
        measured[name] = {"sdk": source_rows[0], "hops": hops, "sink": arrivals[0]}
    probes = read(evidence / "probes.json")
    check_probe_matrix(probes)
    check_process_inventory(read(evidence / "commands.json"), read(evidence / "cleanup.json"), probes, execution, build)
    check_runtime_arguments(read(evidence / "commands.json"), evidence, build)
    for probe in probes:
        require(Path(probe["log"]).name == probe["log"], "Probe log must be in retained evidence inventory")
        check_probe_observations(probe, rows(evidence / probe["log"]), component, sink, wire)
    # Opaque synthetic SDK probes establish SDK sufficiency without pretending
    # zero-filled bytes are valid semantic manifests.
    for n in (45508608, 48933548, 67108863, 67108864, 67108865):
        check_sdk_data_delivery(n, sdk, wire, component, sink)
    for n in (134217727, 134217728, 134217729):
        matches = [r for r in sdk if r["Name"] == f"api-protobuf-{n}"]
        require(len(matches) == 1 and matches[0]["SdkCalls"] == 1 and matches[0]["Result"].startswith("Unknown:")
                and matches[0].get("GrpcStatus") == ("Internal" if n <= 134217728 else "ResourceExhausted"), "SDK protobuf boundary must fail at the intended boundary")
        frames = [r for r in wire["api"] if r.get("kind") == "grpcFrame" and r.get("direction") == "request" and r.get("syntheticCase") == f"api-protobuf-{n}"]
        require(len(frames) == (1 if n <= 134217728 else 0), "SDK ceiling must distinguish complete wire at/below cap from client rejection above")
        if frames:
            require(frames[0]["protobufBytes"] == n and frames[0]["dataBytes"] == matches[0]["DataBytes"]
                    and frames[0]["dataSha256"] == matches[0]["DataSha256"].lower(), "Exact SDK/API protobuf boundary frame required")
        require(not any(r.get("syntheticCase") == f"api-protobuf-{n}" for r in sink + component if r.get("kind") in ("sink", "componentAdmission")), "SDK protobuf case cannot reach sink/admission")
        if frames:
            # These SDK envelopes carry data close to the API ceiling; forwarded
            # metadata makes the component request exceed its receive ceiling.
            # SDK calls are sequential. Bound this refusal to this API invocation,
            # before the next SDK/Go invocation, rather than borrowing a later row.
            start = frames[0]["timeUnixNano"]
            next_times = [r["timeUnixNano"] for r in wire["api"] if r.get("kind") == "grpcFrame" and r.get("direction") == "request" and r["timeUnixNano"] > start]
            end = min(next_times) if next_times else float("inf")
            boundaries = [r for r in wire["component"] if r.get("kind") == "declaredOverCap" and r.get("direction") == "request" and start <= r["timeUnixNano"] < end]
            require(bool(boundaries) and len({(r["connection"], r["stream"], r["bytes"]) for r in boundaries}) == 1
                    and all(134217728 < r["bytes"] < 134217728 + 66560 for r in boundaries)
                    and not any(r.get("kind") == "componentAdmission" and start <= r["timeUnixNano"] < end for r in component), "SDK at/below API ceiling must reach its own component receive-size refusal")
    faults = read(evidence / "sdk-faults.json") + read(evidence / "sdk-reconnect-lookup.json")
    check_fault_history(faults, sink, wire)
    response = next(row for row in sdk if row["Name"] == "response-1048575")
    require(response["SdkCalls"] == 1 and response["Result"] == "ReceivedSyntheticUnverified"
            and response.get("ResponseDataBytes", 0) > 1000000, "Actual SDK must receive the near-cap response")
    requests = [r for r in wire["component"] if r.get("kind") == "grpcFrame" and r.get("direction") == "request" and r.get("syntheticCase") == response["Name"]]
    api_requests = [r for r in wire["api"] if r.get("kind") == "grpcFrame" and r.get("direction") == "request" and r.get("syntheticCase") == response["Name"]]
    for hop, incoming in (("component", requests), ("api", api_requests)):
        require(len(incoming) == 1, "SDK response case requires its own request")
        frames = [r for r in wire[hop] if r.get("kind") == "grpcFrame" and r.get("direction") == "response"
                  and r.get("connection") == incoming[0]["connection"] and r.get("stream") == incoming[0]["stream"]]
        require(len(frames) == 1 and frames[0]["dataBytes"] == response["ResponseDataBytes"]
                and frames[0]["dataSha256"] == response["ResponseDataSha256"].lower()
                and frames[0]["compressed"] is False and frames[0]["rawWireValid"] is True
                and (hop != "component" or frames[0]["wireBytes"] == 1048575), "SDK received bytes must match independently measured full response")
    isolation = read(evidence / "isolation.json")
    require(isolation.get("sameUidIsolation") is False and isolation.get("appExcludedMountProven") is False and isolation.get("productionIsolationQualified") is False, "Local socket ACLs do not establish app/process isolation")
    return {"kind": "ExecutedSyntheticLocalF1Audit", "measuredWitnesses": measured, "boundaryProbes": len(probes),
            "faultOriginals": 4, "configuredRetries": 0, "sourcePin": source["daprCommit"],
            "evidenceRoot": str(evidence), "evidenceManifestSha256": sha(evidence / "sha256.json"),
            "DependencyAvailable": False, "LiveReady": False, "KeyDeliveryEnabled": False, "CompletionPermission": False,
            "PhysicalEvidence": False, "qualificationEffects": 0}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--evidence", required=True)
    parser.add_argument("--build-root", required=True)
    parser.add_argument("--packet")
    args = parser.parse_args()
    result = audit(args.evidence, args.build_root)
    if args.packet:
        check_packet(args.packet, result, args.build_root, args.evidence)
    print(json.dumps(result, sort_keys=True, indent=2))
