---
title: 'Apply publication and operations repository controls'
type: 'chore'
created: '2026-10-07'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
story_key: '4-4-apply-publication-and-operations-repository-controls'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Platform `main` is unprotected, Hexalith's base permission is write, and Builds permits bypass by every owner and QuentinDV. Operations and notification repositories are absent; installed Apps would inherit write access.

**Approach:** Establish Story 4.4's GitHub trust boundary with reviewed repository rulesets, CODEOWNERS, read base permissions, and private `Hexalith.Operations` and `Hexalith.Notifications` repositories. Supply repeatable discovery, change proposals and verification, then apply and measure the controls.

## Boundaries & Constraints

**Always:** Administrator is `jpiquot` (6775094); private repository writers are exactly `jpiquot` and second owner `tinouit` (183023925), as already approved. Keep GitHub Free; no billing change. Set organization base permission to read. Platform `main` requires a PR, at least one approving code-owner review, and blocks force-push/deletion; its sole bypass is Administrator. Preserve Builds' existing checks/rules while restricting its bypass to Administrator. Protect every Platform tag: separate creation authorization from update/deletion rules with no immutability bypass. Initially only Administrator creates tags; the publication workflow and its restricted identity are owned by Story 4.12 and receive no speculative bypass here. Private repository workflow tokens remain read-only with PR approval disabled; their content/workflow/admin credentials stay on named writers' devices. Exclude installed Apps from those repositories before adding executable content. Verify effective permissions, including inherited grants, rather than just direct collaborators. Require owner custody evidence for writable credentials; inaccessible PAT inventory is an unresolved verification dependency.

**Never:** Change organization ownership, buy Team, revoke unrelated integrations or credentials, alter Builds source/workflows, deploy executors, deliver notifications, or touch cluster resources. Do not equate local checks or HTTP 404 with operational acceptance. Do not store credentials, private keys or secret values in Git. Keep Story 4.4 incomplete until all live controls and custody evidence pass.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Fresh baseline | Current settings match reviewed proposal | Apply named changes and retain measured readback | Drift stops affected changes; rediscover/review |
| Tag creation bypass | Administrator can create tags | Existing tags still cannot be updated/deleted | Separate immutable ruleset has no bypass |
| New private repository | An installation selects all repositories | Exclude it while preserving existing repository access | Do not populate executable content or claim acceptance |
| Incomplete observation | PAT inventory inaccessible, pagination incomplete, or API rejects a rule | Explicit blocked verification with retained evidence | Never infer absence or weaken the policy |

</frozen-after-approval>

## Code Map

- `.github/CODEOWNERS` -- absent; no root workflows currently exist.
- `eng/repository-controls/` -- new controls; reuse `eng/` standard-library/unittest convention.
- `_bmad-output/implementation-artifacts/evidence/epic-4/4-4/20261007T101349Z/discovery.json` -- sanitized GET baseline: two owners, no teams/outside collaborators, seven all-repository Apps; PAT queries unavailable.
- GitHub `Hexalith/Hexalith.Builds` ruleset `19196298` -- change bypass only; retain Codacy, SonarCloud and commitlint checks and all other settings. Its submodule stays untouched.
- `_bmad-output/specs/spec-platform/brownfield.md` and `.memlog.md` -- approved two-writer boundary; cluster decisions do not override it.

## Tasks & Acceptance

**Execution:**
- [ ] `.github/CODEOWNERS` -- assign `*`, `/.github/workflows/**` and `/.github/CODEOWNERS` to `@jpiquot`; publish to Platform `main` through a reviewed change so ownership is effective.
- [ ] `eng/repository-controls/desired-state.json` -- encode explicit repository names/actor IDs, main protection, separate tag creation/immutability rules, read base permissions and private-repository access policy.
- [ ] `eng/repository-controls/controls.py` -- implement sanitized, paginated discovery, exact change payload proposals and effective-state verification; retain errors and detect stale baselines. Never auto-apply during discovery/verification.
- [ ] `eng/repository-controls/test_controls.py` -- test the matrix plus broader bypass, missing CODEOWNERS, inherited writers, writable workflow overrides and partial API results.
- [ ] `eng/repository-controls/README.md` -- document ordered application, App selection changes preserving existing access, credential custody collection, rollback and Administrator recovery. Read-only defaults do not prevent workflow permission overrides; verify workflow contents too.
- [ ] `_bmad-output/implementation-artifacts/evidence/epic-4/4-4/` -- retain reviewed payloads, fresh pre-change checks and readbacks. Apply Platform rulesets, Builds bypass and organization read default; create private repositories and verify App exclusion, only named writers, token settings and credential custody. Record manual-only or inaccessible steps as outstanding.
- [ ] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- synchronize 4.4 status; mark done only after operational acceptance.

**Acceptance Criteria:**
- Given Platform `main`, when controls are inspected, then PR/code-owner review, force-push/deletion denial and only Administrator bypass are active, and workflow/CODEOWNERS ownership resolves to Administrator.
- Given Platform tags, when policies are inspected, then only Administrator or a qualified publication identity may create them and no actor bypasses update/deletion denial.
- Given Builds, when its ruleset is compared, then only Administrator bypass remains and existing rules/checks are preserved.
- Given Hexalith and the two private repositories, when effective access and workflow configuration are verified, then base permission is read, only the two named owners write, workflows have no repository write authority, and no writable PAT/App/deploy key exists outside their devices.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Repository names and all-tag coverage are implementation choices. Owners retain power to edit rulesets. GitHub configuration changes and repository creation have external effects; local preparation does not authorize them.

## Verification

- `python3 -m unittest discover -s eng/repository-controls -p 'test_*.py'` -- meaningful policy/error cases pass.
- Run `controls.py` verification against fresh GitHub discovery and owner custody evidence -- every acceptance criterion has measured evidence; unsupported observations remain blocked.
- `git diff --check` -- no whitespace errors; sprint YAML parses with unrelated statuses preserved.
- API contracts: [rulesets](https://docs.github.com/en/rest/repos/rules), [workflow permissions](https://docs.github.com/en/organizations/managing-organization-settings/disabling-or-limiting-github-actions-for-your-organization).
