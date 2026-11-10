---
title: '2.11 (part 3) Complete the Parties lifecycle caller'
type: 'feature'
created: '2026-11-11'
status: 'ready-for-dev'
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

- [ ] `B/src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs`, `R/ModuleCommandExecutionService.cs`, `R/CompositionEngineOptions.cs`, `R/CompositionEngine.cs`, `R/CompositionPortAllocator.cs`: add optional `--eventstore-port`; validate, claim and bind it. Preserve independent defaults and blockers.
- [ ] `R/NativeTestExecutor.cs`, `R/SelectedRoutingCatalogSnapshot.cs`: permit validated immutable routing snapshots for attachments; retain isolated output, owner/environment checks and gate/access-observation refusals.
- [ ] `E/src/Hexalith.EventStore.Testing/TestEnvironmentDescriptor.cs`: consume ready attachments only with matching active unexpired holds; bound fixture requests by expiry using existing client cancellation.
- [ ] New `P/tests/Hexalith.Parties.Platform.AttachedLifecycleTests/{Hexalith.Parties.Platform.AttachedLifecycleTests.csproj,AttachedLifecycleQualificationTests.cs}`, `P/scripts/render-platform-declaration.py`, `P/module/hexalith.module-manifest.v2.json`, `P/Hexalith.Parties{,.Standalone}.slnx`: declare a separate attached suite using shared clients and bounded real-service reads; wait finitely for controls and publish atomic markers. Exclude it from ordinary solution builds. Preserve owner persistence assertions.
- [ ] `P/scripts/qualify-disposable-ci.py`, `P/.github/workflows/ci-lifecycle-qualification.yml`: expose individual cases and extend matrix accounting. Journal attached launches before release; correlate one exact admission with owner/job/profile/lineage, recover without an owner receipt, retain attached results, and reject ambiguous identities. Add separate failure-then-cancellation dispatch.
- [ ] `B/test/Hexalith.Builds.Module.Tests/{AttachedSuiteExecutionTests,NativeTestExecutorTests,StartupReadinessTests,CompositionResourceClaimStoreTests}.cs`, `E/tests/Hexalith.EventStore.Testing.Tests/TestEnvironmentDescriptorReaderTests.cs`, `P/tests/ci-lifecycle/`, `P/scripts/test_platform_declaration.py`: cover every matrix row, launch recovery, invalid port/hold, mismatch and cleanup failure.
- [ ] `B/README.md`, `E/docs/reference/nuget-packages.md`, `P/docs/ci-lifecycle-qualification.md`, `_bmad-output/implementation-artifacts/evidence/2-11/part-3/README.md`: document usage, exact revisions, checks and release dependencies.

**Acceptance Criteria:**
- Given supported prerequisites, when a case runs, then its real mechanism and contract records are verified; missing evidence fails.
- Given failure/interruption, when finalization runs, then all issued owners receive exact cleanup and unrelated resources survive.

## Implementation Notes

Approved by the user on 2026-11-11 with “approve and stop”. The frozen intent is locked; implementation has not started.

Baseline: 106/106 caller tests. Recheck concurrently advancing heads; preserve incoming work. Package/hosted verification requires compatible releases.

## Spec Change Log

## Review Triage Log

## Verification

Build changed C# projects in Debug with source references and `-nodeReuse:false`; run affected xUnit classes directly. From P run `python3 -B -m unittest discover -s tests/ci-lifecycle -v`, `python3 -B scripts/render-platform-declaration.py --check`, `python3 -B scripts/test_platform_declaration.py`, and `actionlint .github/workflows/ci-lifecycle-qualification.yml`. Confirm schema bytes unchanged.
