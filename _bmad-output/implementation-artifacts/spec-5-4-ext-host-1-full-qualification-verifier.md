---
title: EXT-HOST-1 full qualification verifier for Agents Story 5.4
type: feature
created: 2026-10-18
status: done
route: dispatch
review_loop_iteration: 0
baseline_commit: 5f18593639832d1245577dd93ec07687517d3acb
context:
  - docs/ext-host-1-agents-composition.md
  - /home/administrator/projects/hexalith/agents/_bmad-output/planning-artifacts/external-dependency-register.md
  - /home/administrator/projects/hexalith/agents/_bmad-output/implementation-artifacts/tests/story-5-4-owner-qualification-2026-10-18/README.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Full has no pinned `--profile` and `verify_full.py` returns 1 on every path, so OQ-5.4-1 has no host pass path. The 2026-10-18 run only recorded unit suites and four empty bindings.

**Approach:** Forward `--profile` to Full. Pass only when that profile is complete and the named custody and guard cells passed. Missing or secret profiles keep today's refusal. Exit 0 is component qualification only. Live, the cohort, and Available stay gated. The v2 packet pins the command and profile; `acceptedReplacementCommit` stays null. This session does not commit or seal a packet on the dirty tree.

## Boundaries & Constraints

**Always:** Keep `REFUSAL`, `BINDING_GAPS`, `binding_gaps`, `run_suite`, and both suite filters. A complete profile is one `missing_references` accepts with no secret. Required Passed names: `RoutingUsesAuthenticatedOriginAndRetainedReceiptDigest`, `ExactLookupDoesNotMutateAndRequiresCurrentPrivateAuthority`, `DurableFairDrainRetainsUnknownAndAdvancesOtherTenantAfterRestart`, `UnavailableSpoolOrOriginalKeyNeverReportsProcessedDenial`, `OriginalObservationIsDurableAndStableAcrossRetryAndRestart`, `SameRevisionDifferentStateCannotPassRestoreProof`, `LostRecorderAcknowledgementRecoversExactSourceBeforeSpoolAck`, `LostAcknowledgementAndRetryReuseStoredFirstSeenWithoutRenewal`, `StartupEveryReadinessAndContentAdmissionRunFreshCanaries`, `AcceptedAttributionAndInstalledFencesAreJointWithEverySourceWrite`, `HoldAndSealSerializeAtConditionalEffectWithExactStaleLookup`, `PostSealAdmissionCannotBecomeContentContainmentOrSuccessfulCompletion`, `CompromiseAndReplacementRemainExactSameBatchWithoutClearingGlobalBlock`. Copy existing `evidence/` to `evidence/prior/<sha256>/` before writing; never replace that digest. The attempt key is SHA-256 of HEAD, canonical profile, and plan. The same key returns the stored code and skips suites. A different complete key exits 1 and keeps the first attempt. Incomplete-profile refusals may repeat. `effects` and `liveEffects` stay 0. `dependencyAvailable` and `productionEngineRegistered` stay false. Every result lists `missingRuntimeInputs`: `EXT-PROTECTION-1`, `trust-anchors`, `OQ-5.4-1`.

**Never:** Edit the Agents register, 2026-10-18 evidence, or Story 1.10 files. Run Live, cohort phases, or `/tmp/ext-host-1-full`. Mark Available. Accept keyword matches as coverage. Echo secrets. Weaken no-profile refusals in `VerifyFullTests` or `verify_works_host.py:249`. Install an engine or mint credentials.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Profile absent | Full, no `--profile` | Exit 1, current `REFUSAL`, four gaps, zero effects | Suite success is not a pass |
| Bad profile | Pending, incomplete, non-object, or secret | Exit 1 before suites | Secret text absent from stderr |
| Cells passed | Complete profile; every required name Passed | Exit 0; `componentQualification` true; live flags false | No `REFUSAL`; not Available |
| Cell or suite missing | Complete profile; failed or unnamed cell | Exit 1; `coverageGaps` names it; zero effects | Prior evidence kept |
| Same target | Same HEAD, profile, and plan | Stored code; suites not started | First attempt unchanged |
| Other target | Different complete profile; attempt exists | Exit 1; first attempt kept | No second suite |
| Packet | Clean temporary checkout | Command pins HEAD, `--profile`, detached profile path | `acceptedReplacementCommit` null |
| Parser | `--profile` missing a path, or unknown flag | Exit 1 before pins and dotnet | No-profile Full still refuses |

