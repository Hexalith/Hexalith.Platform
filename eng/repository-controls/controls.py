#!/usr/bin/env python3
"""Read-only observations, reviewable proposals and fail-closed policy checks.

No command in this module applies changes. Discovery requires explicit opt-in;
the other commands operate exclusively on local, sanitized JSON records.
"""

import argparse
import base64
import copy
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import subprocess
from urllib.parse import quote, urlparse


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_POLICY = Path(__file__).with_name("desired-state.json")
RULESET_FIELDS = ("name", "target", "enforcement", "bypass_actors", "conditions", "rules")
SAFE_FIELDS = set("""
id login type full_name name private visibility archived default_branch size plan
default_repository_permission default_workflow_permissions can_approve_pull_request_reviews
permissions permission role_name owner user members_count slug parent
target enforcement source source_type ruleset_id ruleset_source ruleset_source_type
bypass_actors actor_id actor_type bypass_mode conditions ref_name include exclude
rules parameters required_approving_review_count require_code_owner_review
dismiss_stale_reviews_on_push require_last_push_approval required_review_thread_resolution
allowed_merge_methods dismissal_restriction allowed_actors enabled
require_extra_approval_for_unattributed_changes required_reviewers
do_not_enforce_on_create required_status_checks context integration_id
strict_required_status_checks_policy required_deployments required_environments
required_linear_history required_signatures restrict_pushes update_allows_fetch_and_merge
created_at updated_at current_user_can_bypass app_id app_slug repository_selection
installations repositories total_count suspended_at target_type read_only verified
sha ref object tree path mode truncated encoding content errors line column
repository_ids repository_id repositories_url token_expired expires_at
""".split())
TOKEN_PATTERN = re.compile(r"(?:gh[pousr]_[A-Za-z0-9_]{12,}|github_pat_[A-Za-z0-9_]{12,}|-----BEGIN [^-]*PRIVATE KEY-----)")


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode()


def digest(value):
    return hashlib.sha256(canonical(value)).hexdigest()


