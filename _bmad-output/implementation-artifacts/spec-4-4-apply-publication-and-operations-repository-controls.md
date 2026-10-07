---
title: 'Apply publication and operations repository controls'
type: 'chore'
created: '2026-10-07'
status: 'in-progress'
baseline_commit: 'bed37580a4213edb3e4698525e530b85aa4a80ea'
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
- [x] `.github/CODEOWNERS` -- assign `*`, `/.github/workflows/**` and `/.github/CODEOWNERS` to `@jpiquot`; publish to Platform `main` through a reviewed change so ownership is effective. Published by user-authorized, operator-reviewed PR #3; live content audit passes and GitHub returns no ownership errors. GitHub review submissions were empty; no formal PR approval is claimed.
- [x] `eng/repository-controls/desired-state.json` -- encode explicit repository names/actor IDs, main protection, separate tag creation/immutability rules, read base permissions and private-repository access policy.
- [x] `eng/repository-controls/controls.py` -- implement sanitized, paginated discovery, exact change payload proposals and effective-state verification; retain errors and detect stale baselines. Never auto-apply during discovery/verification.
- [x] `eng/repository-controls/test_controls.py` -- test the matrix plus broader bypass, missing CODEOWNERS, inherited writers, writable workflow overrides and partial API results.
- [x] `eng/repository-controls/README.md` -- document ordered application, App selection changes preserving existing access, credential custody collection, rollback and Administrator recovery. Read-only defaults do not prevent workflow permission overrides; verify workflow contents too.
- [ ] `_bmad-output/implementation-artifacts/evidence/epic-4/4-4/` -- retain reviewed payloads, fresh pre-change checks and readbacks. Apply Platform rulesets, Builds bypass and organization read default; create private repositories and verify App exclusion, only named writers, token settings and credential custody. Five live settings changes passed immediate readback, recorded in `20261007T135137Z-live/`. Seven all-repository App selections, Travis's selected coverage, PAT inventories, private repository creation/configuration and both owners' custody evidence remain outstanding.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- synchronize 4.4 status; mark done only after operational acceptance. Remains `in-progress`.

**Acceptance Criteria:**
- Given Platform `main`, when controls are inspected, then PR/code-owner review, force-push/deletion denial and only Administrator bypass are active, and workflow/CODEOWNERS ownership resolves to Administrator.
- Given Platform tags, when policies are inspected, then only Administrator or a qualified publication identity may create them and no actor bypasses update/deletion denial.
- Given Builds, when its ruleset is compared, then only Administrator bypass remains and existing rules/checks are preserved.
- Given Hexalith and the two private repositories, when effective access and workflow configuration are verified, then base permission is read, only the two named owners write, workflows have no repository write authority, and no writable PAT/App/deploy key exists outside their devices.

## Implementation Notes

- 2026-10-07: After reviewing the prepared artifacts and being asked to authorize live discovery, CODEOWNERS publication and application of the prepared controls, the user replied "proceed". This supersedes the no-push/no-remote restriction for those Story 4.4 operations. Preserve the frozen policy; record unsupported/manual checks and incomplete custody as outstanding.

- The initial preparation followed the rendered step-03 rule: "No push. No remote ops." Its retained discovery checks remain historical blocked evidence. The later explicit authorization above enabled the scoped live operations. Local verification alone never marks the story done.
- 2026-10-07: The user replied "provide it yourself" to the evidence request. Collected fresh CLI discovery, the connected App's 67-repository inventory, credential-store file metadata and browser sign-in state. These observations are not authenticated custody statements from either owner. No private repository was created because App exclusion prerequisites remain unproved; no integration, unrelated credential, ownership or billing setting was changed.

## Spec Change Log

## Review Triage Log

