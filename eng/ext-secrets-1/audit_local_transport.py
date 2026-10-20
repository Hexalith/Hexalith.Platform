#!/usr/bin/env python3
"""Fail-closed audit of executed synthetic local F1 evidence, never qualification."""
import argparse
import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent / "local-transport"))
from protocol import verify as verify_protocol

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
    present = {str(p.relative_to(evidence)) for p in evidence.rglob("*") if p.is_file() and p.name not in ("sha256.json", "audit.json")}
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
        expected |= {f"response-unknown-{hop}", f"response-compression-{hop}"}
    names = [probe["name"] for probe in probes]
    require(len(names) == len(set(names)) and set(names) == expected, "Missing, duplicate or unexpected boundary probe")


def check_frame(frame, data_bytes, data_hash, protobuf_bytes=None, protobuf_hash=None):
    require(frame.get("dataBytes") == data_bytes and frame.get("dataSha256") == data_hash
            and frame.get("wireBytes") == frame.get("protobufBytes", -5) + 5
            and frame.get("compressed") is False and frame.get("unknownBytes") == 0, "Exact complete uncompressed closed frame required")
    if protobuf_bytes is not None:
        require(frame["protobufBytes"] == protobuf_bytes and frame["sha256"] == protobuf_hash, "Exact retained API bytes/hash required")
    metadata = frame["metadata"]
    require(metadata["entries"] <= 64 and metadata["maximumKeyUtf8Bytes"] <= 256 and metadata["maximumValueUtf8Bytes"] <= 4096
            and metadata["aggregateUtf8Bytes"] <= 65536, "Complete measured metadata exceeds profile")


