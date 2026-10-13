#!/usr/bin/env python3
"""Prepare an offline Timesheets enrollment candidate. Never deploy or accept evidence."""

import argparse
from contextlib import contextmanager
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import stat
import subprocess
import tempfile


SCRIPT = Path(__file__).resolve()
HERE = SCRIPT.parent
EXECUTING_SCRIPT_SHA256 = hashlib.sha256(SCRIPT.read_bytes()).hexdigest()
DECLARATION = Path("eng/timesheets-deployment/hexalith.module-manifest.v2.json")
GENERATOR = "eng/timesheets-deployment/prepare.py"
OWNER_REPORT = "docs/deployed-magic-link-acceptance.md"
OWNER_REPORT_SHA256 = "f3fa3e844f0e23f732371e80258945feede0866b0608e5768457433ebacb2118"
PIN = "references/Hexalith.Timesheets"
SPINE = Path("_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md")
STATUS = Path("_bmad-output/implementation-artifacts/sprint-status.yaml")
BACKLOG = ("4-14", "4-15", "4-16", "4-17", "4-22")
SOURCE_FILES = (
    "src/Hexalith.Timesheets/Hexalith.Timesheets.csproj",
    "src/Hexalith.Timesheets/Program.cs",
    "src/Hexalith.Timesheets/Runtime/InternalSurfaceGuard.cs",
    "src/Hexalith.Timesheets/Runtime/InternalSurfaceOptions.cs",
    "src/Hexalith.Timesheets/Runtime/EventStoreGatewayWorkloadAssertionSource.cs",
    "src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkIndexMigrationEndpoints.cs",
    "src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkConfirmationCapabilityEndpoints.cs",
    "src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs",
    "src/Hexalith.Timesheets.Contracts/Models/MagicLinks/MagicLinkCapabilityReadModelAddress.cs",
    "src/Hexalith.Timesheets.Projections/MagicLinks/IMagicLinkV1VerifiedInventoryProvider.cs",
    "src/Hexalith.Timesheets.Projections/MagicLinks/UnavailableMagicLinkV1VerifiedInventoryProvider.cs",
    "src/Hexalith.Timesheets.Projections/MagicLinks/IMagicLinkIndexMigrationAuthority.cs",
    "src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexProjectionHandler.cs",
    "src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkConfirmationCapabilityProjectionHandler.cs",
    "docs/deployed-magic-link-acceptance.md",
    "_bmad-output/implementation-artifacts/3-6-resume-checkpoint.md",
)
PLATFORM_FILES = (
    str(DECLARATION), GENERATOR,
    "src/Hexalith.Platform.Composition/PlatformDeclarationSet.cs",
    "src/Hexalith.Platform.Composition/PlatformServerBindings.cs",
    "references/Hexalith.Builds/schemas/hexalith.module-manifest.v2.json",
    "references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs",
    "_bmad-output/specs/spec-platform/SPEC.md", str(SPINE),
    "_bmad-output/planning-artifacts/epics.md", str(STATUS), ".gitmodules",
    *(f"src/Hexalith.Platform.Composition/Declarations/{owner}.json" for owner in ("eventstore", "tenants", "memories")),
    *(f"references/Hexalith.{owner}/module/hexalith.module-manifest.v2.json" for owner in ("EventStore", "Tenants", "Memories")),
)
MAX_BYTES = 2 * 1024 * 1024


class PreparationRefused(ValueError):
    """A bounded diagnostic that contains no raw source or Git output."""


def require(condition, code):
    if not condition:
        raise PreparationRefused(code)


def canonical(value):
    return (json.dumps(value, sort_keys=True, indent=2, ensure_ascii=False, allow_nan=False) + "\n").encode("utf-8")


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def read_file(root, relative):
    path = root / relative
    require(not any(p.is_symlink() for p in (path, *path.parents)), "symlink-input")
    require(path.is_file() and path.stat().st_size <= MAX_BYTES, "missing-or-oversized-input")
    with path.open("rb") as stream:
        data = stream.read(MAX_BYTES + 1)
    require(len(data) <= MAX_BYTES, "oversized-input")
    return data


