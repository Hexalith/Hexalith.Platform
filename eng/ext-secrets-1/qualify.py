#!/usr/bin/env python3
"""Fail-closed owner-command gate; transport is exclusively Dapr service invocation.

This is a client protocol, not an installation of owner qualification authorities.
Only schema-checked public claims and detached proof digests enter evidence.
"""
import argparse
import base64
import hashlib
import http.client
import json
import os
from pathlib import Path
import queue
import re
import shlex
import stat
import socket
import subprocess
import sys
import tempfile
import threading
import time
import urllib.error
import urllib.request
import uuid

from cases import plan, plan_digest, wire_plan

PROTOCOL = "EXT-SECRETS-1.qualification.v1"
STAGES = ("AfterEffect", "AfterRestart", "AfterRestore", "AfterLookup")
SHA = re.compile(r"^[0-9a-f]{64}$")
COMMIT = re.compile(r"^[0-9a-f]{40}$")
IDENTITY = re.compile(r"^[A-Za-z0-9_.:/@-]{1,2048}$")
GLOBAL = {"leakCount": 0, "foreignEffects": 0, "allCopyRevival": 0,
          "immutableIdentity": 1, "immutableOutcome": 1, "persistedProofCount": 4}
REQUIRED = (
    "runtime.providerBinding", "runtime.hmacCredentialReference", "runtime.tenantInventoryReference", "runtime.currentInventoryAuthorityReference",
    "runtime.tenantA", "runtime.tenantB", "runtime.profileReference", "runtime.issuer", "runtime.audience",
    "runtime.profileVersion", "runtime.profileNotBefore", "runtime.profileValidUntil", "runtime.actorRoleAuthorityReference",
    "runtime.capabilitySignerReference", "runtime.capabilitySignerCredentialReference", "runtime.replayRegistrarReference",
    "runtime.independentSignerAccount", "runtime.independentSignerCredentialReference", "runtime.publicTrustReference",
    "runtime.currentRevocationReference", "runtime.replayRegistrarCredentialReference", "runtime.replayExactScope",
    "runtime.compromiseSubscriberCredentialReference", "runtime.compromisePublisherReference",
    "runtime.compromiseSubscriptionReference", "runtime.compromiseGuardMirrorReference",
    "runtime.allCopyLifecycleAuthorityReference", "runtime.allCopyInventoryReference", "runtime.nonrollbackJournalReference",
    "runtime.restoreTarget", "runtime.authenticatedRequesterTransport", "runtime.migrationAuthorityReference",
    "runtime.protectionOwnerReference", "runtime.publicAnchorRetentionReference",
    "qualification.isolatedTargetId", "qualification.enrollmentCredentialReference", "qualification.daprApiCredentialReference",
    "qualification.executor.appId", "qualification.executor.identity", "qualification.executor.version", "qualification.executor.publicAnchorFile",
    "qualification.observer.appId", "qualification.observer.identity", "qualification.observer.version", "qualification.observer.publicAnchorFile",
    "qualification.enrollment.appId", "qualification.enrollment.identity", "qualification.enrollment.version", "qualification.enrollment.publicAnchorFile",
)
REQUIRED += tuple(f"qualification.{role}.{field}" for role in ("executor", "observer", "enrollment")
                  for field in ("appChannelCredentialReference", "callerAuthenticatorReference", "callerScopeAuthorityReference"))
IMPLEMENTATIONS = ("IndependentRoleAppChannelAndAuthenticatedCallerScopes", "InstalledOwnerQualificationExecutor", "IndependentPersistedStateObserver",
                   "IsolatedTargetEnrollmentAuthority", "QualifiedAllCopyLifecycleAdapter",
                   "QualifiedNonrollbackRestoreAdapter", "IndependentCurrentInventoryAuthority",
                   "IndependentDecisionSignerAndActorRoleAuthority", "PrivateExactReplayRegistrarBinding",
                   "PrivateAuthenticatedCompromiseSubscriberBinding")


class Unavailable(Exception):
    def __init__(self, *inputs):
        self.inputs = sorted(set(inputs))
        super().__init__("DependencyNotAvailable: EXT-SECRETS-1")


class QualificationFailed(Exception):
    pass


class ReplacementAcceptancePacket(Exception):
    def __init__(self):
        super().__init__("ReplacementAcceptancePacket: EXT-SECRETS-1")


def canonical(value):
    # Protocol claims contain ASCII names/identities and integral counters only.
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode()


def digest(value):
    return hashlib.sha256(canonical(value)).hexdigest()


def get(value, path):
    for part in path.split("."):
        if not isinstance(value, dict):
            return None
        value = value.get(part)
    return value


def bounded_public_file(path, limit, *, forbidden_inodes=()):
    # Open nonblocking before inspecting the descriptor: FIFOs/devices/symlinks cannot stall a proof check.
    descriptor = os.open(path, os.O_RDONLY | os.O_NONBLOCK | os.O_NOFOLLOW)
    try:
        metadata = os.fstat(descriptor)
        if (metadata.st_dev, metadata.st_ino) in forbidden_inodes:
            raise ValueError("configured credential cannot be public evidence")
        if not stat.S_ISREG(metadata.st_mode) or metadata.st_size > limit:
            raise ValueError("bounded regular public file required")
        data = bytearray()
        while len(data) <= limit:
            chunk = os.read(descriptor, min(65536, limit + 1 - len(data)))
            if not chunk:
                return bytes(data)
            data.extend(chunk)
        raise ValueError("public file exceeds bound")
    finally:
        os.close(descriptor)


def read_json(path):
    return decode_json(bounded_public_file(path, 1024 * 1024))


def read_public_anchor(path):
    return bounded_public_file(path, 4096)


def command_suffix(eventstore, artifacts, evidence):
    return ("pwsh -NoProfile -File ../platform/eng/verify-ext-secrets-1.ps1 -Mode Live "
            f"-EventStoreSourceRoot {shlex.quote(str(eventstore))} -ArtifactsDirectory {shlex.quote(str(artifacts))} "
            f"-EvidenceDirectory {shlex.quote(str(evidence))}")


def read_public_configuration(path):
    try:
        return parse_public_configuration(bounded_public_file(path, 1024 * 1024))
    except (OSError, ValueError, TypeError, UnicodeError):
        raise Unavailable("ValidPublicRuntimeReferenceConfiguration") from None


def parse_public_configuration(data):
    """Validate captured public bytes so evidence hashes can bind the interpreted snapshot."""
    try:
        value = decode_json(data)
        validate_public_configuration(value)
        return value
    except (ValueError, TypeError, UnicodeError):
        # Neither rejected field names nor their values may enter a report or exception message.
        raise Unavailable("ValidPublicRuntimeReferenceConfiguration") from None


def validate_public_configuration(value):
    nullable = set(REQUIRED) | {"runtime.callTimeout", "runtime.acceptedReplacementCommit"}
    def object_fields(obj, allowed, prefix=""):
        if not isinstance(obj, dict) or set(obj) - set(allowed):
            raise ValueError("public configuration schema")
        for name, child in obj.items():
            shape = allowed[name]
            path = prefix + name
            if child is None and path in nullable:
                continue  # Missing provisioning references remain explicit blocked inputs.
            if isinstance(shape, dict):
                object_fields(child, shape, path + ".")
            elif type(child) is not shape or isinstance(child, str) and (not child or len(child) > 2048 or any(ord(c) < 32 for c in child)):
                raise ValueError("public configuration type")
    runtime = {name.split(".")[1]: str for name in REQUIRED if name.startswith("runtime.")}
    runtime.update({name: str for name in ("acceptedReplacementCommit", "host", "kubernetesApi", "verifiedProviderResource", "providerServiceReference", "overlappingExportDisposition")})
    runtime.update(callTimeout=int, timing={k: int for k in ("L", "S", "O", "H", "R")}, decisionKeysOutsideCustody=bool)
    role = {"appId": str, "identity": str, "version": str, "publicAnchorFile": str,
            "appChannelCredentialReference": str, "callerAuthenticatorReference": str, "callerScopeAuthorityReference": str}
    qualification = {"isolatedTargetId": str, "enrollmentCredentialReference": str, "daprApiCredentialReference": str,
                     "daprEndpoint": str, "requestTimeoutSeconds": int, **{r: role for r in ("executor", "observer", "enrollment")}}
    object_fields(value, {"schema": str, "runtime": runtime, "qualification": qualification})
    if not isinstance(value.get("runtime"), dict) or not isinstance(value.get("qualification"), dict):
        raise ValueError("public configuration objects")


def decode_json(data):
    def pairs(items):
        result = {}
        for k, v in items:
            if k in result:
                raise ValueError("duplicate field")
            result[k] = v
        return result
    return json.loads(data, object_pairs_hook=pairs, parse_constant=lambda _: (_ for _ in ()).throw(ValueError("number")))


def utc_epoch(value):
    from datetime import datetime, timezone
    if not isinstance(value, str) or not value.endswith("Z"):
        raise ValueError("UTC required")
    return datetime.fromisoformat(value.replace("Z", "+00:00")).astimezone(timezone.utc).timestamp()


def public_key_identity(data):
    from cryptography.hazmat.primitives import serialization
    from cryptography.hazmat.primitives.asymmetric import ec
    key = serialization.load_pem_public_key(data)
    if not isinstance(key, ec.EllipticCurvePublicKey) or not isinstance(key.curve, ec.SECP256R1):
        raise ValueError("P-256 required")
    return key.public_bytes(serialization.Encoding.DER, serialization.PublicFormat.SubjectPublicKeyInfo)


