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

### Review Findings

Code review pass 2 (2026-11-12) of the delivered commits: Builds `b1681870..bfda07ee`, EventStore `b882c100..04e56e1c`, Parties `fc9ea3f3..e35b8c1e` (33 files, 2,579 diff lines). Platform evidence files were left out. All four layers ran with no failures: blind-hunter, edge-case-hunter, verification-gap and acceptance-auditor. They raised 53 findings. Triage produced 2 decision-needed entries (both resolved to patch), 19 patch, 2 defer, and 12 rejected findings. Line anchors refer to the delivered commits.

- [x] [Review][Patch] `--eventstore-port` is claimed but never bound in Platform v2 compositions, so `collision` proves only a claim conflict [src/Hexalith.Platform.Composition/PlatformAppModel.cs:309] (high; verification-gap+edge-case-hunter; decision resolved 2026-11-12: option (a), bind it in v2)
  - In v2, `RunTopology.ComposePlatform` hands the plan to `PlatformAppModel.ComposeRun`. That model registers eventstore with `WithHttpEndpoint(name: "http")` and no port (`src/Hexalith.Platform.Composition/PlatformAppModel.cs:309`). Nothing in v2 reads `plan.Ports.EventStoreHttp`; only the legacy `RunTopology.ConfigureEventStore` path binds it (`RunTopology.cs:110`).
  - Parties uses a v2 manifest. An owner started with `--eventstore-port N` therefore claims and probes N, while EventStore listens on a port Aspire assigns.
  - The challenger's `HXR031` comes from the runner claim store on an unbound plan port, not from "the live owner's EventStore endpoint". The Builds README and the Parties document both claim the live endpoint.
  - `ExplicitPortCommandForwardsAndNamesManagedOwnerAsync` only covers a v1 manifest with a fake AppHost.
  - Frozen matrix row: "Challenger requests a live owner's endpoint port"; Never: "replace collisions with metadata locks".
  - Options:
    - (a) Bind eventstore HTTP to `plan.Ports.EventStoreHttp` in `PlatformAppModel` when a run plan exists. This is a Platform model change, so it needs a Platform release and the Parties model pin.
    - (b) Refuse `--eventstore-port` for v2 with `HXC002` and choose another genuine resource that v2 binds from the plan.
    - (c) Accept the claim-level conflict and correct the README and docs.
