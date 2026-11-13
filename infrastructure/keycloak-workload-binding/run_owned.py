#!/usr/bin/env python3
"""Launch one disposable enrollment owner with private trust, a finite lease and exact cleanup.

Linux only. No administrative user or administration API is used. Child output is
forwarded, but this module emits only metadata and constant failure codes.
"""
import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import signal
import ssl
import stat
import subprocess
import sys
import tempfile
import time
import urllib.parse
import urllib.request

IMAGE = "quay.io/keycloak/keycloak:26.6.4"
OWNER = "disposable-folders-acceptance"
PREFIX = "HEXALITH_RUN_OWNED_WORKLOAD_"
PROFILE = "https://hexalith.com/security/workload-binding/v1"
LABEL = "com.hexalith.run-owned-workload"
ROOT = Path(__file__).resolve().parent
JAR = ROOT / "target/keycloak-workload-binding-1.0.0-SNAPSHOT.jar"


class Cancellation(BaseException):
    """Terminal-signal cancellation. Not an OSError, so I/O retry handlers cannot swallow it."""


def refuse(code):
    raise RuntimeError("Run-owned workload enrollment refused: " + code)


def command(*args, timeout=30, binary=False, environment=None):
    try:
        result = subprocess.run(args, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=timeout, check=True, env=environment)
        return result.stdout if binary else result.stdout.decode().strip()
    except (subprocess.SubprocessError, OSError) as error:
        output = (getattr(error, "output", b"") or b"") + (getattr(error, "stderr", b"") or b"")
        if isinstance(output, bytes):
            output = output.decode(errors="replace")
        reason = next((name for name, phrase in (("port-conflict", "already allocated"), ("address-conflict", "address already in use"),
            ("missing-path", "no such file or directory"), ("mount-type", "not a directory"), ("permissions", "permission denied"),
            ("mount-config", "invalid mount config"), ("exists", "file exists"), ("daemon", "cannot connect")) if phrase in output.lower()), "unavailable")
        refuse("owned-command-failed:" + Path(args[0]).name + "-" + (args[1] if len(args) > 1 and args[1] in ("run", "exec", "inspect", "ps", "rm", "req", "x509", "pkey", "dgst") else "operation") + ":" + reason)


def process_identity(pid):
    """PID reuse resistant Linux identity, shared with the C# bridge."""
    try:
        value = Path(f"/proc/{pid}/stat").read_text()
        start = value[value.rindex(")") + 2:].split()[19]
        return Path("/proc/sys/kernel/random/boot_id").read_text().strip() + ":" + start
    except (OSError, ValueError, IndexError):
        refuse("owner-or-child-process-unavailable")


def private_write(path, data):
    raw = data if isinstance(data, bytes) else json.dumps(data, separators=(",", ":")).encode()
    temporary = path.with_name(path.name + ".writing")
    with os.fdopen(os.open(temporary, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), "wb") as stream:
        stream.write(raw)
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, path)


def unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            refuse("duplicate-json-field")
        result[key] = value
    return result


def read_private(path, maximum=65536, include_raw=False):
    info = path.lstat()
    if not stat.S_ISREG(info.st_mode) or info.st_uid != os.getuid() or info.st_mode & 0o077 or info.st_size > maximum:
        refuse("private-artifact-required")
    raw = path.read_bytes()
    value = json.loads(raw, object_pairs_hook=unique)
    return (value, raw) if include_raw else value


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def cohort_digest(inventory_bytes):
    # This is the SHA-256 of the exact request inventory bytes, not a second JSON canonicalization.
    return hashlib.sha256(inventory_bytes).hexdigest().upper()


def request_inventory_bytes(raw):
    """Extract the already validated inventory value without changing its JSON spelling."""
    value = raw.decode("utf-8")
    decoder = json.JSONDecoder(object_pairs_hook=unique)
    offset = value.index("{") + 1
    while True:
        offset += len(value[offset:]) - len(value[offset:].lstrip())
        name, offset = decoder.raw_decode(value, offset)
        offset += len(value[offset:]) - len(value[offset:].lstrip())
        if value[offset] != ":":
            refuse("request-fields")
        offset += 1
        offset += len(value[offset:]) - len(value[offset:].lstrip())
        start = offset
        _, offset = decoder.raw_decode(value, offset)
        if name == "inventory":
            return value[start:offset].encode("utf-8")
        offset += len(value[offset:]) - len(value[offset:].lstrip())
        if value[offset] != ",":
            refuse("request-fields")
        offset += 1


