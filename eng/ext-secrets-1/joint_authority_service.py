"""Independent OQ authority receiving implementation. Never initializes production genesis.

Serve behind an authenticated Dapr app channel using separately enrolled workload
and public owner trust. SQLite resides on independently administered durable storage
outside application restore. Installed storage/replication proof remains mandatory.
"""
import json
import sqlite3
import contextvars

# A private request carries its workload identity into the same durable transaction
# that admits work. No SQL lock is retained while a native effect or process runs.
_workload_admission = contextvars.ContextVar("cohort_workload_admission", default=None)

def _authorize_admission(database, path):
    admission = _workload_admission.get()
    if admission is None: return  # Direct calls are isolated/internal composition, never HTTP authentication.
    import time
    if admission[0] != str(path): raise Blocked("SameDurableWorkloadAdmissionStore")
    row = database.execute("SELECT methods_json,valid_until,revoked FROM workload_grants WHERE bearer_sha256=?", (admission[1],)).fetchone()
    if row is None or row[2] or row[1] <= int(time.time()) or admission[2] not in json.loads(row[0]):
        raise Blocked("CurrentExactCohortWorkloadScope")

from contextlib import contextmanager, closing
from joint_live_phase_runner import PROTOCOL, PHASES, partitions, canonical, digest, Blocked