</frozen-after-approval>

## Code Map

- `eng/verify-agents-host.sh:12-51` — parse and forward `--profile` on Full. LocalScaffold still Debug-builds `apphost.cs`. A component pass is neither unavailable nor Available.
- `eng/ext-host-1/verify_full.py:10-127` — replace unconditional `return 1`. Add lost-ack evidence and the required-name gate. No profile still matches `verify_works_host.py:249`.
- `eng/ext-host-1/prepare_acceptance.py:136-138` — pin `--profile` and detached `agents-deployment-profile.json` in `exactCommand`. Keep schema v2, null acceptance, `missing_references`, and `digest`.
- `eng/ext-host-1/test_verify_full.py`, `test_prepare_acceptance.py` — keep current assertions and add the matrix rows, including the parser probe.
- `AgentsDeploymentProfile.cs:18-33` — same keys and constants. Do not change composition or the custody and guard tests.

## Tasks & Acceptance

**Execution:**
- [x] `eng/verify-agents-host.sh` — pinned `--profile` for Full, and an honest closing line.
- [x] `eng/ext-host-1/verify_full.py` — named-cell qualification, fail-closed gaps, prior-evidence snapshot, one complete attempt.
- [x] `eng/ext-host-1/test_verify_full.py` — matrix rows except the packet row.
- [x] `eng/ext-host-1/prepare_acceptance.py` and `test_prepare_acceptance.py` — command pins `--profile`; nothing is accepted.
- [x] `docs/ext-host-1-agents-composition.md` — document the component path and the live gate only.

**Acceptance Criteria:**
- Given Full without a profile, when the script runs, then it exits 1 with `DependencyNotAvailable: EXT-HOST-1` and no live effect.
- Given a complete profile and every required Passed name, when Full finishes, then it exits 0, records the cells, and leaves `dependencyAvailable` false.
- Given a second complete target in the same artifacts root, when Full runs, then the first attempt remains and suites do not start.
- Given a clean temporary candidate, when preparation finishes, then the command pins HEAD, `--profile`, and the detached profile, and `acceptedReplacementCommit` is null.

## Implementation Notes

Full forwards `--profile` and exits 0 only for a complete profile whose thirteen required cells passed. No-profile and bad-profile paths still print `DependencyNotAvailable: EXT-HOST-1` with zero effects. Prior evidence is snapshotted by content digest, and one complete attempt is stored per artifacts root. The packet command pins HEAD, `--profile`, and the detached profile path; `acceptedReplacementCommit` stays null. No packet was sealed on this dirty tree, and `/tmp/ext-host-1-full` was not run. `missingRuntimeInputs` remains `EXT-PROTECTION-1`, `trust-anchors`, and `OQ-5.4-1`. Review patches lock mixed theory outcomes, a changed HEAD, and a later missing or pending profile, resolve relative Full paths before `cd`, and document the one-attempt artifacts directory.

## Spec Change Log

## Review Triage Log

