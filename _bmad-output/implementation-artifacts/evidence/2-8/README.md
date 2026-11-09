# Story 2.8 implementation and qualification evidence

## Review loop 2, 2026-11-09 (targeted fix, no revert)

This continuation implements the spec's *Review loop 2 tasks* under the same remote-operation authorization. Every record is in [review-loop-2-20261109](review-loop-2-20261109/). All results are `baseline-only`.

- **Sources.** Builds `b411bafb` → `95b65d45`, `54970a7a` and `bde7fd4f` on PR [#177](https://github.com/Hexalith/Hexalith.Builds/pull/177). The last two commits only reword docstrings and move a suppression; every PR check passes at `bde7fd4f`. Parties `2849c5b4` → `4c0167b8` on `fix/lifecycle-cancel-signal-566b9bfc`. Platform holds the PB-9 test and its read-only preparation accessor, plus the PB-15 and BS-4 contract text.
- **External delivery, not part of this loop.** An external `/pushall` under the owner account did two things this loop did not:
  - It merged PR #177 into Builds `main` as `494c96a9` at 08:13Z.
  - It committed this Platform working tree to Platform `main` as `6381fd6f`, which failed its tuple-CI preflight in the legacy lane.
- **Local checks** ([results](review-loop-2-20261109/local-results-loop-2.json)):
  - 232 Python tests with 626 subtests, including 11 new helper process and unit controls;
  - the validator: legacy 411 and v2 302 assertions;
  - 351/351 cases in the seven focused xUnit classes;
  - Parties ci-lifecycle 95/95 and integration scripts 46/46, with both the pinned and the patched helper.

  The new controls fail against `b411bafb`, apart from the escalation coverage (PB-3), which already passed there. The [loop-2 row index](review-loop-2-20261109/row-index-loop-2.json) and [path audit](review-loop-2-20261109/path-audit-loop-2.json) cover the touched rows and the fix delta.
- **BS-4, Parties lifecycle cancel-execution, run [42587199919](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42587199919): passed** ([audit](review-loop-2-20261109/hosted-l-cancel-execution-42587199919.json)).
  - The run had an active 240 s same-job hold, and the single request came at a ready age of 2.81 s.
  - The controller forwarded SIGINT only to the two tool processes. A kernel signal trace shows no controller or helper signal reaching a composed process.
  - Timestamps:
    - admission-closing write (first outcome `cancelled`/HXC130): 08:59:34.220Z;
    - withdrawal commit: 08:59:34.463Z;
    - first composed-environment stop (AppHost exit): 08:59:34.662Z.
  - The exact down left empty inventories.
  - The run exercises the controller with the pinned 4.44.13 tool and the `3c80` helper. Only the digest data, byte-identical to `bde7fd4f`'s, came from a proof path.
  - Open for Step 04: the tool SIGKILLed its own native test host at 08:59:34.138–.203Z, before both marks.
- **BS-4, v2 execution cancellation: not achieved.** No cancellation was sent in any attempt:
  - Runs 42582136922 and 42582794137 failed on HTTP 504 Dapr downloads.
  - Run 42587081454 failed with HXP020, because the proof's long native test changed the suite inputs.
- **Amended BS-4 proofs on Builds v4.45.1 (`25de7a74`): neither reached a cancellation.**
  - Lifecycle rerun [42593229291](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42593229291) ([audit](review-loop-2-20261109/hosted-l-cancel-execution-42593229291.json)) was refused with HXW009, then HXW007. The staged Builds commit must equal the Platform `0f4e27f1` pin `3c80`, and no Platform release pins v4.45.1.
  - v2 late-startup run [42593223538](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42593223538) ([audit](review-loop-2-20261109/hosted-v2-late-startup-42593223538.json)) had unchanged test inputs. It was refused with HXP020 0.2 s after the AppHost started, before the 130 s ready point. The likely cause is tool 4.45.1 and Dapr 1.19.1 against the Platform `0f4e27f1`/Builds `3c80` tuple.
  - What the v2 run's kernel trace did confirm on a hosted runner:
    - the muxer leads the recorded session, the tool is its direct child, and the run-tagged AppHost is the tool's child;
    - the only signals were the tool's own startup-teardown SIGKILLs;
    - both downs verified empty inventories.
- **BS-4 rebound v2 late-startup proof (second owner amendment): passed.**
  - The first attempt, 42596887673 ([audit](review-loop-2-20261109/hosted-v2-late-startup-42596887673.json)), was not achieved because of an observer instrumentation defect. The run itself was correct.
  - The authorized rerun, [42598774773](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42598774773) ([audit](review-loop-2-20261109/hosted-v2-late-startup-42598774773.json)), passed. It ran `domain-ci-v2.yml@bde7fd4f` with tool 4.44.13, and its gitlinks, suites and tests were unchanged from `4c0167b8`.
  - Its records reached the finalizer's job log, and their SHA-256 values match. The observer never wrote inside a checkout.
  - The helper sent SIGINT only to the gated muxer and the tool, while the AppHost, DCP and four owned containers were live.
  - No signal from outside the runtime reached any composed process. The first outcome was `cancelled`/HXC130.
  - The finalizer's exact down verified empty inventories.
  - Lifecycle on patched Builds is deferred to the post-review re-pin chain.
- **Two lifecycle setup attempts also failed before any owner existed:**
  - Run 42583201444 failed with HXW009: Platform pins Builds `3c80`.
  - Run 42584756093 failed with HXT007 after a submodule overlay.

  All attempts are retained.

Open, not claimed:

- the deferred lifecycle `cancel-execution` gate on patched Builds, after Platform pins such a release through L/M;
- the test-host ruling;
- PB-9 execution, which is a gate for Platform L/M delivery;
- adopting a Builds release that carries the digest file;
- Task 134 and normal delivery, which wait for Step 04.

## Review loop 1, 2026-11-09 (targeted fix, no revert)

This continuation runs the spec's *Review loop 1 tasks* under the same remote-operation authorization (hosted proof, no merges). Every record is in [review-loop-1-20261109](review-loop-1-20261109/). All results are `baseline-only`: no candidate acceptance, no Platform tool acceptance and no Story 2.10 full-matrix pass.

- **Sources.** Builds draft PR [#177](https://github.com/Hexalith/Hexalith.Builds/pull/177) on `fix/ci-v2-cancellation-and-cleanup-27145e52`, unmerged:
  - `a51f9876` holds the fixes;
  - `4caa1e02` and `b411bafb` hold the behavior-preserving Codacy refactor and docstrings.

  Every PR check passes at `b411bafb`, Codacy included. Parties `2849c5b4` is on `fix/lifecycle-cancel-signal-566b9bfc`, unmerged and without a PR. The Platform PA-15 test and PA-22 contract edits sit uncommitted in the canonical working tree.
- **BS-1 hosted proof, two runs.** In each run the patched v2 caller's integration step received exactly one audited cancellation request. The runner signalled only the step process. The `exec`-run helper recorded SIGINT and delivered it to the tool session. The tool recorded `cancelled`/HXC130, and the helper returned 130 without running a down. The `always()` finalizer then ran the exact down with job status `cancelled`, verified empty inventories and kept the declared lifecycle diagnostics. Dapr archives were verified before extraction. Both requests landed during startup, before any owned resource existed; the owned-resource cancellation paths stay covered by the 2026-11-08 Parties runs below.
  - [42539638836](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42539638836) used Builds `a51f9876` and caller `c864d9d5`; the request came at step age 46.1 s ([audit](review-loop-1-20261109/hosted-v2-cancellation-42539638836.json)).
  - [42542502127](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42542502127) used the current PR head `b411bafb` and caller `6cf9d795`; the request came at step age 47.3 s ([audit](review-loop-1-20261109/hosted-v2-cancellation-42542502127.json)).

  Both proof branches were deleted after their single run.
- **BS-2.** The [upstream checksum files](review-loop-1-20261109/dapr-upstream-sha256/) match the pinned digests in Builds `Props/dapr-toolchain-sha256.json`.
- **Local checks at `a51f9876`** ([results](review-loop-1-20261109/local-results.json)):
  - 350/350 cases in the seven focused xUnit classes;
  - the whole Module.Tests assembly, 2,912 passed with 5 pre-existing environment skips;
  - 221 Python tests with 607 subtests;
  - the workflow validator, legacy 411 and v2 285 assertions;
  - red controls that fail against the previous helper and workflow;
  - Parties ci-lifecycle 85/85 and integration scripts 43/43 with both helpers.
- **Re-checks after the refactor** ([record](review-loop-1-20261109/codacy-refactor-b411bafb.json)): the same suites pass at `4caa1e02`/`b411bafb`. The two split tests passed 6/6 in each of 5 repeats, and every case the loop-1 row index maps still passes.
- **Index and audit.** The [loop-1 row index](review-loop-1-20261109/row-index-loop-1.json) marks every touched frozen row as covered by executed passing tests. The [loop-1 path audit](review-loop-1-20261109/path-audit-loop-1.json) lists every changed path of this fix delta. [evidence-sha256.json](review-loop-1-20261109/evidence-sha256.json) binds the records.

Open, not claimed:

- **PA-15 has not run.** It needs loopback listeners, which the local rules forbid, and `eng/tuple-ci-suites.json` does not select it. Its hash-pinned suite file must admit it during Platform L/M delivery.
- **L refuses until Parties adopts a Builds release that carries the digest file.** This is by design.
- **Not opened yet:** the normal Builds and Parties PR reviews and merges, and Platform delivery. They wait for Step 04.

## Focused hosted proof, 2026-11-08 (course-corrected execution)

This continuation follows the [compact active spec](../../spec-2-8-run-ci-integration-on-disposable-runners-with-guaranteed-cleanup.md) and its 2026-11-08 remote-operation authorization (hosted proof, no merges). Every record below is in [focused-hosted-proof-20261108](focused-hosted-proof-20261108/). All results are `baseline-only` lifecycle observations: they supply no candidate acceptance, no Platform tool acceptance and no full Story 2.10 matrix pass.

- **Fixed inputs.** [The campaign inventory](focused-hosted-proof-20261108/campaign-inventory.json) was recorded before the first dispatch. It names caller Parties `566b9bfc` (latest main with passing CI 42479678863), executor Builds `3c809618` / v4.44.13, tool package `036d32fb…`, catalog `c66fb47d…`, policy v1, budgets and the three required scenarios. One [recorded input change](focused-hosted-proof-20261108/campaign-input-change-1.json) moved the cancellation scenarios to `926c1c3f`, described below.
- **Local resource-free checks.** [Local results](focused-hosted-proof-20261108/local-focused-results.json) at Builds `27145e52`: 329/329 xUnit cases in the six lifecycle classes, 181 Python tests with 546 subtests, and the workflow contract validator (legacy 411, v2 254 assertions). No case was skipped or filtered out.
- **Matrix, run [42521615234](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42521615234).** Passed every step: passing, failing, retry-with-peer and partial startup ([audit](focused-hosted-proof-20261108/hosted-matrix-42521615234.json)). Cross-job probes and completed owners were refused with HXR033. Same-job holds were admitted, and retries kept the original expiry. Holds were withdrawn as `ci-job-completed` before expiry, then cleaned up exactly. Under real metadata locks, cleanup refused with HXR037 and then HXR023, leaving resources in place. The hold was withdrawn while 13 owned containers were still live, and the exact retry then succeeded with the peer preserved. After SIGKILL in Starting, the partial-startup owner was recovered and kept its `cancelled` first outcome across the retry. All five owners ended with empty inventories. Every owner kept its declared redacted content-addressed diagnostics ([retention check](focused-hosted-proof-20261108/diagnostic-retention.json)). The retry/peer stage is Story 2.10 scope and is reported, not claimed for 2.10.
- **Startup cancellation, first attempt, run [42524190110](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42524190110): FAILED, retained.** One audited request was sent at a ready age of 2.87 s ([receipt](focused-hosted-proof-20261108/cancel-request-42524190110.json)). The always-finalizer then recovered the exact owner from its receipt. It recorded `cancelled`/`run-cancelled` and cleaned 19 owned resources, leaving empty inventories. However, the controller never received the runner signal, so it refused its verdict. The cause: the runner signals only the step's `bash -e` process, which never forwards the signal. Parties fix `926c1c3f` (branch `fix/lifecycle-cancel-signal-566b9bfc`, not merged) runs that step with `exec`. It adds a process-level regression that failed before the fix.
- **Startup cancellation, run [42526256372](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42526256372): passed.** A single cancellation request was sent at a ready age of 2.47 s ([receipt](focused-hosted-proof-20261108/cancel-request-42526256372.json)). The controller saw SIGINT at 22:00:22.63Z, and the signal-time snapshot showed the owner `Starting`, with no outcome and no native execution. The first outcome was `cancelled`/HXC130 at 22:00:22.75Z. The independent finalizer and the exact retry both succeeded with empty inventories, and the first outcome was unchanged ([audit](focused-hosted-proof-20261108/hosted-cancel-startup-42526256372.json)).
- **Execution cancellation, run [42528046681](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42528046681): passed.** A same-job 240-second hold was admitted at 22:26:27.20Z, after the real persistence checkpoint, and expired at 22:30:27.20Z. One request was sent at a ready age of 3.70 s ([receipt](focused-hosted-proof-20261108/cancel-request-42528046681.json)). At signal time (22:26:33.62Z) the owner was `Ready`, its native test process was still running and the original hold was active. The first outcome was `cancelled`/HXC130 at 22:26:34.72Z. The hold was then withdrawn as `ci-job-cancelled` (HXI011) at 22:26:35.95Z, before its original expiry. Cleanup completed for all 22 owned resources with no leftovers, and the exact retry changed neither the outcome nor the withdrawal ([audit](focused-hosted-proof-20261108/hosted-cancel-execution-42528046681.json)).
- **Callers.** On Parties `566b9bfc` and `926c1c3f`, every baseline job ran all 15 isolated projects (6,393 passed, 14 pre-existing NotExecuted), including Parties.Tests, Sample.Tests and Ci.Tests. Each also ran blocking package integration with native 28/28. EventStore main `b0ea2fce` CI 42492835374 ran platform-package integration with tenants: native 9/9, retained diagnostics and empty inventories. Real prerequisite refusals in external main runs (Parties 42516570737, EventStore 42519687843, HXW011 from untagged Platform gitlinks) never provisioned and finalized as `not-allocated`.
- **Platform final checks.** Main `7fbe852f` passed tuple CI 42520045812 (current 412/412 and legacy 349/349 against real legacy `d4c4b469`) and all five SDK lanes in 42520045879.
- **Audit and index.** The [incremental path audit](focused-hosted-proof-20261108/path-audit.json) reuses the parent350 receipt for unchanged blobs. It maps 29 newer active paths to incoming Story 3.2/Builds 4.45.0 work and verifies 926 of 931 manifest hash bindings; the five exceptions are this living README. The [six-row index](focused-hosted-proof-20261108/row-index.json) maps every frozen matrix row to executed passing tests.

Open, not claimed: a normal Parties PR for `926c1c3f`, after Parties main's external `/pushall` gitlink breakage is repaired; Platform delivery of this evidence through the L/M protocol after Step 04 review; Story 2.10's full qualification and tool enrollment.

## Current continuation, 2026-11-06

The user chose **keep newer**. Story 2.8 remains in progress. The merged Platform source `ec282f4338c4b6d900e32b827821fc97a272fe76` retains incoming public 1.5.3/source `d1da347d` and every earlier reviewed repair, with real legacy parent `e02c71f6`. The [complete proposal and source audit](release-phase/remote-resumption-20261103/parent-platform-ec282-complete-proposal-real-pair-and-full-messages-audit-20261106.json) verifies the full change, retained histories, exact eight metadata paths and all 23 actual full commit messages. Focused 18, current 271 and original legacy 349 cases passed locally, with [current](release-phase/remote-resumption-20261103/parent-platform-seed-union-current-local-independent-audit-20261106.json) and [legacy](release-phase/remote-resumption-20261103/parent-platform-seed-union-legacy-local-independent-audit-20261106.json) independently authenticated against all tracked source inputs and genuine public binary bodies.

The ordinary feature branch is `story-2-8/seed-snapshot-union-ec282f43`. [Hosted tuple42161942017](https://github.com/Hexalith/Hexalith.Platform/actions/runs/42161942017) passed both current271 and original349 jobs. The [independent full artifact/source audit](release-phase/remote-resumption-20261103/parent-platform-ec282-current271-and-original349-feature-hosted-audit-20261106.json) verifies both exact sources, all tracked inputs and genuine public bytes. [All five hosted SDK rows](release-phase/remote-resumption-20261103/parent-platform-ec282-sdk-catalog-hosted-audit-20261106.json) passed independent75/236/14 verification. These are feature checks; genuine review, normal main/publication and native/Story acceptance remain required. The existing real CODEOWNER review-path clarification is pending; no identity or review policy bypass is authorized.

The seed union is closed at its immutable checkpoint, with77,985 indexed files/3,653,943,622 bytes and six declared links. The aggregate closed census at that checkpoint was 552,570 files/25,107,510,025 bytes. Earlier failed firstM hosted tests, corrected deadline root and all earlier source/evidence remain retained. The [newest closed index audit](release-phase/remote-resumption-20261103/parent-platform-seed-union-closed-checkpoint-independent-audit-20261106.json) independently verified every body, declared link and nested anchor. The [preceding complete continuation](release-phase/remote-resumption-20261103/readme-continuation-before-20261106-044700.md) retains the chronological producer, source, release and first-outcome history.

Genuine public Builds4.44.6/sourcead50 and Memories3.0.25/source246157 are [bound to the actual qualified producer and signed bytes](release-phase/remote-resumption-20261103/parent-builds4446-actual-qualified-producer-and-tuple-binding-20261106.json). Incoming public Platform1.5.3 and its original349/current259 [hosted source profiles](release-phase/remote-resumption-20261103/parent-platform153-current259-and-original349-hosted-audit-20261106.json) and [SDK rows](release-phase/remote-resumption-20261103/parent-platform153-sdk-catalog-hosted-audit-20261106.json) are independently authenticated. Their evidence does not qualify our added source repairs.

Newer Parties sourcefb66 adopted actual public153, but its [first normal hosted run failed at startup](release-phase/remote-resumption-20261103/parent-parties-fb66-first-hosted-outcome-and-cleanup-20261106.json), before the unchanged600-second deadline. All6,364 isolated cases passed with14 existing unexecuted cases; native0. Durable owner cleanup completed with every remaining inventory empty. The underlying exception and running-body cause remain unproved. Historical EventStoreeff9 [passed nine genuine native cases and exact cleanup](release-phase/remote-resumption-20261103/parent-eventstore-eff9-historical4443-native9-hosted-audit-20261106.json) on its historical4443 input; the final cohort still needs fresh evidence.

A [concrete source observation](release-phase/remote-resumption-20261103/parent-builds-ad50-safe-startup-status-loss-observation-20261106.json) found that Builds validates a safe failure status, then drops it when projecting unready-resource diagnostics. The bounded fix at8e9a is now [normal BuildsPR172](https://github.com/Hexalith/Hexalith.Builds/pull/172). Its [first red regression](release-phase/remote-resumption-20261103/parent-builds-safe-startup-first-red-independent-audit-20261106.json) reproduced the loss, and the [fixed20-case audit](release-phase/remote-resumption-20261103/parent-builds-8e9a-fixed20-and-1051-output-independent-audit-20261106.json) verifies all selected controls and1,051 unchanged compiled bodies. Its [closed checkpoint audit](release-phase/remote-resumption-20261103/parent-builds-8e9a-closed-checkpoint-independent-audit-20261106.json) verifies all25,149 new indexed bodies and15 declared links; the aggregate at that checkpoint was 577,719 files/27,350,711,088 bytes. Normal CI42165349577 passed all seven jobs and required analyses. The [immediate merge preflight](release-phase/remote-resumption-20261103/parent-builds-172-immediate-normal-merge-preflight-newer-main-refusal-20261106.json) found newer actual main0b086 and stopped before a merge request. Its [fully read native-claims change](release-phase/remote-resumption-20261103/parent-builds-0b086-full-incoming-claims-source-observation-20261106.json) is preserved in normal merge44b7. The [full source-union audit](release-phase/remote-resumption-20261103/parent-builds-44b7-full-proposal-exact-additive-union-and-messages-audit-20261106.json) verifies every object and both histories; [all38 combined controls](release-phase/remote-resumption-20261103/parent-builds-44b7-combined38-and-1051-output-independent-audit-20261106.json) passed. The [existing PR was updated normally](release-phase/remote-resumption-20261103/parent-builds-44b7-normal-existing-pr-update-20261106.json), and fresh CI42169041883 is active. Incoming0b mainCI42166762850 passed separately. The [combined closed checkpoint audit](release-phase/remote-resumption-20261103/parent-builds-44b7-closed-checkpoint-independent-audit-20261106.json) verifies all 23,868 additional indexed bodies, 15 declared links and 15 anchors. The current closed aggregate is **601,587 files/29,343,454,549 bytes**, with 103 declared links. [PR172 merged normally](release-phase/remote-resumption-20261103/parent-builds-172-normal-combined-merge-and-exact-tree-audit-20261106.json) as26b9, with the exact reviewed tree. [Fresh merged-source mainCI42171362850](release-phase/remote-resumption-20261103/parent-builds-26b9-main-ci-carrier-and-real-source-tests-audit-20261106.json) passed, including the independently verified source carrier and actual unit totals. [Normal Release42173656418](https://github.com/Hexalith/Hexalith.Builds/actions/runs/42173656418) is active after the [authorized production-review action](release-phase/remote-resumption-20261103/parent-builds-26b9-normal-protected-production-review-20261106.json). Its actual publication is still pending. The separate existing normal release published genuine **Builds4.44.7/source0b086**. Its [official signature/source/qualification audit](release-phase/remote-resumption-20261103/parent-builds-4447-source-ci-official-audit-20261106.json) verifies both signed tools, all317/41 members,55 retained payloads and301 source fixtures. The [actual finite producer binding](release-phase/remote-resumption-20261103/parent-builds4447-actual-qualified-producer-and-tuple-binding-20261106.json) is retained as intermediate authority; accepted pins and cohort are unchanged. This addresses missing diagnostic detail; it does not assert a native cause. No raw startup logs, new schema, production budget changes or retries are authorized.

The [sole specification](../../spec-2-8-run-ci-integration-on-disposable-runners-with-guaranteed-cleanup.md) controls the next work. Final public Parties publication and cohort admission, fresh28Parties/9EventStore native checks, all three sequential manual lifecycle/cancellation observations, every original acceptance criterion and six frozen matrix rows remain open. No candidate or whole-Story acceptance is claimed.

## Retained continuation, 2026-11-04

**Current gate: Parties full CI.** Genuine Builds4.44.2/sourceae68 and Platformv1.3.0/source338 are published and independently authenticated. EventStore PR392 is merged asdbc6; its fresh-main CI passed9nativecases and exact10-resource cleanup. Parties PR61 nowc83fead177ca91f4d4b5aa09fc2ec5826ae0f173 selects the genuine full tuple plus the narrowly reproduced generated-checkout exclusion. Its5-path capability correction passed449uniqueaffected.NETcases and68single-attemptlocalbrowsercases; the2-path checkout correction passed5focusedcases with realGit boundaries. Parent [capability source/control audit](release-phase/remote-resumption-20261103/parent-parties-434b-source-and-control-audit.json), [generated-checkout audit](release-phase/remote-resumption-20261103/parent-parties-c83-source-and-control-audit.json) and [local served-asset audit](release-phase/remote-resumption-20261103/parent-parties-capability-local-ui-audit.json) retain exact inputs and first failures.

Fresh [CI41831835601](https://github.com/Hexalith/Hexalith.Parties/actions/runs/41831835601) completed FAILURE atc83 after all6independentjobs and15isolatedprojects passed6,331cases/0fail/14NotExecuted. The [actual hostedUIartifact](release-phase/remote-resumption-20261103/parent-parties-c83-ui-hosted-audit.json) verifies68single-attemptpasses0retry/flaky/skip and6servedassetrecords matching the4signedFC4.7.6payloads. Integration stillrefusedHXP020before services; [first-outcome/emptycleanup summary](release-phase/remote-resumption-20261103/parent-parties-c83-hosted-summary.json) and [full artifact audit](release-phase/remote-resumption-20261103/parent-parties-c83-failure-artifact-audit.json) preserveactualevidence. No nativecase or manualscenario has executed.

Fullhandoffdiagnosis nowtargets a precise publishedprotocolprojection: signedBuildsAppHostRunner nullsResourceClaimStateDirectory for effect-free --validate-platform, while Platform338compares thatprojectedplan to the originalpersistedprivateplan byte-for-byte. Parent andsameimplementationagent independentlyfoundthe mismatch inactualsource. A real signed-AppHost/key/plan redreproduction and minimalowningcorrection are beingpreparedlocally; ordinaryclaimbindingandallsource/key/planrefusals remainrequired. No unchangedCIretry or unqualifiedpublication/adoption is authorized bythisfinding.

Actual changed-source [CI41824961081](https://github.com/Hexalith/Hexalith.Parties/actions/runs/41824961081) completed FAILURE after all6independentjobs and15isolatedprojects passed6,330cases/0fail/14NotExecuted. The [hosted UI audit](release-phase/remote-resumption-20261103/parent-parties-434b-ui-hosted-audit.json) records67single-attemptpasses/1focuscasepassedunderoriginalretry,0unexpected/skipped; firstfailure retained. Integration then refused before resources withHXP020ordinaryParties parenthandoff/sourceproof; [actual first-outcome/emptycleanup summary](release-phase/remote-resumption-20261103/parent-parties-434b-hosted-summary.json), [complete artifact audit](release-phase/remote-resumption-20261103/parent-parties-434b-failure-artifact-audit.json). No nativecase executed. The first b7 [isolated/UI failure audit](release-phase/remote-resumption-20261103/parent-parties-b7-failure-artifact-audit.json) remains historical; strict provisioning refusal was correct.

The same agent is investigating a concrete workflow/source-cleanliness mismatch locally: actualworkflowchecksouthelperat.hexalith/builds-execution, whilePartiesdoesnotignorethatgeneratedpathandPlatformrequirescleanGitroots. [RealGitpolicy observation](release-phase/remote-resumption-20261103/parent-parties-434b-generated-helper-git-policy-observation.json) confirms it is untracked; othergenericHXP020conditions still require diagnosis. Any fix remains narrow and must preserve real source/test/declaration/key checks, tuple, budgets and original failure evidence. No unchanged retry or new publication is implied.

The three real manual lifecycle scenarios, normal Parties merge/fresh-main evidence and final original-baseline diff/review remain pending. The [current spec history archive](release-phase/remote-resumption-20261103/implementation-history-through-20261104-1158.md) and [hash receipt](release-phase/remote-resumption-20261103/spec-history-archive-20261104-1158.json) preserve all prior scopes and observations. No candidate or whole-story acceptance is claimed.

## Earlier continuation checkpoints (retained history)

**Publication is gated by an actual main failure.** [ad54catalog job log/receipt](release-phase/remote-resumption-20261103/parent-builds-ad54-catalog-failure.json) shows the branch audit's generatedFromRevision2bcis unavailable after normal squash in a fresh main checkout;3050reported ancestry errors cascade from it. Parent missed that history dependency despite identical source trees and passing PRCI. The same implementation agent is preparing a normal audit-only follow-up anchored in retained main history; no validator weakening, orphan-history fetch or unchanged retry. The unfinished [eight-path Platform source draft](release-phase/remote-resumption-20261103/platform-full-parties-source-preparation-checkpoint.json) is safely retained, including its first three compile errors, with accepted tuple/pins unchanged. No release or native acceptance.

Builds PR159merged normally asad54fc75b8a1da663ee39da6a2397fc2a965159cafter all final-head checks passed. [Merge response](release-phase/remote-resumption-20261103/parent-builds-pr159-merge-response.json) and [postmerge source](release-phase/remote-resumption-20261103/parent-builds-pr159-postmerge-api.json) bind the identical reviewed tree. Parent [hosted package audit](release-phase/remote-resumption-20261103/parent-builds-4c160-hosted-package-audit.json) verifies55payloads/296fixtures and explicitly retains the CI carrier's skipped duplicate source-validation/release-ineligible status. The [separate full hosted .NET job](release-phase/remote-resumption-20261103/parent-builds-4c160-hosted-dotnet-audit.json) passed. Actual new-main push CI41789206152is running; no public version or accepted new tuple yet. The finite Parties source proposal is being prepared separately with its recorded tuple unchanged.

[EventStore005csource audit](release-phase/remote-resumption-20261103/parent-eventstore-005c-platform122-source-audit.json) verifies the two-path Platform1.2.2adoption, preserving every native/manifest/workflow/tool input. [Five actual caller controls](release-phase/remote-resumption-20261103/parent-eventstore-005c-focused-control-audit.json) and signed tool preparation passed. Existing PR392is pushed; fresh CI41787766444is running and supplies no native acceptance yet. [Builds4c160refactor review](release-phase/remote-resumption-20261103/parent-builds-4c160-refactor-review.json) independently passed43helper cases; required analysis checks pass while full CI41786512669runs. [Final evidence index audit](release-phase/remote-resumption-20261103/parent-builds-4c160-evidence-index-audit.json) verifies all110agent receipts. Previous05e CIwas normally cancelled after the final refactor; its successful local package qualification remains source-bound.

[Latest full handoff archive](release-phase/remote-resumption-20261103/implementation-history-through-20261104-063613.md), [SHA/size receipt](release-phase/remote-resumption-20261103/spec-history-archive-20261104-063613.json), preserves all prior source/review states. The current spec consolidates execution and the next bounded EventStore task. [Platform1.2.2 tuple](release-phase/remote-resumption-20261103/parent-platform-122-hosted-tuple-audit.json) passed196actual cases, with all8reader cases positively observed; [SDKmatrix](release-phase/remote-resumption-20261103/parent-platform-122-sdk-catalog-audit.json) passed60cases/208finite gates. This external release retains the authentic4.44.0tuple and supplies no real native/lifecycle acceptance. [05e local package audit](release-phase/remote-resumption-20261103/parent-builds-05e-local-qualification-audit.json) verifies the completed normal qualifier,4archives/55payloads/296fixtures, before the required Python readability refactor.

The latest EventStore a0c run completed failure before services: [actual terminal artifact audit](release-phase/remote-resumption-20261103/parent-eventstore-a0c-terminal-artifact-audit.json), [five independent gates](release-phase/remote-resumption-20261103/parent-eventstore-a0c-independent-artifact-audit.json). All16,160 executed isolated tests passed; native failed building the mapped catalog reader. The [independent prepared-mapping reproduction](release-phase/remote-resumption-20261103/parent-eventstore-a0c-catalog-host-role-component-audit.json) fails in the consumer location and succeeds in the existing generated-host role with six authentic public DLL hashes. Hosted compiler logs are absent; no native acceptance is claimed.

[Builds PR159](https://github.com/Hexalith/Hexalith.Builds/pull/159) proposes the bounded retention fix and Memories3.0.23 adoption. Parent independently passed41helper controls at05e and [verified only the Memories audit rows changed](release-phase/remote-resumption-20261103/parent-builds-05e-package-audit-scope.json). Full local source validation passed; package consumer controls and hosted CI are pending. [Required Codacy annotations](release-phase/remote-resumption-20261103/parent-builds-05e-codacy-annotations.json) need a scoped readability/formatting correction before merge. No source/review gate is bypassed.

[Full owner component findings](release-phase/remote-resumption-20261103/full-owner-component-findings.md), [checkpoint](release-phase/remote-resumption-20261103/full-owner-component-checkpoint.json), and [parent audit](release-phase/remote-resumption-20261103/parent-full-owner-component-audit.json) supersede the inferred need to publish an image merely to include the Memories extension. Five actual unchanged-reader executions passed; public and measured image owner variants yield identical catalogs (70 routes, 46 policies). Actual receiver capture needs contracts/installed targets; gateway/reader output consumes extension factories. These remain unadmitted component results, with real cross-version native and attachment qualification pending.

[EventStore f15 terminal artifact audit](release-phase/remote-resumption-20261103/parent-eventstore-f15-terminal-artifact-audit.json) records 20 isolated reports: 16,160 executed/passed, zero failed, 49 existing unexecuted cases. Its native gateway timed out at 600 seconds with HXR021; the finalizer succeeded and preserved the first failed outcome. The [reviewed tuple update was then pushed](release-phase/remote-resumption-20261103/parent-eventstore-platform121-push.json), [PR392 metadata updated](release-phase/remote-resumption-20261103/parent-eventstore-platform121-pr-update-connector.json), and fresh CI41779614495 started at the new source and completed as audited above.

The hosted result exposed [missing declared diagnostic retention](release-phase/remote-resumption-20261103/parent-hosted-diagnostic-retention-gap.json). The helper task is reopened for a bounded fix preserving only known redacted, digest-bound lifecycle/native/functional artifacts. A normal Builds proposal is in progress with that fix and the authenticated Memories 3.0.23 catalog adoption. The current [05:35 archive receipt](release-phase/remote-resumption-20261103/spec-history-archive-20261104-0535.json) preserves the full preceding handoff.

[FrontComposer 4.7.6 prerequisite audit](release-phase/remote-resumption-20261103/parent-frontcomposer-476-official-package-audit.json) verifies all eight signed public packages against the immutable release source and 149 matching release payload members. Normal release and exact-source CI passed. This records the revision selected by newer Builds main for a possible later coherent adoption; the current Platform 1.2.1 tuple remains unchanged, and no served UI/native acceptance is claimed.

The [current readable handoff](../../spec-2-8-run-ci-integration-on-disposable-runners-with-guaranteed-cleanup.md) preserves all earlier execution history in [the 05:15 archive](release-phase/remote-resumption-20261103/implementation-history-through-20261104-0515.md), [SHA/size receipt](release-phase/remote-resumption-20261103/spec-history-archive-20261104-0515.json). Baselines, frozen intent and original task checkboxes are unchanged.

[Builds 4.44.0 official audit](release-phase/remote-resumption-20261103/parent-builds-4440-official-audit.json) verifies both signed public CLIs, exact unsigned inventory reconstruction, actual versions, 55 qualification payloads and 295 source-bound fixtures. [Platform 1.2.1 SDK catalog audit](release-phase/remote-resumption-20261103/parent-platform-121-sdk-catalog-audit.json) verifies four sets of 15 binding/refusal cases, retained executable hashes and all 208 finite evaluations.

[EventStore a0c source audit](release-phase/remote-resumption-20261103/parent-eventstore-a0c-platform121-source-audit.json) checks the six-path coherent tuple adoption and five actual passing contracts. It was pushed after prior f15 CI41774344462 completed; fresh CI41779614495 completed failure before services, as audited above. [Parties c10 hosted declaration](release-phase/remote-resumption-20261103/parent-parties-c10-declaration-hosted-audit.json) passed within the unchanged ten-minute limit, and [its UI artifact](release-phase/remote-resumption-20261103/parent-parties-c10-ui-hosted-audit.json) records 68 clean single-attempt passes. Actual package admission failed before services; native acceptance remains incomplete.

The confirmed Memories image extension omission is an observation, not yet a demonstrated runtime dependency: gateway/catalog-reader output loads public adapters, while domain receivers observe contracts and installed targets. The completed component task resolved that boundary without demonstrating an owner publication correction; real runtime compatibility remains pending. The earlier component checkpoint and its stated interpretation remain preserved.

Platform 1.2.1 at `728baa7c0f006d86e71d6c9b29527dabc8e16e49` has an independently audited hosted tuple: [188 passing controls and immutable source/package comparisons](release-phase/remote-resumption-20261103/parent-platform-121-hosted-tuple-audit.json). [Artifact retrieval](release-phase/remote-resumption-20261103/parent-platform-121-hosted-retrieval.json) records the four SDK catalog archives; the completed detailed catalog audit is linked above. [Canonical Builds synchronization](release-phase/remote-resumption-20261103/parent-platform-728-canonical-builds-sync.json) aligns the direct checkout with the recorded 4.44.0 tuple.

[Memories 3.0.23 component audit](release-phase/remote-resumption-20261103/memories3023-component-checkpoint.json) verifies the public package family and live image/source identities. Ordinary domain output omits the extension assembly; catalog registration and finite receiver calls are present. The current image SDK bytes need explicit cohort qualification. These results do not supply native or Story 2.8 acceptance.

[EventStore f15 correction audit](release-phase/remote-resumption-20261103/parent-eventstore-f15-hosted-fix-source-audit.json) independently checks the residual governance fixture fix and its red/green reports. The existing proposal now selects the already released Platform 1.2.1/Builds 4.44.0 EventStore+Tenants profile and has the hosted pre-service failure audited above. Parties and the actual lifecycle matrix remain incomplete.

## Earlier checkpoints
Historical continuation (2026-11-03): the user authorized remote package verification,
the dedicated registry reader procedure, reference caller PRs and hosted cleanup
qualification. The reader is applied and measured, with only its two pull-only
secrets delivered to EventStore and Parties. Builds v4.43.4 is published from
`26991e8c542704be44cd8d9f58084d56fa627adb`; signed package content, release inventory,
qualification payloads and embedded EventStore SDK bytes are verified. Published
Platform v1.1.5 at `bf101b9d5f19ba741852b0230e526d1f01422022` records the matching
tuple and released snapshots; redundant PR14 is closed unmerged. The reference v2 proposals were merged externally on 2026-11-03 at 14:51–14:52 UTC,
with their known native blockers still unresolved. Fresh coherent adoption and real
hosted runtime/lifecycle acceptance remain incomplete; no candidate acceptance is
claimed.

Current receipts are under [remote-resumption-20261103](release-phase/remote-resumption-20261103/):
[registry](release-phase/remote-resumption-20261103/registry-live-checkpoint.json),
[published content](release-phase/remote-resumption-20261103/builds-4434-checkpoint.json),
[independent content audit](release-phase/remote-resumption-20261103/parent-builds-4434-content-audit.json),
and [Platform supersession](release-phase/remote-resumption-20261103/parent-platform-pr14-superseded.json).
The active [implementation spec](../../spec-2-8-run-ci-integration-on-disposable-runners-with-guaranteed-cleanup.md)
records the next sequential work. The earlier local-only handoff below is retained
as historical provenance; its old versions and authorization limits describe that
attempt, not the current execution state.

## Historical native qualification blockers

Parties [PR59](https://github.com/Hexalith/Hexalith.Parties/pull/59) at
`9c439ec5bbf2f7a79048ab797686cae6affa465f` completed all fifteen isolated projects:
6,291 executed tests passed, zero failed, and five optional tests were not executed.
The full UI and package-consumer gates passed. The first native invocation failed
before allocation with HXP020 because the published producer looked for the catalog
inside an uninitialized nested Platform checkout. Its verified classified artifact
records `not-allocated`; it supplies no real service teardown credit.

[Builds PR155](https://github.com/Hexalith/Hexalith.Builds/pull/155) merged normally as
`e07ac76e9b2a6c0ef34c02507bea054d25e48be5` after full exact-head CI and required
analysis checks passed. Its tree exactly matches reviewed head `8b99302b592acaa49937bf3f714317378c5c1d09`. The focused 83
cases pass; the parent independently reran the eight new real-Git cases. Its fresh
[main CI](https://github.com/Hexalith/Hexalith.Builds/actions/runs/41657058349)
was cancelled after an external main advance to `090797e333748b1a62db0a857d8d0f0e40b524fe`.
That incoming merge retained the runtime fix but duplicated the approved KeyVault
package row. Both exact hosted failures are retained. All six local catalog/audit
controls passed after removing the extra row, and external work merged both
identical proposals as current main `725754075e1d6369430a3adf94d46fb71e6eaf9e`.
The parent verified its complete tree equals reviewed e07. Fresh
[main CI](https://github.com/Hexalith/Hexalith.Builds/actions/runs/41661561500)
completed successfully, including every required job and final aggregation.
Parent rechecked source/current main and no active publisher, then dispatched
[Release41666723123](https://github.com/Hexalith/Hexalith.Builds/actions/runs/41666723123).
Its exact-source preflight and normal protected production review passed under
existing authorization; qualification is active. No new version or artifact is
assumed. The cancelled earlier run supplies no passing credit.
The normal prerequisite publication completed as v4.43.5 from
`89f62052c932d054bda2b435bdfef60d96e77612`, preserving the Azure catalog additions.
That version retains HXP020 and is not adopted as the corrected native producer.
The externally published Platform v1.1.6 resolves to
`8ea8fe092f8abb5c932a61830aae16051d442463`, with the same old 4.43.5/89 binding.
A fresh qualified publication and coherent Platform/reference adoption remain.

The dependent [Parties PR60](https://github.com/Hexalith/Hexalith.Parties/pull/60)
is a draft. Parent reviewed its full source and launch/resume recovery fixes, then
independently passed all 27 controls at clean `760062d8464e21cddaec9f3d8497fa82a61a3251`.
Actual-process controls verify the preallocation gate and interrupted-launch recovery;
resume errors retain exact cleanup attempts and diagnostics. A live cancellation
phase snapshot is being added before hosted qualification. These are local source
controls with no native service allocation. Actual
service success/failure, startup/execution cancellation, partial startup, active
attachment withdrawal, exact cleanup retry, peer preservation and verified zero
owned leftovers remain incomplete. No candidate acceptance is claimed.

Current detailed receipts include [isolated execution](release-phase/remote-resumption-20261103/parent-parties-9c-isolated-project-audit.json),
[exact native refusal](release-phase/remote-resumption-20261103/parent-parties-9c-native-first-refusal.json),
[producer correction](release-phase/remote-resumption-20261103/builds-direct-catalog-codacy-checkpoint.json),
and [immutable publication](release-phase/remote-resumption-20261103/builds-4435-observed-publication.json).

## Historical local implementation handoff

Status: local implementation and controls; hosted qualification is incomplete.
No package/image publication, remote CI dispatch, accepted-pin change or candidate
acceptance is authorized or claimed by this work.

The runner now permits compatible active same-job CI attachments, publishes an
external owner receipt before allocation, withdraws and reports active holds before
teardown, preserves first outcomes/expiry, rejects unknown cleanup discovery or
owned leftovers, and suppresses CI failure retention. The workflow requires declared
package suites and successful isolated tests before provisioning, uses the exact
pinned tool and verified Release/package preparation, and has both a helper `finally`
and an independent bounded `always()` exact-owner finalizer.

The Platform changes make the existing native preparation and catalog participant
paths available in package builds. Missing catalog reader/package/image inputs still
refuse before acceptance; released owner-snapshot provenance checks remain in force.
EventStore's descriptor SDK owns the shared exact-stream persistence observer now
used by Parties. EventStore's new suite checks the real gateway's readiness and
anonymous rejection plus actual isolated Redis end-state. Parties retains its full
real command/query/event-stream end-state suite. The EventStore infrastructure suite
alone does not establish domain command-to-event behavior or candidate acceptance.

## Local verification provenance

`focused-provenance.json` records exact commands, source SHA-256 values and the
loaded Debug assembly/dependency hashes. The fallback recompiles the changed
Tooling, CLI and Module.Tests assemblies and may reuse other fixture/dependency
binaries. It is local source diagnostics; it is neither a Release package test nor
hosted evidence. Individual `.txt` results and build output preserve successful
checks and broad blockers separately. No `.log` evidence is relied on for tracking.

The broad Module.Tests graph initially failed without mapping on missing
Commons/FrontComposer fixture references. With the existing verified source mapping
it failed `HXW005`: mapped FrontComposer source duplicates restored
`Hexalith.FrontComposer.Contracts/4.7.1`. This gate was not weakened. The focused
no-reference-rebuild lane is documented separately.

The first Platform no-reference-rebuild attempt exposed reused Tenants assembly
version drift. The subsequent full mapped graph resolves it and all 13 focused
Platform controls pass; their actual source/binary hashes are retained separately
in `platform-focused-provenance.json`.
A reference suite run without its descriptor is a required negative control and
fails with zero skips; it is never counted as an integration pass.

## Recorded local results

- Builds: 238/238 serialized cases pass (CI lifecycle 14, admission 130, engine 43,
  runner review regressions 51); CLI/Tooling and focused test builds have zero warnings/errors.
- Platform: the full mapped Debug graph builds with zero warnings/errors; native
  preparation 8/8 and package/catalog prerequisites 5/5 pass.
- EventStore: mapped SDK/reference/control builds pass with zero warnings/errors;
  descriptor controls 15/15 pass. Its absent-descriptor reference control fails
  2/2 tests with zero skips, as required.
- Parties: mapped reference build passes with zero warnings/errors, declaration
  controls 7/7 and isolated observation controls 4/4 pass. Its absent-descriptor
  native control fails 1/1 with zero skips, as required.
- Python integration helper 15/15, preparation helper 9/9, required-CI-status
  control and 223 executable/static workflow assertions pass.
- The package reference suite against current published EventStore `3.128.4`
  refuses at compile time on missing descriptor APIs; this is a publication
  prerequisite, not a verified package integration result.

`verification.json` separates positive checks, expected negative controls and the
remaining broad/publication/hosted gates. Initial failed focused results are retained
as `initial-*` and superseded by the final `.txt` results and focused provenance.

## Audit correction controls

The helper now forwards cancellation that arrives inside `Popen` immediately after
child assignment; repeated handling does not send duplicate interrupts. The 15 helper
controls include the exact constructor-time race and an actual helper subprocess
using a temporary fake `dotnet` executable. SIGTERM reaches the helper, SIGINT reaches
the child, and the real receipt-based finalizer executes exact down with cancelled
job status. No child/finalizer mock is used by that process control.

Five additional CI lifecycle cases execute `ModuleCommandExecutionService` for
success, failure, cancellation and partial-startup failure/cancellation. A live owned
AppHost process reads the committed cleanup request, withdrawal report and first
terminal record at its first stop signal. Tests assert original expiry, immutable
first outcome/time, no CI failure retention, complete cleanup and no hold waiting.
The partial-startup callback calls the real engine `FailAndTearDownAsync` path.

These are local source-mode fixtures with simulated CI identity and real native/
AppHost processes; composition observations are offline. They do not authenticate
package artifacts or qualify an Aspire/Dapr/Docker topology or hosted lifecycle.
`pre-audit-*` receipts/results retain the earlier 233-case binary provenance; the
current focused receipt and `.txt` results identify the rebuilt 238-case binary.
Hosted publication/qualification remains pending and the frozen intent is unchanged.

## Concrete publication inputs and dependency order

Existing source checkouts are identified in `release-inputs.json`, together with
changed-file hashes. They are uncommitted implementation inputs, not publishable
exact-source authority. Owners must first choose reviewed committed revisions and
release versions through their existing release decisions. No future version or
release SHA is invented here.

| Order | Owner and required source/package inputs | Existing release/check entry points |
| --- | --- | --- |
| 1 | EventStore descriptor APIs in `src/Hexalith.EventStore.Testing` and `src/Hexalith.EventStore.Testing.Integration`, including `AspireTopologyFixtureBase`, private descriptor/endpoint readers and `TestEnvironmentStreamReader`; compatible Contracts/Client/Server/ServiceDefaults/Aspire catalog/bootstrap APIs | `references/Hexalith.EventStore/.github/workflows/release.yml`; `tools/release-packages.json`; `scripts/pack-release-packages.py`; `scripts/validate-nuget-packages.py`; `scripts/validate-consumer-package-references.py` |
| 2 | Tenants Contracts/Server catalog and bootstrap delegation APIs plus the declared domain/API images; Memories Contracts/EventStore/EventStoreExtensions catalog and tenant APIs plus declared server/domain/clock/telemetry images, all compatible with step 1 | Each owner's existing `.github/workflows/release.yml`, package declarations and container provenance gates; exact main source and protected publication remain mandatory |
| 3 | Receipt-capable Builds CLI/Tooling and packaged AppHost/EventStoreHost ABI, runner helper and reusable workflow, bound to approved compatible owner-package inputs from steps 1–2 | `references/Hexalith.Builds/.github/workflows/build-release.yml`; `Tools/build-g4-tool-packages.ps1`; `Tools/test-g4-tool-package-contracts.ps1`; `Tools/publish-g4-tool-packages.ps1` |
| 4 | Platform Composition/native preparation/catalog reader changes, together with exact released EventStore/Tenants/Memories declaration snapshots, commit/hash provenance and image identities | Platform's existing declaration provenance and package composition checks; receipt-capable tool adoption/qualification decisions remain separate |
| 5 | EventStore/Parties reference enrollment, test projects and solution entries; Parties published Contracts compatible with its `CreatePartyComposite`/query/event suite; exact new Builds reusable-workflow SHA and local tool version | E/P existing `ci.yml`; E/P explicit release callers after exact-source CI; `scripts/render-platform-declaration.py --check` and strict renderer tests in Parties |
| 6 | Disposable hosted reference CI plus deliberate cancellation/partial-startup lifecycle qualification | Required observations below; no remote execution occurred in this implementation |

Prerequisite release source and caller activation must be reviewed as separate
commit groups. EventStore step 1 consists of SDK/helper and SDK-control changes;
its new native test project/solution/manifest/tool/CI enrollment belongs to step 5.
This prevents the prerequisite release from depending on the receipt-capable Builds
release it enables. Use the existing green-source release gates for each
prerequisite group. Do not solve the ordering by bypassing integration or weakening
publication/provenance checks.

At step 3 an owner-approved catalog update may be necessary before the new Builds
tool can be packaged against the released APIs. At step 4 replace snapshots only
with exact bytes from the approved owner releases and their recorded commit/digest.
At step 5 update accepted reference pins only after those releases and independent
adoption decisions exist. These are explicit future decisions; this change makes
none of them.

Current accepted values remain: EventStore `3.128.4`, Tenants `5.11.2`, Memories
`3.0.10`, Parties `2.0.0` in Builds' central family catalog; Parties' tool pin
`4.39.0` and reusable-workflow pin
`1745ea0f09df9e518aeea3eeb1a7df836f67e09d` remain unchanged. EventStore newly
enrolls the already existing `4.39.0` tool coordinate; it does not assert that this
old release supports the new receipt option. EventStore's existing workflow ref is
still `main`. The staged new inputs cannot run against an older reusable workflow;
they intentionally grant no acceptance before activation against an approved new
workflow/tool release.

Platform's existing snapshot provenance remains EventStore `3.120.0`, Tenants
`5.8.0` and Memories `2.27.1`. Those snapshots are incompatible with the current
family catalog and remain refused. Source implementation is not permission to
relabel them or select unreleased package versions.

Before changing any accepted pin, require all of:

1. Owner-selected exact committed release source with successful exact-source CI
   and existing protected publication approval; package and image content identities
   must trace to that revision's Release outputs.
2. The declared release manifest's complete family packages and downstream consumer
   checks, including exported descriptor/persistence APIs and catalog/bootstrap
   compatibility. Keep Testing and Testing.Integration on the EventStore family pin.
3. Published tool archive/AppHost ABI qualification and exact installed tool version
   verification; no global tool, floating restore or legacy execution fallback.
4. Exact declaration bytes/commit/SHA-256 and released image provenance matching the
   selected catalog family. Retained registry access must be pull-only; no staging or
   production provider credential enters integration jobs.
5. Separate Builds adoption and Platform acceptance/integration qualification
   references for the exact tool/policy. A compatible numeric range alone is
   insufficient, and baseline-only results cannot be promoted into candidate evidence.

## Hosted evidence still required for completion

Save a revision-bound record for both reference repositories that identifies source
SHA, actual reusable-workflow SHA, tool version/archive hash, all loaded package/image
identities, GitHub repository/run/attempt/job and disposable Linux runner kind.
Record isolated completion before any service allocation and nonempty complete
required package suites. Then exercise success, failing tests, cancellation during
startup, cancellation during execution and interrupted partial startup.

For each terminal path retain the durable preallocation receipt, first outcome/time,
attachment acceptance/absolute expiry, same-job positive and cross-job negative
admission, admission closure, withdrawal document/report recorded before resource
stops, exact down attempts and final verified zero owned container/process/workspace/
claim IDs. Include a failed or unknown-discovery cleanup followed by successful exact
retry and an unaffected peer-owner control. Cancellation after a recorded failure
must preserve that failure while reporting `ci-job-cancelled` withdrawal. Retain only
classified redacted documents, outside deleted workspaces.

No hosted observation in this directory currently supplies those qualifications.
Story completion therefore remains pending approved publication, reference activation
and hosted cancellation/partial-startup evidence.

Verified corrected Builds4.43.6 publication: [checkpoint](release-phase/remote-resumption-20261103/builds-4436-checkpoint.json), [signing transformation](release-phase/remote-resumption-20261103/builds-4436-signing-transformation.json), [producer/payloads](release-phase/remote-resumption-20261103/builds-4436-evidence-payload.json), [embedded content](release-phase/remote-resumption-20261103/builds-4436-packaged-content.json). Exact725 source; all55 payloads, two signed packages, offline revocation and actual executable verified. Platform adoption and real native/hosted acceptance remain pending.

Original942 hosted tuple160/0/0 and four catalog SDK bindings passed: [tuple audit](release-phase/remote-resumption-20261103/parent-platform-942-hosted-tuple-audit.json), [catalog audit](release-phase/remote-resumption-20261103/parent-platform-942-hosted-catalog-audit.json). Incomingddf regressed only the Builds gitlink; [PR17 repair checkpoint](release-phase/remote-resumption-20261103/platform-builds-pin-repair-checkpoint.json), [independent exact-source audit](release-phase/remote-resumption-20261103/parent-platform-91b-source-audit.json). Fresh91b hosted checks/normal source integration remain pending. Selected Tenants7 signature content hash matches assets; actual hosted copied/loaded output DLL measurement remains a native-reference requirement.

Current actualmained tuple160/0/0 is verified: [tuple audit](release-phase/remote-resumption-20261103/parent-platform-ed156-hosted-tuple-audit.json). Current-main SDK3.132.1 fifteen binding/208capability cases and retained executable hashes verified: [SDK audit](release-phase/remote-resumption-20261103/parent-platform-ed156-sdk-3_132_1-audit.json). Three other SDK jobs remain queued; no new Platform release/native/lifecycle acceptance is claimed.

Genuine Platform1.1.7/ed156 and published Builds4.43.6/725 are now qualified: [release/tag/gate checkpoint](release-phase/remote-resumption-20261103/parent-platform-117-qualified-checkpoint.json). Parent avoided duplicate publication; all160tuple/fourSDK results and artifacts verified. Fresh reference caller adoption and real native/lifecycle acceptance remain pending.

Parent independently compared all18 lifecycle workflow/controller/native fixture/control paths at actual Parties adoption base6795536e3fe5fd4cf44eb5f4c67e69f3374532b4 against reviewed PR60 heade42448ab0a94f88d2f92436c8c023edf56f5d223: every Git blob and byte hash is identical. [Source preservation receipt](release-phase/remote-resumption-20261103/parent-parties-679-lifecycle-source-preservation.json). This proves preservation only; the new adoption head and real hosted lifecycle execution remain separate requirements.

Parent read the complete corrected EventStore8-path net patch at5df9d07df44dca400ff09468a91c34e9685c3dd1 against actual main2253b733539a84f436443d3b4996ce2136437bef and compared the real725 mapping/plan/state/native handoff source. The correction derives context/output from existing metadata and verifies caller invocation, with all incoming paths/gitlinks preserved. Actual scrubbed-process TRX7/7passed; no real native acceptance yet. [Independent source audit](release-phase/remote-resumption-20261103/parent-eventstore-5df-native-binding-source-audit.json).

Parties coherent adoption PR61 is open at126acd2d7ab08ad614ede09b0cb931573b053a03 (base6795536e3fe5fd4cf44eb5f4c67e69f3374532b4). [Parent complete16-path audit](release-phase/remote-resumption-20261103/parent-parties-126acd-source-audit.json) and [initial11-path selection audit](release-phase/remote-resumption-20261103/parent-parties-cb795-adoption-source-audit.json) preserve workflow/ledger/matrix/controller/real assertion boundaries. Parent independently inspected actual13/69/3TRX and32/7final control output. [Agent checkpoint](release-phase/remote-resumption-20261103/parties-platform117-checkpoint.json). ECI41711963375 and PCI41714178534 remain queued; all actual reference native/SDK/cleanup and later lifecycle acceptance are still incomplete. [Superseded cancelledE107artifact audit](release-phase/remote-resumption-20261103/parent-eventstore-107-superseded-artifacts.json) preserves9partial isolated reports and actualcontext/not-allocated without acceptance credit.

Consolidated mutable execution handoff after both coherent reference PRs opened, preserving complete prior spec bytes: [implementation-history-through-20261103-204237.md](release-phase/remote-resumption-20261103/implementation-history-through-20261103-204237.md),65324bytes/SHA256093a469160043b4238046d297d121adc3362ec9a90850d88ce551d8215c30b8a. [Consolidation receipt](release-phase/remote-resumption-20261103/spec-history-archive-20261103-204237.json). Original task/context prefix, baselines, frozen hash and two unchecked tasks unchanged.

[First actual Parties126acd hosted gate audit](release-phase/remote-resumption-20261103/parent-parties-126acd-first-hosted-gates.json):52Memories18protocol/20BMADcontrolspassed; full logs and actualb1f400PRmerge parents/tree verified against reviewed126acd. Required whole-project/native/cleanup qualification remains pending.

[Actual Parties126 UIartifact audit](release-phase/remote-resumption-20261103/parent-parties-126acd-ui-hosted-audit.json):official13268879291/SHA7c5f1313269ace34c5ff0e223337b63401cb7235bd767439923bc80a89da190a,67clean/1flaky/0unexpected/skipped. Firstfailure screenshot/context/trace and actualretryretained; unchangedconfiguredCIretries2. This jobpassedbutisnotclean68/0retry andprovidesnonativecredit.

Actual Parties126 declarationjob126189732414 exhausted its10minutejobtimeout: directthreeownerfullhistoryinitialization9m41s, released4.43.6validationprintedSuccessbeforetimerkilledjob; generator/strict7testsdidnotrun. [Fullfirstlog](release-phase/remote-resumption-20261103/parent-parties-126acd-declaration-first-job.txt), [actualjob/steps/checkannotations](release-phase/remote-resumption-20261103/parent-parties-126acd-declaration-first-cancellation.json). Narrow samePR task records depth1directexactgitlinkcheckout correction andgenuinecoldsetupverification;10minutejoblimit/assertionsremainunchanged. No nativeorcleanupacceptance.

PartiesPR61nowc10ce7e8f323ded3a69711160c5b814fb95f5904/base7d3bc8879c39d32f34866df5098a0d369821db03: [one-lineboundedcheckout/sourcepreservationaudit](release-phase/remote-resumption-20261103/parent-parties-c10ce-source-preservation.json), [independentcoldrepository/realreleasedvalidationaudit](release-phase/remote-resumption-20261103/parent-parties-shallow-cold-source-audit.json), [agentcheckpoint](release-phase/remote-resumption-20261103/parties-declaration-shallow-checkpoint.json). FreshCI41718277171queued; actualhosted10min/nativeacceptancepending. Old126independentpasses/supersededcancellationremainseparatehistory; [offline3+22audit](release-phase/remote-resumption-20261103/parent-parties-126acd-offline-cohort-audit.json). E8dCI41711963375buildingwith3independentgatespassed;allrequiredreference/native/lifecycleworkstillincomplete.

Nov4 actual reference failures: [verified official archives, every20E/15PTRX and classified native JSON](release-phase/remote-resumption-20261103/parent-reference-20261104-failed-artifact-audit.json). E8d attempt2 (external rerun) has2isolated and1Contracts failure, blocking native allocation. Pc10 has6294isolatedpasses across15projects then HXP020before services; empty cleanup does not supply real service teardown credit. Source correction task is scoped to the three actual Efailures; parent investigates Ppublished preflight separately.


2026-11-04 finite ordinary Parties source proposal: [draft Platform PR22](https://github.com/Hexalith/Hexalith.Platform/pull/22), head9f8d4402c8d0f17aca6290a3b93fe8ae660ab480/base991e2df97dc8493e41096006b0d3203a71730ae8. [Checkpoint](release-phase/remote-resumption-20261103/platform-full-parties-proposal-checkpoint.json), [complete17-path patch](release-phase/remote-resumption-20261103/platform-full-parties-proposal.patch), [hash index](release-phase/remote-resumption-20261103/platform-full-parties-proposal-evidence-index.json). Production/test Release package builds pass; all238actual required source/component cases pass (201incoming plus37proof controls). Separate current full reader checks12signed public bodies and generates70routes/46policies; actual wrong public bytes and invalid cohorts refuse. Original failures and component mapping-field correction remain explicit. Accepted tuple/Builds6249/M18provenance unchanged. Genuine new tuple, full immutable-image parent admission, coherent generated reader, real native/wire and lifecycle qualification remain incomplete; no services or candidate/Story acceptance.


2026-11-04 published Platform1.2.3/source991 is independently authenticated: [actual API/tag/run receipts](release-phase/remote-resumption-20261103/parent-platform-123-api-receipts.json), [downloaded artifact digests](release-phase/remote-resumption-20261103/parent-platform-123-hosted-retrieval.json), [201 actual tuple cases/source/package audit](release-phase/remote-resumption-20261103/parent-platform-123-hosted-tuple-audit.json), [60 SDK matrix/208 capability audit](release-phase/remote-resumption-20261103/parent-platform-123-sdk-catalog-audit.json). Genuine incoming Tenants API APP_API_TOKEN binding is verified through actual SDK and unstarted-model controls; no native acceptance is inferred. Existing EPR392 gets only direct Platform991/documentation; old005c600s startup failure and exact10-resource zero-leftover cleanup remain [actual artifact evidence](release-phase/remote-resumption-20261103/parent-eventstore-005c-terminal-artifact-audit.json).

Builds audit correction PR160 passed all exact-head gates, including hosted115generator/103validator scenarios: [actual catalog job log](release-phase/remote-resumption-20261103/parent-builds-pr160-hosted-catalog-log.txt), [55-member artifact audit](release-phase/remote-resumption-20261103/parent-builds-pr160-hosted-package-audit.json). Normal squash merged asae68c59bd2f4145a23ccf68c3711f98eaf62195f with exact reviewed tree68e0d981c2387ac45cbd63b8ee1d0c954ec2c609: [request](release-phase/remote-resumption-20261103/parent-builds-pr160-merge-request.json), [response](release-phase/remote-resumption-20261103/parent-builds-pr160-merge-response.json), [source/API receipt](release-phase/remote-resumption-20261103/parent-builds-pr160-postmerge-api.json). Fresh main push CI41795651910 is running; no corrected public version or accepted new tuple is assumed. Parent independently [read the17-path Platform source draft and audited all104evidence hashes/238actual cases](release-phase/remote-resumption-20261103/parent-platform-full-parties-proposal-source-and-control-audit.json); complete full-profile admission/new-tuple/runtime qualification remains pending.


2026-11-04 EventStore existing PR392 now adopts genuine Platform 1.2.3/source991 at b63dc2fe15e41621741d0e49e088f10046ec7bed. [Checkpoint](release-phase/remote-resumption-20261103/eventstore-platform123-checkpoint.json), [two-path correction](release-phase/remote-resumption-20261103/eventstore-platform123-correction.patch), [full proposal patch](release-phase/remote-resumption-20261103/eventstore-platform123-current-main.patch), [hash index](release-phase/remote-resumption-20261103/eventstore-platform123-evidence-index.json). Normal preparation, genuine signed CLI 4.44.0, Release/package build and actual five caller/harness tests passed; three released owner controls and static checks passed. All 44 protected workflow/tool/declaration/native/harness input hashes remain unchanged; Builds6249/Tenants1b2 and all twenty isolated/five independent jobs remain. Actual new CI41796717649/attempt1 is in progress. Old005c startup failure/exact cleanup remain separately retained; no native acceptance, recovered missing exit log or lifecycle-retention claim.


Builds accepted mainae68 passed complete PUSH CI41795651910: [terminal source/jobs](release-phase/remote-resumption-20261103/parent-builds-ae68-terminal-ci-api.json), [actual.NET counters/log audit](release-phase/remote-resumption-20261103/parent-builds-ae68-hosted-dotnet-audit.json), [55payload/296fixture carrier audit](release-phase/remote-resumption-20261103/parent-builds-ae68-hosted-package-audit.json). Normal Release41798378661dispatched after unchanged source guard0/no active publisher, then authorized required production review through existing role: [dispatch](release-phase/remote-resumption-20261103/parent-builds-ae68-release-dispatch-response.json), [review](release-phase/remote-resumption-20261103/parent-builds-ae68-production-review-response.json). Publication and genuine downstream tuple remain pending. Existing EPR392nowb63dc2fe/Platform1.2.3, freshCI41796717649; [complete two-path source audit](release-phase/remote-resumption-20261103/parent-eventstore-b63-platform123-source-audit.json), [28evidence/actual5TRXaudit](release-phase/remote-resumption-20261103/parent-eventstore-b63-focused-control-audit.json). No native/lifecycle acceptance yet.


2026-11-04 finite Parties entry/reader preparation is committed locally as 5ac302c3a15fbeb127bb387722aec4012b913c4d (not pushed; draft PR22 remains 9f8d). [Checkpoint](release-phase/remote-resumption-20261103/platform-full-parties-entry-preparation-checkpoint.json), [nine-path correction](release-phase/remote-resumption-20261103/platform-full-parties-entry-preparation-correction.patch), [full proposal](release-phase/remote-resumption-20261103/platform-full-parties-entry-preparation-full-proposal.patch), [hash index](release-phase/remote-resumption-20261103/platform-full-parties-entry-preparation-evidence-index.json). Actual owner/catalog root corrections preserve normal producer mapping semantics; isolated direct Parties source advances only to reviewed f1f33/40cee under the recorded exception, retaining backend639/860 roles. Release/package build passed with zero warnings/errors; all77focused controls passed, including real normal resolution. Both new full positives are red at the loaded M18 public-body mismatch before mapping or ComposeRun;28associated full cases remain unrun and required counts stay238. Genuine published tuple/provenance adoption and full/runtime qualification remain pending; no service or acceptance claim.


Full Parties entry preparation is preserved locally at5ac302c3a15fbeb127bb387722aec4012b913c4d/unpushed, remotePR22still9f8d: [28-file source/control audit](release-phase/remote-resumption-20261103/parent-platform-full-parties-5ac-source-and-control-audit.json), [actual checkpoint](release-phase/remote-resumption-20261103/platform-full-parties-entry-preparation-checkpoint.json). Parent read all9paths; actual77focusedcases pass, both full positives fail at the real loadedM18hash before mapping/ComposeRun,28associatedcases unrun/profile238unchanged. Normal owner/catalog-root assumptions are corrected, and only isolated directP advances to alreadymerged reviewedf1f33/actual40cee lifecycle declaration: [concrete source prerequisite audit](release-phase/remote-resumption-20261103/parent-platform-parties-source-prerequisite-audit.json). Published Pbackend639/860authority and B6249/tool4.44.0/M18tuple stay distinct and unchanged. New required positive admission/reader and genuine full qualification remain pending publication. E b63 [actual retained independent artifact audit](release-phase/remote-resumption-20261103/parent-eventstore-b63-independent-artifact-audit.json) verifies1518Contracts passes/2NotExecuted and7Tsource passes; main native qualification remains separate. [Initial API/artifact metadata](release-phase/remote-resumption-20261103/parent-eventstore-b63-initial-api-and-independent-artifacts.json).


EventStore b63 actual CI41796717649completed success: [full actual20TRX/native metadata/zero-leftover audit](release-phase/remote-resumption-20261103/parent-eventstore-b63-terminal-artifact-audit.json), [tested PRmerge/head same-tree proof](release-phase/remote-resumption-20261103/parent-eventstore-b63-tested-tree-api.json). All16160executed isolated cases pass/49NotExecuted;5independentjobs green. All10resources ready, CLI native report passed, immutable passed first outcome and exact10-resource cleanup verified. Old4.44.0helper omitted actual referenced nativeTRX and lifecycle bodies, so exact native case/body evidence and final coherent new tuple/Story acceptance remain pending. Four actual loaded-assembly identities are observations, not independently retained DLLs. No candidate/lifecycle qualification is claimed.


Consolidated completed mutable handoffs without changing the original approved prefix/baselines/tasks/frozen block: [complete83,546-byte archive](release-phase/remote-resumption-20261103/implementation-history-through-20261104-085300.md),SHAfc16db28c0d006627bce31cf573d033279ee8011927d344031f802a25598acbd; [archive receipt](release-phase/remote-resumption-20261103/spec-history-archive-20261104-085300.json). Current state: Bnormalpublisher41798378661active; successful E/Tenvironment/cleanup with oldhelperreportgap; Platform5ac/P26658local proposals preserved for genuine tuple adoption. [Independent Parties localmerge audit](release-phase/remote-resumption-20261103/parent-parties-26658-incoming-source-and-control-audit.json) verifies61evidencefiles,243incoming/22protectedobjects,alloriginalmatrix/ledgerhistory,69+21actualTRXpasses. No finalnative/lifecycle/candidateacceptance.


EventStore PR392 reconciled newmainaeec and pushed04d082ab normally after independent source/63-file/363-TRX audit: [parent review](release-phase/remote-resumption-20261103/parent-eventstore-04d-source-and-control-audit.json), [normalpush](release-phase/remote-resumption-20261103/parent-eventstore-04d-push-response.json), [reviewed finalPRmetadata](release-phase/remote-resumption-20261103/parent-eventstore-04d-pr-update-response.json), [initial exact-head CI41808430947](release-phase/remote-resumption-20261103/parent-eventstore-04d-initial-hosted-api.json). All incomingauth/routing/E2Esource retained. [Independent executor/runtime decision](release-phase/remote-resumption-20261103/parent-eventstore-independent-executor-source-audit.json) and [reference cohort rationale](release-phase/remote-resumption-20261103/parent-reference-runtime-cohort-decision.json) preserve genuineETruntime4.44.0/6249/Platform1.2.3with reviewedexecutorAE; fullPartiesstillrequiresgenuineM23publication. Fresh fullhosted/nativeTRX/lifecycle/cleanup gates pending; no finalacceptance.


Genuine Builds4.44.2/sourceae68 published by successful normalRelease41798378661: [source/tag API](release-phase/remote-resumption-20261103/parent-builds-4442-tag-api.json), [actual public/source/producer receipts](release-phase/remote-resumption-20261103/parent-builds-4442-release-api.json), [independent bothsignedCLI/unsignedreconstruction/55payload/296fixture audit](release-phase/remote-resumption-20261103/parent-builds-4442-official-audit.json), [exact qualified tuple binding](release-phase/remote-resumption-20261103/parent-builds-4442-qualified-tuple-binding.json). Both actual publicCLIs execute4.44.2+ae/offlinesignature0; inventoryeligible/fullsource+controlspass. Module signedarchiveeded95db31ca23e41fe7ba630c823224833755e1970e26ff4cd8f3482ba69dbf, embeddedTooling99e7345682f7a066185825f899f5ce902745a6804f5b0a9923e9fce0a6adfe61, inventory5922e0ada052d0c7c74a875dccb42070672b3d34e8bba2a3fa1e64c5b7c782ff. Existing Platform5acproposal is now tasked with coherent genuine tuple/M23provenance and unconditional fullentry/reader/normal requiredprofile execution; no reference native/lifecycle/candidateacceptance follows from producer publication.

2026-11-04 incoming-main retention check: EventStore main `aeec5012fa3e1e07426a8fdd145709bce93f619a`, [CI41805685299](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/41805685299), passed20isolatedprojects16224cases (49NotExecuted), Contracts1528cases (2NotExecuted), source-mode7cases and the2actualgateway/persistence nativecases. Corrected source-executorae retained both exact referenced lifecycle/native bodies and all10ownedresources completed cleanup with empty remaining inventories. [Compact parent audit](release-phase/remote-resumption-20261103/parent-eventstore-aeec-hosted-summary.json) avoids double counting the native TRX duplicated in the blocking archive. This incoming-source observation does not replace the proposed04d run or remaining Parties/manual lifecycle gates.

2026-11-04 exact proposed EventStore `04d082abcf62c893c2bccf3c462ffbfbb7b46387`: [CI41808430947](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/41808430947) passed all6jobs. Actual tested merge971has the same tree as04d. Parent authenticated20isolatedreports16224passes/49NotExecuted, Contracts1528passes/2NotExecuted, Tsource7passes and **9/9nativepasses0skip**, including2realgateway/Redisend-state/anonymous401 facts. Both exact lifecycle/native diagnostic bodies are now retained and hash correctly. All10resources ready and exactcleanupcomplete, everyremaininginventoryempty with no discovery uncertainty. [Parent audit](release-phase/remote-resumption-20261103/parent-eventstore-04d-hosted-summary.json) records immutable outcome/time, actualsource/tool/executorroles and nativeassembly-observation limitations. PR392stillawaits separate requiredlive-sidecar before normalmerge; full Parties/manual lifecycle/Story acceptance remains pending.

2026-11-04 genuine [Platformv1.3.0](https://github.com/Hexalith/Hexalith.Platform/releases/tag/v1.3.0), release433814405/source338a6afc31850761f49c0de7d399266dd15cfe84, published11:10:27Z. Actual fresh-main [tuple41817311075](https://github.com/Hexalith/Hexalith.Platform/actions/runs/41817311075) passed271cases0skip and parent verified19,425Git source hashes/317signedarchive members/315retained bodies/fullreader12publichashes. Actual main [SDK41816978023](https://github.com/Hexalith/Hexalith.Platform/actions/runs/41816978023) passed60cases/208gates; actual retained49bodies perSDK verified,307absentmembers explicit. [Public identity receipt](release-phase/remote-resumption-20261103/parent-platform-130-official-identity.json) authenticates real tag/release/source; no native/candidate/Story acceptance. Existing Parties26658proposal is now assigned coherent genuine caller adoption.

2026-11-04 EventStore PR392 merged asdbc6e7e08d605ba6c6c790e3e08a15dc8bbdec8a; fresh-main CI41815244205 succeeded, including9actualnativepasses and exact10-resource cleanup with zero owned leftovers. [Merged-source native/retention audit](release-phase/remote-resumption-20261103/parent-eventstore-dbc6-hosted-summary.json), [additional merged-source owner artifact audit](release-phase/remote-resumption-20261103/parent-eventstore-dbc6-additional-owner-artifact-audit.json):444publishedcompatibility/318live-sidecar/1parallelization passes,4explicitworkerNotExecuted. This completes Ereference evidence; Parties/manual gates remain pending.

Parties PR61 adopted genuine4.44.2/AE/Platform1.3.0-338 atb7dcd84e5ebdc79f975c3a645727ddf89f199d76: [complete19-path source and56-file evidence audit](release-phase/remote-resumption-20261103/parent-parties-b7-complete-source-and-control-audit.json), [actual first CI41821031463 failure artifact audit](release-phase/remote-resumption-20261103/parent-parties-b7-failure-artifact-audit.json). All15isolatedprojects ran:6,281passes/1compiledMemoriesrefresh-capability failure/14NotExecuted. UI61passed/7failed, all21attempt errors in stale exact4.7.5guard against selected4.7.6. Strict workflow refused provisioning; actual cleanupnot-allocated provides no native acceptance. Minimal exact-version owning selector/guard corrections are assigned locally; original assertions/first outcomes are retained. Actual complete changed-source CI/native/manual lifecycle remains required.


Current continuation (2026-11-06 03:00UTC): current M494885 isolated271 passed and all five hosted SDK rows passed; original same-code L6c349 and hosted tuple42152948110 remain in progress. See [independent local271 audit](release-phase/remote-resumption-20261103/parent-platform4948-current271-local-independent-audit-20261106.json), [exact ordinary feature/dispatch receipt](release-phase/remote-resumption-20261103/parent-platform4948-ordinary-feature-and-hosted-dispatch-20261106.json) and [SDK75/236/14 audit](release-phase/remote-resumption-20261103/parent-platform4948-sdk-catalog-hosted-audit-20261106.json). Actual incoming public Platform1.5.2/e7e and its distinct259/349/catalog5 results are independently authenticated. Newer Parties ebb30 is preserved; [its first normal hosted failure](release-phase/remote-resumption-20261103/parent-parties-ebb30-first-hosted-outcome-and-cleanup-20261106.json) retains6364isolatedpasses/14oldNotExecuted, startup failure BEFORE600sdeadline, native0 and exact zero-owned-leftover cleanup. Underlying cause remains unproved. Source review/publication, final public-Parties/EventStore native and all three sequential manual lifecycle runs remain pending; candidate acceptance remains false.


Latest checkpoint (2026-11-06 03:14UTC): the first immutable M494885/L6c pair has actual independently authenticated local271+349 passes and all five hosted SDK rows75/236/14. Its [first hosted tuple failure](release-phase/remote-resumption-20261103/parent-platform4948-first-hosted-handoff-timeout-audit-20261106.json) is retained:268passes/two full-validation fixture45second timeouts/Preparation1unrun/dependentlegacy skipped. The same sequential agent is sealing this checkpoint before a separate fixture correction/new truthfulL-M pair; the correction uses only the remaining existing absolute600s plan deadline and retains early45, every assertion and all production budgets. No unchanged rerun, PR/main/publication/policy mutation or candidate acceptance. A genuine PR-author/CODEOWNER review path is pending. Newer Parties ebb30 is preserved with [all68single-attempt browser checks](release-phase/remote-resumption-20261103/parent-parties-ebb30-ui68-single-attempt-hosted-audit-20261106.json) and [20actual exactMemory25 build-policy cases/60retained evaluations](release-phase/remote-resumption-20261103/parent-parties-ebb30-exact-memory25-refresh-capability-hosted-audit-20261106.json); its startup failure still has no proved underlying cause or native completion.


Latest continuation (2026-11-06 07:10UTC): genuine public Builds4.44.8/source26b9 is independently authenticated: [signed producer and tuple binding](release-phase/remote-resumption-20261103/parent-builds4448-actual-qualified-producer-and-tuple-binding-20261106.json). New PlatformMe810/Lb65 normally retains both reviewed ec282 safeguards and newer public154/13fc gateway work: [complete source/history/metadata/message audit](release-phase/remote-resumption-20261103/parent-platform-e810-complete-proposal-real-pair-and-full-messages-audit-20261106.json). The distinct feature branch has active [tuple42178949195](https://github.com/Hexalith/Hexalith.Platform/actions/runs/42178949195) and [catalog5/42178951812](https://github.com/Hexalith/Hexalith.Platform/actions/runs/42178951812); focused37 passed locally, first full-current missing-official-tag refusal is preserved and a separately recorded corrected-preparation run is active. Newer Partiesdfcf is retained with [first hosted outcome audit](release-phase/remote-resumption-20261103/parent-parties-dfcf-first-hosted-outcome-finite-status-and-cleanup-20261106.json):6364isolatedpasses/14oldNotExecuted, native0, finite owner-preparation InvalidOperationException before600second deadline and complete zero-owned-leftover cleanup. Its throwing guard remains unproved. Actual Platform review/publication, final publicParties/cohort/native28+9 and all three sequential manual lifecycle gates remain open; candidate/Story acceptance is false.


Closed local checkpoint (2026-11-06 07:20UTC): the newer scoped gateway and genuine4448 proposal now has independently verified focused37/current271/original349 passes, exact original cases/source/public-package/output identities, and its preserved first missing-tag preparation failure. [Independent complete checkpoint audit](release-phase/remote-resumption-20261103/parent-platform-scoped-gateway-closed-checkpoint-independent-audit-20261106.json) rehashed79,324files/3,921,425,603bytes, all6declared links and27anchors, with exact complete nonGit file census. [Current271](release-phase/remote-resumption-20261103/parent-platform-scoped-gateway-current-local-independent-audit-20261106.json), [original349](release-phase/remote-resumption-20261103/parent-platform-scoped-gateway-legacy-local-independent-audit-20261106.json), [focused37](release-phase/remote-resumption-20261103/parent-platform-e810-focused37-independent-audit-20261106.json) and [hosted SDK75/236/14](release-phase/remote-resumption-20261103/parent-platform-e810-sdk-catalog-hosted-audit-20261106.json) remain separate from pending hosted tuple42178949195. All earlier roots are preserved; the implementation agent is idle. Actual contributor-author/CODEOWNER review, final public producers/cohort, native28+9, three manual lifecycle scenarios and original frozen acceptance gates remain open.


Verified hosted completion (2026-11-06 07:39UTC): [exact Me810/Lb65 tuple42178949195](https://github.com/Hexalith/Hexalith.Platform/actions/runs/42178949195) passed both current271 and original349; [independent complete hosted audit](release-phase/remote-resumption-20261103/parent-platform-e810-current271-and-original349-feature-hosted-audit-20261106.json) verifies every21953trackedGitblob input perlane, exact cases/profiles, signed4448/4443 archives and original8metadata boundary. All5SDK75/236/14 and local37/271/349 are independently verified. Watcher9092ended0; no watcher remainsactive. [Concrete review handoff](release-phase/remote-resumption-20261103/parent-platform-e810-review-handoff-20261106.md) and [remaining blocker/gates](release-phase/remote-resumption-20261103/parent-story28-e810-verified-handoff-and-required-contributor-blocker-20261106.json): a real contributor must author the PR so authenticatedjpiquot can supply the required CODEOWNER review. No PR/main/policy/bypass action occurred; final publication, public Parties/cohort, native28+9 and all three sequential manual lifecycle scenarios remain pending. Story2.8 stays in progress at Step03.


User stop/save (2026-11-06): work is stopped, the single implementation agent is idle, and no owned local process or monitor remains active. [Resume handoff](../../story-2-8-session-handoff-20261106-0749.md) records verified e810 local/hosted results, the user's explicit existing review-bypass authorization, newer incomingd068, and the clean preparation-only readiness-throttle root. No merge, source edit, test or new source pair started in that new root. Hosted runs were untouched. Resume only when the user asks; Story2.8 remains in progress at Step03.