def acceptance(register, *, register_text=None):
    text = bounded_public_file(register, 1024 * 1024).decode("utf-8") if register_text is None else register_text
    section = text.split("### EXT-SECRETS-1 — Platform Secret Resolution", 1)[1].split("### EXT-EXPORT-STORE-1", 1)[0]
    target = re.search(r"\| `TargetVersionOrCommit` \| `([0-9a-f]{40})` \|", section).group(1)
    if not re.search(r"\| `AcceptedStatus` \| `(?:Committed|Available)` \|", section):
        raise ValueError("not accepted")
    from joint_live_phase_runner import calendar_date
    date_match = re.search(r"\| `TargetIntegrationDate` \| `([^`]+)` \|", section)
    calendar_date(date_match.group(1) if date_match else None)
    command = re.search(r"Command: `(.*)`\s*\|?\s*$", section, re.MULTILINE).group(1)
    if f'"{target}"' not in command or "status --porcelain" not in command:
        raise ValueError("not exact")
    # The owner accepts these immutable trust/config pins in the authoritative register.
    # A local config's accepted/isolated/qualified flag has no authority.
    binding_match = re.search(r"\| `RuntimeQualificationBinding` \| `([^`]+)` \|", section)
    binding = json.loads(binding_match.group(1)) if binding_match else None
    return target, command, binding


def cohort_prerequisite_inputs(register, target, *, register_text=None):
    """The full plan crosses the accepted joint cohort; a standalone run cannot satisfy it."""
    text = bounded_public_file(register, 1024 * 1024).decode("utf-8") if register_text is None else register_text
    if "### OQ-5.4-1" not in text:
        return ["OQ-5.4-1.AuthoritativeCohortRecord", "OQ-5.4-1.IndependentCurrentCohortBindingAndOwners"]
    missing = []
    def section(record):
        match = re.search(r"^### " + re.escape(record) + r"\b[^\n]*\n(.*?)(?=^### |^## |\Z)", text, re.M | re.S)
        return match.group(1) if match else ""
    protection, host = section("EXT-PROTECTION-1"), section("EXT-HOST-1")
    def accepted_owner(value):
        return (re.search(r"\| `AcceptedStatus` \| `(?:Committed|Available)` \|", value)
                and re.search(r"\| `TargetVersionOrCommit` \| `[0-9a-f]{40}` \|", value)
                and re.search(r"\| `TargetIntegrationDate` \| `\d{4}-\d{2}-\d{2}` \|", value)
                and re.search(r"Command: `[^`\n]+`", value))
    if not accepted_owner(protection):
        missing.append("OQ-5.4-1.CommittedProtectionOwner")
    if not accepted_owner(host) or f'| `TargetVersionOrCommit` | `{target}` |' not in host:
        missing.append("OQ-5.4-1.SharedPlatformTarget")
    # A profile must be in the accepted executable command, not just explanatory prose.
    command = re.search(r"Command: `([^`]+)`", host)
    try:
        arguments = shlex.split(command.group(1)) if command is not None else []
        position = arguments.index("--profile")
        profile = arguments[position + 1]
        valid_profile = bool(profile and not profile.startswith("-") and not any(x in profile for x in ("$", "`", "<", ">")))
    except (ValueError, IndexError):
        valid_profile = False
    if not accepted_owner(host) or not valid_profile:
        missing.append("OQ-5.4-1.AcceptedPinnedHostProfile")
    # This client implements the complete secrets plan. It does not execute the protection
    # owner phase, interleave host composition, or prove every consumer refuses. Those
    # phase receipts must come from a real joint runner before this standalone entry can
    # acquire any authority for the mutual cases. No local/config flag supplies them.
    missing.append("OQ-5.4-1.IndependentCurrentCohortBindingAndOwners")
    return missing


def git(root, *args):
    return subprocess.check_output(["git", "-C", str(root), *args], stderr=subprocess.DEVNULL,
                                   timeout=10, text=True).strip()


def load_replacement_packet(path):
    try:
        data = bounded_public_file(path, 1024 * 1024)
    except OSError:
        return None
    except ValueError:
        raise ReplacementAcceptancePacket() from None
    if len(data) > 1024 * 1024:
        raise ReplacementAcceptancePacket()
    try:
        packet = decode_json(data)
    except (ValueError, TypeError, json.JSONDecodeError, UnicodeError):
        raise ReplacementAcceptancePacket()
    if not isinstance(packet, dict):
        raise ReplacementAcceptancePacket()
    return packet


def replacement_packet_inputs(packet, config, target, head, binding=None):
    """Compare pins to the loaded configuration, plan, and anchor set. A 64-hex string is not itself a pin."""
    if not isinstance(packet, dict):
        raise ReplacementAcceptancePacket()
    if packet.get("schema") not in ("EXT-SECRETS-1.replacement-acceptance.v1", "EXT-SECRETS-1.replacement-acceptance.v2"):
        raise ReplacementAcceptancePacket()
    missing = []
    if packet.get("configurationSha256") != digest(config):
        missing.append("PinnedConfigurationSha256")
    if packet.get("planSha256") != plan_digest():
        missing.append("PinnedPlanSha256")
    anchors = {f"{role}AnchorSha256": packet.get(f"{role}AnchorSha256") for role in ("executor", "observer", "enrollment")}
    if packet.get("publicAnchorSha256") != digest(anchors):
        missing.append("PinnedPublicAnchorSha256")
    for role in ("executor", "observer", "enrollment"):
        try:
            data = read_public_anchor(get(config, f"qualification.{role}.publicAnchorFile"))
            public_key_identity(data)
            actual = hashlib.sha256(data).hexdigest()
            if anchors[f"{role}AnchorSha256"] != actual or binding is not None and binding.get(f"{role}AnchorSha256") != actual:
                missing.append("PinnedPublicAnchorSha256")
        except (OSError, TypeError, ValueError, ImportError):
            missing.append("PinnedPublicAnchorSha256")
    if binding is not None and (binding.get("configurationSha256") != packet.get("configurationSha256") or binding.get("planSha256") != packet.get("planSha256")):
        missing.append("PacketMatchesAuthoritativeRuntimeQualificationBinding")
    commit = packet.get("candidateCommit") if packet.get("schema") == "EXT-SECRETS-1.replacement-acceptance.v2" else packet.get("acceptedReplacementCommit")
    accepted = isinstance(commit, str) and COMMIT.fullmatch(commit) and commit == target and commit == head
    if not accepted or packet.get("dependencyAvailable") is not False or packet.get("liveCompatibilityAuthorized") is not False or packet.get("productionMutationAllowed") is not False:
        missing.append("LiveCompatibilityNotAuthorized")
    return missing


def qualification_prerequisite_inputs(config):
    """Shared named public-reference checks; credential contents are never read or reported."""
    missing = []
    for role in ("executor", "observer", "enrollment"):
        for field in ("identity", "version", "appId"):
            value = get(config, f"qualification.{role}.{field}")
            if not isinstance(value, str) or not IDENTITY.fullmatch(value):
                missing.append(f"qualification.{role}.{field}")
    if get(config, "qualification.daprEndpoint") != "http://127.0.0.1:3500":
        missing.append("LocalEnrolledDaprSidecar")
    timeout = get(config, "qualification.requestTimeoutSeconds")
    if type(timeout) is not int or timeout not in range(1, 31):
        missing.append("BoundedQualificationCalls")
    for path, name in (("qualification.daprApiCredentialReference", "MountedQualificationDaprCredential"),
                       ("qualification.enrollmentCredentialReference", "MountedQualificationCallerCredential")):
        credential = get(config, path)
        if not mounted_credential_reference(credential):
            missing.append(name)
    return missing


def mounted_credential_reference(reference):
    """Inspect mount metadata without reading credentials; Kubernetes projected files may be links."""
    if not isinstance(reference, str) or not reference.startswith("file:/"):
        return False
    try:
        metadata = os.stat(reference[5:])
        return stat.S_ISREG(metadata.st_mode) and 0 < metadata.st_size <= 8192
    except OSError:
        return False


def read_mounted_credential(reference, missing):
    """Validate and read one bounded regular descriptor, including projected Secret mounts."""
    descriptor = None
    try:
        if not isinstance(reference, str) or not reference.startswith("file:/"):
            raise ValueError("credential reference")
        descriptor = os.open(reference[5:], os.O_RDONLY | os.O_NONBLOCK)
        metadata = os.fstat(descriptor)
        if not stat.S_ISREG(metadata.st_mode) or not 0 < metadata.st_size <= 8192:
            raise ValueError("bounded regular credential file")
        # fstat and read share the same descriptor. An atomic projected-volume rotation
        # cannot switch this operation to another file between validation and reading.
        data = bytearray()
        while len(data) <= 8192:
            chunk = os.read(descriptor, min(8193 - len(data), 4096))
            if not chunk:
                break
            data.extend(chunk)
        if len(data) > 8192:
            raise ValueError("credential size")
        token = data.decode("utf-8", errors="strict").strip()
        data[:] = b"\x00" * len(data)
        if not token or any(ord(c) <= 32 or ord(c) >= 127 for c in token):
            raise ValueError("credential shape")
        return token
    except (OSError, ValueError, UnicodeError):
        raise Unavailable(missing) from None
    finally:
        if descriptor is not None:
            os.close(descriptor)


