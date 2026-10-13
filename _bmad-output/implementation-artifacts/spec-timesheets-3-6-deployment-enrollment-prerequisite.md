---
title: 'Prepare the Timesheets Story 3.6 hosted enrollment prerequisite'
type: 'feature'
created: '2026-10-13'
status: 'done'
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
- [x] `eng/timesheets-deployment/hexalith.module-manifest.v2.json` — declare the single Timesheets host, dependencies, surface boundaries, provider needs and rebuild inventory as a candidate, outside active composition.
- [x] `eng/timesheets-deployment/prepare.py` — generate a local blocked plan from exact source bytes and existing backlog; accept no authority inputs and execute only bounded read-only Git commands.
- [x] `eng/timesheets-deployment/plan.json` and `README.md` — retain reproducible candidate identity, exact proposed target/endpoints/ownership, prerequisite matrix, approval boundary and execution/rollback sequence.
- [x] `eng/timesheets-deployment/test_prepare.py` and `tests/Hexalith.Platform.Composition.Tests/TimesheetsEnrollmentTests.cs` — verify missing/dirty source and exclusive-write refusals, immutable denial gates, actual v2 validation and surface/dependency safety.

**Acceptance Criteria:**
- Given the existing hosting architecture, when preparing Timesheets, then the proposed target is staging `hexalith-staging` / `hexalith-staging-data` on `jpiquot@local`, with `timesheets.hexalith.com` and internal 8081 kept private.
- Given absent operational providers or retained artifacts, when preparation succeeds, then the plan records exact missing production bindings, certified inventory/maintenance/continuity and subscriber/shared-rebuild proof, with deployment unauthorized and acceptance FAIL.
- Given the existing Timesheets root submodule, when preparation inspects source identity, then its full revision is retained without update and any candidate revision difference remains an explicit enrollment blocker.
- Given the reversible implementation, when validation finishes, then a reviewable plan exists before any infrastructure approval is requested and legacy state/submodules/Timesheets tracking remain untouched.

## Implementation Notes

- User explicitly requested completing reversible implementation and validation before an infrastructure approval request. This authorizes the local build work and supersedes the workflow's intermediate spec-approval checkpoint. No infrastructure approval is inferred.
- Initial Platform revision: `be294641fe06490b7f1d9788f383512088842e7d`; pre-existing modified nested references: ChatBot, Conversations, EventStore. Preserve them.
- This is a Platform prerequisite increment, not Platform Epic 3 Story 3.6; no sprint story key is selected.
- Concurrent external `/pushall` advanced Platform to `abfd2496a2c32829944fd819f5f401db5e4b8123` and direct submodule pins, including Timesheets `a662ff130779524cbd759f76a018c5b330608eb3`; this run issued no such mutation. Selected latest sibling source is `0e336693234c70735a3c8cd289a1c195a21e49fd`; the plan records the difference as an enrollment blocker.
- Added the six scoped prerequisite files. Unsupported runtime/provider configuration remains empty with an explicit owner-complete declaration blocker. Exact confirm/adjust submit routes are included; internal SDK paths remain private. Existing root Timesheets declaration was retained.
- Implementation verification: SDK 10.0.401 Debug build passed with zero warnings/errors; full Composition.Tests passed 54 with three existing opt-in live skips (57 total). Independent parent checks passed Python 18/18 in 2.199s and focused C# 6/6 in 0.331s. The plan reproduced byte-for-byte at preparation hash `b454bc88af39ea1a7831760075dbeba63231c512ce36c27617925df49a67a00a`.
- Matrix audit: valid preparation/reproduction, missing/dirty/wrong-root inputs, existing/racing destination, inert local receipt/unsupported authority arguments, and server/surface/dependency drift each have executed passing tests. Schema drift uses mutation-specific canonical validator diagnostics. No infrastructure or domain operation occurred.
- Review repairs retain no-follow directory descriptors, publish complete output by exclusive hard link, protect actual worktree/common Git metadata, pin executing generator and the dated owner report, hash canonical/mapped owner declarations, enforce policies/callers, observe dirty direct checkouts without reading arbitrary untracked data, and normalize malformed JSON refusals. Every accepted finding is resolved; nothing was deferred.
- Concurrent external owner-pin commit advanced Platform to `422e7bf13e48a56b3d308eb1cfdd763c17a4b26b`; the refreshed plan records that exact preparation snapshot. Its preparation hash is `80aaf70967771c671e9245982a906ea9b77d56bdb6b568ee60015ad4e33305c7`. Candidate and direct Timesheets revisions remain distinct and blocked. A later local commit intentionally changes the next preparation's observed Platform HEAD, as documented.
- Final parent verification: Python 31/31 in 5.257s; `dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj --configuration Debug --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false` passed in 20.69s with zero warnings/errors; full individual C# runner passed 54/57 with three existing opt-in live skips in 1.376s. The initial ordinary rebuild stalled after restore and its own invocation was stopped; the serialized retry supplies the completed build result.
- Final plan reproduced byte-for-byte before commit. Scoped whitespace checks passed for all six implementation files. Original `.gitmodules`, both sprint tracking files, the supplied Timesheets acceptance document and latest checkpoint retain their initial hashes. This run issued no infrastructure, domain, publication or submodule update operation.

