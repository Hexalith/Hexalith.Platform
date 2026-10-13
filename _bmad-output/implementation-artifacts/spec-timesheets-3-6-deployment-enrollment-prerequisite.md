---
title: 'Prepare the Timesheets Story 3.6 hosted enrollment prerequisite'
type: 'feature'
created: '2026-10-13'
status: 'in-progress'
baseline_commit: 'be294641fe06490b7f1d9788f383512088842e7d'
route: 'dispatch'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="user authorized reversible implementation before infrastructure approval">

## Intent

**Problem:** Timesheets Story 3.6 has passing isolated evidence but no qualified deployed target. Platform's current declaration composition supports Development only, and hosted environment/release work remains unfinished.

**Approach:** Prepare a Platform-owned v2 enrollment candidate and reproducible, offline staging plan using the existing Builds declaration validator and Platform hosted architecture. Bind the exact candidate source, declaration bytes, proposed namespace/endpoints and operator ownership. Describe missing retained artifacts and operational providers as blockers, never invented evidence.

## Boundaries & Constraints

**Always:** Keep deployment and Story 3.6 acceptance closed. Use staging on the designated cluster, separate application/data namespaces, the mandatory domain minimum (EventStore, Tenants, Memories), split public/internal Timesheets listeners, production-grade authentication, retained digest-pinned Release artifacts and module-owned acceptance. Treat chosen names as proposed until infrastructure is approved and observed. A source revision or preparation hash is not a released image identity. Jérôme Piquot is proposed Administrator/operator, not a granted Timesheets migration principal.

**Never:** Mutate infrastructure, deploy, publish, issue domain commands, migrate/clear legacy data, change nested submodules, replace denial/inventory providers with fixtures, mint receipts, add a second AppHost or mark Timesheets Story 3.6 complete. The direct Timesheets submodule already exists; preserve its current pin and record any difference from the latest sibling source. Do not alter other stories' tracking.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Valid preparation | Clean candidate Timesheets root and existing Platform sources | Deterministic plan with exact full source revision and content hashes; deployment blocked | Preparation success does not authorize deployment |
| Missing/dirty source | Missing required files, wrong repository root or modified candidate source | No plan produced | Bounded failure without raw Git output |
| Existing output | Requested destination exists | Preserve destination | Exclusive creation refuses |
| Provider claims | Locally authored plan or fixture says prerequisites pass | Tool cannot accept receipts, grants or gate overrides | No deployment/activation capability |
| Declaration drift | Candidate disagrees with module server/surface minimum | Validation fails | Existing schema validator plus policy assertions |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Composition/PlatformDeclarationSet.cs` and `PlatformServerBindings.cs`: reuse v2 declaration semantics and domain dependency minimum; existing runtime is Development-only and has no Timesheets binding.
- `references/Hexalith.Builds/schemas/hexalith.module-manifest.v2.json` and `PlatformManifestValidator`: existing schema and atomic validator; read only.
- `_bmad-output/specs/spec-platform/SPEC.md` and architecture spine: separate staging app/data namespaces, Gateway API, OpenBao, Dapr trust, retained Release package, explicit authority and data-preserving recovery.
- `_bmad-output/planning-artifacts/epics.md` and `sprint-status.yaml`: unfinished hosting/release gates, especially 4.14–4.17 and 4.22; keep status unchanged.
- `/home/administrator/projects/hexalith/timesheets/docs/deployed-magic-link-acceptance.md` and `3-6-resume-checkpoint.md`: latest prerequisite evidence; read only.
- Timesheets `Program.cs`, `InternalSurfaceGuard`, workload assertion source and migration registration: preserve public 8080/internal 8081, default-deny migration authority and unavailable certified inventory; no runtime edits.

## Tasks & Acceptance

**Execution:**
- [ ] `eng/timesheets-deployment/hexalith.module-manifest.v2.json` — declare the single Timesheets host, dependencies, surface boundaries, provider needs and rebuild inventory as a candidate, outside active composition.
- [ ] `eng/timesheets-deployment/prepare.py` — generate a local blocked plan from exact source bytes and existing backlog; accept no authority inputs and execute only bounded read-only Git commands.
- [ ] `eng/timesheets-deployment/plan.json` and `README.md` — retain reproducible candidate identity, exact proposed target/endpoints/ownership, prerequisite matrix, approval boundary and execution/rollback sequence.
- [ ] `eng/timesheets-deployment/test_prepare.py` and `tests/Hexalith.Platform.Composition.Tests/TimesheetsEnrollmentTests.cs` — verify missing/dirty source and exclusive-write refusals, immutable denial gates, actual v2 validation and surface/dependency safety.

**Acceptance Criteria:**
- Given the existing hosting architecture, when preparing Timesheets, then the proposed target is staging `hexalith-staging` / `hexalith-staging-data` on `jpiquot@local`, with `timesheets.hexalith.com` and internal 8081 kept private.
- Given absent operational providers or retained artifacts, when preparation succeeds, then the plan records exact missing production bindings, certified inventory/maintenance/continuity and subscriber/shared-rebuild proof, with deployment unauthorized and acceptance FAIL.
- Given the existing Timesheets root submodule, when preparation inspects source identity, then its full revision is retained without update and any candidate revision difference remains an explicit enrollment blocker.
- Given the reversible implementation, when validation finishes, then a reviewable plan exists before any infrastructure approval is requested and legacy state/submodules/Timesheets tracking remain untouched.

## Implementation Notes

- User explicitly requested completing reversible implementation and validation before an infrastructure approval request. This authorizes the local build work and supersedes the workflow's intermediate spec-approval checkpoint. No infrastructure approval is inferred.
- Initial Platform revision: `be294641fe06490b7f1d9788f383512088842e7d`; pre-existing modified nested references: ChatBot, Conversations, EventStore. Preserve them.
- This is a Platform prerequisite increment, not Platform Epic 3 Story 3.6; no sprint story key is selected.

## Spec Change Log

## Review Triage Log

## Verification

- `python3 -m unittest discover -s eng/timesheets-deployment -p 'test_*.py' -v` — offline policy and safe-file tests pass.
- Build/run `Hexalith.Platform.Composition.Tests` individually using pinned SDK 10.0.401 — actual Builds schema validator and enrollment boundary checks pass.
- Regenerate the plan against the selected clean Timesheets source and compare bytes; `git diff --check` — deterministic output and clean whitespace.
