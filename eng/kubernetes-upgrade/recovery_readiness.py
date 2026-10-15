#!/usr/bin/env python3
"""Check local recovery metadata only. Never execute, authenticate or authorize."""

import argparse
from datetime import datetime, timedelta, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import stat


RECORDS = ("workload", "source", "point", "restore", "node", "native", "cleanup")
SCOPES = ("source-observation", "snapshot-and-node-capture", "immutable-publication",
          "validator-readback", "recipient-decryption", "isolated-restore",
          "recovered-native-and-ssh", "target-cleanup", "4.1-record-signing")
HEX = re.compile(r"[0-9a-f]{64}")
ATTEMPT = re.compile(r"[a-z0-9][a-z0-9-]{0,79}")
MAX_METADATA_BYTES = 8 * 1024 * 1024
MAX_METADATA_DEPTH = 64
FINGERPRINT = re.compile(r"SHA256:[A-Za-z0-9+/]{43}")
LOCAL_TOOL_ROLES = {"local-" + n for n in ("kubectl", "age", "etcd", "etcdctl", "etcdutl", "ssh", "ssh-keygen", "docker", "tar", "timeout", "python")}
RUNTIME_TOOL_ROLES = {"source-" + n for n in ("etcd", "etcdctl", "etcdutl", "kubeadm", "kubelet", "kubectl", "tar", "python")} | {"recovery-" + n for n in ("etcd", "etcdctl", "etcdutl", "tar", "python")}
ISOLATION_FIELDS = ("network", "readOnlyRoot", "tmpfs", "privileged", "writableHostMounts",
                    "readOnlyMounts", "hostMounts", "publishedPorts", "hostPid", "hostIpc", "devices",
                    "capabilities", "addedCapabilities", "droppedCapabilities", "noNewPrivileges",
                    "tmpfsFlags", "inputDelivery", "toolsDelivery", "sourceNetworkProbeDenied")


class Refusal(ValueError):
    """A fixed, sanitized reason safe to include in the public result."""


def require(condition, reason):
    if not condition:
        raise Refusal(reason)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def named(value):
    return isinstance(value, str) and bool(value.strip()) and value == value.strip()


def hexadecimal(value):
    return isinstance(value, str) and bool(HEX.fullmatch(value))


def immutable(item, now):
    created, published, read = (timestamp(item[k]) for k in ("createdAt", "publishedAt", "readAt"))
    expiry = timestamp(item["retainUntil"])
    require(created <= published <= read <= now and expiry >= created + timedelta(days=30) and expiry > now,
            "immutable publication chronology or retention interval invalid")
    require(named(item["versionId"]) and item["encryption"] == "AES256" and item["lockMode"] == "COMPLIANCE"
            and item["objectLockRetention"] == item["retentionApi"] == {"mode": "COMPLIANCE", "retainUntil": item["retainUntil"]},
            "immutable retention API mismatch")
    return published, read


def destination_object(item, destination, trust, key, sha256, size, now):
    """Compare the complete claimed writer object and independent readback."""
    chronology = immutable(item, now)
    require(all(item[k] == destination[k] for k in ("endpoint", "bucket", "region", "attemptPrefix"))
            and item["objectKey"] == destination["attemptPrefix"] + key
            and named(item["versionId"]) and item["readBackVersionId"] == item["versionId"]
            and hexadecimal(sha256) and item["sha256"] == item["readBackSha256"] == sha256
            and type(size) is int and size > 0 and type(item["bytes"]) is int
            and type(item["readBackBytes"]) is int and item["bytes"] == item["readBackBytes"] == size
            and item["uploadedBy"] == trust["writerIdentity"]
            and item["readBy"] == trust["validatorIdentity"] != trust["writerIdentity"]
            and item["readBackCreatedAt"] == item["createdAt"]
            and item["readBackEncryption"] == item["encryption"] == "AES256"
            and item["readBackLockMode"] == item["lockMode"] == "COMPLIANCE"
            and item["readBackRetention"] == item["objectLockRetention"] == item["retentionApi"],
            "exact immutable destination or independent readback mismatch")
    return chronology


def in_window(record, window):
    require(timestamp(window["start"]) <= timestamp(record["capturedAt"])
            < timestamp(record["expiresAt"]) <= timestamp(window["exclusiveEnd"]),
            "record outside retained window")