class CohortAuthorityStore:
    def __init__(self, path, verify_owner):
        self.path, self.verify_owner = str(path), verify_owner
        # mode=rw prohibits creating a database or enrolling acceptance during startup.
        with self.connect() as database:
            for name in ("accepted_sets", "current_set", "attempts", "originals", "results"):
                database.execute("SELECT * FROM " + name + " LIMIT 0")

    @contextmanager
    def connect(self):
        database = sqlite3.connect("file:" + self.path + "?mode=rw", uri=True, timeout=10)
        database.execute("PRAGMA synchronous=FULL")
        database.execute("PRAGMA foreign_keys=ON")
        try:
            yield database
            database.commit()
        except BaseException:
            database.rollback()
            raise
        finally:
            database.close()

    def _current(self, database, binding):
        key = digest(binding)
        row = database.execute("SELECT binding_json FROM accepted_sets WHERE binding_digest=? AND accepted=1", (key,)).fetchone()
        current = database.execute("SELECT binding_digest FROM current_set WHERE slot=1", ()).fetchone()
        return row is not None and bytes(row[0]) == canonical(binding) and current is not None and current[0] == key

    def dispatch(self, method, value):
        with self.connect() as database:
            database.execute("BEGIN IMMEDIATE" if method in ("reserve", "retain", "publish") else "BEGIN")
            _authorize_admission(database, self.path)
            if method == "qualification-attempt":
                key = value.get("bindingDigest")
                row = database.execute("SELECT binding_json FROM accepted_sets WHERE binding_digest=?", (key,)).fetchone()
                binding = json.loads(row[0]) if row is not None else {}
                attempt = database.execute("SELECT attempt_id FROM attempts WHERE binding_digest=?", (key,)).fetchone()
                exact = (row is not None and self._current(database, binding) and attempt is not None and attempt[0] == value.get("attemptId")
                    and binding.get("platformCommit") == value.get("targetCommit"))
                import qualify as q
                exact = exact and value.get("planSha256") == q.plan_digest()
                case_id = value.get("caseId")
                if case_id is not None:
                    phases = [phase for phase, cases in partitions().items() if "secrets:" + str(case_id) in cases]
                    phase = phases[0] if len(phases) == 1 else -1
                    original = database.execute("SELECT request_json FROM originals WHERE attempt_id=? AND phase=?", (value.get("attemptId"), phase)).fetchone()
                    exact = exact and original is not None and "secrets:" + str(case_id) in json.loads(original[0]).get("cases", [])
                return {"exact": bool(exact), "binding": value}
            if method in ("preflight", "current", "reserve", "completion", "lookup-attempt"):
                binding = value["binding"]
                if not self._current(database, binding):
                    if method == "preflight":
                        return {"missingInputs": ["IndependentCurrentAcceptedInstalledTargetSet"]}
                    if method == "current":
                        return {"current": False}
                    raise Blocked("IndependentCurrentAcceptedInstalledTargetSet")
                key = digest(binding)
                if method == "preflight":
                    return {"missingInputs": []}
                if method == "current":
                    return {"current": True}
                if method == "lookup-attempt":
                    attempt = database.execute("SELECT attempt_id FROM attempts WHERE binding_digest=?", (key,)).fetchone()
                    if attempt is None: return {"bindingDigest":key,"state":"Absent","effectState":"NotDispatched"}
                    original = database.execute("SELECT request_id FROM originals WHERE attempt_id=? ORDER BY phase DESC LIMIT 1", (attempt[0],)).fetchone()
                    return {"bindingDigest":key,"state":"Retained","attemptId":attempt[0],"effectState":"Unknown" if original else "NotDispatched",
                            "phaseRequestId":original[0] if original else None}
                if method == "reserve":
                    import time
                    ready = value.get("ownerReadiness", [])
                    if not isinstance(ready, list) or len(ready) != 4:
                        raise Blocked("CurrentInstalledOwnerReadinessBeforeReservation")
                    now = int(time.time())
                    for phase, receipt in enumerate(ready):
                        payload = receipt.get("payload", {}) if isinstance(receipt, dict) else {}
                        if (not self.verify_owner(phase, receipt) or payload.get("bindingDigest") != key or payload.get("phase") != phase
                                or payload.get("cases") != partitions()[phase]
                                or payload.get("profileSha256") != binding.get("phaseExecutionProfileSha256", {}).get(str(phase))
                                or not now - 30 <= payload.get("observedAt", 0) <= now < payload.get("validUntil", 0) <= payload["observedAt"] + 30):
                            raise Blocked("CurrentInstalledOwnerReadinessBeforeReservation")
                    row = database.execute("SELECT attempt_id FROM attempts WHERE binding_digest=?", (key,)).fetchone()
                    if row is None:
                        database.execute("INSERT INTO attempts(binding_digest,attempt_id) VALUES(?,?)", (key, value["attemptId"]))
                        attempt = value["attemptId"]
                    else:
                        attempt = row[0]
                    return {"bindingDigest": key, "attemptId": attempt}
                if method == "completion":
                    candidate = value["result"]
                    attempt = database.execute("SELECT attempt_id FROM attempts WHERE binding_digest=?", (key,)).fetchone()
                    if attempt is None or candidate.get("attemptId") != attempt[0] or candidate.get("bindingDigest") != key:
                        return {"exact": False}
                    previous = key
                    for phase in range(4):
                        rows = database.execute("SELECT r.result_json FROM originals o JOIN results r ON r.request_id=o.request_id WHERE o.attempt_id=? AND o.phase=?", (attempt[0], phase)).fetchall()
                        if len(rows) != 1:
                            return {"exact": False}
                        result = json.loads(rows[0][0]); payload = result["payload"]
                        if payload.get("state") != "Passed" or payload.get("previousSha256") != previous or payload.get("passedCases") != partitions()[phase]:
                            return {"exact": False}
                        previous = digest(result)
                    return {"exact": candidate.get("resultSha256") == previous and candidate.get("cohortPassed") is True}
            request = value["request"]
            key, phase, attempt = request["bindingDigest"], request["phase"], request["attemptId"]
            binding_row = database.execute("SELECT binding_json FROM accepted_sets WHERE binding_digest=?", (key,)).fetchone()
            if binding_row is None or not self._current(database, json.loads(binding_row[0])) or type(phase) is not int or phase not in range(4):
                raise Blocked("ExactCurrentPhaseBinding")
            reserved = database.execute("SELECT attempt_id FROM attempts WHERE binding_digest=?", (key,)).fetchone()
            if reserved is None or reserved[0] != attempt or request.get("cases") != partitions()[phase] or request.get("owner") != PHASES[phase] or request.get("protocol") != PROTOCOL:
                raise Blocked("ExactReservedPhasePartition")
            previous = key
            if phase:
                rows = database.execute("SELECT r.result_json FROM originals o JOIN results r ON r.request_id=o.request_id WHERE o.attempt_id=? AND o.phase=?", (attempt, phase - 1)).fetchall()
                if len(rows) != 1 or json.loads(rows[0][0])["payload"].get("state") != "Passed":
                    raise Blocked("FixedCompletePhaseOrdering")
                previous = digest(json.loads(rows[0][0]))
            if request.get("previousSha256") != previous or request.get("requestId") != digest([attempt, key, phase, previous]):
                raise Blocked("ExactPhaseChainIdentity")
            original = database.execute("SELECT request_json FROM originals WHERE request_id=?", (request["requestId"],)).fetchone()
            if method == "phase-admission":
                return {"exact": original is not None and bytes(original[0]) == canonical(request)}
            if method == "retain":
                if original is not None:
                    if bytes(original[0]) != canonical(request):
                        raise Blocked("ChangedOriginalPhase")
                    return {"first": False}
                database.execute("INSERT INTO originals(request_id,attempt_id,phase,request_json) VALUES(?,?,?,?)", (request["requestId"], attempt, phase, canonical(request)))
                return {"first": True}
            if method == "publish":
                result = value["result"]
                if original is None or bytes(original[0]) != canonical(request) or not self.verify_owner(phase, result):
                    raise Blocked("SignedExactPhaseOriginal")
                payload = result.get("payload", {})
                if any(payload.get(name) != request[name] for name in ("requestId", "attemptId", "bindingDigest", "phase", "previousSha256")):
                    raise Blocked("ChangedPhaseOutcomeIdentity")
                prior = database.execute("SELECT result_json FROM results WHERE request_id=?", (request["requestId"],)).fetchone()
                if prior is not None:
                    return {"exact": bytes(prior[0]) == canonical(result)}
                database.execute("INSERT INTO results(request_id,result_json) VALUES(?,?)", (request["requestId"], canonical(result)))
                return {"exact": True}
            raise Blocked("InstalledAuthorityMethod")