def validate_inventory(inventory):
    if set(inventory) != {"tenant", "receivers", "clients"}:
        refuse("inventory-fields")
    tenant, receivers, clients = inventory["tenant"], inventory["receivers"], inventory["clients"]
    if not isinstance(tenant, str) or not re.fullmatch(r"[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?", tenant) or tenant == "system":
        refuse("selected-tenant")
    if not isinstance(clients, list) or not 4 <= len(clients) <= 32 or not isinstance(receivers, list):
        refuse("cohort")
    resources = [client.get("resource") for client in clients]
    if len(set(resources)) != len(resources) or sorted(receivers) != sorted(resources) or len(set(receivers)) != len(receivers):
        refuse("cohort")
    if not {"eventstore", "tenants", "folders", "folders-workers"}.issubset(resources):
        refuse("cohort")
    if any(value not in {"eventstore", "tenants", "folders", "folders-replica"} and not re.fullmatch(r"folders-workers(?:-[a-z0-9-]+)?", value) for value in resources):
        refuse("cohort")
    identities = {}
    for client in clients:
        if set(client) != {"resource", "clientId", "workload", "probes"}:
            refuse("client-fields")
        workload = client["workload"]
        if not isinstance(workload, str) or not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_.-]{0,127}", workload) or client["clientId"] != workload:
            refuse("actual-workload-identity")
        probes = client["probes"]
        if not isinstance(probes, list) or not 1 <= len(probes) <= 64:
            refuse("probes")
        if workload in identities and identities[workload] != probes:
            refuse("replica-permissions-changed")
        identities[workload] = probes
        for probe in probes:
            if set(probe) != {"grantId", "revision", "audience", "operation", "bindings"} or type(probe["revision"]) is not int or probe["revision"] != 1:
                refuse("probe-fields")
            if not isinstance(probe["grantId"], str) or not re.fullmatch(r"[A-Za-z0-9-]{1,128}", probe["grantId"]):
                refuse("grant-identity")
    workloads = {c["resource"]: c["workload"] for c in clients}
    if len({workloads[name] for name in ("eventstore", "tenants", "folders", "folders-workers")}) != 4:
        refuse("distinct-primary-identities")
    for name in ("folders-workers-proof-a", "folders-workers-proof-b"):
        if name in workloads and list(workloads.values()).count(workloads[name]) != 1:
            refuse("distinct-proof-identities")
    expected_gateway = [(workloads[receiver], "domain-service:" + operation, {})
        for receiver in ("folders", "tenants") for operation in ("process", "project", "metadata", "replay-state")]
    for receiver, domain, storage_tenant in (("folders", "folders", tenant), ("tenants", "tenants", "system"), ("tenants", "global-administrators", "system")):
        expected_gateway.append((workloads[receiver], "domain-service:query",
            {"eventstore:bound-tenant": storage_tenant, "eventstore:bound-domain": domain, "eventstore:bound-query-authority-v1": None}))
    for client in clients:
        if client["resource"] == "eventstore":
            expected = expected_gateway
        elif client["resource"] == "tenants":
            expected = [(workloads["eventstore"], operation, {}) for operation in ("eventstore:command-submit", "eventstore:stream-read")]
        else:
            expected = [(workloads["eventstore"], "eventstore:stream-read", {"eventstore:bound-tenant": tenant, "eventstore:bound-domain": "folders"})]
        remaining = list(expected)
        for probe in client["probes"]:
            bindings = probe["bindings"]
            if not isinstance(bindings, dict):
                refuse("probe-bindings")
            compare = dict(bindings)
            if "eventstore:bound-query-authority-v1" in compare:
                if not isinstance(compare["eventstore:bound-query-authority-v1"], str) or not re.fullmatch(r"[0-9A-F]{64}", compare["eventstore:bound-query-authority-v1"]):
                    refuse("query-digest")
                compare["eventstore:bound-query-authority-v1"] = None
            match = (probe["audience"], probe["operation"], compare)
            if match not in remaining:
                refuse("exact-permission-inventory")
            remaining.remove(match)
        if remaining:
            refuse("incomplete-permission-inventory")
    return identities


