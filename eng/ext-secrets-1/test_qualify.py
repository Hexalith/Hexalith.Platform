"""Offline protocol/gate tests. Ephemeral signatures and fake transport are never Live evidence."""
import base64
import copy
import hashlib
import http.server
import json
from pathlib import Path
import subprocess
import tempfile
import threading
import time
import unittest
from unittest.mock import patch

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.hazmat.primitives.asymmetric.utils import decode_dss_signature

import qualify as q
from cases import plan, plan_digest


def enrolled_scope(case, target, issuer="issuer-ref"):
    targets = [q.digest({"target": case.id, "i": i}) for i in (1, 2)]
    return {"tenantId": "system" if "SecurityObservation" in case.id else "tenant-a", "targetIds": targets,
        "providerCopyIds": [q.digest({"copy": case.id, "i": i}) for i in (1, 2)] if case.id in ("S3.all-copy-destroy-restore", "Recovery.lifecycle.lost-result") else [],
        "logicalId": target, "sealId": "seal", "batchId": "batch", "manifestDigest": q.digest(targets),
        "initialAttestationOrdinal": 1, "initialKeyVersion": "v1", "successorKeyVersion": "v3", "exclusiveExpiry": 1000,
        "issuer": issuer, "nonceDigest": q.digest({"nonce": case.id})}


def state(request, outcome, case, result_sha):
    scope = request["enrolledScope"]
    result = {k: None for k in ("sealId", "batchId", "manifestDigest", "attestationOrdinal", "keyVersion", "deliveredAt",
              "exclusiveExpiry", "replayNamespace", "replayTenant", "nonceDigest", "firstSeenAt", "retainUntil", "actualIssuedGuardRevision", "replayIssuer", "rotation")}
    result.update(requestId=request["requestId"], outcome=outcome, receiptId=result_sha, tenantId=scope["tenantId"], status=outcome,
                  journalRevision=1, attempts=[], logicalId=scope["logicalId"], attestationHistory=[])
    result.update({k: [] for k in ("destroyedTargetIds", "pinnedTargetIds", "activeCredentialIds", "erasedCopyIds")})
    result.update(targetIds=scope["targetIds"], providerCopyIds=scope["providerCopyIds"],
                  timing={k: None for k in ("checkedAt", "exclusiveExpiry", "releasedAt", "authorityChangedAt", "rotatedAt", "overlapEndsAt")})
    if outcome in ("Consumed", "Erased", "AlreadyDestroyed", "Destroyed", "CompletionSealed") or case.id == "S3.lifecycle.destroy": result["destroyedTargetIds"] = list(scope["targetIds"])
    if case.expected.get("retainedPins") == 1: result["pinnedTargetIds"] = list(scope["targetIds"])
    if case.expected.get("activeCredentials") == 1: result["activeCredentialIds"] = [q.digest({"credential": case.id})]
    if case.id.startswith(("S3.delivery", "S2.expiry.")) or case.id == "S3.principal-direct-delivery" or outcome == "Delivered":
        checked = 900
        if case.id.startswith("S2.expiry."): checked = {"before": 999, "at": 1000, "after": 1001}[case.boundary]
        if case.id.startswith("S3.delivery-expiry."): checked = {"before": 1001, "at": 1000, "after": 999}[case.boundary]
        delivered = checked if outcome == "Delivered" else None
        result.update(exclusiveExpiry=1000, deliveredAt=delivered)
        result["timing"].update(checkedAt=checked, exclusiveExpiry=1000, releasedAt=delivered,
            authorityChangedAt=checked if "actor-revocation" in case.id else None)
    if outcome in ("Recorded", "DomainIdempotent"):
        result.update(replayNamespace="system/trusted-envelope-replay", replayTenant=scope["tenantId"], replayIssuer=scope["issuer"],
                      nonceDigest=scope["nonceDigest"], firstSeenAt=1000, retainUntil=604801000)
    if case.id == "S3.all-copy-destroy-restore" or outcome == "Destroyed": result["erasedCopyIds"] = list(scope["providerCopyIds"])
    if case.id.startswith(("v23.", "S4.capability")) or case.id == "S4.routine-rotation-retention" \
            or outcome in ("Signed", "Consumed", "ConsumptionBlocked", "Unconsumed", "Issued", "DeletionBatchDispatchAuthorized", "CompletionSealed"):
        result.update({k: scope[k] for k in ("sealId", "batchId", "manifestDigest")})
        result.update(attestationOrdinal=1, keyVersion="v1")
    if outcome in ("Consumed", "Unconsumed", "Issued", "DeletionBatchDispatchAuthorized", "CompletionSealed"):
        result["actualIssuedGuardRevision"] = 1
    if outcome in ("Issued", "DeletionBatchDispatchAuthorized", "CompletionSealed"):
        result["activeCredentialIds"] = [q.digest({"credential": case.id})]
    if case.expected.get("signingAttempts") == 3:
        result.update(attestationOrdinal=1, keyVersion="v3" if "with-rotation" in case.id else "v1", actualIssuedGuardRevision=31)
        result["attempts"] = [{"sealId": scope["sealId"], "batchId": scope["batchId"], "manifestDigest": scope["manifestDigest"], "attestationOrdinal": 1,
            "signingAttemptOrdinal": i, "intendedGuardRevision": i * 10, "actualIssuedGuardRevision": 31 if i == 3 else None,
            "keyVersion": f"v{i}" if "with-rotation" in case.id else "v1", "requestId": q.digest({"attempt": i}), "outcome": "Issued" if i == 3 else "ObsoleteUnissued",
            "noIssueProofSha256": None if i == 3 else q.digest({"event": "NoIssue", "case": case.id, "position": 6 if i == 1 else 8})} for i in (1, 2, 3)]
    if case.id in ("S1.rotation-invalidation", "S4.routine-rotation-retention", "v23.stale-attempt-with-rotation") or case.id.startswith("S2.overlap-"):
        result.update(keyVersion="v1" if case.id.startswith("S2.overlap-") else "v3", rotation={"previousKeyVersion": "v1", "currentKeyVersion": "v3", "rotatedAt": 1000,
            "oldVerifyUntil": 601000, "publicAnchorId": "anchor", "publicAnchorVersion": "v1"})
        result["timing"]["rotatedAt"] = 1000
        if case.id.startswith("S2.overlap-"):
            result["timing"].update(overlapEndsAt=601000, checkedAt=601000 + {"before": -1, "at": 0, "after": 1}[case.id.rsplit("-", 1)[1]])
    if case.id in ("v23.same-batch-reattest", "Recovery.activation.lost-result"):
        result.update(keyVersion="v3", attestationOrdinal=2)
        result["attestationHistory"] = [{"ordinal": i, "keyVersion": "v1" if i == 1 else "v3", "credentialId": q.digest({"credential": case.id, "ordinal": i}),
            "status": "RevokedBlocked" if i == 1 else "Active", "sealId": scope["sealId"], "batchId": scope["batchId"], "manifestDigest": scope["manifestDigest"],
            "blockReceiptId": q.digest({"event": "Block", "case": case.id}), "dispatchReceiptId": q.digest({"event": "Dispatch", "case": case.id})} for i in (1, 2)]
        result["activeCredentialIds"] = [result["attestationHistory"][-1]["credentialId"]]
    return result


