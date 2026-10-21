# Live SM-2 evidence — 2026-10-20

Five runnable modules passed real debugger/source-edit checks. **Overall SM-2 sign-off remains open** for unadopted modules, the enrollment gaps below and module-owner acceptance.

| Module | Workspace | Current-pin runtime proof | Evidence |
| --- | --- | --- | --- |
| EventStore | Platform | PASS: breakpoint, source edit, restoration and cleanup | [record](eventstore-live/sm-2-checklist.md) |
| Memories | Platform | PASS: breakpoint, source edit, restoration and cleanup | [record](memories-live/sm-2-checklist.md) |
| McpCli | Platform | PASS: breakpoint, source edit, restoration and cleanup | [record](mcpcli-live/sm-2-checklist.md) |
| Parties | Independent module workspace | PASS: breakpoint, source edit, restoration and cleanup | [record](parties-live/sm-2-checklist.md) |
| Tenants | Independent module workspace | PASS: breakpoint, source edit, restoration and cleanup | [record](tenants-live/sm-2-checklist.md) |
| Folders | Independent module workspace | OPEN: no v2 declaration at the recorded revision | [prerequisite](remaining-module-prerequisites.json) |
| Projects | Independent module workspace | OPEN: no v2 declaration at the recorded revision | [prerequisite](remaining-module-prerequisites.json) |

## Exact inputs

- Platform snapshot: `4f036ced45aa40700948c19949bb874881c960b5`.
- Published tool: `4.35.0+fff4a212180d8cd1c5c0759655a9bc4fb6963c45`.
- Catalog/release tag: `fff4a212180d8cd1c5c0759655a9bc4fb6963c45` / `v4.35.0`.
- SDK `10.0.402`, Aspire CLI `13.6.1`, isolated Dapr CLI/runtime `1.18.0`/`1.18.2`. Actual tool outputs and debugger binary SHA-256 are in [environment.json](environment.json).
- [Machine-readable results](results.json), [context](context.json) and [cleanup](cleanup.json).

Parties and Tenants used local disposable qualification commits aligning their direct references and tool pin with this Platform snapshot. These commits change no application source or declaration. Their owner branches were preserved; the qualification commits were not pushed. The [Parties](parties-qualification/identity.json) and [Tenants](tenants-qualification/identity.json) records include the owner base, local commit identity, exact patch, raw commit object and pinned commitlint validation.

Every accepted source mapping binds committed identity, composition identity and checkout identity to the Platform snapshot, with the exact current catalog. Source mappings retain the untagged-Platform HXW011 warning where applicable. This is source/Debug evidence.

## What was observed

Each server-module baseline reached declared readiness and returned HTTP 200 from `/ready`. After exact-run down, a reversible source edit added a unique response header in active-checkout code. The module tool rebuilt and started the environment; netcoredbg hit that source line, and a real HTTP response carried the marker. McpCli instead rebuilt its run-scoped Debug DLL, hit the edited `CatalogService.ListModules`, emitted the new stderr marker and returned the expected offline `catalog_empty` exit `2`.

Per-module records preserve exact commands, exits, run identities, selected projects, full debugger transcripts, source patches and hashes, loaded assembly/PDB hashes, source mappings, restoration and cleanup. All modified probe sources were restored byte-for-byte. Exact-run down removed the runner state/workspace; ownership checks found no remaining run containers or processes. Existing global Dapr containers remained running. [Direct Platform gitlinks and nested checks](platform-direct-only-references.json) and each domain record prove direct-only initialization.

## Open sign-off work

- Folders and Projects have no v2 declaration at the Platform-recorded revisions; their SM-2 rows remain open.
- Parties and Tenants need hosting-inventory JSON records for the new kit. Tenants also needs the explicit minimum `memories` dependency in its declaration. Its actual runtime minimum included Memories; that does not repair the declaration.
- The released adoption validator fails to enumerate these real-sized tracked-file lists: 472,879 bytes for Parties and 259,736 for Tenants. Git succeeds, while the shared process capture is bounded to 65,536 characters. The inferred truncation cause and exact post-qualification reports are preserved in [kit-preflight-gaps.md](kit-preflight-gaps.md), [Parties report](parties/qualified-adoption.txt) and [Tenants report](tenants/qualified-adoption.txt). This needs a Builds fix/regression and reviewed release; the failing check was not reported as passing.
- Module-owner source/Dapr audit acceptance and final evidence acceptance remain pending in the checklists.

This supports Story 3.11 without closing it. Hosting retirement and source/package/deployed parity retain their separate owner gates.

## Reproduce and assess

1. Create disposable workspaces at the recorded owner bases and Platform snapshot; initialize root-declared references only. Use shared local Git objects and sparse checkouts for unused content. Include every declaration-owned test entry even when its test profile is not selected.
2. Apply the exported local qualification patches for the domain workspaces. Committed, staged and checked-out pins must agree. Restore the exact published tool.
3. Use the per-module `commands.json` sequences and [proof driver](tools/proof.py.txt), [debugger attach helper](tools/attach.py.txt) and [qualification setup](tools/qualify-domain.py.txt). Configure their recorded context paths for the new disposable locations. The driver keeps its command session alive through debugging and exact-run cleanup. McpCli uses the existing Story 1.11 `tools/ncdb_launch.py`.
4. Require a real bound breakpoint, observed marker, byte-for-byte source restoration, matching mapping identities and successful exact-run cleanup. [Report checks](tools/report.py.txt) validate those retained records.

[Attempt notes](attempt-notes.md) identify setup failures and superseded staged-only identity runs. Only the final `*-live` results above count as current-pin proof. Earlier transcripts are retained for traceability.

The final Platform evidence commit message passed its pinned commitlint CLI ([result](commitlint.txt)).
