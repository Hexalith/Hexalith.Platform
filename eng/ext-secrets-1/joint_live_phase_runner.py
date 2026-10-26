"""Retained OQ-5.4-1 coordinator. Construction/preflight have no live effects.

Ports must be independently enrolled, current authenticated owner services. A Python
callback or local flag is never installation evidence. There is deliberately no CLI
that constructs live ports from repository paths or starts an attempt.
"""
from datetime import date
import hashlib
import json
import os
from pathlib import Path
import re
import uuid

from cases import plan, wire_plan

PROTOCOL = "OQ-5.4-1.joint.v1"
PHASES = ("protection", "secrets", "host-refusing-consumers", "mutual")
RECOVERY_PHASES = {"lifecycle": 3, "delivery": 3, "signing": 3, "replay": 1,
                   "consume": 3, "compromise-block": 3, "activation": 3,
                   "guard-issue": 3, "dispatch": 3, "completion": 3}
REQUEST_DEADLINE_SECONDS = 30
HOST_CASES = ("H1", "H2", "H3", "H4", "v21", "v23")


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode()


def digest(value):
    return hashlib.sha256(canonical(value)).hexdigest()


def calendar_date(value):
    if not isinstance(value, str) or not re.fullmatch(r"[0-9]{4}-[0-9]{2}-[0-9]{2}", value):
        raise ValueError("ExplicitIntegrationDate")
    if date.fromisoformat(value).isoformat() != value:
        raise ValueError("ExplicitIntegrationDate")
    return value


def partitions():
    groups = {phase: [] for phase in range(4)}
    groups[0] = ["EXT-PROTECTION-1.complete-owner-command"]
    for case in plan():
        if case.id.startswith("Recovery."):
            phase = RECOVERY_PHASES[case.id.split(".")[1]]
        else:
            phase = 3 if case.id.startswith(("S3.", "S4.", "v23.", "Leakage.")) else 1
        groups[phase].append("secrets:" + case.id)
    groups[2] = ["host:H1", "host:H2", "host:all-consumers-refuse"]
    groups[3].extend("host:" + name for name in HOST_CASES if name not in ("H1", "H2"))
    flattened = [item for values in groups.values() for item in values]
    expected = {"secrets:" + case.id for case in plan()} | {"host:" + name for name in HOST_CASES} | {
        "host:all-consumers-refuse", "EXT-PROTECTION-1.complete-owner-command"}
    if len(flattened) != len(set(flattened)) or set(flattened) != expected:
        raise ValueError("DisjointCompleteCohortPlan")
    return groups


def full_plan():
    return {"protocol": PROTOCOL, "phases": PHASES, "partitions": partitions(),
            "secrets": wire_plan(), "hostRequirements": HOST_CASES,
            "recoveryAssignments": RECOVERY_PHASES, "identityHistoryDays": 365,
            "productGates": ["OD-HUMAN-EXACT-CONVERSATION-SCOPE-1", "OD-GOVERNANCE-CLASS-RANGE-SCOPE-1"]}


class Blocked(Exception):
    def __init__(self, *missing):
        self.missing = sorted(set(missing))
        # Local admission failures cannot establish absence of an earlier retained attempt.
        self.effects_started = True
        self.attempt_state = "LookupRequired"
        self.effect_state = "Unknown"
        self.attempt_id = self.phase_request_id = None
        super().__init__("DependencyNotAvailable: EXT-SECRETS-1, EXT-HOST-1")