def git(root, *args):
    # Fixed callers below use only local read commands; disable optional index writes,
    # fsmonitor helpers, lazy fetches, inherited Git overrides and interactive prompts.
    env = {k: v for k, v in os.environ.items() if not k.startswith("GIT_")}
    env.update(GIT_OPTIONAL_LOCKS="0", GIT_NO_LAZY_FETCH="1", GIT_TERMINAL_PROMPT="0")
    command = ["git", "-c", "core.fsmonitor=false", "-c", "core.commitGraph=false",
               "-c", "core.untrackedCache=false", "-C", str(root), *args]
    with tempfile.TemporaryFile() as stdout, tempfile.TemporaryFile() as stderr:
        try:
            result = subprocess.run(command, stdin=subprocess.DEVNULL, stdout=stdout,
                                    stderr=stderr, env=env, timeout=15, check=False)
        except (OSError, subprocess.TimeoutExpired):
            raise PreparationRefused("git-read-unavailable") from None
        require(result.returncode == 0 and stdout.tell() <= MAX_BYTES, "git-read-refused")
        stdout.seek(0)
        return stdout.read()


def repository_root(path):
    root = Path(path).absolute()
    require(not any(p.is_symlink() for p in (root, *root.parents)), "symlink-root")
    require(root.is_dir(), "missing-repository")
    require(Path(git(root, "rev-parse", "--show-toplevel").decode().strip()).resolve() == root.resolve(),
            "wrong-repository-root")
    return root.resolve()


def revision(root):
    value = git(root, "rev-parse", "HEAD").decode().strip()
    require(re.fullmatch(r"(?:[0-9a-f]{40}|[0-9a-f]{64})", value), "invalid-source-revision")
    return value


def git_metadata(root):
    paths = git(root, "rev-parse", "--path-format=absolute", "--git-dir", "--git-common-dir").decode().splitlines()
    require(len(paths) == 2 and all(Path(p).is_absolute() for p in paths), "invalid-git-metadata")
    return tuple(Path(p).resolve() for p in paths)


def source_status(root):
    return git(root, "status", "--porcelain=v1", "--untracked-files=all", "--ignore-submodules=all")


def clean_source(root):
    require(not source_status(root), "dirty-candidate-source")


def checkout_snapshot(root):
    status_bytes = source_status(root)
    commit = revision(root)
    content = {}
    # Unknown dirty files remain a blocker by status alone. Only the same
    # allowlisted source inputs used for the candidate may be read here.
    for relative in SOURCE_FILES:
        path = root / relative
        if any(p.is_symlink() for p in (path, *path.parents)):
            content[relative] = "symlink-path-unread"
        elif path.is_file():
            content[relative] = sha256(read_file(root, relative))
        else:
            content[relative] = "missing-file"
    require(revision(root) == commit and source_status(root) == status_bytes, "checkout-changed-during-observation")
    return {"revision": commit, "clean": not status_bytes,
            "statusSha256": sha256(status_bytes), "contentSha256": sha256(canonical(content))}


def source_identity(root):
    clean_source(root)
    commit = revision(root)
    hashes = {}
    for relative in SOURCE_FILES:
        data = read_file(root, relative)
        require(data == git(root, "show", f"{commit}:{relative}"), "uncommitted-source-input")
        hashes[relative] = sha256(data)
    require(hashes[OWNER_REPORT] == OWNER_REPORT_SHA256, "incompatible-dated-owner-report")
    require(revision(root) == commit, "source-changed-during-preparation")
    clean_source(root)
    return {"repository": "Hexalith.Timesheets", "revision": commit, "filesSha256": hashes,
            "consumedInputsSha256": sha256(canonical(hashes)), "clean": True,
            "cleanScope": "own-root tracked/untracked files; ignored files and nested submodule state excluded",
            "releasedImageDigest": None}


