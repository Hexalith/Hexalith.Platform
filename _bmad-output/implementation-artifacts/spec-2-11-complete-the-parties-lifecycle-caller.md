---
title: '2.11 (part 3) Complete the Parties lifecycle caller'
type: 'feature'
created: '2026-11-11'
status: 'in-progress'
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

### Review Findings

Code review pass 2 (2026-11-12) of the delivered commits: Builds `b1681870..bfda07ee`, EventStore `b882c100..04e56e1c`, Parties `fc9ea3f3..e35b8c1e` (33 files, 2,579 diff lines). Platform evidence files were left out. All four layers ran with no failures: blind-hunter, edge-case-hunter, verification-gap and acceptance-auditor. They raised 53 findings. Triage produced 2 decision-needed entries (both resolved to patch), 19 patch, 2 defer, and 12 rejected findings. Line anchors refer to the delivered commits.

- [ ] [Review][Patch] `--eventstore-port` is claimed but never bound in Platform v2 compositions, so `collision` proves only a claim conflict [src/Hexalith.Platform.Composition/PlatformAppModel.cs:309] (high; verification-gap+edge-case-hunter; decision resolved 2026-11-12: option (a), bind it in v2)
  - In v2, `RunTopology.ComposePlatform` hands the plan to `PlatformAppModel.ComposeRun`. That model registers eventstore with `WithHttpEndpoint(name: "http")` and no port (`src/Hexalith.Platform.Composition/PlatformAppModel.cs:309`). Nothing in v2 reads `plan.Ports.EventStoreHttp`; only the legacy `RunTopology.ConfigureEventStore` path binds it (`RunTopology.cs:110`).
  - Parties uses a v2 manifest. An owner started with `--eventstore-port N` therefore claims and probes N, while EventStore listens on a port Aspire assigns.
  - The challenger's `HXR031` comes from the runner claim store on an unbound plan port, not from "the live owner's EventStore endpoint". The Builds README and the Parties document both claim the live endpoint.
  - `ExplicitPortCommandForwardsAndNamesManagedOwnerAsync` only covers a v1 manifest with a fake AppHost.
  - Frozen matrix row: "Challenger requests a live owner's endpoint port"; Never: "replace collisions with metadata locks".
  - Options:
    - (a) Bind eventstore HTTP to `plan.Ports.EventStoreHttp` in `PlatformAppModel` when a run plan exists. This is a Platform model change, so it needs a Platform release and the Parties model pin.
    - (b) Refuse `--eventstore-port` for v2 with `HXC002` and choose another genuine resource that v2 binds from the plan.
    - (c) Accept the claim-level conflict and correct the README and docs.