| # | Verdict | Evidence |
| --- | --- | --- |
| BH1 | false | `verify` returns 1 and leaves the sealed attempt when a later profile is missing or incomplete. The spec keeps that first attempt; the four-gap refusal is the publish path used when no attempt exists. |
| BH2 | low | A corrupt `component-attempt.json` is treated as an empty key and blocks later runs. Rejected: everyday runs do not produce a torn file, and recovery would add branches. |
| BH3 | low | A crash between `publish` and `write_attempt` can omit the seal. Rejected: a retry then snapshots prior evidence and completes one attempt; an atomic protocol is extra machinery. |
| BH4 | false | Same-key replay returns the stored code without rereading `evidence/`, which is the specified skip. Exit 0 is that stored result. |
| BH5 | false | The attempt key is defined as HEAD, profile, and plan. Cleanliness is enforced by the packet command, not by adding worktree bytes to that key. |
| BH6 | medium | The default artifacts directory is stable, and the composition doc never says a later commit or profile needs a fresh `--artifacts-path`. |
| BH7 | medium | `--profile` and `--artifacts-path` are opened after `cd` to the repo root, so a relative path from another directory misses the caller's file. |
| BH8 | low | Failed lost-ack rows are omitted from `matchedTests` while `requiredCells` and `coverageGaps` still record the failure, so qualification does not pass. Rejected: the pass decision does not read `matchedTests`. |
| BH9 | false | An empty suite is reported as the thirteen missing cell names. That is what `coverageGaps` is specified to name, and stderr still says the suites failed. |
| BH10 | defer | `README.md` still says the default host script expects a successful Release AppHost build. Full already exited before any build, and this change does not own that section. |
| BH11 | false | The run-id and `sha256sum` wording is Story 1.10 README text. This intent forbids editing those files. |
| BH12 | false | The Builds formatting note and unit-test totals are Story 1.10 evidence. This intent forbids editing those files. |
| BH13 | low | An existing prior digest directory, including a partial crash copy, is left in place. Rejected: the spec says never replace that digest, and crash repair adds a second writer. |
| BH14 | low | The code map still cites the pre-change line range, and the baseline diff also contains Story 1.10 files this session did not edit. Rejected: correcting the map means editing the spec, and the other files are excluded by intent. |
| BH15 | defer | The custody diagram's missing deletion edges are in the separate EXT-PROTECTION design that was already in the working tree. |
| EH1 | medium | Same relative-path resolution as BH7. Confirmed: `cd` is at `verify-agents-host.sh` before either path is passed to Python. |
| EH2 | low | LocalScaffold accepts `--profile` and ignores it. Rejected: the spec forwards the profile only for Full, and failing LocalScaffold would add a new refusal. |
| EH3 | false | Same as BH5. A dirty tree is not part of the specified attempt key. |
| EH4 | low | HEAD is read before the suites, so a commit during `dotnet test` is not folded into the key. Rejected: the profile value used for the key is the already loaded object, and re-reading after the suite adds a race branch. |
| EH5 | false | A stored non-zero code does not print the pass line. Replaying a stored 0 after evidence deletion is the specified same-key return. |
| EH6 | low | Same partial-snapshot case as BH13. Rejected for the same reason. |
| EH7 | low | Two concurrent runs can both pass the empty-attempt check. Rejected: the owner command is a single process, and a lock is new control flow. |
| EH8 | low | `write_bytes` follows a symlink that an operator already placed. Rejected: that is not how the artifacts directory is created, and symlink rejection is a new guard. |
| EH9 | defer | Missing `dotnet` still raises `FileNotFoundError` from the preserved `run_suite` helper. That helper predates this change. |
| EH10 | low | A file path passed as `--artifacts-path` makes `mkdir` raise. Rejected: callers pass a directory, and the check would be a new branch. |
| VG1 | medium | `cell_records` requires every row Passed, but tests supply one row per method. `RoutingUsesAuthenticatedOriginAndRetainedReceiptDigest` and `AcceptedAttributionAndInstalledFencesAreJointWithEverySourceWrite` are theories. A mixed Passed/Failed pair is untested. |
| VG2 | medium | The attempt key includes HEAD, but no test changes `git_head`. Removing `head` from the key would leave the current replay tests green. |
| VG3 | medium | After a sealed attempt, a missing or pending profile returns 1 without calling suites. No test covers that follow-up, so deleting the early returns would still pass. |

## Verification

**Commands:**
- `python3 -m unittest test_verify_full.py test_prepare_acceptance.py` from `eng/ext-host-1` — expected: existing refusal and packet tests still pass with the new rows.
- `bash eng/verify-agents-host.sh --profile` from the repo root — expected: exit 1 before pin tests.