class JointLivePhaseRunner:
    """One exclusive attempt per immutable accepted set, signed phase chain, no unknown resend.

    Authority methods: preflight(binding), reserve(binding, attempt), current(binding),
    retain(request), publish(request, result), verify(request, result). Reserve and
    retain are atomic durable operations outside the application restore cut.
    Owners execute(request) and lookup(request) retain exact original identities.
    Their results contain signed payloads with complete passedCases and state.
    """
    def __init__(self, binding, authority, owners):
        self.binding = json.loads(canonical(binding))
        self.authority, self.owners = authority, owners

    def preflight(self):
        missing = []
        b = self.binding
        if not re.fullmatch(r"[0-9a-f]{40}", str(b.get("platformCommit", ""))):
            missing.append("ImmutableSharedPlatformCommit")
        if b.get("secretsCommit") != b.get("platformCommit") or b.get("hostCommit") != b.get("platformCommit"):
            missing.append("SamePlatformTarget")
        for name in ("platformIntegrationDate", "protectionIntegrationDate"):
            try:
                calendar_date(b.get(name))
            except (TypeError, ValueError):
                missing.append(name)
        for name in ("configurationSha256", "profileSha256", "protectionProfileSha256", "executorAnchorSha256",
                     "observerAnchorSha256", "enrollmentAnchorSha256", "currentTrustSha256", "installedImageSha256"):
            if not re.fullmatch(r"[0-9a-f]{64}", str(b.get(name, ""))):
                missing.append(name)
        if b.get("planSha256") != digest(full_plan()):
            missing.append("CompleteCohortPlan")
        if b.get("host") != "192.168.1.30" or b.get("partiesBranch") != "B" or b.get("timing") != {
                "L": 300, "S": 30, "O": 600, "H": 86400, "R": 604800}:
            missing.append("AcceptedConstants")
        if self.authority is None:
            missing.append("IndependentCurrentAttemptAuthority")
        else:
            missing.extend(self.authority.preflight(b))
        if set(self.owners) != set(PHASES):
            missing.append("InstalledIndependentPhaseOwners")
        readiness = []
        if not missing:
            for phase, name in enumerate(PHASES):
                report = self.owners[name].preflight(b, phase, partitions()[phase])
                missing.extend(report.get("missingInputs", ["CurrentInstalledPhaseReadiness"]))
                readiness.append(report.get("readiness"))
        if missing:
            raise Blocked(*missing)
        self._readiness = readiness
        return digest(b)

    def run(self):
        self.attempt_state, self.effect_state = "LookupRequired", "Unknown"
        self.attempt_id = self.phase_request_id = None
        try:
            return self._run()
        except Exception as error:
            blocked = error if isinstance(error, Blocked) else Blocked("ExactOriginalLookupRequired")
            blocked.attempt_state, blocked.effect_state = self.attempt_state, self.effect_state
            blocked.attempt_id, blocked.phase_request_id = self.attempt_id, self.phase_request_id
            blocked.effects_started = self.effect_state != "NotDispatched"
            raise blocked from None

    def run_complete(self):
        result = self.run()
        try:
            if not verify_complete_result(result, self.binding, self.authority):
                raise Blocked("SameCompletePairedOwnerResult")
        except Exception as error:
            blocked = error if isinstance(error, Blocked) else Blocked("SameCompletePairedOwnerResult")
            blocked.attempt_state, blocked.effect_state = self.attempt_state, self.effect_state
            blocked.attempt_id, blocked.phase_request_id = self.attempt_id, self.phase_request_id
            blocked.effects_started = self.effect_state != "NotDispatched"
            raise blocked from None
        return result

    def _run(self):
        # Establish retained uncertainty before any mutable owner readiness can fail.
        key = digest(self.binding)
        retained_state = self.authority.lookup_attempt(self.binding)
        if not isinstance(retained_state, dict) or retained_state.get("bindingDigest") != key:
            raise Blocked("AuthoritativeRetainedAttemptLookupRequired")
        if retained_state.get("state") == "Absent" and retained_state.get("effectState") == "NotDispatched":
            self.attempt_state, self.effect_state = "NotReserved", "NotDispatched"
        elif retained_state.get("state") == "Retained" and retained_state.get("attemptId"):
            self.attempt_state, self.attempt_id = "Reserved", retained_state["attemptId"]
            self.phase_request_id = retained_state.get("phaseRequestId")
            self.effect_state = retained_state.get("effectState") if retained_state.get("effectState") in ("Unknown", "NotDispatched") else "Unknown"
        else:
            raise Blocked("AuthoritativeRetainedAttemptLookupRequired")
        key = self.preflight()  # reserves nothing, including on refusal
        self.attempt_state = "ReservationUnknown"
        retained = self.authority.reserve(self.binding, str(uuid.uuid4()), self._readiness)
        if retained.get("bindingDigest") != key or not retained.get("attemptId"):
            raise Blocked("ExactRetainedAttempt")
        self.attempt_state = "Reserved"
        attempt = retained["attemptId"]
        self.attempt_id = attempt
        previous = key
        for phase, name in enumerate(PHASES):
            if not self.authority.current(self.binding):
                raise Blocked("CurrentNonrollbackAcceptedSet")
            request = {"protocol": PROTOCOL, "attemptId": attempt, "bindingDigest": key,
                       "phase": phase, "owner": name, "previousSha256": previous,
                       "cases": partitions()[phase], "requestId": digest([attempt, key, phase, previous])}
            self.phase_request_id = request["requestId"]
            # False means an original may already have executed. Only exact lookup is legal.
            self.effect_state = "Unknown" # A retained predecessor may already have dispatched.
            first = self.authority.retain(request)
            owner = self.owners[name]
            try:
                self.effect_state = "Dispatched" if first else "Unknown"
                result = owner.execute(request) if first else owner.lookup(request)
                if not first and isinstance(result, dict) and result.get("payload", {}).get("state") == "OwnerAbsent":
                    import time
                    proof = result["payload"]; now = int(time.time())
                    if (not self.authority.verify(request, result) or proof.get("requestId") != request["requestId"]
                            or proof.get("requestSha256") != digest(request) or proof.get("attemptId") != attempt
                            or proof.get("bindingDigest") != key or proof.get("phase") != phase
                            or not now - 30 <= proof.get("observedAt", 0) <= now < proof.get("validUntil", 0) <= proof["observedAt"] + 30
                            or not hasattr(owner, "execute_if_absent")):
                        raise Blocked("ExactCurrentOwnerAbsenceRequired")
                    self.effect_state = "Dispatched"
                    result = owner.execute_if_absent(request, result)
            except Exception:
                result = None
            if result is None:
                raise Blocked("ExactOriginalLookupRequired", request["requestId"])
            if not self.authority.verify(request, result):
                raise Blocked("IndependentSignedPhaseChain")
            payload = result.get("payload", {})
            if any(payload.get(field) != request[field] for field in ("attemptId", "bindingDigest", "phase", "previousSha256", "requestId")):
                raise Blocked("ExactOriginalPhaseIdentity")
            # Persist failed/uncertain signed outcomes as well as success; never reset an attempt.
            self.authority.publish(request, result)
            if payload.get("state") != "Passed" or payload.get("passedCases") != request["cases"]:
                raise Blocked("CompletePairedCohortPass", request["requestId"])
            self.effect_state = "Observed"
            previous = digest(result)
        return {"protocol": PROTOCOL, "attemptId": attempt, "bindingDigest": key,
                "resultSha256": previous, "records": ["EXT-SECRETS-1", "EXT-HOST-1"],
                "cohortPassed": True, "dependencyAvailable": False, "acceptanceMutation": False}