def gitlink(root, index=False):
    data = git(root, "ls-files", "--stage", "--", PIN) if index else git(root, "ls-tree", "HEAD", "--", PIN)
    pattern = rb"160000 ([0-9a-f]{40}|[0-9a-f]{64})" + (rb" 0\t" if index else rb"\t")
    if not index:
        pattern = rb"160000 commit ([0-9a-f]{40}|[0-9a-f]{64})\t"
    match = re.fullmatch(pattern + PIN.encode() + rb"\n", data)
    require(match is not None, "missing-or-conflicted-direct-gitlink")
    return match.group(1).decode()


def declaration_policy(data):
    def pairs(items):
        result = {}
        for key, value in items:
            require(key not in result, "duplicate-declaration-key")
            result[key] = value
        return result
    try:
        document = json.loads(data, object_pairs_hook=pairs)
        require(document["schema"] == "hexalith.module-manifest.v2" and len(document["modules"]) == 1,
                "candidate-schema-or-module-drift")
        module = document["modules"][0]
        require(module["identity"] == {
            "moduleId": "timesheets", "dependencies": ["eventstore", "tenants", "memories"],
            "servers": [{"id": "host", "appId": "timesheets", "resourceId": "timesheets", "enabled": True, "required": True}]},
            "candidate-server-or-dependency-drift")
        surfaces = module["surfaces"]["interfaces"]
        expected = {"confirm": ("/api/timesheets/magic-links/confirm", "public"),
                    "adjust": ("/api/timesheets/magic-links/adjust", "public"),
                    "capability-management": ("/api/timesheets/magic-links/confirmation-capabilities", "public"),
                    "metadata": ("/metadata/timesheets", "public"),
                    **{n: ("/" + n, "internal-only") for n in ("process", "replay-state", "query", "project", "admin")}}
        require(len(surfaces) == len(expected) and {s["name"] for s in surfaces} == set(expected),
                "candidate-surface-drift")
        for surface in surfaces:
            require((surface["routePrefix"], surface["exposure"]) == expected[surface["name"]]
                    and surface["server"] == "host" and surface["protocol"] == "http"
                    and surface["surfaceClass"] != "agent", "candidate-surface-boundary-drift")
            name = surface["name"]
            internal = expected[name][1] == "internal-only"
            policy = ("authenticated-workload-or-qualified-operator" if internal else
                      "magic-link-capability" if name in ("confirm", "adjust") else
                      "authenticated-owner-authorized" if name == "capability-management" else "public-metadata")
            kind = ("query" if name in ("query", "metadata") else
                    "administration" if name in ("replay-state", "admin") else "command")
            require(surface["authorization"] == {"policy": policy, "permissions": []}
                    and surface["callers"] == (["eventstore"] if internal else [])
                    and surface["operations"] == [{"name": name, "kind": kind, "authorizationPolicy": policy}],
                    "candidate-authorization-drift")
        require(module["lifecycle"]["readiness"] == [{"server": "host", "kind": "http", "endpoint": "/ready"}]
                and not module["lifecycle"]["replay"]["reopenBeforeReplay"], "candidate-lifecycle-drift")
    except (KeyError, TypeError, ValueError, RecursionError, UnicodeDecodeError):
        raise PreparationRefused("invalid-candidate-declaration") from None
    return module


def backlog(root):
    text = read_file(root, str(STATUS)).decode("utf-8")
    result = {}
    for number in BACKLOG:
        matches = re.findall(r"^  (" + re.escape(number) + r"-[a-z0-9-]+): ([a-z-]+)\s*$", text, re.MULTILINE)
        require(len(matches) == 1, "missing-or-ambiguous-hosting-backlog")
        result[number] = {"key": matches[0][0], "status": matches[0][1]}
    return result


