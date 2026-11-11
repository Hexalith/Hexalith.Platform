---
title: '2.11 (part 3) Complete the Parties lifecycle caller'
type: 'feature'
created: '2026-11-11'
status: 'done'
baseline_commit: '34dfbcb9a584fd0c3a71d449c75e8e290b9c47bb'
builds_baseline_commit: 'b1681870d0b08a5fe103133214c67fda7a449eb1'
eventstore_baseline_commit: 'b882c1005050aaebfa70618b5a72c1c8be452281'
parties_baseline_commit: 'fc9ea3f3b7b8544f8a8f7b8f7912ceb45489766c'
story_key: '2-11-supervise-attached-runs-and-report-the-missing-lifecycle-pr'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/docs/contracts/test-environment-v1.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Parties caller lacks required scenarios; real attached suites encounter runner and SDK refusals.

**Approach:** Extend the caller with the runner/SDK prerequisites below.

**Decision (2026-11-11, user):** Include Builds and EventStore prerequisites, including the fixed-port request and attached fixture support, so part 3 covers every scenario.

## Boundaries & Constraints

**Always:** Preserve scenarios, outcomes/deadlines/expiry, retention, private access and withdrawal-before-teardown. Use same-job owners and real services. Keep the ready-artifact/operator-cancel protocol; failure-before-cancellation proves failure preservation. Finalize each owner; retain classified results and report missing proof.

**Never:** Forge claims, replace collisions with metadata locks, duplicate SDK credential guards in Parties, share owner gates or live access observations, change v1 schemas/policy, raise budgets, dispatch workflows, publish packages, update pins or claim Story 2.10 qualification.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Expected behavior | Failure handling |
| --- | --- | --- | --- |
| Collision | Challenger requests a live owner's endpoint port | HXR031 names blocker; owner and unrelated peer preserved | Other errors do not pass |
| Startup timeout | Pause exact Starting AppHost until original finite deadline | Failed startup timing/override preserved; exact cleanup | Missed window fails |
| Failure then cancellation | Durable failed result before operator cancellation | Same first outcome/time after cleanup/retry | No ordinary cancellation credit |
| Owner cancellation | Running attached suite before owner cancellation | Withdrawn; work/access ended before owner teardown | Failed termination/leftovers fail |
| Attached failure | Real attached child intentionally fails | Failed attached result; original owner outcome preserved | Local retention/CI cleanup unchanged |
| Hold expiry | Live attached child reaches original expiry | Withdrawn/hold-expired; terminated work/access; peer survives | Retry cannot renew expiry |
| Invalid handoff | Expired/ended/foreign hold or mismatched snapshot | No fixture clients or qualifying result | Explicit refusal |

</frozen-after-approval>

## Code Map

Paths use repository-relative aliases: B = `references/Hexalith.Builds`, E = `references/Hexalith.EventStore`, P = `references/Hexalith.Parties`; R = `B/src/libraries/Hexalith.Builds.Tooling/Runtime`.

- `R/CompositionPortAllocator.cs` avoids/retries conflicts; the caller needs an explicit resource request.
- `R/NativeTestExecutor.cs:400` refuses routing-required attachments; `R/SelectedRoutingCatalogSnapshot.cs` validates public owner snapshots.
- `E/src/Hexalith.EventStore.Testing/TestEnvironmentDescriptor.cs:25` rejects attached roles.
- `P/scripts/qualify-disposable-ci.py`: reuse journal/gate, PID checks, startup pause, peer inventory and finalizer.
- `P/tests/Hexalith.Parties.Platform.IntegrationTests/NativeDiagnosticBinding.cs`: preserve owner-only diagnostics; attachments use the shared fixture separately.

## Tasks & Acceptance

Dependency order:

