# Story 2.8 Parties prerequisite preflight

Read-only source snapshot: Hexalith.Parties main
4e2b2e8dae4835944adb55ccc8a86acbc470897e.
https://github.com/Hexalith/Hexalith.Parties/commit/4e2b2e8dae4835944adb55ccc8a86acbc470897e

Both requested projects build in Release with zero warnings/errors:
- src/Hexalith.Parties.EventStoreExtensions/Hexalith.Parties.EventStoreExtensions.csproj (1.12s).
- src/Hexalith.Parties/Hexalith.Parties.csproj, the real Web service (3.43s).

Exact commands and output are in build-commands.json and the two *-release-build.txt
files. The owning CI's UseHexalithProjectReferences=false and UseNuGetDeps=true flags
were used with Configuration=Release, -m:1 and /nr:false. No audit/warning suppression
or source repair was added. These are compile probes, not tests or runtime acceptance.

Supported import and identity

Clean sibling worktrees under /tmp/hexalith-story28-parties-package contain Parties
at 4e2b2e8d and root-declared Builds at its exact Parties gitlink 81c88b34d09d34260b92d7f0928dbbc1c7446baa.
Directory.Packages.props uses its existing second sibling import. No property shim,
submodule initialization, private EventStore draft or source fallback was used.
HexalithVersionsLoaded evaluates true. The owning props select SDK 3.130.0,
Tenants 6.0.0, Memories 3.0.13 and FrontComposer 4.7.4 without overrides or pin edits.

Evaluation and restored-assets receipts show own-repository project edges only and
external Hexalith NuGet edges. All 12 resolved Hexalith package cache sources are
https://api.nuget.org/v3/index.json. SDK 3.130.0 archives name official source
484f4bd82fd2ef4b6c2653fdde57feeeb6d85a3c; Tenants 6.0.0 names 9e5a991d0ba304bac7b1a5809cd71797b47d78ed;
Memories 3.0.13 names 0a1f6bf94301094afd0045c4e396dad4dd613ce6. Archive IDs, versions,
repository metadata, SHA-256, signature presence and actual own-project paths are
retained in resolved-package-identity.json. Signature presence is not a new
cryptographic qualification claim. Built own assembly hashes are separate.

Actual published inventory

The live reachable stable tag floor and latest GitHub release are v2.0.0 at
3e1dd7cac6ee4db1db48d1bfb478e1b5848f7b8e; last release workflow 40633736715 succeeded.
https://github.com/Hexalith/Hexalith.Parties/actions/runs/40633736715
The authoritative tools/release-packages.json and release workflow declare 11 packages
and three container projects (parties, parties-mcp, parties-ui). Ten package IDs have
latest stable 2.0.0 on nuget.org; Hexalith.Parties.EventStoreExtensions returns 404 and
has not been published. This is an actual version-index observation, not complete
archive/container qualification. The older checklist's nine-package wording is stale;
the executable manifest/workflow use 11. No future version was predicted or packed.

Live source gates and exact ledger blockers

Main push CI 41224674293 failed at restore and UI restore because SDK 3.130.0 was not
yet published (NU1102, nearest 3.129.0). Subsequent tests/browser execution were skipped.
https://github.com/Hexalith/Hexalith.Parties/actions/runs/41224674293
The new narrow builds prove those two graphs can now restore/compile; they do not
turn the historical full CI red result green.

Independent range run 41224674137 failed on seven real unsigned advances.
https://github.com/Hexalith/Hexalith.Parties/actions/runs/41224674137
The exact push base 166fd757cd512779c8df8d9c1060eae8c63cfe20 was read from that job's
actual log. Re-executing the owning --push-range gate fails on the same seven refs;
--diff against the live v2.0.0 source also fails on those same seven refs:
Builds, EventStore, FrontComposer, McpCli, Memories, Platform and Tenants.
missing-gitlink-signoffs.json records every full selected SHA. No signoff authority
was invented. The logs also contain a pre-existing missing commit-graph warning;
it was not repaired and does not explain the explicit gate refusal.

Observed classic main protection returns 404; effective branch rules/rulesets are
empty. This does not waive the workflow's exact-current-main successful push CI and
independent push-range proof, nor its release-floor signoff gate. Production has
required reviewer jpiquot, prevent_self_review=false, can_admins_bypass=false and a
custom main-only branch policy. No protection or environment approval was changed.

Smallest valid next action

Have the owner validate the seven actual selections or choose deliberate resets,
and prepare the corresponding ledger/source correction through a normal Parties PR.
Then require fresh complete current-main push CI and independent range proof, using
the now-published SDK input. Preserve all tests and publication protections. After
those gates pass, the existing protected manual Release workflow and complete
11-package/three-image publication verification are the normal path. Do not dispatch
against the currently red source, relabel its old run, fabricate ledger approvals or
substitute private SDK source. This preflight does not execute that next action.

Limits and preservation

No source/CI repair, commit, push, PR/main merge, release dispatch, environment
approval, credential provisioning or caller adoption occurred. The shared dirty
Parties HEAD, index and three source files are byte-preserved. Clean worktree/source
and exact catalog hashes are recorded in source-receipt.json. All temporary dependency
worktrees come from root-declared repositories; nested submodules remain uninitialized.
Only build outputs and these evidence files were produced. Full unit/integration/UI
and catalog-runtime behavior, whole-family packing, OCI archives and hosted Story 2.8
reference cancellation/partial-startup acceptance remain unqualified. The user's
dedicated CI reader choice is coordinator context; this task made no registry change.
Story remains in progress and grants no candidate acceptance.