def execution_plan(bindings):
    """The pre-run plan excludes runtime target IDs and future proof hashes."""
    return {"attemptId": bindings["attemptId"], "procedure": bindings["procedure"],
            "tools": bindings["tools"], "runtimeTools": bindings["runtimeTools"],
            "source": {k: bindings["source"][k] for k in
                       ("clusterUid", "nodeUid", "etcdClusterId", "memberId", "endpoint", "dataDirectory")},
            "targetNames": {k: bindings["target"][k] for k in ("etcdContainerName", "nodeContainerName")},
            "custodyDirectory": bindings["custodyDirectory"], "privatePaths": bindings["privatePaths"],
            "storage": bindings["storage"], "scopes": list(SCOPES), "window": bindings["window"]}


def timestamp(value):
    require(isinstance(value, str), "unknown timestamp")
    try:
        result = datetime.fromisoformat(value.replace("Z", "+00:00"))
        require(result.tzinfo is not None, "timestamp requires timezone")
        return result.astimezone(timezone.utc)
    except (ValueError, OverflowError):
        raise Refusal("invalid or timezone-unqualified timestamp") from None


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "duplicate JSON field")
        result[key] = value
    return result


def safe_path(path, private=False):
    """Refuse symlink traversal and replacement by another local user."""
    path = Path(path)
    require(path.is_absolute() and ".." not in path.parts, "absolute nontraversing path required")
    for entry in [*reversed(path.parents), path]:
        info = entry.lstat()
        require(not stat.S_ISLNK(info.st_mode), "symlink custody refused")
        require(info.st_uid in (0, os.getuid()), "untrusted path owner")
        mode = stat.S_IMODE(info.st_mode)
        shared = stat.S_ISDIR(info.st_mode) and mode & stat.S_ISVTX
        require(not mode & 0o022 or shared, "writable custody or tool refused")
    if private:
        require(path.stat().st_uid == os.getuid() and not path.stat().st_mode & 0o077,
                "private input mode or owner refused")
    return path


def read_file(path, private=False):
    path = safe_path(path, private)
    require(stat.S_ISREG(path.stat().st_mode), "regular local file required")
    require(path.stat().st_size <= MAX_METADATA_BYTES, "metadata size limit exceeded")
    with path.open("rb") as stream:
        raw = stream.read(MAX_METADATA_BYTES + 1)
    require(len(raw) <= MAX_METADATA_BYTES, "metadata size limit exceeded")
    return raw


def file_digest(path):
    """Hash complete tool bytes without applying the metadata size limit."""
    path = safe_path(path)
    require(stat.S_ISREG(path.stat().st_mode), "regular local file required")
    result = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(chunk)
    return result.hexdigest()


def parse_json(raw):
    require(len(raw) <= MAX_METADATA_BYTES, "metadata size limit exceeded")
    depth, quoted, escaped = 0, False, False
    for character in raw:
        if quoted:
            if escaped:
                escaped = False
            elif character == 92:
                escaped = True
            elif character == 34:
                quoted = False
        elif character == 34:
            quoted = True
        elif character in (91, 123):
            depth += 1
            require(depth <= MAX_METADATA_DEPTH, "metadata nesting limit exceeded")
        elif character in (93, 125):
            depth -= 1
    try:
        value = json.loads(raw, object_pairs_hook=unique_object,
                           parse_constant=lambda _: require(False, "nonfinite JSON refused"))
    except (RecursionError, MemoryError):
        raise Refusal("metadata parser resource limit exceeded") from None
    require(isinstance(value, dict), "JSON object required")
    return value


def read_json(path):
    return parse_json(read_file(path, True))


def fresh(record, now, maximum):
    captured, expiry = timestamp(record.get("capturedAt")), timestamp(record.get("expiresAt"))
    require(type(maximum) is int and 0 < maximum <= 86400
            and captured <= now < expiry <= captured + timedelta(seconds=maximum),
            "expired, future or excessive prerequisite age")