def blockers():
    rows = (
        ("infrastructure-approval", "Platform Administrator", "Exact target, namespaces, Gateway, DNS, environment identities and private access need approval and current observation."),
        ("hosted-composition", "Platform / Builds", "Development-only composition has no Timesheets binding; staging environment/release stories 4.14–4.17 and 4.22 are unfinished."),
        ("retained-release", "Builds / Timesheets / Platform", "No qualified Timesheets Release image, owner packages, chart OCI digest, provenance, catalog identity or retained rollback package. Source hashes never substitute for image digests."),
        ("resource-binding-issuer", "EventStore / Platform identity", "Production IWorkloadAssertionIssuer with CanBindResources must sign per-request tenant/domain/actor claims accepted by the gateway; client credentials alone and development symmetric signing do not qualify."),
        ("access-and-reference-gates", "Timesheets / Tenants / Projects / Works / Parties", "DenyAllTimesheetsAuthorizationGate, DenyAllTimesheetsTenantAccessValidator, DenyAllProjectReferenceValidator, DenyAllWorkReferenceValidator and DenyAllContributorPartyValidator remain registered. Qualify real providers and negative access tests before normal commands."),
        ("certified-inventory", "Timesheets authoritative source owner", "IMagicLinkV1VerifiedInventoryProvider is unavailable. Complete cross-tenant issuance coverage, immutable source cut, authenticated receipt verification and current status/retrieval are absent."),
        ("maintenance-and-migration-authority", "Timesheets / Platform operator authority", "DenyAllMagicLinkIndexMigrationAuthority remains. Enforced writer/delivery quiescence and a separately qualified server-side migration principal are absent; proposed Administrator ownership grants no migration authority."),
        ("source-continuity", "Timesheets authoritative source owner / EventStore", "Owner-source continuity and retained pending-delivery identities/recovery must be certified for the exact legacy cut; never infer them from a Kubernetes census."),
        ("subscriber-and-shared-rebuild", "Timesheets / EventStore", "Installed subscriber delivery, shared-rebuild acceptance, full magic-link critical flow and persisted end-state proof on the exact target are absent; isolated manual delivery is insufficient."),
        ("environment-bindings", "Platform environment-layer owner", "Separate application/data namespaces, OpenBao, staging realm, Dapr trust/ACLs, durable broker, fixed timesheets-readmodels component, private 8081 networking and readiness need qualified hosted bindings."),
        ("owner-complete-declaration", "Timesheets / EventStore / Platform", "Candidate runtime.dapr/extensions and integration.topics/secrets/egress/providers are empty because qualified owner binding ports and hosted contracts are absent. Obtain an owner-complete runtime, delivery and provider declaration before enrollment; schema validity proves no installed capability."),
        ("module-owned-acceptance", "Timesheets", "Story 3.6 acceptance and release remain FAIL. Only owner-run evidence bound to the retained release and observed target can close the gate."),
    )
    return [{"id": name, "owner": owner, "status": "missing", "required": detail} for name, owner, detail in rows]


