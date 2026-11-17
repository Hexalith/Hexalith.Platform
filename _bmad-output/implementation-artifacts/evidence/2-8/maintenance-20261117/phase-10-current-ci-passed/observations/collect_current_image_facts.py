"""Collect authenticated current image metadata on a disposable GitHub runner only."""
import argparse
import base64
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import urllib.error
import urllib.parse
import urllib.request

REGISTRY = "https://registry.hexalith.com"
IMAGES = (("tenants", "7.0.7"), ("tenants-api", "7.0.7"), ("memories", "3.0.27"),
          ("memories-domain-service", "3.0.27"), ("memories-access-telemetry-clock", "3.0.27"),
          ("memories-access-telemetry", "3.0.27"))
ACCEPT = ", ".join(("application/vnd.oci.image.index.v1+json", "application/vnd.oci.image.manifest.v1+json",
                    "application/vnd.docker.distribution.manifest.list.v2+json",
                    "application/vnd.docker.distribution.manifest.v2+json"))
PUBLIC_LABELS = ("org.opencontainers.image.source", "org.opencontainers.image.revision",
                 "org.opencontainers.image.version", "org.opencontainers.image.created")


def decode(raw):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("Duplicate registry JSON field.")
            result[key] = value
        return result
    return json.loads(raw, object_pairs_hook=unique)


def digest(raw):
    return "sha256:" + hashlib.sha256(raw).hexdigest()


def require_digest(value):
    if not isinstance(value, str) or re.fullmatch(r"sha256:[0-9a-f]{64}", value) is None:
        raise ValueError("Require one exact OCI SHA256 digest.")
    return value


def token_locator(challenge, repository):
    if not challenge.startswith("Bearer ") or not re.fullmatch(r"[a-z0-9-]+", repository):
        raise ValueError("Unsupported registry authentication challenge.")
    fields = dict(re.findall(r'([a-z]+)="([^"\\]*)"', challenge[7:]))
    realm = fields.get("realm", "")
    parsed = urllib.parse.urlsplit(realm)
    origin = urllib.parse.urlsplit(REGISTRY)
    if (parsed.scheme, parsed.netloc) != (origin.scheme, origin.netloc) or parsed.query or parsed.fragment:
        raise ValueError("Registry credentials cannot leave the exact trusted registry origin.")
    service = fields.get("service")
    if not service or len(service) > 200:
        raise ValueError("Missing bounded registry token service.")
    return realm + "?" + urllib.parse.urlencode({"service": service, "scope": "repository:" + repository + ":pull"})


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, file, code, message, headers, new_url):
        raise ValueError("Registry metadata collection does not follow redirects.")


class Registry:
    def __init__(self, username, password):
        if not username or not password:
            raise ValueError("The dedicated pull reader is unavailable.")
        self._basic = "Basic " + base64.b64encode((username + ":" + password).encode()).decode()
        self._authorizations = {}
        self.token_scopes = set()
        self.authentication_schemes = {}
        self._opener = urllib.request.build_opener(NoRedirect())

    def _read(self, url, authorization=None):
        if urllib.parse.urlsplit(url).netloc != urllib.parse.urlsplit(REGISTRY).netloc:
            raise ValueError("Refuse a request outside the exact registry origin.")
        headers = {"Accept": ACCEPT, "Accept-Encoding": "identity"}
        if authorization is not None:
            headers["Authorization"] = authorization
        with self._opener.open(urllib.request.Request(url, headers=headers), timeout=45) as response:
            if response.headers.get("Content-Encoding", "identity") != "identity":
                raise ValueError("Cannot authenticate encoded registry metadata.")
            raw = response.read(4 * 1024 * 1024 + 1)
            if len(raw) > 4 * 1024 * 1024:
                raise ValueError("Registry metadata exceeds its bounded size.")
            return raw, response.headers

    def get(self, repository, kind, reference, expected=None):
        if not re.fullmatch(r"[a-z0-9-]+", repository) or kind not in ("manifests", "blobs"):
            raise ValueError("Unsupported registry metadata input.")
        if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+|sha256:[0-9a-f]{64}", reference):
            raise ValueError("Require an exact tag or digest.")
        url = REGISTRY + "/v2/" + repository + "/" + kind + "/" + reference
        authorization = self._authorizations.get(repository)
        try:
            raw, headers = self._read(url, authorization)
        except urllib.error.HTTPError as error:
            if error.code != 401 or authorization is not None:
                raise ValueError("Registry metadata request failed: HTTP " + str(error.code)) from None
            challenge = error.headers.get("WWW-Authenticate", "")
            if challenge.startswith("Basic "):
                authorization = self._basic
                self.authentication_schemes[repository] = "basic-reader-on-read-only-request"
            else:
                locator = token_locator(challenge, repository)
                token_body, _ = self._read(locator, self._basic)
                document = decode(token_body)
                token = document.get("token") or document.get("access_token")
                if not isinstance(token, str) or not token or len(token) > 32768:
                    raise ValueError("The registry did not issue a bounded pull token.")
                authorization = "Bearer " + token
                self.token_scopes.add("repository:" + repository + ":pull")
                self.authentication_schemes[repository] = "bearer-pull-token"
            self._authorizations[repository] = authorization
            raw, headers = self._read(url, authorization)
        observed = digest(raw)
        declared = headers.get("Docker-Content-Digest")
        if kind == "manifests" and declared is None:
            raise ValueError("Registry manifest has no authenticated content digest.")
        if declared is not None and require_digest(declared) != observed:
            raise ValueError("Registry metadata differs from its content digest.")
        if expected is not None and require_digest(expected) != observed:
            raise ValueError("Registry metadata differs from its descriptor digest.")
        return raw, observed