def build_realm(inventory, directory, not_before, deadline):
    identities = validate_inventory(inventory)
    clients, entries, scopes = [], [], set()
    credentials = {}
    for workload, probes in identities.items():
        secret = secrets.token_urlsafe(48)
        secret_file = directory / ("client-" + hashlib.sha256(workload.encode()).hexdigest()[:24] + ".secret")
        private_write(secret_file, secret.encode())
        credentials[workload] = secret_file
        grants, own_scopes = [], set()
        for probe in probes:
            own_scopes.update(("eventstore-audience." + probe["audience"], "eventstore-operation." + probe["operation"].replace(":", ".")))
            grants.append(dict(grantId=probe["grantId"], revision=probe["revision"], clientId=workload, workload=workload,
                audience=probe["audience"], operations=[probe["operation"]],
                bindings={key: [value] for key, value in probe["bindings"].items() if key != "eventstore:bound-query-authority-v1"},
                allowUnbound=not probe["bindings"], allowQueryAuthorityDigest=probe["operation"] == "domain-service:query",
                notBefore=not_before, expiresAt=deadline, revoked=False))
        scopes.update(own_scopes)
        clients.append(dict(clientId=workload, enabled=True, publicClient=False, secret=secret, protocol="openid-connect",
            serviceAccountsEnabled=True, standardFlowEnabled=False, implicitFlowEnabled=False, directAccessGrantsEnabled=False,
            fullScopeAllowed=False, defaultClientScopes=[], optionalClientScopes=sorted(own_scopes),
            attributes={"hexalith.workload-binding.workload": workload, "hexalith.workload-binding.grants": json.dumps(grants)},
            protocolMappers=[dict(name="workload-binding", protocol="openid-connect", protocolMapper="hexalith-workload-binding-v1", config={})]))
    for client in inventory["clients"]:
        entries.append(dict(client, secretFile=str(credentials[client["workload"]])))
    return dict(realm="run-owned", enabled=True, sslRequired="all", defaultSignatureAlgorithm="RS256",
        attributes={"hexalith.workload-binding.enabled": "true", "hexalith.workload-binding.signer-not-after": str(deadline)},
        clientScopes=[dict(name=scope, protocol="openid-connect", attributes={}, protocolMappers=[]) for scope in sorted(scopes)],
        clients=clients, clientProfiles={"profiles": [dict(name="workload-binding", executors=[dict(executor="hexalith-workload-binding-v1", configuration={})])]},
        clientPolicies={"policies": [dict(name="workload-binding", enabled=True, conditions=[dict(condition="any-client", configuration={})], profiles=["workload-binding"])]}), entries