class RetainedPhaseOwner:
    """Receiving executor publication: ambiguous subprocess effects never repeat execute after restart."""
    def __init__(self, path, phase, binding, executor, sign, authority):
        self.path, self.phase, self.binding = str(path), phase, binding
        self.executor, self.sign, self.authority = executor, sign, authority

    @contextmanager
    def _connect(self):
        database = sqlite3.connect("file:" + self.path + "?mode=rw", uri=True, timeout=10)
        database.execute("PRAGMA synchronous=FULL")
        try:
            yield database
            database.commit()
        except BaseException:
            database.rollback()
            raise
        finally:
            database.close()

    def dispatch(self, method, request):
        absence = None
        if method == "execute-if-absent":
            absence, request = request.get("absence"), request.get("request", {})
        if method == "preflight":
            if request.get("binding") != self.binding or request.get("phase") != self.phase or request.get("cases") != partitions()[self.phase]:
                raise Blocked("ExactInstalledPreflightPartition")
            missing = self.executor.preflight()
            try:
                with self._connect() as database:
                    database.execute("SELECT request_id,request_json,result_json FROM phase_originals LIMIT 0")
                    database.execute("SELECT request_id,nonce,absence_json FROM phase_absences LIMIT 0")
            except sqlite3.Error:
                missing.append("IndependentPhaseOriginalAndAbsenceStorageSchema")
            if not self.authority.call("current", {"binding": self.binding}).get("current"):
                missing.append("CurrentAcceptedPhaseSet")
            import time
            now = int(time.time())
            return {"missingInputs": missing, "readiness": None if missing else self.sign({"bindingDigest": digest(self.binding), "phase": self.phase,
                "cases": partitions()[self.phase], "profileSha256": self.binding["phaseExecutionProfileSha256"][str(self.phase)], "observedAt": now, "validUntil": now + 30})}
        if (request.get("bindingDigest") != digest(self.binding) or request.get("phase") != self.phase
                or request.get("cases") != partitions()[self.phase] or method not in ("execute", "lookup", "execute-if-absent")):
            raise Blocked("ExactInstalledPhaseScope")
        if self.authority.call("phase-admission", {"request": request, "recovery": method == "lookup"}).get("exact") is not True:
            raise Blocked("CurrentRetainedAuthorityAdmission")
        with self._connect() as db:
            db.execute("BEGIN IMMEDIATE")
            _authorize_admission(db, self.path)
            original = db.execute("SELECT request_json,result_json FROM phase_originals WHERE request_id=?", (request["requestId"],)).fetchone()
            if original is not None and bytes(original[0]) != canonical(request):
                raise Blocked("ChangedRetainedPhaseOriginal")
            if original is not None and original[1] is not None:
                return json.loads(original[1])
            first = original is None
            if first:
                import time
                import uuid
                if method == "lookup":
                    now = int(time.time())
                    prior = db.execute("SELECT absence_json FROM phase_absences WHERE request_id=?", (request["requestId"],)).fetchone()
                    if prior is not None:
                        old = json.loads(prior[0]); value = old.get("payload", {})
                        if value.get("requestSha256") == digest(request) and now < value.get("validUntil", 0) <= value.get("observedAt", 0) + 30:
                            return old
                    db.execute("DELETE FROM phase_absences WHERE request_id=?", (request["requestId"],))
                    proof = self.sign({"state": "OwnerAbsent", "requestId": request["requestId"], "requestSha256": digest(request),
                        "bindingDigest": digest(self.binding), "attemptId": request["attemptId"], "phase": self.phase,
                        "nonce": str(uuid.uuid4()), "observedAt": now, "validUntil": now + 30})
                    db.execute("INSERT INTO phase_absences VALUES(?,?,?)", (request["requestId"], proof["payload"]["nonce"], canonical(proof)))
                    return proof
                if method == "execute-if-absent":
                    proof = absence.get("payload", {}) if isinstance(absence, dict) else {}
                    retained = db.execute("SELECT absence_json FROM phase_absences WHERE request_id=? AND nonce=?",
                        (request["requestId"], proof.get("nonce"))).fetchone()
                    now = int(time.time())
                    if (retained is None or bytes(retained[0]) != canonical(absence) or proof.get("state") != "OwnerAbsent"
                            or proof.get("requestSha256") != digest(request) or proof.get("attemptId") != request["attemptId"]
                            or proof.get("bindingDigest") != digest(self.binding) or proof.get("phase") != self.phase
                            or not now - 30 <= proof.get("observedAt", 0) <= now < proof.get("validUntil", 0) <= proof["observedAt"] + 30):
                        raise Blocked("ExactCurrentOwnerAbsenceRequired")
                db.execute("DELETE FROM phase_absences WHERE request_id=?", (request["requestId"],))
                db.execute("INSERT INTO phase_originals VALUES(?,?,NULL)", (request["requestId"], canonical(request)))
        # The original is durable before dispatch. Unknown restarts only ask the executor for exact lookup.
        observed = self.executor.execute(request) if first else self.executor.lookup(request)
        if observed is None:
            return None
        state, passed, evidence = observed
        if state == "Passed" and passed != request["cases"]:
            raise Blocked("CompleteOwnerCasePartition")
        result = self.sign({**{name: request[name] for name in ("attemptId", "bindingDigest", "phase", "previousSha256", "requestId")},
                            "state": state, "passedCases": passed, "ownerEvidenceSha256": digest(evidence)})
        with self._connect() as db:
            db.execute("BEGIN IMMEDIATE")
            _authorize_admission(db, self.path)
            prior = db.execute("SELECT result_json FROM phase_originals WHERE request_id=?", (request["requestId"],)).fetchone()[0]
            if prior is not None:
                return json.loads(prior)
            db.execute("UPDATE phase_originals SET result_json=? WHERE request_id=? AND result_json IS NULL", (canonical(result), request["requestId"]))
        return result