def collect(client, repository, version, output):
    output = Path(output)
    raw, tag_digest = client.get(repository, "manifests", version)
    tag = decode(raw)
    (output / (repository + "-tag.json")).write_bytes(raw)
    manifest_digest = tag_digest
    if "manifests" in tag:
        members = [item for item in tag["manifests"] if item.get("platform", {}).get("os") == "linux"
                   and item.get("platform", {}).get("architecture") == "amd64"
                   and item.get("platform", {}).get("variant") in (None, "")]
        if len(members) != 1:
            raise ValueError("Require exactly one Linux amd64 manifest.")
        manifest_digest = require_digest(members[0]["digest"])
        raw, observed = client.get(repository, "manifests", manifest_digest, manifest_digest)
        if observed != manifest_digest:
            raise ValueError("Registry child digest changed.")
    manifest = decode(raw)
    if manifest.get("schemaVersion") != 2 or "config" not in manifest or "layers" not in manifest:
        raise ValueError("Unsupported OCI image manifest.")
    (output / (repository + "-manifest.json")).write_bytes(raw)
    config_digest = require_digest(manifest["config"]["digest"])
    config_raw, observed = client.get(repository, "blobs", config_digest, config_digest)
    if observed != config_digest:
        raise ValueError("Registry image configuration digest changed.")
    config = decode(config_raw)
    if config.get("os") != "linux" or config.get("architecture") != "amd64":
        raise ValueError("Image configuration differs from Linux amd64.")
    labels = config.get("config", {}).get("Labels") or {}
    sanitized = {"os": config["os"], "architecture": config["architecture"], "digest": config_digest,
                 "labels": {key: labels[key] for key in PUBLIC_LABELS if key in labels}}
    # Never retain image environment, credentials, commands or arbitrary labels.
    (output / (repository + "-config-observation.json")).write_text(json.dumps(sanitized, indent=2) + "\n")
    return {"reference": "registry.hexalith.com/" + repository + ":" + version, "tagDigest": tag_digest,
            "linuxAmd64ManifestDigest": manifest_digest, "configDigest": config_digest,
            "labels": sanitized["labels"], "qualificationCredit": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if (os.environ.get("GITHUB_ACTIONS") != "true" or os.environ.get("CI") != "true"
            or os.environ.get("RUNNER_ENVIRONMENT") != "github-hosted" or os.environ.get("RUNNER_OS") != "Linux"
            or os.environ.get("RUNNER_ARCH") != "X64" or os.environ.get("GITHUB_REPOSITORY") != "Hexalith/Hexalith.Parties"):
        raise ValueError("Real registry collection requires its disposable hosted Parties job.")
    username = os.environ.pop("REGISTRY_READER_USERNAME", None)
    password = os.environ.pop("REGISTRY_READER_PASSWORD", None)
    client = Registry(username, password)
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    report = {"asOf": datetime.datetime.now(datetime.timezone.utc).isoformat(), "status": "running",
              "subjectBuildsVersion": "6.3.0", "subjectBuildsSource": "7afc4981e21bc2fcce687199a3fd2c61fe87336e",
              "subjectPlatformCandidate": "408481da06128ae50ad274d5dddcd4234d8b677d",
              "context": {key: os.environ.get(key) for key in ("GITHUB_REPOSITORY", "GITHUB_SHA", "GITHUB_RUN_ID",
                  "GITHUB_RUN_ATTEMPT", "GITHUB_JOB", "RUNNER_ENVIRONMENT", "RUNNER_OS", "RUNNER_ARCH", "ImageOS", "ImageVersion")},
              "images": [], "requestMethods": ["GET"], "servicesStarted": False,
              "candidateAcceptance": False, "nativeQualificationCredit": False}
    try:
        for repository, version in IMAGES:
            report["images"].append(collect(client, repository, version, output))
        report["status"] = "metadata-complete-only"
    except BaseException as error:
        report.update(status="failed", reason=type(error).__name__)
        raise
    finally:
        report.update(tokenScopesRequested=sorted(client.token_scopes), authenticationSchemes=client.authentication_schemes)
        (output / "current-image-facts.json").write_text(json.dumps(report, indent=2) + "\n")


if __name__ == "__main__":
    main()
