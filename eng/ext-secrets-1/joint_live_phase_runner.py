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
        if missing:
            raise Blocked(*missing)
        return digest(b)

    def run(self):
        key = self.preflight()  # reserves nothing, including on refusal
        retained = self.authority.reserve(self.binding, str(uuid.uuid4()))
        if retained.get("bindingDigest") != key or not retained.get("attemptId"):
            raise Blocked("ExactRetainedAttempt")
        attempt = retained["attemptId"]
        previous = key
        for phase, name in enumerate(PHASES):
            if not self.authority.current(self.binding):
                raise Blocked("CurrentNonrollbackAcceptedSet")
            request = {"protocol": PROTOCOL, "attemptId": attempt, "bindingDigest": key,
                       "phase": phase, "owner": name, "previousSha256": previous,
                       "cases": partitions()[phase], "requestId": digest([attempt, key, phase, previous])}
            # False means an original may already have executed. Only exact lookup is legal.
            first = self.authority.retain(request)
            owner = self.owners[name]
            try:
                result = owner.execute(request) if first else owner.lookup(request)
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
        import urllib.request
        request_id = str(uuid.uuid4())
        request = {"protocol": PROTOCOL, "requestId": request_id, "method": method, "payload": payload}
        body = canonical(request)
        app = self.descriptor["appId"]
        if not re.fullmatch(r"[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}", app):
            raise Blocked("EnrolledDaprApplication")
        bearer = self.q.read_mounted_credential(self.descriptor["credentialReference"], "CohortWorkloadCredential")
        channel = self.q.read_mounted_credential(self.dapr_credential_reference, "CohortDaprCredential")
        url = self.endpoint + "/v1.0/invoke/" + app + "/method/joint/v1/" + method
        headers = {"Authorization": "Bearer " + bearer, "dapr-api-token": channel, "Content-Type": "application/json"}
        # No redirects, proxies or transport retries, including after uncertain owner execution.
        opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), _NoRedirect())
        with opener.open(urllib.request.Request(url, data=body, headers=headers, method="POST"), timeout=30) as response:
            raw = response.read(1024 * 1024 + 1)
        if len(raw) > 1024 * 1024:
            raise Blocked("BoundedCohortProof")
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

    def reserve(self, binding, attempt):
        return self.rpc.call("reserve", {"binding": binding, "attemptId": attempt})

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

    def execute(self, request):
        return self.rpc.call("execute", request)

    def lookup(self, request):
        return self.rpc.call("lookup", request)


def run_installed_cohort(configuration_path, register_path):
    """Common owner entrypoint; requires an explicit externally enrolled installation, never synthesizes ports."""
    import qualify as q
    config = q.read_json(configuration_path)
    binding = config["binding"]
    register = q.bounded_public_file(register_path, 1024 * 1024)
    if hashlib.sha256(register).hexdigest() != binding.get("registerSha256"):
        raise Blocked("ExactAcceptedCoordinatedRegister")
    text = register.decode("utf-8")
    for record in ("EXT-SECRETS-1", "EXT-HOST-1", "EXT-PROTECTION-1"):
        match = re.search(r"^### " + record + r"\b[^\n]*\n(.*?)(?=^### |^## |\Z)", text, re.M | re.S)
        section = match.group(1) if match else ""
        target = binding["platformCommit"] if record != "EXT-PROTECTION-1" else binding.get("protectionCommit")
        integration = binding["platformIntegrationDate"] if record != "EXT-PROTECTION-1" else binding["protectionIntegrationDate"]
        if (f'| `TargetVersionOrCommit` | `{target}` |' not in section or f'| `TargetIntegrationDate` | `{integration}` |' not in section
                or not re.search(r"\| `AcceptedStatus` \| `(?:Committed|Available)` \|", section)):
            raise Blocked("ExactAcceptedCoordinatedRecords")
    endpoint = config["daprEndpoint"]
    from urllib.parse import urlparse
    parsed = urlparse(endpoint)
    if parsed.scheme != "http" or parsed.hostname not in ("127.0.0.1", "localhost", "::1") or parsed.path not in ("", "/"):
        raise Blocked("LoopbackDaprEndpoint")
    owners = {name: DaprPhaseOwner(DaprCohortRpc(config["services"][name], endpoint, config["daprCredentialReference"])) for name in PHASES}
    authority = DaprCohortAuthority(DaprCohortRpc(config["services"]["authority"], endpoint, config["daprCredentialReference"]), owners)
    result = JointLivePhaseRunner(binding, authority, owners).run()
    if not verify_complete_result(result, binding, authority):
        raise Blocked("SameCompletePairedOwnerResult")
    return result