def verify_complete_result(result, binding, authority):
    """Both owner entrypoints use the same independent exact retained completion lookup."""
    if authority is None or not authority.current(binding):
        return False
    return authority.verify_completion(binding, result) and result.get("bindingDigest") == digest(binding) and result.get("cohortPassed") is True

class DaprCohortRpc:
    """Concrete Dapr-only private transport with public ES256 verification and a bounded deadline."""
    def __init__(self, descriptor, dapr_endpoint, dapr_credential_reference):
        import qualify as q
        self.q, self.descriptor = q, descriptor
        self.endpoint = dapr_endpoint.rstrip("/")
        self.dapr_credential_reference = dapr_credential_reference
        raw = q.read_public_anchor(descriptor["publicAnchorFile"])
        if hashlib.sha256(raw).hexdigest() != descriptor["publicAnchorSha256"]:
            raise Blocked("ExactInstalledCohortAnchor")
        from cryptography.hazmat.primitives import serialization
        self.key = serialization.load_pem_public_key(raw)
        q.public_key_identity(raw)

    def verify(self, envelope):
        try:
            import base64
            from cryptography.hazmat.primitives import hashes
            from cryptography.hazmat.primitives.asymmetric import ec
            if set(envelope) != {"payload", "signature"}:
                return False
            signature = base64.b64decode(envelope["signature"], validate=True)
            self.key.verify(signature, canonical(envelope["payload"]), ec.ECDSA(hashes.SHA256()))
            return True
        except Exception:
            return False

    def call(self, method, payload):
        import http.client
        import queue
        import socket
        import threading
        import time
        from urllib.parse import urlparse
        request_id = str(uuid.uuid4())
        request = {"protocol": PROTOCOL, "requestId": request_id, "method": method, "payload": payload}
        body = canonical(request)
        app = self.descriptor["appId"]
        if not re.fullmatch(r"[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}", app):
            raise Blocked("EnrolledDaprApplication")
        parsed = urlparse(self.endpoint)
        if parsed.scheme != "http" or parsed.hostname not in ("127.0.0.1", "localhost", "::1"):
            raise Blocked("LoopbackDaprEndpoint")
        deadline, result = time.monotonic() + REQUEST_DEADLINE_SECONDS, queue.Queue(maxsize=1)
        connection = http.client.HTTPConnection(parsed.hostname, parsed.port or 80, timeout=REQUEST_DEADLINE_SECONDS)
        def remaining():
            seconds = deadline - time.monotonic()
            if seconds <= 0: raise TimeoutError("CohortMonotonicDeadline")
            return seconds
        def read():
            try:
                bearer = self.q.read_mounted_credential(self.descriptor["credentialReference"], "CohortWorkloadCredential")
                channel = self.q.read_mounted_credential(self.dapr_credential_reference, "CohortDaprCredential")
                connection.timeout = remaining(); connection.connect()
                connection.sock.settimeout(remaining())
                connection.request("POST", "/v1.0/invoke/" + app + "/method/joint/v1/" + method, body,
                    {"Authorization": "Bearer " + bearer, "dapr-api-token": channel, "Content-Type": "application/json"})
                bearer = channel = None
                response = connection.getresponse()
                if response.status != 200: raise Blocked("CurrentCohortTransport")
                raw = bytearray()
                while True:
                    remaining()
                    chunk = response.read1(min(65536, 1024 * 1024 + 1 - len(raw)))
                    if not chunk: break
                    raw.extend(chunk)
                    if len(raw) > 1024 * 1024: raise Blocked("BoundedCohortProof")
                remaining(); result.put((True, bytes(raw)))
            except Exception as error:
                result.put((False, error))
            finally:
                connection.close()
        threading.Thread(target=read, daemon=True).start()
        try:
            succeeded, raw = result.get(timeout=remaining())
            remaining()
            if not succeeded: raise raw
        except queue.Empty:
            raise Blocked("CohortMonotonicDeadline") from None
        finally:
            if connection.sock is not None:
                try: connection.sock.shutdown(socket.SHUT_RDWR)
                except OSError: pass
            connection.close()
        envelope = self.q.decode_json(raw)
        if not self.verify(envelope):
            raise Blocked("SignedCurrentCohortProof")
        proof = envelope["payload"]
        import time
        now = int(time.time())
        if (proof.get("requestId") != request_id or proof.get("method") != method or proof.get("requestSha256") != hashlib.sha256(body).hexdigest()
                or not now - 30 <= proof.get("observedAt", 0) <= now or not now < proof.get("validUntil", 0) <= proof["observedAt"] + 30):
            raise Blocked("ExactCurrentCohortResponse")
        return proof["result"]