def build_plan(platform_root, source_root):
    platform = repository_root(platform_root)
    source = repository_root(source_root)
    platform_revision = revision(platform)
    inputs = {p: sha256(read_file(platform, p)) for p in PLATFORM_FILES}
    require(inputs[GENERATOR] == EXECUTING_SCRIPT_SHA256, "executing-generator-mismatch")
    declaration_policy(read_file(platform, str(DECLARATION)))
    candidate = source_identity(source)
    declared, indexed = gitlink(platform), gitlink(platform, index=True)
    direct = repository_root(platform / PIN)
    checkout = checkout_snapshot(direct)
    required = blockers()
    if candidate["revision"] != indexed or indexed != declared or checkout["revision"] != indexed:
        required.append({"id": "source-enrollment-drift", "owner": "Platform / Timesheets",
                         "status": "blocked", "required": "Candidate, declared gitlink, index gitlink and observed direct checkout differ. Resolve enrollment under a separately approved source change; preparation preserves every revision."})
    if not checkout["clean"]:
        required.append({"id": "dirty-direct-checkout", "owner": "Platform / Timesheets",
                         "status": "blocked", "required": "Observed direct Timesheets checkout contains modified or untracked bytes. Preserve them and resolve source cleanliness before enrollment; HEAD alone does not describe these bytes."})
    plan = {
        "schema": "hexalith.timesheets-enrollment-preparation.v1", "classification": "offline-candidate",
        "preparationResult": "prepared-blocked", "deploymentAuthorized": False, "activationAuthorized": False,
        "acceptance": "FAIL", "story": "Timesheets 3.6", "liveEvidenceCollected": False,
        "candidate": candidate,
        "generator": {"path": GENERATOR, "sha256": EXECUTING_SCRIPT_SHA256,
                      "snapshotSemantics": "Executing script bytes must match selected Platform input; Platform HEAD participates in identity."},
        "platform": {"revision": platform_revision, "inputsSha256": inputs,
                     "inputsArePreparationBytes": True, "activeCompositionChanged": False},
        "directSubmodule": {"path": PIN, "declaredRevision": declared, "indexRevision": indexed,
                            "observedCheckoutRevision": checkout["revision"], "observedCheckoutClean": checkout["clean"],
                            "observedCheckoutCleanScope": "own-root tracked/untracked files; ignored files and nested submodule state excluded",
                            "observedCheckoutStatusSha256": checkout["statusSha256"],
                            "observedCheckoutContentSha256": checkout["contentSha256"],
                            "observedCheckoutContentScope": "allowlisted SOURCE_FILES only; unknown dirty content is unread and uncertified",
                            "updatedByPreparation": False},
        "declaration": {"path": str(DECLARATION), "sha256": inputs[str(DECLARATION)],
                        "status": "candidate-outside-active-composition",
                        "schemaValidation": "required-separate-Builds-validator-check"},
        "proposedTarget": {"status": "proposed-unapproved-unobserved", "environment": "staging",
                           "clusterContext": "jpiquot@local", "clusterAddress": "192.168.1.30",
                           "clusterApi": "https://192.168.1.30:6443",
                           "applicationNamespace": "hexalith-staging", "dataNamespace": "hexalith-staging-data",
                           "publicHostname": "timesheets.hexalith.com", "publicOrigin": "https://timesheets.hexalith.com",
                           "gateway": {"api": "Gateway API", "namespace": "hexalith-staging-data",
                                       "name": "hexalith-staging", "routeNamespace": "hexalith-staging",
                                       "crossNamespaceParentReference": True}},
        "expectedClusterEvidence": {"kubeSystemNamespaceUid": "b5860674-1d1e-4a3b-b413-e1bb3b9a4557",
                                    "observedAt": "2026-10-13", "source": "docs/deployed-magic-link-acceptance.md",
                                    "sourceSha256": OWNER_REPORT_SHA256,
                                    "scope": "dated-owner-source-report", "freshNativeBindingRequired": True},
        "publicRouteAllowlist": [{"path": "/api/timesheets/magic-links/confirm", "match": "Exact"},
                                 {"path": "/api/timesheets/magic-links/confirm/submit", "match": "Exact"},
                                 {"path": "/api/timesheets/magic-links/adjust", "match": "Exact"},
                                 {"path": "/api/timesheets/magic-links/adjust/submit", "match": "Exact"},
                                 {"path": "/api/timesheets/magic-links/confirmation-capabilities", "match": "PathPrefix"},
                                 {"path": "/metadata/timesheets", "match": "Exact"}],
        "forbiddenPublicRoutePrefixes": ["/", "/process", "/project", "/query", "/replay-state", "/admin"],
        "listeners": {"public": {"port": 8080, "service": "timesheets-public", "exposure": "public"},
                      "internal": {"port": 8081, "service": "timesheets-internal", "exposure": "private-only",
                                   "endpoint": "http://timesheets-internal.hexalith-staging.svc.cluster.local:8081",
                                   "routePublished": False},
                      "configuration": {"Timesheets:InternalSurface:Port": 8081,
                                        "Timesheets:InternalSurface:AllowOnAnyPort": False}},
        "ownership": {"administrator": "Jérôme Piquot", "operator": "Jérôme Piquot",
                      "status": "proposed", "migrationPrincipalGranted": False,
                      "nativeContextConfersDomainAuthority": False},
        "authentication": {"mode": "production-grade-asymmetric-oidc-and-bound-workload-assertions",
                           "realm": "staging", "qualified": False,
                           "developmentSymmetricSigningAllowed": False, "fixtureAccessAllowed": False},
        "componentBindings": [{"name": "timesheets-readmodels", "recoveryClass": "rebuild-only",
                               "nameBinding": "module-hardcoded-literal", "configurationKey": None,
                               "qualifiedHostedBinding": False,
                               "note": "No invented rename configuration key. Scope this exact component to the staging Timesheets sidecar; durable shared-rebuild transport/coordination must preserve owner continuity."}],
        "retainedRelease": {"configuration": "Release", "proposedImageRepository": "registry.hexalith.com/timesheets",
                            "imageDigest": None, "chartOciDigest": None,
                            "packageIdentities": [], "provenance": None, "rollbackPackageDigest": None},
        "hostingBacklog": backlog(platform), "prerequisites": required,
        "approvalBoundary": "Review this preparation before requesting infrastructure approval. No approval, receipt, grant or gate override is accepted by this tool. Approval of infrastructure does not establish provider authority or module acceptance.",
        "executionSequence": ["Approve the exact proposed infrastructure and bind current observed identities.",
                              "Complete and qualify environment-layer/hosted composition and owner operational providers.",
                              "Resolve source enrollment drift by separate authorization; produce and retain digest-pinned Release artifacts and provenance.",
                              "Validate and dry-run the retained application package with private 8081 and explicit public path allowlist; qualify access denials.",
                              "Deploy only through the approved Platform release procedure after its gates pass; retain baseline and recovery point.",
                              "Run Timesheets-owned deployed acceptance with certified inventory, enforced maintenance, owner continuity and subscriber/shared-rebuild proof before reopening acceptance."],
        "rollbackSequence": ["Stop the failed attempt, retain diagnostics and close its user ingress.",
                             "If qualified compatible rollback exists, restore the retained application package once and verify readiness/smokes.",
                             "Never rewind legacy data, events, revocations, credentials or index state with application rollback.",
                             "For incompatible retained-data state, use the approved named data-restore recovery under owner control; never clear or migrate legacy data from preparation."],
    }
    require(revision(platform) == platform_revision and gitlink(platform) == declared
            and gitlink(platform, index=True) == indexed and checkout_snapshot(direct) == checkout,
            "platform-identity-changed-during-preparation")
    require(all(sha256(read_file(platform, p)) == value for p, value in inputs.items()),
            "platform-input-changed-during-preparation")
    require(source_identity(source) == candidate, "source-changed-during-preparation")
    require(sha256(read_file(HERE, SCRIPT.name)) == EXECUTING_SCRIPT_SHA256, "executing-generator-changed")
    plan["preparationSha256"] = sha256(canonical(plan))
    return plan