class Lease:
    def __init__(self, lifetime, owner=OWNER):
        if owner != OWNER or not isinstance(lifetime, int) or not 1 <= lifetime <= 86400 or sys.platform != "linux":
            refuse("explicit-disposable-owner-and-finite-linux-lifetime-required")
        if not JAR.is_file():
            refuse("build-provider-first")
        self.deadline = int(time.time()) + lifetime
        self.command_environment = dict(os.environ)
        if not self.command_environment.get("DOCKER_CONTEXT") and not self.command_environment.get("DOCKER_HOST"):
            self.command_environment["DOCKER_CONTEXT"] = command("docker", "context", "show",
                timeout=min(30, self.remaining()), environment=self.command_environment)
        self.run_id = secrets.token_hex(16)
        self.directory = Path(tempfile.mkdtemp(prefix="hexalith-run-owned-workload-"))
        try:
            os.chmod(self.directory, 0o700)
            self.directory_inode = self.directory.stat().st_ino
            self.name = "hexalith-run-owned-workload-" + self.run_id
            self.child = None
            self.created = False
            self.container_id = None
            self.receipt = None
            self.journal = dict(runId=self.run_id, container=self.name, creationStarted=False)
            private_write(self.directory / "journal.json", self.journal)
            # Maven's output may be rebuilt concurrently; bind and mount one private snapshot.
            self.provider = self.directory / "provider.jar"
            private_write(self.provider, JAR.read_bytes())
        except BaseException:
            try:
                shutil.rmtree(self.directory)
            except OSError:
                print(json.dumps(dict(qualification="run-owned enrollment",
                    cleanupIncomplete=["owned-private-material-cleanup-incomplete"], remainingContainer=None,
                    remainingPrivateDirectory=str(self.directory))), flush=True)
                refuse("owned-private-material-cleanup-incomplete")
            raise

    def remaining(self):
        remaining = self.deadline - time.time()
        if remaining <= 0:
            refuse("fixed-owner-deadline-expired")
        return remaining

    def run(self, *args, binary=False, limit=30):
        # Cleanup has its own bounded commands; every preparation/provisioning command uses the original lease.
        return command(*args, timeout=min(limit, self.remaining()), binary=binary, environment=self.command_environment)

    def prepare(self):
        directory = self.directory
        self.pin_docker_context()
        self.run("openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes", "-days", "1", "-subj", "/CN=Run-owned workload CA",
            "-addext", "basicConstraints=critical,CA:TRUE", "-addext", "keyUsage=critical,keyCertSign,cRLSign", "-keyout", str(directory / "ca.key"), "-out", str(directory / "ca.pem"))
        self.run("openssl", "req", "-newkey", "rsa:2048", "-nodes", "-subj", "/CN=localhost", "-keyout", str(directory / "tls.key"), "-out", str(directory / "tls.csr"))
        private_write(directory / "tls.ext", b"subjectAltName=DNS:localhost\nbasicConstraints=critical,CA:FALSE\nkeyUsage=digitalSignature,keyEncipherment\nextendedKeyUsage=serverAuth\n")
        self.run("openssl", "x509", "-req", "-in", str(directory / "tls.csr"), "-CA", str(directory / "ca.pem"), "-CAkey", str(directory / "ca.key"),
            "-CAcreateserial", "-days", "1", "-extfile", str(directory / "tls.ext"), "-out", str(directory / "tls.pem"))
        inherited_name = os.environ.get("SSL_CERT_FILE") if "SSL_CERT_FILE" in os.environ else ssl.get_default_verify_paths().cafile
        if not inherited_name:
            refuse("system-or-inherited-launch-trust-required")
        inherited = Path(inherited_name)
        if not inherited.is_file():
            refuse("system-or-inherited-launch-trust-required")
        try:
            inherited_trust = inherited.read_bytes()
        except OSError:
            refuse("system-or-inherited-launch-trust-required")
        private_write(directory / "trust.pem", inherited_trust + b"\n" + (directory / "ca.pem").read_bytes())
        for path in directory.rglob("*"):
            path.chmod(0o700 if path.is_dir() else 0o600)
        self.check()

    def pin_docker_context(self):
        context = self.command_environment.get("DOCKER_CONTEXT")
        if not context or context == "default":
            return
        # Context names are mutable. Import the selected endpoint and TLS material into private config.
        archive = self.directory / "docker-context.tar"
        self.run("docker", "context", "export", context, str(archive))
        archive.chmod(0o600)
        configuration = self.directory / "docker-config"
        configuration.mkdir(mode=0o700)
        self.command_environment["DOCKER_CONFIG"] = str(configuration)
        self.run("docker", "context", "import", context, str(archive))

    def bind(self, child_pid, capability):
        self.receipt = dict(schema="hexalith.run-owned-workload/v1", owner=OWNER, runId=self.run_id, capability=capability,
            ownerPid=os.getpid(), ownerIdentity=process_identity(os.getpid()), childPid=child_pid, childIdentity=process_identity(child_pid),
            directory=str(self.directory), deadline=self.deadline, trustFile=str(self.directory / "trust.pem"),
            trustSha256=digest(self.directory / "trust.pem"), providerSha256=digest(self.provider))
        private_write(self.directory / "receipt.json", self.receipt)

    def check(self, require_child=False):
        if require_child and self.child is not None and self.child.poll() is not None:
            refuse("child-exited-during-provisioning")
        if int(time.time()) >= self.deadline:
            refuse("fixed-owner-deadline-expired")
        if self.receipt and (process_identity(self.receipt["childPid"]) != self.receipt["childIdentity"] or process_identity(self.receipt["ownerPid"]) != self.receipt["ownerIdentity"]):
            refuse("process-binding-changed")

    def provision(self):
        self.check(require_child=True)
        request, request_bytes = read_private(self.directory / "request.json", include_raw=True)
        if set(request) != {"runId", "capability", "childPid", "childIdentity", "deadline", "inventory", "inventorySha256"}:
            refuse("request-fields")
        for field in ("runId", "capability", "childPid", "childIdentity", "deadline"):
            if request[field] != self.receipt[field]:
                refuse("foreign-owner-request")
        inventory = request["inventory"]
        if request["inventorySha256"] != cohort_digest(request_inventory_bytes(request_bytes)):
            refuse("cohort-binding-changed")
        if digest(self.provider) != self.receipt["providerSha256"]:
            refuse("provider-binding-changed")
        realm, clients = build_realm(inventory, self.directory, int(time.time()) - 1, self.deadline)
        private_write(self.directory / "realm.json", realm)
        self.journal["creationStarted"] = True
        private_write(self.directory / "journal.json", self.journal)
        self.created = True
        self.check(require_child=True)
        container_id = self.run("docker", "run", "--detach", "--rm", "--name", self.name, "--label", LABEL + "=" + self.run_id,
            "--user", f"{os.getuid()}:0", "--publish", "127.0.0.1::8443",
            "--volume", f"{self.provider}:/opt/keycloak/providers/workload-binding.jar:ro",
            "--volume", f"{self.directory / 'tls.pem'}:/run-owned/tls.pem:ro", "--volume", f"{self.directory / 'tls.key'}:/run-owned/tls.key:ro",
            "--volume", f"{self.directory / 'realm.json'}:/opt/keycloak/data/import/run-owned-realm.json:ro",
            IMAGE, "start-dev", "--import-realm", "--http-enabled=false", "--https-certificate-file=/run-owned/tls.pem",
            "--https-certificate-key-file=/run-owned/tls.key", "--hostname=localhost",
            limit=60)
        if not re.fullmatch(r"[0-9a-f]{64}", container_id):
            refuse("container-identity")
        self.container_id = container_id
        self.journal["containerId"] = container_id
        private_write(self.directory / "journal.json", self.journal)
        base = self.https_origin()
        issuer = base + "/realms/run-owned"
        context = ssl.create_default_context(cafile=str(self.directory / "trust.pem"))
        while True:
            self.check(require_child=True)
            if (self.directory / "release.json").exists():
                refuse("cohort-adoption-cancelled")
            try:
                metadata = self.get(issuer + "/.well-known/openid-configuration", context)
                break
            except (OSError, json.JSONDecodeError, UnicodeDecodeError):
                time.sleep(0.25)
        version = self.run("docker", "exec", self.container_id, "/opt/keycloak/bin/kc.sh", "--version")
        if not re.search(r"Keycloak 26\.6\.4(?:\s|$)", version):
            refuse("exact-server-version-required")
        if metadata["issuer"] != issuer or metadata["token_endpoint"] != issuer + "/protocol/openid-connect/token" or metadata["jwks_uri"] != issuer + "/protocol/openid-connect/certs":
            refuse("public-metadata")
        jwks = self.get(metadata["jwks_uri"], context)
        signers = [key for key in jwks["keys"] if key.get("kty") == "RSA" and key.get("alg") == "RS256" and key.get("use") == "sig"]
        if len(signers) != 1:
            refuse("public-signer-identity")
        signer = signers[0]
        private_write(self.directory / "signer.pem", ("-----BEGIN CERTIFICATE-----\n" + signer["x5c"][0] + "\n-----END CERTIFICATE-----\n").encode())
        public = self.run("openssl", "x509", "-in", str(self.directory / "signer.pem"), "-pubkey", "-noout")
        private_write(self.directory / "public.pem", public.encode())
        spki = self.run("openssl", "pkey", "-pubin", "-in", str(self.directory / "public.pem"), "-outform", "DER", binary=True)
        manifest = dict(schema="hexalith.workload-enrollment/v1", authority=issuer, issuer=issuer, tokenEndpoint=metadata["token_endpoint"],
            notBefore=int(time.time()) - 1, expiresAt=self.deadline, signingKeyId=signer["kid"], signingPublicKeySha256=hashlib.sha256(spki).hexdigest().upper(),
            receivers=inventory["receivers"], clients=clients)
        # Every fallible daemon observation precedes publication; an adopted child is never failed afterwards.
        image_id = self.run("docker", "inspect", self.container_id, "--format", "{{.Image}}")
        self.check(require_child=True)
        private_write(self.directory / "manifest.json", manifest)
        private_write(self.directory / "result.json", dict(runId=self.run_id, deadline=self.deadline, inventorySha256=request["inventorySha256"],
            manifestPath=str(self.directory / "manifest.json"), manifestSha256=digest(self.directory / "manifest.json")))
        return dict(qualification="run-owned enrollment", serverVersion="26.6.4", imageId=image_id,
            providerSha256=self.receipt["providerSha256"], resourceCount=len(clients), workloadCount=len(realm["clients"]),
            administrativeIdentity=False, startupImport=True)

    def https_origin(self):
        # Keycloak fixes the hostname; Docker supplies the actual loopback port, never a socket guess.
        template = '{"id":{{json .Id}},"runId":{{json (index .Config.Labels "' + LABEL + '")}},"bindings":{{json .NetworkSettings.Ports}}}'
        snapshot = json.loads(self.run("docker", "inspect", self.container_id, "--format", template), object_pairs_hook=unique)
        if not isinstance(snapshot, dict) or set(snapshot) != {"id", "runId", "bindings"} or snapshot["id"] != self.container_id or snapshot["runId"] != self.run_id:
            refuse("owned-loopback-port-binding-required")
        bindings = snapshot["bindings"]
        if not isinstance(bindings, dict) or {name for name, value in bindings.items() if value is not None} != {"8443/tcp"}:
            refuse("owned-loopback-port-binding-required")
        published = bindings["8443/tcp"]
        if not isinstance(published, list) or len(published) != 1 or not isinstance(published[0], dict) or set(published[0]) != {"HostIp", "HostPort"}:
            refuse("owned-loopback-port-binding-required")
        address = published[0]
        port = address["HostPort"]
        if address["HostIp"] != "127.0.0.1" or not isinstance(port, str) or not re.fullmatch(r"[1-9][0-9]{0,4}", port) or int(port) > 65535:
            refuse("owned-loopback-port-binding-required")
        return "https://localhost:" + port

    def get(self, url, context):
        self.check(require_child=True)
        # No redirects or cross-origin followups are allowed for authority metadata.
        opener = urllib.request.build_opener(urllib.request.HTTPSHandler(context=context), NoRedirect())
        # Socket timeouts bound inactivity. The dedicated Linux supervisor also bounds total elapsed time.
        remaining = self.remaining()
        previous_handler = signal.signal(signal.SIGALRM, lambda *_: refuse("fixed-owner-deadline-expired"))
        started = time.monotonic()
        previous_timer = signal.setitimer(signal.ITIMER_REAL, remaining)
        try:
            with opener.open(url, timeout=min(5, self.remaining())) as response:
                raw = response.read(65537)
                if len(raw) > 65536:
                    refuse("public-document-size")
                self.check(require_child=True)
                return json.loads(raw, object_pairs_hook=unique)
        finally:
            signal.setitimer(signal.ITIMER_REAL, 0)
            signal.signal(signal.SIGALRM, previous_handler)
            if previous_timer[0]:
                signal.setitimer(signal.ITIMER_REAL, max(0.000001, previous_timer[0] - (time.monotonic() - started)), previous_timer[1])

    def container_snapshot(self):
        # ID and ownership label are captured in one daemon snapshot. A reused name is never a delete target.
        selector = "id=" + self.container_id if self.container_id else "name=^/" + self.name + "$"
        template = '{"id":"{{.ID}}","runId":"{{.Label "' + LABEL + '"}}"}'
        found = command("docker", "ps", "--all", "--no-trunc", "--filter", selector, "--format", template, timeout=10, environment=self.command_environment)
        if not found:
            return None
        snapshot = json.loads(found, object_pairs_hook=unique)
        if set(snapshot) != {"id", "runId"} or not re.fullmatch(r"[0-9a-f]{64}", snapshot["id"]) or snapshot["runId"] != self.run_id:
            refuse("foreign-container-preserved")
        if self.container_id and snapshot["id"] != self.container_id:
            refuse("foreign-container-preserved")
        self.container_id = snapshot["id"]
        self.journal["containerId"] = self.container_id
        return self.container_id

    def cleanup(self):
        failures = []
        if self.created:
            try:
                container_id = self.container_snapshot()
                if container_id:
                    command("docker", "rm", "--force", "--volumes", container_id, timeout=10, environment=self.command_environment)
                    if self.container_snapshot():
                        refuse("owned-container-remains")
                self.created = False
            except (RuntimeError, OSError, ValueError, KeyError, TypeError):
                failures.append("owned-container-cleanup-incomplete")
        # Directory absence is the bridge's private acknowledgement. Keep it until container removal is proven.
        if not failures:
            try:
                if os.path.lexists(self.directory):
                    if self.directory.is_symlink() or self.directory.stat().st_ino != self.directory_inode:
                        refuse("foreign-directory-preserved")
                    shutil.rmtree(self.directory)
                if os.path.lexists(self.directory):
                    refuse("owned-directory-remains")
            except (OSError, RuntimeError):
                failures.append("owned-private-material-cleanup-incomplete")
        if failures:
            print(json.dumps(dict(qualification="run-owned enrollment", cleanupIncomplete=failures,
                remainingContainer=self.container_id or self.name if "owned-container-cleanup-incomplete" in failures else None,
                remainingPrivateDirectory=str(self.directory) if os.path.lexists(self.directory) else None)), flush=True)
            refuse(",".join(failures))


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        refuse("authority-redirect")


