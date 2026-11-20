---
title: '2.14 Reconcile the five dependency revisions'
type: 'chore'
created: '2026-11-19'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'e899cf23dda711fbd85db9ec2b87365d9453ab60'
story_key: '2-14-pin-builds-and-owner-modules-by-released-version'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Main's tuple CI has failed `read_selection` since the `707cf901` submodule sync. The five dependency gitlinks no longer match the committed bindings: Builds `99baf1db` (v6.4.1) against pin 6.3.2, plus EventStore `2ec21f63` (v5.4.0), Parties `fc509479`, Tenants `d04974cf` and Memories `0856785c` (v4.1.0) against `eng/current-tuple-source.json`. Audit action 1 requires reconciling them before a new candidate is published.

**Approach:** Adopt the selected five revisions on one commit through the documented adoption procedures, as version bumps plus regenerated locks and binding. Prove it with a hosted tuple CI run linked to that commit. This also exercises the 2.14 criterion that an adoption needs no edit to C#, verifiers or docs.

**Decisions (owner, 2026-11-19):**
- **Revisions: upstream main heads plus Builds v6.4.3** (renegotiated 2026-11-20 from v6.4.2).
  - EventStore `2ec21f63` (v5.4.0), Memories `0856785c` (v4.1.0), Tenants `d04974cf` and Parties `fc509479` stay where they are; only their binding entries change.
  - Builds moves to v6.4.3 `dc225a69`, with every pin at 6.4.3 and the range floor `[6.4.3, 7.0.0-0)`. The `0e922c8c` commit on main pins `apphost.cs` Dapr hosting to 17.0.0-beta.1078, and only v6.4.3's catalog selects that version. `verify-platform-pins.py` requires the two to match.
- **Redrift:** `/pushall` resyncing gitlinks without refreshing the binding is out of scope. It is logged in deferred-work.
- **Size:** keep one spec (about 1,900 tokens).
- **SDK guard (renegotiated 2026-11-19):** `eng/test_source_sdk_propagation.py:41` accepted only a fixed list of EventStore source SDKs, and Builds moves the default to 5.3.1 in v6.4.2 (Builds `e3ab0a85`) and to 5.4.0 in v6.4.3. Replace the list with a range rule: a parseable version of at least 4.1.0, read the way `d80bdc94` reads versions. Land it first as its own `test(ci): …` commit.
- **Release (renegotiated 2026-11-20):** `b8048739` (`fix: story 2.14`, the planning commit) and `0e922c8c` (`fix(deps)`) are already on `origin/main`. The first green tuple CI on main therefore cuts a patch release, v2.2.3, and the owner accepts it. Nothing is reworded or force-pushed.

## Boundaries & Constraints

**Always:**
- Change only these:
  - the Builds pins (`eng/builds-tuple.json`, `.config/dotnet-tools.json`, the `hexalith-platform.json` range floor);
  - gitlinks that the selection moves;
  - the generated `eng/builds-tool.lock.json`, `eng/owner-packages.lock.json` and `eng/current-tuple-source.json`;
  - this story's compact evidence summary and sprint-status line.
- Commit the gitlinks and pins before running the generators, because they read `HEAD`.
- Land on `main` as two commits over `origin/main` `b8048739`: first the `test(ci)` guard commit (only `eng/test_source_sdk_propagation.py`), then the single `build(deps): …` adoption commit.

**Never:**
- Edit C#, Python verifiers, tests or docs to make the adoption pass, except the SDK guard decided above. If a check fails because an owner revision changed behavior, stop and report the failing test and the owner commit.
- Change `provenance.json` or the declaration snapshots, or retire the SDK 4.2 view. Those belong to the deferred view slice.
- Touch module repositories or their remotes.
- Push or dispatch without the owner's yes at that moment.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Reconciled commit | Selected gitlinks, pins and regenerated files committed | `read_selection`, `builds_publication`, `builds_tuple`, `verify_checkout` and lock `--check` pass; current42, base42 and extensions pass with positive counts and zero skips | N/A |
| Generator before commit | Gitlink moved but uncommitted | The generator refuses, or locks the previous catalog | Commit first, then regenerate |
| Behavior change in an owner revision | A suite fails on the new revision | Work stops with the test, owner and commit named | No source edit; owner decides |
| Tag and gitlink disagree | Builds tag commit ≠ gitlink | `builds_publication` refuses | Move the gitlink to the tag's commit |

</frozen-after-approval>

## Code Map

