---
title: '2.14 Reconcile the five dependency revisions'
type: 'chore'
created: '2026-11-19'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
story_key: '2-14-pin-builds-and-owner-modules-by-released-version'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Main's tuple CI has failed `read_selection` since the `707cf901` submodule sync. The five dependency gitlinks no longer match the committed bindings: Builds `99baf1db` (v6.4.1) against pin 6.3.2, plus EventStore `2ec21f63` (v5.4.0), Parties `fc509479`, Tenants `d04974cf` and Memories `0856785c` (v4.1.0) against `eng/current-tuple-source.json`. Audit action 1 requires reconciling them before a new candidate is published.

**Approach:** Adopt the selected five revisions on one commit through the documented adoption procedures, as version bumps plus regenerated locks and binding. Prove it with a hosted tuple CI run linked to that commit. This also exercises the 2.14 criterion that an adoption needs no edit to C#, verifiers or docs.

**Decisions (owner, 2026-11-19):**
- **Revisions: upstream main heads plus Builds v6.4.2.**
  - EventStore `2ec21f63` (v5.4.0), Memories `0856785c` (v4.1.0), Tenants `d04974cf` and Parties `fc509479` stay where they are; only their binding entries change.
  - Builds moves to v6.4.2 `8ad2c228`, with every pin at 6.4.2 and the range floor `[6.4.2, 7.0.0-0)`.
- **Redrift:** `/pushall` resyncing gitlinks without refreshing the binding is out of scope. It is logged in deferred-work.
- **Size:** keep one spec (about 1,900 tokens).

## Boundaries & Constraints

**Always:**
- Change only these:
  - the Builds pins (`eng/builds-tuple.json`, `.config/dotnet-tools.json`, the `hexalith-platform.json` range floor);
  - gitlinks that the selection moves;
  - the generated `eng/builds-tool.lock.json`, `eng/owner-packages.lock.json` and `eng/current-tuple-source.json`;
  - this story's compact evidence summary and sprint-status line.
- Commit the gitlinks and pins before running the generators, because they read `HEAD`.
- Land as `build(deps): …` so semantic-release cuts no release.

**Never:**
- Edit C#, Python verifiers, tests or docs to make the adoption pass. If a check fails because an owner revision changed behavior, stop and report the failing test and the owner commit.
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
- `eng/owner_packages_lock.py`: `pinned_catalog` (:234) reads the Builds gitlink, and `run_built_owner_references` (:120) reads the Parties gitlink and checkout. The 18 locked packages are SDK 4.2 view versions (EventStore 4.2.0, Tenants 7.0.7, Memories 3.0.27, Parties 3.1.1, McpCli 2.0.1). `--check` reported `current` on 2026-11-19 at `HEAD` (Builds `99baf1db`, Parties `fc509479`). v6.4.2 has the same catalog blob, `29dcf83b`.
- What the suites read: Parties is built from source as the Release owner (`PlatformCurrentOwnerProof.cs:300`; its checkout must equal the gitlink, `PlatformPartiesRunProof.cs:92-93`), and `Protection.Tests.csproj:16-19` compiles three EventStore files. Tenants and Memories supply only their manifests. CI sets `UseHexalithProjectReferences=false` (`run_tuple_ci.py:351`).
- Procedures: `eng/tuple-source-preparation.md:9-24` (binding from `git write-tree`), `:57-67` (Builds release), `:92-107` (owner release). No hardcoded revision outside the five pin files.
- `.github/workflows/tuple-ci.yml` lists the hosted checks. `source-release.yml` follows on `workflow_run`. No `feat`/`fix` commit has landed since v2.2.2.

## Tasks & Acceptance

**Execution:**
- [ ] Worktree `/home/administrator/projects/hexalith/platform-2-14-reconcile`, branch `chore/2-14-reconcile-revisions` from `origin/main`. Initialize the submodules by reusing local objects (`--reference`). Other sessions' `/pushall` commits the main checkout.
- [ ] `eng/builds-tuple.json`, `.config/dotnet-tools.json`, `hexalith-platform.json`, `references/Hexalith.Builds`: set 6.4.2/v6.4.2 and the floor `[6.4.2, 7.0.0-0)`, move the gitlink to `8ad2c228` (and its checkout), run `python3 eng/builds_tool_lock.py`, then commit.
- [ ] `eng/current-tuple-source.json`: regenerate it with `prepare_tuple_sources.py binding --source <HEAD>`, and check that only the four `sourceRevision` values change. Run `owner_packages_lock.py --check`, regenerate only if it reports drift, then commit.
- [ ] Run the Verification commands. Write `_bmad-output/implementation-artifacts/evidence/2-14-reconcile/summary.md` with the revisions, commands, counts and run link; no bulk logs.
- [ ] With the owner's yes, land a single fast-forward `build(deps)` commit on `main`, then record the hosted tuple CI run in the summary and in the sprint-status comment.

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