def enable_subreaper():
    # Reap orphaned descendants ourselves so a dead group leader cannot leave zombies holding its group.
    if ctypes.CDLL(None, use_errno=True).prctl(36, 1, 0, 0, 0) != 0:
        refuse("owned-child-subreaper-unavailable")


def group_members(group):
    members = []
    for path in Path("/proc").iterdir():
        if not path.name.isdigit():
            continue
        try:
            value = (path / "stat").read_text()
            fields = value[value.rindex(")") + 2:].split()
            if int(fields[2]) == group and int(fields[3]) == group:
                members.append(int(path.name))
        except (OSError, ValueError, IndexError):
            continue
    return members


def process_snapshot():
    processes = {}
    for path in Path("/proc").iterdir():
        if not path.name.isdigit():
            continue
        try:
            value = (path / "stat").read_text()
            fields = value[value.rindex(")") + 2:].split()
            processes[int(path.name)] = (int(fields[1]), int(fields[2]), int(fields[3]), fields[19])
        except (OSError, ValueError, IndexError):
            continue
    return processes


def owned_members(child, existing):
    # The dedicated subreaper owns newly adopted children even when they escape with setsid.
    # Every process in a tree that predates this launch remains unrelated after reparenting.
    processes = process_snapshot()
    members = {pid for pid, (parent, group, session, start) in processes.items()
        if group == child.pid or session == child.pid
        or parent == os.getpid() and (pid, start) not in existing}
    while True:
        descendants = {pid for pid, (parent, _, _, _) in processes.items() if parent in members}
        expanded = members | descendants
        if expanded == members:
            return {pid: processes[pid][3] for pid in members}
        members = expanded