- `eng/current_tuple_source.py:50-85` `read_selection`: each owner gitlink must equal `sourceRevision`, and `provenance.json` must equal its digest. `:103-121` `verify_checkout` compares the owner checkout HEAD and manifest. `:125` `create_binding` reads Git objects only.
- `eng/prepare_tuple_sources.py binding --source <sha|tree> [--output new.json]`: regenerates the binding. The four owner manifests are byte-identical at old and new revisions, so only `sourceRevision` changes.
- Builds: `eng/builds_tuple.py:70,80` reads the gitlink from `HEAD` and verifies the catalog blob. `eng/builds_publication.py --revision <sha>` requires the release tag to name the gitlink (needs `GH_TOKEN`). `eng/builds_tool_lock.py` downloads, verifies and rewrites the lock; it has no `--check`.
  - Catalog 6.3.2 → 6.4.x changes only the net11 defaults (EventStore 5.3.1, Tenants 8.0.2, Folders 2.0.1) and adds `Props/AspireAppHost.Build.targets`. v6.4.1 and v6.4.2 have identical `Props/`.
  - v6.4.2 → v6.4.3 changes `Props/Directory.Packages.props` only in defaults: Dapr hosting 1052 → 1078, EventStore 5.3.1 → 5.4.0, Memories 4.0.0 → 4.1.0, Conversations 2.0.0 → 2.1.0 and FrontComposer 6.0.0 → 6.0.1. The SDK 4.2 view versions are unchanged. The catalog blob changes from `29dcf83b` to `eca86204`, so `owner_packages_lock.py --check` may report drift; regenerating that lock is allowed.
- `eng/owner_packages_lock.py`: `pinned_catalog` (:234) reads the Builds gitlink, and `run_built_owner_references` (:120) reads the Parties gitlink and checkout. The 18 locked packages are SDK 4.2 view versions (EventStore 4.2.0, Tenants 7.0.7, Memories 3.0.27, Parties 3.1.1, McpCli 2.0.1). `--check` reported `current` on 2026-11-19 at `HEAD` (Builds `99baf1db`, Parties `fc509479`). v6.4.2 has the same catalog blob, `29dcf83b`; v6.4.3 does not (see above).
- What the suites read: Parties is built from source as the Release owner (`PlatformCurrentOwnerProof.cs:300`; its checkout must equal the gitlink, `PlatformPartiesRunProof.cs:92-93`), and `Protection.Tests.csproj:16-19` compiles three EventStore files. Tenants and Memories supply only their manifests. CI sets `UseHexalithProjectReferences=false` (`run_tuple_ci.py:351`).
- Procedures: `eng/tuple-source-preparation.md:9-24` (binding from `git write-tree`), `:57-67` (Builds release), `:92-107` (owner release). No hardcoded revision outside the five pin files.
- `.github/workflows/tuple-ci.yml` lists the hosted checks. `source-release.yml` follows on `workflow_run`. Two `fix` commits, `0e922c8c` and `b8048739`, are already on main, so a green run releases v2.2.3.

## Tasks & Acceptance

**Execution:**
- [x] Worktree `/home/administrator/projects/hexalith/platform-2-14-reconcile`, branch `chore/2-14-reconcile-revisions` from `origin/main`. Initialize the submodules by reusing local objects (`--reference`). Other sessions' `/pushall` commits the main checkout.
- [x] `eng/builds-tuple.json`, `.config/dotnet-tools.json`, `hexalith-platform.json`, `references/Hexalith.Builds`: set 6.4.3/v6.4.3 and the floor `[6.4.3, 7.0.0-0)`, move the gitlink to `dc225a69` (and its checkout), run `python3 eng/builds_tool_lock.py`, then commit.
- [x] `eng/current-tuple-source.json`: regenerate it with `prepare_tuple_sources.py binding --source <HEAD>`, and check that only the four `sourceRevision` values change. Run `owner_packages_lock.py --check`, regenerate only if it reports drift, then commit.
- [x] `eng/test_source_sdk_propagation.py:41`: replace the fixed SDK list with the range rule (a parseable version of at least 4.1.0). Add a negative case if the file has a natural place for one. Commit it alone as `test(ci): …` (`277e520c`, on `e899cf23`).
- [x] Rebase `277e520c` onto `origin/main` `b8048739` (the result must touch only that test file), then redo the two adoption tasks above on top of it as one `build(deps)` commit.
- [x] Run the Verification commands. Write `_bmad-output/implementation-artifacts/evidence/2-14-reconcile/summary.md` with the revisions, commands, counts and run link; no bulk logs.
- [x] With the owner's yes, land a single fast-forward `build(deps)` commit on `main`, then record the hosted tuple CI run in the summary and in the sprint-status comment.

**Acceptance Criteria:**
- Given the landed commit, when hosted tuple CI runs, then `resolve-sources`, `tuple` and `tuple-verdict` are green and the run is linked from the evidence summary.
- Given that commit's diff against its parent, when inspected, then it contains only the files listed under Always.