def observation(request, execution, case):
    persisted = state(request, execution["outcome"], case, execution["resultSha256"])
    payload = {k: request[k] for k in ("protocol", "runId", "targetCommit", "isolatedTargetId", "planSha256", "requestId", "caseId", "caseTargetId")}
    payload.update(outcome=execution["outcome"], resultSha256=execution["resultSha256"], metrics=q.GLOBAL | case.expected, linearization={})
    sequence = [("OriginalPersisted", 50), ("Restart", 100), ("Restore", 200), ("Lookup", 300)]
    if execution["outcome"] in ("Consumed", "CompletionSealed"): sequence += [("Dispatch", 3), ("Reserve", 5), ("Consume", 7)]
    if execution["outcome"] == "CompletionSealed": sequence += [("Complete", 9)]
    if execution["outcome"] == "Delivered": sequence += [("Release", 5)]
    if execution["outcome"] == "Signed": sequence += [("Sign", 5)]
    if execution["outcome"] == "ConsumptionBlocked": sequence += [("Block", 5)]
    if execution["outcome"] == "Issued" and case.expected.get("signingAttempts") != 3: sequence += [("Issue", 5)]
    if execution["outcome"] == "DeletionBatchDispatchAuthorized": sequence += [("Dispatch", 5)]
    if case.id in ("S3.lifecycle.destroy", "S3.all-copy-destroy-restore") or execution["outcome"] == "Destroyed": sequence += [("Destroy", 5)]
    if case.expected.get("originalEffects") == 1: sequence += [("Effect", 20)]
    if case.id.startswith("v23.") and "reserve." in case.id:
        if execution["outcome"] == "Blocked": sequence += [("Block", 5), ("Reserve", 6)]
        else: sequence += [("Block", 6)]
    if case.id.startswith("v23.second-compromise."):
        sequence += [("Activate", 5), ("Block", 6)] if execution["outcome"] == "BlockedAfterActivation" else [("Block", 5), ("ActivationBlocked", 6)]
    if case.id in ("v23.same-batch-reattest", "Recovery.activation.lost-result"):
        sequence = [e for e in sequence if e[0] not in ("Dispatch", "Reserve", "Consume")]
        kinds = ("Block", "Issue", "Dispatch", "Activate") + (("Reserve", "Consume") if execution["outcome"] == "Consumed" else ())
        sequence += list(zip(kinds, range(5, 5 + len(kinds))))
    if case.expected.get("signingAttempts") == 3: sequence += [("Sign", 5), ("NoIssue", 6), ("Sign", 7), ("NoIssue", 8), ("Sign", 9), ("Issue", 10)]
    if case.boundary != "none":
        trigger, effect = q.race_event_kinds(case.id, execution["outcome"])
        winner = "Effect" if case.boundary == "after" else "Trigger"
        if case.id.startswith("S2.expiry."): winner = "Effect" if case.boundary == "before" else "Trigger"
        trigger_pos, effect_pos = (5, 6) if winner == "Trigger" else (6, 5)
        # Existing actual reserve/block and activation records already carry the chosen order.
        for kind, pos in ((trigger, trigger_pos), (effect, effect_pos)):
            existing = next((p for k, p in sequence if k == kind), None)
            if existing is None: sequence.append((kind, pos))
            elif kind == trigger: trigger_pos = existing
            else: effect_pos = existing
        payload["linearization"] = {"boundary": case.boundary, "triggerPosition": trigger_pos,
                                    "effectPosition": effect_pos, "winner": "Trigger" if trigger_pos < effect_pos else "Effect"}
    # Stable ordering with unique positions; a normal effect receipt and clock event can occupy different ledger positions.
    sequence = sorted(sequence, key=lambda p: p[1])
    occupied = set()
    adjusted = []
    for kind, pos in sequence:
        while pos in occupied: pos += 1
        occupied.add(pos); adjusted.append((kind, pos))
    if case.boundary != "none":
        trigger, effect = q.race_event_kinds(case.id, execution["outcome"])
        payload["linearization"].update(triggerPosition=next(p for k,p in adjusted if k==trigger), effectPosition=next(p for k,p in adjusted if k==effect))
    recovery = {"OriginalPersisted", "Restart", "Restore", "Lookup"}
    receipts = []
    for kind, position in adjusted:
        occurred = persisted["timing"]["exclusiveExpiry"] if kind == "Expiry" else persisted["timing"]["checkedAt"] if kind in ("Verify", "Release", "DenyRelease", "ActorRevocation") else position
        if kind == "OriginalPersisted" and execution["outcome"] in ("Recorded", "DomainIdempotent"): occurred = persisted["firstSeenAt"]
        receipt = {"kind": kind, "position": position, "receiptId": persisted["receiptId"] if kind in recovery else q.digest({"event": kind, "case": case.id, "position": position}),
            "keyVersion": persisted["keyVersion"], "guardRevision": 1, "blockSetRevision": 3 if kind == "Block" else 2, "occurredAt": occurred}
        if case.id.startswith("v23.second-compromise.") and kind in ("Block", "Activate", "ActivationBlocked"): receipt["keyVersion"] = "v3"
        if case.expected.get("signingAttempts") == 3:
            if kind == "Sign":
                attempt = persisted["attempts"][sum(1 for r in receipts if r["kind"] == "Sign")]
                receipt.update(keyVersion=attempt["keyVersion"], guardRevision=attempt["intendedGuardRevision"])
            if kind == "Issue": receipt.update(keyVersion=persisted["keyVersion"], guardRevision=persisted["actualIssuedGuardRevision"])
            if kind == "NoIssue":
                attempt = persisted["attempts"][sum(1 for r in receipts if r["kind"] == "NoIssue")]
                receipt.update(receiptId=attempt["noIssueProofSha256"], keyVersion=attempt["keyVersion"], guardRevision=attempt["intendedGuardRevision"])
        if case.id in ("v23.same-batch-reattest", "Recovery.activation.lost-result"):
            if kind == "Block": receipt.update(keyVersion="v1", receiptId=persisted["attestationHistory"][0]["blockReceiptId"])
            if kind == "Dispatch": receipt.update(keyVersion="v3", receiptId=persisted["attestationHistory"][1]["dispatchReceiptId"])
        receipts.append(receipt)
    payload["retainedReceipts"] = receipts
    payload["trace"] = [r | {"requestId": request["requestId"], "tenantId": persisted["tenantId"], "batchId": persisted["batchId"],
        "originalReceiptId": persisted["receiptId"], "stateSha256": q.digest(persisted)} for r in receipts]
    payload["proofReferences"] = list(dict.fromkeys([persisted["receiptId"]] + [r["receiptId"] for r in receipts] + [q.digest({"snapshot": stage, "case": case.id}) for stage in q.STAGES]))
    payload["snapshots"] = [{"stage": stage, "originalRequestId": request["requestId"], "originalResultSha256": execution["resultSha256"],
        "stateSha256": q.digest(persisted), "authorityRevision": "r1", "state": copy.deepcopy(persisted)} for stage in q.STAGES]
    return payload