def audit(evidence, build):
    evidence, build = Path(evidence).resolve(), Path(build).resolve()
    check_inventory(evidence)
    execution = read(evidence / "execution.json")
    check_authority(execution)
    for label, path in (("daprd", build / "bin/daprd-stable"), ("fixture", build / "bin/f1fixture"), ("sdkDriver", Path(execution["assembly"]))):
        require(sha(path) == execution["binarySha256"][label], "Changed executed binary: " + label)
    source = read(build / "source/source-inputs.json")
    require(Path(execution["buildRoot"]).resolve() == build, "Executed build root must match audited artifact root")
    require(source.get("daprCommit") == "4cef924a0b1360c69d810deec8cd59bf853f6721", "Pinned runtime commit required")
    require(source.get("patchSha256") == "c19d126445132b1b08894a98f75a653f8d1f6ce53a9386fd4d3d8743d28efbfd"
            and source.get("patchedSourceSha256") == "41d6010a43956eda01ae0d5c4bc94daa95e61e8523f5031c417e81cd1879be4a", "Exact retained header patch required")
    require(source["archiveSha256"] == sha(build / "source/dapr.tar.gz") == "321eabbc9124a516ec00fd8350db3e5950ceb2da66c35a223b55a32fddb7ab4e", "Changed pinned source archive")
    require(sha(build / "tools/go-toolchain.zip") == "0221cfe82f9c88c717677cb5c1f59f598d177cd7eac18c11f90ae29432d356f9", "Changed pinned toolchain archive")
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
    require(all(row.get("MaxSendMessageBytes") == 134217728 and row.get("MaxReceiveMessageBytes") == 1048575
                and row.get("ConfiguredRetries") == 0 and row.get("CompletionPermission") is False for row in sdk), "Effective SDK profile must stay bounded and synthetic")
    measured = {}
    for name, (data_bytes, data_hash, api_bytes, api_hash) in WITNESSES.items():
        source_rows = [row for row in sdk if row["Name"] == name]
        require(len(source_rows) == 1 and source_rows[0]["SdkCalls"] == 1 and source_rows[0]["Result"] == "ReceivedSyntheticUnverified", "One actual SDK witness call required: " + name)
        require(source_rows[0]["DataBytes"] == data_bytes and source_rows[0]["DataSha256"].lower() == data_hash, "Exact retained SDK witness bytes required: " + name)
        hops = {}
        for hop in ("api", "component"):
            frames = [row for row in wire[hop] if row.get("kind") == "grpcFrame" and row.get("direction") == "request" and row.get("dataSha256") == data_hash]
            require(len(frames) == 1, "One complete unsplit witness at " + hop)
            frame = frames[0]
            check_frame(frame, data_bytes, data_hash, api_bytes if hop == "api" else None, api_hash if hop == "api" else None)
            if hop == "api":
                require(frame["protobufBytes"] == api_bytes and frame["sha256"] == api_hash, "Exact retained API bytes/hash required")
            metadata = frame["metadata"]
            require(metadata["entries"] <= 64 and metadata["maximumKeyUtf8Bytes"] <= 256 and metadata["maximumValueUtf8Bytes"] <= 4096 and metadata["aggregateUtf8Bytes"] <= 65536, "Complete measured metadata exceeds profile")
            hops[hop] = frame
        arrivals = [row for row in sink if row.get("kind") == "sink" and row.get("path") == "/send" and row.get("dataSha256") == data_hash]
        require(len(arrivals) == 1 and arrivals[0]["dataBytes"] == data_bytes and arrivals[0]["attempts"] == 1, "Exact complete sink witness once required")
        measured[name] = {"sdk": source_rows[0], "hops": hops, "sink": arrivals[0]}
    probes = read(evidence / "probes.json")
    check_probe_matrix(probes)
    for probe in probes:
        observations = rows(evidence / probe["log"])
        result, name, hop = probe["result"], probe["name"], probe["hop"]
        require(result == observations[-1], "Probe summary differs from original observation")
        start, end = observations[0]["timeUnixNano"], result["timeUnixNano"]
        admissions = [row for row in component if row.get("kind") == "componentAdmission" and start <= row["timeUnixNano"] <= end]
        arrivals = [row for row in sink if row.get("kind") == "sink" and row.get("path") == "/send" and start <= row["timeUnixNano"] <= end]
        if name.startswith("header-"):
            size = int(name.rsplit("-", 1)[1])
            headers = [row for row in wire[hop] if row.get("kind") == "decodedHeaders" and row.get("direction") == "request" and start <= row["timeUnixNano"] <= end]
            require(any(row["decodedListBytes"] == size for row in headers), "Independent exact decoded header-list observation required")
            if size == 65537:
                require(not admissions and result["grpcStatus"] != "0", "Over-limit decoded headers must fail before component admission")
            else:
                require(len(admissions) == 1, "At/below header cap must pass header transport")
            continue
        if name.startswith("compression-"):
            require(result.get("grpcStatus") not in (None, "", "0") and not admissions and not arrivals, "Actual compressed request must fail before component admission/sink")
            continue
        code = result["code"]
        if name.startswith("data-"):
            accepted = int(name[5:]) <= 67108864
        elif name.startswith("protobuf-"):
            n = int(name.rsplit("-", 1)[1])
            accepted = hop == "api" and n <= 134217728
            require((len(admissions) == 1) == (n <= 134217728), "Exact protobuf ceiling must be measured at selected receive hop")
        elif name.startswith(("entries-", "key-", "value-", "aggregate-")):
            require(len(admissions) == 1, "Complete metadata guard must observe probe")
            field = name.split("-", 1)[0]
            limit = {"entries": ("entries", 64), "key": ("maximumKeyUtf8Bytes", 256), "value": ("maximumValueUtf8Bytes", 4096), "aggregate": ("aggregateUtf8Bytes", 65536)}[field]
            n = int(name.rsplit("-", 1)[1])
            require(admissions[0]["metadata"][limit[0]] == n, "Complete post-sanitization metadata boundary differs from target")
            accepted = n <= limit[1]
        elif name.startswith("response-"):
            accepted = name.endswith("-1048575")
            guards = [row for row in component if row.get("kind") == "responseGuard" and start <= row["timeUnixNano"] <= end]
            if "compression" not in name:
                require(len(guards) == 1, "Independent full response guard measurement required")
                if "unknown" not in name:
                    require(guards[0]["wireBytes"] == int(name.rsplit("-", 1)[1]) and guards[0]["contentTypeBytes"] > 0 and guards[0]["metadata"]["entries"] > 0, "Complete response includes content type/metadata at exact boundary")
                else:
                    require(guards[0]["unknownBytes"] > 0, "Actual unknown response field required")
            if accepted:
                frames = [row for row in wire["component"] if row.get("kind") == "grpcFrame" and row.get("direction") == "response" and start <= row["timeUnixNano"] <= end]
                require(len(frames) == 1 and frames[0]["wireBytes"] == 1048575 and frames[0]["unknownBytes"] == 0 and frames[0]["compressed"] is False, "Independent full component response frame below exclusive cap required")
        elif name == "metadata-baseline":
            accepted = True
            require(len(admissions) == 1 and admissions[0]["metadata"]["entries"] > 0, "Generated/forwarded map cannot be assumed empty")
        else:
            raise ValueError("Unhandled required probe")
        require((code == "OK") == accepted, "Unexpected boundary outcome: " + name + ":" + code)
        if not accepted:
            require(not arrivals, "Rejected probe reached sink: " + name)
    # Opaque synthetic SDK probes establish SDK sufficiency without pretending
    # zero-filled bytes are valid semantic manifests.
    for n in (45508608, 48933548, 67108863, 67108864, 67108865):
        matches = [r for r in sdk if r["Name"] == f"data-{n}"]
        require(len(matches) == 1 and matches[0]["SdkCalls"] == 1 and (matches[0]["Result"] == "ReceivedSyntheticUnverified") == (n <= 67108864), "SDK data-bound probe missing or wrong")
        digest = matches[0]["DataSha256"].lower()
        for hop in ("api", "component"):
            frames = [r for r in wire[hop] if r.get("kind") == "grpcFrame" and r.get("direction") == "request"
                      and r.get("syntheticCase") == f"data-{n}"] if hop == "api" else [r for r in wire[hop] if r.get("kind") == "grpcFrame" and r.get("direction") == "request" and r.get("dataSha256") == digest]
            require(bool(frames), "Opaque SDK data bytes must be observed at every transport hop")
            for frame in frames:
                check_frame(frame, n, digest)
        arrivals = [r for r in sink if r.get("kind") == "sink" and r.get("path") == "/send" and r.get("dataSha256") == digest]
        require(bool(arrivals) == (n <= 67108864) and all(r["dataBytes"] == n for r in arrivals), "Opaque SDK data-bound sink bytes/refusal must be observed")
    for n in (134217727, 134217728, 134217729):
        matches = [r for r in sdk if r["Name"] == f"api-protobuf-{n}"]
        require(len(matches) == 1 and matches[0]["Result"].startswith("Unknown:"), "SDK protobuf boundary must remain refused/unknown above data budget")
        frames = [r for r in wire["api"] if r.get("kind") == "grpcFrame" and r.get("direction") == "request" and r.get("syntheticCase") == f"api-protobuf-{n}"]
        require(len(frames) == (1 if n <= 134217728 else 0), "SDK ceiling must distinguish complete wire at/below cap from client rejection above")
        if frames:
            require(frames[0]["protobufBytes"] == n and frames[0]["dataBytes"] == matches[0]["DataBytes"]
                    and frames[0]["dataSha256"] == matches[0]["DataSha256"].lower(), "Exact SDK/API protobuf boundary frame required")
    faults = read(evidence / "sdk-faults.json") + read(evidence / "sdk-reconnect-lookup.json")
    require(len(faults) == 8 and len({row["Name"] for row in faults}) == 8, "Exact fault/recovery schedule required")
    for fault, expected in (("cancellation", 1), ("late-duplicate", 2), ("lost-result", 1), ("reconnect", 1)):
        identity = "synthetic-original-" + fault
        attempts = [row for row in sink if row.get("kind") == "sink" and row.get("path") == "/send" and row.get("identity") == identity]
        lookups = [row for row in sink if row.get("kind") == "sink" and row.get("path") == "/status" and row.get("identity") == identity]
        require(len(attempts) == expected and len(lookups) == 1 and lookups[0]["attempts"] == expected, "Original attempt counts and status-only recovery required: " + fault)
        require(all(row["dataSha256"] == attempts[0]["dataSha256"] for row in attempts + lookups), "Recovery identity/payload replaced")
        send = next(row for row in faults if row["Name"] == fault)
        lookup = next(row for row in faults if row["Name"] == "status-" + fault)
        require(lookup["SdkCalls"] == 1 and lookup["Result"] == "ReceivedSyntheticUnverified", "Successful one-call SDK status lookup required")
        require(send["SdkCalls"] == 1 and (fault == "reconnect" or send["Result"].startswith("Unknown:")), "One SDK original send and honest uncertainty required")
        if fault == "reconnect":
            require(attempts[0]["producerIncarnation"] == "component-1" and lookups[0]["producerIncarnation"] == "component-2", "Original and replacement incarnation observations required")
    require(all(row.get("stopped") is True for row in read(evidence / "cleanup.json")), "All owned processes must be stopped")
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
    args = parser.parse_args()
    print(json.dumps(audit(args.evidence, args.build_root), sort_keys=True, indent=2))
