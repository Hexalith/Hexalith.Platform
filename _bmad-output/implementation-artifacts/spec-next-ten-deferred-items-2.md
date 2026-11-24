---
title: 'Next ten Platform deferred items, second pass'
type: 'bugfix'
created: '2026-11-24'
status: 'done'
route: 'dispatch'
baseline_commit: 'eb4d5480daefba123d5452a8c9f6002bd17a896c'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete ten actionable items from the Platform deferred-work ledger, in listed order. Give each completion its own commit and record the reason for each skipped item. Verify the full build and repository tests, resolve failures, and push directly to main.

## Boundaries & Constraints

Prioritize the root ledger. Skip entries whose work already shipped and entries requiring protected inputs, approved authority, or a separate live action. Leave blocked entries open. Keep ad hoc job workspaces under the private TMPDIR from `run-with-temp` and retain useful logs outside it. The work is deliberately a batch of independently verifiable changes.

</frozen-after-approval>

## Code Map

- Story 4.28 and the canonical planning epics duplicate its numbered criteria. The Rancher operations handoff supplies both approved source decisions.
- `src/Hexalith.Platform.Identity/PlatformActorRegistry.cs` and its enrollment caller own registry proof issuance. The configured registry service identity is the proof source. `PlatformIdentityAdmissionTests.cs` has a reusable actor proxy and an overly permissive bootstrap verifier substitute.
- `eng/cluster-management/test_rehearse.py` and `test_review_loop5.py` have refusal fixtures for the uncovered G166 branches. The prior line anchors resolve at `614b330e`. The tuple preflight can run the existing offline discovery command with PyYAML.
- `src/Hexalith.Platform.Custody/PlatformHmacKeySnapshot.cs` computes tags for three custody authenticators. `PlatformKeyResolution.Check` owns scope, revocation, issue/verify distinction, retention overlap and expiry; use the same evaluation instant at the tag boundary. The Custody test assembly has internal access.
- `src/Hexalith.Platform.Composition/PlatformApplicationGraph.cs` builds the Platform model before resource creation. The pinned Builds tooling validates shape and selects the declaration closure. Composition-level names and resource consistency belong before binding selected resources.

## Tasks & Acceptance

- [x] `_bmad-output/implementation-artifacts/4-28-deploy-rancher-and-register-the-existing-cluster.md` and `../planning-artifacts/epics.md` — record numbered certificate, jpiquot removal and local-admin outage obligations (G135), then align administrator and deputy policy (G136).
- [x] `src/Hexalith.Platform.Identity/PlatformActorRegistry.cs` — restrict mutation and verify proof source (G137). `tests/Hexalith.Platform.Identity.Tests/PlatformIdentityAdmissionTests.cs` — verify a real bootstrap signature against an unchanged and a changed mutation (G140).
- [x] `eng/cluster-management/test_rehearse.py` and `test_review_loop5.py` — cover the twelve historical guard refusals (G166). `.github/workflows/tuple-ci.yml` — add the blocking offline suite (G167).
- [x] `src/Hexalith.Platform.Custody/PlatformHmacKeySnapshot.cs` and the three custody callers — enforce revocation and then key validity at tag use in separate commits with direct tests.
- [x] `src/Hexalith.Platform.Composition/` — enforce names within the task, hook, interface and operation collections, then compare declared resource requests and limits in a separate item. Keep these checks in composition and exercise its normal entry path.
- [x] `deferred-work.md` — record each completion in its own commit and document skipped items encountered along the way.

- Given a ledger with superseded and blocked items, when the batch finishes, then exactly ten newly completed entries have resolutions and separate commits.
- Given the committed batch, when the Release solution and repository tests run, then all executable tests pass or remaining environmental dependencies are identified with their exact effects.
- Given successful verification, when main is pushed, then the remote contains the completed commits.

## Implementation Notes

The five existing blocked rows have prior reasons. G138 and G143 already shipped in 2ac8e73c09fe631c5620723b54784d2e28e8941f with focused tests. G139 requires an approved production caller, route and human/workload identity handoff. The current EventStore Dapr scheme supplies eventstore:workload instead of the legacy claim expected by Platform and rejects combined credentials outside its bound command. The Custody integration row is obsolete because the solution already includes the populated test and source projects. The recorded October 8 EventStore GC confirmation supersedes the next-nightly item. The intervening 4.2 qualifications require protected records, an approved policy, independent network observation or live access changes. LP-F1 and LP-F2 repeat the existing protected-record entries. LP-F3 has aggregate byte verification but needs the referenced per-object records. Composition symbolic references span recovery and critical-flow scopes still owned by their specific execution contracts. The authoritative-restore hook row needs a decision about native/operator recovery.