- [x] [Review][Patch] The ten-case `matrix` cannot finish inside the unchanged 45-minute qualification job [references/Hexalith.Parties/.github/workflows/ci-lifecycle-qualification.yml:55] (high; blind-hunter+acceptance-auditor+edge-case-hunter; decision resolved 2026-11-12: option (a), raise only this job's `timeout-minutes` to 90. The user confirmed that the frozen "Never raise budgets" covers lifecycle and policy deadlines, not CI wall-clock time)
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
- [x] [Review][Patch] `collision` and `hold-expiry` start their peer only after the owner suite's 240 s bound has begun [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1394] (high; edge-case-hunter)
  - `LifecycleQualificationTests` cancels itself 240 s after its test starts.
  - `self.start(owner)` returns once the owner is executing. Only then does `self.start('collision-peer')` or `self.start('expiry-peer')` (`:1433`) run a complete peer startup, which took 165–230 s in hosted CI.
  - `hold-expiry` then adds the attached launch, the 120 s hold and two lineage probes before `pass`, so the owner suite's bound expires first and the case fails deterministically. `collision` leaves roughly 0–55 s for the challenger.
  - Fix: `begin()` both owners and then `ready()` both, as `retry_with_peer` already does (`:1529`). After the fix, `hold-expiry` stays tight at about 160–200 s of about 210 s; record that margin.
- [x] [Review][Patch] `invalid_lineage` accepts any `HXR033`/`HXR036` refusal [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1490] (medium; blind-hunter+edge-case-hunter)
  - Builds emits a specific reason for each refusal:
    - `HXR036 attachment-ended` / `attachment-expired` from `Retry`;
    - `HXR036 unknown-attachment-request`;
    - `HXR033` for unrelated preconditions such as `owner-not-live` and `attacher-context-unverifiable` (`CompositionAttachAdmission.cs:313-331`, `:241`, `:284`).
  - An unrelated refusal therefore passes the hold-expiry and invalid-handoff lineage proofs.
  - Fix: require `HXR036` with the exact expected reason for each presented lineage: expired, ended or unknown.
- [x] [Review][Patch] The attached CLI's own exit code and public diagnostics are never verified [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1456] (medium; acceptance-auditor)
  - `attached_case` waits on the attached child but never inspects its return code or diagnostics.
  - `finalize_attached` (`:1376`) checks only the public `runId`.
  - Owners pass through `verify_original_owner_command`. An attached command that reports a cleanup or evidence diagnostic alongside a valid result passes unnoticed.
  - Fix: assert the attached exit code and diagnostic categories for each expected cause, using the Builds attached result mapping.
- [x] [Review][Patch] Builds README: the new section sits under License and earlier passages now contradict it [references/Hexalith.Builds/README.md:1399] (medium; blind-hunter)
  - `:641-643` still says "the Parties caller is not adopted, owners requiring routing-catalog observations fail an attached run as `startup-failure`".
  - `:752-756` says claim conflicts always retry up to 64 attempts; an explicit port never retries.
  - The `--attach-to` incompatibility list at `:635` omits `--eventstore-port`.
  - Fix: edit these in place and move the section. The explicit-port wording depends on the first decision above.
- [x] [Review][Patch] `AttachedExpiryCancelsFixtureRequests` relies on a 1-second hold measured from the wall clock [references/Hexalith.EventStore/tests/Hexalith.EventStore.Testing.Tests/TestEnvironmentDescriptorReaderTests.cs:59] (medium; blind-hunter+verification-gap+edge-case-hunter)
  - The hold starts before the file write, schema evaluation and client creation.
  - On a loaded agent the constructor throws `HXT108`, or `CreateHttpClient` throws `OperationCanceledException`, so the test is flaky.
  - Fix: use a multi-second hold.
- [x] [Review][Patch] The explicit-port parse test cannot tell a refusal from an accepted port [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/StartupReadinessTests.cs:37] (medium; verification-gap)
  - The test uses the manifest `unused`, so an accepted port also ends in `HXM005` with exit 1.
  - Fix: assert `HXC001` for each case.
- [x] [Review][Patch] The explicit-port guards with `--attach-to`, `Down` and existing runs are untested [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs:450] (medium; verification-gap+blind-hunter+acceptance-auditor)
  - Deleting both the CLI guard and the `ModuleCommandExecutionService.cs:119` guard leaves every test passing.
  - Fix: add an attached `--eventstore-port` refusal to `RefusedRequestAdmitsNothingAndStartsNoChildAsync`, plus service-level cases.
  - Not required here: the CLI refuses this case with `HXC001`, while other attach-incompatible options use `HXC002`.
- [x] [Review][Patch] The attached candidate-policy refusal has no test [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/NativeTestExecutor.cs:400] (medium; verification-gap+blind-hunter)
  - This is now the only guard keeping attached suites out of candidate-gated owners. Removing `CandidatePolicy is not null` leaves every test passing.
- [x] [Review][Patch] Only the foreign-`ownerRunId` row of the routing-snapshot identity checks is tested [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/SelectedRoutingCatalogSnapshot.cs:83] (medium; verification-gap+blind-hunter)
  - Add rows for a sibling `runId`, the owner role on an attached identity, and a hold with a foreign owner or environment. Cover the owner-descriptor `role`/`ownerRunId` requirement too.
- [x] [Review][Patch] Controller launch arguments for attached and collision launches are unverified [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:441] (medium; verification-gap+blind-hunter)
  - Mutation check: dropping `--eventstore-port`, `--attach-to`/`--hold-seconds` or the journaled `eventStorePort` leaves all 167 tests passing.
  - Fix: add `launch_owner` argument tests with `gated_exec` patched.
- [x] [Review][Patch] Failure-then-cancellation is only tested at helper level [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1806] (medium; verification-gap)
  - Mutating the `start-cancellation --phase failure` capture, the `wait-cancellation` tolerance or the failure classification in `finalize_cancellation` leaves all tests passing.
  - Fix: add tests at `main()` level.
- [x] [Review][Patch] The attached control/readiness handshake with the C# fixture is unverified [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1271] (medium; verification-gap)
  - `attached_control` is always mocked. The success path of `begin_attached` (`:1222`) and its marker checks are untested.
  - Mutation check: trimming the payload or deleting the marker binding leaves all tests passing.
  - Fix: assert the exact control keys, and that a mismatched marker is rejected. The C# compile lane waits for the Testing package release.
- [x] [Review][Patch] The `invalid_lineage`/`invalid_handoff` refusal checks are always mocked [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1479] (medium; verification-gap+acceptance-auditor)
  - Replacing the whole `require` with `pass` leaves all tests passing.
  - Fix: patch `tool` to return success, an admission, the wrong rule or a changed ledger, and expect each to raise.
- [x] [Review][Patch] The C# owner-label list must match Python `OWNER_LABELS`, but nothing checks it [references/Hexalith.Parties/tests/Hexalith.Parties.Platform.IntegrationTests/NativeDiagnosticBinding.cs:119] (medium; verification-gap+blind-hunter)
  - Drift breaks every later owner's native-diagnostic check in the hosted matrix.
  - Fix: add a test that ties the two sets together.
- [x] [Review][Patch] Both lineage probes overwrite one retained evidence file [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1486] (low; acceptance-auditor+edge-case-hunter)
  - Both use the tag `invalid-attached-lineage`, and `tool()` writes `<label>-<tag>.json`.
  - Fix: use a distinct tag for each lineage.
- [x] [Review][Patch] The EventStore attached-hold theory does not assert which rule refused each case [references/Hexalith.EventStore/tests/Hexalith.EventStore.Testing.Tests/TestEnvironmentDescriptorReaderTests.cs:21] (low; blind-hunter)
  - The `foreign`, `snapshot` and `missing` rows are refused earlier by reader binding validation, so the new constructor checks are never reached.
  - Fix: assert the expected rule for each row.
- [x] [Review][Patch] The EventStore docs paragraph sits after the Next/Related footer and has no heading [references/Hexalith.EventStore/docs/reference/nuget-packages.md:672] (low; blind-hunter)
  - Move it into the `Hexalith.EventStore.Testing` section and update the "qualifies no attached execution" sentence there (`:541-543`).
- [x] [Review][Patch] Two workflow step names are stale [references/Hexalith.Parties/.github/workflows/ci-lifecycle-qualification.yml:186] (low; blind-hunter)
  - "...passing failing partial-startup and exact retry scenarios" now runs ten cases; "Build the explicit declared lifecycle suites" builds one project.
  - Rename both, together with the name lookups in `test_review_fixes.py`.
- [x] [Review][Patch] The new 8 MiB retained-artifact cap also applies to the journal and `observations.json` [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:315] (low; blind-hunter)
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

#### Code review pass 3 (2026-11-12)

Pass 3 reviewed the resumed correction run: Platform `3f7659e2` (`src/` and `tests/` only), Builds `3c2f5944..565b9e63`, EventStore `76f743ba..8934b4f4` and Parties `e2a135cd..9a08b88e`. That is 14 files and 1,257 diff lines; evidence files were left out. All four layers completed: blind-hunter, edge-case-hunter, verification-gap and acceptance-auditor. They raised 42 findings. Triage produced 0 decision-needed, 9 patch, 0 defer and 23 rejected. The acceptance auditor confirmed that all 21 pass-2 patch items are implemented. Line anchors refer to the delivered commits.

- [x] [Review][Patch] `collision` still passes on a claim-only conflict; nothing proves the owner's EventStore listens on the requested port [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1482] (medium; acceptance-auditor+verification-gap+edge-case-hunter)
  - `collision()` reads `plan['ports']['eventStoreHttp']` and accepts any `HXR031` that names the owner.
  - Builds `AllocateOwned` claims the port before probing it (`CompositionPortAllocator.cs:185-188`). The challenger therefore gets `HXR031` whether or not the port is actually bound.
  - Under the current pins, the Platform composition release lacks `3f7659e2`. The owner's EventStore then listens on an Aspire-assigned port, yet the hosted case still passes. The Parties doc itself says "a claim conflict alone supplies no live-service proof".
  - Fix:
    - Before launching the challenger, read the owner's readiness document (`<workspace>/readiness.json`, `eventStoreEndpoint`) and require its loopback port to equal the plan port.
    - Add a `wrong-bound-port` fault to `test_collision_requires_exact_blocker_and_unchanged_peer`.
- [x] [Review][Patch] `AttachedExpiryCancelsFixtureRequests` can still flake or hang [references/Hexalith.EventStore/tests/Hexalith.EventStore.Testing.Tests/TestEnvironmentDescriptorReaderTests.cs:76] (medium; acceptance-auditor+edge-case-hunter+blind-hunter)
  - **Flake:** the 5 s hold starts at `accepted`. That is before the file write, the reader, client creation, the TCP accept and the 2 s header read. On a loaded agent, either the constructor throws `HXT108` or `IsCompleted.ShouldBeFalse` fails.
  - **Hang:** if the hold expires before the client connects, the request is cancelled. `AcceptTcpClientAsync` (`:83`) waits only on the test-context token, so the test hangs.
  - **Missing assertion:** nothing checks that cancellation happened at or after `expiresAt`, so an early cancellation also passes.
  - Fix: use a longer hold (about 15 s), bound the accept with `WaitAsync`, and assert that the observed cancellation is not earlier than the hold expiry.
- [x] [Review][Patch] The non-`test-failure` branch of `verify_attached_command` is never exercised [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1428] (medium; verification-gap+blind-hunter)
  - Replacing the branch with the fixed `(4, 'ProductOrTest', 'HXT007', 'ProductOrTest')` tuple leaves the whole suite passing.
  - The `rebound-failure` fault only proves that the branch is not taken.
  - Its `ruleId or 'HXT007'` default mirrors Relay and is correct.
  - Fix:
    - Add finalize cases for failed outcomes with a cause other than test-failure, with exits 3 and 6.
    - Add a mismatched-category counterpart that must refuse.
- [x] [Review][Patch] No test pins the attached public result's `runId` binding [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1454] (medium; verification-gap)
  - Replacing the `runId` condition with `True` leaves the suite passing. Another run's public output could then be verified and retained as this run's evidence.
  - Fix: add a `foreign-run` fault to `test_attached_exit_public_mapping_and_cleanup_diagnostics_are_verified_for_each_cause`.
- [x] [Review][Patch] No positive test checks the refusal reason that `invalid_handoff` expects [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1594] (medium; verification-gap)
  - Changing the call to `expected_reason='attachment-expired'` leaves the suite passing.
  - Builds `Retry` returns `attachment-ended` for an ended hold, so with that change the hosted case would fail.
  - Fix: add a positive `invalid_handoff` test.
    - The double returns `HXR036` with `attachment-ended`, then `unknown-attachment-request`.
    - Assert success and both evidence tags.
- [x] [Review][Patch] The failed-outcome mapping in `verify_attached_command` differs from Builds `AttachedSuiteCommand.Relay` [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1426] (low; blind-hunter+acceptance-auditor+edge-case-hunter)
  - For every failed outcome, Relay uses the ledger's `execution.exitCode` (it maps null, 0 and 130 to `ProductOrTest`) and `execution.ruleId ?? HXT007`.
  - The controller hard-codes `(4, HXT007)` for `test-failure`, which leads to refusals:
    - The host-vanished stamp (`AttachedSuiteTermination.cs:249`: exit 3, `HXT009`) is refused as "public outcome differs".
    - A skipped-test `HXT004` failure is refused the same way.
    - The fallback branch refuses exit codes that Builds maps to 4.
  - Only runs that have already failed are affected. Even so, the finalizer reports a correct CLI result as a contract mismatch.
  - Fix: for every failed outcome, derive the expected tuple from the ledger execution exactly as Relay does.
- [x] [Review][Patch] The identity comparison inside `verify_attached_command` can never fail [references/Hexalith.Parties/scripts/qualify-disposable-ci.py:1445] (low; verification-gap+blind-hunter)
  - `recover_launch` already refuses a changed attached identity. It then copies the record's identity onto the entry, so the second comparison checks the entry against itself.
  - Only `recover_launch` (`:917`) catches a rebound record. The owner-side rebind claim was not demonstrated, because `begin()` refuses a second launch per label.
  - Fix: delete the redundant comparison.
- [x] [Review][Patch] The Platform isolation assertion checks only `Port`, not `TargetPort` [tests/Hexalith.Platform.Composition.Tests/PlatformRunIsolationTests.cs:41] (low; edge-case-hunter)
  - If another resource reused the run-plan EventStore port as its target port, the assertion would still pass.
  - Fix: also require `value.TargetPort != plan.Ports.EventStoreHttp`.
- [x] [Review][Patch] `test_review_pass2.py` imports `json` without using it, and its imports are out of order [references/Hexalith.Parties/tests/ci-lifecycle/test_review_pass2.py:3] (low; blind-hunter)

**Rejected (pass 3):**
- `false` — The service accepts `--eventstore-port` with an attached suite (blind-hunter, edge-case-hunter): the CLI passes `attachTo` as the service `runId` (`ModuleCommandApplication.cs:478`), so the guard `runId is not null` refuses it.
- `false` — The routing-snapshot theory has no rows for descriptor `environmentId` or `catalog.instanceId` (blind-hunter, edge-case-hunter): `SelectedRoutingCatalogSnapshotTests` already covers both, as `wrong-environment` and `wrong-instance`.
- `false` — The binding applies to every v2 run, not only explicit ports; and the Platform test lacks an explicit-port case (blind-hunter, acceptance-auditor): this is resolved decision (a), "when a run plan exists". The runner already claims the plan port for every run. The model only sees `plan.Ports`, so an explicit port and an allocated port reach it as the same input. The unowned-case assertion is vacuous on purpose, because there is no plan port. Proof that the port is actually bound belongs to the collision patch.
- `false` — The proxied endpoint leaves EventStore on an unclaimed DCP target port (edge-case-hunter): EventStore already listened on an Aspire-assigned port before this change. The claimed, published endpoint the challenger requests is now the plan port.
- `false` — A non-object public result or non-dict diagnostics escape the finalizer as `AttributeError` (edge-case-hunter): the file is the typed `ToolCommandResult` output. This is the same unreachable malformed-producer input rejected in passes 1 and 2.
- `low` — `write()` is uncapped for every caller (blind-hunter, verification-gap, edge-case-hunter): `write()` was already uncapped at the pre-part-3 baseline `fc9ea3f3`. Patch 21 restored that and kept the cap on retained artifact copies. A tool output over 8 MiB is unlikely, and a second writer adds surface.
- `low` — The live-child path never cross-checks the durable exit record (blind-hunter): this only matters if the supervisor fails to write its own record, and the fix adds a guard.
- `low` — The same invalid port gets `HXC001` from the CLI and `HXC002` from the service (blind-hunter): this layering predates the change and was already noted in pass 2.
- `low` — The README does not describe the `SocketException` when an unmanaged process holds the port (blind-hunter): pass 2 rejected this same behavior as low.
- `low` — The candidate-policy test covers one of the executor's four refusal conditions (blind-hunter): pass 2 asked only for `CandidatePolicy`. Snapshot and admission tests cover hold identity mismatches, and the CLI refuses observation-requiring suites earlier with `HXC002`.
- `low` — No step-level timeout reserves time for the finalizer (blind-hunter): the structure predates this change, and `always()` steps still run after the job is cancelled.
- `low` — Nothing measures the hold-expiry margin (blind-hunter): `observe()` timestamps every observation, and a missed window still fails loudly.
- `low` — The Parties doc names the pre-binding Platform input and keeps a review log (blind-hunter): it states the binding behavior that is required, following the pattern of the existing preparation paragraph.
- `low` — The EventStore constructor's own foreign, missing and snapshot checks are never reached (acceptance-auditor): the reader refuses those rows first, as pass 2's patch asserted. Reaching the constructor would need a test that bypasses the reader.
- `low` — The main-level failure-then-cancellation test replaces `finalize` (acceptance-auditor): it covers the three mutations pass 2 named. Testing the full chain needs a new harness.
- `low` — A signal arriving between the release write and handler installation kills the supervisor (edge-case-hunter): this microsecond window existed before the change, and the finalizer then fails loudly on the missing admission or exit record.
- `low` — A signal-killed CLI is recorded as `-N` while the supervisor exits `256-N` (edge-case-hunter): both values fail every expected mapping, so no outcome changes.
- `low` — An early signal can reach only the muxer before the tool process exists (edge-case-hunter): this muxer boundary predates the change, and owner withdrawal still ends the attached run. The fix would add waiting and retries.

## Implementation Notes

Approved by the user on 2026-11-11 with “approve and stop”. The frozen intent is locked. The initial implementation and both review passes are complete. All 21 approved review-pass-2 corrections and the three independently identified correction groups were completed in the resumed 2026-11-12 bmad-build run.

Resume from the current checked-out revisions, preserving incoming work and the original baseline identifiers. Execute sequentially with no push or other remote operations. The resolved review decisions authorize binding the Platform v2 EventStore HTTP endpoint to the run-plan port and increasing only the qualification job wall-clock timeout to 90 minutes. Compatible Platform release adoption remains a documented release dependency; do not publish or update pins. Save this run's local verification under `evidence/2-11/part-3/checks/review-pass-2-*` and record exact current input revisions and modified bytes.

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

### Resumed second-review implementation audit (2026-11-12)

- Independently read the complete correction diff across all four owning repositories, including new tests; all 21 approved patch items are complete. The original baseline identifiers remain unchanged, and intervening incoming work is preserved. Private Git indexes captured the original-baseline and resumed-run diffs without altering user staging.
- Verified all modified input bytes against the saved manifest and all 268 selected xUnit results as passing. The 180 controller and 7 declaration controls also pass, totaling 455 local cases. All seven frozen matrix rows have named passing coverage in [the matrix audit](evidence/2-11/part-3/checks/review-pass-2-matrix-audit.json).
- Five Debug builds, renderer and actionlint pass under the documented local source/framework arrangements. Standard Platform framework drift and the broader generated-reader isolation blocker remain visible; hosted qualification and its timing measurements remain separate.

### Resumed correction review (2026-11-12)

All three independent layers completed before triage: blind hunter (B), edge-case hunter (E) and verification-gap reviewer (V). Their 13 findings are recorded individually below, before grouping. V1/V2 were filed as Other findings and independently reproduced. Review compares the resumed-run delta against the four exact input HEADs; original baseline history, claims and unrelated incoming work are retained separately.

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B1 | medium | patch G1 | Independently reproduced: write() publishes >8 MiB JSON, but read() and optional_read() reject it; launch_owner and independent Qualification construction use those readers. |
| B2 | medium | patch G2 | Independently reproduced SIGINT during Popen entry: child_birth is unset and forward() makes no delivery; attached process_signal targets only this supervisor. |
| B3 | false | reject | The cached code is itself a genuine Popen OS observation durably saved in the job-bound journal, or a previously verified monitor observation. finalize_attached still recovers the launch and checks its identity; no production writer changes an already-published exit. Editing or removing exit fields does not invalidate the separately persisted observation. |
| B4 | false | reject | Actual public files are generated from Builds typed ToolCommandResult, ToolOutcome and ToolDiagnostic objects. No demonstrated publisher/caller emits the proposed array outcome or string diagnostic; this needs manually substituted malformed output, as with the prior rejected malformed-producer finding. |
| B5 | false | reject | AttachedSuiteCommand.Relay derives status, typed phase/category/rule and top-level outcome from the same final attachment. Its actual publisher cannot emit completed status with a failed outcome, or a non-null rule on Passed(); the proposed inconsistent document has no reachable producing path. |
| B6 | false | reject | Relay assigns RunId and Attachment from the same validated final CompositionAttachment. Existing admission/result/ledger checks bind that hold; the proposed foreign attachment with the original top-level runId requires substituting publisher output. |
| B7 | false | reject | The only writer uses now() after child.wait(); year-2999 content in a present-time record needs forged metadata or an unshown clock change. No demonstrated runtime path publishes the proposed timestamp contradiction. |
| B8 | false | reject | CompositionAttachAdmission.Refuse returns a single HXR036 topology diagnostic and TopologyOrLifecycle exit; AttachedSuiteCommand returns that refusal directly before execution. No real expected-reason refusal adds an evidence failure or another exit mapping. |
| B9 | low | patch G3 | The test only delays on RequestCancellation. Existing disposal tests cover client linkage, but a blocked fixture HTTP request directly verifies the attached-expiry behavior with a small test-only correction. |
| E1 | medium | patch G2 | Same independently reproduced launch-boundary defect as B2; retain this distinct finding and correct the shared pending-signal loss. |
| E2 | medium | patch G1 | Same independently reproduced metadata contract mismatch as B1; the unchanged bounded read_bytes default is still used by metadata readers. |
| V1 | medium | patch G1 | Filed as Other findings, independently verified: the regression uses json.loads(path.read_text()) and misses read()/optional_read() rejecting the writer-created journal. |
| V2 | medium | patch G2 | Filed as Other findings, independently verified: signalling at the Popen boundary is lost, while the existing real-child test waits until the ready marker before interrupting. |

The seven surviving reports share three roots: G1 aligns operational metadata reads/writes, G2 preserves cancellation through child launch, and G3 strengthens the SDK expiry test with an outstanding request. These are direct internal corrections or a focused test change; no public contract, frozen intent, pin or new policy decision is needed. No new deferral or loopback applies.

### Final resumed-run verification and delivery (2026-11-12)

- Re-engaged the original implementer for G1–G3, read the complete patch diff, then independently reran all five Debug builds and the complete local verification set. All 456 cases pass: Builds 176, EventStore 62, Platform 2, Parties C# 28, controller 181 and declaration 7. All five builds have zero warnings/errors; renderer and actionlint pass. Tested source bytes stayed unchanged during this run.
- G1 now reads operational journals through the matching uncapped metadata contract while preserving artifact and held-file/link checks; G2 queues SIGINT/SIGTERM through child construction/birth capture; G3 verifies hold expiry cancels an actual outstanding HTTP request with a longer independent timeout. [Final verification](evidence/2-11/part-3/checks/review-pass-2-final-verification.json) and [matrix/correction audit](evidence/2-11/part-3/checks/review-pass-2-final-matrix-audit.json) retain exact named passing evidence.
- All 13 independent findings were triaged individually: seven reports corrected in three groups, six rejected on traced publisher/recovery invariants, with no new deferral. Previously deferred hosted timing/order measurements remain open. Default Platform framework drift and broader catalog isolation remain documented limitations; successful focused validation uses the saved local framework/helper overrides. No hosted qualification, release publication or pin change is claimed.
- Local delivery follows the workflow using pinned commitlint 21.2.4 for every exact full message. Builds, EventStore and Parties local commits are recorded in [the delivery manifest](evidence/2-11/part-3/checks/review-pass-2-local-commits.json). The Platform commit preserves its original submodule pointers; there is no push or remote operation.

### Review pass 3 corrections (2026-11-12)

The user chose to apply all 9 pass-3 patches.

- **Parties** `f5321450` (`fix(ci): prove the live collision endpoint`) and **EventStore** `bb9e2b51f` (`test(testing): bound the attached expiry request check`):
  - Each was made in a scratch worktree on `fix/2-11-review-pass3`, branched from the delivered heads `9a08b88e` and `8934b4f48`.
  - Pushed to module `main` on 2026-11-13 at the user's request:
    - Parties fast-forwarded `e2a135cd..f5321450`.
    - EventStore merged as `76fe863f1`.
    - Builds `565b9e63` merged as `90263457`.
  - The Platform Parties and EventStore gitlinks now pin exactly `f5321450` and `bb9e2b51f`. The Builds gitlink stays at `565b9e63`.
- **Platform**: the `PlatformRunIsolationTests.cs` `TargetPort` assertion is committed with this record.
- **What changed:**
  - `collision()` now requires the owner readiness `eventStoreEndpoint` to be a loopback `http` URL on the claimed plan port, and to accept a TCP connection, before the challenger runs.
  - `verify_attached_command` mirrors Builds `Relay` for every failed outcome. The hosted `attached-failure` case still requires the intentional `(4, ProductOrTest, HXT007)` test failure.
  - The dead identity re-check was removed.
  - The EventStore expiry test uses a 15 s hold and a bounded accept, and asserts that cancellation is not earlier than the expiry (250 ms tolerance).
- **Local results:**
  - Parties controller: 183/183 pass. Six mutations of the new checks each fail the suite, and the controller was restored byte-identical after each one.
  - EventStore `TestEnvironmentDescriptorReaderTests`: 62/62 pass.
  - Platform `EventStoreEndpointUsesRunPlanPortAndKeepsUnownedAllocationAsync`: 2/2 pass, run with the saved net11 props and `pwsh` on `PATH`.
  - The outputs are in `evidence/2-11/part-3/checks/review-pass-3-*`.
- The Builds sources and tests, the workflow, the declarations and the schemas are unchanged. No hosted qualification, release publication or pin change is claimed.