def preflight(root, config_path, register, caller, eventstore_argument, artifacts_argument, evidence_argument, acceptance_packet_path=None):
    missing = []
    config = {}
    try:
        register_text = bounded_public_file(register, 1024 * 1024).decode("utf-8")
        target, command, binding = acceptance(register, register_text=register_text)
        missing.extend(cohort_prerequisite_inputs(register, target, register_text=register_text))
    except (OSError, ValueError, IndexError, AttributeError, TypeError):
        raise Unavailable("AuthoritativeAcceptedTargetAndOwnerCommand")
    try:
        if git(root, "rev-parse", "HEAD") != target:
            missing.append("ExactAcceptedTargetCheckout")
        if git(root, "status", "--porcelain"):
            missing.append("CleanAcceptedTargetCheckout")
    except (OSError, subprocess.SubprocessError):
        missing.append("ExactAcceptedTargetCheckout")
    # Preserve the exact pinned owner invocation; other Live calls are preflight-only.
    expected_suffix = command_suffix(eventstore_argument, artifacts_argument, evidence_argument)
    if not command.endswith(expected_suffix) or Path(caller).resolve() != (Path(root).parent / "agents").resolve():
        missing.append("ExactAcceptedOwnerCommand")
    if not config_path:
        missing.append("RuntimeReferenceConfiguration")
    else:
        try:
            config = read_public_configuration(config_path)
        except Unavailable:
            missing.append("ValidPublicRuntimeReferenceConfiguration")
    for name in REQUIRED:
        value = get(config, name)
        if not isinstance(value, str) or not value or len(value) > 2048 or any(ord(c) < 32 for c in value):
            missing.append(name)
    if config.get("schema") != PROTOCOL:
        missing.append("RuntimeConfigurationProtocolVersion")
    if get(config, "runtime.host") != "192.168.1.30":
        missing.append("AcceptedHost192.168.1.30")
    if get(config, "runtime.timing") != {"L": 300, "S": 30, "O": 600, "H": 86400, "R": 604800}:
        missing.append("AcceptedEnvelopeTiming")
    if get(config, "runtime.tenantA") == get(config, "runtime.tenantB"):
        missing.append("DistinctEnrolledTenants")
    if get(config, "runtime.decisionKeysOutsideCustody") is not True:
        missing.append("IndependentDecisionKeyCustody")
    if get(config, "runtime.overlappingExportDisposition") != "Open":
        missing.append("PreservedOpenExportDisposition")
    try:
        if not utc_epoch(get(config, "runtime.profileNotBefore")) <= time.time() < utc_epoch(get(config, "runtime.profileValidUntil")):
            missing.append("CurrentProfileValidity")
    except (ValueError, TypeError):
        missing.append("CurrentProfileValidity")
    if binding is None:
        missing.append("AuthoritativelyAcceptedRuntimeQualificationBinding")
    else:
        if binding.get("configurationSha256") != digest(config) or binding.get("planSha256") != plan_digest():
            missing.append("AcceptedRuntimeConfigurationAndCompletePlan")
        decoded_keys = []
        for role in ("executor", "observer", "enrollment"):
            try:
                data = read_public_anchor(get(config, f"qualification.{role}.publicAnchorFile"))
                if hashlib.sha256(data).hexdigest() != binding.get(f"{role}AnchorSha256"):
                    missing.append(f"Accepted{role.title()}PublicAnchor")
                decoded_keys.append(public_key_identity(data))
            except (OSError, TypeError, ValueError, ImportError):
                missing.append(f"Accepted{role.title()}PublicAnchor")
        pins = [binding.get(f"{role}AnchorSha256") for role in ("executor", "observer", "enrollment")]
        identities = [get(config, f"qualification.{role}.identity") for role in ("executor", "observer", "enrollment")]
        apps = [get(config, f"qualification.{role}.appId") for role in ("executor", "observer", "enrollment")]
        if len(set(pins)) != 3 or len(set(decoded_keys)) != 3 or len(set(identities)) != 3 or len(set(apps)) != 3 or not all(isinstance(p, str) and SHA.fullmatch(p) for p in pins):
            missing.append("IndependentEnrolledQualificationAuthorities")
    missing.extend(qualification_prerequisite_inputs(config))
    if type(get(config, "runtime.callTimeout")) is not int or get(config, "runtime.callTimeout") not in range(1, 31):
        missing.append("runtime.callTimeout")
    if get(config, "runtime.acceptedReplacementCommit") != target:
        missing.append("runtime.acceptedReplacementCommit")
    try:
        import cryptography  # noqa: F401
    except ImportError:
        missing.append("ES256PublicProofVerifier")
    packet_path = acceptance_packet_path or os.environ.get("EXT_SECRETS_1_ACCEPTANCE_PACKET")
    if not packet_path:
        missing.append("DetachedImmutableCandidateAcceptancePacket")
    else:
        packet_file = Path(packet_path).resolve()
        if packet_file.is_relative_to(Path(root).resolve()):
            missing.append("AcceptancePacketOutsideCandidateCheckout")
        packet = load_replacement_packet(packet_file)
        if packet is None:
            missing.append("DetachedImmutableCandidateAcceptancePacket")
            packet = {}
        try:
            head = git(root, "rev-parse", "HEAD")
        except (OSError, subprocess.SubprocessError):
            head = None
        # A commit equal to the register target and HEAD is the later acceptance condition. It does not skip pin checks above.
        if packet:
            missing.extend(replacement_packet_inputs(packet, config, target, head, binding))
        else:
            missing.append("DetachedImmutableCandidateAcceptancePacket")
        if packet.get("schema") == "EXT-SECRETS-1.replacement-acceptance.v2" and packet.get("exactCommand") != command:
            missing.append("PacketMatchesAuthoritativeExactOwnerCommand")
    if missing:
        raise Unavailable(*missing, *IMPLEMENTATIONS)
    return config, target, binding


def verify_signed(envelope, role, config, binding, *, original_time=None):
    from cryptography.hazmat.primitives import hashes, serialization
    from cryptography.hazmat.primitives.asymmetric import ec
    from cryptography.hazmat.primitives.asymmetric.utils import encode_dss_signature
    if not isinstance(envelope, dict) or set(envelope) != {"payload", "signature"}:
        raise QualificationFailed("ProofSchema")
    payload = envelope["payload"]
    if not isinstance(payload, dict) or not isinstance(envelope["signature"], str):
        raise QualificationFailed("ProofSchema")
    try:
        raw = base64.b64decode(envelope["signature"], validate=True)
        if len(raw) != 64 or base64.b64encode(raw).decode() != envelope["signature"]:
            raise ValueError("ES256 signature")
        key_bytes = read_public_anchor(get(config, f"qualification.{role}.publicAnchorFile"))
        if hashlib.sha256(key_bytes).hexdigest() != binding[f"{role}AnchorSha256"]:
            raise ValueError("changed anchor")
        key = serialization.load_pem_public_key(key_bytes)
        if not isinstance(key, ec.EllipticCurvePublicKey) or not isinstance(key.curve, ec.SECP256R1):
            raise ValueError("ES256 key")
        key.verify(encode_dss_signature(int.from_bytes(raw[:32], "big"), int.from_bytes(raw[32:], "big")),
                   canonical(payload), ec.ECDSA(hashes.SHA256()))
    except Exception as exc:
        raise QualificationFailed("IndependentSignature") from exc
    if payload.get("protocol") != PROTOCOL or payload.get("identity") != get(config, f"qualification.{role}.identity") \
            or payload.get("version") != get(config, f"qualification.{role}.version"):
        raise QualificationFailed("EnrolledAuthorityIdentity")
    instant = time.time() if original_time is None else original_time
    if type(instant) not in (int, float) or instant > time.time() + 1 \
            or type(payload.get("observedAt")) is not int or type(payload.get("validUntil")) is not int \
            or not instant - 30 <= payload["observedAt"] <= instant + 1 \
            or not instant < payload["validUntil"] <= payload["observedAt"] + 30:
        raise QualificationFailed("CurrentProofAuthority")
    return payload


class DaprTransport:
    def __init__(self, config):
        self.config = config
        self.host, self.port = "127.0.0.1", 3500

    def call(self, role, operation, request):
        app = get(self.config, f"qualification.{role}.appId")
        # No direct provider, gateway, SSH or production API fallback exists.
        deadline = time.monotonic() + get(self.config, "qualification.requestTimeoutSeconds")
        result = queue.Queue(maxsize=1)
        connection = http.client.HTTPConnection(self.host, self.port, timeout=get(self.config, "qualification.requestTimeoutSeconds"))

        def remaining():
            seconds = deadline - time.monotonic()
            if seconds <= 0:
                raise TimeoutError("QualificationCallDeadline")
            return seconds

        def abort():
            if connection.sock is not None:
                try: connection.sock.shutdown(socket.SHUT_RDWR)
                except OSError: pass
            connection.close()

        def read():
            token = None
            caller_token = None
            response = None
            try:
                token = read_mounted_credential(get(self.config, "qualification.daprApiCredentialReference"), "MountedQualificationDaprCredential")
                caller_token = read_mounted_credential(get(self.config, "qualification.enrollmentCredentialReference"), "MountedQualificationCallerCredential")
                connection.timeout = remaining()
                connection.connect()
                connection.sock.settimeout(remaining())
                connection.request("POST", f"/v1.0/invoke/{app}/method/ext-secrets-1/v1/{operation}", canonical(request),
                                   {"Content-Type": "application/json", "dapr-api-token": token, "Authorization": "Bearer " + caller_token})
                token = None
                caller_token = None
                response = connection.getresponse()
                if 300 <= response.status < 400:
                    raise QualificationFailed("DaprRedirectProhibited")
                if response.status != 200:
                    raise OSError("QualificationTransportUnavailable")
                body = bytearray()
                while True:
                    remaining()
                    chunk = response.read1(min(65536, 1024 * 1024 + 1 - len(body)))
                    if not chunk: break
                    body.extend(chunk)
                    if len(body) > 1024 * 1024:
                        raise QualificationFailed("OversizedProtocolResponse")
                remaining()
                result.put((True, decode_json(body)))
            except http.client.HTTPException:
                result.put((False, OSError("QualificationTransportUnavailable")))
            except Exception as exc:
                result.put((False, exc))
            finally:
                token = None
                caller_token = None
                if response is not None: response.close()
                connection.close()

        # Only transport work runs detached. No result/journal mutation can occur after caller timeout.
        worker = threading.Thread(target=read, daemon=True)
        worker.start()
        try:
            succeeded, value = result.get(timeout=remaining())
            remaining()
            if not succeeded: raise value
            return value
        except queue.Empty as exc:
            raise TimeoutError("QualificationCallDeadline") from exc
        finally:
            abort()


def check_binding(payload, request):
    for key in ("runId", "targetCommit", "isolatedTargetId", "planSha256", "requestId"):
        if payload.get(key) != request[key]:
            raise QualificationFailed("ExactRunTargetRequestBinding")