@contextmanager
def output_directory(destination):
    """Retain every directory descriptor; never traverse an output symlink."""
    chain = []
    flags = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC
    try:
        chain.append((os.open(destination.anchor, flags), None, None))
        for name in destination.parent.parts[1:]:
            parent = chain[-1][0]
            chain.append((os.open(name, flags, dir_fd=parent), parent, name))
        yield chain[-1][0], chain
    except OSError:
        raise PreparationRefused("output-directory-unavailable") from None
    finally:
        for fd, _, _ in reversed(chain):
            os.close(fd)


def file_identity(value):
    return value.st_dev, value.st_ino


def unchanged_directory_chain(chain):
    try:
        for fd, parent, name in chain[1:]:
            observed = os.stat(name, dir_fd=parent, follow_symlinks=False)
            require(stat.S_ISDIR(observed.st_mode) and file_identity(observed) == file_identity(os.fstat(fd)),
                    "output-directory-changed")
    except OSError:
        raise PreparationRefused("output-directory-changed") from None


def cleanup_owned_temp(directory, name, identity):
    try:
        observed = os.stat(name, dir_fd=directory, follow_symlinks=False)
    except FileNotFoundError:
        return
    if file_identity(observed) == identity:
        os.unlink(name, dir_fd=directory)


def publish_new(directory, chain, name, data):
    temporary = ".timesheets-plan-" + secrets.token_hex(16) + ".tmp"
    owned = None
    try:
        fd = os.open(temporary, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC,
                     0o600, dir_fd=directory)
        owned = file_identity(os.fstat(fd))
        try:
            stream = os.fdopen(fd, "wb")
        except BaseException:
            os.close(fd)
            raise
        with stream:
            require(stream.write(data) == len(data), "incomplete-output-write")
            stream.flush()
            os.fsync(stream.fileno())
            os.fchmod(stream.fileno(), 0o644)
        # Close succeeded before publication. A replaced ancestor cannot redirect
        # either the temporary write or the exclusive hard-link to another tree.
        unchanged_directory_chain(chain)
        require(file_identity(os.stat(temporary, dir_fd=directory, follow_symlinks=False)) == owned,
                "owned-output-temp-changed")
        try:
            os.link(temporary, name, src_dir_fd=directory, dst_dir_fd=directory, follow_symlinks=False)
        except FileExistsError:
            raise PreparationRefused("output-already-exists") from None
    except OSError:
        raise PreparationRefused("output-publication-failed") from None
    finally:
        if owned is not None:
            cleanup_owned_temp(directory, temporary, owned)