def load(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("duplicate-json-key")
            result[key] = value
        return result
    return json.loads(Path(path).read_text(), object_pairs_hook=unique)


def timestamp():
    return datetime.now(timezone.utc).isoformat()


def parse_time(value):
    result = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if result.tzinfo is None:
        raise ValueError("timezone-required")
    return result


def fresh(value, now, max_age):
    try:
        age = (now - parse_time(value)).total_seconds()
        return 0 <= age <= max_age
    except (ValueError, TypeError, AttributeError):
        return False


def sanitize(value, field=None):
    """Keep only policy metadata. Never retain keys, tokens or API error text."""
    if field == "permissions" and isinstance(value, dict):
        return {k: sanitize(v, "permissions") if isinstance(v, dict) else v
                for k, v in value.items() if re.fullmatch(r"[a-z_]+", k) and
                (isinstance(v, dict) or type(v) is bool or v in ("read", "write", "none"))}
    if isinstance(value, dict):
        return {k: sanitize(v, k) for k, v in value.items() if k in SAFE_FIELDS and k != "content"}
    if isinstance(value, list):
        return [sanitize(v, field) for v in value]
    if isinstance(value, str) and TOKEN_PATTERN.search(value):
        return "[redacted]"
    return value


def workflow_audit(content):
    """Conservative YAML subset; uncertain syntax requires manual review.

    No YAML parser is bundled. Aliases, flow/multiline ambiguity and external
    credentials fail closed; workflow bytes never enter retained discovery.
    """
    problems = set()
    if TOKEN_PATTERN.search(content):
        problems.add("embedded-credential")
    if re.search(r"secrets\s*(?:\.|\[|:)", content, re.I):
        # Even a read-only GITHUB_TOKEN should be used as github.token; unknown
        # secrets can carry an owner's PAT irrespective of token defaults.
        problems.add("external-credential-reference")
    if re.search(r"(?:^|\s)[&*][A-Za-z_]|<<\s*:|!(?:!|<|[A-Za-z])|^---|^\.\.\.", content, re.M):
        problems.add("unsupported-yaml-permissions-syntax")
    if re.search(r"^\s*[?:]\s|\\(?:[uUx][0-9a-fA-F]|\r?\n)", content, re.M):
        problems.add("unsupported-yaml-permissions-syntax")
    if re.search(r'''[\s:{,]\s*['"]?write(?:-all)?['"]?(?:\s|[,}\]]|$)''', content):
        problems.add("writable-workflow-permissions")
    permission_indent = None
    for raw in content.splitlines():
        line = raw.split("#", 1)[0].rstrip()
        if not line.strip():
            continue
        indent = len(line) - len(line.lstrip(" "))
        if "\t" in line[:indent + 1]:
            problems.add("unsupported-yaml-permissions-syntax")
        match = re.match(r'''^\s*(?:permissions|"permissions"|'permissions')\s*:\s*(.*?)\s*$''', line)
        if match:
            value = match.group(1)
            permission_indent = indent if not value else None
            if value in ("{}", "read-all", "'read-all'", '"read-all"'):
                continue
            if value:
                if "write" in value.lower():
                    problems.add("writable-workflow-permissions")
                else:
                    problems.add("unsupported-yaml-permissions-syntax")
            continue
        if re.search(r"permissions\s*[\"']?\s*:", line, re.I):
            problems.add("unsupported-yaml-permissions-syntax")
        if re.search(r'''["'][^"']*\\[^"']*["']\s*:''', line):
            problems.add("unsupported-yaml-permissions-syntax")
        if permission_indent is not None:
            if indent <= permission_indent:
                permission_indent = None
            else:
                item = re.fullmatch(r'''\s*([a-z][a-z_-]*)\s*:\s*['"]?(read|none)['"]?\s*''', line)
                if not item:
                    problems.add("writable-workflow-permissions" if "write" in line.lower()
                                 else "unsupported-yaml-permissions-syntax")
    return {"result": "pass" if not problems else "blocked", "problems": sorted(problems),
            "content_sha256": hashlib.sha256(content.encode()).hexdigest(),
            "auditor": "conservative-standard-library-v1"}


def codeowners_audit(content, policy):
    lines = [line.strip() for line in content.splitlines() if line.strip() and not line.lstrip().startswith("#")]
    return {"result": "pass" if lines == policy["platform"]["codeowners"] else "fail",
            "content_sha256": hashlib.sha256(content.encode()).hexdigest()}


def gh_get(endpoint):
    """GET only; auth stays in gh's credential store, never in arguments/output."""
    parsed = urlparse(endpoint)
    if parsed.scheme != "https" or parsed.netloc != "api.github.com" or parsed.fragment or parsed.username:
        raise ValueError("github-api-endpoint-required")
    path = parsed.path.lstrip("/") + ("?" + parsed.query if parsed.query else "")
    result = subprocess.run(["gh", "api", "--method", "GET", "--include", path,
                             "-H", "Accept: application/vnd.github+json", "-H", "X-GitHub-Api-Version: 2022-11-28"],
                            capture_output=True, text=True, timeout=60)
    # gh prints headers and a JSON body with --include. Ignore stderr entirely.
    raw = result.stdout.replace("\r\n", "\n")
    header, separator, body = raw.partition("\n\n")
    match = re.match(r"HTTP/\S+\s+(\d+)", header)
    status = int(match.group(1)) if match else 0
    headers = {}
    for line in header.splitlines()[1:]:
        key, colon, value = line.partition(":")
        if colon:
            headers[key.lower()] = value.strip()
    try:
        data = json.loads(body) if separator else None
    except ValueError:
        data = None
    return status, headers, data


class Collector:
    def __init__(self, policy, transport=gh_get):
        self.policy = policy
        self.transport = transport
        self.observations = []

    def get(self, name, path, list_key=None, content_kind=None):
        endpoint = "https://api.github.com/" + path.lstrip("/")
        first_endpoint = endpoint
        pages, seen, aggregate, error, complete = [], set(), None, None, False
        while endpoint and len(pages) < 1000:
            if endpoint in seen:
                error = {"condition": "pagination-cycle", "status": 0}
                break
            seen.add(endpoint)
            try:
                status, headers, raw = self.transport(endpoint)
            except (OSError, ValueError, subprocess.SubprocessError):
                status, headers, raw = 0, {}, None
            page = {"endpoint": endpoint, "status": status, "observed_at_utc": timestamp()}
            pages.append(page)
            if status != 200 or raw is None:
                error = {"condition": "api-observation-unavailable", "status": status}
                break
            if content_kind:
                try:
                    content = base64.b64decode(raw["content"], validate=False).decode("utf-8")
                    audit = (codeowners_audit(content, self.policy) if content_kind == "codeowners"
                             else workflow_audit(content))
                    data = {"path": raw.get("path"), "sha": raw.get("sha"), "content_observed": True,
                            "audit": audit}
                except (KeyError, ValueError, UnicodeError, TypeError):
                    error = {"condition": "content-observation-unavailable", "status": status}
                    break
            else:
                data = sanitize(raw)
                # Replacing a ruleset from a projection must never remove an
                # unfamiliar parameter/rule. Preserve the error, not a PUT.
                if isinstance(raw, dict) and "rules" in raw and any(data.get(k) != raw.get(k) for k in RULESET_FIELDS):
                    error = {"condition": "lossy-ruleset-projection", "status": status}
                    break
            items = data.get(list_key) if list_key and isinstance(data, dict) else data
            if list_key and not isinstance(items, list):
                error = {"condition": "malformed-paginated-result", "status": status}
                break
            if aggregate is None:
                aggregate = copy.deepcopy(data)
            elif list_key:
                aggregate[list_key].extend(items)
            elif isinstance(data, list) and isinstance(aggregate, list):
                aggregate.extend(data)
            else:
                error = {"condition": "unexpected-pagination", "status": status}
                break
            link = headers.get("link", headers.get("Link", ""))
            next_match = re.search(r'<([^>]+)>;\s*rel="next"', link)
            endpoint = next_match.group(1) if next_match else None
            page["next_endpoint"] = endpoint
            if endpoint:
                parsed = urlparse(endpoint)
                if parsed.scheme != "https" or parsed.netloc != "api.github.com" or parsed.username:
                    error = {"condition": "unsafe-pagination-link", "status": status}
                    break
            else:
                complete = True
        if not complete and error is None:
            error = {"condition": "pagination-limit", "status": 0}
        if list_key and isinstance(aggregate, dict) and "total_count" in aggregate:
            if aggregate["total_count"] != len(aggregate[list_key]):
                complete = False
                error = {"condition": "pagination-total-mismatch", "status": 200}
        items = aggregate.get(list_key) if list_key and isinstance(aggregate, dict) else aggregate
        if isinstance(items, list) and items:
            for key in ("id", "ref", "path"):
                if all(isinstance(v, dict) and key in v for v in items):
                    if len({v[key] for v in items}) != len(items):
                        complete = False
                        error = {"condition": "duplicate-paginated-result", "status": 200}
                    break
        if isinstance(aggregate, dict) and aggregate.get("truncated"):
            complete = False
            error = {"condition": "truncated-tree", "status": 200}
        observation = {"name": name, "endpoint": first_endpoint, "method": "GET", "data": aggregate,
                       "exit_code": 0 if complete and not error else 1,
                       "observed_at_utc": timestamp(),
                       "pagination": {"complete": complete, "page_count": len(pages), "pages": pages}}
        if error:
            observation["error"] = error
        self.observations.append(observation)
        return aggregate if complete and not error else None


def resolved_ref_sha(ref, read_tag):
    """Peel annotated tags, including nested tags; ambiguous objects fail closed."""
    obj, seen = ref.get("object", {}), set()
    while obj.get("type") == "tag":
        sha = obj.get("sha")
        if not isinstance(sha, str) or sha in seen or len(seen) >= 20:
            return None
        seen.add(sha)
        tag = read_tag(sha)
        if not isinstance(tag, dict):
            return None
        obj = tag.get("object", {})
    return obj.get("sha") if obj.get("type") in ("commit", "tree") else None


def discover(policy, transport=gh_get):
    started = timestamp()
    collector = Collector(policy, transport)
    org = policy["organization"]["login"]
    user = collector.get("current_user", "user") or {}
    collector.get("organization", f"orgs/{org}")
    owners = collector.get("organization_owners", f"orgs/{org}/members?role=admin&per_page=100") or []
    members = collector.get("organization_members", f"orgs/{org}/members?per_page=100") or []
    outside = collector.get("organization_outside_collaborators", f"orgs/{org}/outside_collaborators?per_page=100") or []
    teams = collector.get("organization_teams", f"orgs/{org}/teams?per_page=100") or []
    team_members = []
    for team in teams:
        team_members.extend(collector.get(f"team.{team['id']}.members",
                            f"orgs/{org}/teams/{quote(team['slug'], safe='')}/members?per_page=100") or [])
    collector.get("organization_repositories", f"orgs/{org}/repos?type=all&per_page=100")
    collector.get("organization_rulesets", f"orgs/{org}/rulesets?per_page=100")
    installations = collector.get("organization_installations", f"orgs/{org}/installations?per_page=100", "installations") or {}
    for app in installations.get("installations", []):
        collector.get(f"installation.{app['id']}.repositories",
                      f"user/installations/{app['id']}/repositories?per_page=100", "repositories")
    collector.get("fine_grained_pat_grants", f"orgs/{org}/personal-access-tokens?per_page=100")
    collector.get("fine_grained_pat_requests", f"orgs/{org}/personal-access-token-requests?per_page=100")
    for observation in list(collector.observations):
        if observation["name"] in ("fine_grained_pat_grants", "fine_grained_pat_requests"):
            for grant in observation.get("data") or []:
                if isinstance(grant, dict) and "id" in grant:
                    request = observation["name"] == "fine_grained_pat_requests"
                    collector.get(f"pat.{'request' if request else 'grant'}.{grant['id']}.repositories",
                                  f"orgs/{org}/{'personal-access-token-requests' if request else 'personal-access-tokens'}/{grant['id']}/repositories?per_page=100")
    for repo in [policy["platform"], policy["builds"], *policy["private_repositories"]]:
        name, full = repo["name"], repo["full_name"]
        metadata = collector.get(f"{name}.repository", f"repos/{full}")
        if metadata is None:
            continue
        rulesets = collector.get(f"{name}.rulesets", f"repos/{full}/rulesets?includes_parents=true&per_page=100") or []
        for rule in rulesets:
            detail_name = ("Builds.main_ruleset_detail" if name == policy["builds"]["name"] and rule["id"] == policy["builds"]["ruleset_id"]
                           else f"{name}.ruleset.{rule['id']}")
            collector.get(detail_name, f"repos/{full}/rulesets/{rule['id']}")
        if name == policy["platform"]["name"]:
            collector.get(f"{name}.main_effective_rules", f"repos/{full}/rules/branches/main")
            collector.get(f"{name}.codeowners", f"repos/{full}/contents/.github/CODEOWNERS?ref=main", content_kind="codeowners")
            collector.get(f"{name}.codeowners_errors", f"repos/{full}/codeowners/errors?ref=main")
        collector.get(f"{name}.workflow_permissions", f"repos/{full}/actions/permissions/workflow")
        collaborators = collector.get(f"{name}.collaborators", f"repos/{full}/collaborators?affiliation=all&per_page=100") or []
        collector.get(f"{name}.teams", f"repos/{full}/teams?per_page=100")
        collector.get(f"{name}.deploy_keys", f"repos/{full}/keys?per_page=100")
        actors = {actor["id"]: actor for actor in [*owners, *members, *outside, *team_members, *collaborators]}
        for actor_id, actor in sorted(actors.items()):
            collector.get(f"{name}.permission.{actor_id}",
                          f"repos/{full}/collaborators/{quote(actor['login'], safe='')}/permission")
        if repo not in policy["private_repositories"]:
            continue
        # Every retained branch/tag tree is inspected, not just the default branch.
        refs = []
        for kind in ("heads", "tags"):
            refs.extend(collector.get(f"{name}.refs.{kind}",
                        f"repos/{full}/git/matching-refs/{kind}/?per_page=100") or [])
        tag_cache = {}

        def read_tag(sha):
            if sha not in tag_cache:
                tag_cache[sha] = collector.get(f"{name}.tag.{sha}", f"repos/{full}/git/tags/{sha}")
            return tag_cache[sha]

        tree_shas = {resolved_ref_sha(ref, read_tag) for ref in refs}
        for sha in sorted(tree_shas - {None}):
            tree = collector.get(f"{name}.tree.{sha}", f"repos/{full}/git/trees/{sha}?recursive=1") or {}
            for entry in tree.get("tree", []):
                path = entry.get("path", "")
                if path.startswith(".github/workflows/") and path.endswith((".yml", ".yaml")):
                    collector.get(f"{name}.workflow.{sha}.{path}",
                                  f"repos/{full}/contents/{quote(path, safe='/')}?ref={sha}", content_kind="workflow")
    return {"schema_version": 2, "story": "4.4", "root_repository": policy["platform"]["full_name"],
            "authenticated_actor": user.get("login"), "collection_mode": "GitHub REST GET only",
            "started_at_utc": started, "captured_at_utc": timestamp(),
            "sanitization": "Allowlisted metadata and content hashes/audits only; no credentials or workflow source.",
            "observations": collector.observations,
            "limits": ["Owner device and classic PAT custody require separate owner evidence.",
                       "Organization rulesets may be unavailable on Free; inherited repository rules still require complete readback.",
                       "An API error or 404 is never evidence of absence.",
                       "GET only; no mutation or operational acceptance."]}


def index(discovery):
    observations = discovery.get("observations", [])
    result = {item["name"]: item for item in observations}
    if len(result) != len(observations):
        raise ValueError("duplicate-observation-name")
    return result


def state_hash(observation):
    # Observation timestamps and page response headers change on every GET;
    # policy state (including a missing or failed observation) must not change.
    return digest({"data": observation.get("data"), "exit_code": observation.get("exit_code"),
                   "status": observation.get("error", {}).get("status")})


def ruleset_payload(detail):
    if not all(key in detail for key in RULESET_FIELDS):
        raise ValueError("incomplete-ruleset-detail")
    return {key: copy.deepcopy(detail[key]) for key in RULESET_FIELDS}


def propose(discovery, policy):
    observed, changes = index(discovery), []
    org, platform, builds = policy["organization"], policy["platform"], policy["builds"]
    common = ["current_user", "organization", "organization_owners"]

    def add(change_id, method, endpoint, payload, dependencies, manual=False):
        preconditions = {name: state_hash(observed[name]) if name in observed else None
                         for name in [*common, *dependencies]}
        changes.append({"id": change_id, "method": method, "endpoint": endpoint, "payload": payload,
                        "manual_only": manual, "status": "review-required",
                        "preconditions": preconditions,
                        "baseline_observation_gaps": [name for name in preconditions if name not in observed or
                         observed[name].get("method") != "GET" or observed[name].get("exit_code") != 0 or
                         not observed[name].get("pagination", {}).get("complete")]})

    add("organization-read-default", "PATCH", f"/orgs/{org['login']}",
        {"default_repository_permission": org["default_repository_permission"]}, ["organization"])
    for ruleset in platform["rulesets"]:
        existing = [r for r in observed.get(f"{platform['name']}.rulesets", {}).get("data") or []
                    if r.get("name") == ruleset["name"]]
        dependencies = [f"{platform['name']}.repository", f"{platform['name']}.rulesets"]
        if any(rule.get("type") == "pull_request" and rule.get("parameters", {}).get("require_code_owner_review")
               for rule in ruleset["rules"]):
            dependencies.extend([f"{platform['name']}.codeowners", f"{platform['name']}.codeowners_errors"])
        if len(existing) == 1 and existing[0].get("source_type") == "Repository":
            rule_id = existing[0]["id"]
            dependencies.append(f"{platform['name']}.ruleset.{rule_id}")
            add(ruleset["name"], "PUT", f"/repos/{platform['full_name']}/rulesets/{rule_id}", ruleset, dependencies)
        else:
            add(ruleset["name"], "POST", f"/repos/{platform['full_name']}/rulesets",
                ruleset if not existing else None, dependencies)
    detail = observed.get("Builds.main_ruleset_detail", {}).get("data")
    if isinstance(detail, dict) and observed["Builds.main_ruleset_detail"].get("exit_code") == 0:
        payload = ruleset_payload(detail)
        payload["bypass_actors"] = builds["bypass_actors"]
        add("builds-administrator-bypass", "PUT", f"/repos/{builds['full_name']}/rulesets/{builds['ruleset_id']}",
            payload, ["Builds.main_ruleset_detail", f"{builds['name']}.repository"])
    else:
        add("builds-administrator-bypass", "PUT", f"/repos/{builds['full_name']}/rulesets/{builds['ruleset_id']}",
            None, ["Builds.main_ruleset_detail"])
    repositories = observed.get("organization_repositories", {}).get("data") or []
    apps = observed.get("organization_installations", {}).get("data") or {}
    for app in apps.get("installations", []):
        selection_name = f"installation.{app['id']}.repositories"
        selected = observed.get(selection_name, {}).get("data") or {}
        preserve = repositories if app.get("repository_selection") == "all" else selected.get("repositories", [])
        add(f"exclude-private-repositories-from-app-{app['id']}", "MANUAL", f"/organizations/{org['login']}/settings/installations/{app['id']}",
            {"installation_id": app["id"], "repository_selection": "selected",
             "preserve_repository_ids": sorted(repo["id"] for repo in preserve if repo.get("full_name") not in
                                                {r["full_name"] for r in policy["private_repositories"]}),
             "exclude_full_names": [repo["full_name"] for repo in policy["private_repositories"]]},
            ["organization_installations", "organization_repositories", selection_name], manual=True)
    for repo in policy["private_repositories"]:
        add(f"create-{repo['name']}", "POST", f"/orgs/{org['login']}/repos",
            {"name": repo["name"], "private": True, "auto_init": False},
            ["organization", "organization_repositories", "organization_installations",
             *[f"installation.{app['id']}.repositories" for app in apps.get("installations", [])]])
        add(f"read-only-workflow-token-{repo['name']}", "PUT", f"/repos/{repo['full_name']}/actions/permissions/workflow",
            {"default_workflow_permissions": "read", "can_approve_pull_request_reviews": False},
            [f"{repo['name']}.repository", f"{repo['name']}.workflow_permissions"])
    return {"schema_version": 1, "story": "4.4", "baseline_sha256": digest(discovery),
            "desired_state_sha256": digest(policy), "mutation_authorized": False,
            "operational_acceptance": False, "changes": changes,
            "outstanding": ["Review payloads; rediscover before each affected change.",
                            "Publish CODEOWNERS on Platform main through a reviewed PR.",
                            "Manually preserve installation coverage and exclude both private repositories before executable content.",
                            "Creation and post-creation token settings need separate fresh checkpoints.",
                            "Collect owner custody and PAT evidence; API errors cannot qualify absence."]}


def preflight(proposal, discovery, policy, baseline, now=None):
    now = now or datetime.now(timezone.utc)
    observed = index(discovery)
    global_problems = []
    if proposal != propose(baseline, policy):
        global_problems.append("reviewed-proposal-or-baseline-changed")
    if discovery.get("collection_mode") != "GitHub REST GET only" or not fresh(
            discovery.get("captured_at_utc"), now, policy["evidence_max_age_seconds"]):
        global_problems.append("fresh-read-only-discovery-required")
    actor = observed.get("current_user", {}).get("data") or {}
    if any(actor.get(k) != policy["administrator"][k] for k in ("id", "login")):
        global_problems.append("administrator-identity-unproved")
    org = observed.get("organization", {}).get("data") or {}
    if any(org.get(k) != policy["organization"][k] for k in ("id", "login")) or org.get("plan", {}).get("name") != "free":
        global_problems.append("organization-identity-or-free-plan-unproved")
    owners = observed.get("organization_owners", {}).get("data") or []
    if {(a.get("id"), a.get("login")) for a in owners} != {(a["id"], a["login"]) for a in policy["private_repository_writers"]}:
        global_problems.append("approved-owner-boundary-unproved")
    checks = []
    for change in proposal["changes"]:
        problems = list(global_problems)
        for name, expected in change["preconditions"].items():
            observation = observed.get(name)
            if expected is None or observation is None or state_hash(observation) != expected:
                problems.append(f"stale-or-missing-baseline:{name}")
            if observation is None or observation.get("method") != "GET" or observation.get("exit_code") != 0 or not observation.get("pagination", {}).get("complete"):
                problems.append(f"incomplete-observation:{name}")
            elif not fresh(observation.get("observed_at_utc"), now, policy["evidence_max_age_seconds"]):
                problems.append(f"stale-observation:{name}")
        codeowners_name = f"{policy['platform']['name']}.codeowners"
        if codeowners_name in change["preconditions"]:
            codeowners = observed.get(codeowners_name, {}).get("data") or {}
            if codeowners.get("content_observed") is not True or codeowners.get("audit", {}).get("result") != "pass":
                problems.append("publish-administrator-codeowners-first")
            errors = observed.get(f"{policy['platform']['name']}.codeowners_errors", {}).get("data")
            if not isinstance(errors, dict) or errors.get("errors") != []:
                problems.append("resolve-codeowners-errors-first")
        if change["id"].startswith("create-"):
            existing = observed.get("organization_repositories", {}).get("data") or []
            if any(repo.get("full_name") == policy["organization"]["login"] + "/" + change["payload"]["name"] for repo in existing):
                problems.append("repository-already-exists")
            if observed.get("organization", {}).get("data", {}).get("default_repository_permission") != "read":
                problems.append("set-organization-read-default-first")
            apps = observed.get("organization_installations", {}).get("data") or {}
            if any(app.get("repository_selection") != "selected" for app in apps.get("installations", [])):
                problems.append("exclude-all-repository-apps-first")
        if change.get("payload") is None:
            problems.append("exact-payload-unavailable")
        if change["manual_only"]:
            problems.append("manual-installation-selection-and-readback-required")
        checks.append({"id": change["id"], "result": "blocked" if problems else "ready-for-human-review",
                       "problems": sorted(set(problems))})
    return {"verification_result": "blocked" if global_problems or any(c["problems"] for c in checks) else "pass",
            "discovery_sha256": digest(discovery), "proposal_sha256": digest(proposal),
            "mutation_authorized": False, "operational_acceptance": False,
            "issues": global_problems, "changes": checks}


def verify(discovery, policy, baseline, custody=None, now=None):
    now = now or datetime.now(timezone.utc)
    observed, issues = index(discovery), []
    max_age = policy["evidence_max_age_seconds"]

    def issue(condition, observation=None):
        record = {"condition": condition}
        if observation:
            record["observation"] = observation
        if record not in issues:
            issues.append(record)

    def need(name):
        item = observed.get(name)
        if item is None:
            issue("missing-observation", name)
            return None
        if item.get("method") != "GET" or item.get("exit_code") != 0:
            issue("api-observation-unavailable", name)
            return None
        if not item.get("pagination", {}).get("complete"):
            issue("incomplete-pagination-or-observation", name)
            return None
        if not fresh(item.get("observed_at_utc"), now, max_age):
            issue("stale-observation", name)
            return None
        return item.get("data")

    if discovery.get("collection_mode") != "GitHub REST GET only" or not fresh(discovery.get("captured_at_utc"), now, max_age):
        issue("fresh-read-only-discovery-required")
    admin, writers = policy["administrator"], policy["private_repository_writers"]
    user = need("current_user") or {}
    if any(user.get(key) != admin[key] for key in ("id", "login")):
        issue("administrator-observation-required")
    organization = need("organization") or {}
    if organization.get("login") != policy["organization"]["login"] or organization.get("id") != policy["organization"]["id"]:
        issue("organization-identity-mismatch")
    if organization.get("default_repository_permission") != "read":
        issue("organization-base-permission-not-read")
    if organization.get("plan", {}).get("name") != "free":
        issue("github-free-plan-required")
    owners = need("organization_owners") or []
    if {(a.get("id"), a.get("login")) for a in owners} != {(a["id"], a["login"]) for a in writers}:
        issue("private-writer-owner-boundary-mismatch")
    members = need("organization_members") or []
    outside = need("organization_outside_collaborators") or []
    org_teams = need("organization_teams") or []
    team_members = []
    for team in org_teams:
        team_members.extend(need(f"team.{team['id']}.members") or [])
    need("organization_repositories")

    platform = policy["platform"]
    metadata = need(f"{platform['name']}.repository") or {}
    if metadata.get("id") != platform["id"] or metadata.get("full_name") != platform["full_name"]:
        issue("platform-identity-mismatch")
    ruleset_summaries = need(f"{platform['name']}.rulesets") or []
    details = []
    for summary in ruleset_summaries:
        detail = need(f"{platform['name']}.ruleset.{summary['id']}")
        if detail:
            details.append(detail)
    for wanted in platform["rulesets"]:
        matches = [r for r in details if r.get("name") == wanted["name"]]
        if len(matches) != 1:
            issue("required-platform-ruleset-missing-or-ambiguous", wanted["name"])
            continue
        detail = matches[0]
        try:
            actual = ruleset_payload(detail)
        except ValueError:
            actual = None
        # GitHub may add default PR parameters and order the rules on readback.
        # Match every requested field while retaining these harmless defaults.
        matches = actual is not None and all(actual.get(k) == wanted[k] for k in RULESET_FIELDS if k != "rules")
        actual_rules = {r.get("type"): r for r in (actual or {}).get("rules", [])}
        if len(actual_rules) != len((actual or {}).get("rules", [])):
            matches = False
        if set(actual_rules) != {r["type"] for r in wanted["rules"]}:
            matches = False
        for required in wanted["rules"]:
            measured = actual_rules.get(required["type"], {})
            if any(measured.get("parameters", {}).get(k) != v for k, v in required.get("parameters", {}).items()):
                matches = False
        if not matches:
            issue("platform-ruleset-policy-mismatch", wanted["name"])
        if detail.get("source_type") != "Repository" or detail.get("source") != platform["full_name"]:
            issue("platform-ruleset-source-unproved", wanted["name"])
    # Bypass lists belong to each ruleset, not to an aggregate of effective rules.
    # A second applicable immutable-tag rule with a bypass also needs review.
    for detail in details:
        if detail.get("enforcement") != "active":
            continue
        types = {r.get("type") for r in detail.get("rules", [])}
        bypass = detail.get("bypass_actors", [])
        if detail.get("target") == "tag" and types & {"update", "deletion"} and bypass:
            issue("tag-immutability-bypass-forbidden", str(detail.get("id")))
        elif detail.get("target") in ("tag", "branch") and bypass and bypass != platform["rulesets"][0]["bypass_actors"]:
            issue("broader-platform-bypass-forbidden", str(detail.get("id")))
    effective = need(f"{platform['name']}.main_effective_rules") or []
    by_type = {r.get("type"): r for r in effective}
    pr = by_type.get("pull_request", {}).get("parameters", {})
    if not {"deletion", "non_fast_forward", "pull_request"}.issubset(by_type) or not pr.get("require_code_owner_review") or pr.get("required_approving_review_count", 0) < 1:
        issue("main-effective-rules-incomplete")
    codeowners = need(f"{platform['name']}.codeowners") or {}
    if codeowners.get("content_observed") is not True or codeowners.get("audit", {}).get("result") != "pass":
        issue("administrator-codeowners-not-published")
    errors = need(f"{platform['name']}.codeowners_errors")
    if not isinstance(errors, dict) or errors.get("errors") != []:
        issue("codeowners-resolution-errors-or-unproved")

    builds = policy["builds"]
    metadata = need(f"{builds['name']}.repository") or {}
    if metadata.get("id") != builds["id"] or metadata.get("full_name") != builds["full_name"]:
        issue("builds-identity-mismatch")
    current = need("Builds.main_ruleset_detail") or {}
    original = index(baseline).get("Builds.main_ruleset_detail", {}).get("data") or {}
    try:
        expected = ruleset_payload(original)
        expected["bypass_actors"] = builds["bypass_actors"]
        if ruleset_payload(current) != expected or current.get("id") != builds["ruleset_id"]:
            issue("builds-settings-or-checks-changed")
    except ValueError:
        issue("builds-reviewed-baseline-or-readback-incomplete")

    apps = need("organization_installations") or {}
    app_coverage = {}
    original_observed = index(baseline)
    old_installations = original_observed.get("organization_installations", {}).get("data") or {}
    old_apps = {a["id"]: a for a in old_installations.get("installations", [])}
    if {a.get("id") for a in apps.get("installations", [])} != set(old_apps):
        issue("installed-app-set-changed-from-reviewed-baseline")
    for app in apps.get("installations", []):
        coverage = need(f"installation.{app['id']}.repositories")
        coverage_complete = isinstance(coverage, dict) and isinstance(coverage.get("repositories"), list)
        if not coverage_complete:
            issue("app-repository-coverage-unproved", str(app["id"]))
            coverage = {}
        app_coverage[app["id"]] = coverage.get("repositories", [])
        if app.get("repository_selection") != "selected":
            issue("all-repository-app-can-access-private-repositories", str(app["id"]))
        old_app = old_apps.get(app["id"], {})
        if any(app.get(k) != old_app.get(k) for k in ("app_id", "permissions", "suspended_at")):
            issue("unrelated-app-permissions-or-state-changed", str(app["id"]))
        coverage_name = ("organization_repositories" if old_app.get("repository_selection") == "all"
                         else f"installation.{app['id']}.repositories")
        old_observation = original_observed.get(coverage_name, {})
        baseline_complete = old_observation.get("exit_code") == 0 and old_observation.get("pagination", {}).get("complete")
        if not baseline_complete:
            issue("app-preservation-baseline-incomplete", str(app["id"]))
        old_data = old_observation.get("data") or ([] if coverage_name == "organization_repositories" else {})
        old_repos = old_data if coverage_name == "organization_repositories" else old_data.get("repositories", [])
        private_names = {r["full_name"] for r in policy["private_repositories"]}
        old_ids = {r["id"] for r in old_repos if r.get("full_name") not in private_names}
        current_ids = {r["id"] for r in coverage.get("repositories", []) if r.get("full_name") not in private_names}
        if not coverage_complete or not baseline_complete:
            issue("app-repository-access-preservation-unproved", str(app["id"]))
        elif old_ids != current_ids:
            issue("unrelated-app-repository-access-changed", str(app["id"]))
    for name in ("fine_grained_pat_grants", "fine_grained_pat_requests"):
        grants = need(name)  # Inaccessible inventories are an unresolved dependency.
        if grants is not None and not isinstance(grants, list):
            issue("pat-inventory-shape-unproved", name)
            continue
        for grant in grants or []:
            owner = grant.get("owner", {})
            permissions = grant.get("permissions")
            if not isinstance(permissions, dict):
                issue("pat-permission-extent-unproved", name)
                continue
            def writable(value):
                return (any(writable(v) for v in value.values()) if isinstance(value, dict)
                        else value == "write")
            if writable(permissions):
                if (owner.get("id"), owner.get("login")) not in {(a["id"], a["login"]) for a in writers}:
                    issue("unauthorized-writable-pat-grant-or-request", name)
                # Selected and all-repository PAT grants can both carry private
                # authority. Bind the exact grant's repository scope for custody.
                kind = "request" if name == "fine_grained_pat_requests" else "grant"
                repositories = need(f"pat.{kind}.{grant.get('id')}.repositories")
                if repositories is not None and not isinstance(repositories, list):
                    issue("pat-repository-extent-unproved", name)
    writer_ids = {a["id"] for a in writers}
    private_ids = []
    for repo in policy["private_repositories"]:
        name = repo["name"]
        metadata = need(f"{name}.repository") or {}
        private_ids.append(metadata.get("id"))
        if metadata.get("full_name") != repo["full_name"] or metadata.get("private") is not True or metadata.get("visibility") != "private":
            issue("private-repository-unproved", name)
        settings = need(f"{name}.workflow_permissions") or {}
        if settings.get("default_workflow_permissions") != "read" or settings.get("can_approve_pull_request_reviews") is not False:
            issue("private-workflow-token-settings-unsafe", name)
        collaborators = need(f"{name}.collaborators") or []
        teams = need(f"{name}.teams") or []
        if any(t.get("id") not in {team.get("id") for team in org_teams} for t in teams):
            issue("repository-team-membership-unproved", name)
        actors = {a["id"]: a for a in [*owners, *members, *outside, *team_members, *collaborators]}
        effective_writers = set()
        for actor_id, actor in actors.items():
            permission = need(f"{name}.permission.{actor_id}") or {}
            measured_user = permission.get("user", {})
            if measured_user.get("id") != actor_id or measured_user.get("login") != actor.get("login"):
                issue("effective-actor-identity-unproved", f"{name}:{actor_id}")
            flags = measured_user.get("permissions", {})
            if permission.get("permission") in ("write", "admin", "maintain") or any(flags.get(key) is True for key in ("push", "maintain", "admin")):
                effective_writers.add(actor_id)
        if effective_writers != writer_ids:
            issue("effective-private-writers-mismatch", name)
        for team in teams:
            if team.get("permission") not in ("pull", "read", "triage") or any(team.get("permissions", {}).get(k) is True for k in ("push", "maintain", "admin")):
                issue("inherited-team-write-forbidden", name)
        for key in need(f"{name}.deploy_keys") or []:
            if key.get("read_only") is not True:
                issue("writable-deploy-key-forbidden", name)
        for app_id, coverage in app_coverage.items():
            if any(r.get("id") == metadata.get("id") or r.get("full_name") == repo["full_name"] for r in coverage):
                issue("installed-app-not-excluded", f"{name}:{app_id}")
        refs = []
        for kind in ("heads", "tags"):
            refs.extend(need(f"{name}.refs.{kind}") or [])
        if not any(r.get("ref", "").startswith("refs/heads/") for r in refs):
            issue("private-workflow-content-inventory-unproved", name)
        tree_shas = {resolved_ref_sha(ref, lambda sha: need(f"{name}.tag.{sha}")) for ref in refs}
        if None in tree_shas:
            issue("private-workflow-tag-object-unproved", name)
        for sha in sorted(tree_shas - {None}):
            tree = need(f"{name}.tree.{sha}") or {}
            if tree.get("truncated") is not False or not isinstance(tree.get("tree"), list):
                issue("private-workflow-tree-incomplete", name)
            for entry in tree.get("tree", []):
                path = entry.get("path", "")
                if path.startswith(".github/workflows/") and path.endswith((".yml", ".yaml")):
                    workflow = need(f"{name}.workflow.{sha}.{path}") or {}
                    if entry.get("type") != "blob" or entry.get("mode") != "100644" or workflow.get("sha") != entry.get("sha") or workflow.get("content_observed") is not True or workflow.get("audit", {}).get("result") != "pass":
                        issue("private-workflow-write-override-or-content-unproved", f"{name}:{path}")

    # Attestations name both owners and bind the exact discovery. They carry
    # evidence references/hashes, never credential values. Authenticity/custody
    # of these owner statements is checked by the human operational reviewer.
    if not custody or custody.get("discovery_sha256") != digest(discovery) or not fresh(custody.get("captured_at_utc"), now, max_age):
        issue("fresh-owner-custody-evidence-required")
    else:
        attestations = custody.get("owners", [])
        if {(a.get("id"), a.get("login")) for a in attestations} != {(a["id"], a["login"]) for a in writers}:
            issue("both-named-owner-custody-statements-required")
        for actor in attestations:
            scopes = {item.get("kind"): item for item in actor.get("inventories", [])}
            for kind in ("classic-pat", "fine-grained-pat", "app-credentials", "deploy-keys", "cached-credentials", "workflow-and-admin-credentials"):
                item = scopes.get(kind, {})
                if item.get("complete") is not True or item.get("writable_credentials_on_named_writer_devices_only") is not True or not re.fullmatch(r"[0-9a-f]{64}", item.get("evidence_sha256", "")) or not item.get("evidence_reference"):
                    issue("credential-custody-inventory-incomplete", f"{actor.get('id')}:{kind}")
        if custody.get("no_writable_credentials_outside_named_writer_devices") is not True:
            issue("outside-device-writable-credential-custody-unproved")
        if set(custody.get("private_repository_ids", [])) != set(private_ids) or None in private_ids:
            issue("custody-private-repository-binding-mismatch")
    return {"schema_version": 1, "story": "4.4", "verification_result": "blocked" if issues else "pass",
            "verification_scope": "local consistency of supplied observations and owner statements",
            "discovery_sha256": digest(discovery), "desired_state_sha256": digest(policy),
            "baseline_sha256": digest(baseline), "custody_sha256": digest(custody) if custody else None,
            "mutation_authorized": False, "operational_acceptance": False, "complete": False,
            "issues": issues,
            "retained_api_errors": [{"name": name, "status": item.get("error", {}).get("status"),
                                     "condition": item.get("error", {}).get("condition", "api-observation-unavailable")}
                                    for name, item in observed.items() if item.get("exit_code") != 0]}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--policy", type=Path, default=DEFAULT_POLICY)
    sub = parser.add_subparsers(dest="command", required=True)
    read = sub.add_parser("discover", help="explicitly opted-in GET-only GitHub discovery")
    read.add_argument("--live-read-only", action="store_true", required=True)
    for command in ("propose", "preflight", "verify"):
        child = sub.add_parser(command)
        child.add_argument("--discovery", type=Path, required=True)
        if command == "preflight":
            child.add_argument("--proposal", type=Path, required=True)
            child.add_argument("--baseline", type=Path, required=True)
        if command == "verify":
            child.add_argument("--baseline", type=Path, required=True)
            child.add_argument("--custody", type=Path)
    for child in sub.choices.values():
        child.add_argument("--output", type=Path)
    args = parser.parse_args(argv)
    try:
        policy = load(args.policy)
        if args.command == "discover":
            report = discover(policy)
        elif args.command == "propose":
            report = propose(load(args.discovery), policy)
        elif args.command == "preflight":
            report = preflight(load(args.proposal), load(args.discovery), policy, load(args.baseline))
        else:
            report = verify(load(args.discovery), policy, load(args.baseline), load(args.custody) if args.custody else None)
        rendered = json.dumps(report, indent=2, sort_keys=True) + "\n"
        if args.output:
            args.output.parent.mkdir(parents=True, exist_ok=True)
            args.output.write_text(rendered)
        else:
            print(rendered, end="")
        return 1 if report.get("verification_result") == "blocked" else 0
    except (OSError, ValueError, KeyError, TypeError):
        # Do not echo raw untrusted input, API output, or paths containing tokens.
        print(json.dumps({"verification_result": "blocked", "condition": "invalid-or-inaccessible-input",
                          "mutation_authorized": False, "operational_acceptance": False}))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