def check_observation(case, payload, execution, request):
    check_binding(payload, request)
    fields = {"protocol", "identity", "version", "observedAt", "validUntil", "runId", "targetCommit",
              "isolatedTargetId", "planSha256", "requestId", "caseId", "outcome", "resultSha256", "metrics",
              "snapshots", "linearization", "proofReferences", "trace", "caseTargetId", "retainedReceipts"}
    if set(payload) != fields or payload["caseId"] != case.id or payload["caseTargetId"] != request["caseTargetId"] or payload["outcome"] not in case.outcomes \
            or payload["resultSha256"] != execution["resultSha256"] or payload["outcome"] != execution["outcome"]:
        raise QualificationFailed("IndependentOutcome")
    expected = GLOBAL | case.expected
    metrics = payload["metrics"]
    if not isinstance(metrics, dict) or set(metrics) != set(expected) or any(type(metrics.get(k)) is not int or metrics[k] != v for k, v in expected.items()):
        raise QualificationFailed("PersistedInvariant")
    snapshots = payload["snapshots"]
    if not isinstance(snapshots, list) or len(snapshots) != len(STAGES):
        raise QualificationFailed("RecoveryPersistedProof")
    for snapshot, stage in zip(snapshots, STAGES):
        if not isinstance(snapshot, dict) or set(snapshot) != {"stage", "originalRequestId", "originalResultSha256", "stateSha256", "authorityRevision", "state"} \
                or snapshot["stage"] != stage or snapshot["originalRequestId"] != request["requestId"] \
                or snapshot["originalResultSha256"] != execution["resultSha256"] \
                or not SHA.fullmatch(snapshot["stateSha256"]) or not IDENTITY.fullmatch(snapshot["authorityRevision"]) \
                or snapshot["stateSha256"] != digest(snapshot["state"]):
            raise QualificationFailed("RecoveryPersistedProof")
    check_persisted_state(case, payload, execution, request)
    proofs = payload["proofReferences"]
    if not isinstance(proofs, list) or len(proofs) < 4 or len(set(proofs)) != len(proofs) \
            or any(not isinstance(v, str) or not SHA.fullmatch(v) for v in proofs):
        raise QualificationFailed("IndependentPersistedReferences")
    order = payload["linearization"]
    if case.boundary != "none":
        if not isinstance(order, dict) or set(order) != {"boundary", "triggerPosition", "effectPosition", "winner"} \
                or order["boundary"] != case.boundary or type(order["triggerPosition"]) is not int \
                or type(order["effectPosition"]) is not int or order["triggerPosition"] < 0 or order["effectPosition"] < 0 \
                or order["triggerPosition"] == order["effectPosition"] or order["winner"] not in ("Trigger", "Effect"):
            raise QualificationFailed("RaceLinearization")
        winner = "Trigger" if order["triggerPosition"] < order["effectPosition"] else "Effect"
        before_winner = "Effect" if case.id.startswith("S2.expiry.") else "Trigger"
        after_winner = "Trigger" if case.id.startswith("S2.expiry.") else "Effect"
        if order["winner"] != winner or case.boundary == "before" and winner != before_winner \
                or case.boundary == "after" and winner != after_winner:
            raise QualificationFailed("RaceLinearization")
        effect_outcomes = {"Delivered", "Consumed", "Completed", "Verified"}
        # Envelope expiry-before means verification won; delivery/block before means cancellation won.
        if not case.id.startswith(("S2.expiry.", "S3.delivery-expiry.", "v23.second-compromise.")) and (payload["outcome"] in effect_outcomes) != (winner == "Effect"):
            raise QualificationFailed("RaceOutcomeState")
    elif order != {}:
        raise QualificationFailed("UnexpectedRaceProof")


