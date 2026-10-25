"""Independent OQ authority receiving implementation. Never initializes production genesis.

Serve behind an authenticated Dapr app channel using separately enrolled workload
and public owner trust. SQLite resides on independently administered durable storage
outside application restore. Installed storage/replication proof remains mandatory.
"""
import json
import sqlite3
from joint_live_phase_runner import PROTOCOL, PHASES, partitions, canonical, digest, Blocked


class CohortAuthorityStore:
    def __init__(self, path, verify_owner):
        self.path, self.verify_owner = str(path), verify_owner
        # mode=rw prohibits creating a database or enrolling acceptance during startup.
        with self.connect() as database:
            for name in ("accepted_sets", "current_set", "attempts", "originals", "results"):
                database.execute("SELECT * FROM " + name + " LIMIT 0")

    def connect(self):
        database = sqlite3.connect("file:" + self.path + "?mode=rw", uri=True, timeout=10)
        database.execute("PRAGMA synchronous=FULL")
        database.execute("PRAGMA foreign_keys=ON")
        return database

    def _current(self, database, binding):
        key = digest(binding)
        row = database.execute("SELECT binding_json FROM accepted_sets WHERE binding_digest=? AND accepted=1", (key,)).fetchone()
        current = database.execute("SELECT binding_digest FROM current_set WHERE slot=1", ()).fetchone()
        return row is not None and bytes(row[0]) == canonical(binding) and current is not None and current[0] == key

    def dispatch(self, method, value):
        with self.connect() as database:
            database.execute("BEGIN IMMEDIATE" if method in ("reserve", "retain", "publish") else "BEGIN")
            if method in ("preflight", "current", "reserve", "completion"):
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
                if method == "reserve":
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

    def _connect(self):
        database = sqlite3.connect("file:" + self.path + "?mode=rw", uri=True, timeout=10)
        database.execute("PRAGMA synchronous=FULL")
        return database

    def dispatch(self, method, request):
        if (request.get("bindingDigest") != digest(self.binding) or request.get("phase") != self.phase
                or request.get("cases") != partitions()[self.phase] or method not in ("execute", "lookup")):
            raise Blocked("ExactInstalledPhaseScope")
        if self.authority.call("phase-admission", {"request": request, "recovery": method == "lookup"}).get("exact") is not True:
            raise Blocked("CurrentRetainedAuthorityAdmission")
        with self._connect() as db:
            db.execute("BEGIN IMMEDIATE")
            original = db.execute("SELECT request_json,result_json FROM phase_originals WHERE request_id=?", (request["requestId"],)).fetchone()
            if original is not None and bytes(original[0]) != canonical(request):
                raise Blocked("ChangedRetainedPhaseOriginal")
            if original is not None and original[1] is not None:
                return json.loads(original[1])
            first = original is None
            if first:
                if method == "lookup":
                    return None
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
        for artifact in self.descriptor["artifacts"]:
            if hashlib.sha256(Path(artifact["path"]).read_bytes()).hexdigest() != artifact["sha256"]:
                raise Blocked("ExactInstalledPhaseExecutable")
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
        with sqlite3.connect("file:" + self.database_path + "?mode=rw", uri=True) as db:
            row = db.execute("SELECT methods_json,valid_until,revoked FROM workload_grants WHERE bearer_sha256=?", (hashlib.sha256(bearer[7:].encode()).hexdigest(),)).fetchone()
        if row is None or row[2] or row[1] <= now or method not in json.loads(row[0]):
            raise Blocked("CurrentExactCohortWorkloadScope")
        result = self.dispatch(method, request["payload"])
        payload = {"requestId": request["requestId"], "method": method, "requestSha256": hashlib.sha256(body).hexdigest(),
                   "observedAt": now, "validUntil": min(now + 30, row[1]), "result": result}
        signature = self.signer.sign(canonical(payload), ec.ECDSA(hashes.SHA256()))
        return {"payload": payload, "signature": base64.b64encode(signature).decode()}

    def serve(self, host, port):
        from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
        application = self
        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *args): pass
            def do_POST(self):
                try:
                    if not self.path.startswith('/joint/v1/') or self.headers.get('Transfer-Encoding'):
                        raise ValueError('request')
                    length = int(self.headers.get('Content-Length', '-1'))
                    if not 0 < length <= 1024 * 1024:
                        raise ValueError('bound')
                    self.connection.settimeout(30)
                    result = application.handle(self.path[len('/joint/v1/'):], self.headers, self.rfile.read(length))
                    data = canonical(result)
                    self.send_response(200); self.send_header('Content-Type', 'application/json'); self.send_header('Content-Length', str(len(data))); self.end_headers(); self.wfile.write(data)
                except Exception:
                    self.send_response(503); self.send_header('Content-Length', '0'); self.end_headers()
        ThreadingHTTPServer((host, port), Handler).serve_forever()


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
    with sqlite3.connect('file:' + config['databasePath'] + '?mode=rw', uri=True) as db:
        enrollment = db.execute('SELECT public_anchor_sha256 FROM service_enrollment WHERE role=?', (config['role'],)).fetchone()
    if enrollment is None or enrollment[0] != hashlib.sha256(public).hexdigest():
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