def local_record(root, reference, trust, now, maximum, destination, window):
    """Inspect claimed verification receipts; never treat their claims as authentication."""
    require(isinstance(reference, dict), "missing signed record")
    contents = {}
    for field in ("record", "signature", "verification", "readback"):
        item = reference.get(field, {})
        require(isinstance(item, dict), "malformed record reference")
        name = item.get("file")
        require(isinstance(name, str) and re.fullmatch(r"[a-z0-9][a-z0-9.-]*", name),
                "record must be a direct custody filename")
        require(bool(HEX.fullmatch(item.get("sha256", ""))), "missing exact record digest")
        contents[field] = read_file(root / name, True)
        require(digest(contents[field]) == item["sha256"], "record or signature hash drift")
    record = parse_json(contents["record"])
    verification = parse_json(contents["verification"])
    readback = parse_json(contents["readback"])
    fresh(record, now, maximum)
    require(contents["signature"].startswith(b"-----BEGIN SSH SIGNATURE-----\n"), "unsigned record")
    require(verification == {"method": "ssh-keygen-Y-verify", "exitCode": 0,
            "recordSha256": digest(contents["record"]), "signatureSha256": digest(contents["signature"]),
            **trust}, "missing or mismatched verification receipt")
    require(readback.get("recordSha256") == digest(contents["record"])
            and readback.get("signatureSha256") == digest(contents["signature"])
            and readback.get("credentialIdentity") == trust["validatorIdentity"]
            and readback.get("credentialIdentity") != trust["writerIdentity"]
            and readback.get("encryption") == "AES256" and readback.get("lockMode") == "COMPLIANCE"
            and bool(readback.get("versionId")) and bool(readback.get("signatureVersionId"))
            and readback.get("bytesMatch") is True and readback.get("retentionApiMatches") is True
            and timestamp(readback.get("retainUntil")) > now, "immutable readback mismatch")
    published, read = destination_object(readback, destination, trust, reference["record"]["file"],
                                          digest(contents["record"]), len(contents["record"]), now)
    signature_published, signature_read = destination_object(
        readback["signatureObject"], destination, trust, reference["signature"]["file"],
        digest(contents["signature"]), len(contents["signature"]), now)
    require(readback["signatureVersionId"] == readback["signatureObject"]["versionId"], "signature version mismatch")
    require(timestamp(record["capturedAt"]) <= published <= signature_published
            and timestamp(readback["signatureObject"]["createdAt"]) >= timestamp(record["capturedAt"]),
            "record or signature publication precedes its evidence")
    fresh(readback, now, maximum)
    in_window(record, window)
    in_window(readback, window)
    require(all(timestamp(window["start"]) <= timestamp(item[k]) < timestamp(window["exclusiveEnd"])
                for item in (readback, readback["signatureObject"]) for k in ("createdAt", "publishedAt", "readAt")),
            "immutable observation outside retained window")
    return record, digest(contents["record"]), max(read, signature_read)