def check_persisted_state(case, payload, execution, request):
    states = [s["state"] for s in payload["snapshots"]]
    keys = {"requestId", "outcome", "receiptId", "tenantId", "logicalId", "sealId", "batchId", "manifestDigest",
            "attestationOrdinal", "keyVersion", "status", "targetIds", "destroyedTargetIds", "pinnedTargetIds",
            "activeCredentialIds", "providerCopyIds", "erasedCopyIds", "journalRevision", "deliveredAt", "exclusiveExpiry",
            "replayNamespace", "replayTenant", "nonceDigest", "firstSeenAt", "retainUntil", "actualIssuedGuardRevision", "attempts",
            "replayIssuer", "timing", "rotation", "attestationHistory"}
    scope = request["enrolledScope"]
    for state in states:
        if not isinstance(state, dict) or set(state) != keys or state["requestId"] != request["requestId"] \
                or state["outcome"] != execution["outcome"] or state["status"] != execution["outcome"] \
                or state["receiptId"] != execution["resultSha256"] or state["logicalId"] != scope["logicalId"] \
                or state["tenantId"] != scope["tenantId"] or not SHA.fullmatch(state["receiptId"] or "") \
                or not IDENTITY.fullmatch(state["tenantId"] or "") or type(state["journalRevision"]) is not int or state["journalRevision"] < 1:
            raise QualificationFailed("ExactPersistedState")
        for k in ("logicalId", "sealId", "batchId", "keyVersion", "status", "replayNamespace", "replayTenant", "replayIssuer"):
            if state[k] is not None and (not isinstance(state[k], str) or not IDENTITY.fullmatch(state[k])):
                raise QualificationFailed("ContentFreeStateSchema")
        for k in ("manifestDigest", "nonceDigest"):
            if state[k] is not None and (not isinstance(state[k], str) or not SHA.fullmatch(state[k])):
                raise QualificationFailed("ContentFreeStateSchema")
        for k in ("attestationOrdinal", "deliveredAt", "exclusiveExpiry", "firstSeenAt", "retainUntil", "actualIssuedGuardRevision"):
            if state[k] is not None and (type(state[k]) is not int or not 0 <= state[k] <= 9007199254740991):
                raise QualificationFailed("StateOrdinalOrInstant")
        for k in ("targetIds", "destroyedTargetIds", "pinnedTargetIds", "activeCredentialIds", "providerCopyIds", "erasedCopyIds"):
            values = state[k]
            if not isinstance(values, list) or len(values) != len(set(values)) or any(not isinstance(v, str) or not SHA.fullmatch(v) for v in values):
                raise QualificationFailed("ExactPersistedTargetVector")
        if len(state["activeCredentialIds"]) > 1 or set(state["destroyedTargetIds"]) & set(state["pinnedTargetIds"]) \
                or not set(state["pinnedTargetIds"]) <= set(state["targetIds"]) \
                or not set(state["destroyedTargetIds"]) <= set(state["targetIds"]):
            raise QualificationFailed("ProtectionPersistedInvariant")
        if state["targetIds"] != scope["targetIds"] or state["providerCopyIds"] != scope["providerCopyIds"]:
            raise QualificationFailed("EnrolledProtectionTargetScope")
        for metric, vector in (("activeCredentials", "activeCredentialIds"), ("destroyedTargets", "destroyedTargetIds")):
            if metric in payload["metrics"] and len(state[vector]) != payload["metrics"][metric]:
                raise QualificationFailed("MetricStateVectorContradiction")
        restrictive = {"Denied", "Blocked", "ConsumptionBlocked", "NotDelivered", "Expired", "Unknown", "ObsoleteUnissued", "ActivationBlocked", "BlockedAfterActivation"}
        if state["outcome"] in restrictive and state["destroyedTargetIds"]:
            raise QualificationFailed("DestructionAfterRestrictiveOutcome")
        if state["deliveredAt"] is not None and state["outcome"] != "Delivered":
            raise QualificationFailed("DeliveryAfterRestrictiveOutcome")
        if state["outcome"] in ("Consumed", "Erased", "AlreadyDestroyed", "Destroyed", "CompletionSealed") or case.id == "S3.lifecycle.destroy":
            if not state["targetIds"] or state["destroyedTargetIds"] != state["targetIds"]:
                raise QualificationFailed("EmptyOrPartialTerminalDestruction")
        if payload["metrics"].get("retainedPins") == 1 and not state["pinnedTargetIds"]:
            raise QualificationFailed("MissingPersistedPin")
        if payload["metrics"].get("atomicAllOrNone") == 1 and state["destroyedTargetIds"] not in ([], state["targetIds"]):
            raise QualificationFailed("PartialDestruction")
        if (case.id == "S3.all-copy-destroy-restore" or state["outcome"] == "Destroyed") and (not state["providerCopyIds"] or state["erasedCopyIds"] != state["providerCopyIds"]):
            raise QualificationFailed("AllCopyRestoreRevival")
        if case.id.startswith("S3.delivery") or case.id == "S3.principal-direct-delivery" or state["outcome"] == "Delivered":
            if payload["outcome"] == "Delivered" and (state["deliveredAt"] is None or state["exclusiveExpiry"] is None
                                                       or state["deliveredAt"] >= state["exclusiveExpiry"]):
                raise QualificationFailed("ExclusiveDeliveryExpiry")
            if payload["outcome"] == "NotDelivered" and state["deliveredAt"] is not None:
                raise QualificationFailed("FalseDelivery")
        if payload["outcome"] in ("Recorded", "DomainIdempotent"):
            if state["replayNamespace"] != "system/trusted-envelope-replay" or state["replayTenant"] != scope["tenantId"] \
                    or state["replayIssuer"] != scope["issuer"] or state["nonceDigest"] != scope["nonceDigest"] \
                    or state["firstSeenAt"] is None or state["retainUntil"] != state["firstSeenAt"] + 604800000:
                raise QualificationFailed("ExactReplayScopeRetention")
        if not isinstance(state["attempts"], list):
            raise QualificationFailed("PersistedSigningAttempts")
        if payload["metrics"].get("signingAttempts") == 3:
            check_attempts(state)
        elif state["attempts"]:
            raise QualificationFailed("UnexpectedSigningAttempts")
        check_timing_and_attestations(case, state, scope)
    # All four independent reads reproduce the exact original identity/outcome/receipt after restart/restore/lookup.
    if any(s != states[0] for s in states[1:]):
        raise QualificationFailed("RestartRestoreOriginalChanged")
    trace = payload["trace"]
    trace_fields = {"kind", "position", "requestId", "receiptId", "tenantId", "batchId", "keyVersion",
                    "guardRevision", "blockSetRevision", "occurredAt", "stateSha256", "originalReceiptId"}
    kinds = {"OriginalPersisted", "Effect", "Lookup", "Restart", "Restore", "Block", "Reserve", "Consume", "Activate",
             "ActivationBlocked", "Dispatch", "Hold", "Compromise", "Complete", "Sign", "NoIssue", "Issue",
             "Expiry", "Verify", "Release", "DenyRelease", "ActorRevocation", "Destroy", "GuardMutation"}
    if not isinstance(trace, list) or not 4 <= len(trace) <= 100:
        raise QualificationFailed("OrderedPersistedTrace")
    previous = -1
    for event in trace:
        if not isinstance(event, dict) or set(event) != trace_fields or event["kind"] not in kinds \
                or type(event["position"]) is not int or event["position"] <= previous \
                or event["requestId"] != request["requestId"] or event["tenantId"] != states[0]["tenantId"] \
                or event["batchId"] != states[0]["batchId"] or not SHA.fullmatch(event["receiptId"] or "") \
                or event["originalReceiptId"] != states[0]["receiptId"] or event["receiptId"] not in payload["proofReferences"] \
                or event["stateSha256"] != payload["snapshots"][0]["stateSha256"] or type(event["occurredAt"]) is not int \
                or type(event["guardRevision"]) is not int or type(event["blockSetRevision"]) is not int \
                or event["keyVersion"] is not None and not IDENTITY.fullmatch(event["keyVersion"]):
            raise QualificationFailed("OrderedPersistedTrace")
        previous = event["position"]
    # The retained owner receipt vector is independently read with the original state;
    # trace entries must identify those exact records, rather than arbitrary well-shaped hashes.
    retained_fields = trace_fields - {"stateSha256", "requestId", "tenantId", "batchId", "originalReceiptId"}
    retained = [{k: event[k] for k in retained_fields} for event in trace]
    if payload["retainedReceipts"] != retained:
        raise QualificationFailed("TraceDoesNotMatchRetainedReceipts")
    positions = {}
    for event in trace:
        positions.setdefault(event["kind"], []).append(event["position"])
    for kind in ("OriginalPersisted", "Restart", "Restore", "Lookup"):
        if kind not in positions:
            raise QualificationFailed("RequiredRecoveryTrace")
    if not positions["OriginalPersisted"][0] < positions["Restart"][0] < positions["Restore"][0] < positions["Lookup"][-1]:
        raise QualificationFailed("RecoveryTraceOrder")
    recovery = {"OriginalPersisted": "AfterEffect", "Restart": "AfterRestart", "Restore": "AfterRestore", "Lookup": "AfterLookup"}
    for kind, stage in recovery.items():
        event = next(e for e in trace if e["kind"] == kind)
        snapshot = next(s for s in payload["snapshots"] if s["stage"] == stage)
        if event["stateSha256"] != snapshot["stateSha256"] or event["receiptId"] != snapshot["state"]["receiptId"]:
            raise QualificationFailed("UnrelatedRecoveryReceiptOrState")
    if states[0]["outcome"] in ("Consumed", "CompletionSealed") and any(k not in positions for k in ("Dispatch", "Reserve", "Consume")):
        raise QualificationFailed("ConsumedWithoutDispatchReserveConsume")
    if states[0]["outcome"] in ("Consumed", "CompletionSealed") and not positions["Dispatch"][0] < positions["Reserve"][0] < positions["Consume"][0]:
        raise QualificationFailed("ConsumedOwnerOrder")
    if states[0]["outcome"] in ("Consumed", "CompletionSealed"):
        dispatch = next(e for e in trace if e["kind"] == "Dispatch")
        if type(states[0]["actualIssuedGuardRevision"]) is not int or states[0]["actualIssuedGuardRevision"] < 1 \
                or dispatch["guardRevision"] < states[0]["actualIssuedGuardRevision"] or dispatch["keyVersion"] != states[0]["keyVersion"]:
            raise QualificationFailed("ConsumedWithoutExactIssuedDispatch")
        for event in trace:
            if event["kind"] in ("Reserve", "Consume") and (event["keyVersion"] != states[0]["keyVersion"]
                    or event["guardRevision"] < states[0]["actualIssuedGuardRevision"]):
                raise QualificationFailed("ConsumptionReceiptOutsideOriginalKeyOrRevision")
    if (case.id in ("S3.lifecycle.destroy", "S3.all-copy-destroy-restore") or states[0]["outcome"] == "Destroyed") and "Destroy" not in positions:
        raise QualificationFailed("DestructionReceiptMissing")
    if "activations" in payload["metrics"] and len(positions.get("Activate", [])) != payload["metrics"]["activations"]:
        raise QualificationFailed("ActivationReceiptCountContradiction")
    if states[0]["outcome"] == "ConsumptionBlocked" and ("Block" not in positions or any(k in positions for k in ("Reserve", "Consume", "Destroy", "Release", "Activate"))):
        raise QualificationFailed("RecoveredBlockNotRestrictive")
    if states[0]["outcome"] == "ConsumptionBlocked":
        minimum_revision = states[0]["actualIssuedGuardRevision"] or 0
        if any(event["keyVersion"] != states[0]["keyVersion"] or event["guardRevision"] < minimum_revision
               for event in trace if event["kind"] == "Block"):
            raise QualificationFailed("BlockReceiptOutsideOriginalKeyOrRevision")
    if states[0]["outcome"] == "Delivered":
        releases = [e for e in trace if e["kind"] == "Release"]
        if len(releases) != 1 or releases[0]["occurredAt"] != states[0]["deliveredAt"]:
            raise QualificationFailed("NativeDeliveryReceiptMissing")
    if states[0]["outcome"] in ("Recorded", "DomainIdempotent"):
        original = next(e for e in trace if e["kind"] == "OriginalPersisted")
        if original["occurredAt"] != states[0]["firstSeenAt"]:
            raise QualificationFailed("OriginalReplayFirstSeenChanged")
    if states[0]["outcome"] == "Signed":
        signs = [e for e in trace if e["kind"] == "Sign"]
        if len(signs) != 1 or signs[0]["keyVersion"] != states[0]["keyVersion"] or signs[0]["guardRevision"] < 1 \
                or states[0]["actualIssuedGuardRevision"] is not None or states[0]["destroyedTargetIds"] or states[0]["activeCredentialIds"]:
            raise QualificationFailed("NativeSignatureIsNotIssueAuthority")
    if states[0]["outcome"] in ("Issued", "DeletionBatchDispatchAuthorized", "CompletionSealed"):
        issued_revision = states[0]["actualIssuedGuardRevision"]
        if type(issued_revision) is not int or issued_revision < 1 or len(states[0]["activeCredentialIds"]) != 1:
            raise QualificationFailed("NativeIssuedStateMissing")
        required = "Issue" if states[0]["outcome"] == "Issued" else "Dispatch" if states[0]["outcome"] == "DeletionBatchDispatchAuthorized" else "Complete"
        if len(positions.get(required, [])) != 1:
            raise QualificationFailed("NativeOperationReceiptMissing")
        event = next(e for e in trace if e["kind"] == required)
        if event["keyVersion"] != states[0]["keyVersion"] or event["guardRevision"] < issued_revision \
                or required == "Issue" and event["guardRevision"] != issued_revision:
            raise QualificationFailed("NativeIssuedReceiptMismatch")
        if required == "Complete" and positions["Consume"][0] >= positions["Complete"][0]:
            raise QualificationFailed("CompletionBeforeOriginalConsumption")
    if states[0]["outcome"] in {"Denied", "Blocked", "NotDelivered", "Expired", "ActivationBlocked", "BlockedAfterActivation"} \
            and any(k in positions for k in ("Effect", "Consume", "Destroy", "Release")):
        raise QualificationFailed("ForbiddenEffectAfterDenialOrBlock")
    if case.id.startswith("v23.") and "reserve." in case.id:
        if "Block" not in positions or "Reserve" not in positions:
            raise QualificationFailed("SameOwnerReserveBlockTrace")
        blocked_first = positions["Block"][0] < positions["Reserve"][0]
        if blocked_first != (payload["outcome"] == "Blocked") or blocked_first and "Consume" in positions:
            raise QualificationFailed("SameOwnerReserveBlockWinner")
    if case.id.startswith("v23.second-compromise."):
        if "Block" not in positions or "Consume" in positions or "Reserve" in positions:
            raise QualificationFailed("ReplacementKeyEffect")
        activated = "Activate" in positions
        if activated != (payload["outcome"] == "BlockedAfterActivation") \
                or activated and positions["Activate"][0] >= positions["Block"][0] \
                or not activated and "ActivationBlocked" not in positions:
            raise QualificationFailed("ActivationCompareWinner")
        block = next(e for e in trace if e["kind"] == "Block")
        compare = next(e for e in trace if e["kind"] in ("Activate", "ActivationBlocked"))
        if compare["keyVersion"] != block["keyVersion"] or compare["blockSetRevision"] >= block["blockSetRevision"]:
            raise QualificationFailed("ReplacementKeyBlockSetCompare")
        if block["keyVersion"] != scope["successorKeyVersion"]:
            raise QualificationFailed("ReplacementKeyOutsideEnrolledScope")
    if case.id in ("v23.same-batch-reattest", "Recovery.activation.lost-result"):
        required = ("Block", "Issue", "Dispatch", "Activate") + (("Reserve", "Consume") if states[0]["outcome"] == "Consumed" else ())
        if any(k not in positions for k in required) or not all(positions[a][0] < positions[b][0] for a, b in zip(required, required[1:])) \
                or states[0]["outcome"] == "Unconsumed" and (states[0]["destroyedTargetIds"] or "Reserve" in positions or "Consume" in positions):
            raise QualificationFailed("ReattestationOwnerOrder")
        if states[0]["outcome"] == "Unconsumed" and len(positions["Activate"]) != 1:
            raise QualificationFailed("NativeActivationReceiptCount")
        old, new = states[0]["attestationHistory"]
        block = next(e for e in trace if e["kind"] == "Block")
        dispatch = next(e for e in trace if e["kind"] == "Dispatch")
        if block["receiptId"] != old["blockReceiptId"] or dispatch["receiptId"] != new["dispatchReceiptId"] \
                or block["keyVersion"] != old["keyVersion"] or dispatch["keyVersion"] != new["keyVersion"]:
            raise QualificationFailed("ReattestationNotBoundToBlockAndDispatch")
        issued_revision = states[0]["actualIssuedGuardRevision"]
        if type(issued_revision) is not int or issued_revision < 1 or any(
                event["keyVersion"] != new["keyVersion"] or event["keyVersion"] != states[0]["keyVersion"]
                or event["guardRevision"] < issued_revision
                or event["kind"] == "Issue" and event["guardRevision"] != issued_revision
                for event in trace if event["kind"] in ("Issue", "Activate", "Dispatch")):
            raise QualificationFailed("ActivationReceiptOutsideSuccessorKeyOrRevision")
    if case.boundary != "none":
        trigger, effect = race_event_kinds(case.id, states[0]["outcome"])
        order = payload["linearization"]
        triggers = [e for e in trace if e["kind"] == trigger]
        effects = [e for e in trace if e["kind"] == effect]
        if len(triggers) != 1 or len(effects) != 1 or order.get("triggerPosition") != triggers[0]["position"] \
                or order.get("effectPosition") != effects[0]["position"]:
            raise QualificationFailed("RacePositionsNotPersistedEvents")
        timing = states[0]["timing"]
        if trigger in ("Expiry", "ActorRevocation"):
            instant = timing["exclusiveExpiry"] if trigger == "Expiry" else timing["authorityChangedAt"]
            if instant is None or triggers[0]["occurredAt"] != instant or effects[0]["occurredAt"] != timing["checkedAt"]:
                raise QualificationFailed("RaceTimingNotActualReceiptInstants")
        if trigger == "ActorRevocation" and states[0]["outcome"] == "Delivered" and states[0]["deliveredAt"] > timing["authorityChangedAt"]:
            raise QualificationFailed("DeliveryAfterActorRevocation")
    if payload["metrics"].get("signingAttempts") == 3:
        for kind, count in (("Sign", 3), ("NoIssue", 2), ("Issue", 1)):
            if len(positions.get(kind, [])) != count: raise QualificationFailed("SigningAttemptReceiptsMissing")
        signs = [e for e in trace if e["kind"] == "Sign"]
        no_issues = [e for e in trace if e["kind"] == "NoIssue"]
        if len({e["receiptId"] for e in no_issues}) != 2:
            raise QualificationFailed("ReusedTerminalNoIssueProof")
        issued = next(e for e in trace if e["kind"] == "Issue")
        for sign, attempt in zip(signs, states[0]["attempts"]):
            if sign["keyVersion"] != attempt["keyVersion"] or sign["guardRevision"] != attempt["intendedGuardRevision"]:
                raise QualificationFailed("SigningReceiptNotExactAttempt")
        for no_issue, attempt in zip(no_issues, states[0]["attempts"][:2]):
            if no_issue["receiptId"] != attempt["noIssueProofSha256"] or no_issue["keyVersion"] != attempt["keyVersion"] \
                    or no_issue["guardRevision"] != attempt["intendedGuardRevision"]:
                raise QualificationFailed("NoIssueReceiptNotExactTerminalProof")
        if not signs[0]["position"] < no_issues[0]["position"] < signs[1]["position"] < no_issues[1]["position"] < signs[2]["position"] < issued["position"]:
            raise QualificationFailed("TerminalNoIssueSigningOrder")
        if issued["keyVersion"] != states[0]["keyVersion"] or issued["guardRevision"] != states[0]["actualIssuedGuardRevision"]:
            raise QualificationFailed("IssueReceiptNotSuccessfulAttempt")
    if payload["metrics"].get("originalEffects") == 1 and len(positions.get("Effect", [])) != 1:
        raise QualificationFailed("OriginalEffectReceiptMissing")


