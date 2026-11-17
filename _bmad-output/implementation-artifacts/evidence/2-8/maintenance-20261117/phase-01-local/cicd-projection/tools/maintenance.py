#!/usr/bin/env python3
"""Render the two explicitly approved maintenance lanes from committed CiCd policy."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import stat
import subprocess

POLICY = "policies/maintenance/story-2-8.json"
OWNER = "https://github.com/Hexalith/Hexalith.CiCd.git"
REPOSITORIES = {"Hexalith/Hexalith.Builds", "Hexalith/Hexalith.Platform"}
REVISION = re.compile(r"[0-9a-f]{40}")


class MaintenanceError(ValueError):
    """Policy, source or owned content is unavailable or has changed."""


def git(repository, *arguments):
    result = subprocess.run(["git", "-c", "submodule.recurse=false", "-C", str(repository), *arguments],
                            capture_output=True, timeout=30, check=False)
    if result.returncode:
        raise MaintenanceError("required immutable Git input is unavailable")
    return result.stdout


def digest(value):
    return hashlib.sha256(value).hexdigest()


def path(root, relative):
    selected = PurePosixPath(relative)
    if selected.is_absolute() or ".." in selected.parts or not selected.parts:
        raise MaintenanceError("unsafe owned path")
    for ancestor in (root, *root.parents):
        if ancestor.is_symlink():
            raise MaintenanceError("owned roots cannot traverse symlinks")
    target = root
    for part in selected.parts:
        target /= part
        if target.is_symlink():
            raise MaintenanceError("owned paths cannot traverse symlinks")
    return target


def origin(repository, expected):
    actual = git(repository, "remote", "get-url", "origin").decode().strip()
    if actual.removesuffix(".git") != expected.removesuffix(".git"):
        raise MaintenanceError("foreign owner or target origin")


def select(policy, repository, role):
    if policy.get("schema") != "hexalith.cicd.maintenance-lanes.v1" or set(policy.get("lanes", {})) != REPOSITORIES:
        raise MaintenanceError("unknown maintenance policy")
    if repository not in REPOSITORIES or role not in {"maintenance", "registration"}:
        raise MaintenanceError("unapproved repository or delivery role")
    lane = policy["lanes"][repository]
    expected = "4.44.x" if repository.endswith("Builds") else "1.7.x"
    if lane.get("branch") != expected or lane.get("releaseRange") != expected:
        raise MaintenanceError("maintenance scope is not the approved exact line")
    profile = policy["ownedOutputs"][repository][role]
    if not REVISION.fullmatch(profile.get("baseRevision", "")) or not profile.get("files"):
        raise MaintenanceError("maintenance source or owned outputs are missing")
    if role == "maintenance" and profile["baseRevision"] != lane["baseRevision"]:
        raise MaintenanceError("maintenance source differs from the frozen lane")
    return lane, profile


def production_branch(api, policy, *, apply=False):
    """Add only 4.44.x to the existing environment; retain every reviewer/rule."""
    lane, _ = select(policy, "Hexalith/Hexalith.Builds", "maintenance")
    if (lane.get("requiredEnvironment") != "production"
            or policy.get("environmentProposal", {}).get("request") != {"name": "4.44.x", "type": "branch"}):
        raise MaintenanceError("the committed policy does not authorize this exact environment addition")
    endpoint = "repos/Hexalith/Hexalith.Builds/environments/production"
    environment = api("GET", endpoint)
    rules = environment.get("protection_rules")
    if (not isinstance(rules, list)
            or not any(rule.get("type") == "required_reviewers" and rule.get("reviewers") for rule in rules)
            or environment.get("deployment_branch_policy") != {"protected_branches": False, "custom_branch_policies": True}):
        raise MaintenanceError("the existing production reviewer/branch policy must remain enabled")
    branch_endpoint = endpoint + "/deployment-branch-policies"
    before = api("GET", branch_endpoint)
    policies = before.get("branch_policies")
    if (not isinstance(policies, list) or before.get("total_count") != len(policies)
            or not 0 < len(policies) < 100 or not any(item.get("name") == "main" and item.get("type") == "branch" for item in policies)):
        raise MaintenanceError("production branch-policy discovery is incomplete")
    requested = {"name": "4.44.x", "type": "branch"}
    existing = [item for item in policies if item.get("name") == "4.44.x"]
    if existing and (len(existing) != 1 or existing[0].get("type") != "branch"):
        raise MaintenanceError("a different maintenance environment policy already exists")
    receipt = {"environment": "production", "requested": requested, "applied": False,
               "existingProtectionRulesSha256": digest(json.dumps(rules, sort_keys=True).encode()),
               "reviewersUnchanged": True, "normalNugetLoginRequired": True}
    if not apply or existing:
        return receipt
    # One request, no retry or reviewer/protection setting mutation.
    created = api("POST", branch_endpoint, requested)
    if created.get("name") != "4.44.x" or created.get("type") != "branch" or type(created.get("id")) is not int:
        raise MaintenanceError("maintenance environment policy creation was not authenticated")
    after_environment = api("GET", endpoint)
    after = api("GET", branch_endpoint)
    if (after_environment.get("protection_rules") != rules
            or after_environment.get("deployment_branch_policy") != environment["deployment_branch_policy"]
            or after_environment.get("can_admins_bypass") != environment.get("can_admins_bypass")
            or after.get("total_count") != len(policies) + 1
            or not all(item in after.get("branch_policies", []) for item in policies)
            or created not in after.get("branch_policies", [])):
        raise MaintenanceError("production policy changed unexpectedly; retain the primary result")
    receipt.update(applied=True, createdPolicyId=created["id"])
    return receipt


def render(owner, owner_revision, target, target_revision, repository, role, output, *, apply=False):
    """Authenticate owner/source ancestry before producing or applying any owned file."""
    for value in (owner_revision, target_revision):
        if not REVISION.fullmatch(value):
            raise MaintenanceError("full immutable Git revisions are required")
    origin(owner, OWNER)
    origin(target, f"https://github.com/{repository}.git")
    if git(target, "rev-parse", "HEAD").decode().strip() != target_revision:
        raise MaintenanceError("target checkout differs from the selected source")
    implementation = git(owner, "show", owner_revision + ":tools/maintenance.py")
    if implementation != Path(__file__).read_bytes():
        raise MaintenanceError("executing maintenance tool differs from its committed owner revision")
    policy_bytes = git(owner, "show", owner_revision + ":" + POLICY)
    policy = json.loads(policy_bytes)
    lane, profile = select(policy, repository, role)
    git(target, "merge-base", "--is-ancestor", profile["baseRevision"], target_revision)
    branch = subprocess.run(["git", "-C", str(target), "symbolic-ref", "--quiet", "--short", "HEAD"],
                            capture_output=True, timeout=30, check=False)
    actual_branch = branch.stdout.decode().strip() if branch.returncode == 0 else None
    if apply and role == "maintenance" and actual_branch != lane["branch"]:
        raise MaintenanceError("maintenance apply requires the actual approved named branch")
    if role == "registration" and git(target, "rev-parse", "refs/remotes/origin/main").decode().strip() != target_revision:
        raise MaintenanceError("registration delivery requires the freshly selected current main")
    desired = {}
    owned_paths = list(profile["files"])
    if apply and git(target, "status", "--porcelain=v1", "-z", "--untracked-files=all", "--", *owned_paths):
        raise MaintenanceError("owned target paths have staged, unstaged, deleted or untracked changes")
    for relative, entry in profile["files"].items():
        if not relative.startswith(".github/workflows/") or not relative.endswith(".yml"):
            raise MaintenanceError("policy attempts to write outside its exact owned workflow scope")
        content = git(owner, "show", owner_revision + ":" + entry["template"])
        if digest(content) != entry["sha256"]:
            raise MaintenanceError("committed owned template differs from the policy hash")
        current = subprocess.run(["git", "-C", str(target), "show", target_revision + ":" + relative],
                                 capture_output=True, timeout=30, check=False)
        actual = digest(current.stdout) if current.returncode == 0 else None
        if actual not in {entry["baseSha256"], entry["sha256"]}:
            raise MaintenanceError("incoming owned workflow changed; update the owning policy first")
        selected = path(target, relative)
        if apply and selected.exists():
            if current.returncode != 0 or not stat.S_ISREG(selected.stat().st_mode) or selected.stat().st_mode & 0o111:
                raise MaintenanceError("owned target path is untracked, nonregular or has a changed mode")
            if selected.read_bytes() != current.stdout:
                raise MaintenanceError("uncommitted target workflow would be overwritten")
        desired[relative] = content
    receipt = {"schema": "hexalith.cicd.maintenance-delivery.v1", "ownerSource": owner_revision,
               "ownerPolicyPath": POLICY, "ownerPolicySha256": digest(policy_bytes),
               "repository": repository, "role": role, "targetSource": target_revision,
               "branch": actual_branch, "intendedBranch": lane["branch"] if role == "maintenance" else "main",
               "detachedProposal": actual_branch is None, "base": profile["baseRevision"],
               "ownedFiles": {name: digest(value) for name, value in desired.items()},
               "normalSourceAndPublicationChecksRequired": True, "runtimeOrCandidateAcceptance": False}
    receipt_bytes = (json.dumps(receipt, indent=2) + "\n").encode()
    writes = [(path(target if apply else output, relative), content) for relative, content in desired.items()]
    receipt_path = path(output, "maintenance-delivery.json")
    # Validate the complete output plan, including the receipt, before the first write.
    for selected, content in writes + [(receipt_path, receipt_bytes)]:
        if selected.exists() and not apply and selected.read_bytes() != content:
            raise MaintenanceError("an unrelated preparation output would be overwritten")
    if receipt_path.exists() and receipt_path.read_bytes() != receipt_bytes:
        raise MaintenanceError("an unrelated delivery receipt would be overwritten")
    if git(target, "rev-parse", "HEAD").decode().strip() != target_revision:
        raise MaintenanceError("target source changed before owned delivery")
    # No writes occur until every owner/source/path/hash/status/output check has succeeded.
    for selected, content in writes + [(receipt_path, receipt_bytes)]:
        selected.parent.mkdir(parents=True, exist_ok=True)
        selected.write_bytes(content)
        selected.chmod(0o644)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for option in ("owner", "owner-revision", "target", "target-revision", "repository", "role", "output"):
        parser.add_argument("--" + option, required=True)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    try:
        render(Path(args.owner), args.owner_revision, Path(args.target), args.target_revision,
               args.repository, args.role, Path(args.output), apply=args.apply)
    except (OSError, ValueError, KeyError, subprocess.SubprocessError) as error:
        parser.exit(1, f"maintenance delivery refused: {type(error).__name__}\n")


if __name__ == "__main__":
    main()