class InstalledPhaseExecutor:
    """Real installed command execution plus independent persisted proof. Exit status alone never establishes a phase pass.

    Command argv, immutable executable hashes and independent observer public key are
    pinned by the accepted installed binding. Unknown command results use observer
    lookup and never launch a second process. No Agents consumer command is permitted.
    """
    def __init__(self, descriptor, observer, binding):
        self.descriptor, self.observer, self.binding = descriptor, observer, binding
        profiles = binding.get("phaseExecutionProfileSha256", {})
        if profiles.get(str(descriptor.get("phase"))) != digest({name: value for name, value in descriptor.items() if name != "bindingDigest"}):
            raise Blocked("ExactAcceptedPhaseExecutionProfile")

    def preflight(self):
        from case_driver_inventory import blockers
        import hashlib
        from pathlib import Path
        missing = []
        command=self.descriptor.get("command",[])
        if not isinstance(command,list) or command.count("--phase-command")!=1 or self.descriptor.get("recoveryCommand")!=["--phase-finalize" if arg=="--phase-command" else arg for arg in command]:
            missing.append("PinnedMetadataOnlyPhaseFinalizer")
        missing += blockers([name.removeprefix("secrets:") for name in self.descriptor.get("cases",[]) if name.startswith("secrets:")])
        if self.binding.get("phaseExecutionProfileSha256",{}).get(str(self.descriptor.get("phase"))) != digest({name:value for name,value in self.descriptor.items() if name!="bindingDigest"}):
            missing.append("ExactAcceptedPhaseExecutionProfile")
        from execution_profile import validate_execution_profile
        missing.extend(validate_execution_profile(self.descriptor))
        report = self.observer.call("preflight-phase", {"binding": self.binding, "phase": self.descriptor["phase"], "cases": partitions()[self.descriptor["phase"]]})
        missing.extend(report.get("missingInputs", ["IndependentCurrentPhaseObservationInputs"]))
        return missing

    def _observe(self, request):
        observed = self.observer.call("lookup-phase-original", {"request": request, "binding": self.binding})
        if observed is None or observed.get("state") == "Unknown":
            return None
        if observed.get("requestSha256") != digest(request) or observed.get("bindingDigest") != digest(self.binding):
            raise Blocked("ExactIndependentPersistedPhaseObservation")
        passed = observed.get("passedCases", [])
        if observed.get("state") == "Passed" and passed != request["cases"]:
            raise Blocked("IndependentlyCompletePhaseEvidence")
        return observed["state"], passed, observed

    def execute(self, request):
        import subprocess
        import hashlib
        from pathlib import Path
        command = self.descriptor["command"]
        from execution_profile import validate_execution_profile
        missing = validate_execution_profile(self.descriptor)
        if self.binding.get("phaseExecutionProfileSha256",{}).get(str(self.descriptor.get("phase"))) != digest({name:value for name,value in self.descriptor.items() if name!="bindingDigest"}):
            missing.append("ExactAcceptedPhaseExecutionProfile")
        if missing: raise Blocked(*missing)
        if self.descriptor.get("bindingDigest") != digest(self.binding) or self.descriptor.get("cases") != request["cases"]:
            raise Blocked("AcceptedOwnerCommandPartition")
        # Exact enrolled argv; no shell interpolation, environment inheritance or generic dispatch callback.
        with subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                              cwd=self.descriptor["workingDirectory"], env=self.descriptor["environment"]) as process:
            try:
                process.communicate(canonical(request), timeout=self.descriptor["completionTimeoutSeconds"])
            except subprocess.TimeoutExpired:
                # Stopping the launcher is not proof of effect cancellation; original remains lookup-only.
                process.kill(); process.wait()
                return None
        return self._observe(request)

    def lookup(self, request):
        observed = self._observe(request)
        if observed is not None: return observed
        import subprocess
        import hashlib
        from pathlib import Path
        command = self.descriptor.get("recoveryCommand")
        normal = self.descriptor["command"]
        expected = ["--phase-finalize" if arg == "--phase-command" else arg for arg in normal]
        if normal.count("--phase-command") != 1 or command != expected:
            raise Blocked("PinnedMetadataOnlyPhaseFinalizer")
        from execution_profile import validate_execution_profile
        missing = validate_execution_profile(self.descriptor)
        if self.binding.get("phaseExecutionProfileSha256",{}).get(str(self.descriptor.get("phase"))) != digest({name:value for name,value in self.descriptor.items() if name!="bindingDigest"}):
            missing.append("ExactAcceptedPhaseExecutionProfile")
        if missing: raise Blocked(*missing)
        # Restricted finalization reads immutable evidence only. It cannot resume normal work.
        with subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                              cwd=self.descriptor["workingDirectory"], env=self.descriptor["environment"]) as process:
            try: process.communicate(canonical(request), timeout=min(30,self.descriptor["completionTimeoutSeconds"]))
            except subprocess.TimeoutExpired:
                process.kill(); process.wait(); return None
        return self._observe(request)