- [x] `B/src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs`, `R/ModuleCommandExecutionService.cs`, `R/CompositionEngineOptions.cs`, `R/CompositionEngine.cs`, `R/CompositionPortAllocator.cs`: add optional `--eventstore-port`; validate, claim and bind it. Preserve independent defaults and blockers.
- [x] `R/NativeTestExecutor.cs`, `R/SelectedRoutingCatalogSnapshot.cs`: permit validated immutable routing snapshots for attachments; retain isolated output, owner/environment checks and gate/access-observation refusals.
- [x] `E/src/Hexalith.EventStore.Testing/TestEnvironmentDescriptor.cs`: consume ready attachments only with matching active unexpired holds; bound fixture requests by expiry using existing client cancellation.
- [x] New `P/tests/Hexalith.Parties.Platform.AttachedLifecycleTests/{Hexalith.Parties.Platform.AttachedLifecycleTests.csproj,AttachedLifecycleQualificationTests.cs}`, `P/scripts/render-platform-declaration.py`, `P/module/hexalith.module-manifest.v2.json`, `P/Hexalith.Parties{,.Standalone}.slnx`: declare a separate attached suite using shared clients and bounded real-service reads; wait finitely for controls and publish atomic markers. Exclude it from ordinary solution builds. Preserve owner persistence assertions.
- [x] `P/scripts/qualify-disposable-ci.py`, `P/.github/workflows/ci-lifecycle-qualification.yml`: expose individual cases and extend matrix accounting. Journal attached launches before release; correlate one exact admission with owner/job/profile/lineage, recover without an owner receipt, retain attached results, and reject ambiguous identities. Add separate failure-then-cancellation dispatch.
- [x] `B/test/Hexalith.Builds.Module.Tests/{AttachedSuiteExecutionTests,NativeTestExecutorTests,StartupReadinessTests,CompositionResourceClaimStoreTests}.cs`, `E/tests/Hexalith.EventStore.Testing.Tests/TestEnvironmentDescriptorReaderTests.cs`, `P/tests/ci-lifecycle/`, `P/scripts/test_platform_declaration.py`: cover every matrix row, launch recovery, invalid port/hold, mismatch and cleanup failure.
- [x] `B/README.md`, `E/docs/reference/nuget-packages.md`, `P/docs/ci-lifecycle-qualification.md`, `_bmad-output/implementation-artifacts/evidence/2-11/part-3/README.md`: document usage, exact revisions, checks and release dependencies.

**Acceptance Criteria:**
- Given supported prerequisites, when a case runs, then its real mechanism and contract records are verified; missing evidence fails.
- Given failure/interruption, when finalization runs, then all issued owners receive exact cleanup and unrelated resources survive.

## Implementation Notes

Approved by the user on 2026-11-11 with “approve and stop”. The frozen intent is locked. Implementation, task/matrix audit, independent review and final verification are complete.

Baseline: 106/106 caller tests. Recheck concurrently advancing heads; preserve incoming work. Package/hosted verification requires compatible releases.

## Spec Change Log

## Review Triage Log

### Review pass 1 (2026-11-11)

All three layers completed before triage. B = blind, E = edge-case, V = verification-gap. Every finding is recorded separately before grouping.

| Finding | Verdict | Route | Verified evidence |
| --- | --- | --- | --- |
| B1 | medium | patch G1 | The caller expects owner-cancelled, but the actual CI branch in Builds ModuleCommandExecutionService withdraws and drains as ci-job-cancelled. The hosted case rejects correct behavior. |
| B2 | medium | patch G2 | containers uses docker ps -aq and accepts any remaining container. Builds stops its AppHost before taking provisioning.lock, so that lock and a stopped surviving container cannot prove ordering. Require the exact original running owner inventory at the termination observation and fail missing proof. |
| B3 | medium | patch G3 | attached_result hashes one diagnostic buffer then reopens its path. A changed buffer can be retained under the earlier identity, and a vanished report is skipped. Keep the validated result and diagnostic buffers for retention. |
| B4 | high | patch G4 | Any diagnostic labeled redacted/public or internal with a json/trx suffix passes, including native-endpoint-credentials.json. Reuse the technical helper's exact diagnostic identity/filename allowlist before copying. |
| B5 | medium | patch G5 | attached_case waits for the owner but checks only its archived first status; Builds can preserve a passed first test while reporting later cleanup/evidence failure. Check the original command exit and public diagnostics before any successful retry can hide them. |
| B6 | medium | patch G6 | A failed/test-failure result with any native-report TRX passes the intentional-failure scenario. A service-read failure after readiness is reachable and has the same cause. Require the actual test's identifiable intentional assertion in the held report. |
| B7 | low | reject | The new controller does not independently implement the complete v1 reader, so malformed manually substituted files can satisfy its selected checks. The production publisher calls TestEnvironmentContractReader.ReadResult before atomic publication (TestResultWriter.PublishAsync), and this controller grants no qualification/candidate acceptance. This rare malformed-producer gap would require a new validator/dependency or public surface rather than a direct correction; reject under the low-finding complexity rule. Existing contract-reader validation remains required. |
| B8 | medium | patch G7 | Path.exists treats dangling private-file links as absent. Detect directory entries themselves so a surviving descriptor or credential entry cannot pass termination proof. |
| B9 | medium | patch G8 | The new attached project is built for every selection although its SDK properties are unavailable at unchanged package pins. Old focused/startup selections acquire an unrelated prerequisite. Build it only for attached selections and matrix. |
| B10 | medium | patch G9 | The new accounting test replaces the scenario methods with mocks, leaving orchestration unexecuted. Add focused resource-free method tests covering actual cause selection, preserved command failures and teardown proof, plus collision/timeout flows. |
| E1 | medium | patch G1 | Same verified CI cause mismatch as B1; preserve this separate finding and fix the shared expected-cause selection. |
| E2 | medium | patch G3 | Same verified validated-byte reread as B3, affecting both the result and diagnostic artifacts. |
| E3 | medium | patch G3 | Same reread root cause also lets an artifact disappear before retention without failing. Required validated evidence must be retained from held bytes. |
| V1 | medium | patch G10 | Pre-verified regression gap: malformed port CLI tests and direct allocator tests do not exercise valid CLI-to-engine forwarding. Add command-level exact-port and real managed-conflict assertions. |
| V2 | medium | patch G11 | Pre-verified regression gap: all execution-cancellation inputs omit attachedExecutionCheckpoint. Removing its new identity guard in memory still passed all 154 controls. Cover matching, missing, changed PID and changed birth snapshots. |