def race_event_kinds(case_id, outcome):
    if case_id.startswith("S2.expiry."): return "Expiry", "Verify"
    if case_id.startswith("S3.delivery-expiry."): return "Expiry", "Release" if outcome == "Delivered" else "DenyRelease"
    if case_id.startswith("S3.delivery-actor-revocation."): return "ActorRevocation", "Release" if outcome == "Delivered" else "DenyRelease"
    if "second-compromise." in case_id: return "Block", "Activate" if outcome == "BlockedAfterActivation" else "ActivationBlocked"
    if "reserve." in case_id: return "Block", "Reserve"
    if "hold-dispatch." in case_id: return "Hold", "Dispatch"
    if "compromise-dispatch." in case_id: return "Compromise", "Dispatch"
    if "guard-mutation-completion." in case_id: return "GuardMutation", "Complete"
    raise QualificationFailed("UnknownRaceContract")


def check_timing_and_attestations(case, state, scope):
    timing = state["timing"]
    fields = {"checkedAt", "exclusiveExpiry", "releasedAt", "authorityChangedAt", "rotatedAt", "overlapEndsAt"}
    if not isinstance(timing, dict) or set(timing) != fields or any(v is not None and (type(v) is not int or v < 0) for v in timing.values()):
        raise QualificationFailed("ActualTimingSchema")
    if state["exclusiveExpiry"] is not None and state["exclusiveExpiry"] != scope["exclusiveExpiry"]:
        raise QualificationFailed("EnrolledExpiryChanged")
    capability_proof = case.expected.get("capabilityExpiryFields") == 0 or case.id.startswith((
        "Recovery.signing.", "Recovery.guard-issue.", "Recovery.dispatch.", "Recovery.consume.",
        "Recovery.activation.", "Recovery.compromise-block.", "Recovery.completion."))
    if capability_proof and (state["exclusiveExpiry"] is not None or timing["exclusiveExpiry"] is not None or timing["releasedAt"] is not None):
        raise QualificationFailed("NonExpiringCapabilityHasExpiryOrRelease")
    if case.id.startswith(("S2.expiry.", "S3.delivery-expiry.", "S3.delivery-actor-revocation.")) or case.id == "S3.principal-direct-delivery" or state["outcome"] == "Delivered":
        if timing["exclusiveExpiry"] != scope["exclusiveExpiry"] or state["exclusiveExpiry"] != scope["exclusiveExpiry"] or timing["checkedAt"] is None \
                or timing["releasedAt"] != state["deliveredAt"]:
            raise QualificationFailed("MissingActualExpiryInstant")
        if state["outcome"] == "Delivered" and not timing["checkedAt"] <= state["deliveredAt"] < timing["exclusiveExpiry"]:
            raise QualificationFailed("ReleaseOutsideExclusiveExpiry")
        if case.id.startswith("S2.expiry."):
            delta = timing["checkedAt"] - timing["exclusiveExpiry"]
            wanted = {"before": -1, "at": 0, "after": 1}[case.boundary]
            if (delta > 0) - (delta < 0) != wanted or (state["outcome"] == "Verified") != (delta < 0):
                raise QualificationFailed("EnvelopeExpiryBoundary")
        if case.id.startswith("S3.delivery-expiry."):
            delta = timing["checkedAt"] - timing["exclusiveExpiry"]
            wanted = {"before": 1, "at": 0, "after": -1}[case.boundary]
            if (delta > 0) - (delta < 0) != wanted or (state["outcome"] == "Delivered") != (delta < 0):
                raise QualificationFailed("DeliveryExpiryBoundary")
    rotation_needed = case.id in ("S1.rotation-invalidation", "S4.routine-rotation-retention", "v23.stale-attempt-with-rotation") or case.id.startswith("S2.overlap-")
    rotation = state["rotation"]
    if rotation_needed:
        fields = {"previousKeyVersion", "currentKeyVersion", "rotatedAt", "oldVerifyUntil", "publicAnchorId", "publicAnchorVersion"}
        if not isinstance(rotation, dict) or set(rotation) != fields or rotation["previousKeyVersion"] != scope["initialKeyVersion"] \
                or rotation["currentKeyVersion"] != scope["successorKeyVersion"] or rotation["previousKeyVersion"] == rotation["currentKeyVersion"] \
                or state["keyVersion"] != (rotation["previousKeyVersion"] if case.id.startswith("S2.overlap-") else rotation["currentKeyVersion"]) \
                or type(rotation["rotatedAt"]) is not int or rotation["rotatedAt"] < 1 \
                or type(rotation["oldVerifyUntil"]) is not int or rotation["oldVerifyUntil"] <= rotation["rotatedAt"] \
                or timing["rotatedAt"] != rotation["rotatedAt"] or not IDENTITY.fullmatch(rotation["publicAnchorId"] or "") \
                or not IDENTITY.fullmatch(rotation["publicAnchorVersion"] or ""):
            raise QualificationFailed("ActualHealthyRotation")
        if case.id.startswith("S2.overlap-"):
            end = rotation["rotatedAt"] + 600000
            wanted = {"before": -1, "at": 0, "after": 1}[case.id.rsplit("-", 1)[1]]
            delta = (timing["checkedAt"] or 0) - end
            if timing["overlapEndsAt"] != end or (delta > 0) - (delta < 0) != wanted:
                raise QualificationFailed("ActualRotationOverlapBoundary")
            if state["outcome"] == "Verified" and timing["checkedAt"] >= rotation["oldVerifyUntil"]:
                raise QualificationFailed("RetainedVerificationOutsideExclusiveKeyWindow")
    elif rotation is not None:
        raise QualificationFailed("UnexpectedRotationProof")
    history = state["attestationHistory"]
    if not isinstance(history, list): raise QualificationFailed("AttestationHistorySchema")
    batch_required = case.id.startswith(("v23.", "S4.capability")) or case.id == "S4.routine-rotation-retention" \
        or state["outcome"] in ("Signed", "Issued", "Consumed", "ConsumptionBlocked", "Unconsumed", "DeletionBatchDispatchAuthorized", "CompletionSealed")
    if batch_required and any(state[k] != scope[k] or not state[k] for k in ("sealId", "batchId", "manifestDigest")):
        raise QualificationFailed("MissingExactStableBatchIdentity")
    if batch_required and (type(state["attestationOrdinal"]) is not int or state["attestationOrdinal"] < scope["initialAttestationOrdinal"]
                          or state["keyVersion"] not in (scope["initialKeyVersion"], scope["successorKeyVersion"])):
        raise QualificationFailed("MissingEnrolledAttestationIdentity")
    if case.id in ("v23.same-batch-reattest", "Recovery.activation.lost-result"):
        fields = {"ordinal", "keyVersion", "credentialId", "status", "sealId", "batchId", "manifestDigest", "blockReceiptId", "dispatchReceiptId"}
        if len(history) != 2 or any(not isinstance(a, dict) or set(a) != fields for a in history):
            raise QualificationFailed("PredecessorSuccessorAttestationProof")
        old, new = history
        if old["ordinal"] != scope["initialAttestationOrdinal"] or new["ordinal"] != old["ordinal"] + 1 \
                or old["keyVersion"] != scope["initialKeyVersion"] or new["keyVersion"] != scope["successorKeyVersion"] \
                or old["keyVersion"] == new["keyVersion"] or old["status"] != "RevokedBlocked" or new["status"] != "Active" \
                or old["credentialId"] == new["credentialId"] or state["activeCredentialIds"] != [new["credentialId"]] \
                or state["attestationOrdinal"] != new["ordinal"] or state["keyVersion"] != new["keyVersion"]:
            raise QualificationFailed("PredecessorSuccessorAttestationIdentity")
        for a in history:
            if any(a[k] != scope[k] for k in ("sealId", "batchId", "manifestDigest")) or any(not SHA.fullmatch(a[k] or "") for k in ("credentialId", "blockReceiptId", "dispatchReceiptId")):
                raise QualificationFailed("PredecessorSuccessorAttestationReceipt")
    elif history:
        raise QualificationFailed("UnexpectedAttestationHistory")