## Spec Change Log

## Review Triage Log

| Finding | Verdict / route | Evidence |
|---|---|---|
| Blind 1 | medium / patch | A reproduced parent replacement redirects the pathname writer into protected Timesheets source. Retain validated directory descriptors for publication. |
| Blind 2 | medium / patch | A reproduced write failure leaves truncated output and blocks retry. Publish only complete bytes with exclusive destination creation and owned temporary cleanup. |
| Blind 3 | low / patch | `prepare` resolves original root arguments before the symlink check, making that check ineffective for root aliases. Validate the supplied paths first. |
| Blind 4 | medium / patch | An alternate Platform root can supply different generator bytes from those executing, yielding an inaccurate generator identity. Require the selected bytes to match this executing generator. |
| Blind 5 | medium / patch | Changed committed owner reports can be attributed the hardcoded cluster UID/date. Bind those expected facts to the exact inspected qualification-report hash and refuse other reports. |
| Blind 6 | medium / patch | Candidate policy/caller changes to anonymous pass the preparer's policy checks. Assert the declared authorization, operation and internal caller boundaries. |
| Blind 7 | medium / patch | Owner declarations exercised by canonical validation/closure tests are absent from input hashes. Bind the three canonical snapshots and three mapped owner declarations. |
| Blind 8 | medium / patch | Dirty direct-checkout bytes do not change HEAD and currently produce no drift flag. Record observed cleanliness and block enrollment while preserving dirty work. |
| Blind 9 | false / reject | Platform HEAD is explicitly a recorded input, and README requires unchanged source revisions for byte reproduction. An empty commit changes that input, so a new preparation identity is expected; cross-revision equality is not promised. The retained artifact is a local preparation snapshot, not a committed release. |
| Edge 1 | medium / patch, grouped with Blind 1 | The output-ancestor race has the same pathname-resolution root cause and the same protected-source mutation outcome. |
| Edge 2 | medium / patch | A linked worktree's actual Git/common metadata lies outside its checkout; the lexical `.git` exclusion misses it. Inspect both repositories' actual metadata directories before output. |
| Edge 3 | medium / patch, grouped with Blind 2 | A failure during write/close leaves a truncated exclusively created destination. Complete output before publication. |
| Edge 4 | low / patch | Python digit-limit `ValueError` and deep-JSON `RecursionError` escape the existing JSON refusal conversion. Convert them to bounded refusal and test them. |
| Verification 1 | medium / patch | Pre-verified mutation evidence: removing the final Platform-input recheck leaves all 18 tests passing and permits mismatched backlog/hash publication. Add a changing-Platform-input regression asserting no output. |

## Verification

- `python3 -m unittest discover -s eng/timesheets-deployment -p 'test_*.py' -v` — offline policy and safe-file tests pass.
- Build/run `Hexalith.Platform.Composition.Tests` individually using pinned SDK 10.0.401 — actual Builds schema validator and enrollment boundary checks pass.
- Regenerate the plan against the selected clean Timesheets source and compare bytes; `git diff --check` — deterministic output and clean whitespace.