class CohortHttpApplication:
    """Authenticated bounded receiving host, using durable current workload grants and independent role signing."""
    def __init__(self, database_path, signer, dispatch, app_channel_reference):
        self.database_path, self.signer, self.dispatch = str(database_path), signer, dispatch
        self.app_channel_reference = app_channel_reference

    def handle(self, method, headers, body):
        import hashlib
        import hmac
        import time
        import base64
        import qualify as q
        from cryptography.hazmat.primitives import hashes
        from cryptography.hazmat.primitives.asymmetric import ec
        channel = q.read_mounted_credential(self.app_channel_reference, "IndependentAppChannel")
        if len(body) > 1024 * 1024 or not hmac.compare_digest(headers.get("dapr-api-token", ""), channel):
            raise Blocked("IndependentAppChannel")
        bearer = headers.get("Authorization", "")
        if not bearer.startswith("Bearer ") or len(bearer) > 8199:
            raise Blocked("IndependentWorkloadCredential")
        request = q.decode_json(body)
        if set(request) != {"protocol", "requestId", "method", "payload"} or request["protocol"] != PROTOCOL or request["method"] != method:
            raise Blocked("ClosedExactCohortRequest")
        now = int(time.time())
        with closing(sqlite3.connect("file:" + self.database_path + "?mode=rw", uri=True)) as db:
            row = db.execute("SELECT methods_json,valid_until,revoked FROM workload_grants WHERE bearer_sha256=?", (hashlib.sha256(bearer[7:].encode()).hexdigest(),)).fetchone()
        if row is None or row[2] or row[1] <= now or method not in json.loads(row[0]):
            raise Blocked("CurrentExactCohortWorkloadScope")
        admission = _workload_admission.set((self.database_path, hashlib.sha256(bearer[7:].encode()).hexdigest(), method))
        try:
            result = self.dispatch(method, request["payload"])
        finally:
            _workload_admission.reset(admission)
        payload = {"requestId": request["requestId"], "method": method, "requestSha256": hashlib.sha256(body).hexdigest(),
                   "observedAt": now, "validUntil": min(now + 30, row[1]), "result": result}
        signature = self.signer.sign(canonical(payload), ec.ECDSA(hashes.SHA256()))
        return {"payload": payload, "signature": base64.b64encode(signature).decode()}

    def serve(self, host, port):
        from bounded_cohort_http import make_server
        with make_server(self,host,port) as server:
            server.serve_forever()

