---
title: 'Retire historical tuple-replay lanes and decouple Platform from release identities'
created: '2026-11-15'
status: 'approved-applied-to-planning'
scope: 'moderate'
mode: 'batch'
approval_basis: 'User chose "Retire old lanes" and "Version-only pins" in this session, then invoked bmad-correct-course with the argument "apply changes".'
main_repair: 'deferred by user decision ("Leave main alone"); owned by Story 2.13 if still needed when it starts'
---

# Sprint change proposal: tuple CI simplification

## 1. Issue summary

The user asked why so many `qualification/*` branches are created and never cleaned up, and whether this is over-engineered. Later in the same conversation they observed that implementation work in other Hexalith modules keeps forcing edits to `eng/builds-tuple.json`, which signals too much coupling between modules.

**Mechanism.** `.github/workflows/tuple-ci.yml` runs the candidate code in three lanes:

| Lane | What it pins | Cases |
| --- | --- | --- |
| Current | the candidate's own pins | n/a |
| Legacy | Builds 4.44.3 / `fc212224` | 349 |
| Pre-extension | Builds 4.45.0 / `27145e52`, EventStore SDK 4.1 / `cab8363c` | 412 |

A package-reader binding adds SDK 4.2 reader inputs on top. Each historical lane must check out a *separate real commit*. That commit has to equal the candidate except for an allowlist of pin files, which `eng/legacy_tuple_source.py` `ALLOWED`, `eng/pre_extension_tuple_source.py` and `eng/package_reader_source.py` enforce. The candidate then names those three commits in its descriptors. GitHub Actions can fetch only commits reachable from a ref, so every Platform change, even a documentation edit, mints three sibling commits and pushes three branches.

**Evidence (observed read-only on 2026-11-15):**

- Origin holds at least 105 archived branches: 81 `archive/qualification-branches/*` and 24 `archive/qualification/*` tags. All were created from 2026-11-12 to 2026-11-14 in legacy, package-reader and pre-extension triples.
- `/pushall` merges every branch into the default branch. That includes these sibling commits, which were never meant to be merged. They differ only in pin files, so the 3-way merge applies their historical pins without a conflict. Main's history contains 13 such merges.
- **Main is currently broken.** `origin/main` `0c3f55d1` pins Builds `27145e52` (4.45.0) instead of `e2bbbd08` (6.0.5). It also pins EventStore `cab8363c` instead of `df976e00`, Parties `ebe1739d` instead of `a494093d`, tool 4.45.0, and the 349-case profile. The last tree with current pins is `3bda4d79`. Exactly 8 paths differ from it.
- Tuple CI failed on 10 consecutive main pushes, from `81c14686` to `0c3f55d1`, after the last green run on `1321febb` at 05:59 UTC. On `00ed0607`, `resolve-sources` reports "The original 412 controls or their positive counts changed." The same code passed as PR `94e17e74`.
- **Coupling.** `eng/builds-tuple.json` pins one exact Builds release with about 12 identities: the release, run and artifact IDs, six sha256 values and an embedded manifest. Composition source compiles in release identities. For example, `PlatformPartiesValidationContext.IsPublishedPair` hard-codes the AppHost and Tooling DLL hashes of each admitted Builds release. The counts are 132 unique 40- or 64-hex literals in `src/**/*.cs`, 94 in tests and 57 in `eng/*.py`. Since 2026-11-01, 180 of 706 Platform commits touched `builds-tuple.json` and 311 touched tuple descriptors or scripts.
- **The historical lanes protect no consumer.** No module pins Builds 4.44.3 or 4.45.0. Module Builds gitlinks and tools range from 4.48.2 to 6.0.3. The legacy and pre-extension descriptors declare `qualificationCredit: false`, and the package-reader verifier returns `runtimeAcceptance: false` and `candidateAcceptance: false`. None of them contributes to any acceptance decision.
- The `Platform main reviewed changes` ruleset requires no status checks, so removing jobs needs no ruleset change.

**Issue type:** a technical limitation discovered during implementation, made worse by a tooling interaction (`/pushall`).

## 2. Impact analysis

