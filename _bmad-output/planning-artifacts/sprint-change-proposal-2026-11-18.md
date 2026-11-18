# Sprint Change Proposal — 2026-11-18: Stop over-proving the CI lifecycle

- **Trigger:** Story 2.8, *Run CI integration on disposable runners with guaranteed cleanup*
- **Issue type:** failed approach requiring a different solution
- **Mode:** batch
- **Scope:** moderate (backlog rescope and document edits; no rollback)
- **Status:** approved 2026-11-18. E1–E8 applied in Platform; E9–E10 in [Hexalith.AI.Tools PR #2](https://github.com/Hexalith/Hexalith.AI.Tools/pull/2)

## 1. Issue summary

The CI lifecycle work has built proof machinery far beyond what planning requires. That machinery now blocks delivery instead of protecting it.

Evidence collected on 2026-11-18:

- **Cost of 2.8:**
  - Story 2.8 has been open since 2026-10-31, through three review rounds and 35 spec commits.
  - Its spec is 202 KB, plus a 704 KB archive. The bmad-build target for a spec is 900–1,600 tokens.
- **Today's attempts:**
  - The first hosted attempt was refused with HXP020.
  - The second, [44262928117](https://github.com/Hexalith/Hexalith.Parties/actions/runs/44262928117), stopped on one flaky isolated test (420 of 421 passed).
  - Retrying it needed an owner exception. The session spent about 430k agent tokens.
- **HXP020 has no planning basis:**
  - Platform refused Parties' unchanged declaration under the ordinary profile. Platform source hardcodes declaration hashes and image digests: 84 distinct SHA-256 literals in `src/Hexalith.Platform.Composition`.
  - The allowlists arrived in `338a6afc` (2026-11-04) and `a04df42f` (2026-11-05). No planning requirement asks for them.
- **Evidence in git:**
  - Git tracks 1.2 GB of evidence in 22,681 files, 512 MB of it for Story 2.8.
  - The shared rule says "do not commit bulk diagnostic evidence" (`references/Hexalith.AI.Tools/hexalith-llm-instructions.md:361`).
- **The harness 2.8 extends is scheduled for deletion:**
  - CiCd deletes `domain-ci-v2.yml` (`references/Hexalith.CiCd/_bmad-output/specs/spec-cicd/SPEC.md:35`).
  - CiCd owns every module's `.github/workflows/` (`SPEC.md:144-150`) and forbids CI-of-CI checks (CAP-10).
  - CiCd story 21 removes Parties `tests/ci-lifecycle/` and the controller scripts (`spec-21-migrate-parties.md:56`).

### What planning actually mandates

| Mechanism | Mandated? | Source |
| --- | --- | --- |
| CI cleanup after success, failure or cancellation; attachments end before cleanup; leftovers reported | Yes | FR-4 (PRD:145-147), SM-C4 (PRD:396), AD-10 (ARCH:173) |
| A dedicated hosted scenario harness, signal choreography, re-proof on every new tuple | No | Added during 2.8 implementation and review |
| No result reuse across acceptance decisions | Yes, for candidate decisions | POL:79 (Stories 3.1 and 3.9; not a 2.8 criterion) |
| Hardcoded declaration-hash and image-digest allowlists, "measured cohorts" | No | Not in PRD, architecture, epics or contracts |
| Exact pins | Partly | AD-4 (ARCH:105) pins the catalog and a tool version *range* |
| SHA-256 evidence manifests and receipts for all evidence | Added later | 2026-11-08 correction (PRD:160, ARCH:255) |
| Re-run only after a source change | Release CI only | Shared instructions :354-355; POL:51 allows retries |
| Extra `platform-current-package` profile | No | Exists only to route around the allowlist |

## 2. Impact analysis

- **Epic 2:**
  - 2.8 can close now on the hosted proof it already has.
  - 2.10 is rescoped and 2.14 is extended.
  - The other Epic 2 stories are done and unaffected.
- **Epic 3:**
  - Story 3.12 consumes 2.10's lifecycle scenarios (EP:2054), which the rescoped 2.10 still produces.
  - Policy v1 candidate rules for 3.1 and 3.9 are unchanged.
- **Epic 4:** no dependency on 2.8 or 2.10 (EP:703).
- **PRD:** the MVP, FR-4 and SM-C4 are unchanged. One clarification is added and the 2026-11-08 execution note is amended.
- **Architecture:** one sentence is added to AD-10, and two 2026-11-08 execution bullets are amended.
- **Policy v1:** unchanged. Its 2.10 obligation, to qualify the attachment, expiry, withdrawal and retention examples (POL:231-232), is met on the reference composition.
- **Code:**
  - Nothing is rolled back. Owner receipts, job-bound attachment, withdrawal and cleanup stay as implemented.
  - 2.14 removes the hash allowlists.
  - CiCd story 21 removes the Parties harness.
- **Builds:**
  - The 2.8 same-job attach fallback is needed because Builds 6.3 clears GitHub job variables from the AppHost environment.
  - It reached Builds `main` in `37e1a600` through `/pushall`, without review. The 2.8 Step 04 review covers it, and the next normal Builds release ships it.

## 3. Recommended approach

**Direct adjustment** (option 1):

- **Rollback** is rejected: it would throw away working, tested code for no gain in safety.
- **An MVP review** is not needed.

The estimates are:

- **Effort:** low for the document edits; medium for the extended 2.14.
- **Risk:** low.
- **Timeline:** 2.8 closes after one short review, and 2.10 stops waiting on a hosted harness.

This needs no new epic or stories. The existing ones already cover every item:

- 2.14 covers the allowlists.
- CiCd story 21 covers the harness.
- The document edits cover the remaining rules.

## 4. Detailed change proposals

### Stories (`_bmad-output/planning-artifacts/epics.md`)

**E1. Story 2.8: add closure after the "Execution boundary" paragraph.** The acceptance criteria are unchanged.

NEW:
> **Closure (2026-11-18 course correction):** By owner decision, the acceptance criteria are met by hosted Parties runs [42521615234](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42521615234) (pass, fail, retry with peer, partial startup, same-job and cross-job attachment), [42526256372](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42526256372) (startup cancellation) and [42528046681](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42528046681) (execution cancellation). All three ran on Parties `926c1c3f` with Builds 4.44.13. The workflow passes only the read-only registry pull secret. The Builds 6.3 same-job attach fallback is covered by resource-free tests and the Step 04 review. Re-proving on the newest tuple is not required. See [sprint-change-proposal-2026-11-18.md](sprint-change-proposal-2026-11-18.md).

**E2. Story 2.10: rescope.**

Replace the "Execution boundary" paragraph and the "Blocked (2026-11-10, user decision)" paragraph. Their history remains in git and in the 2.10 spec. NEW:
> **Scope (2026-11-18 course correction):** Run the scenario list below locally on the reference composition with the pinned released tool, recording one compact summary per scenario. For CI, count Story 2.8's hosted runs under their original identities, plus Parties' normal CI through the shared CiCd workflow (success and failure with the always-run cleanup report). No dedicated hosted scenario workflow or controller is built. Tool acceptance and pins follow Story 2.14's version rule. **Depends on:** Story 2.14; the CI part also depends on CiCd story 21.

Acceptance criterion 1:
- OLD: `**When** scenarios run locally and in CI`
- NEW: `**When** scenarios run locally, and CI runs its success, failure and cancellation paths`

Acceptance criterion 3:
- OLD: `**Then** the tool version that carries it is recorded as Platform-accepted and pinned in Platform and in Parties`
- NEW: `**Then** the tool version that carries it is recorded as Platform-accepted and pinned by released version in Platform and in Parties, under Story 2.14's version compatibility rule`

**E3. Story 2.14: extend.**
- **Repo line:** `Hexalith.Platform` becomes `Hexalith.Platform, Hexalith.Parties (owner review)`.
- **Origin line:** append "2026-11-18: also removes the declaration and image allowlists behind HXP020 ([proposal](sprint-change-proposal-2026-11-18.md))."
- **New acceptance criterion:**

> **Given** owner-module declarations and images
> **When** Composition admits a package-mode run
> **Then** it uses no hardcoded declaration-hash or image-digest allowlist and no measured image cohort (`PlatformPartiesPackageCatalogQualification`, `PlatformCurrentOwnerProof`)
> **And** each test project declares one package integration profile, so `platform-current-package` is retired

### PRD (`prds/prd-platform-2026-09-27/prd.md`)

**E4. FR-4: add a bullet after "Cleanup can be retried safely…" (line 147).**

NEW:
> - On a disposable GitHub-hosted runner, an always-run cleanup step that stops the job's owned runs and reports leftovers is the cleanup record, and the runner's disposal at job end is the backstop. Proving this does not need a dedicated hosted scenario workflow.

**E5. Line 160, the execution clarification: two sentence replacements.**

Sentence 1:
- OLD: "Pin qualification inputs for each campaign, use focused checks before broad acceptance runs, and preserve evidence through verifiable immutable receipts."
- NEW: "Pin qualification inputs for each campaign and use focused checks before broad acceptance runs. Keep evidence compact: a summary in the spec and links to CI runs and artifacts, with no bulk logs or archives in git."

Sentence 2:
- OLD: "Full reference qualification remains necessary before accepting runner lifecycle capability."
- NEW: "Story 2.10, as rescoped on 2026-11-18, is the reference qualification required before accepting runner lifecycle capability."

### Architecture (`architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md`)

**E6. AD-10 "Stop and cleanup" (line 173): append a sentence.**

NEW: "On a disposable hosted runner, the job's always-run cleanup step and its leftover report are the cleanup record; runner disposal is the backstop."

**E7. Line 255: replace.**
- OLD: "Review active source/configuration/test/documentation changes and verify unchanged historical evidence with complete manifests, hashes and original receipts. Missing proof or an integrity discrepancy reopens only the affected audit scope. Preserve first failures and all archive bytes; auditing an audit does not create a recursive source-reading obligation."
- NEW: "Review active source, configuration, test and documentation changes. Historical evidence stays as committed and is not re-verified unless a decision consumes it. New evidence is a compact summary with links to CI runs and artifacts."

**E8. Line 256: replace the last sentence.**
- OLD: "Lifecycle cancellation scenarios retain separate real workflow runs and job-owned cleanup."
- NEW: "Lifecycle cancellation is proven by Story 2.8's recorded hosted runs and Builds' lifecycle tests; module CI keeps job-owned always-run cleanup."

### Shared instructions (`references/Hexalith.AI.Tools/hexalith-llm-instructions.md`)

**E9. Lines 354-355: append to the bullet.**

NEW: "Outside release CI, one re-run of a failure that does not reproduce is allowed; record the flaky test as a bug instead of adding gates."

**E10. Lines 361-362: append to the bullet.**

NEW: "Keep story specs short: record decisions and current state, and leave run-by-run history to commits and CI run links."

### Sprint status

**E11.** No epics or stories are added or removed:
- `2-8-…` becomes `review` when its Step 04 starts.
- 2.10 stays `in-progress` and 2.14 stays `backlog`.

## 5. Implementation handoff

**Scope:** moderate. The Developer agent (bmad-build) executes. The owner approves PRs and merges as usual.

1. **Apply edits E1–E8 and E11** in Platform.
2. **Deliver E9–E10** as a normal Hexalith.AI.Tools change. Its `main` pushes can trigger a release.
3. **Close Story 2.8:**
   - Record the closure in its spec.
   - Run Step 04 on only the remaining delta: Builds `37e1a600` (attach fallback and tests) and the Platform v2.1.11 current-owner admission.
   - Mark 2.8 done.
   - Drop Parties proof commit `063400a5` and its scratch clone. Dispatch no more 2.8 hosted runs.
4. **Story 2.14 (extended)** is next.
5. **Story 2.10 (rescoped):**
   - Run the local scenarios now.
   - Run the CI part after CiCd story 21 and Story 2.14.

**Success criteria:**
- 2.8 is done without another hosted run.
- After 2.14, adopting a Builds or owner-module release, including a new image or declaration, needs no Platform C# edit, and Parties declares one package profile.
- New evidence commits are summaries and links only.
- 2.10 closes without a dedicated hosted scenario workflow.

## Checklist record

| Item | Status |
| --- | --- |
| 1.1–1.3 Trigger, problem, evidence | [x] Done |
| 2.1–2.5 Epic impact (Epic 2 adjusted; Epics 3 and 4 unaffected; no resequencing) | [x] Done |
| 3.1 PRD | [x] Done (E4, E5) |
| 3.2 Architecture | [x] Done (E6–E8) |
| 3.3 UX | [N/A] |
| 3.4 Other artifacts (CI, shared instructions) | [x] Done (E9, E10); CiCd story 21 already owns the harness removal |
| 4.1 Direct adjustment | Viable: low–medium effort, low risk |
| 4.2 Rollback | Not viable: discards working code |
| 4.3 MVP review | Not needed |
| 4.4 Selected path | Direct adjustment |
| 5.1–5.5 Proposal components | [x] Done |
| 6.1–6.2 Review | [x] Done |
| 6.3 Approval | [x] Approved by the owner on 2026-11-18 (approve all) |
| 6.4 Sprint status | [x] No epic or story changes; 2.8 moves to `review` when its Step 04 starts |
| 6.5 Handoff | Section 5 |