import urllib.request as _urlrequest
class _NoRedirect(_urlrequest.HTTPRedirectHandler):
    def redirect_request(self, request, file, code, message, headers, url):
        return None


class DaprCohortAuthority:
    def __init__(self, rpc, owners):
        self.rpc, self.owners = rpc, owners

    def preflight(self, binding):
        return self.rpc.call("preflight", {"binding": binding}).get("missingInputs", ["IndependentCohortAdmission"])

    def current(self, binding):
        return self.rpc.call("current", {"binding": binding}).get("current") is True

    def lookup_attempt(self, binding):
        return self.rpc.call("lookup-attempt", {"binding":binding})

    def reserve(self, binding, attempt, readiness):
        return self.rpc.call("reserve", {"binding": binding, "attemptId": attempt, "ownerReadiness": readiness})

    def retain(self, request):
        return self.rpc.call("retain", {"request": request}).get("first") is True

    def publish(self, request, result):
        if self.rpc.call("publish", {"request": request, "result": result}).get("exact") is not True:
            raise Blocked("DurableExactPhasePublication")

    def verify(self, request, result):
        return self.owners[PHASES[request["phase"]]].rpc.verify(result)

    def verify_completion(self, binding, result):
        return self.rpc.call("completion", {"binding": binding, "result": result}).get("exact") is True


class DaprPhaseOwner:
    def __init__(self, rpc):
        self.rpc = rpc

    def preflight(self, binding, phase, cases):
        return self.rpc.call("preflight", {"binding": binding, "phase": phase, "cases": cases})

    def execute(self, request):
        return self.rpc.call("execute", request)

    def lookup(self, request):
        return self.rpc.call("lookup", request)

    def execute_if_absent(self, request, absence):
        return self.rpc.call("execute-if-absent", {"request": request, "absence": absence})


