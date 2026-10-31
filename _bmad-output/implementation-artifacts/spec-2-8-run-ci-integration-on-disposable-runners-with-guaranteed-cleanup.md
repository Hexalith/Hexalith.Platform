---
title: '2.8 Run CI integration on disposable runners with guaranteed cleanup'
type: 'feature'
created: '2026-10-31'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '03a099bba258c37a8f16c8b2e842ae842b1ad6d6'
builds_baseline_commit: 'a22b55f0a4f6d2aaa3a5ccc505896064ac595364'
story_key: '2-8-run-ci-integration-on-disposable-runners-with-guaranteed-cle'
context:
  - '{project-root}/docs/contracts/candidate-and-attachment-policy-v1.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.Builds/AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** CI lacks runner-owned integration and unconditional cleanup; CI attachments are refused.

**Approach:** Run isolated tests before blocking package integration on disposable Linux GitHub-hosted runners; withdraw same-job attachments before teardown.

## Boundaries & Constraints

**Always:**
- Decision 2026-10-31: full story in stages, including package prerequisites/reference enrollment. Publication approval is separate; no remote implementation operations.
- Isolated tests pass without servers before provisioning. Integration requires declared suites, the exact pinned tool, Release/package dependencies and real services; missing prerequisites fail.
- Admit compatible active CI owners within the exact repository/run/attempt/job. Preserve compatibility, policy and expiry checks; reject completed owners.
- Persist ownership before allocation. After success, failure, cancellation or partial startup, bounded cleanup uses that receipt and an unconditional workflow finalizer.
- Close admission, record/report active attachments as withdrawn, then stop resources. Preserve first outcomes and original expiry. CI never retains failure resources or waits beyond job completion for holds.
- Cleanup is idempotent and ownership-limited. Unknown discovery or owned leftovers prevents completion. Preserve redacted results outside deleted workspaces.
- No staging/production provider credentials; registry access is pull-only. Preserve publication/provenance gates.
- Completion requires reference package CI and cancellation/partial-startup evidence.

**Never:** Change policy/schema semantics or local retention; claim candidate acceptance; implement deferred attached execution; publish or change accepted pins without release decisions.

## I/O & Edge-Case Matrix

| Scenario | Expected behavior |
| --- | --- |
| Isolated failure | No integration provisioning; blocking failure |
| Integration success/failure | Preserve outcome; withdraw active holds, then exact cleanup |
| Cancellation during startup/execution | Recover durable owner identity; withdraw, cleanup, retain diagnostics |
| Same-job/cross-job attachment | Admit compatible active owner / refuse before acceptance |
| Cleanup failure or unknown discovery | Non-success with explicit leftovers; retry permitted |
| Missing tool/suite/package preparation | Fail before acceptance; no legacy advisory fallback |

</frozen-after-approval>

## Code Map

B = `references/Hexalith.Builds`; R = `B/src/libraries/Hexalith.Builds.Tooling/Runtime`; T = `B/test/Hexalith.Builds.Module.Tests`; W = `B/.github/workflows`; E/P = `references/Hexalith.EventStore`/`references/Hexalith.Parties`.

- `R/CompositionAttachAdmission.cs`: retain `CheckContext`; remove blanket CI refusal.
- `R/CompositionEngine.cs`: `CanHoldAttachments`, session down and `DownOwnedCoreAsync` currently assume local attachments. Preserve partial-start cleanup and exact-resource verification.
- `R/CompositionAttachmentStore.cs`: extend `WithdrawAsync` to existing CI end reasons. `R/CompositionRunListing.cs`: restrict failure retention to local owners.
- `R/ModuleCommandExecutionService.cs`: add CI withdrawal without local hold waiting.
- `R/CompositionEngineOptions.cs`, new `R/CompositionCiOwnerReceipt.cs`, `B/src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs`: add `test --ci-owner-receipt`; persist exact job/owner identities before allocation, refuse an unwritable receipt.
- `W/domain-ci.yml`: replace direct integration/advisory paths; gate provisioning on isolated results. Reuse `B/Github/prepare-module-build/prepare_module_build.py` exact-tool/package preparation.
- New `B/Github/run-module-integration/run_module_integration.py`: execute and finalize via receipt; invoke exact `down --run-id`, validate job binding and propagate failures.
- `src/Hexalith.Platform.Composition/{PlatformNativeTestPreparation,PlatformAppModel,PlatformRoutingCatalogBootstrap,PlatformRoutingCatalog,PlatformTenantBootstrapAccess}.cs` and `Hexalith.Platform.Composition.csproj`: source guards/package refusals block native setup and catalog binding.
- `E/module/hexalith.module-manifest.v2.json`, new `E/tests/Hexalith.EventStore.Platform.IntegrationTests/`, E/P `.config/dotnet-tools.json` and `.github/workflows/ci.yml`: enroll descriptor-only reference integration. P declares `platform-package`, awaiting migrated E testing APIs.
- E/P solutions, P `scripts/render-platform-declaration.py`, B `Props/Directory.Packages.props`: preserve generated declarations and package authority; prepare compatible release inputs.