def prepare(platform_root, source_root, output):
    # Validate the caller's original roots before resolution can hide aliases.
    platform = repository_root(platform_root)
    source = repository_root(source_root)
    supplied = Path(output).absolute()
    require(not any(p.is_symlink() for p in (supplied, *supplied.parents)), "symlink-output")
    destination = Path(os.path.abspath(supplied))
    metadata = (git_metadata(platform), git_metadata(source))
    protected = (source, platform / "references", platform / ".git", *metadata[0], *metadata[1])
    require(not any(destination.is_relative_to(path) for path in protected), "output-inside-protected-tree")
    with output_directory(destination) as (directory, chain):
        try:
            os.stat(destination.name, dir_fd=directory, follow_symlinks=False)
        except FileNotFoundError:
            pass
        else:
            raise PreparationRefused("output-already-exists")
        plan = build_plan(platform, source)
        require((git_metadata(platform), git_metadata(source)) == metadata, "git-metadata-changed")
        unchanged_directory_chain(chain)
        publish_new(directory, chain, destination.name, canonical(plan))
    return plan


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--platform-root", type=Path, default=HERE.parents[1])
    parser.add_argument("--source-root", type=Path, required=True, help="Clean Timesheets repository root")
    parser.add_argument("--output", type=Path, required=True, help="New local plan file; existing destinations refused")
    args = parser.parse_args(argv)
    try:
        plan = prepare(args.platform_root, args.source_root, args.output)
    except (PreparationRefused, OSError, UnicodeError):
        print("Preparation refused: invalid, changed or unavailable input/output. No deployment was performed.")
        return 1
    print(f"Prepared blocked plan {plan['preparationSha256']}; deployment unauthorized; acceptance FAIL.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