## Design Notes

The selected revisions keep the gitlinks on upstream main, which `/pushall` already selects. Of the owner sources, only Parties (built from source) and three EventStore files reach the build. The rest supply only manifests that have not changed. EventStore and Memories are therefore pure binding refreshes.

## Verification

**Commands** (`export PATH="$HOME/.dotnet/tools:$PATH"`; builds use `-nodeReuse:false`):
- `python3 -B eng/test_current_tuple_source.py`, then `read_selection` and `verify_checkout` on `HEAD` -- expected: pass.
- `python3 -B eng/builds_tuple.py` equals `git -C references/Hexalith.Builds rev-parse HEAD`, and `python3 -B eng/builds_publication.py --revision $(git rev-parse HEAD)` passes.
- `python3 -B eng/owner_packages_lock.py --check` -- expected: pass.
- The `tuple-ci.yml` control step (`npm run test:source-release`, `test_tuple_sdk_suites.py`, `test_builds_publication.py`, `test_builds_tool_lock.py`, `test_bound_builds.py`) -- expected: pass.
- `NO_COLOR=1 TERM=dumb python3 eng/run_tuple_ci.py --evidence artifacts/<suite> --suite <current42|base42|extensions> --eventstore-sdk 4.2.0` -- expected: positive counts, zero skips.

## Spec Change Log

- 2026-11-19: Implementation stopped at current42 `source-sdk-propagation-controls`. The fixed SDK list at `eng/test_source_sdk_propagation.py:41` rejects the Builds v6.4.2 default, EventStore 5.3.1. The owner chose the range rule as a separate `test(ci)` commit, and a `docs(planning)` reword of `7c283bcb`. Commits `f42c5062` and `59c2e652` are kept and get squashed after the guard commit.
- 2026-11-20: The v6.4.2 tree (`277e520c` + `954b320a`, based on `e899cf23`) passed locally: control step, current42 380/380, base42 67/67, extensions 9/9. Meanwhile `origin/main` gained `0e922c8c` (apphost Dapr 1078) and `b8048739` (the pushed `7c283bcb` content), and Builds released v6.4.3. The v6.4.2 adoption would fail `pin-alignment` on main, so the owner retargeted to v6.4.3 and accepted the v2.2.3 release. The `docs(planning)` reword is moot. For review, diff the landing commits against `b8048739`, not `baseline_commit`, because the commits in between are another session's work.
- 2026-11-20: The v6.4.3 adoption passed on `b8048739`. The commits are `1eb8c269` `test(ci)` and `b334e161` `build(deps)`: the latter is `0f428198` amended with the evidence summary and the sprint-status line. Results: control step, current42 380/380, base42 67/67, extensions 9/9, zero skips. After the amend, `read_selection`, `verify_checkout`, the lock `--check` and `builds_publication` were rerun. The matrix rows are covered by `test_current_tuple_source.py` (committed-binding and worktree-mutation tests), `test_tag_drift_from_the_gitlink_refuses`, and the two documented stops.
- 2026-11-20: Review iteration 0 produced seven patches (rows 1, 2, 6, 9, 10, 11 and 16) and rejected twelve findings; no loopback. The final stack is `527c3fec` `test(ci)` and `5f684500` `build(deps)` on `b8048739`. These were rerun on it: the control step (84, 14, 30, 21, 13, 8, 12), `test_current_tuple_source` (16), `test_owner_packages_lock` (11), `read_selection`, `verify_checkout`, `builds_tuple`, `builds_publication` and the lock `--check`. The suites were not rerun after the comment-only patch; hosted CI on the landed commit is the gate.
- 2026-11-20: With the owner's yes, `527c3fec` and `5f684500` were pushed as a fast-forward of `b8048739`. Hosted tuple CI run 44712072381 passed `resolve-sources`, `tuple` and `tuple-verdict`: current42 380, base42 67, extensions 9, zero skips. Source release 44723218751 published v2.2.3 at `5f684500`. Audit action `audit-2026-11-19-candidate-repair` is marked done. The adoption needed the `test(ci)` guard edit, so the no-edit adoption criterion applies from the next Builds bump onward.

## Review Triage Log

Iteration 0, diff `b8048739..b334e161`, from three layers: blind, edge-case and verification-gap.

