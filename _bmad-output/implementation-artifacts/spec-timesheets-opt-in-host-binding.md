---
title: 'Opt-in Timesheets source host binding'
type: 'feature'
created: '2026-10-13'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'ce6f2d34398803644b29d02ba57625898190ce20'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Platform retains a valid Timesheets enrollment candidate but cannot bind its host through its existing declaration composition. The latest Timesheets qualification checkpoint explicitly leaves deployed acceptance blocked.

**Approach:** Add an explicit Development/source-mode opt-in to the existing Platform model, consuming `eng/timesheets-deployment/hexalith.module-manifest.v2.json`. Bind the mapped Timesheets host, dependencies, listener split, local authentication and exact read-model component; deliver focused tests and a current local qualification record.

## Boundaries & Constraints

**Always:** Preserve default-deny Timesheets access/reference/migration authorization and unavailable inventory providers. Keep public and internal surfaces on separate listeners; Dapr must invoke the internal listener. Use the single command-selected source mapping and existing Platform composition. Retain checkpoint limitations: production resource-binding issuer and access/reference providers, certified cross-tenant inventory, enforced maintenance and migration authority, source/pending-delivery continuity, deployed subscriber/shared-rebuild evidence and retained release artifacts remain absent. Story 3.6/Epic 3 remain in progress and release FAIL. Preserve existing user changes.

**Never:** Deploy, issue domain commands, migrate or initialize legacy state, initialize nested submodules, publish release artifacts, commit/push, modify Timesheets owner source/tracking or its pinned report, or enable the LiveDaprFixture authorization bypass. Local composition/build/test success grants no deployment or acceptance authority.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Default | Timesheets opt-in absent/false | Existing reference graph without Timesheets | Existing behavior |
| Source opt-in | Enabled, mapped host and candidate available | One Timesheets host plus six reference servers; `/ready`; exact read-model component | Atomic preparation |
| Missing source | Enabled, missing mapping/project | No resources added, owner-specific refusal | No nested/sibling fallback |
| Package/production | Enabled in unsupported mode | No resources added | Explicit qualified-release/profile refusal |
| Unsafe routing | Opt-in graph | Internal endpoint excluded from external/reference advertisement; Dapr uses internal; guard is bound to internal port and AllowOnAnyPort=false | Never bind fixture grants |
| Inert candidate | Candidate supplied with opt-in false | Timesheets selection refuses missing opt-in | No implicit activation |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Composition/PlatformAppModel.cs` — Prepare/Compose/ComposeRun share the declaration model; resources, scoped components and sidecars are attached here. Preserve Works/Agents routes.
- `PlatformCompositionOptions.cs`, `PlatformServerBindings.cs`, `PlatformLocalProfile.cs` in that directory — explicit inputs, executable resolution, shared Development signing material. Candidate extensions are empty; host inputs need a narrowly scoped local binding without claiming qualified declaration providers.
- `PlatformDeclarationSet.cs` — validates supplied files and selects active-module closure. Domain minimum is EventStore/Tenants/Memories.
- `apphost.cs` — thin existing entry delegates into PlatformAppModel; opt-in belongs in this model, not another AppHost.
- `tests/Hexalith.Platform.Composition.Tests/PlatformAppModelTests.cs` — reuse graph/environment/sidecar test patterns. `TimesheetsEnrollmentTests.cs` protects the unchanged offline candidate and retained plan denial gates.
- `references/Hexalith.Timesheets/src/Hexalith.Timesheets/Program.cs` — existing host kernel and InternalSurfaceGuard; read-only. Host project is `src/Hexalith.Timesheets/Hexalith.Timesheets.csproj` under mapped owner root.
- `references/Hexalith.Timesheets/src/Hexalith.Timesheets.AppHost/Program.cs` — owner reference listener/workload pattern and EventStore domain registration `*|timesheets|v1`; read-only. Local public/internal contract is 8080/8081; dynamically assigned local equivalents are allowed if the guard and Dapr agree.
- `references/Hexalith.Timesheets/src/Hexalith.Timesheets.AppHost/DaprComponents/Timesheets/timesheets-readmodels.yaml` — exact hardcoded component, Timesheets-only scope, Redis DB14; use a Platform-owned local template against composition-owned Redis.
- `references/Hexalith.Timesheets/_bmad-output/implementation-artifacts/3-6-resume-checkpoint.md` — latest 2026-10-13 qualification; production/certification/deployed proof still blocked. Never edit owner evidence.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Platform.Composition/PlatformCompositionOptions.cs` and binding/model helpers — add opt-in and mapped source project, integrate candidate/closure atomically, refuse unsupported modes and implicit activation.
- [x] `PlatformAppModel.cs` and local binding/component files — wire distinct listeners, guarded internal port, Dapr internal endpoint, Timesheets-only read-model state store, local workload authentication and gateway registration without enabling fixture grants or adding certified providers. Runtime source build must use mapped root EventStore rather than nested checkouts.
- [x] `tests/Hexalith.Platform.Composition.Tests/TimesheetsHostBindingTests.cs` — execute focused matrix tests, including file-entry configuration path, sidecar routing, scoped storage and shared local authentication; keep offline enrollment tests passing.
- [x] `eng/timesheets-deployment/README.md` and local qualification document — document exact opt-in, verification commands/results and remaining blockers. Preserve retained plan and pinned candidate bytes.