The surviving groups all have direct internal corrections or focused test additions, add no public surface, and address demonstrated inputs. No frozen intent or spec amendment is needed; no loopback or deferral applies.

## Verification

Build changed C# projects in Debug with source references and `-nodeReuse:false`; run affected xUnit classes directly. From P run `python3 -B -m unittest discover -s tests/ci-lifecycle -v`, `python3 -B scripts/render-platform-declaration.py --check`, `python3 -B scripts/test_platform_declaration.py`, and `actionlint .github/workflows/ci-lifecycle-qualification.yml`. Confirm schema bytes unchanged.


### Implementation acceptance audit (2026-11-11)

- Read the complete task diff across Platform, Builds, EventStore and Parties using isolated temporary Git indexes, preserving existing staged work and gitlinks. All seven implementation tasks are complete.
- Every frozen matrix row has covering tests in the executed Builds, EventStore or Parties controls. The source hashes match the saved verification inputs; selected xUnit classes report no failures, skips or unexecuted tests.
- Verified saved output: Builds 160/160 focused tests plus the final 47/47 attached-suite run after the two routing cases were added; EventStore 62/62; Parties controller 154/154, declaration 7/7 and focused C# controls 28/28. All recorded Debug builds, declaration renderer and actionlint checks pass.
- Independently compared all five authoritative/mirrored v1 schema files against their recorded hashes and owning repository HEAD bytes; all are unchanged.
- [Evidence and exact verification commands](evidence/2-11/part-3/README.md) distinguish local controls from the compatible published prerequisites and actual hosted qualification still required. No package publication, pin change, workflow dispatch or Story 2.10 qualification is claimed.

### Review correction verification (2026-11-11)

- All 11 patch groups are corrected and the full correction diff was read. No frozen intent, public contract, dependency pin or schema was changed.
- Final verification from the corrected tree: Builds 163/163; EventStore 62/62; Parties focused C# 28/28, controller 167/167 and declaration 7/7. All 427 final test cases pass, with zero selected xUnit skips or unexecuted tests. Four Debug builds have zero warnings/errors; renderer and actionlint pass.
- [Final matrix audit](evidence/2-11/part-3/checks/review-pass-1-matrix-audit.json) records executed passing coverage for all seven frozen rows. Final source/document hashes are stable; all five v1 schema files still match owning HEAD bytes.
- The three review layers produced 15 findings, recorded individually above. Fourteen were corrected in 11 groups; B7 was rejected under the low-finding complexity rule. No finding was silently dropped or deferred.
- Local delivery: Builds `bfda07eea7dcc8ef0a6da0fa14643aa70eebec78`, EventStore `04e56e1c1d4657d6fbdea0aa03a5f434c0fe51e5`, Parties `e35b8c1eff0184c95444af9a01a8beba78f87110`. Exact messages passed pinned commitlint 21.2.4. No push or gitlink/pin update. The only source-tree edit after verification removes stale “uncommitted” wording from Parties documentation; all tested code bytes are unchanged.
