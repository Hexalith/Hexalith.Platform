import base64
import copy
from datetime import datetime, timedelta, timezone
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import controls


BASELINE = controls.ROOT / "_bmad-output/implementation-artifacts/evidence/epic-4/4-4/20261007T101349Z/discovery.json"


class PolicyTests(unittest.TestCase):
    def setUp(self):
        self.policy = controls.load(controls.DEFAULT_POLICY)
        self.retained = controls.load(BASELINE)
        self.now = datetime(2026, 10, 7, 12, 0, tzinfo=timezone.utc)
        self.time = self.now.isoformat()
        self.baseline = copy.deepcopy(self.retained)
        for item in self.baseline["observations"]:
            item.update(observed_at_utc=self.time, pagination={"complete": True, "page_count": 1})
        self.baseline["captured_at_utc"] = self.time
        # Synthetic fixture explicitly proves selected-installation coverage;
        # retained real discovery does not and must remain blocked.
        apps = controls.index(self.baseline)["organization_installations"]["data"]["installations"]
        for app in apps:
            if app["repository_selection"] == "selected":
                self.put(self.baseline, f"installation.{app['id']}.repositories", {"repositories": [], "total_count": 0})
        self.discovery = self.accepted_state()
        self.custody = self.owner_custody()

    def put(self, discovery, name, data, **extra):
        item = {"name": name, "endpoint": "https://api.github.com/synthetic", "method": "GET", "data": copy.deepcopy(data),
                "exit_code": 0, "observed_at_utc": self.time, "pagination": {"complete": True, "page_count": 1}}
        item.update(extra)
        discovery["observations"] = [o for o in discovery["observations"] if o["name"] != name] + [item]

    def item(self, name):
        return controls.index(self.discovery)[name]

    def accepted_state(self):
        discovery = copy.deepcopy(self.baseline)
        org = controls.index(discovery)["organization"]["data"]
        org["default_repository_permission"] = "read"
        summaries = []
        effective = []
        for rule_id, wanted in enumerate(self.policy["platform"]["rulesets"], 100):
            detail = copy.deepcopy(wanted)
            detail.update(id=rule_id, source=self.policy["platform"]["full_name"], source_type="Repository")
            self.put(discovery, f"Hexalith.Platform.ruleset.{rule_id}", detail)
            summaries.append({"id": rule_id, "name": wanted["name"], "source_type": "Repository"})
            if wanted["target"] == "branch":
                effective = wanted["rules"]
        self.put(discovery, "Hexalith.Platform.rulesets", summaries)
        self.put(discovery, "Hexalith.Platform.main_effective_rules", effective)
        self.put(discovery, "Hexalith.Platform.codeowners", {"content_observed": True,
                 "audit": controls.codeowners_audit("\n".join(self.policy["platform"]["codeowners"]), self.policy)})
        self.put(discovery, "Hexalith.Platform.codeowners_errors", {"errors": []})
        builds = controls.index(discovery)["Builds.main_ruleset_detail"]["data"]
        builds["bypass_actors"] = copy.deepcopy(self.policy["builds"]["bypass_actors"])
        apps = controls.index(discovery)["organization_installations"]["data"]["installations"]
        repositories = controls.index(self.baseline)["organization_repositories"]["data"]
        old_apps = controls.index(self.baseline)["organization_installations"]["data"]["installations"]
        for app, old_app in zip(apps, old_apps):
            selected = copy.deepcopy(repositories) if old_app["repository_selection"] == "all" else []
            app["repository_selection"] = "selected"
            self.put(discovery, f"installation.{app['id']}.repositories", {"repositories": selected, "total_count": len(selected)})
        self.put(discovery, "fine_grained_pat_grants", [])
        self.put(discovery, "fine_grained_pat_requests", [])
        for repo_id, repo in enumerate(self.policy["private_repositories"], 10000):
            name = repo["name"]
            metadata = {"id": repo_id, "full_name": repo["full_name"], "private": True, "visibility": "private"}
            self.put(discovery, f"{name}.repository", metadata)
            self.put(discovery, f"{name}.workflow_permissions", {"default_workflow_permissions": "read", "can_approve_pull_request_reviews": False})
            self.put(discovery, f"{name}.collaborators", self.policy["private_repository_writers"])
            self.put(discovery, f"{name}.teams", [])
            self.put(discovery, f"{name}.deploy_keys", [])
            for actor in self.policy["private_repository_writers"]:
                self.put(discovery, f"{name}.permission.{actor['id']}", {"user": actor, "permission": "admin"})
            self.put(discovery, f"{name}.refs.heads", [{"ref": "refs/heads/main", "object": {"type": "commit", "sha": "a" * 40}}])
            self.put(discovery, f"{name}.refs.tags", [])
            self.put(discovery, f"{name}.tree.{'a' * 40}", {"truncated": False, "tree": [{"path": "README.md", "type": "blob", "mode": "100644", "sha": "b" * 40}]})
        return discovery

    def owner_custody(self):
        owners = []
        for actor in self.policy["private_repository_writers"]:
            owners.append({**actor, "inventories": [{"kind": kind, "complete": True,
                           "writable_credentials_on_named_writer_devices_only": True,
                           "evidence_reference": "owner-retained-synthetic-reference", "evidence_sha256": "1" * 64}
                           for kind in ("classic-pat", "fine-grained-pat", "app-credentials", "deploy-keys", "cached-credentials", "workflow-and-admin-credentials")]})
        return {"captured_at_utc": self.time, "discovery_sha256": controls.digest(self.discovery),
                "owners": owners, "private_repository_ids": [10000, 10001],
                "no_writable_credentials_outside_named_writer_devices": True}

    def verify(self, rebind=True):
        if rebind:
            self.custody["discovery_sha256"] = controls.digest(self.discovery)
        return controls.verify(self.discovery, self.policy, self.baseline, self.custody, self.now)

    def conditions(self, report):
        return {issue["condition"] for issue in report["issues"]}

    def test_complete_synthetic_state_passes_only_local_consistency(self):
        report = self.verify()
        self.assertEqual(report["verification_result"], "pass", report)
        for flag in ("mutation_authorized", "operational_acceptance", "complete"):
            self.assertFalse(report[flag])

    def test_tag_creation_bypass_is_separate_from_immutability(self):
        create, immutable = self.policy["platform"]["rulesets"][1:]
        self.assertEqual(create["conditions"]["ref_name"], {"include": ["~ALL"], "exclude": []})
        self.assertEqual({r["type"] for r in create["rules"]}, {"creation"})
        self.assertEqual({r["type"] for r in immutable["rules"]}, {"update", "deletion"})
        self.assertEqual(create["bypass_actors"], self.policy["builds"]["bypass_actors"])
        self.assertEqual(immutable["bypass_actors"], [])

    def test_tag_update_or_deletion_bypass_fails(self):
        self.item("Hexalith.Platform.ruleset.102")["data"]["bypass_actors"] = self.policy["builds"]["bypass_actors"]
        self.assertIn("tag-immutability-bypass-forbidden", self.conditions(self.verify()))

    def test_broader_owner_or_user_bypass_fails(self):
        for actor in ({"actor_id": None, "actor_type": "OrganizationAdmin", "bypass_mode": "always"},
                      {"actor_id": 183023925, "actor_type": "User", "bypass_mode": "always"}):
            self.item("Hexalith.Platform.ruleset.100")["data"]["bypass_actors"] = [actor]
            with self.subTest(actor=actor):
                self.assertIn("broader-platform-bypass-forbidden", self.conditions(self.verify()))

    def test_all_tag_coverage_cannot_have_exclusions(self):
        self.item("Hexalith.Platform.ruleset.102")["data"]["conditions"]["ref_name"]["exclude"] = ["refs/tags/legacy-*"]
        self.assertIn("platform-ruleset-policy-mismatch", self.conditions(self.verify()))

    def test_missing_codeowners_or_codeowners_errors_block(self):
        self.discovery["observations"] = [o for o in self.discovery["observations"] if o["name"] != "Hexalith.Platform.codeowners"]
        self.assertIn("administrator-codeowners-not-published", self.conditions(self.verify()))
        self.item("Hexalith.Platform.codeowners_errors")["data"] = {"errors": [{"line": 1}]}
        self.assertIn("codeowners-resolution-errors-or-unproved", self.conditions(self.verify()))

    def test_codeowners_cannot_be_overridden_by_later_patterns(self):
        content = "\n".join(self.policy["platform"]["codeowners"]) + "\n* @tinouit\n"
        self.assertEqual(controls.codeowners_audit(content, self.policy)["result"], "fail")
        local = (controls.ROOT / ".github/CODEOWNERS").read_text()
        self.assertEqual(controls.codeowners_audit(local, self.policy)["result"], "pass")

    def test_builds_proposal_preserves_all_non_bypass_settings(self):
        proposal = controls.propose(self.retained, self.policy)
        payload = next(c["payload"] for c in proposal["changes"] if c["id"] == "builds-administrator-bypass")
        expected = controls.ruleset_payload(controls.index(self.retained)["Builds.main_ruleset_detail"]["data"])
        original_checks = copy.deepcopy(expected["rules"])
        expected["bypass_actors"] = self.policy["builds"]["bypass_actors"]
        self.assertEqual(payload, expected)
        self.assertEqual(payload["rules"], original_checks)
        contexts = {c["context"] for r in payload["rules"] if r["type"] == "required_status_checks" for c in r["parameters"]["required_status_checks"]}
        self.assertEqual(contexts, {"Codacy Static Code Analysis", "SonarCloud Code Analysis", "commitlint / commitlint"})

    def test_builds_check_removal_or_extra_bypass_fails(self):
        self.item("Builds.main_ruleset_detail")["data"]["rules"].pop()
        self.assertIn("builds-settings-or-checks-changed", self.conditions(self.verify()))

    def test_builds_preservation_requires_complete_successful_reviewed_baseline(self):
        for defect in ("failed", "partial", "mutation", "wrong-id"):
            baseline = copy.deepcopy(self.baseline)
            observation = controls.index(baseline)["Builds.main_ruleset_detail"]
            if defect == "failed":
                observation.update(exit_code=1, error={"status": 403})
            elif defect == "partial":
                observation["pagination"]["complete"] = False
            elif defect == "mutation":
                observation["method"] = "PUT"
            else:
                observation["data"]["id"] = 77
            with self.subTest(defect=defect):
                report = controls.verify(self.discovery, self.policy, baseline, self.custody, self.now)
                self.assertIn("builds-reviewed-baseline-or-readback-incomplete", self.conditions(report))

    def test_preservation_baseline_may_be_historical_when_current_readback_is_fresh(self):
        baseline = copy.deepcopy(self.baseline)
        baseline["captured_at_utc"] = "2020-01-01T00:00:00Z"
        for observation in baseline["observations"]:
            observation["observed_at_utc"] = baseline["captured_at_utc"]
        report = controls.verify(self.discovery, self.policy, baseline, self.custody, self.now)
        self.assertEqual(report["verification_result"], "pass", report)

    def test_inherited_organization_member_writer_fails_even_without_direct_collaboration(self):
        stranger = {"id": 77, "login": "inherited-writer"}
        self.item("organization_members")["data"].append(stranger)
        for repo in self.policy["private_repositories"]:
            self.put(self.discovery, f"{repo['name']}.permission.77", {"user": stranger, "permission": "write"})
        self.assertIn("effective-private-writers-mismatch", self.conditions(self.verify()))

    def test_team_inheritance_is_verified(self):
        team = {"id": 70, "slug": "unexpected", "permission": "push"}
        self.item("organization_teams")["data"] = [team]
        self.put(self.discovery, "team.70.members", [{"id": 77, "login": "team-writer"}])
        self.item("Hexalith.Operations.teams")["data"] = [team]
        self.assertIn("inherited-team-write-forbidden", self.conditions(self.verify()))
        self.assertIn("missing-observation", self.conditions(self.verify()))

    def test_custom_role_push_flag_counts_as_write(self):
        stranger = {"id": 77, "login": "custom-writer", "permissions": {"push": True}}
        self.item("Hexalith.Operations.collaborators")["data"].append(stranger)
        self.put(self.discovery, "Hexalith.Operations.permission.77", {"user": stranger, "permission": "read", "role_name": "custom-role"})
        self.assertIn("effective-private-writers-mismatch", self.conditions(self.verify()))

    def test_unknown_effective_permission_extent_is_not_read_access(self):
        stranger = {"id": 77, "login": "unproved-role"}
        self.item("organization_members")["data"].append(stranger)
        for repo in self.policy["private_repositories"]:
            self.put(self.discovery, f"{repo['name']}.permission.77", {"user": stranger, "permission": "future-role"})
        self.assertIn("effective-permission-extent-unproved", self.conditions(self.verify()))

    def test_collaborator_write_capability_cannot_be_erased_by_permission_readback(self):
        stranger = {"id": 77, "login": "custom-writer", "permissions": {"push": True}}
        self.item("Hexalith.Operations.collaborators")["data"].append(stranger)
        self.put(self.discovery, "Hexalith.Operations.permission.77",
                 {"user": {"id": 77, "login": "custom-writer"}, "permission": "read", "role_name": "custom-role"})
        self.assertIn("effective-private-writers-mismatch", self.conditions(self.verify()))

    def test_read_defaults_do_not_mask_writable_workflow_override(self):
        content = "name: read-default-override\npermissions:\n  contents: write\njobs:\n  example:\n    runs-on: ubuntu-latest\n"
        path = ".github/workflows/override.yml"
        self.item(f"Hexalith.Operations.tree.{'a' * 40}")["data"]["tree"].append({"path": path, "mode": "100644", "type": "blob", "sha": "c" * 40})
        self.put(self.discovery, f"Hexalith.Operations.workflow.{'a' * 40}.{path}",
                 {"content_observed": True, "sha": "c" * 40, "audit": controls.workflow_audit(content)})
        self.assertIn("private-workflow-write-override-or-content-unproved", self.conditions(self.verify()))

    def test_workflow_audit_covers_job_flow_and_obscured_overrides(self):
        for content in ("permissions: write-all", "jobs:\n  example:\n    permissions:\n      contents: write",
                        "permissions: {contents: write}", '"permissions": {"contents": "write"}',
                        '"permiss\\u0069ons": write-all', "permissions: *grant", "grant: &grant {contents: write}",
                        "jobs: {test: {permissions: write-all}}", "run: echo ${{ secrets.WRITABLE_PAT }}",
                        '? permissions\n: write-all', '? "permiss\\\nions"\n: "wr\\u0069te-all"',
                        'secrets: inherit'):
            with self.subTest(content=content):
                self.assertEqual(controls.workflow_audit(content)["result"], "blocked")
        for content in ("permissions: {}\njobs:\n  test:\n    permissions:\n      contents: read",
                        "permissions: read-all\nname: read-only", "name: default-read-only\njobs: {}"):
            with self.subTest(content=content):
                self.assertEqual(controls.workflow_audit(content)["result"], "pass")

    def test_all_branches_and_tags_require_complete_trees(self):
        self.item("Hexalith.Operations.refs.heads")["data"].append({"ref": "refs/heads/hidden", "object": {"type": "commit", "sha": "d" * 40}})
        self.assertIn("private-workflow-tree-incomplete", self.conditions(self.verify()))
        self.assertIn("missing-observation", self.conditions(self.verify()))

    def test_all_repository_apps_and_selected_private_access_fail(self):
        app = self.item("organization_installations")["data"]["installations"][0]
        app["repository_selection"] = "all"
        self.assertIn("all-repository-app-can-access-private-repositories", self.conditions(self.verify()))
        app["repository_selection"] = "selected"
        self.item(f"installation.{app['id']}.repositories")["data"]["repositories"].append({"id": 10000, "full_name": "Hexalith/Hexalith.Operations"})
        self.assertIn("installed-app-not-excluded", self.conditions(self.verify()))

    def test_app_selection_preserves_all_existing_repository_access(self):
        app = self.item("organization_installations")["data"]["installations"][1]
        self.item(f"installation.{app['id']}.repositories")["data"]["repositories"].pop()
        self.assertIn("unrelated-app-repository-access-changed", self.conditions(self.verify()))
        app["permissions"]["contents"] = "write"
        self.assertIn("unrelated-app-permissions-or-state-changed", self.conditions(self.verify()))

    def test_unavailable_app_coverage_does_not_claim_repository_access_changed(self):
        app = self.item("organization_installations")["data"]["installations"][1]
        item = self.item(f"installation.{app['id']}.repositories")
        item["exit_code"] = 1
        item["status"] = 403
        item["data"] = {"message": "Resource not accessible by integration"}
        report = self.verify()
        self.assertEqual("blocked", report["verification_result"])
        self.assertIn("api-observation-unavailable", self.conditions(report))
        self.assertIn("app-repository-access-preservation-unproved", self.conditions(report))
        self.assertNotIn("unrelated-app-repository-access-changed", self.conditions(report))

    def test_incomplete_app_baseline_does_not_claim_repository_access_changed(self):
        item = controls.index(self.baseline)["organization_repositories"]
        item["pagination"]["complete"] = False
        item["data"] = []
        report = self.verify()
        self.assertEqual("blocked", report["verification_result"])
        self.assertIn("app-preservation-baseline-incomplete", self.conditions(report))
        self.assertIn("app-repository-access-preservation-unproved", self.conditions(report))
        self.assertNotIn("unrelated-app-repository-access-changed", self.conditions(report))

    def test_app_preservation_requires_complete_reviewed_installation_inventory(self):
        for defect in ("failed", "partial", "mutation"):
            baseline = copy.deepcopy(self.baseline)
            observation = controls.index(baseline)["organization_installations"]
            if defect == "failed":
                observation.update(exit_code=1, error={"status": 403})
            elif defect == "partial":
                observation["pagination"]["complete"] = False
            else:
                observation["method"] = "PUT"
            with self.subTest(defect=defect):
                report = controls.verify(self.discovery, self.policy, baseline, self.custody, self.now)
                self.assertIn("app-preservation-installation-baseline-incomplete", self.conditions(report))

    def test_unknown_pat_inventory_and_unauthorized_writable_grant_fail(self):
        for name in ("fine_grained_pat_grants", "fine_grained_pat_requests"):
            self.item(name)["data"] = [{"id": 321, "owner": {"id": 77, "login": "outside-owner"},
                                        "permissions": {"repository": {"contents": "write"}}}]
            self.assertIn("unauthorized-writable-pat-grant-or-request", self.conditions(self.verify()))
        self.item("fine_grained_pat_grants")["data"] = [{"id": 11, "owner": self.policy["administrator"]}]
        self.assertIn("pat-permission-extent-unproved", self.conditions(self.verify()))

    def test_named_owner_writable_pat_requires_scope_and_custody(self):
        self.item("fine_grained_pat_grants")["data"] = [{"id": 11, "owner": self.policy["administrator"],
                                                       "permissions": {"contents": "write"}}]
        self.assertIn("missing-observation", self.conditions(self.verify()))
        self.put(self.discovery, "pat.grant.11.repositories", [{"id": 10000}])
        self.assertEqual(self.verify()["verification_result"], "pass")
        self.custody["owners"][0]["inventories"][0]["complete"] = False
        self.assertIn("credential-custody-inventory-incomplete", self.conditions(self.verify()))

    def test_unknown_empty_or_malformed_pat_permissions_are_unproved(self):
        for permission in ({}, {"contents": "admin"}, {"contents": True}, {"repository": {}},
                           {"contents": ["read", "write"]}, {"repository": {"contents": "future-value"}}):
            self.item("fine_grained_pat_grants")["data"] = [{"id": 11, "owner": self.policy["administrator"],
                                                           "permissions": permission}]
            with self.subTest(permissions=permission):
                self.assertIn("pat-permission-extent-unproved", self.conditions(self.verify()))

    def test_inaccessible_pat_inventory_is_not_absence(self):
        self.item("fine_grained_pat_grants").update(exit_code=1, data=None, error={"status": 404})
        report = self.verify()
        self.assertIn("api-observation-unavailable", self.conditions(report))
        self.assertTrue(any(e["name"] == "fine_grained_pat_grants" and e["status"] == 404 for e in report["retained_api_errors"]))

    def test_writable_deploy_key_and_workflow_approval_fail(self):
        self.item("Hexalith.Operations.deploy_keys")["data"] = [{"id": 9, "read_only": False}]
        self.item("Hexalith.Notifications.workflow_permissions")["data"]["can_approve_pull_request_reviews"] = True
        conditions = self.conditions(self.verify())
        self.assertIn("writable-deploy-key-forbidden", conditions)
        self.assertIn("private-workflow-token-settings-unsafe", conditions)

    def test_missing_pagination_stale_state_and_truncated_tree_fail(self):
        self.item("organization_members").pop("pagination")
        self.item("organization_owners")["observed_at_utc"] = (self.now - timedelta(minutes=16)).isoformat()
        self.item(f"Hexalith.Operations.tree.{'a' * 40}")["data"]["truncated"] = True
        conditions = self.conditions(self.verify())
        self.assertIn("incomplete-pagination-or-observation", conditions)
        self.assertIn("stale-observation", conditions)
        self.assertIn("private-workflow-tree-incomplete", conditions)

    def test_custody_is_required_for_both_owners_and_bound_to_discovery(self):
        self.custody["owners"].pop()
        self.assertIn("both-named-owner-custody-statements-required", self.conditions(self.verify()))
        self.custody["discovery_sha256"] = "0" * 64
        self.assertIn("fresh-owner-custody-evidence-required", self.conditions(self.verify(rebind=False)))

    def test_retained_real_discovery_stays_blocked_and_404_is_not_absence(self):
        report = controls.verify(self.retained, self.policy, self.retained, now=self.now)
        self.assertEqual(report["verification_result"], "blocked")
        conditions = self.conditions(report)
        self.assertIn("fresh-read-only-discovery-required", conditions)
        self.assertIn("incomplete-pagination-or-observation", conditions)
        self.assertIn("private-repository-unproved", conditions)
        self.assertIn("fresh-owner-custody-evidence-required", conditions)

    def test_stale_baseline_stops_only_affected_changes(self):
        proposal = controls.propose(self.baseline, self.policy)
        changed = copy.deepcopy(self.baseline)
        controls.index(changed)["Builds.main_ruleset_detail"]["data"]["rules"].pop()
        report = controls.preflight(proposal, changed, self.policy, self.baseline, self.now)
        checks = {c["id"]: c for c in report["changes"]}
        self.assertEqual(checks["builds-administrator-bypass"]["result"], "blocked")
        self.assertEqual(checks["organization-read-default"]["result"], "ready-for-human-review")

    def test_every_settings_change_requires_fresh_complete_get_identity_observations(self):
        baseline = copy.deepcopy(self.discovery)
        proposal = controls.propose(baseline, self.policy)
        mutable = [change["id"] for change in proposal["changes"]
                   if not change["manual_only"] and not change["id"].startswith("create-")]
        ready = controls.preflight(proposal, baseline, self.policy, baseline, self.now)
        self.assertTrue(all(change["result"] == "ready-for-human-review"
                            for change in ready["changes"] if change["id"] in mutable))
        for name in ("current_user", "organization", "organization_owners"):
            for defect in ("missing", "failed", "stale", "incomplete", "not-get"):
                changed = copy.deepcopy(baseline)
                observation = controls.index(changed)[name]
                if defect == "missing":
                    changed["observations"].remove(observation)
                elif defect == "failed":
                    observation.update(exit_code=1, error={"status": 403})
                elif defect == "stale":
                    observation["observed_at_utc"] = (self.now - timedelta(minutes=16)).isoformat()
                elif defect == "incomplete":
                    observation["pagination"]["complete"] = False
                else:
                    observation["method"] = "PATCH"
                with self.subTest(name=name, defect=defect):
                    report = controls.preflight(proposal, changed, self.policy, baseline, self.now)
                    self.assertTrue(all(change["result"] == "blocked"
                                        for change in report["changes"] if change["id"] in mutable))

    def test_main_change_requires_published_codeowners_without_resolution_errors(self):
        main_name = self.policy["platform"]["rulesets"][0]["name"]
        for observation_name, data, expected in (
                ("Hexalith.Platform.codeowners", {"content_observed": False}, "publish-administrator-codeowners-first"),
                ("Hexalith.Platform.codeowners", {"content_observed": True, "audit": {"result": "fail"}}, "publish-administrator-codeowners-first"),
                ("Hexalith.Platform.codeowners_errors", {"errors": [{"line": 1}]}, "resolve-codeowners-errors-first"),
                ("Hexalith.Platform.codeowners_errors", None, "resolve-codeowners-errors-first")):
            baseline = copy.deepcopy(self.discovery)
            self.put(baseline, observation_name, data)
            proposal = controls.propose(baseline, self.policy)
            report = controls.preflight(proposal, baseline, self.policy, baseline, self.now)
            checks = {change["id"]: change for change in report["changes"]}
            with self.subTest(observation=observation_name, data=data):
                self.assertIn(expected, checks[main_name]["problems"])
                self.assertEqual(checks[main_name]["result"], "blocked")
                self.assertEqual(checks["Platform tag creation authorization"]["result"], "ready-for-human-review")

    def test_main_proposal_binds_codeowners_observation_and_freshness(self):
        baseline = copy.deepcopy(self.discovery)
        proposal = controls.propose(baseline, self.policy)
        main_name = self.policy["platform"]["rulesets"][0]["name"]
        main_change = next(change for change in proposal["changes"] if change["id"] == main_name)
        for name in ("Hexalith.Platform.codeowners", "Hexalith.Platform.codeowners_errors"):
            self.assertIn(name, main_change["preconditions"])
            for defect in ("missing", "failed", "stale", "changed"):
                changed = copy.deepcopy(baseline)
                observation = controls.index(changed)[name]
                if defect == "missing":
                    changed["observations"].remove(observation)
                elif defect == "failed":
                    observation.update(exit_code=1, error={"status": 403})
                elif defect == "stale":
                    observation["observed_at_utc"] = (self.now - timedelta(minutes=16)).isoformat()
                else:
                    observation["data"]["sha"] = "changed-after-review"
                with self.subTest(name=name, defect=defect):
                    report = controls.preflight(proposal, changed, self.policy, baseline, self.now)
                    check = next(change for change in report["changes"] if change["id"] == main_name)
                    self.assertEqual(check["result"], "blocked")

    def test_tampered_reviewed_method_endpoint_payload_or_removed_changes_cannot_pass(self):
        original = controls.propose(self.baseline, self.policy)
        for field, value in (("method", "DELETE"), ("endpoint", "/orgs/other"), ("payload", {"default_repository_permission": "write"})):
            proposal = copy.deepcopy(original)
            proposal["changes"][0][field] = value
            with self.subTest(field=field):
                report = controls.preflight(proposal, self.baseline, self.policy, self.baseline, self.now)
                self.assertIn("reviewed-proposal-or-baseline-changed", report["issues"])
        proposal = copy.deepcopy(original)
        proposal["changes"] = []
        self.assertEqual(controls.preflight(proposal, self.baseline, self.policy, self.baseline, self.now)["verification_result"], "blocked")

    def test_creation_requires_read_default_and_selected_apps_first(self):
        proposal = controls.propose(self.baseline, self.policy)
        report = controls.preflight(proposal, self.baseline, self.policy, self.baseline, self.now)
        creation = next(c for c in report["changes"] if c["id"] == "create-Hexalith.Operations")
        self.assertIn("set-organization-read-default-first", creation["problems"])
        self.assertIn("exclude-all-repository-apps-first", creation["problems"])
        self.assertFalse(report["mutation_authorized"])

    def test_creation_prerequisites_cannot_be_missing_failed_or_stale(self):
        proposal = controls.propose(self.baseline, self.policy)
        for name in ("organization", "organization_installations", "organization_owners", "current_user"):
            for defect in ("missing", "failed", "stale"):
                changed = copy.deepcopy(self.baseline)
                observation = controls.index(changed)[name]
                if defect == "missing":
                    changed["observations"].remove(observation)
                elif defect == "failed":
                    observation.update(exit_code=1, error={"status": 403})
                else:
                    observation["observed_at_utc"] = (self.now - timedelta(minutes=16)).isoformat()
                with self.subTest(name=name, defect=defect):
                    report = controls.preflight(proposal, changed, self.policy, self.baseline, self.now)
                    creation = next(c for c in report["changes"] if c["id"] == "create-Hexalith.Operations")
                    self.assertEqual(creation["result"], "blocked")

    def test_unapproved_identity_cannot_be_legitimized_as_new_baseline(self):
        for name, field, value in (("current_user", "id", 77), ("organization", "id", 77),
                                   ("organization", "login", "Other")):
            changed = copy.deepcopy(self.baseline)
            controls.index(changed)[name]["data"][field] = value
            proposal = controls.propose(changed, self.policy)
            with self.subTest(name=name, field=field):
                report = controls.preflight(proposal, changed, self.policy, changed, self.now)
                self.assertEqual(report["verification_result"], "blocked")
                self.assertTrue(report["issues"])
        changed = copy.deepcopy(self.baseline)
        controls.index(changed)["organization_owners"]["data"].append({"id": 77, "login": "new-owner"})
        report = controls.preflight(controls.propose(changed, self.policy), changed, self.policy, changed, self.now)
        self.assertIn("approved-owner-boundary-unproved", report["issues"])

    def test_annotated_tag_tree_is_peeled_and_inspected(self):
        name = "Hexalith.Operations"
        self.item(f"{name}.refs.tags")["data"] = [{"ref": "refs/tags/example", "object": {"type": "tag", "sha": "e" * 40}}]
        self.put(self.discovery, f"{name}.tag.{'e' * 40}", {"object": {"type": "tag", "sha": "f" * 40}})
        self.put(self.discovery, f"{name}.tag.{'f' * 40}", {"object": {"type": "commit", "sha": "a" * 40}})
        self.assertEqual(self.verify()["verification_result"], "pass")
        self.item(f"{name}.tag.{'f' * 40}")["data"]["object"] = {"type": "tag", "sha": "e" * 40}
        self.assertIn("private-workflow-tag-object-unproved", self.conditions(self.verify()))

    def test_empty_repository_does_not_prove_no_executable_workflow(self):
        self.item("Hexalith.Operations.refs.heads")["data"] = []
        self.assertIn("private-workflow-content-inventory-unproved", self.conditions(self.verify()))

    def test_unknown_selected_app_coverage_keeps_exact_proposal_blocked(self):
        proposal = controls.propose(self.retained, self.policy)
        manual = next(c for c in proposal["changes"] if c["id"] == "exclude-private-repositories-from-app-31615326")
        self.assertIn("installation.31615326.repositories", manual["baseline_observation_gaps"])
        self.assertTrue(manual["manual_only"])

    def test_readback_defaults_and_rule_order_do_not_hide_valid_policy(self):
        detail = self.item("Hexalith.Platform.ruleset.100")["data"]
        detail["rules"].reverse()
        detail["rules"][0]["parameters"]["allowed_merge_methods"] = ["merge", "squash", "rebase"]
        self.assertEqual(self.verify()["verification_result"], "pass")