def check_attempts(state):
    attempts = state["attempts"]
    fields = {"sealId", "batchId", "manifestDigest", "attestationOrdinal", "signingAttemptOrdinal", "intendedGuardRevision",
              "actualIssuedGuardRevision", "keyVersion", "requestId", "outcome", "noIssueProofSha256"}
    if len(attempts) != 3:
        raise QualificationFailed("TwoStaleAttemptsMissing")
    previous_revision = -1
    for ordinal, attempt in enumerate(attempts, 1):
        if not isinstance(attempt, dict) or set(attempt) != fields \
                or any(attempt[k] != state[k] for k in ("sealId", "batchId", "manifestDigest", "attestationOrdinal")) \
                or attempt["signingAttemptOrdinal"] != ordinal or type(attempt["intendedGuardRevision"]) is not int \
                or attempt["intendedGuardRevision"] <= previous_revision or not IDENTITY.fullmatch(attempt["keyVersion"] or "") \
                or not SHA.fullmatch(attempt["requestId"] or ""):
            raise QualificationFailed("StableSigningAttemptIdentity")
        previous_revision = attempt["intendedGuardRevision"]
        if ordinal < 3:
            if attempt["outcome"] != "ObsoleteUnissued" or attempt["actualIssuedGuardRevision"] is not None \
                    or not SHA.fullmatch(attempt["noIssueProofSha256"] or ""):
                raise QualificationFailed("ExactTerminalNoIssue")
        elif attempt["outcome"] != "Issued" or type(attempt["actualIssuedGuardRevision"]) is not int \
                or attempt["actualIssuedGuardRevision"] <= attempt["intendedGuardRevision"] \
                or attempt["actualIssuedGuardRevision"] != state["actualIssuedGuardRevision"] or attempt["noIssueProofSha256"] is not None:
            raise QualificationFailed("SuccessfulIssueRevisionSeparate")
    if len({a["requestId"] for a in attempts}) != 3 or not state["sealId"] or not state["batchId"] or not state["manifestDigest"]:
        raise QualificationFailed("DistinctDeterministicSigningAttempts")
    if state["keyVersion"] != attempts[-1]["keyVersion"]:
        raise QualificationFailed("FinalSigningKeyDoesNotMatchSuccessfulAttempt")


class RunJournal:
    """Durable originals survive process loss. A fresh invocation never repeats a started request."""
    def __init__(self, directory, config, target):
        self.directory = Path(directory)
        self.directory.mkdir(parents=True, exist_ok=True)
        self._sync_directory(self.directory.parent)
        active = self.directory / "active-run.json"
        identity = {"targetCommit": target, "isolatedTargetId": get(config, "qualification.isolatedTargetId"),
                    "configurationSha256": digest(config), "planSha256": plan_digest()}
        try:
            self.run = str(uuid.uuid4())
            self._publish(active, identity | {"runId": self.run})
        except FileExistsError:
            saved = read_json(active)
            if any(saved.get(k) != v for k, v in identity.items()):
                raise Unavailable("ExactOriginalRunRecoveryOrAcceptedDisjointTargetRequired")
            self.run = saved["runId"]
        self.originals = self.directory / self.run
        self.originals.mkdir(exist_ok=True)
        self._sync_directory(self.directory)
        self.lock = self.directory / "invocation.lock"
        try:
            self.lock.mkdir()
            self._sync_directory(self.directory)
        except FileExistsError as exc:
            raise Unavailable("ExclusiveOriginalRunRecoveryLock") from exc

    def begin(self, request):
        path = self.originals / (request["requestId"] + ".json")
        try:
            self._publish(path, request)
            return True
        except FileExistsError:
            if read_json(path) != request:
                raise QualificationFailed("OriginalJournalIdentityChanged")
            return False

    def is_started(self, request):
        path = self.originals / (request["requestId"] + ".json")
        if not path.exists(): return False
        if read_json(path) != request: raise QualificationFailed("OriginalJournalIdentityChanged")
        return True

    def bind_enrollment(self, installed):
        self._retain("enrolled-case-targets.json", {"caseTargetIds": installed["caseTargetIds"],
            "caseScopes": installed["caseScopes"], "qualifiedReferencesSha256": installed["qualifiedReferencesSha256"],
            "currentProfileSha256": installed["currentProfileSha256"]})

    def retain_outcome(self, request, execution):
        self._retain(request["requestId"] + "-outcome.json", {"outcome": execution["outcome"],
                     "resultSha256": execution["resultSha256"], "caseTargetId": execution["caseTargetId"]})

    def _retain(self, filename, value):
        path = self.originals / filename
        try:
            self._publish(path, value)
        except FileExistsError:
            if read_json(path) != value:
                raise QualificationFailed("ImmutableOriginalOutcomeOrEnrollmentChanged")

    @staticmethod
    def _publish(path, value):
        # A fully serialized/flushed temp file is linked atomically without replacing any original.
        descriptor, name = tempfile.mkstemp(prefix=".pending-", dir=path.parent)
        temporary = Path(name)
        try:
            with os.fdopen(descriptor, "wb") as handle:
                handle.write(canonical(value))
                handle.flush()
                os.fsync(handle.fileno())
            os.link(temporary, path)
            RunJournal._sync_directory(path.parent)
        finally:
            temporary.unlink(missing_ok=True)
            RunJournal._sync_directory(path.parent)

    def close(self):
        self.lock.rmdir()
        self._sync_directory(self.directory)

    @staticmethod
    def _sync_directory(directory):
        descriptor = os.open(directory, os.O_RDONLY | os.O_DIRECTORY)
        try:
            os.fsync(descriptor)
        finally:
            os.close(descriptor)