def run_installed_cohort(configuration_path, register_path):
    """Preserve uncertainty through every local refusal before authenticated retained-attempt lookup."""
    try:
        return _run_installed_cohort(configuration_path, register_path)
    except Blocked:
        raise
    except Exception:
        raise Blocked("ValidCoordinatedInstallationAndExactOriginalLookup") from None


def _run_installed_cohort(configuration_path, register_path):
    """Common owner entrypoint; requires an explicit externally enrolled installation, never synthesizes ports."""
    import qualify as q
    configuration_bytes = q.bounded_public_file(configuration_path, 1024 * 1024)
    config = q.decode_json(configuration_bytes)
    if not isinstance(config, dict) or not isinstance(config.get("binding"), dict): raise Blocked("JointConfiguration.Structure")
    binding = config["binding"]
    validate_source_checkout(binding)
    from coordinated_packets import validate_joint_configuration
    missing = validate_joint_configuration(config)
    if missing: raise Blocked(*missing)
    register = q.bounded_public_file(register_path, 1024 * 1024)
    text = register.decode("utf-8")
    for record in ("EXT-SECRETS-1", "EXT-HOST-1", "EXT-PROTECTION-1"):
        match = re.search(r"^### " + record + r"\b[^\n]*\n(.*?)(?=^### |^## |\Z)", text, re.M | re.S)
        section = match.group(1) if match else ""
        target = binding["platformCommit"] if record != "EXT-PROTECTION-1" else binding.get("protectionCommit")
        integration = binding["platformIntegrationDate"] if record != "EXT-PROTECTION-1" else binding["protectionIntegrationDate"]
        if (f'| `TargetVersionOrCommit` | `{target}` |' not in section or f'| `TargetIntegrationDate` | `{integration}` |' not in section
                or not re.search(r"\| `AcceptedStatus` \| `(?:Committed|Available)` \|", section)):
            raise Blocked("ExactAcceptedCoordinatedRecords")
        if record != "EXT-PROTECTION-1":
            pins = re.search(r"\| `JointQualificationBinding` \| `([^`]+)` \|", section)
            expected = {"configurationSha256": hashlib.sha256(configuration_bytes).hexdigest(),
                        "bindingSha256": digest(binding), "planSha256": digest(full_plan())}
            if pins is None or q.decode_json(pins.group(1)) != expected:
                raise Blocked("ExactAcceptedJointConfigurationAndTrust")
        else:
            pins = re.search(r"\| `ProtectionQualificationBinding` \| `([^`]+)` \|", section)
            expected = {"targetCommit": binding["protectionCommit"], "integrationDate": integration,
                        "profileSha256": binding["protectionProfileSha256"],
                        "configurationSha256": hashlib.sha256(configuration_bytes).hexdigest(), "planSha256": digest(full_plan())}
            if pins is None or q.decode_json(pins.group(1)) != expected:
                raise Blocked("ExactAcceptedProtectionConfigurationProfileAndPlan")
    endpoint = config["daprEndpoint"]
    from urllib.parse import urlparse
    parsed = urlparse(endpoint)
    if parsed.scheme != "http" or parsed.hostname not in ("127.0.0.1", "localhost", "::1") or parsed.path not in ("", "/"):
        raise Blocked("LoopbackDaprEndpoint")
    owners = {name: DaprPhaseOwner(DaprCohortRpc(config["services"][name], endpoint, config["daprCredentialReference"])) for name in PHASES}
    authority = DaprCohortAuthority(DaprCohortRpc(config["services"]["authority"], endpoint, config["daprCredentialReference"]), owners)
    return JointLivePhaseRunner(binding, authority, owners).run_complete()


def validate_source_checkout(binding, root=None):
    """Validate the executing source checkout before contacting any admission service."""
    import subprocess
    import qualify as q
    root = Path(__file__).resolve().parents[2] if root is None else Path(root)
    try:
        if q.git(root, "rev-parse", "HEAD") != binding.get("platformCommit") or q.git(root, "status", "--porcelain"):
            raise Blocked("ExactCleanExecutingPlatformCheckout")
    except (OSError, subprocess.SubprocessError):
        raise Blocked("ExactCleanExecutingPlatformCheckout") from None