Use a case-sensitive identity set per module and collection, with operation names scoped per interface. For cpu and memoryMiB, compare only matching request/limit pairs and refuse a request above its limit. Use exact comparison of JSON numeric forms so equivalent or distinct high-precision values are handled without silently rounding; this does not choose provisioning representations for the separate conversion item.

Complete and commit each item before advancing to the next. The parent performs final suite verification and the main push. Do not operate on production services.

## Item Commits

| Ledger item | Original reference | Commit |
| --- | --- | --- |
| DW-1 | G135 | `bf37588d` |
| DW-2 | G136 | `0b4c7411` |
| DW-3 | G137 | `61554ca0` |
| DW-5 | G140 | `bb580b33` |
| DW-7 | G166 | `51e96f8d` |
| DW-8 | G167 | `81a7bbfd` |
| DW-10 | 4.2 Corrected Blind 10 and Corrected Edge 4 | `a2f65b85` |
| DW-11 | 4.2 Corrected Blind 11 | `cee3fe68` |
| DW-13 | BH03 | `290eb159` |
| DW-14 | BH09/EH06 | `d6271090` |

## Review Triage Log

| Finding | Verdict and action |
| --- | --- |
| BMAD ignores statuses inside legacy flat blocks. | Medium, patch. Encode this batch's ten completions and four supersessions as canonical closed entries with original evidence. The installed parser confirmed their closure. |
| Other components can resolve the shared registry signing service through DI. | Low, pre-existing. The configured key stays inside the trusted host DI boundary. The checked enrollment service is the only production mutation call and the facade no longer exposes a public mutation method. |
| The public read facade's source argument does not influence its proof. | Low, pre-existing. The read proof deliberately uses the registry capability identity. Admission readers establish their own caller authorization before reading. |
| The last asynchronous private-owner grant or profile read can outlive the key window. | Medium, patch. Recheck the confirmed key when releasing authorization and exercise expiry during that read. Two reviewers found the same boundary. |
| Independent custody of the sealed Rancher account could introduce a second administrator. | False. AC4 requires sole Administrator authority for both local-account choices, and AC13 requires an approved amendment for the sealed choice. |
| The older migration spec still mandates deputy recovery. | Medium, patch. Name the controlling October 3 decision in the 4.28 handoff and criteria. |
| Isolated restore could reconnect without validating current grant and revocation lineage. | False for the combined contract. The governing Rancher runbook already requires a complete verified current signed lineage and denies reconnection on missing, conflicting or gapped records. |
| Five retained archive objects could be left terminating after cleanup. | Medium, patch. Require approved finalizer handling and native post-removal readback in the numbered criterion. |
| The revocation exercise could remove the new binding before retiring the prior path. | Medium, patch. Verify the final approved binding is live before cleanup. |
| Signed-retirement tests can skip when ssh-keygen is missing. | Medium, patch. Verify the executable before discovery in CI. |
| The existing automatic tuple filters omit new Composition cases. | Medium, patch. Add focused execution without changing the existing exact suite counts. Two review layers identified the omission. |
| The existing automatic tuple filters omit the direct HMAC snapshot cases. | Medium, patch. Run the direct lifecycle and affected caller tests in CI. |
| Duplicate-name negatives use identical objects. | Medium, patch. Keep each name and change another valid field to prove identity-based rejection. |
| Private-owner tests do not verify rejection versus valid retained-key authorization after rotation. | Medium, patch. Issue, rotate, verify within overlap and confirm the caller uses verification mode. |

## Verification

- The Release solution built with the authenticated, pinned Builds 6.7.0 tool, with zero errors and 11 existing Aspire obsolete API warnings. The verified source is `ccf11d13`. The subsequent merge from main changes planning and implementation records only.
- All 12 solution test projects ran. The aggregated xUnit results, including the standalone fixture observer against a local loopback fixture, contain 3,863 passing cases. Twenty-one conditional cases and one explicit case require opt-in live harnesses or hosted configuration.
- Composition covered all 401 discovered methods across 997 cases. Eighty-three unaffected methods retained their passing or default-skipped results from the item commit. All remaining methods ran against the reviewed code. Four methods affected by shared build-directory contention passed in a clean serial replay. The hosted source mapping tests used the approved `759bb897` checkout and passed all 23 cases.
- All 38 repository check groups, 311 offline cluster-management cases, 23 Keycloak Maven cases, and 33 Custody Dapr source-component cases passed. The standalone fixture compile and loopback observer check cover the native entry from source.
- The ledger contains ten new completions, four superseded entries and the reasons for retaining 25 blocked entries. It has 505 open items at the verified main merge.