| # | Layer | Finding | Verdict | Evidence | Route |
|---|-------|---------|---------|----------|-------|
| 1 | blind | The summary relies on "the spec's change log", but the landed tree has the stale `b8048739` copy of the spec | low | `b334e161` holds the draft spec (v6.4.2, no change log); the amended spec is uncommitted in the main checkout | patch: write the attempt history in the summary |
| 2 | blind | Verification names the unreachable `0f428198`, truncated, and leaves out the checks rerun on the final commit | low | The amend replaced `0f428198`; the summary doesn't list the reruns of `read_selection`, `verify_checkout`, lock `--check` and `builds_publication` | patch: give the full SHA and list the reruns |
| 3 | blind | The evidence doesn't record which source SDK the controls passed against | false | `artifacts/.../source-sdk-metadata/evaluation-0.stdout` records `HexalithEventStoreVersion` 5.4.0 from the real MSBuild evaluation | reject |
| 4 | blind | `parsed()` is copied from `verify_sdk_gates.py` rather than shared | low | It is a verbatim copy, but a shared module across `eng/` and `tests/catalog-builder/` adds import plumbing, and the drift risk is small | reject (low; the fix is more than a direct correction) |
| 5 | blind | The docstring says "MSBuild's version-function reading", but whitespace and Int32 handling differ from MSBuild | low | The docstring matches the existing `verify_sdk_gates.py` helper; catalog defaults never carry whitespace or exceed Int32 | reject (low; rewording only one copy makes the two diverge) |
| 6 | blind | The `SOURCE_SDK_FLOOR` comment calls the catalog default a range rule and 4.1.0 "the oldest recorded source SDK" | low | The range belongs to the guard, not the catalog; 4.1.0 is the oldest value the old list held | patch: reword the comment |
| 7 | blind | The control misses boundary cases (`4.1.0-preview.1`, `4.1`, `4.0.99`, `4.1.0.0`, bare `v`) | low | Ignoring the prerelease suffix is MSBuild's own semantics, and the catalog default moves only forward | reject (low; unlikely, and the fix adds cases) |
| 8 | blind | The new control tests the helper, not the `setUpClass` call, and is pure Python under a "real metadata" scope | low | Catching a regression would need a future edit that drops the call; the scope string is an existing label | reject (low; the fix adds a refusal-path harness) |
| 9 | blind | Summary labels are ambiguous: run-controls 30 versus the SDK controls' 30, "Groups" undefined, 5.4.0 versus `--eventstore-sdk 4.2.0` | low | The rows use short labels, and both counts are 30 | patch: name the scripts, define groups, explain the SDK 4.2 view |
| 10 | blind | The summary leaves out the other catalog changes and the code change in the Builds range | low | `99baf1db..dc225a69` also moves Conversations, FrontComposer, Memories and the catalog blob, and includes `fix(release)` | patch: add one bullet |
| 11 | blind | The summary doesn't mention the v2.2.3 release, and the audit action item has no pointer | low | The hosted placeholder has no source-release slot. The action item correctly stays open until hosted CI qualifies; it closes in the post-landing record | patch: add a release line and slot (the action item closes after landing) |
| 12 | edge | The guard refuses a default with surrounding whitespace | low | The catalog writes plain `5.4.0`; the case is unreachable from the controlled catalog | reject (low; unlikely, and the fix adds a branch) |
| 13 | edge | The guard admits a part that overflows Int32 | low | Unrealistic version; MSBuild would then fail loudly in later controls | reject (low; unlikely, and the fix adds a guard) |
| 14 | edge | A non-normalized default (`v5.4.0`, `5.4`) passes the guard, then misses the exact asset keys | low | The later controls fail loudly; the catalog always writes three-part versions | reject (low; loud failure, and the fix adds a check) |
| 15 | edge + gap | A default of 4.1.0 equals the explicit control SDK, so the precedence controls pass vacuously | low | The old list admitted 4.1.0 too; the default is 5.4.0 and only moves forward; "at least 4.1.0" is the frozen decision | reject (low, unchanged by this story; a fix would contradict the frozen decision) |
| 16 | edge | The commit message says v6.4.2 moved the default to 5.3.1, but `e3ab0a85` is already in v6.3.3 and v6.4.0 | low | `git tag --contains e3ab0a85` lists v6.3.3 first | patch: correct the `1eb8c269` message |
| 17 | edge | The no-edit adoption criterion would be recorded as exercised although a gating control changed | low | The frozen Intent sentence; the summary and commits make no such claim. The step-05 report says the criterion holds only from the next adoption | reject (the fix edits the spec) |
| 18 | edge | The worktree is on a detached HEAD, not branch `chore/2-14-reconcile-revisions` | low | This is deliberate: `/pushall` merges local `refs/heads` (memory). The worktree HEAD and its reflog keep the commits reachable | reject (low) |
| 19 | edge | The task says "a single fast-forward `build(deps)` commit", but two commits land | low | The frozen Always now lists two commits; only the task line is stale | reject (the fix edits the spec) |