### Checklist status

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Trigger | [x] | User review of branch sprawl and coupling. The stories affected are 3.2 (in progress) and 2.10 (in progress), plus the release-workspace chore (in review). |
| 1.2 Problem type | [x] | Technical limitation and tooling interaction |
| 1.3 Evidence | [x] | See section 1 |
| 2.1 Current epic (2) | [x] | It can complete. Two stories are added. |
| 2.2 Epic changes | [x] | Add Stories 2.13 and 2.14 |
| 2.3 Future epics | [x] | Epics 3 and 4 consume CI results but not the historical lanes. Their release-record binding (AD-4, AR-12) is preserved by Story 2.14's lock-file identities. |
| 2.4 Obsolete or new epics | [N/A] | None |
| 2.5 Resequencing | [x] | Do 2.13 before 2.14. Both precede Story 3.2's remaining Platform adoption, which no longer needs L/M companions once 2.13 lands. |
| 3.1 PRD | [N/A] | No PRD requirement names historical tuple replay. The MVP is unchanged. |
| 3.2 Architecture | [!] | Rewrite one *CI qualification execution* bullet in the Platform spine |
| 3.3 UX | [N/A] | None |
| 3.4 Secondary artifacts | [!] | Story specs 2.10 and 3.2 and the release-workspace spec. Docs, workflow and `eng/` scripts belong to the Story 2.13 implementation. |
| 4.1 Direct adjustment | [x] | Viable. Effort is medium (2.13) plus medium (2.14). Risk is low (2.13) and medium (2.14). |
| 4.2 Rollback | [N/A] | Nothing completed needs a revert. The `/pushall` damage on main is a tree repair, not a story rollback. |
| 4.3 MVP review | [N/A] | Not needed |
| 4.4 Selected path | [x] | Direct adjustment |

### Epic impact

Epic 2 gains Story 2.13, which retires the historical tuple-replay lanes, and Story 2.14, which pins Builds and owner modules by released version. Epic 2 stays the right home because these changes concern Platform's own CI integration (AR-12).

### Story impact

- **Story 3.2 (in progress):** its open Platform-adoption task requires the "refreshed same-source legacy L/M process". Once 2.13 lands, adoption goes through the current-tuple lane only. Until then, the existing process still gates CI, so work in flight is not invalidated.
- **Story 2.10 (in progress, blocked on releases):** it recorded 349 legacy and 412 pre-extension passes as history. Its final qualification records only the current-tuple lane after 2.13 lands. The completed task that preserved the `legacy-public-cohort` check context is historical and stays as written.
- **Release-workspace chore (`spec-update-and-release-workspace.md`, in review):** its task to "repair historical same-code companions" and its verification commands for the legacy and pre-extension controls are superseded by 2.13.

### Artifact conflicts

- Platform architecture spine, *CI qualification execution*: the bullet that allows concurrent current and legacy lanes "retaining existing required check names" must reflect that the historical lanes are retired.
- PRD and UX: none.

### Technical impact

- 2.13 edits about 22 files: `tuple-ci.yml`, the three historical-source modules and their tests, two descriptors and the frozen historical suite profiles. It moves the helpers that remaining code imports into `current_tuple_source.py` and the other remaining modules, and it updates `run_tuple_ci.py`, `file_host_catalog.py`, `PlatformPartiesPackageFixture.cs`, `tests/source-release/tuple-workflow.test.mjs` and the docs that require the same-code L/M proof.
- 2.14 touches Composition source and tests (`IsPublishedPair` and its compatibility matrix), `eng/builds_tuple.py` and its binding, and restore settings (lock files and locked mode).
- After 2.13, no qualification branches or tags are created. `/pushall` needs no change because nothing remains for it to merge. The archived tags stay until the owner decides otherwise. The Story 4.17 broker-candidate tags are out of scope.

## 3. Recommended approach

**Direct adjustment:** add two stories to Epic 2.

- **Rationale:** the historical lanes have high recurring cost and no consumer. Their cost is three commits and three branches per change, main breakage through `/pushall`, two extra CI jobs per run, and about 430 lines of verifier code plus tests. They grant no qualification credit. Pinning by version moves artifact integrity into NuGet's lock-file content hashes, which `restore` maintains automatically. A dependency release then becomes a version bump instead of a Platform source edit.
- **Effort:** 2.13 is medium, about one focused build iteration. 2.14 is medium because it touches compiled admission code.
- **Risk:**
  - 2.13 is low. It removes regression coverage only for Builds 4.44.3 and 4.45.0 with SDK 4.1, which no module uses.
  - 2.14 is medium. It replaces in-repo release-identity proof with lock-file hashes and must keep the expected-versus-loaded identities that AR-12 requires.
