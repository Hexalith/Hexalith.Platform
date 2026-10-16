---
title: EXT-SECRETS-1 exact-target runtime delivery and qualification
type: feature
created: 2026-10-16
status: ready-for-dev
route: dispatch
review_loop_iteration: 0
baseline_commit: 114e08830da2d8cca0e77cd870b8867506b4c68b
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EXT-SECRETS-1 remains Committed. Adapters have authorization defects, qualification hosts always deny, and independent installed authorities remain unverified. The tracked packet requires its own commit hash, preventing acceptance.

**Approach:** Complete runtime/provisioning source, local verification and a detached immutable-candidate packet. Bind verified references. After acceptance and independent enrollment, qualify all 234 S1–S4/v23 cases, then follow Platform host → Parties → Conversations → Agents Story 5.4 with each dependency gate.

## Boundaries & Constraints

**Always:** Reuse custody/Dapr on `192.168.1.30`. Preserve 2026-10-08 integration, Branch B, 365-day history, named approvals, L/S/O/H/R=300/30/600/86400/604800 seconds, Open export disposition with zero effects, and original recovery journals. Decision private keys stay outside Platform/Agents. Record credential references only. Keep executor/observer/enrollment independent. Return missing provisioning inputs.

**Never:** Invent identities, qualification or availability; substitute fixtures/providers/databases; reuse operator/Memories credentials; change unrelated work; or execute live effects before accepted target/configuration/plan/anchors and independent isolated-target enrollment.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Required behavior | Failure |
| --- | --- | --- | --- |
| Authority unavailable | Missing, stale, denied, conflicting or cross-request proof | No new effect; identify missing authority | DependencyNotAvailable |
| Original uncertain | Signing, mutation or execution result lost | Persist identity; exact lookup only across restart/restore | Retain Unknown and journal |
| Candidate prepared | Clean immutable source plus installed references/public anchors | Detached packet pins exact commit, command and binding | Missing references block readiness |
| Target accepted | Independent enrollment and all exact pins verified | Complete isolated 234-case qualification with persisted end-state/ACL proofs | Any missing case blocks availability |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Secrets.Runtime/`: capability/inventory/decision adapters; empty absence, incomplete identities and echo-only authorization need correction.
- `src/Hexalith.Platform.Custody/`: reuse contracts, canonical capability codec, retained originals and private composition.
- `src/Hexalith.Platform.Secrets.Qualification.{Executor,Observer,Enrollment}/`: unconditional denials; observer storage is volatile.
- `eng/ext-secrets-1/{qualify.py,cases.py,contract-case-matrix.txt}`: preserve complete plan, proof validation and original-run journal.
- `eng/verify-ext-secrets-1.ps1`: currently verifies custody only; runtime tests live in `tests/Hexalith.Platform.Secrets.Runtime.Tests/`.

## Tasks & Acceptance

**Execution:**
- [ ] Runtime `DaprCapabilityAdapters.cs` — require fresh request-bound typed absence; bind complete revocation/revisions; allow valid empty-batch blocks; supply canonical signing payload.
- [ ] Runtime `DaprInventoryAuthorityAdapter.cs`, `DaprDecisionAuthorityAdapter.cs` — require fresh affirmative authorization, exact provider/anchor/inventory, transition bytes and complete final manifest/cohort/profile.
- [ ] Runtime `SecretsDapr.cs`, `SecretsProof.cs` — bound streaming responses within existing deadlines.
- [ ] Qualification projects — prepare enrolled execution/lookup, independent persisted-owner observations and isolation enrollment; deny until real providers/storage/signers/scopes exist. Never derive proof from expected outcomes.
- [ ] `eng/ext-secrets-1/provisioning/` — add unapplied role-specific workload/storage/credential, Dapr ACL/isolation artifacts and owner-input map; refresh census.
- [ ] `eng/ext-secrets-1/prepare_acceptance.py`, `qualify.py`, packet/README — detach packet after candidate commit; bind real PEM hashes to authoritative RuntimeQualificationBinding/configuration/plan; generate exact command and blocked-input report.
- [ ] Runtime tests, `eng/ext-secrets-1/test_*.py`, verification script — regress all authority/absence/substitution/oversize failures; retain custody/XML audit and add runtime/host coverage.
- [ ] Spec evidence — record results, candidate and remaining inputs; qualify and continue downstream only after prerequisite gates pass.

**Acceptance Criteria:**
- Given denial/staleness/incomplete identity/unbound absence, when adapters run, then no effect occurs.
- Given changed bytes/cohort/profile/revocation, when checked, then substitution fails; exact-original recovery survives.
- Given missing installed authorities, when preparing/verifying, then live stays blocked and register/status unchanged.
- Given accepted target and independent enrollment, when qualifying, then every case proves persisted end-state, isolation, revocation, rotation, restart, restore and lost-result recovery before availability acceptance.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

No intent choices remain. Missing identities are provisioning dependencies. Source/candidate commit are reversible; live effects stay gated. The packet cannot belong to the commit it identifies.

## Verification

- Run `eng/verify-ext-secrets-1.ps1 -Mode Local` with fresh paths and EventStore source; require warning-free builds and complete passing XML.
- Run Python `test_*.py` and runtime/host regressions; verify zero-effect acceptance/enrollment denials.
- Retain exact-target qualification evidence and original journals; missing gates block downstream advancement.