- 2026-10-07 parent implementation audit (not a completed workflow review): resolved lossy Builds rule/parameter projection, successful PAT grant/request authorization and scope checking, reviewed proposal method/endpoint/payload integrity binding, preservation of unrelated App repository access, missing creation prerequisites and pinned actor/organization/owner checks, and shared annotated-tag traversal. Mocked traversal now retains each repository's shared or nested tag object once, including failed observations.
- Live inspection satisfies the Platform main/tag settings and Builds preservation criteria. The private repository/access/custody criterion remains unmet. Step-03 tasks and acceptance verification therefore remain incomplete; step-04 has not started and the story stays `in-progress`.
- Live failed App queries revealed a diagnostic issue: unavailable coverage was compared as an empty selection and described as changed access. Verification now reports unproved preservation without alleging a change; two regression tests pass. It still blocks acceptance.
- 2026-10-07 resumed implementation audit: every settings proposal now binds fresh, complete GET observations of Administrator, organization/Free plan and both approved owners. Platform main's code-owner review change additionally binds passing published CODEOWNERS and an empty resolution-error inventory. Three regression tests pass. Fresh discovery confirms existing public controls; App/PAT/private repository/custody dependencies remain blocked, so step-03 acceptance remains incomplete.
- 2026-10-07 further implementation audit: discarded permission metadata, unknown/empty PAT permission extent and unknown effective permission levels now block verification. Positive collaborator write flags remain effective even when a permission response omits them. Builds and App preservation require complete successful historical GET baselines without imposing freshness on historical evidence. Seven regression tests pass; synthetic observations are copied so fixtures cannot change the approved writer policy. Fresh discovery still blocks private repository/App/PAT/custody acceptance; no additional live mutation was made.

## Design Notes

Repository names and all-tag coverage are implementation choices. Owners retain power to edit rulesets. GitHub configuration changes and repository creation have external effects; the live operations use the user's subsequent explicit authorization.

- Local verification establishes consistency of supplied observations and owner statements only; it never asserts operational acceptance or authorizes a mutation. Workflow source is audited in memory and retained as hashes/results; ambiguous YAML is blocked. Annotated tags are peeled to commit/tree objects, and initially empty repository workflow inventories remain unproved until complete usable refs/trees are observed after App exclusion.

## Verification

- `python3 -m unittest discover -s eng/repository-controls -p 'test_*.py'` -- meaningful policy/error cases pass.
- Run `controls.py` verification against fresh GitHub discovery and owner custody evidence -- require measured evidence for every acceptance criterion; unsupported observations remain blocked.
- `git diff --check` -- no whitespace errors; sprint YAML parses with unrelated statuses preserved.
- API contracts: [rulesets](https://docs.github.com/en/rest/repos/rules), [workflow permissions](https://docs.github.com/en/organizations/managing-organization-settings/disabling-or-limiting-github-actions-for-your-organization).
- Local preparation: 44 tests pass. Retained-baseline preflight and verification both return `blocked` (exit 1); no fresh GitHub or owner custody evidence was collected. See `evidence/epic-4/4-4/20261007T111755Z-local-preparation/summary.md`.
- Authorized live run: 46 tests pass after the diagnostic correction. Each of the five settings mutations passed fresh preflight and immediate readback. Post-change full verification returns `blocked` (exit 1), `complete: false`, `operational_acceptance: false`. See [live evidence](evidence/epic-4/4-4/20261007T135137Z-live/summary.md).
- Resumed implementation: 49 tests pass; whitespace and sprint YAML checks pass. Fresh read-only discovery retains 44 observations; preflight and full verification remain `blocked` (exit 1), with no outstanding inspected public-control finding. No mutation, commit, push or owner custody assertion was made. See [resumed verification evidence](evidence/epic-4/4-4/20261007T143111Z-resumed-verification/summary.md).
- Further implementation: 56 tests pass, including the frozen I/O matrix and seven regression tests for preservation/permission gaps. Fresh GET discovery at `2026-10-07T15:38:43.177510+00:00` retains 44 observations; preflight and full verification remain blocked, `complete: false`, `operational_acceptance: false`. All eight App coverage reads return 403 and both PAT inventories return 404. The connector proves only its own 67-repository selection; Travis remains 403. Browser control fails with `Transport closed`, and no running Chrome/debug session is available. The two private repositories and both owners' authenticated custody evidence remain outstanding; Story 4.4 stays `in-progress`. See [further verification evidence](evidence/epic-4/4-4/20261007T153815Z-implementation-verification/summary.md).