## Tasks & Acceptance

- [x] Receipt/CLI files above — preallocation recovery.
- [x] Attachment/engine/service/listing files above — job-bound admission and withdrawal before cleanup; preserve local semantics.
- [x] Workflow/helper above — blocking integration/finalization; retire advisory.
- [x] Platform composition files above and `tests/Hexalith.Platform.Composition.Tests/{PlatformNativeTestPreparationTests,PlatformAppModelTests}.cs` — enable/test package-native setup and catalog prerequisites.
- [ ] Reference files above — enroll real gateway/persistence and Parties suites; prepare compatible packages and publication handoff, then qualify hosted CI before completion.
- [x] `T/CiRunLifecycleTests.cs`, existing `T/AttachAdmissionTests.cs`, `T/CompositionEngineTests.cs`, `T/ModuleRunnerReviewRegressionTests.cs` — matrix/local regressions, peers, retries and first outcomes.
- [x] New `B/Github/run-module-integration/tests/test_run_module_integration.py`, `B/Tools/test-domain-workflow-test-platforms.ps1`, `W/ci.yml` — executable controls.
- [x] `W/domain-ci.md`, `B/README.md`, `docs/contracts/test-environment-v1.md`, `_bmad-output/implementation-artifacts/evidence/2-8/README.md` — migration and evidence.

**Acceptance Criteria:**
- Given disposable Linux hosted CI, when isolated tests pass, then runner-owned package integration is required and advisory AppHost testing supplies no acceptance.
- Given any CI terminal path, when teardown starts, then active attachments have ended and verified completion requires zero owned leftovers.
- Given an attachment request, when job identity differs, then admission refuses; a compatible same-job request succeeds.
- Given CI permissions and inputs, when inspected, then no hosted provider credential or registry write access is available.

## Implementation Notes

Local implementation is staged across Builds, Platform, EventStore and Parties. The full mapped Platform build passes; the Builds broad graph retains its pre-existing HXW005 FrontComposer source/package collision, with focused source diagnostics and exact reused-artifact hashes recorded separately. Published EventStore 3.128.4 lacks the migrated descriptor APIs, so caller activation and hosted qualification remain pending approved coordinated releases. See `evidence/2-8/README.md`, `verification.json` and `release-inputs.json`.

Coordinator final audit reran all 15 helper tests and 14 named CI lifecycle cases successfully and verified current release-source hashes and unchanged frozen intent. Both independent audit findings are resolved: constructor-time cancellation is forwarded once, and five executing CI cases observe committed withdrawal at the first AppHost stop. Final Builds coverage is 238 cases; Platform controls are 13. The remaining unchecked task is approved publication/reference activation and hosted qualification. Full story acceptance is not claimed.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Action / evidence |
| --- | --- | --- |
| Signal during `Popen` can leave the newly created child uninterrupted | medium | Resolved: recheck cancellation after child assignment; constructor-time and real-subprocess signal controls pass (helper 15/15). |
| CI withdrawal ordering lacks an executing selected-suite/partial-startup stop observer | medium | Resolved: five executing service/partial-startup cases use a live AppHost stop observer; the full CI class passes 14/14. Source fixtures and simulated CI supply no hosted credit. |

## Verification

From B: build Module.Tests in Debug; run explicit xUnit classes with `-parallelMode none`, helper unittests and `pwsh -File Tools/test-domain-workflow-test-platforms.ps1`. Run individual Platform/reference projects. Record broad blockers. Hosted qualification requires approved publication/remote execution and revision-bound lifecycle/cleanup observations.