def execute(config, target, binding, transport, persist=lambda name, value: None, journal=None, case_ids=None):
    run = journal.run if journal else str(uuid.uuid4())
    base = {"protocol": PROTOCOL, "runId": run, "targetCommit": target,
            "isolatedTargetId": get(config, "qualification.isolatedTargetId"), "planSha256": plan_digest()}
    admission = base | {"requestId": digest(base | {"operation": "enroll"}), "operation": "enroll"}
    def enroll():
        try:
            enrolled = transport.call("enrollment", "enroll", admission)
        except (OSError, TimeoutError, urllib.error.URLError) as exc:
            raise Unavailable("IsolatedTargetEnrollmentAuthority", "InstalledOwnerQualificationExecutor", "IndependentPersistedStateObserver") from exc
        installed = verify_signed(enrolled, "enrollment", config, binding)
        check_binding(installed, admission)
        expected = set(base) | {"requestId", "identity", "version", "observedAt", "validUntil", "targetClass",
                           "productionMutationAllowed", "executorIdentity", "executorVersion", "observerIdentity",
                           "observerVersion", "qualifiedReferencesSha256", "completeCaseIds", "caseTargetIds", "caseScopes",
                           "currentProfileSha256", "currentProfileStatus"}
        if set(installed) != expected or installed["targetClass"] != "IsolatedQualification" \
            or installed["productionMutationAllowed"] is not False or installed["completeCaseIds"] != [c.id for c in plan()] \
            or installed["qualifiedReferencesSha256"] != digest(config["runtime"]) \
            or installed["currentProfileStatus"] != "Current" or installed["currentProfileSha256"] != digest(profile_identity(config)) \
            or not utc_epoch(get(config, "runtime.profileNotBefore")) <= time.time() < utc_epoch(get(config, "runtime.profileValidUntil")):
            raise QualificationFailed("IndependentIsolatedEnrollment")
        for role in ("executor", "observer"):
            if installed[f"{role}Identity"] != get(config, f"qualification.{role}.identity") \
                or installed[f"{role}Version"] != get(config, f"qualification.{role}.version"):
                raise QualificationFailed("InstalledAdapterIdentity")
        targets = installed["caseTargetIds"]
        if not isinstance(targets, dict) or set(targets) != {c.id for c in plan()} or len(set(targets.values())) != len(plan()) \
            or any(not isinstance(v, str) or not SHA.fullmatch(v) for v in targets.values()):
            raise QualificationFailed("IndependentlyDisjointCaseTargets")
        validate_scopes(installed["caseScopes"], targets, config)
        if journal: journal.bind_enrollment(installed)
        return enrolled, installed
    enrolled, installed = enroll()
    targets = installed["caseTargetIds"]
    persist("enrollment", enrolled)
    persist("effects-started", {"runId": run, "targetCommit": target})
    selected_cases = tuple(case for case in plan() if case_ids is None or case.id in case_ids)
    if case_ids is not None and set(case_ids) != {case.id for case in selected_cases}:
        raise QualificationFailed("ExactDisjointPhaseCaseSelection")
    results = []
    for case in selected_cases:
        request = base | {"caseId": case.id, "caseTargetId": targets[case.id], "enrolledScope": installed["caseScopes"][case.id], "scenario": {"outcomes": list(case.outcomes), "expected": GLOBAL | case.expected,
                                                       "boundary": case.boundary, "persistedStages": list(STAGES)}}
        request["requestId"] = digest(request)
        persist("pending-original", request)
        original_is_new = not journal.is_started(request) if journal else True
        if original_is_new:
            refreshed, current = enroll()
            stable = lambda value: {k: v for k, v in value.items() if k not in ("observedAt", "validUntil")}
            if stable(current) != stable(installed):
                raise QualificationFailed("EnrolledAuthorityChangedBeforeEffect")
            persist("current-enrollment-" + request["requestId"], refreshed)
            # Checked immediately before the new effect; no command/capability/expiry is renewed.
            if not time.time() < current["validUntil"] or not time.time() < utc_epoch(get(config, "runtime.profileValidUntil")):
                raise QualificationFailed("CurrentAuthorityExpiredBeforeEffect")
        if journal: original_is_new = journal.begin(request)
        # Publication may have used the remaining exclusive authority window. Once published,
        # an expired original stays started and can only be looked up on a later authorized run.
        if original_is_new and (not time.time() < current["validUntil"]
                or not utc_epoch(get(config, "runtime.profileNotBefore")) <= time.time() < utc_epoch(get(config, "runtime.profileValidUntil"))):
            raise QualificationFailed("CurrentAuthorityExpiredDuringPublication")
        try:
            envelope = transport.call("executor", "execute" if original_is_new else "lookup", request)
        except (OSError, TimeoutError, urllib.error.URLError):
            # An unknown effect never repeats execute. Only the same original identity is looked up once.
            envelope = transport.call("executor", "lookup", request)
        execution = verify_signed(envelope, "executor", config, binding)
        check_binding(execution, request)
        expected_execution = set(base) | {"identity", "version", "observedAt", "validUntil", "requestId", "caseId", "caseTargetId", "outcome", "resultSha256"}
        if set(execution) != expected_execution or execution["caseId"] != case.id or execution["outcome"] not in case.outcomes \
                or execution["caseTargetId"] != request["caseTargetId"] \
                or not isinstance(execution["resultSha256"], str) or not SHA.fullmatch(execution["resultSha256"]):
            raise QualificationFailed("ExactOriginalExecutionOutcome")
        if journal:
            journal.retain_outcome(request, execution)
        persist("execution", envelope)
        observed = transport.call("observer", "observe", request)
        observation = verify_signed(observed, "observer", config, binding)
        check_observation(case, observation, execution, request)
        # Retain complete validated signed evidence; schema excludes secret/content fields.
        results.append({"caseId": case.id, "requestId": request["requestId"], "execution": envelope,
                        "observation": observed})
        persist("case", results[-1])
    if [r["caseId"] for r in results] != [c.id for c in selected_cases]:
        raise QualificationFailed("IncompletePlan")
    return {"code": "QualificationPassed" if case_ids is None else "PhaseQualificationPassed", "dependency": "EXT-SECRETS-1", "dependencyAvailable": False,
            "permitsOwnerAcceptanceForExactTarget": True, "targetCommit": target, "runId": run,
            "planSha256": plan_digest(), "caseCount": len(results), "enrollmentProof": enrolled, "cases": results}


def profile_identity(config):
    return {k: config["runtime"][k] for k in ("profileReference", "issuer", "audience", "profileVersion", "profileNotBefore", "profileValidUntil", "timing")}


def validate_scopes(scopes, targets, config):
    fields = {"tenantId", "targetIds", "providerCopyIds", "logicalId", "sealId", "batchId", "manifestDigest",
              "initialAttestationOrdinal", "initialKeyVersion", "successorKeyVersion", "exclusiveExpiry", "issuer", "nonceDigest"}
    if not isinstance(scopes, dict) or set(scopes) != set(targets):
        raise QualificationFailed("EnrolledConcreteCaseScopes")
    all_targets = []
    for case_id, scope in scopes.items():
        if not isinstance(scope, dict) or set(scope) != fields or scope["tenantId"] not in (config["runtime"]["tenantA"], config["runtime"]["tenantB"], "system") \
                or scope["logicalId"] != targets[case_id] or scope["issuer"] != config["runtime"]["issuer"] \
                or not isinstance(scope["targetIds"], list) or not scope["targetIds"] or len(set(scope["targetIds"])) != len(scope["targetIds"]) \
                or not isinstance(scope["providerCopyIds"], list) or len(set(scope["providerCopyIds"])) != len(scope["providerCopyIds"]) \
                or any(not isinstance(v, str) or not SHA.fullmatch(v) for v in scope["targetIds"] + scope["providerCopyIds"]):
            raise QualificationFailed("EnrolledConcreteCaseScopes")
        for k in ("sealId", "batchId", "initialKeyVersion", "successorKeyVersion"):
            if not isinstance(scope[k], str) or not IDENTITY.fullmatch(scope[k]):
                raise QualificationFailed("EnrolledBatchAndKeyIdentity")
        if not SHA.fullmatch(scope["manifestDigest"] or "") or not SHA.fullmatch(scope["nonceDigest"] or "") \
                or type(scope["initialAttestationOrdinal"]) is not int or scope["initialAttestationOrdinal"] < 1 \
                or type(scope["exclusiveExpiry"]) is not int or scope["exclusiveExpiry"] < 1:
            raise QualificationFailed("EnrolledBatchAndKeyIdentity")
        all_targets.extend(scope["targetIds"])
    if len(set(all_targets)) != len(all_targets):
        raise QualificationFailed("IndependentlyDisjointProtectionTargets")


def main():
    parser = argparse.ArgumentParser()
    for name in ("root", "caller", "eventstore-argument", "artifacts-argument", "evidence-argument"):
        parser.add_argument(f"--{name}", required=True)
    args = parser.parse_args()
    evidence_root = Path(args.evidence_argument).resolve()
    evidence_root.mkdir(parents=True, exist_ok=True)
    evidence = evidence_root / ("attempt-" + str(uuid.uuid4()))
    evidence.mkdir()
    # Always retain the full immutable case plan, even when no prerequisites exist.
    (evidence / "live-plan.json").write_bytes(canonical({"protocol": PROTOCOL, "planSha256": plan_digest(), "cases": wire_plan()}))
    effects_started = False
    cohort_failure = {}
    journal = None
    try:
        root = Path(args.root).resolve()
        register = root.parent / "agents/_bmad-output/planning-artifacts/external-dependency-register.md"
        cohort_configuration = os.environ.get("PLATFORM_JOINT_COHORT_CONFIGURATION")
        if cohort_configuration:
            from joint_live_phase_runner import run_installed_cohort, Blocked
            try:
                result = run_installed_cohort(cohort_configuration, register)
            except Blocked as error:
                effects_started = error.effects_started
                cohort_failure = {"attemptState": error.attempt_state, "effectState": error.effect_state}
                raise Unavailable(*error.missing) from None
            (evidence / "joint-cohort-result.json").write_bytes(canonical(result))
            print("JointCohortPassed: EXT-SECRETS-1, EXT-HOST-1; acceptance unchanged.")
            return 0
        config, target, binding = preflight(root, os.environ.get("EXT_SECRETS_1_RUNTIME_CONFIGURATION"), register,
                                            args.caller, args.eventstore_argument, args.artifacts_argument, args.evidence_argument)
        def persist(name, value):
            nonlocal effects_started
            if name == "effects-started":
                effects_started = True
            suffix = value.get("requestId", value.get("caseId", "run"))
            safe_name = hashlib.sha256(str(suffix).encode()).hexdigest()
            (evidence / f"live-{name}-{safe_name}.json").write_bytes(canonical(value))
        journal = RunJournal(evidence_root / "original-run-journal", config, target)
        result = execute(config, target, binding, DaprTransport(config), persist, journal)
        exit_code = 0
    except Unavailable as exc:
        result = {"code": "DependencyNotAvailable", "dependency": "EXT-SECRETS-1", "dependencyAvailable": False,
                  "missingInputs": exc.inputs, "effectsStarted": effects_started, "qualificationEffects": 0 if not effects_started else "Unknown", **cohort_failure}
        exit_code = 2
    except ReplacementAcceptancePacket:
        result = {"code": "ReplacementAcceptancePacket", "dependency": "EXT-SECRETS-1", "dependencyAvailable": False,
                  "effectsStarted": effects_started, "qualificationEffects": 0}
        exit_code = 4
    except Exception:
        # Provider error strings and arbitrary response content never enter evidence, logs or stdout.
        result = {"code": "QualificationFailed", "dependency": "EXT-SECRETS-1", "dependencyAvailable": False,
                  "effectsStarted": effects_started, "originalLookupRequired": True}
        exit_code = 3
    finally:
        if journal is not None:
            journal.close()
    (evidence / "live-evidence.json").write_bytes(canonical(result))
    print(f"{result['code']}: EXT-SECRETS-1; evidence persisted.")
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