class CollectorTests(unittest.TestCase):
    def setUp(self):
        self.policy = controls.load(controls.DEFAULT_POLICY)

    def test_discovery_caches_shared_annotated_tag_observations_per_repository(self):
        for scenario in ("shared-object", "shared-nested-object", "shared-unavailable-object"):
            calls = []
            names = {repo["full_name"]: repo["name"] for repo in self.policy["private_repositories"]}

            def transport(endpoint):
                calls.append(endpoint)
                path = controls.urlparse(endpoint).path
                if path == "/user":
                    return 200, {}, self.policy["administrator"]
                if path == "/orgs/Hexalith/installations":
                    return 200, {}, {"installations": [], "total_count": 0}
                for full in names:
                    prefix = f"/repos/{full}"
                    if path == prefix:
                        return 200, {}, {"id": 10000, "full_name": full, "private": True}
                    if path == prefix + "/git/matching-refs/heads/":
                        return 200, {}, [{"ref": "refs/heads/main", "object": {"type": "commit", "sha": "a" * 40}}]
                    if path == prefix + "/git/matching-refs/tags/":
                        return 200, {}, [{"ref": "refs/tags/first", "object": {"type": "tag", "sha": "e" * 40}},
                                         {"ref": "refs/tags/second", "object": {"type": "tag", "sha": ("g" if scenario == "shared-nested-object" else "e") * 40}}]
                    if path.startswith(prefix + "/git/tags/"):
                        sha = path.rsplit("/", 1)[1]
                        if scenario == "shared-unavailable-object":
                            return 404, {}, {"message": "Not Found"}
                        nested = scenario == "shared-nested-object" and sha != "f" * 40
                        return 200, {}, {"object": {"type": "tag" if nested else "commit", "sha": ("f" if nested else "a") * 40}}
                    if path.startswith(prefix + "/git/trees/"):
                        return 200, {}, {"truncated": False, "tree": []}
                    if path.startswith(prefix + "/"):
                        return 200, {}, []
                if path.startswith("/repos/"):
                    return 404, {}, {"message": "Not Found"}
                return 200, {}, []

            with self.subTest(scenario=scenario):
                discovery = controls.discover(self.policy, transport=transport)
                observed = controls.index(discovery)
                self.assertEqual(len(observed), len(discovery["observations"]))
                for full, name in names.items():
                    tag_shas = ("e", "f", "g") if scenario == "shared-nested-object" else ("e",)
                    for character in tag_shas:
                        endpoint = f"https://api.github.com/repos/{full}/git/tags/{character * 40}"
                        self.assertEqual(calls.count(endpoint), 1)
                        self.assertIn(f"{name}.tag.{character * 40}", observed)
                    if scenario == "shared-unavailable-object":
                        self.assertEqual(observed[f"{name}.tag.{'e' * 40}"]["error"]["status"], 404)
                    self.assertEqual(calls.count(f"https://api.github.com/repos/{full}/git/trees/{'a' * 40}?recursive=1"), 1)

    def test_pagination_follows_every_page_and_keeps_terminal_proof(self):
        seen = []
        def transport(endpoint):
            seen.append(endpoint)
            if len(seen) == 1:
                return 200, {"link": '<https://api.github.com/items?page=2>; rel="next"'}, [{"id": 1}]
            return 200, {}, [{"id": 2}]
        collector = controls.Collector(self.policy, transport)
        self.assertEqual(collector.get("items", "items"), [{"id": 1}, {"id": 2}])
        self.assertTrue(collector.observations[0]["pagination"]["complete"])
        self.assertEqual(collector.observations[0]["pagination"]["page_count"], 2)

    def test_page_two_failure_keeps_partial_data_and_blocks(self):
        calls = iter([(200, {"link": '<https://api.github.com/items?page=2>; rel="next"'}, [{"id": 1}]), (403, {}, {"message": "private error"})])
        collector = controls.Collector(self.policy, lambda _: next(calls))
        self.assertIsNone(collector.get("items", "items"))
        observation = collector.observations[0]
        self.assertEqual(observation["data"], [{"id": 1}])
        self.assertEqual(observation["error"]["status"], 403)
        self.assertFalse(observation["pagination"]["complete"])
        self.assertNotIn("private error", json.dumps(observation))

    def test_total_mismatch_cycle_unsafe_next_and_truncation_block(self):
        cases = [(200, {}, {"installations": [], "total_count": 1}, "installations", "pagination-total-mismatch"),
                 (200, {"link": '<https://api.github.com/items>; rel="next"'}, [], None, "pagination-cycle"),
                 (200, {"link": '<https://example.test/items>; rel="next"'}, [], None, "unsafe-pagination-link"),
                 (200, {}, {"tree": [], "truncated": True}, None, "truncated-tree")]
        for status, headers, data, list_key, expected in cases:
            with self.subTest(expected=expected):
                collector = controls.Collector(self.policy, lambda _: (status, headers, data))
                self.assertIsNone(collector.get("items", "items", list_key))
                self.assertEqual(collector.observations[0]["error"]["condition"], expected)

    def test_lossy_rule_parameter_projection_blocks_replacement_payload(self):
        baseline = controls.load(BASELINE)
        raw = controls.index(baseline)["Builds.main_ruleset_detail"]["data"]
        raw["rules"][0]["parameters"] = {"new_policy_field": {"preserve_me": True}}
        collector = controls.Collector(self.policy, lambda _: (200, {}, raw))
        self.assertIsNone(collector.get("Builds.main_ruleset_detail", "ruleset"))
        observation = collector.observations[0]
        self.assertEqual(observation["error"]["condition"], "lossy-ruleset-projection")
        baseline["observations"] = [o for o in baseline["observations"] if o["name"] != observation["name"]] + [observation]
        proposal = controls.propose(baseline, self.policy)
        change = next(c for c in proposal["changes"] if c["id"] == "builds-administrator-bypass")
        self.assertIsNone(change["payload"])

    def test_discarded_permission_values_block_collected_access_evidence(self):
        cases = [([{ "id": 11, "permissions": {"contents": "admin"}}], None),
                 ({"user": {"id": 11, "permissions": {"push": "future-value"}}}, None),
                 ({"installations": [{"id": 11, "permissions": {"future-scope": "write"}}],
                   "total_count": 1}, "installations")]
        for raw, list_key in cases:
            with self.subTest(raw=raw):
                collector = controls.Collector(self.policy, lambda _: (200, {}, raw))
                self.assertIsNone(collector.get("items", "items", list_key))
                self.assertEqual(collector.observations[0]["error"]["condition"], "lossy-permission-projection")
                self.assertEqual(collector.observations[0]["exit_code"], 1)

    def test_duplicate_paginated_items_block_observation(self):
        collector = controls.Collector(self.policy, lambda _: (200, {}, [{"id": 1}, {"id": 1}]))
        self.assertIsNone(collector.get("items", "items"))
        self.assertEqual(collector.observations[0]["error"]["condition"], "duplicate-paginated-result")

    def test_deploy_keys_tokens_and_untrusted_error_text_are_not_retained(self):
        value = {"id": 1, "key": "ssh-ed25519 PRIVATE-MATERIAL", "token": "secret", "read_only": False,
                 "login": "ghp_abcdefghijklmnopqrst", "permissions": {"repository": {"contents": "write"}}}
        sanitized = controls.sanitize(value)
        self.assertEqual(sanitized["permissions"], {"repository": {"contents": "write"}})
        for value in ("PRIVATE-MATERIAL", "secret", "ghp_abcdefghijklmnopqrst"):
            self.assertNotIn(value, json.dumps(sanitized))

    def test_workflow_source_is_audited_in_memory_and_not_retained(self):
        content = "permissions: write-all\nrun: echo ghp_abcdefghijklmnopqrst\n"
        raw = {"content": base64.b64encode(content.encode()).decode(), "path": ".github/workflows/test.yml", "sha": "a" * 40}
        collector = controls.Collector(self.policy, lambda _: (200, {}, raw))
        result = collector.get("workflow", "workflow", content_kind="workflow")
        self.assertEqual(result["audit"]["result"], "blocked")
        self.assertNotIn(content, json.dumps(collector.observations))
        self.assertNotIn("ghp_abcdefghijklmnopqrst", json.dumps(collector.observations))

    def test_transport_is_get_only_and_uses_no_token_argument(self):
        response = subprocess.CompletedProcess([], 0, 'HTTP/2.0 200 OK\nlink: <https://api.github.com/items?page=2>; rel="next"\n\n[]', "")
        with patch.object(controls.subprocess, "run", return_value=response) as run:
            self.assertEqual(controls.gh_get("https://api.github.com/items")[0], 200)
            argv = run.call_args.args[0]
            self.assertEqual(argv[argv.index("--method") + 1], "GET")
            self.assertNotIn("--input", argv)
        for url in ("https://example.test/items", "https://user:password@api.github.com/items", "http://api.github.com/items"):
            with self.subTest(url=url), self.assertRaises(ValueError):
                controls.gh_get(url)

    def test_cli_local_commands_never_invoke_transport_and_no_apply_command_exists(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(controls, "gh_get", side_effect=AssertionError("remote request")):
            output = Path(directory) / "proposal.json"
            self.assertEqual(controls.main(["propose", "--discovery", str(BASELINE), "--output", str(output)]), 0)
            self.assertFalse(json.loads(output.read_text())["mutation_authorized"])
        for command in ("apply", "discover"):
            completed = subprocess.run([sys.executable, str(Path(controls.__file__)), command], capture_output=True, text=True)
            self.assertEqual(completed.returncode, 2)

    def test_duplicate_json_keys_and_duplicate_observations_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "duplicate.json"
            source.write_text('{"id": 1, "id": 2}')
            with self.assertRaises(ValueError):
                controls.load(source)
        with self.assertRaises(ValueError):
            controls.index({"observations": [{"name": "same"}, {"name": "same"}]})


if __name__ == "__main__":
    unittest.main()
