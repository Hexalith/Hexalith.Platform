---
title: 'Bound Custody permitted-reference startup acquisition (R23B-7)'
type: 'bugfix'
created: '2026-10-29'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Address R23B-7 only. Custody synchronously reads its permitted-reference file before startup without acquisition bounds. Acquire and validate that startup snapshot with finite size, file-type and cancellation/deadline bounds; retain ownership of actual outstanding work through stalled open, read and disposal after callers stop waiting. Invalid or unavailable input must prevent startup and readiness. Preserve exact permitted-reference semantics, the projected ConfigMap mount and restart-based rotation contract, completed R23B-6, its 93 Authority tests, sealed source23 evidence, baseline failures and unrelated work. Add meaningful startup, input, stalled acquisition, cancellation and worker ownership tests. Never emit credential values. Work in an isolated worktree without touching the shared index. Do not install, run Live/Full/cohort qualification, replay maintenance, edit accepted records, stage, commit or push.

</frozen-after-approval>

## Implementation Notes

- Investigation: no remaining intent gaps or irreversible operations. Narrow footprint: Custody startup registration, a private bounded reader/hosted startup service, friend assembly access and focused tests; separate source verification evidence.
- Contract: `eng/ext-secrets-1/bind_isolated_custody.py:552–567,618–632` converts only runtime mounts to subPath and preserves the whole-directory projected allowlist. Follow its links; inspect the held descriptor. `eng/ext-secrets-1/README.md:227` and `provision_operational_hmac.py:356–362` retain startup snapshots and restart/checksum rotation. Do not change installation scripts or existing R23B-6 documentation.
- Preserve `InventorySources.ParseObject` object/depth32/recursive duplicate-property checks and the 1 MiB bound; preserve four unique valid canonical tenant/purpose scopes from `CustodyBinding.ReadyAsync` and all existing metadata fields. Validate these before startup rather than accepting an empty fallback.
- Use one process-wide, nonqueued dedicated acquisition worker and a two-second total deadline. Caller cancellation remains observable. Sanitize failures and late faults, retaining ownership until actual acquisition and disposal finish. Unsupported platforms refuse. Startup validation runs in a hosted service before the HTTP listener starts and uses its startup cancellation token.
- Verification uses retained local dependency binaries and existing restore assets in the isolated worktree; no dependency installation or full source-graph rebuild. Run focused Custody tests, constrained shared-thread-pool ownership tests, preserved 93 Authority tests, then preservation hashes. Review through the bmad-build oneshot blind reviewer.

- Final implementation uses IHostedLifecycleService.StartingAsync, which gates all service starts even with servicesStartConcurrently=true. Native locators reject embedded NUL; approved projected links remain supported.
- Verification: rebuilt Custody and its tests with no restore and BuildProjectReferences=false, zero warnings/errors. Custody86/86 includes28 existing and58 new cases. Six final ownership cases pass with shared pool maximum2 asserted. Preserved Authority93/93 passes from unchanged retained binaries. No broader failed aggregate was rerun.
- Blind reviewer returned six findings; all were triaged individually and patched. The same reviewer checked all six fixes and reported no unresolved original findings. No deferred item was added.
- Preservation audit verified2479 sealed evidence/source records; Authority and source23 document hashes match. Another session advanced shared main and changed its index during this run; this task performed no shared-index mutation, staging or commit. Changes remain solely in the detached worktree.
- Evidence: docs/implementation/ext-secrets-1-custody-permitted-reference-startup-hardening-2026-10-29.json; retained command logs and XML are under /tmp/ext-secrets-custody-startup-state. No installation, qualification, maintenance replay, accepted-record changes or publication occurred.

## Review Triage Log

- R1 — high, patch: .NET host configuration can start services concurrently. An ordinary hosted StartAsync is insufficient to gate the listener. Move acquisition to IHostedLifecycleService.StartingAsync and test concurrent startup.
- R2 — medium, patch: native UTF-8 marshaling truncates embedded NUL locators. Reject them before admission/open and test refusal.
- R3 — medium, patch: successful entry test intentionally has unavailable runtime; it cannot prove the populated array drives readiness. Add a host integration test with exact current metadata fixture proofs, exact scope permissions and HTTP readiness200 after startup.
- R4 — low, patch: existing per-phase stalls prove bounded waits, but do not distinguish a total deadline from reset phase deadlines. Add a combined open/read/disposal deadline case.
- R5 — medium, patch: current sentinel test covers only late open faults. Cover read/disposal and immediate faults, sanitized diagnostics, no publication, late fault observation and capacity recovery.
- R6 — medium, patch: existing projected rotation occurs after read completion. Add a held production-descriptor read during AtomicWriter replacement and prove old/new startup snapshots without pathname reacquisition.