class PersistedPhaseObserver:
    """Independent receiving reader of exact immutable owner originals and their persisted source evidence."""
    def __init__(self, descriptor, binding, authority):
        import hashlib
        import qualify as q
        from cryptography.hazmat.primitives import serialization
        self.descriptor, self.binding, self.authority = descriptor, binding, authority
        raw = q.read_public_anchor(descriptor["ownerOriginalAnchorFile"])
        if hashlib.sha256(raw).hexdigest() != descriptor["ownerOriginalAnchorSha256"]:
            raise Blocked("ExactIndependentPersistedOriginalTrust")
        q.public_key_identity(raw)
        self.key = serialization.load_pem_public_key(raw)

    def dispatch(self, method, value):
        import hashlib
        import base64
        from pathlib import Path
        import qualify as q
        from cryptography.hazmat.primitives import hashes
        from cryptography.hazmat.primitives.asymmetric import ec
        if value.get("binding") != self.binding or not self.authority.call("current", {"binding": self.binding}).get("current"):
            raise Blocked("CurrentExactObserverBinding")
        if method == "preflight-phase":
            phase = value["phase"]
            if value.get("cases") != partitions()[phase]:
                raise Blocked("CompleteObserverPartition")
            missing = []
            for artifact in self.descriptor["installedArtifacts"]:
                try:
                    if hashlib.sha256(q.bounded_public_file(artifact["path"], 1024 * 1024)).hexdigest() != artifact["sha256"]:
                        missing.append("ExactInstalledObserverInput")
                except OSError:
                    missing.append("InstalledObserverInput")
            from closed_phase_invariants import missing_inputs
            for case_id in value['cases']:
                if not case_id.startswith('secrets:'):
                    missing.extend(missing_inputs(case_id))
            return {"missingInputs": sorted(set(missing))}
        if method != "lookup-phase-original":
            raise Blocked("RestrictedPersistedObserverMethod")
        request = value["request"]
        if self.authority.call("phase-admission", {"request": request}).get("exact") is not True:
            raise Blocked("ExactRetainedObservedOriginal")
        original_id = request["requestId"]
        import re
        if not re.fullmatch('[0-9a-f]{64}', original_id):
            raise Blocked("ExactOriginalLocator")
        root = Path(self.descriptor["evidenceDirectory"]) / original_id
        try:
            envelope = q.read_json(root / 'owner-original.json')
            payload = envelope['payload']
            self.key.verify(base64.b64decode(envelope['signature'], validate=True), canonical(payload), ec.ECDSA(hashes.SHA256()))
            if payload.get('requestSha256') != digest(request) or payload.get('bindingDigest') != digest(self.binding):
                raise Blocked('ChangedPersistedOwnerOriginal')
            observations = payload['observations']
            if [item['caseId'] for item in observations] != request['cases']:
                raise Blocked('CompleteIndependentPersistedCasePartition')
            passed = []
            for item in observations:
                data = q.bounded_public_file(root / (hashlib.sha256(item['caseId'].encode()).hexdigest() + '.json'), 1024 * 1024)
                if hashlib.sha256(data).hexdigest() != item['evidenceSha256']:
                    raise Blocked('ExactPersistedOwnerEvidenceBytes')
                evidence = q.decode_json(data)
                if item['caseId'].startswith('secrets:'):
                    case = next(case for case in q.plan() if 'secrets:' + case.id == item['caseId'])
                    config = q.read_public_configuration(self.descriptor['secretsRuntimeConfiguration'])
                    accepted = self.descriptor['secretsRuntimeBinding']
                    original = evidence['request']
                    if (not isinstance(original, dict) or original.get('runId') != request['attemptId']
                            or original.get('targetCommit') != self.binding.get('platformCommit')
                            or original.get('isolatedTargetId') != config.get('qualification', {}).get('isolatedTargetId')
                            or original.get('planSha256') != q.plan_digest() or original.get('caseId') != case.id
                            or original.get('requestId') != q.digest({k:v for k,v in original.items() if k != 'requestId'})
                            or evidence.get('cohortRequestId') != request['requestId']
                            or q.digest(config) != self.binding.get('configurationSha256')):
                        raise Blocked('ExactCurrentCohortSecretsOriginal')
                    execution = q.verify_signed(evidence['execution'], 'executor', config, accepted, original_time=evidence['execution']['payload']['observedAt'])
                    observation = q.verify_signed(evidence['observation'], 'observer', config, accepted, original_time=evidence['observation']['payload']['observedAt'])
                    enrolled = q.verify_signed(evidence['enrollment'], 'enrollment', config, accepted, original_time=evidence['enrollment']['payload']['observedAt'])
                    if enrolled.get('targetCommit') != original['targetCommit'] or enrolled.get('runId') != original['runId'] \
                            or enrolled.get('caseScopes', {}).get(case.id) != original.get('enrolledScope') \
                            or enrolled.get('caseTargetIds', {}).get(case.id) != original.get('caseTargetId'):
                        raise Blocked('ExactIndependentlyEnrolledCaseScope')
                    q.check_binding(execution, original)
                    q.check_binding(observation, original)
                    q.check_observation(case, observation, execution, original)
                else:
                    self._verify_source_case(request, item['caseId'], evidence)
                passed.append(item['caseId'])
            return {'state': 'Passed', 'requestSha256': digest(request), 'bindingDigest': digest(self.binding), 'passedCases': passed}
        except (OSError, ValueError, KeyError, TypeError, AttributeError):
            return {'state': 'Unknown', 'requestSha256': digest(request), 'bindingDigest': digest(self.binding), 'passedCases': []}


    def _verify_source_case(self, request, case_id, evidence):
        """Read independently signed persisted source bytes and apply the accepted per-case invariant profile."""
        import base64
        import hashlib
        from pathlib import Path
        import qualify as q
        from cryptography.hazmat.primitives import serialization, hashes
        from cryptography.hazmat.primitives.asymmetric import ec
        profile = self.descriptor.get('sourceEvidence', {}).get(case_id)
        required = {
            'EXT-PROTECTION-1.complete-owner-command': {'G1','G2','G3','G4','G5','G6','G7'},
            'host:H1': {'CurrentAuthenticatedOrigin','ExactPrivateReplayAcl','DurableDenialBeforeReturn','SpoolOutageBlocksIngress'},
            'host:H2': {'CompleteInstalledComposition','BranchBIdentity','NoConsumerExecution'},
            'host:all-consumers-refuse': {'EveryConsumerRefusesUnavailableDependency'},
            'host:H3': {'ActualProductionEngine','PersistedPlaintextAbsence','UnsealEquality','IrreversibleErasedReplay','StartupAndEveryReadiness'},
            'host:H4': {'CurrentMigrationEpoch','RepairFenceAtomicity','ExactBridgeCohort','SuccessorPreservesAllOwnerFacts'},
            'host:v21': {'AcceptedOrdinalAtWrite','HoldSealLinearization','AttestationDispatchOrdering','CompletionBarrierOrdering'},
            'host:v23': {'BlockReserveSameOwner','CompromiseSubscriberOnly','StableBatchSuccessorAttempts','ReplacementKeyBlockSetCompare','UnknownLookupOnly'},
        }.get(case_id)
        if not isinstance(profile, dict) or required is None or not isinstance(profile.get('invariants'), dict) or set(profile['invariants']) != required:
            raise Blocked('InstalledCompleteCaseInvariantProfile', case_id)
        raw_anchor = q.read_public_anchor(profile['anchorFile'])
        if hashlib.sha256(raw_anchor).hexdigest() != profile['anchorSha256'] or q.public_key_identity(raw_anchor) == q.public_key_identity(q.read_public_anchor(self.descriptor['ownerOriginalAnchorFile'])):
            raise Blocked('IndependentPersistedSourceTrust')
        locator = Path(profile['directory']) / request['requestId'] / (hashlib.sha256(case_id.encode()).hexdigest() + '.json')
        source_envelope = q.read_json(locator)
        source = source_envelope['payload']
        key = serialization.load_pem_public_key(raw_anchor)
        key.verify(base64.b64decode(source_envelope['signature'], validate=True), canonical(source), ec.ECDSA(hashes.SHA256()))
        if source.get('requestSha256') != digest(request) or source.get('bindingDigest') != digest(self.binding) or source.get('caseId') != case_id \
                or evidence.get('requestSha256') != digest(request) or evidence.get('caseId') != case_id \
                or not isinstance(source.get('records'), list) or not source['records'] or evidence.get('sourceRecordsSha256') != digest(source['records']):
            raise Blocked('ActualIndependentPersistedSourceBytes')
        records = source['records']
        if any(not isinstance(row, dict) or type(row.get('sourceRevision')) is not int or row['sourceRevision'] <= 0
               or not isinstance(row.get('eventId'), str) or not row['eventId'] or not isinstance(row.get('payload'), dict) for row in records):
            raise Blocked('CompletePersistedSourceRecords')
        # A pinned path/expected-value program is not a closed invariant implementation.
        from closed_phase_invariants import verify
        verify(case_id, source)