**Acceptance Criteria:**
- Given explicit opt-in, when composing from the selected mapping, then the actual Timesheets project and domain minimum appear in the one Platform model with declared readiness and internal delivery wiring.
- Given a public request or absent production providers, when reviewing the local binding, then the module's existing listener guard and default-deny authorization remain intact and no fixture or operational certification is installed.
- Given completed local validation, when recording evidence, then actual executed checks and their limits are explicit while production/release/Story 3.6 gates remain open.

## Implementation Notes

- User explicitly authorizes reversible implementation and validation before approval, superseding intermediate spec approval and the dirty-tree halt. Initial branch main; only pre-existing modified root gitlinks Builds/EventStore/Parties/Timesheets. Preserve their checkout/index state. No sprint story key applies to this Platform prerequisite.
- No unresolved user intent gaps or irreversible actions. Footprint is Platform composition helper/configuration, focused tests and documentation; no owner source mutation. Unknown API details must be verified locally or through Aspire docs, not invented.

- Implementation completed: 19 focused cases passed; full composition runner 73 passed/3 opt-in live skips; composition and mapped Timesheets host builds passed with zero warnings/errors; Python 31/31. Logs and earlier attempts are recorded in `eng/timesheets-deployment/local-host-binding-qualification.md`.
- Matrix audit: every scenario ran in the focused class or existing default graph tests; source ownership and mixed preview refusals were tightened. Seven protected hashes and all index gitlinks match the pre-implementation snapshot. No live startup or owner-source edits.

- Review repairs completed: source-plan selection/hash binding replaces the stripped Builds flag; SDK JWT validation and gateway audience alignment, physical source ownership, canonical candidate deduplication, actual missing-source cases, positive Works routing and typed full registration checks are verified. All accepted findings were patched; no findings were deferred.
- Final verification: focused Timesheets 35/35; full composition 89 passed/3 expected live skips (92 total); mapped source host build zero warnings/errors; Python 31/31. Seven protected hashes and all index gitlinks remain unchanged. This Platform prerequisite spec is done; Timesheets Story 3.6/Epic 3 remain in progress and release FAIL. Implementation remains uncommitted for review, consistent with the frozen reversible-work boundary.

## Spec Change Log

## Review Triage Log

| Finding | Verdict / route | Evidence |
| --- | --- | --- |
| Blind 1 | medium / patch | Builds StartAppHost supplies a scrubbed environment without the Platform Timesheets flag; the test injects it manually. Use the explicitly selected, hash-bound Timesheets source plan as its durable run opt-in. |
| Blind 2 | medium / patch | ConfigureWorkloadJwtBearerOptions narrows ValidAudiences to the receiver option; Platform sets hexalith-eventstore while Timesheets requests eventstore. Align only the enabled local internal receiver with its client contract and verify real SDK options. |
| Blind 3 | medium / patch | SourceMapping.ContainsProject resolves links under any mapped root; lexical canonical-path equality admits a Timesheets project symlink to EventStore. Enforce physical Timesheets-root containment. |
| Blind 4 | low / patch | Supplied paths are absolute but auto-added candidate is not; relative PlatformRoot can duplicate the same declaration. Normalize its path. |
| Blind 5 | medium / patch | SDK contract rejects short keys, empty issuer and incompatible algorithms, but preparation currently accepts them. Reuse the SDK validator and test bounded refusal before resources. |
| Blind 6 | medium / patch, grouped with Blind 1 | Ambient opt-in can append a candidate absent from the signed selected declaration. Require the Timesheets candidate as the hash-bound Builds selection; never expand a different signed plan. |
| Blind 7 | low / patch | The missing.csproj override exercises canonical-path refusal rather than missing canonical source. Add a temporary mapped root missing the real host. |
| Blind 8 | low / reject | Graph tests do not prove a running host's readiness or allocated CLI listeners; this limit is already explicit in evidence. No incorrect runtime outcome was demonstrated. A separate live smoke exceeds the smallest correction and is not required to claim the focused graph/build results. |
| Blind 9 | low / patch | /tmp logs are transient, though the document calls them retained local evidence. Mark their location and preservation snapshot as temporary local diagnostics with no durable artifact claim. |
| Edge 1 | low / patch, grouped with Blind 4 | Same unnormalized auto-added path can duplicate an absolute supplied candidate. |
| Edge 2 | low / reject | A malformed structured caller-index override really raises FormatException/OverflowException before resources, still failing closed. The invalid non-array configuration is not an everyday input; adding index guards is unnecessary to the supported owner contract. |
| Edge 3 | medium / patch, grouped with Blind 3 | Physical containment under another mapped root does not establish Timesheets executable ownership. |
| Verification 1 | medium / patch | Pre-verified: the moved Works dispatch is never tested positively through ComposeEntry; preserve the existing graph fixture and exercise that caller. |
| Verification 2 | medium / patch | Pre-verified: registration checks omit AppId; a wrong gateway destination could pass. Bind and assert the complete generated registration. |

## Verification

- Build `tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj` with pinned .NET 10.0.401, disabled build servers and serialized MSBuild; execute its focused and then full individual test runner.
- Build the mapped Timesheets host using the existing root EventStore path explicitly, without nested initialization. Record any actual dependency/source limitation.
- Run offline preparation Python suite if documentation/helpers affect its assumptions; `git diff --check`; verify owner checkpoint/report/tracking and candidate/plan preservation.
