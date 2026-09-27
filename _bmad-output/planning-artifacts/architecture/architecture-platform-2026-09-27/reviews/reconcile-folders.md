# Folders input reconciliation

Verdict: **PASS at Platform initiative altitude.** Compared the written Platform draft on 2026-09-27. No remaining Folders-specific architecture blocker or additional production-enrollment gate is recommended.

Sources: `references/Hexalith.Folders/_bmad-output/planning-artifacts/architecture.md` (especially D-3, I-2/I-10/I-11, shared-host boundaries); `reconcile-architecture-downstream-2026-09-17.md`; `docs/deployment/supported-mvp-profile.md`; `docs/runbooks/backup-restore.md`. Folders AGENTS and the permitted root-declared AI.Tools baseline were read. No runtime, Git, or upstream change was performed.

## Governing scope correction

The user explicitly directed: **“keep platform MVP, do not block Folders production enrollment. Folders contraints are too strict”.** The Platform draft correctly gives that instruction precedence (Source Precedence, line 154): one-hour RPO, four-hour RTO, seven-day frequent and thirty-day daily retention, accepted single-node failure domain, prepared-capacity restoration, and the common qualified Dapr production profile. Folders' stricter five-minute RPO, thirty-five-day daily retention, no-singleton/multiple-replica topology and local backend/provider prescriptions impose no extra enrollment gates. Source alignment remains documentation work (line 242), not an admission prerequisite.

## Reconciliation results

| Boundary | Result in Platform draft |
| --- | --- |
| Shared EventStore composition | **Resolved.** The shared-host convention (line 125) composes module handlers and idempotency-intent registrations once and rejects app-ID/route conflicts. This preserves Folders' A-9 behavior without launching its existing wrapper alongside another `eventstore` host. |
| Concrete backend/profile | **Resolved under user precedence.** The common profile authority (line 128) requires consumer capability evidence. The Folders integration row (line 164) explicitly excludes a separate Folders-only backend/version gate while preserving actor/replay compatibility. There is no silent v1-to-v2 migration or unsupported claim of current qualification. |
| Recovery and external side effects | **Resolved.** AD-12 delegates recovery boundaries/reconciliation to module owners. The External effects row (line 150) requires restored-operation reconciliation before provider mutations resume and prohibits blind repetition of unknown outcomes. |
| Deletion/hold-safe restoration | **Preserved at the correct altitude.** AD-12 requires trustworthy deletion authority and module-approved recovery sets/checks; source precedence preserves data-safety rules. Folders/EventStore remain responsible for their safe restored admission and deletion/legal-hold dispositions. The spine need not copy the module's detailed WORM export design or reinstate its superseded five-minute operational profile. |
| Hosting adoption | **Resolved.** AD-1 owns composition; the Migration convention (line 132) keeps producer capabilities and module parity ahead of adapter/host retirement. Existing Folders AppHost and wrapper code are adoption work, not evidence that the target already exists. |

Dapr, Kubernetes staging/production, stable app identities, EventStore domain authority, rebuildable projections, non-authoritative broker state and explicit release/recovery evidence are consistent. Folders functional schemas, confidential-value handling, provider catalogs, and precise authorization rules appropriately remain in their module-owned sources.

Implementation must still supply common Platform qualification and actual module behavior/safety evidence. This report adds no stricter Folders infrastructure condition and makes no deployed-readiness claim.