def evaluate(bindings_path, expected_sha256, evidence_path, *, now=None):
    """Return a sanitized closed-gate result even for fully consistent metadata."""
    now = now or datetime.now(timezone.utc)
    result = {"schemaVersion": 1, "kind": "hexalith.offline-recovery-readiness",
              "checkedAt": now.isoformat(), "metadataConsistent": False, "blockers": [],
              "operationalGate": "closed", "mutationAuthorized": False, "remoteRequests": 0,
              "cryptographicVerificationPerformed": False, "credentialLoadingPerformed": False,
              "claimsAreUntrustedMetadata": True}
    try:
        require(bool(HEX.fullmatch(expected_sha256)), "explicit bindings digest required")
        raw = read_file(bindings_path, True)
        require(digest(raw) == expected_sha256, "bindings hash drift")
        b = parse_json(raw)
        require(isinstance(b.get("attemptId"), str) and ATTEMPT.fullmatch(b["attemptId"]),
                "invalid attempt identifier")
        result["attemptId"] = b["attemptId"]
        require(b["schemaVersion"] == 1 and b["kind"] == "hexalith.offline-recovery-bindings",
                "unknown bindings schema")
        root = safe_path(b["custodyDirectory"], True)
        require(root.is_dir() and Path(bindings_path).parent == root and Path(evidence_path).parent == root,
                "attempt custody mismatch")
        for forbidden in b["forbiddenRoots"]:
            forbidden = Path(forbidden)
            require(root != forbidden and forbidden not in root.parents, "unsafe attempt destination")
        require(b["lineage"]["attemptId"] == "20261015t112841z-post-40-recovery-prerequisite",
                "wrong preparation lineage")
        # The historical deadline is independent of this run's preparation or window.
        prior = b["priorRecovery"]
        require(type(prior["maximumAgeSeconds"]) is int and 0 < prior["maximumAgeSeconds"] <= 86400,
                "unknown recovery age policy")
        expiry = min(timestamp(prior["expiresAt"]), timestamp(prior["preparedAt"])
                     + timedelta(seconds=prior["maximumAgeSeconds"]))
        result["earliestRecoveryExpiry"] = expiry.isoformat()
        require(timestamp(prior["preparedAt"]) <= now < expiry, "workload recovery expired or future")
        window, destination = b["window"], b["storage"]
        require(timestamp(window["start"]) <= now < timestamp(window["exclusiveEnd"]),
                "retained window closed or future")
        require(isinstance(destination["endpoint"], str)
                and re.fullmatch(r"https://[^/?#\s]+", destination["endpoint"])
                and all(named(destination[k]) for k in ("bucket", "region"))
                and destination["attemptPrefix"] == "evidence/4-1/" + b["attemptId"] + "/",
                "unknown exact storage destination")
        require(isinstance(b["tools"], list) and len(b["tools"]) == len(LOCAL_TOOL_ROLES)
                and {t.get("role") for t in b["tools"] if isinstance(t, dict)} == LOCAL_TOOL_ROLES,
                "missing exact tool bindings")
        require(b["guard"]["path"] == str(Path(__file__).resolve()), "executing guard binding mismatch")
        for item in [b["lineage"], b["procedure"], b["guard"], *b["tools"]]:
            require(isinstance(item, dict) and isinstance(item.get("path"), str)
                    and isinstance(item.get("sha256"), str) and HEX.fullmatch(item["sha256"]),
                    "malformed exact file binding")
            require(file_digest(item["path"]) == item["sha256"], "lineage, procedure or tool hash drift")
        require(not b["unmeasured"] and not b["uncoveredActions"], "unmeasured identities or uncovered actions")
        require(set(b["runtimeTools"]) == RUNTIME_TOOL_ROLES and all(named(t["path"]) and hexadecimal(t["sha256"])
                and t["measurement"] == "declared-observation" for t in b["runtimeTools"].values()),
                "runtime tool role inventory incomplete or unmeasured")
        source, target, authority = b["source"], b["target"], b["authority"]
        require(all(named(source[k]) for k in ("clusterUid", "etcdClusterId", "memberId", "nodeName", "nodeUid",
                                              "apiEndpoint", "peerEndpoint", "endpoint", "dataDirectory")),
                "unknown source identity")
        require(source["currentObservationAccepted"] is True
                and isinstance(source["apiCaFingerprint"], str) and FINGERPRINT.fullmatch(source["apiCaFingerprint"])
                and all(named(source[k]) for k in ("serverVersion", "etcdVersion"))
                and source["serverVersion"] == source["historicalServerVersion"]
                and source["etcdVersion"] == source["historicalEtcdVersion"],
                "source observations are historical, unknown or untrusted")
        fresh(source, now, b["maximumAgeSeconds"])
        in_window(source, window)
        require(target["network"] == "none" and target["readOnlyRoot"] is True
                and target["tmpfs"] is True and target["privileged"] is False
                and target["writableHostMounts"] == [] and target["publishedPorts"] == []
                and target["readOnlyMounts"] == [] and target["hostMounts"] == []
                and target["hostPid"] is False and target["hostIpc"] is False and target["devices"] == []
                and target["capabilities"] == [] and target["noNewPrivileges"] is True
                and target["tmpfsFlags"] == ["nosuid", "nodev", "noexec", "mode=0700"]
                and target["tmpfsMiB"] == {"etcdRestore": 384, "nodeRestore": 768, "input": 512}
                and target["inputDelivery"] == "stdin" and target["toolsDelivery"] == "image-baked"
                and target["extractionCapabilityQualified"] is True and target["userNamespaceMappingQualified"] is True
                and target["clientEndpoint"] == "http://127.0.0.1:2379"
                and target["peerEndpoint"] == "http://127.0.0.1:2380"
                and target["dataDirectory"] == "/restore/etcd"
                and target["sourceNetworkProbeDenied"] is True, "insufficient isolated fencing")
        require(target["addedCapabilities"] == [] and target["droppedCapabilities"] == ["ALL"]
                and target["nodeIsolation"] == {k: target[k] for k in ISOLATION_FIELDS},
                "both targets require the complete confined topology")
        require(all(target[k] is True for k in ("imageQualified", "engineQualified", "capacityQualified"))
                and all(isinstance(target[k], str) and re.fullmatch(r"sha256:[0-9a-f]{64}", target[k])
                        for k in ("imageDigest", "currentImageId"))
                and isinstance(target["engineFingerprint"], str) and FINGERPRINT.fullmatch(target["engineFingerprint"])
                and named(target["engineContext"]) and named(target["daemonId"])
                and type(target["availableMemoryBytes"]) is int
                and target["availableMemoryBytes"] >= (384 + 768 + 2 * 512) * 1024 * 1024,
                "image, engine or capacity is not currently qualified")
        fresh(target, now, b["maximumAgeSeconds"])
        in_window(target, window)
        require(all(named(target[k]) for k in ("clusterId", "memberId", "etcdContainerId", "nodeContainerId"))
                and target["etcdContainerId"] != target["nodeContainerId"]
                and target["clusterId"] != source["etcdClusterId"] and target["memberId"] != source["memberId"]
                and target["dataDirectory"] != source["dataDirectory"]
                and target["clientEndpoint"] != source["endpoint"], "source and target collide or unknown")
        evidence = read_json(evidence_path)
        require(evidence["attemptId"] == b["attemptId"], "evidence attempt mismatch")
        require(set(evidence["records"]) == set(RECORDS), "incomplete recovery records")
        records, hashes, read_times = {}, {}, {}
        trust = b["trust"]
        require(all(named(trust[k]) for k in ("principal", "validatorIdentity", "writerIdentity"))
                and set(trust) == {"principal", "validatorIdentity", "writerIdentity", "namespace", "trustRootSha256"}
                and trust["namespace"] == "hexalith-recovery"
                and hexadecimal(trust["trustRootSha256"]) and trust["validatorIdentity"] != trust["writerIdentity"],
                "unknown or dependent trust")
        for name in RECORDS:
            records[name], hashes[name], read_times[name] = local_record(
                root, evidence["records"][name], trust, now, b["maximumAgeSeconds"], destination, window)
            require(records[name]["attemptId"] == b["attemptId"] and records[name]["verificationResult"] == "pass",
                    "record attempt or outcome mismatch")
        plan_reference = b["executionPlan"]
        require(Path(plan_reference["path"]).parent == root, "execution plan outside attempt custody")
        plan_bytes = read_file(plan_reference["path"], True)
        require(hexadecimal(plan_reference["sha256"]) and digest(plan_bytes) == plan_reference["sha256"]
                and parse_json(plan_bytes) == execution_plan(b), "pre-run execution plan mismatch")
        scope, scope_hash, scope_read = local_record(
            root, evidence.get("scopeApproval"), trust, now, b["maximumAgeSeconds"], destination, window)
        require(scope["kind"] == "hexalith.recovery-execution-scope-approval"
                and scope["approvedBy"] == trust["principal"] and scope["attemptId"] == b["attemptId"]
                and scope["executionPlanSha256"] == plan_reference["sha256"]
                and scope["procedureSha256"] == b["procedure"]["sha256"]
                and scope["toolInputs"] == b["tools"] and set(scope["scopes"]) == set(SCOPES)
                and scope["windowStart"] == window["start"] and scope["windowEnd"] == window["exclusiveEnd"],
                "pre-run scope approval missing or mismatched")
        first_capture = min(timestamp(records[n]["capturedAt"]) for n in RECORDS if n != "workload")
        require(timestamp(scope["capturedAt"]) <= scope_read <= first_capture,
                "scope approval was not published and read before capture")
        require(max(timestamp(source["capturedAt"]), timestamp(target["capturedAt"])) <= first_capture,
                "source or target qualification followed capture")
        approval, acceptance_hash, _ = local_record(
            root, evidence.get("approval"), trust, now, b["maximumAgeSeconds"], destination, window)
        require(approval["approvedBy"] == trust["principal"] and approval["attemptId"] == b["attemptId"]
                and approval["bindingsSha256"] == expected_sha256 and approval["recordInputs"] == hashes
                and approval["procedureSha256"] == b["procedure"]["sha256"]
                and approval["toolInputs"] == b["tools"] and set(approval["scopes"]) == set(SCOPES)
                and approval["kind"] == "hexalith.recovery-final-acceptance"
                and approval["scopeApprovalSha256"] == scope_hash and acceptance_hash != scope_hash
                and approval["windowStart"] == window["start"] and approval["windowEnd"] == window["exclusiveEnd"],
                "approval missing or exact binding mismatch")
        src, point, restore, node, native, cleanup = (records[n] for n in RECORDS[1:])
        require(src["identity"] == source and src["healthy"] is True and src["alarms"] == []
                and src["versionsMatch"] is True, "source identity, health or version mismatch")
        require(records["workload"]["recoveryId"] == prior["recoveryId"]
                and timestamp(records["workload"]["expiresAt"]) <= expiry, "workload recovery binding mismatch")
        for field in ("revision", "keyCount", "keyHash", "canarySha256", "configurationSha256"):
            require(point[field] == src[field] == restore[field] and point[field] is not None,
                    "snapshot revision, hash, count, canary or configuration mismatch")
        for field in ("keyHash", "canarySha256", "configurationSha256", "snapshotSha256"):
            require(isinstance(point.get(field), str) and HEX.fullmatch(point[field]), "invalid snapshot digest")
        require(type(point["revision"]) is int and point["revision"] > 0
                and type(point["keyCount"]) is int and point["keyCount"] > 0
                and point["integrityVerified"] is True and point["encrypted"] is True
                and restore["snapshotSha256"] == point["snapshotSha256"]
                and restore["recoveryPointSha256"] == hashes["point"] and restore["target"] == target
                and restore["memberHealthy"] is True and restore["revisionBump"] is False,
                "snapshot integrity, restore identity or point binding mismatch")
        require(node["recoveryPointSha256"] == hashes["point"]
                and node["configurationSha256"] == point["configurationSha256"]
                and all(node[k] is True for k in ("encrypted", "ownerModesPreserved", "fileInventoryMatches",
                                                  "certificateKeyPairsMatch", "recoveredFromValidatorCopy")),
                "matching node recovery incomplete")
        for record, plaintext, key in ((point, point["snapshotSha256"], "external-etcd-snapshot.db.age"),
                                       (node, node["archiveSha256"], "node-recovery.tar.gz.age")):
            payload = record["payloadReadback"]
            require(hexadecimal(plaintext) and hexadecimal(payload["decryptedPlaintextSha256"])
                    and isinstance(payload["sha256"], str) and HEX.fullmatch(payload["sha256"])
                    and payload["sha256"] == payload["readBackSha256"]
                    and payload["bytes"] == payload["readBackBytes"] and type(payload["bytes"]) is int
                    and payload["bytes"] > 0 and payload["versionId"]
                    and payload["readBy"] == trust["validatorIdentity"]
                    and payload["encryption"] == "AES256" and payload["lockMode"] == "COMPLIANCE"
                    and payload["decryptedPlaintextSha256"] == plaintext
                    and timestamp(payload["retainUntil"]) > now, "snapshot or archive readback mismatch")
            destination_object(payload, destination, trust, key, payload["sha256"], payload["bytes"], now)
            require(record["immutableOffNodeVersion"] == payload["versionId"], "payload version binding mismatch")
        require(timestamp(src["capturedAt"]) <= timestamp(point["capturedAt"])
                <= timestamp(point["payloadReadback"]["createdAt"])
                and timestamp(point["payloadReadback"]["readAt"]) <= timestamp(restore["startedAt"])
                <= timestamp(restore["finishedAt"]) <= timestamp(restore["capturedAt"])
                and timestamp(src["capturedAt"]) <= timestamp(node["archiveCapturedAt"])
                <= timestamp(node["payloadReadback"]["createdAt"])
                and timestamp(node["payloadReadback"]["readAt"]) <= timestamp(node["startedAt"])
                <= timestamp(node["finishedAt"]) <= timestamp(node["capturedAt"])
                and max(timestamp(restore["finishedAt"]), timestamp(node["finishedAt"])) <= timestamp(native["capturedAt"])
                <= timestamp(cleanup["capturedAt"])
                and timestamp(approval["capturedAt"]) >= max(read_times.values()),
                "recovery dependency chronology invalid")
        require(native["authority"] == authority and native["nodeBundleSha256"] == hashes["node"]
                and authority["currentLineageAccepted"] is True
                and all(named(authority[k]) for k in ("bindingUid", "bindingName", "subject"))
                and all(isinstance(authority[k], str) and FINGERPRINT.fullmatch(authority[k]) for k in
                        ("clientFingerprint", "sshFingerprint", "hostKeyFingerprint", "breakGlassFingerprint"))
                and native["sourceClusterUid"] == source["clusterUid"] and native["recoveredSubject"] == authority["subject"]
                and native["nativeBinding"] == {k: authority[k] for k in ("bindingUid", "bindingName", "subject", "roleRef", "ownerReferences")}
                and authority["lineage"]["dependency"] == "independent-native"
                and all(hexadecimal(authority["lineage"][k]) for k in ("currentHeadSha256", "completeChainSha256", "postCutRevocationsSha256"))
                and authority["lineage"]["sequenceComplete"] is True and authority["lineage"]["postCutRevocationsApplied"] is True
                and timestamp(authority["lineage"]["cutAt"]) <= timestamp(authority["lineage"]["observedAt"]) <= timestamp(native["capturedAt"])
                and timestamp(window["start"]) <= timestamp(authority["lineage"]["observedAt"])
                and now - timestamp(authority["lineage"]["observedAt"]) <= timedelta(seconds=b["maximumAgeSeconds"])
                and authority["ownerReferences"] == [] and authority["dependency"] == "native-kubeadm"
                and authority["bindingUid"] not in b["retainedKubeSphereAuthorityUids"]
                and authority["roleRef"] == {"apiGroup": "rbac.authorization.k8s.io", "kind": "ClusterRole", "name": "cluster-admin"}
                and all(native[k] is True for k in ("recoveredPrivateApi", "recoveredSsh", "breakGlass",
                                                    "independentCustodyReadback", "noNewGrant")),
                "wrong recovered authority or dependency")
        custody = authority["encryptedNativeCustody"]
        destination_object(custody, destination, trust, "independent-native-credential.age",
                           authority["encryptedNativeSha256"], authority["encryptedNativeBytes"], now)
        require(custody["independentCustodyReadback"] is True
                and custody["decryptedCredentialFingerprint"] == authority["clientFingerprint"]
                and timestamp(window["start"]) <= timestamp(custody["readAt"]) <= timestamp(native["capturedAt"])
                and now - timestamp(custody["readAt"]) <= timedelta(seconds=b["maximumAgeSeconds"]),
                "recovered native authority custody is incomplete")
        require(cleanup["target"] == target and cleanup["restoreSha256"] == hashes["restore"]
                and cleanup["nodeBundleSha256"] == hashes["node"]
                and all(cleanup[k] is True for k in ("exactTargetsAbsent", "tmpfsAbsent", "plaintextAbsent",
                                                     "temporaryCredentialsAbsent", "sourceUnchanged")),
                "cleanup absence incomplete")
        paths = {k: b["privatePaths"][k] for k in ("sourceTemporary", "sourceSnapshot", "localPlaintextTmpfs")}
        require(all(named(p) and Path(p).is_absolute() and ".." not in Path(p).parts
                    and p not in ("/", source["dataDirectory"]) for p in paths.values())
                and len(set(paths.values())) == len(paths)
                and Path(paths["sourceTemporary"]) in Path(paths["sourceSnapshot"]).parents,
                "unsafe named source or plaintext cleanup resource")
        children = b["ownedChildProcessIds"]
        require(isinstance(children, list) and bool(children)
                and all(type(pid) is int and pid > 0 for pid in children)
                and len(set(children)) == len(children), "unknown owned child identities")
        require(cleanup["ownedResources"] == {**paths, "etcdContainerId": target["etcdContainerId"],
                "nodeContainerId": target["nodeContainerId"], "childProcessIds": children}
                and cleanup["absentChildProcessIds"] == children
                and cleanup["absentMounts"] == {target[k]: ["/input", "/restore"]
                                               for k in ("etcdContainerId", "nodeContainerId")}
                and all(cleanup[k] is True for k in ("sourceTemporaryAbsent", "sourceSnapshotAbsent",
                        "childrenAbsent", "mountsAbsent", "associatedVolumesAbsent", "listenersAbsent")),
                "named source, child or mount cleanup is incomplete")
        result["metadataConsistent"] = True
    except (ValueError, KeyError, TypeError, OSError, OverflowError, AttributeError,
            RecursionError, MemoryError) as error:
        # Never project file contents, exception values or private paths.
        result["blockers"].append(str(error) if isinstance(error, Refusal)
                                  else "missing, malformed or unsafe local input")
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bindings", required=True, type=Path)
    parser.add_argument("--bindings-sha256", required=True)
    parser.add_argument("--evidence", required=True, type=Path)
    args = parser.parse_args()
    result = evaluate(args.bindings, args.bindings_sha256, args.evidence)
    print(json.dumps(result, sort_keys=True, indent=2))
    return 0 if result["metadataConsistent"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
