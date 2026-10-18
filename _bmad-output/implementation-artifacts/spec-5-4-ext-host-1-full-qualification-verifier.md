---
title: EXT-HOST-1 full qualification verifier for Agents Story 5.4
type: feature
created: 2026-10-18
status: in-progress
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
- [ ] `eng/verify-agents-host.sh` — pinned `--profile` for Full, and an honest closing line.
- [ ] `eng/ext-host-1/verify_full.py` — named-cell qualification, fail-closed gaps, prior-evidence snapshot, one complete attempt.
- [ ] `eng/ext-host-1/test_verify_full.py` — matrix rows except the packet row.
- [ ] `eng/ext-host-1/prepare_acceptance.py` and `test_prepare_acceptance.py` — command pins `--profile`; nothing is accepted.
- [ ] `docs/ext-host-1-agents-composition.md` — document the component path and the live gate only.

**Acceptance Criteria:**
- Given Full without a profile, when the script runs, then it exits 1 with `DependencyNotAvailable: EXT-HOST-1` and no live effect.
- Given a complete profile and every required Passed name, when Full finishes, then it exits 0, records the cells, and leaves `dependencyAvailable` false.
- Given a second complete target in the same artifacts root, when Full runs, then the first attempt remains and suites do not start.
- Given a clean temporary candidate, when preparation finishes, then the command pins HEAD, `--profile`, and the detached profile, and `acceptedReplacementCommit` is null.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `python3 -m unittest test_verify_full.py test_prepare_acceptance.py` from `eng/ext-host-1` — expected: existing refusal and packet tests still pass with the new rows.
- `bash eng/verify-agents-host.sh --profile` from the repo root — expected: exit 1 before pin tests.