def main():
    """Explicit installed receiving service. Provisioning database/trust/credentials is an external owner operation."""
    import argparse
    import hashlib
    from pathlib import Path
    import qualify as q
    from cryptography.hazmat.primitives import serialization
    from joint_live_phase_runner import DaprCohortRpc
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--installation', required=True)
    args = parser.parse_args()
    config = q.read_json(args.installation)
    public = q.read_public_anchor(config['publicAnchorFile'])
    with closing(sqlite3.connect('file:' + config['databasePath'] + '?mode=rw', uri=True)) as db:
        enrollment = db.execute('SELECT public_anchor_sha256,profile_sha256 FROM service_enrollment WHERE role=?', (config['role'],)).fetchone()
    if enrollment is None or enrollment[0] != hashlib.sha256(public).hexdigest() or enrollment[1] != hashlib.sha256(q.bounded_public_file(args.installation, 1024 * 1024)).hexdigest():
        raise Blocked('IndependentServiceEnrollment')
    with open(config['signingKeyFile'], 'rb') as handle:
        signer = serialization.load_pem_private_key(handle.read(8193), password=None)
    if signer.public_key().public_bytes(serialization.Encoding.DER, serialization.PublicFormat.SubjectPublicKeyInfo) != q.public_key_identity(public):
        raise Blocked('ExactEnrolledServiceSigner')
    owners = {int(phase): DaprCohortRpc(descriptor, config['daprEndpoint'], config['daprCredentialReference'])
              for phase, descriptor in config.get('ownerTrust', {}).items()}
    if config['role'] == 'authority':
        store = CohortAuthorityStore(config['databasePath'], lambda phase, result: owners[phase].verify(result))
        dispatch = store.dispatch
    elif config['role'] == 'observer':
        authority = DaprCohortRpc(config['authority'], config['daprEndpoint'], config['daprCredentialReference'])
        if q.public_key_identity(q.read_public_anchor(config['observerProfile']['ownerOriginalAnchorFile'])) == q.public_key_identity(public):
            raise Blocked('IndependentPersistedObserverSourceSigner')
        dispatch = PersistedPhaseObserver(config['observerProfile'], config['binding'], authority).dispatch
    else:
        authority = DaprCohortRpc(config['authority'], config['daprEndpoint'], config['daprCredentialReference'])
        observer = DaprCohortRpc(config['observer'], config['daprEndpoint'], config['daprCredentialReference'])
        phase = config['phase']
        def sign(payload):
            import base64
            from cryptography.hazmat.primitives import hashes
            from cryptography.hazmat.primitives.asymmetric import ec
            return {'payload': payload, 'signature': base64.b64encode(signer.sign(canonical(payload), ec.ECDSA(hashes.SHA256()))).decode()}
        owner = RetainedPhaseOwner(config['databasePath'], phase, config['binding'],
            InstalledPhaseExecutor(config['executor'], observer, config['binding']), sign, authority)
        dispatch = owner.dispatch
    CohortHttpApplication(config['databasePath'], signer, dispatch, config['appChannelCredentialReference']).serve(config['listenHost'], config['listenPort'])


if __name__ == '__main__':
    main()