class Fixture:
    def __init__(self, directory):
        self.directory = Path(directory)
        self.root = self.directory / "platform"
        self.root.mkdir()
        self.agents = self.directory / "agents"
        self.agents.mkdir()
        subprocess.run(["git", "init", "-q", str(self.root)], check=True)
        (self.root / "tracked").write_text("fixture")
        subprocess.run(["git", "-C", str(self.root), "add", "tracked"], check=True)
        subprocess.run(["git", "-C", str(self.root), "-c", "user.name=Offline Test", "-c", "user.email=offline@example.invalid",
                        "commit", "-qm", "Offline test"], check=True)
        self.target = q.git(self.root, "rev-parse", "HEAD")
        self.config = {"schema": q.PROTOCOL, "runtime": {}, "qualification": {}}
        for name in q.REQUIRED:
            cursor = self.config
            parts = name.split(".")
            for part in parts[:-1]:
                cursor = cursor.setdefault(part, {})
            cursor[parts[-1]] = parts[-1] + "-ref"
        self.config["runtime"].update(host="192.168.1.30", tenantA="tenant-a", tenantB="tenant-b", decisionKeysOutsideCustody=True,
            overlappingExportDisposition="Open", profileNotBefore="2026-01-01T00:00:00Z", profileValidUntil="2099-01-01T00:00:00Z",
            timing={"L": 300, "S": 30, "O": 600, "H": 86400, "R": 604800})
        self.config["qualification"].update(daprEndpoint="http://127.0.0.1:3500", requestTimeoutSeconds=1)
        token = self.directory / "api-token"
        token.write_text("offline-test-only")
        self.config["qualification"]["daprApiCredentialReference"] = "file:" + str(token)
        self.private = {}
        self.binding = {"planSha256": plan_digest()}
        for role in ("executor", "observer", "enrollment"):
            key = ec.generate_private_key(ec.SECP256R1())
            self.private[role] = key
            public = key.public_key().public_bytes(serialization.Encoding.PEM, serialization.PublicFormat.SubjectPublicKeyInfo)
            anchor = self.directory / f"{role}.public.pem"
            anchor.write_bytes(public)
            self.config["qualification"][role].update(identity=role, appId=role, version="v1", publicAnchorFile=str(anchor))
            self.binding[f"{role}AnchorSha256"] = hashlib.sha256(public).hexdigest()
        self.binding["configurationSha256"] = q.digest(self.config)
        self.path = self.directory / "references.json"
        self.path.write_bytes(q.canonical(self.config))
        self.register = self.agents / "register.md"
        self.write_register()
        self.calls = []
        self.lose_result = None

    def write_register(self, binding=True):
        self.register.write_text(f'''### EXT-SECRETS-1 — Platform Secret Resolution
| `TargetVersionOrCommit` | `{self.target}` |
| `AcceptedStatus` | `Committed` |
| `TargetIntegrationDate` | `2026-10-08` |
| `Command` | Command: `test "$(git -C ../platform rev-parse HEAD)" = "{self.target}" && test -z "$(git -C ../platform status --porcelain)" && pwsh -NoProfile -File ../platform/eng/verify-ext-secrets-1.ps1 -Mode Live -EventStoreSourceRoot ../eventstore -ArtifactsDirectory /tmp/artifacts -EvidenceDirectory /tmp/evidence` |
''' + (f'| `RuntimeQualificationBinding` | `{json.dumps(self.binding)}` |\n' if binding else ""))

    def preflight(self):
        return q.preflight(self.root, self.path, self.register, self.agents, "../eventstore", "/tmp/artifacts", "/tmp/evidence")

    def sign(self, role, payload):
        payload = dict(payload, identity=role, version="v1")
        payload.setdefault("observedAt", int(time.time()))
        payload.setdefault("validUntil", int(time.time()) + 25)
        signature = self.private[role].sign(q.canonical(payload), ec.ECDSA(hashes.SHA256()))
        r, s = decode_dss_signature(signature)
        return {"payload": payload, "signature": base64.b64encode(r.to_bytes(32, "big") + s.to_bytes(32, "big")).decode()}

    def call(self, role, operation, request):
        self.calls.append((role, operation, request["requestId"]))
        if operation == "enroll":
            payload = {k: request[k] for k in ("protocol", "runId", "targetCommit", "isolatedTargetId", "planSha256", "requestId")}
            payload.update(targetClass="IsolatedQualification", productionMutationAllowed=False,
                executorIdentity="executor", executorVersion="v1", observerIdentity="observer", observerVersion="v1",
                qualifiedReferencesSha256=q.digest(self.config["runtime"]), completeCaseIds=[c.id for c in plan()],
                caseTargetIds={c.id: q.digest({"case-target": c.id, "run": request["runId"]}) for c in plan()},
                currentProfileSha256=q.digest(q.profile_identity(self.config)), currentProfileStatus="Current")
            payload["caseScopes"] = {c.id: enrolled_scope(c, payload["caseTargetIds"][c.id], self.config["runtime"]["issuer"]) for c in plan()}
            return self.sign(role, payload)
        case = next(c for c in plan() if c.id == request["caseId"])
        execution = {k: request[k] for k in ("protocol", "runId", "targetCommit", "isolatedTargetId", "planSha256", "requestId", "caseId", "caseTargetId")}
        outcome = next((v for v in case.outcomes if v in ("Delivered", "Consumed", "Completed")), case.outcomes[0]) if case.boundary == "after" else case.outcomes[0]
        execution.update(outcome=outcome, resultSha256=q.digest({"original": request["requestId"]}))
        if operation == "execute" and self.lose_result == case.id:
            raise TimeoutError("sensitive provider error")
        return self.sign(role, observation(request, execution, case) if role == "observer" else execution)


class QualificationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.fixture = Fixture(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def test_exact_accepted_preflight(self):
        self.assertEqual(self.fixture.target, self.fixture.preflight()[1])
        self.assertFalse(self.fixture.calls)

    def test_config_cannot_self_certify_authorities(self):
        self.fixture.write_register(binding=False)
        with self.assertRaises(q.Unavailable) as caught:
            self.fixture.preflight()
        self.assertIn("AuthoritativelyAcceptedRuntimeQualificationBinding", caught.exception.inputs)
        self.assertFalse(self.fixture.calls)

    def test_dirty_or_changed_target_stops_before_effect(self):
        (self.fixture.root / "tracked").write_text("dirty")
        with self.assertRaises(q.Unavailable) as caught:
            self.fixture.preflight()
        self.assertIn("CleanAcceptedTargetCheckout", caught.exception.inputs)
        self.fixture.target = "a" * 40
        self.fixture.write_register()
        with self.assertRaises(q.Unavailable) as caught:
            self.fixture.preflight()
        self.assertIn("ExactAcceptedTargetCheckout", caught.exception.inputs)
        self.assertFalse(self.fixture.calls)

    def test_missing_inputs_named_no_effect(self):
        with self.assertRaises(q.Unavailable) as caught:
            q.preflight(self.fixture.root, None, self.fixture.register, self.fixture.agents, "../eventstore", "/tmp/artifacts", "/tmp/evidence")
        self.assertIn("runtime.allCopyLifecycleAuthorityReference", caught.exception.inputs)
        self.assertIn("IndependentPersistedStateObserver", caught.exception.inputs)
        self.assertFalse(self.fixture.calls)

    def test_complete_executable_plan_is_only_offline_proof(self):
        result = q.execute(self.fixture.config, self.fixture.target, self.fixture.binding, self.fixture)
        self.assertEqual(len(plan()), result["caseCount"])
        self.assertFalse(result["dependencyAvailable"])
        self.assertEqual([c.id for c in plan()], [c["caseId"] for c in result["cases"]])

    def test_lost_result_exact_original_lookup_never_repeats_execute(self):
        self.fixture.lose_result = "Recovery.signing.lost-result"
        result = q.execute(self.fixture.config, self.fixture.target, self.fixture.binding, self.fixture)
        request = next(c["requestId"] for c in result["cases"] if c["caseId"] == self.fixture.lose_result)
        calls = [op for role, op, key in self.fixture.calls if key == request and role == "executor"]
        self.assertEqual(["execute", "lookup"], calls)

    def test_counterfeit_changed_stale_and_cross_authority_signatures(self):
        request = {"protocol": q.PROTOCOL, "runId": "r", "targetCommit": self.fixture.target,
                   "isolatedTargetId": "t", "planSha256": plan_digest(), "requestId": "a" * 64}
        signed = self.fixture.sign("observer", request)
        for alteration in ("tamper", "stale", "wrong-role"):
            with self.subTest(alteration=alteration):
                changed = copy.deepcopy(signed)
                if alteration == "tamper":
                    changed["payload"]["runId"] = "other"
                elif alteration == "stale":
                    changed = self.fixture.sign("observer", request | {"observedAt": 0, "validUntil": 1})
                else:
                    changed = self.fixture.sign("executor", request)
                with self.assertRaises(q.QualificationFailed):
                    q.verify_signed(changed, "observer", self.fixture.config, self.fixture.binding)

    def sample(self, name):
        case = next(c for c in plan() if c.id == name)
        request = {"protocol": q.PROTOCOL, "runId": "r", "targetCommit": self.fixture.target, "isolatedTargetId": "t",
                   "planSha256": plan_digest(), "requestId": "a" * 64, "caseId": case.id, "caseTargetId": "c" * 64}
        request["enrolledScope"] = enrolled_scope(case, request["caseTargetId"])
        execution = {"outcome": case.outcomes[0], "resultSha256": "b" * 64}
        payload = self.fixture.sign("observer", observation(request, execution, case))["payload"]
        return case, payload, execution, request

    def test_partial_evidence_missing_persisted_proof_and_mismatched_state_fail(self):
        for condition in ("missing-stage", "counterfeit-count", "changed-state", "request", "target", "secret-field"):
            case, payload, execution, request = self.sample("S3.principal-direct-delivery")
            if condition == "missing-stage": payload["snapshots"].pop()
            if condition == "counterfeit-count": payload["metrics"]["leakCount"] = 1
            if condition == "changed-state": payload["snapshots"][2]["state"]["receiptId"] = "c" * 64
            if condition == "request": payload["requestId"] = "c" * 64
            if condition == "target": payload["targetCommit"] = "c" * 40
            if condition == "secret-field": payload["privateKey"] = "sensitive-marker"
            with self.subTest(condition=condition), self.assertRaises(q.QualificationFailed):
                q.check_observation(case, payload, execution, request)

    def test_actual_delivery_expiry_and_restore_invariants(self):
        case, payload, execution, request = self.sample("S3.principal-direct-delivery")
        for snap in payload["snapshots"]:
            snap["state"]["deliveredAt"] = snap["state"]["exclusiveExpiry"]
            snap["stateSha256"] = q.digest(snap["state"])
        with self.assertRaises(q.QualificationFailed): q.check_observation(case, payload, execution, request)
        case, payload, execution, request = self.sample("S3.all-copy-destroy-restore")
        for snap in payload["snapshots"]:
            snap["state"]["erasedCopyIds"].pop()
            snap["stateSha256"] = q.digest(snap["state"])
        with self.assertRaises(q.QualificationFailed): q.check_observation(case, payload, execution, request)

    def test_two_stale_attempts_require_actual_stable_identity_and_no_issue_proofs(self):
        for change in ("batch", "ordinal", "no-issue", "actual-revision"):
            case, payload, execution, request = self.sample("v23.two-consecutive-stale-signing-attempts")
            for snap in payload["snapshots"]:
                a = snap["state"]["attempts"][1]
                if change == "batch": a["batchId"] = "other"
                if change == "ordinal": a["signingAttemptOrdinal"] = 1
                if change == "no-issue": a["noIssueProofSha256"] = None
                if change == "actual-revision": a["actualIssuedGuardRevision"] = 20
                snap["stateSha256"] = q.digest(snap["state"])
            with self.subTest(change=change), self.assertRaises(q.QualificationFailed):
                q.check_observation(case, payload, execution, request)

    def test_reservation_and_compromise_trace_cannot_contradict_state(self):
        case, payload, execution, request = self.sample("v23.key-compromise-reserve.before")
        payload["trace"] = [e for e in payload["trace"] if e["kind"] != "Block"]
        with self.assertRaises(q.QualificationFailed): q.check_observation(case, payload, execution, request)
        case, payload, execution, request = self.sample("v23.second-compromise.at-activation")
        payload["trace"] = [e for e in payload["trace"] if e["kind"] != "ActivationBlocked"]
        with self.assertRaises(q.QualificationFailed): q.check_observation(case, payload, execution, request)

    def test_full_plan_contains_contract_races_and_recovery(self):
        ids = {c.id for c in plan()}
        # Independent fixed review matrix: deletion from production cannot delete an expectation.
        expected = {line for line in (Path(__file__).parent / "contract-case-matrix.txt").read_text().splitlines() if line and not line.startswith("#")}
        self.assertEqual(expected, ids)
        self.assertEqual(len(ids), len(plan()))
        for prefix in ("v23.key-compromise-reserve", "v23.admission-integrity-block-reserve", "S3.delivery-expiry", "S3.delivery-actor-revocation"):
            for point in ("before", "at", "after"): self.assertIn(prefix + "." + point, ids)
        self.assertIn("v23.two-consecutive-stale-signing-attempts", ids)
        self.assertIn("S3.all-copy-destroy-restore", ids)
        self.assertIn("Leakage.all-surfaces", ids)
        for case in plan():
            self.assertNotIn("Pass", case.outcomes)

    def test_rerun_after_unknown_uses_exact_original_lookup(self):
        directory = Path(self.temp.name) / "journal"
        first = q.RunJournal(directory, self.fixture.config, self.fixture.target)
        first_run = first.run
        self.fixture.lose_result = plan()[0].id
        original_call = self.fixture.call
        def lost_lookup(role, operation, request):
            if operation == "lookup": raise TimeoutError("still unknown")
            return original_call(role, operation, request)
        self.fixture.call = lost_lookup
        with self.assertRaises(TimeoutError):
            q.execute(self.fixture.config, self.fixture.target, self.fixture.binding, self.fixture, journal=first)
        first.close()
        second = q.RunJournal(directory, self.fixture.config, self.fixture.target)
        self.assertEqual(first_run, second.run)
        self.fixture.call = original_call
        self.fixture.calls.clear()
        q.execute(self.fixture.config, self.fixture.target, self.fixture.binding, self.fixture, journal=second)
        self.assertEqual("lookup", next(op for role, op, _ in self.fixture.calls if role == "executor"))
        second.close()

    def test_transport_json_rejects_duplicate_and_nonfinite_fields(self):
        for payload in (b'{"outcome":"Denied","outcome":"Delivered"}', b'{"n":NaN}', b'{"n":Infinity}'):
            with self.assertRaises(ValueError): q.decode_json(payload)

    def test_partial_enrolled_plan_prevents_every_execute(self):
        original = self.fixture.call
        def partial(role, operation, request):
            envelope = original(role, operation, request)
            if role == "enrollment":
                payload = envelope["payload"]
                payload["completeCaseIds"].pop()
                return self.fixture.sign(role, payload)
            return envelope
        self.fixture.call = partial
        with self.assertRaises(q.QualificationFailed):
            q.execute(self.fixture.config, self.fixture.target, self.fixture.binding, self.fixture)
        self.assertFalse(any(role == "executor" for role, _, _ in self.fixture.calls))

    def test_arbitrary_live_command_cannot_authorize_effects(self):
        with self.assertRaises(q.Unavailable) as caught:
            q.preflight(self.fixture.root, self.fixture.path, self.fixture.register, self.fixture.root, "../eventstore", "/tmp/other", "/tmp/evidence")
        self.assertIn("ExactAcceptedOwnerCommand", caught.exception.inputs)
        self.assertFalse(self.fixture.calls)

    def test_distinct_pem_files_with_one_decoded_key_are_not_independent(self):
        original = Path(self.fixture.config["qualification"]["executor"]["publicAnchorFile"]).read_bytes()
        observer = Path(self.fixture.config["qualification"]["observer"]["publicAnchorFile"])
        observer.write_bytes(original.replace(b"\n", b"\r\n"))
        self.fixture.binding["observerAnchorSha256"] = hashlib.sha256(observer.read_bytes()).hexdigest()
        self.fixture.write_register()
        self.assertNotEqual(self.fixture.binding["executorAnchorSha256"], self.fixture.binding["observerAnchorSha256"])
        with self.assertRaises(q.Unavailable) as caught: self.fixture.preflight()
        self.assertIn("IndependentEnrolledQualificationAuthorities", caught.exception.inputs)
        self.assertFalse(self.fixture.calls)

    def test_every_new_effect_refreshes_same_enrollment_and_current_profile(self):
        original = self.fixture.call
        enroll_count = 0
        def expired(role, operation, request):
            nonlocal enroll_count
            response = original(role, operation, request)
            if role == "enrollment":
                enroll_count += 1
                if enroll_count == 2:
                    response = self.fixture.sign(role, response["payload"] | {"currentProfileStatus": "Expired"})
            return response
        self.fixture.call = expired
        with self.assertRaises(q.QualificationFailed): q.execute(self.fixture.config, self.fixture.target, self.fixture.binding, self.fixture)
        self.assertEqual(2, enroll_count)
        self.assertFalse(any(role == "executor" for role, _, _ in self.fixture.calls))

    def test_profile_window_expiring_after_admission_prevents_next_effect(self):
        original = self.fixture.call
        enroll_count = 0
        def expire(role, operation, request):
            nonlocal enroll_count
            if role == "enrollment":
                enroll_count += 1
                if enroll_count == 2:
                    self.fixture.config["runtime"]["profileValidUntil"] = "2020-01-01T00:00:00Z"
            return original(role, operation, request)
        self.fixture.call = expire
        with self.assertRaises(q.QualificationFailed): q.execute(self.fixture.config, self.fixture.target, self.fixture.binding, self.fixture)
        self.assertFalse(any(role == "executor" for role, _, _ in self.fixture.calls))

    def test_expired_signed_enrollment_is_rejected_before_effect(self):
        original = self.fixture.call
        count = 0
        def expired(role, operation, request):
            nonlocal count
            result = original(role, operation, request)
            if role == "enrollment":
                count += 1
                if count == 2: result = self.fixture.sign(role, result["payload"] | {"observedAt": int(time.time()) - 30, "validUntil": int(time.time()) - 1})
            return result
        self.fixture.call = expired
        with self.assertRaises(q.QualificationFailed): q.execute(self.fixture.config, self.fixture.target, self.fixture.binding, self.fixture)
        self.assertFalse(any(role == "executor" for role, _, _ in self.fixture.calls))

    def test_real_trickled_headers_and_body_obey_total_deadline(self):
        for mode in ("headers", "body"):
            class Handler(http.server.BaseHTTPRequestHandler):
                def log_message(self, *args): pass
                def do_POST(self):
                    self.rfile.read(int(self.headers["Content-Length"]))
                    try:
                        if mode == "headers":
                            data = b'HTTP/1.1 200 OK\r\nContent-Length: 1000\r\n\r\n'
                            for byte in data:
                                self.connection.sendall(bytes([byte])); time.sleep(.06)
                        else:
                            self.send_response(200); self.send_header("Content-Length", "1000"); self.end_headers()
                            for _ in range(100):
                                self.wfile.write(b" "); self.wfile.flush(); time.sleep(.06)
                    except (BrokenPipeError, ConnectionResetError, OSError): pass
            server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
            server.daemon_threads = True
            threading.Thread(target=server.serve_forever, daemon=True).start()
            transport = q.DaprTransport(self.fixture.config)
            transport.port = server.server_port
            started = time.monotonic()
            with self.subTest(mode=mode), self.assertRaises((TimeoutError, OSError)):
                transport.call("executor", "execute", {"requestId": "a" * 64})
            elapsed = time.monotonic() - started
            server.shutdown(); server.server_close()
            self.assertLess(elapsed, 1.35)

    def change_states(self, payload, change):
        for snapshot in payload["snapshots"]:
            change(snapshot["state"])
            snapshot["stateSha256"] = q.digest(snapshot["state"])
        for event in payload["trace"]:
            event["stateSha256"] = payload["snapshots"][0]["stateSha256"]

    def reconcile_trace(self, payload):
        payload["trace"].sort(key=lambda e: e["position"])
        fields = {"kind", "position", "receiptId", "keyVersion", "guardRevision", "blockSetRevision", "occurredAt"}
        payload["retainedReceipts"] = [{k: e[k] for k in fields} for e in payload["trace"]]

    def test_recovery_keeps_native_outcomes_and_native_proofs(self):
        expected = {"lifecycle": "Destroyed", "delivery": "Delivered", "signing": "Signed", "replay": "Recorded", "consume": "Consumed",
            "compromise-block": "ConsumptionBlocked", "activation": "Unconsumed", "guard-issue": "Issued",
            "dispatch": "DeletionBatchDispatchAuthorized", "completion": "CompletionSealed"}
        for operation, outcome in expected.items():
            case, payload, execution, request = self.sample(f"Recovery.{operation}.lost-result")
            self.assertEqual((outcome,), case.outcomes)
            self.assertEqual(1, payload["metrics"]["lookupOnly"])
            with self.subTest(operation=operation): q.check_observation(case, payload, execution, request)
        changes = (
            ("delivery-instant", "delivery", lambda s: s.update(deliveredAt=None)),
            ("delivery-expiry", "delivery", lambda s: s.update(exclusiveExpiry=None)),
            ("consumption-vector", "consume", lambda s: s.update(destroyedTargetIds=[])),
            ("consumption-issued-state", "consume", lambda s: s.update(actualIssuedGuardRevision=None)),
            ("destroyed-copies", "lifecycle", lambda s: s.update(erasedCopyIds=[])),
            ("signature-claims-issue", "signing", lambda s: s.update(actualIssuedGuardRevision=1)),
            ("signature-missing-batch", "signing", lambda s: s.update(batchId=None)),
            ("issue-missing-revision", "guard-issue", lambda s: s.update(actualIssuedGuardRevision=None)),
            ("dispatch-missing-batch", "dispatch", lambda s: s.update(manifestDigest=None)),
            ("completion-missing-consumption", "completion", lambda s: s.update(destroyedTargetIds=[])),
            ("activation-missing-predecessor", "activation", lambda s: s.update(attestationHistory=[])),
            ("block-destroys-targets", "compromise-block", lambda s: s.update(destroyedTargetIds=list(s["targetIds"]))),
        )
        for name, operation, change in changes:
            case, payload, execution, request = self.sample(f"Recovery.{operation}.lost-result")
            self.change_states(payload, change)
            with self.subTest(contradiction=name), self.assertRaises(q.QualificationFailed): q.check_observation(case, payload, execution, request)
        for operation, kind in (("delivery", "Release"), ("consume", "Consume"), ("consume", "Dispatch"), ("consume", "Reserve"),
                                ("lifecycle", "Destroy"), ("signing", "Sign"), ("guard-issue", "Issue"), ("dispatch", "Dispatch"), ("completion", "Complete"), ("activation", "Activate"), ("compromise-block", "Block")):
            case, payload, execution, request = self.sample(f"Recovery.{operation}.lost-result")
            payload["trace"] = [e for e in payload["trace"] if e["kind"] != kind]
            self.reconcile_trace(payload)
            with self.subTest(operation=operation, missing=kind), self.assertRaises(q.QualificationFailed): q.check_observation(case, payload, execution, request)
        case, payload, execution, request = self.sample("Recovery.guard-issue.lost-result")
        next(e for e in payload["trace"] if e["kind"] == "Issue")["guardRevision"] += 1
        self.reconcile_trace(payload)
        with self.assertRaises(q.QualificationFailed): q.check_observation(case, payload, execution, request)

    def test_exact_terminal_no_issue_receipts_precede_every_successor_sign(self):
        for change in ("foreign-key", "foreign-revision", "unretained-proof", "after-issue", "before-sign", "reused-proof"):
            case, payload, execution, request = self.sample("v23.two-consecutive-stale-signing-attempts")
            no_issues = [e for e in payload["trace"] if e["kind"] == "NoIssue"]
            if change == "foreign-key": no_issues[0]["keyVersion"] = "foreign"
            if change == "foreign-revision": no_issues[0]["guardRevision"] = 99
            if change == "unretained-proof": self.change_states(payload, lambda s: s["attempts"][0].update(noIssueProofSha256="f" * 64))
            if change == "after-issue":
                no_issues[0]["position"] = 11; no_issues[1]["position"] = 12
            if change == "before-sign": no_issues[0]["position"] = 4
            if change == "reused-proof":
                no_issues[1]["receiptId"] = no_issues[0]["receiptId"]
                self.change_states(payload, lambda s: s["attempts"][1].update(noIssueProofSha256=s["attempts"][0]["noIssueProofSha256"]))
            self.reconcile_trace(payload)
            with self.subTest(contradiction=change), self.assertRaises(q.QualificationFailed): q.check_observation(case, payload, execution, request)

    def test_publication_crossing_exclusive_authority_window_never_executes(self):
        for expiry in ("enrollment", "profile"):
            with self.subTest(expiry=expiry), tempfile.TemporaryDirectory() as directory:
                fixture = Fixture(directory)
                clock = [int(time.time())]
                if expiry == "profile":
                    from datetime import datetime, timezone
                    fixture.config["runtime"]["profileValidUntil"] = datetime.fromtimestamp(clock[0] + 20, timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
                journal_dir = Path(directory) / "journal"
                journal = q.RunJournal(journal_dir, fixture.config, fixture.target)
                begin = journal.begin
                def delayed_begin(request):
                    result = begin(request)
                    clock[0] += 40
                    return result
                with patch("qualify.time.time", side_effect=lambda: clock[0]), patch.object(journal, "begin", side_effect=delayed_begin):
                    with self.assertRaises(q.QualificationFailed): q.execute(fixture.config, fixture.target, fixture.binding, fixture, journal=journal)
                self.assertFalse(any(role == "executor" for role, _, _ in fixture.calls))
                originals = [p for p in journal.originals.glob("*.json") if p.name != "enrolled-case-targets.json"]
                self.assertEqual(1, len(originals))
                original_request = q.read_json(originals[0])
                self.assertTrue(journal.is_started(original_request))
                journal.close()
                if expiry == "enrollment":
                    reopened = q.RunJournal(journal_dir, fixture.config, fixture.target)
                    original_call = fixture.call
                    class ExactLookupObserved(Exception): pass
                    def resumed(role, operation, request):
                        if role == "executor":
                            self.assertEqual("lookup", operation)
                            self.assertEqual(original_request["requestId"], request["requestId"])
                            raise ExactLookupObserved()
                        return original_call(role, operation, request)
                    fixture.call = resumed
                    with patch("qualify.time.time", side_effect=lambda: clock[0]):
                        with self.assertRaises(ExactLookupObserved): q.execute(fixture.config, fixture.target, fixture.binding, fixture, journal=reopened)
                    reopened.close()

    def test_activation_count_is_distinct_from_retained_guard_credential(self):
        case, payload, execution, request = self.sample("v23.admission-block-never-reopens")
        self.change_states(payload, lambda s: s.update(activeCredentialIds=["a" * 64]))
        q.check_observation(case, payload, execution, request)
        event = dict(payload["trace"][0], kind="Activate", position=20)
        payload["trace"].append(event)
        self.reconcile_trace(payload)
        with self.assertRaises(q.QualificationFailed): q.check_observation(case, payload, execution, request)

    def test_overlap_checks_retained_original_key_and_separate_current_issuance_key(self):
        for point in ("before", "at", "after"):
            case, payload, execution, request = self.sample("S2.overlap-" + point)
            self.assertEqual(request["enrolledScope"]["initialKeyVersion"], payload["snapshots"][0]["state"]["keyVersion"])
            q.check_observation(case, payload, execution, request)
            self.change_states(payload, lambda s: s.update(keyVersion=s["rotation"]["currentKeyVersion"]))
            with self.subTest(point=point), self.assertRaises(q.QualificationFailed): q.check_observation(case, payload, execution, request)

    def verified_signed_observation(self, payload):
        return q.verify_signed(self.fixture.sign("observer", payload), "observer", self.fixture.config, self.fixture.binding)

    def test_signed_native_consumption_receipts_bind_original_key_and_revision(self):
        for operation in ("consume", "completion"):
            case, payload, execution, request = self.sample(f"Recovery.{operation}.lost-result")
            for kind, revision in (("Dispatch", 5), ("Reserve", 6), ("Consume", 7), ("Complete", 8)):
                for event in payload["trace"]:
                    if event["kind"] == kind: event["guardRevision"] = revision
            self.reconcile_trace(payload)
            q.check_observation(case, self.verified_signed_observation(payload), execution, request)
            for kind in ("Reserve", "Consume"):
                for contradiction in ("foreign-key", "lower-revision"):
                    changed = copy.deepcopy(payload)
                    event = next(e for e in changed["trace"] if e["kind"] == kind)
                    if contradiction == "foreign-key": event["keyVersion"] = "foreign-key"
                    else: event["guardRevision"] = changed["snapshots"][0]["state"]["actualIssuedGuardRevision"] - 1
                    self.reconcile_trace(changed)
                    signed = self.verified_signed_observation(changed)
                    with self.subTest(operation=operation, kind=kind, contradiction=contradiction), self.assertRaises(q.QualificationFailed):
                        q.check_observation(case, signed, execution, request)

    def test_signed_native_activation_and_block_receipts_bind_their_exact_key(self):
        case, payload, execution, request = self.sample("Recovery.activation.lost-result")
        for kind, revision in (("Issue", 1), ("Dispatch", 5), ("Activate", 6)):
            next(e for e in payload["trace"] if e["kind"] == kind)["guardRevision"] = revision
        self.reconcile_trace(payload)
        q.check_observation(case, self.verified_signed_observation(payload), execution, request)
        self.assertNotEqual(next(e for e in payload["trace"] if e["kind"] == "Block")["keyVersion"], payload["snapshots"][0]["state"]["keyVersion"])
        for kind in ("Issue", "Activate", "Dispatch"):
            for contradiction in ("foreign-key", "lower-revision"):
                changed = copy.deepcopy(payload)
                event = next(e for e in changed["trace"] if e["kind"] == kind)
                if contradiction == "foreign-key": event["keyVersion"] = "foreign-key"
                else: event["guardRevision"] = changed["snapshots"][0]["state"]["actualIssuedGuardRevision"] - 1
                self.reconcile_trace(changed)
                with self.subTest(kind=kind, contradiction=contradiction), self.assertRaises(q.QualificationFailed):
                    q.check_observation(case, self.verified_signed_observation(changed), execution, request)
        case, payload, execution, request = self.sample("Recovery.compromise-block.lost-result")
        self.change_states(payload, lambda state: state.update(actualIssuedGuardRevision=2))
        next(e for e in payload["trace"] if e["kind"] == "Block")["guardRevision"] = 4
        self.reconcile_trace(payload)
        q.check_observation(case, self.verified_signed_observation(payload), execution, request)
        for contradiction in ("foreign-key", "lower-revision"):
            changed = copy.deepcopy(payload)
            block = next(e for e in changed["trace"] if e["kind"] == "Block")
            if contradiction == "foreign-key": block["keyVersion"] = "foreign-key"
            else: block["guardRevision"] = 1
            self.reconcile_trace(changed)
            with self.subTest(kind="Block", contradiction=contradiction), self.assertRaises(q.QualificationFailed):
                q.check_observation(case, self.verified_signed_observation(changed), execution, request)

    def test_retained_key_verify_until_is_exclusive_inside_maximum_overlap(self):
        for point, delta in (("before", 1), ("at", 0), ("after", -1)):
            case, payload, execution, request = self.sample("S2.overlap-before")
            def window(state):
                state["timing"]["checkedAt"] = state["rotation"]["rotatedAt"] + 200000
                state["rotation"]["oldVerifyUntil"] = state["timing"]["checkedAt"] + delta
            self.change_states(payload, window)
            signed = self.verified_signed_observation(payload)
            if point == "before": q.check_observation(case, signed, execution, request)
            else:
                with self.subTest(point=point), self.assertRaises(q.QualificationFailed): q.check_observation(case, signed, execution, request)
        for point in ("at", "after"):
            case, payload, execution, request = self.sample("S2.overlap-" + point)
            self.assertEqual("Denied", execution["outcome"])
            q.check_observation(case, self.verified_signed_observation(payload), execution, request)

    def test_activation_issue_receipt_is_the_exact_immutable_successful_append(self):
        for case_id in ("Recovery.activation.lost-result", "v23.same-batch-reattest"):
            case, payload, execution, request = self.sample(case_id)
            issued = payload["snapshots"][0]["state"]["actualIssuedGuardRevision"]
            for kind, offset in (("Dispatch", 4), ("Activate", 5), ("Reserve", 6), ("Consume", 7)):
                for event in payload["trace"]:
                    if event["kind"] == kind: event["guardRevision"] = issued + offset
            self.reconcile_trace(payload)
            # Online authority can advance after the immutable successful Issue append.
            q.check_observation(case, self.verified_signed_observation(payload), execution, request)
            changed = copy.deepcopy(payload)
            next(event for event in changed["trace"] if event["kind"] == "Issue")["guardRevision"] = 12345
            self.reconcile_trace(changed)
            with self.subTest(case=case_id), self.assertRaises(q.QualificationFailed):
                q.check_observation(case, self.verified_signed_observation(changed), execution, request)

    def test_retained_pins_must_include_enrolled_protected_target(self):
        case, payload, execution, request = self.sample("S3.hold-pin")
        q.check_observation(case, payload, execution, request)
        self.change_states(payload, lambda s: s.update(pinnedTargetIds=["f" * 64]))
        with self.assertRaises(q.QualificationFailed): q.check_observation(case, payload, execution, request)

    def test_idempotent_and_recovered_replay_keep_exact_original_scope_and_retention(self):
        for case_id in ("S2.byte-identical-replay", "Recovery.replay.lost-result"):
            case, payload, execution, request = self.sample(case_id)
            q.check_observation(case, payload, execution, request)
            for mutation in ("null", "issuer", "nonce", "tenant", "namespace", "first-seen", "retention"):
                changed = copy.deepcopy(payload)
                def alter(state):
                    if mutation == "null":
                        state.update(replayNamespace=None, replayTenant=None, replayIssuer=None, nonceDigest=None, firstSeenAt=None, retainUntil=None)
                    if mutation == "issuer": state["replayIssuer"] = "foreign"
                    if mutation == "nonce": state["nonceDigest"] = "f" * 64
                    if mutation == "tenant": state["replayTenant"] = "tenant-b"
                    if mutation == "namespace": state["replayNamespace"] = "tenant-a/trusted-envelope-replay"
                    if mutation == "first-seen":
                        state["firstSeenAt"] += 1000; state["retainUntil"] += 1000
                    if mutation == "retention": state["retainUntil"] += 1000
                self.change_states(changed, alter)
                with self.subTest(case=case_id, mutation=mutation), self.assertRaises(q.QualificationFailed): q.check_observation(case, changed, execution, request)

    def test_nonexpiring_capability_proofs_reject_delivery_expiry_or_release_fields(self):
        cases = ("S4.capability-canonical-14-fields", "S4.routine-rotation-retention", "v23.delayed-nonexpiring-restore",
                 "Recovery.signing.lost-result", "Recovery.guard-issue.lost-result", "Recovery.dispatch.lost-result", "Recovery.consume.lost-result")
        for case_id in cases:
            case, payload, execution, request = self.sample(case_id)
            q.check_observation(case, payload, execution, request)
            for field in ("state-expiry", "timing-expiry", "timing-release"):
                changed = copy.deepcopy(payload)
                def alter(state):
                    if field == "state-expiry": state["exclusiveExpiry"] = request["enrolledScope"]["exclusiveExpiry"]
                    if field == "timing-expiry": state["timing"]["exclusiveExpiry"] = request["enrolledScope"]["exclusiveExpiry"]
                    if field == "timing-release": state["timing"]["releasedAt"] = 900
                self.change_states(changed, alter)
                with self.subTest(case=case_id, field=field), self.assertRaises(q.QualificationFailed): q.check_observation(case, changed, execution, request)

    def test_each_demonstrated_persisted_contradiction_is_rejected(self):
        mutations = (
            ("empty-consumed-targets", "v23.same-batch-reattest", lambda s: s.update(targetIds=[], destroyedTargetIds=[])),
            ("destroyed-after-winning-block", "v23.key-compromise-reserve.before", lambda s: s.update(destroyedTargetIds=list(s["targetIds"]))),
            ("one-active-metric-empty-vector", "v23.same-batch-reattest", lambda s: s.update(activeCredentialIds=[])),
            ("zero-active-metric-one-vector", "v23.second-compromise.before-replacement", lambda s: s.update(activeCredentialIds=["a" * 64])),
            ("foreign-replay-tenant", "S2.replay-exact-stream-acl", lambda s: s.update(replayTenant="tenant-b")),
            ("foreign-state-tenant", "S2.replay-exact-stream-acl", lambda s: s.update(tenantId="tenant-b")),
            ("foreign-replay-issuer", "S2.replay-exact-stream-acl", lambda s: s.update(replayIssuer="foreign-issuer")),
            ("foreign-replay-nonce", "S2.replay-exact-stream-acl", lambda s: s.update(nonceDigest="f" * 64)),
            ("missing-seal", "v23.same-batch-reattest", lambda s: s.update(sealId=None)),
            ("missing-batch", "v23.same-batch-reattest", lambda s: s.update(batchId=None)),
            ("missing-manifest", "v23.same-batch-reattest", lambda s: s.update(manifestDigest=None)),
            ("missing-attestation-ordinal", "v23.same-batch-reattest", lambda s: s.update(attestationOrdinal=None)),
            ("missing-predecessor", "v23.same-batch-reattest", lambda s: s.update(attestationHistory=[])),
            ("not-next-attestation", "v23.same-batch-reattest", lambda s: s["attestationHistory"][1].update(ordinal=3)),
            ("final-key-disconnected", "v23.two-consecutive-stale-signing-attempts", lambda s: s.update(keyVersion="unrelated")),
            ("missing-rotation", "v23.stale-attempt-with-rotation", lambda s: s.update(rotation=None)),
            ("no-actual-rotation", "v23.stale-attempt-with-rotation", lambda s: s["rotation"].update(currentKeyVersion="v1")),
            ("missing-expiry-instant", "S2.expiry.before", lambda s: s["timing"].update(checkedAt=None)),
            ("wrong-expiry-side", "S2.expiry.before", lambda s: s["timing"].update(checkedAt=s["exclusiveExpiry"])),
            ("denial-delivered-state", "S3.delivery-substitution.tenant", lambda s: s.update(status="Delivered")),
            ("denial-delivered-instant", "S3.delivery-substitution.tenant", lambda s: s.update(deliveredAt=900)),
            ("empty-lifecycle-destruction", "S3.lifecycle.destroy", lambda s: s.update(targetIds=[], destroyedTargetIds=[])),
            ("foreign-enrolled-target", "v23.same-batch-reattest", lambda s: s.update(targetIds=["f" * 64], destroyedTargetIds=["f" * 64])),
        )
        for name, case_id, mutation in mutations:
            case, payload, execution, request = self.sample(case_id)
            self.change_states(payload, mutation)
            with self.subTest(contradiction=name), self.assertRaises(q.QualificationFailed):
                q.check_observation(case, payload, execution, request)

    def test_each_demonstrated_trace_contradiction_is_rejected(self):
        mutations = (
            ("missing-consume", "v23.same-batch-reattest", "Consume"),
            ("missing-hold", "v23.hold-dispatch.before", "Hold"),
            ("missing-dispatch", "v23.hold-dispatch.before", "Dispatch"),
        )
        for name, case_id, missing in mutations:
            case, payload, execution, request = self.sample(case_id)
            payload["trace"] = [e for e in payload["trace"] if e["kind"] != missing]
            # Remove the corresponding retained record too; typed required event is still mandatory.
            payload["retainedReceipts"] = [r for r in payload["retainedReceipts"] if r["kind"] != missing]
            with self.subTest(contradiction=name), self.assertRaises(q.QualificationFailed): q.check_observation(case, payload, execution, request)
        for name in ("fabricated-race-positions", "unrelated-event-receipt", "unrelated-event-state", "unrelated-original-receipt", "changed-original-result"):
            case, payload, execution, request = self.sample("v23.hold-dispatch.before")
            if name == "fabricated-race-positions": payload["linearization"].update(triggerPosition=43, effectPosition=44)
            if name == "unrelated-event-receipt": payload["trace"][0]["receiptId"] = "f" * 64
            if name == "unrelated-event-state": payload["trace"][0]["stateSha256"] = "f" * 64
            if name == "unrelated-original-receipt": payload["trace"][0]["originalReceiptId"] = "f" * 64
            if name == "changed-original-result": payload["snapshots"][0]["originalResultSha256"] = "f" * 64
            with self.subTest(contradiction=name), self.assertRaises(q.QualificationFailed): q.check_observation(case, payload, execution, request)

    def test_interrupted_journal_serialization_and_publication_leave_no_final_record(self):
        path = Path(self.temp.name) / "original.json"
        for phase in ("serialize", "publish"):
            target = "qualify.canonical" if phase == "serialize" else "qualify.os.link"
            with patch(target, side_effect=OSError("interrupted")):
                with self.assertRaises(OSError): q.RunJournal._publish(path, {"original": 1})
            self.assertFalse(path.exists())
            self.assertFalse(list(path.parent.glob(".pending-*")))
        q.RunJournal._publish(path, {"original": 1})
        self.assertEqual({"original": 1}, q.read_json(path))
        with self.assertRaises(FileExistsError): q.RunJournal._publish(path, {"original": 2})
        self.assertEqual({"original": 1}, q.read_json(path))

    def test_signed_conflicting_original_lookup_is_rejected_after_journal_reopen(self):
        directory = Path(self.temp.name) / "journal"
        journal = q.RunJournal(directory, self.fixture.config, self.fixture.target)
        case, _, execution, request = self.sample("S3.principal-direct-delivery")
        response = {k: request[k] for k in ("protocol", "runId", "targetCommit", "isolatedTargetId", "planSha256", "requestId", "caseId", "caseTargetId")} | execution
        original = q.verify_signed(self.fixture.sign("executor", response), "executor", self.fixture.config, self.fixture.binding)
        journal.begin(request); journal.retain_outcome(request, original); journal.close()
        reopened = q.RunJournal(directory, self.fixture.config, self.fixture.target)
        changed = q.verify_signed(self.fixture.sign("executor", response | {"resultSha256": "f" * 64}), "executor", self.fixture.config, self.fixture.binding)
        with self.assertRaises(q.QualificationFailed): reopened.retain_outcome(request, changed)
        reopened.close()

    def test_changed_enrollment_mapping_is_rejected_after_journal_reopen(self):
        directory = Path(self.temp.name) / "journal"
        journal = q.RunJournal(directory, self.fixture.config, self.fixture.target)
        installed = {"caseTargetIds": {"case": "a" * 64}, "caseScopes": {"case": {"tenantId": "tenant-a"}},
                     "qualifiedReferencesSha256": "b" * 64, "currentProfileSha256": "c" * 64}
        journal.bind_enrollment(installed); journal.close()
        reopened = q.RunJournal(directory, self.fixture.config, self.fixture.target)
        with self.assertRaises(q.QualificationFailed): reopened.bind_enrollment(installed | {"caseTargetIds": {"case": "f" * 64}})
        reopened.close()

    def test_retained_process_lock_is_restrictive(self):
        directory = Path(self.temp.name) / "journal"
        original = q.RunJournal(directory, self.fixture.config, self.fixture.target)
        with self.assertRaises(q.Unavailable) as caught:
            q.RunJournal(directory, self.fixture.config, self.fixture.target)
        self.assertIn("ExclusiveOriginalRunRecoveryLock", caught.exception.inputs)
        self.assertTrue(original.lock.is_dir())
        original.close()


if __name__ == "__main__":
    unittest.main()