def existing_process_tree():
    processes = process_snapshot()
    members = {os.getpid()}
    while True:
        expanded = members | {pid for pid, (parent, _, _, _) in processes.items() if parent in members}
        if expanded == members:
            return {(pid, processes[pid][3]) for pid in members if pid != os.getpid()}
        members = expanded


def stop_child(child, existing_children):
    if child is None:
        return
    # Re-scan while terminating: descendants can create a new session or become subreaper children.
    # Each identity receives each signal once; repeats would trip second-signal force-exit handlers.
    for number in (signal.SIGTERM, signal.SIGKILL):
        deadline = time.monotonic() + 5
        signalled = set()
        while True:
            child.poll()
            members = owned_members(child, existing_children)
            for pid, start in members.items():
                try:
                    # A recycled PID is never a signal target.
                    if (pid, start) not in signalled and process_snapshot().get(pid, (None, None, None, None))[3] == start:
                        os.kill(pid, number)
                        signalled.add((pid, start))
                    os.waitpid(pid, os.WNOHANG)
                except (ProcessLookupError, ChildProcessError):
                    pass
            if not owned_members(child, existing_children):
                return
            if time.monotonic() >= deadline:
                break
            time.sleep(0.02)
    refuse("owned-child-tree-cleanup-incomplete")