- [ ] [Review][Patch] The ten-case `matrix` cannot finish inside the unchanged 45-minute qualification job [references/Hexalith.Parties/.github/workflows/ci-lifecycle-qualification.yml:55] (high; blind-hunter+acceptance-auditor+edge-case-hunter; decision resolved 2026-11-12: option (a), raise only this job's `timeout-minutes` to 90. The user confirmed that the frozen "Never raise budgets" covers lifecycle and policy deadlines, not CI wall-clock time)
  - `timeout-minutes: 45` is unchanged (`ci-lifecycle-qualification.yml:55`).
  - Measured in hosted run 42727219072: the whole job took 15.3 minutes, and the four-case matrix step 827 s. Owner startups took 165–230 s.
  - The six added cases cost roughly 40 more minutes:
    - `startup-timeout` waits the 600 s default deadline plus up to 95 s;
    - `collision` and `hold-expiry` each start two owners;
    - every attached case starts its own owner.
  - So `fullMatrixPassed` can never become true. The `always()` finalizer shares the same budget.
  - Options:
    - (a) Raise the job timeout. This is a CI wall-clock change, not a lifecycle deadline, but it touches the frozen "Never raise budgets".
    - (b) Split the matrix across jobs and aggregate the campaign.
    - (c) Treat the per-case dispatches as the qualification route and re-scope `matrix`.
- [ ] [Review][Patch] `collision` and `hold-expiry` start their peer only after the owner suite's 240 s bound has begun [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1394] (high; edge-case-hunter)
  - `LifecycleQualificationTests` cancels itself 240 s after its test starts.
  - `self.start(owner)` returns once the owner is executing. Only then does `self.start('collision-peer')` or `self.start('expiry-peer')` (`:1433`) run a complete peer startup, which took 165–230 s in hosted CI.
  - `hold-expiry` then adds the attached launch, the 120 s hold and two lineage probes before `pass`, so the owner suite's bound expires first and the case fails deterministically. `collision` leaves roughly 0–55 s for the challenger.
  - Fix: `begin()` both owners and then `ready()` both, as `retry_with_peer` already does (`:1529`). After the fix, `hold-expiry` stays tight at about 160–200 s of about 210 s; record that margin.
- [ ] [Review][Patch] `invalid_lineage` accepts any `HXR033`/`HXR036` refusal [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1490] (medium; blind-hunter+edge-case-hunter)
  - Builds emits a specific reason for each refusal:
    - `HXR036 attachment-ended` / `attachment-expired` from `Retry`;
    - `HXR036 unknown-attachment-request`;
    - `HXR033` for unrelated preconditions such as `owner-not-live` and `attacher-context-unverifiable` (`CompositionAttachAdmission.cs:313-331`, `:241`, `:284`).
  - An unrelated refusal therefore passes the hold-expiry and invalid-handoff lineage proofs.
  - Fix: require `HXR036` with the exact expected reason for each presented lineage: expired, ended or unknown.
- [ ] [Review][Patch] The attached CLI's own exit code and public diagnostics are never verified [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1456] (medium; acceptance-auditor)
  - `attached_case` waits on the attached child but never inspects its return code or diagnostics.
  - `finalize_attached` (`:1376`) checks only the public `runId`.
  - Owners pass through `verify_original_owner_command`. An attached command that reports a cleanup or evidence diagnostic alongside a valid result passes unnoticed.
  - Fix: assert the attached exit code and diagnostic categories for each expected cause, using the Builds attached result mapping.
- [ ] [Review][Patch] Builds README: the new section sits under License and earlier passages now contradict it [references/Hexalith.Builds/README.md:1399] (medium; blind-hunter)
  - `:641-643` still says "the Parties caller is not adopted, owners requiring routing-catalog observations fail an attached run as `startup-failure`".
  - `:752-756` says claim conflicts always retry up to 64 attempts; an explicit port never retries.
  - The `--attach-to` incompatibility list at `:635` omits `--eventstore-port`.
  - Fix: edit these in place and move the section. The explicit-port wording depends on the first decision above.
- [ ] [Review][Patch] `AttachedExpiryCancelsFixtureRequests` relies on a 1-second hold measured from the wall clock [references/Hexalith.EventStore/tests/Hexalith.EventStore.Testing.Tests/TestEnvironmentDescriptorReaderTests.cs:59] (medium; blind-hunter+verification-gap+edge-case-hunter)
  - The hold starts before the file write, schema evaluation and client creation.
  - On a loaded agent the constructor throws `HXT108`, or `CreateHttpClient` throws `OperationCanceledException`, so the test is flaky.
  - Fix: use a multi-second hold.
- [ ] [Review][Patch] The explicit-port parse test cannot tell a refusal from an accepted port [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/StartupReadinessTests.cs:37] (medium; verification-gap)
  - The test uses the manifest `unused`, so an accepted port also ends in `HXM005` with exit 1.
  - Fix: assert `HXC001` for each case.
- [ ] [Review][Patch] The explicit-port guards with `--attach-to`, `Down` and existing runs are untested [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs:450] (medium; verification-gap+blind-hunter+acceptance-auditor)
  - Deleting both the CLI guard and the `ModuleCommandExecutionService.cs:119` guard leaves every test passing.
  - Fix: add an attached `--eventstore-port` refusal to `RefusedRequestAdmitsNothingAndStartsNoChildAsync`, plus service-level cases.
  - Not required here: the CLI refuses this case with `HXC001`, while other attach-incompatible options use `HXC002`.
- [ ] [Review][Patch] The attached candidate-policy refusal has no test [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/NativeTestExecutor.cs:400] (medium; verification-gap+blind-hunter)
  - This is now the only guard keeping attached suites out of candidate-gated owners. Removing `CandidatePolicy is not null` leaves every test passing.
- [ ] [Review][Patch] Only the foreign-`ownerRunId` row of the routing-snapshot identity checks is tested [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/SelectedRoutingCatalogSnapshot.cs:83] (medium; verification-gap+blind-hunter)
  - Add rows for a sibling `runId`, the owner role on an attached identity, and a hold with a foreign owner or environment. Cover the owner-descriptor `role`/`ownerRunId` requirement too.
- [ ] [Review][Patch] Controller launch arguments for attached and collision launches are unverified [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:441] (medium; verification-gap+blind-hunter)
  - Mutation check: dropping `--eventstore-port`, `--attach-to`/`--hold-seconds` or the journaled `eventStorePort` leaves all 167 tests passing.
  - Fix: add `launch_owner` argument tests with `gated_exec` patched.
- [ ] [Review][Patch] Failure-then-cancellation is only tested at helper level [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1806] (medium; verification-gap)
  - Mutating the `start-cancellation --phase failure` capture, the `wait-cancellation` tolerance or the failure classification in `finalize_cancellation` leaves all tests passing.
  - Fix: add tests at `main()` level.
- [ ] [Review][Patch] The attached control/readiness handshake with the C# fixture is unverified [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1271] (medium; verification-gap)
  - `attached_control` is always mocked. The success path of `begin_attached` (`:1222`) and its marker checks are untested.
  - Mutation check: trimming the payload or deleting the marker binding leaves all tests passing.
  - Fix: assert the exact control keys, and that a mismatched marker is rejected. The C# compile lane waits for the Testing package release.
- [ ] [Review][Patch] The `invalid_lineage`/`invalid_handoff` refusal checks are always mocked [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1479] (medium; verification-gap+acceptance-auditor)
  - Replacing the whole `require` with `pass` leaves all tests passing.
  - Fix: patch `tool` to return success, an admission, the wrong rule or a changed ledger, and expect each to raise.
- [ ] [Review][Patch] The C# owner-label list must match Python `OWNER_LABELS`, but nothing checks it [references/Hexalith.Parties/tests/Hexalith.Parties.Platform.IntegrationTests/NativeDiagnosticBinding.cs:119] (medium; verification-gap+blind-hunter)
  - Drift breaks every later owner's native-diagnostic check in the hosted matrix.
  - Fix: add a test that ties the two sets together.
- [ ] [Review][Patch] Both lineage probes overwrite one retained evidence file [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1486] (low; acceptance-auditor+edge-case-hunter)
  - Both use the tag `invalid-attached-lineage`, and `tool()` writes `<label>-<tag>.json`.
  - Fix: use a distinct tag for each lineage.
- [ ] [Review][Patch] The EventStore attached-hold theory does not assert which rule refused each case [references/Hexalith.EventStore/tests/Hexalith.EventStore.Testing.Tests/TestEnvironmentDescriptorReaderTests.cs:21] (low; blind-hunter)
  - The `foreign`, `snapshot` and `missing` rows are refused earlier by reader binding validation, so the new constructor checks are never reached.
  - Fix: assert the expected rule for each row.
- [ ] [Review][Patch] The EventStore docs paragraph sits after the Next/Related footer and has no heading [references/Hexalith.EventStore/docs/reference/nuget-packages.md:672] (low; blind-hunter)
  - Move it into the `Hexalith.EventStore.Testing` section and update the "qualifies no attached execution" sentence there (`:541-543`).
- [ ] [Review][Patch] Two workflow step names are stale [references/Hexalith.Parties/.github/workflows/ci-lifecycle-qualification.yml:186] (low; blind-hunter)
  - "...passing failing partial-startup and exact retry scenarios" now runs ten cases; "Build the explicit declared lifecycle suites" builds one project.
  - Rename both, together with the name lookups in `test_review_fixes.py`.
- [ ] [Review][Patch] The new 8 MiB retained-artifact cap also applies to the journal and `observations.json` [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:315] (low; blind-hunter)
  - `write()` now goes through `write_retained_bytes`. Observed journals are 60–130 KB, so this is latent.
  - Fix: apply the cap only to retained artifact copies.
- [x] [Review][Defer] The owner-cancellation ordering proof may lose its polling race [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1442] — deferred: unverified, high if confirmed (blind-hunter+edge-case-hunter)
  - Builds calls `StopSessionAsync` immediately after the drain (`CompositionEngine.cs:1572-1585`).
  - The controller re-derives the running inventory with `docker ps`, a per-container `docker inspect` and `/proc` reads after it sees the result.
  - To settle it, measure the interval between attached result publication and the first owner process or container change against one poll cycle in a real run. Alternatively, prove the order from timestamps (`terminal.recordedAt` vs container `State.FinishedAt`).
- [x] [Review][Defer] `cancel-execution` may exhaust the owner suite's 240 s bound before operator cancellation [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1803] — deferred: unverified, medium if confirmed (edge-case-hunter)
  - The attached launch now precedes readiness. The 2.10 operator took about 94 s from admission to signal, so the estimate fits, but only narrowly.
  - To settle it, measure elapsed owner-suite time at signal in the first hosted run.

**Rejected:**
- `false` — The new project is missing from `test.ps1`, `DocumentationFitnessTests` and `tests/README.md` (blind-hunter): later Parties commits on `main` list it in all three.
- `false` — Raw string comparison of the marker's `expiresAtUtc` (blind-hunter): both the ledger (`CompositionDocumentStore.SerializerOptions`, with no `DateTimeOffset` converter) and the marker use System.Text.Json's default `DateTimeOffset` writer for the same UTC instant. The 2.10 evidence shows `…9096605+00:00`.
- `false` — The hosted `invalid-handoff` never presents foreign or mismatched snapshots (acceptance-auditor): the real runner cannot issue them without forging, which is forbidden. The SDK reader and constructor refusals cover them, as the docs state.
- `false` — `cancel-execution` cannot run at current pins (acceptance-auditor): this is the documented dependency on compatible releases that every new case shares, and Story 2.10 reruns after those releases.
- `false` — `AttributeError` escapes the finalizer on a non-dict hold end or diagnostic (edge-case-hunter): `TestResultWriter` validates v1 before atomic publication, so that input is unreachable and would fail loudly.
- `low` — A `SocketException` maps to generic `HXR020`, and the probe checks IPv4 only (blind-hunter, edge-case-hunter): this affects only an occupied explicit port held by a non-runner process, and the fix needs a new mapping.
- `low` — An explicit port inside the ephemeral range can be taken before bind (edge-case-hunter): this is unlikely, and the fix adds a guard.
- `low` — The attached routing read re-reads the owner's live files instead of its held capture (blind-hunter, edge-case-hunter): this needs an owner child that mutates its own platform-components; the attached bytes are still fully validated; the fix adds comparison logic.
- `low` — The finalizer never signals a live admitted attached CLI after a refused owner drain (blind-hunter): this is the rare `HXR037` path, it fails loudly, and the fix adds a branch.
- `low` — Invalid-lineage probes bypass the attached journal and launch gate (acceptance-auditor): this matters only if the expected refusal regresses, in which case the unchanged-ledger check fails. Journaling the probes adds complexity.

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
