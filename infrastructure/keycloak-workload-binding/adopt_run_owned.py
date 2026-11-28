"""Verify and adopt the ordinary Projects cohort from its direct Platform launcher."""

import base64
import hashlib
import json
import os
from pathlib import Path
import re
import ssl
import stat
import subprocess
import sys
import tempfile
import time
import urllib.parse
import urllib.request

import run_owned as owned


def encoded(value):
    return json.dumps(value, separators=(",", ":")).encode()


def sha256(raw):
    return hashlib.sha256(raw).hexdigest().upper()


def protected_bytes(path, limit):
    with os.fdopen(os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK), "rb") as stream:
        info = os.fstat(stream.fileno())
        if not stat.S_ISREG(info.st_mode) or info.st_uid != os.getuid() or info.st_mode & 0o077 or info.st_size > limit:
            owned.refuse("private-artifact-required")
        raw = stream.read(limit + 1)
        if len(raw) > limit:
            owned.refuse("private-artifact-required")
        return raw


def parse(raw):
    return json.loads(raw, object_pairs_hook=owned.unique)


def read_private(path, limit=65536):
    return parse(protected_bytes(path, limit))


def publish_once(path, value):
    """Publish finished bytes atomically, without replacing another publisher's request."""
    descriptor, name = tempfile.mkstemp(prefix=path.name + ".", dir=path.parent)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(encoded(value))
            stream.flush()
            os.fsync(stream.fileno())
        os.link(name, path)
    finally:
        Path(name).unlink(missing_ok=True)


def decode(value):
    if not re.fullmatch(r"[A-Za-z0-9_-]+", value):
        owned.refuse("invalid-signer-or-token")
    return base64.urlsafe_b64decode(value + "=" * (-len(value) % 4))