def launch(lifetime, owner, arguments):
    if not arguments:
        refuse("child-command-required")
    # The CLI is a dedicated supervisor. Embedding it beside an unrelated live tree is unsafe:
    # later descendants can be adopted after their original ancestry is no longer observable.
    if existing_process_tree():
        refuse("dedicated-launcher-process-required")
    lease = None
    summary = dict(qualification="run-owned enrollment", provisioned=False)
    previous = {}
    existing_children = set()
    terminating = False
    starting_child = False
    terminal_signals = (signal.SIGTERM, signal.SIGINT, signal.SIGHUP, signal.SIGQUIT, signal.SIGABRT)
    # Hold cancellation until both the handlers and the cleanup owner cover allocation.
    previous_mask = signal.pthread_sigmask(signal.SIG_BLOCK, terminal_signals)
    def terminal(signum, frame):
        nonlocal terminating
        if terminating:
            return
        terminating = True
        if not starting_child:
            raise Cancellation("Run-owned workload enrollment cancelled.")
    try:
        for number in terminal_signals:
            previous[number] = signal.signal(number, terminal)
        lease = Lease(lifetime, owner)
        # The lease pins Docker selection for its own commands only; the child keeps its original environment.
        child_environment = dict(os.environ)
        signal.pthread_sigmask(signal.SIG_SETMASK, previous_mask)
        lease.prepare()
        enable_subreaper()
        capability = secrets.token_hex(32)
        environment = dict(child_environment, **{PREFIX + "RECEIPT": str(lease.directory / "receipt.json"), PREFIX + "CAPABILITY": capability,
            "SSL_CERT_FILE": str(lease.directory / "trust.pem")})
        lease.check()
        existing_children = existing_process_tree()
        # A signal can arrive after OS allocation but before Popen returns its handle.
        # Record ownership before acting on cancellation; the child inherits no blocked signals.
        starting_child = True
        try:
            lease.child = subprocess.Popen([sys.executable, "-B", str(Path(__file__).resolve()), "_child", *arguments], env=environment, start_new_session=True)
            lease.bind(lease.child.pid, capability)
        finally:
            starting_child = False
        if terminating:
            raise Cancellation("Run-owned workload enrollment cancelled.")
        adopted = False
        while lease.child.poll() is None:
            lease.check()
            if not adopted and (lease.directory / "request.json").exists():
                adopted = True
                try:
                    summary.update(lease.provision(), provisioned=True)
                except RuntimeError as error:
                    if str(error) != "Run-owned workload enrollment refused: cohort-adoption-cancelled":
                        raise
                    summary["adoptionCancelled"] = True
            if (lease.directory / "release.json").exists():
                release = read_private(lease.directory / "release.json")
                if release != {"runId": lease.run_id, "capability": capability}:
                    refuse("foreign-release")
                lease.cleanup()
                break
            time.sleep(0.05)
        while lease.child.poll() is None:
            lease.check()
            time.sleep(0.05)
        return lease.child.returncode
    finally:
        terminating = True
        try:
            try:
                if lease is not None: stop_child(lease.child, existing_children)
            finally:
                if lease is not None: lease.cleanup()
            if lease is not None:
                summary.update(observedRemainingContainers=0, observedRemainingTemporaryPaths=0)
            # Child reporters may finish with an ANSI reset without a newline.
            print("\n" + json.dumps(summary), flush=True)
        finally:
            signal.pthread_sigmask(signal.SIG_SETMASK, previous_mask)
            for number, handler in previous.items():
                signal.signal(number, handler)


def child_gate(arguments):
    receipt = Path(os.environ[PREFIX + "RECEIPT"])
    deadline = time.monotonic() + 10
    while not receipt.exists():
        if time.monotonic() >= deadline:
            refuse("launcher-receipt-unavailable")
        time.sleep(0.01)
    os.execvpe(arguments[0], arguments, os.environ)


def main():
    if len(sys.argv) > 1 and sys.argv[1] == "_child":
        child_gate(sys.argv[2:])
        return 1
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--owner", required=True, choices=[OWNER])
    parser.add_argument("--lifetime-seconds", required=True, type=int)
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    arguments = args.command[1:] if args.command[:1] == ["--"] else args.command
    return launch(args.lifetime_seconds, args.owner, arguments)


if __name__ == "__main__":
    try:
        sys.exit(main())
    except RuntimeError as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
    except (Cancellation, OSError, ValueError, KeyError, TypeError):
        # Protected request data, credentials, token responses and command arguments stay private.
        print("Run-owned workload enrollment refused; private producer or lifecycle precondition failed.", file=sys.stderr)
        sys.exit(1)