- **Timeline:** neutral to positive. It removes a recurring cost from every remaining Platform story.
- **Main repair:** by user decision, main is not repaired now. Story 2.13 owns the repair if main still carries the historical pins when 2.13 starts.

## 4. Detailed change proposals

### 4.1 Epics: add Story 2.13 (after Story 2.12)

```
NEW:
### Story 2.13: Retire the historical tuple-replay lanes from Platform CI
(full text as applied in epics.md)
```

The story requires the following:
- Only `resolve-sources` and the current `tuple` lane run. `tuple-verdict` requires both, and no job checks out any commit other than the candidate.
- No Platform change needs companion commits, descriptors or extra refs.
- The removed modules are gone, the remaining controls pass, main's pins are restored if needed, the docs are updated and the leftover local qualification branches are deleted.

Rationale: section 1.

### 4.2 Epics: add Story 2.14 (after Story 2.13)

```
NEW:
### Story 2.14: Pin Builds and owner modules by released version
(full text as applied in epics.md)
```

The story requires the following:
- Version-only Builds and owner pins.
- Lock files in locked mode as the integrity source.
- No Builds or owner release-identity literals in `src/`.
- Adoption of a release as a version bump plus regenerated lock files.
- AR-12 expected and loaded identities preserved.

### 4.3 Architecture spine, *CI qualification execution*

```
OLD:
- CI implementation may resolve independent current/legacy lane inputs in one
  preparation job and run the lanes concurrently, while retaining existing
  required check names and a fail-closed combined gate. This is an implementation
  task, not a claim that the optimization is deployed. Lifecycle cancellation
  scenarios retain separate real workflow runs and job-owned cleanup.

NEW:
- Platform tuple CI runs only the current-tuple lane, on the candidate commit
  itself, behind a fail-closed combined gate (2026-11-15 correction). The legacy,
  pre-extension and package-reader replay lanes are retired: they pinned Builds
  4.44.3 and 4.45.0, which no module uses, and granted no qualification credit.
  Lifecycle cancellation scenarios retain separate real workflow runs and
  job-owned cleanup.
```

### 4.4 Story 3.2 spec, open Platform-adoption task

```
OLD: ... complete tuple metadata together through the existing exact-source tuple
     and refreshed same-source legacy L/M process, preserving concurrent upstream work.
NEW: ... complete tuple metadata together through the exact-source current tuple,
     preserving concurrent upstream work. Until Story 2.13 lands, the existing
     same-source legacy L/M process still gates CI; after it lands, no L/M
     companions are required.
```

A dated course-correction note is appended to the spec.

### 4.5 Story 2.10 spec

A dated course-correction note is appended. After Story 2.13 lands, the final qualification records only the current-tuple lane. The earlier 349- and 412-case results remain historical evidence.

### 4.6 Release-workspace spec

```
OLD: - [ ] Builds publication and Platform tuple files: bind a real current published
     producer, then repair historical same-code companions and current owner verification. ...
NEW: - [ ] Builds publication and Platform tuple files: bind a real current published
     producer and repair current owner verification. (Historical same-code companions:
     superseded by Story 2.13, 2026-11-15.) ...
```

A dated note is appended. It says the legacy and pre-extension verification commands are retired by Story 2.13.

### 4.7 Sprint status

Add `2-13-retire-the-historical-tuple-replay-lanes-from-platform-ci: backlog` and `2-14-pin-builds-and-owner-modules-by-released-version: backlog` under Epic 2. Epic 2 stays `in-progress`.

## 5. Implementation handoff

- **Scope:** moderate. The backlog gains two stories, and in-progress stories carry sequencing notes.
- **Recipients:** the Developer agent, via `bmad-build`: Story 2.13 first, then 2.14. The owner coordinates with the session working on Story 3.2, which edits the same tuple files. A 2.13 branch rebases onto that work before landing.
- **Success criteria:**
  - 2.13: tuple CI on main is green with only the current lane. A doc-only Platform change lands with no extra commit or ref. A `/pushall` run leaves main's pins unchanged. The historical-source modules and descriptors are gone.
  - 2.14: adopting a new Builds or owner release changes only versions and lock files. `src/` holds no Builds or owner release-identity literal. AR-12 run records still bind expected and loaded content identities.
- **Not done by this correction:** main is not repaired, nothing is pushed, and no archived tag is deleted.