def der(tag, data):
    size = len(data)
    length = size.to_bytes((size.bit_length() + 7) // 8, "big")
    return bytes((tag, size)) + data if size < 128 else bytes((tag, 0x80 | len(length))) + length + data


def rsa_spki(key):
    def integer(raw):
        raw = raw.lstrip(b"\0")
        if not raw:
            owned.refuse("invalid-public-signer")
        return der(2, (b"\0" if raw[0] & 0x80 else b"") + raw)

    modulus, exponent = decode(key["n"]), decode(key["e"])
    if not 256 <= len(modulus) <= 512 or not 1 <= len(exponent) <= 8:
        owned.refuse("invalid-public-signer")
    public_key = der(0x30, integer(modulus) + integer(exponent))
    rsa_algorithm = bytes.fromhex("300D06092A864886F70D0101010500")
    return der(0x30, rsa_algorithm + der(3, b"\0" + public_key))


class ProjectsEnrollment:
    """A single-use, process-bound credential set with a verified HTTPS signer."""

    def __init__(self, environment):
        if sys.platform != "linux":
            owned.refuse("linux-launcher-required")
        path = Path(environment.get(owned.PREFIX + "RECEIPT", ""))
        self.capability = environment.get(owned.PREFIX + "CAPABILITY", "")
        if not path.is_absolute() or path.name != "receipt.json" or not re.fullmatch(r"[0-9a-f]{64}", self.capability):
            owned.refuse("explicit-projects-launcher-receipt-required")
        self.directory = path.parent
        info = self.directory.lstat()
        if self.directory != self.directory.resolve() or not stat.S_ISDIR(info.st_mode) or info.st_uid != os.getuid() or info.st_mode & 0o077:
            owned.refuse("private-owner-path-required")
        self.identity = info.st_dev, info.st_ino
        receipt = read_private(path)
        if (set(receipt) != {"schema", "owner", "runId", "capability", "ownerPid", "ownerIdentity", "childPid",
                "childIdentity", "directory", "deadline", "trustFile", "trustSha256", "providerSha256"}
                or receipt["schema"] != "hexalith.run-owned-workload/v1" or receipt["owner"] != owned.PROJECTS_OWNER
                or receipt["directory"] != str(self.directory) or receipt["capability"] != self.capability
                or not re.fullmatch(r"[0-9a-f]{32}", receipt["runId"])
                or not re.fullmatch(r"[0-9A-F]{64}", receipt["providerSha256"])):
            owned.refuse("foreign-launcher-receipt")
        self.receipt = receipt
        self.deadline = receipt["deadline"]
        if type(self.deadline) is not int or self.deadline > int(time.time()) + 86400:
            owned.refuse("invalid-owner-deadline")
        self.check()
        self.trust_file = self.directory / "trust.pem"
        if receipt["trustFile"] != str(self.trust_file) or environment.get("SSL_CERT_FILE") != str(self.trust_file):
            owned.refuse("missing-prelaunch-trust")
        self.trust = protected_bytes(self.trust_file, 4194304)
        if sha256(self.trust) != receipt["trustSha256"]:
            owned.refuse("changed-prelaunch-trust")
        context = ssl.create_default_context(cadata=self.trust.decode("ascii"))
        self.opener = urllib.request.build_opener(urllib.request.ProxyHandler({}),
            urllib.request.HTTPSHandler(context=context), owned.NoRedirect())

    def check(self):
        self.check_owner()
        remaining = self.deadline - time.time()
        if remaining <= 0:
            owned.refuse("fixed-owner-deadline-expired")
        return remaining

    def check_owner(self):
        receipt = self.receipt
        info = self.directory.lstat()
        if (not stat.S_ISDIR(info.st_mode) or (info.st_dev, info.st_ino) != self.identity
                or info.st_uid != os.getuid() or info.st_mode & 0o077):
            owned.refuse("changed-owner-directory")
        child, parent = os.getpid(), os.getppid()
        if (receipt["childPid"] != child or receipt["ownerPid"] != parent
                or owned.process_identity(child) != receipt["childIdentity"]
                or owned.process_identity(parent) != receipt["ownerIdentity"]):
            owned.refuse("owner-child-process-binding")

    def request_json(self, url, form=None):
        request = urllib.request.Request(url, data=None if form is None else urllib.parse.urlencode(form).encode())
        # The adopter is the launcher child. The launcher also supervises the
        # absolute deadline while a request is waiting on the network.
        with self.opener.open(request, timeout=min(5, self.check())) as response:
            raw = response.read(65537)
        if len(raw) > 65536:
            owned.refuse("public-document-size")
        self.check()
        return parse(raw)

    def adopt(self, tenant):
        """Return only fully verified enrollment. Refusals use credential-free codes."""
        try:
            return self.verify(tenant)
        except (OSError, ValueError, KeyError, TypeError, AttributeError, subprocess.SubprocessError):
            owned.refuse("projects-adoption-verification-failed")

    def verify(self, tenant):
        self.check()
        inventory = owned.projects_inventory(tenant)
        owned.validate_inventory(inventory, owned.PROJECTS_OWNER)
        inventory_digest = sha256(encoded(inventory))
        request = {key: self.receipt[key] for key in ("runId", "capability", "childPid", "childIdentity", "deadline")}
        try:
            publish_once(self.directory / "request.json", dict(request, inventory=inventory, inventorySha256=inventory_digest))
        except FileExistsError:
            owned.refuse("owner-request-already-adopted")
        result_path = self.directory / "result.json"
        while not result_path.exists():
            self.check()
            time.sleep(0.05)
        output = read_private(result_path)
        path = self.directory / "manifest.json"
        raw = protected_bytes(path, 65536)
        if output != dict(runId=self.receipt["runId"], deadline=self.deadline, inventorySha256=inventory_digest,
                manifestPath=str(path), manifestSha256=sha256(raw)):
            owned.refuse("foreign-result-or-changed-cohort")
        manifest = parse(raw)
        if (set(manifest) != {"schema", "authority", "issuer", "tokenEndpoint", "notBefore", "expiresAt",
                "signingKeyId", "signingPublicKeySha256", "clients", "receivers"}
                or manifest["schema"] != "hexalith.workload-enrollment/v1"
                or manifest["expiresAt"] != self.deadline or type(manifest["notBefore"]) is not int
                or not 0 <= manifest["notBefore"] <= time.time() < manifest["expiresAt"]):
            owned.refuse("invalid-manifest-or-validity")
        issuer = manifest["issuer"]
        if (not isinstance(issuer, str) or not re.fullmatch(r"https://localhost:[1-9][0-9]{0,4}/realms/run-owned", issuer)
                or urllib.parse.urlsplit(issuer).port > 65535 or manifest["authority"] != issuer
                or manifest["tokenEndpoint"] != issuer + "/protocol/openid-connect/token"):
            owned.refuse("disposable-loopback-authority-required")
        if (manifest["receivers"] != inventory["receivers"]
                or len(manifest["clients"]) != len(inventory["clients"])):
            owned.refuse("changed-cohort")
        credentials = {}
        for expected, client in zip(inventory["clients"], manifest["clients"], strict=True):
            resource = expected["resource"]
            expected_secret = self.directory / ("client-" + hashlib.sha256(resource.encode()).hexdigest()[:24] + ".secret")
            if client != dict(expected, secretFile=str(expected_secret)):
                owned.refuse("changed-cohort-or-tenant")
            secret = protected_bytes(expected_secret, 4096).decode()
            if not re.fullmatch(r"[A-Za-z0-9_-]{32,128}", secret):
                owned.refuse("invalid-client-secret")
            credentials[resource] = secret
        if len(set(credentials.values())) != len(credentials):
            owned.refuse("duplicate-client-credentials")
        metadata = self.request_json(issuer + "/.well-known/openid-configuration")
        jwks_uri = issuer + "/protocol/openid-connect/certs"
        if (metadata["issuer"], metadata["token_endpoint"], metadata["jwks_uri"]) != (issuer, manifest["tokenEndpoint"], jwks_uri):
            owned.refuse("public-metadata")
        candidates = [key for key in self.request_json(jwks_uri)["keys"]
            if key.get("kty") == "RSA" and key.get("alg") == "RS256" and key.get("use") == "sig"]
        if len(candidates) != 1 or candidates[0]["kid"] != manifest["signingKeyId"]:
            owned.refuse("public-signer-identity")
        public_key = rsa_spki(candidates[0])
        if sha256(public_key) != manifest["signingPublicKeySha256"]:
            owned.refuse("public-signer-digest")
        with tempfile.TemporaryDirectory(prefix="verify-", dir=self.directory) as directory:
            scratch = Path(directory)
            owned.private_write(scratch / "public.der", public_key)
            for client in inventory["clients"]:
                for probe in client["probes"]:
                    token = self.issue(manifest["tokenEndpoint"], client["workload"], credentials[client["resource"]], probe)
                    self.verify_token(token, manifest, client, probe, scratch)
        self.check()
        if sha256(protected_bytes(self.trust_file, 4194304)) != self.receipt["trustSha256"]:
            owned.refuse("changed-prelaunch-trust")
        self.issuer = issuer
        self.token_endpoint = manifest["tokenEndpoint"]
        self.credentials = credentials
        self.evidence = dict(issuer=issuer, manifestSha256=sha256(raw), signerSpkiSha256=sha256(public_key),
            providerSha256=self.receipt["providerSha256"], deadline=self.deadline, resourceCount=len(credentials),
            freshVerifiedProbes=sum(len(client["probes"]) for client in inventory["clients"]))
        return self

    def issue(self, endpoint, workload, secret, probe):
        audience, operation = probe["audience"], probe["operation"]
        form = dict(grant_type="client_credentials", client_id=workload, client_secret=secret,
            scope=f"eventstore-audience.{audience} eventstore-operation." + operation.replace(":", "."),
            authorization_details=json.dumps([dict(type=owned.PROFILE, audience=audience, operation=operation, bindings=probe["bindings"])]))
        return self.request_json(endpoint, form)["access_token"]

    def verify_token(self, token, manifest, client, probe, scratch):
        if len(token) > 16384:
            owned.refuse("invalid-token-size")
        header, payload, signature = token.split(".")
        key, claims = parse(decode(header)), parse(decode(payload))
        audience = claims["aud"]
        if isinstance(audience, list) and len(audience) == 1:
            audience = audience[0]
        issued, expiry, not_before, now = claims["iat"], claims["exp"], claims["nbf"], int(time.time())
        if (key["alg"] != "RS256" or key["kid"] != manifest["signingKeyId"] or claims["iss"] != manifest["issuer"]
                or claims["azp"] != client["workload"] or audience != probe["audience"]
                or claims["eventstore:operation"] != probe["operation"] or claims["eventstore:surface"] != "service"
                or claims["eventstore:grant-id"] != probe["grantId"]
                or type(claims["eventstore:grant-revision"]) is not int or claims["eventstore:grant-revision"] != probe["revision"]
                or any(type(value) is not int for value in (issued, expiry, not_before))
                or not manifest["notBefore"] <= issued <= now < expiry <= self.deadline
                or expiry - issued > 120 or not_before != issued
                or {key: value for key, value in claims.items() if key.startswith("eventstore:bound-")} != probe["bindings"]):
            owned.refuse("signed-probe-claims")
        owned.private_write(scratch / "signature", decode(signature))
        checked = subprocess.run(["openssl", "dgst", "-sha256", "-verify", str(scratch / "public.der"),
            "-keyform", "DER", "-signature", str(scratch / "signature")], input=(header + "." + payload).encode("ascii"),
            capture_output=True, timeout=min(5, self.check()))
        if checked.returncode != 0:
            owned.refuse("signed-probe-signature")

    def release(self):
        """Await the Platform owner's verified removal of its realm and private artifacts."""
        release = dict(runId=self.receipt["runId"], capability=self.capability)
        try:
            self.check_owner()
            try:
                publish_once(self.directory / "release.json", release)
            except FileExistsError:
                if read_private(self.directory / "release.json") != release:
                    owned.refuse("foreign-release")
        except FileNotFoundError:
            if os.path.lexists(self.directory):
                raise
            return
        end = time.monotonic() + 30
        while os.path.lexists(self.directory):
            if time.monotonic() >= end:
                owned.refuse("owned-cleanup-incomplete")
            time.sleep(0.05)
