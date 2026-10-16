---
title: EXT-SECRETS-1 exact-target runtime delivery and qualification
type: feature
created: 2026-10-16
status: in-progress
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
- [x] Runtime `DaprCapabilityAdapters.cs` — require fresh request-bound typed absence; bind complete revocation/revisions; allow valid empty-batch blocks; supply canonical signing payload.
- [x] Runtime `DaprInventoryAuthorityAdapter.cs`, `DaprDecisionAuthorityAdapter.cs` — require fresh affirmative authorization, exact provider/anchor/inventory, transition bytes and complete final manifest/cohort/profile.
- [x] Runtime `SecretsDapr.cs`, `SecretsProof.cs` — bound streaming responses within existing deadlines.
- [x] Qualification projects — prepare enrolled execution/lookup, independent persisted-owner observations and isolation enrollment; deny until real providers/storage/signers/scopes exist. Never derive proof from expected outcomes.
- [x] `eng/ext-secrets-1/provisioning/` — add unapplied role-specific workload/storage/credential, Dapr ACL/isolation artifacts and owner-input map; refresh census.
- [x] `eng/ext-secrets-1/prepare_acceptance.py`, `qualify.py`, packet/README — detach packet after candidate commit; bind real PEM hashes to authoritative RuntimeQualificationBinding/configuration/plan; generate exact command and blocked-input report.
- [x] Runtime tests, `eng/ext-secrets-1/test_*.py`, verification script — regress all authority/absence/substitution/oversize failures; retain custody/XML audit and add runtime/host coverage.
- [ ] Spec evidence — record results, candidate and remaining inputs; qualify and continue downstream only after prerequisite gates pass.

**Acceptance Criteria:**
- Given denial/staleness/incomplete identity/unbound absence, when adapters run, then no effect occurs.
- Given changed bytes/cohort/profile/revocation, when checked, then substitution fails; exact-original recovery survives.
- Given missing installed authorities, when preparing/verifying, then live stays blocked and register/status unchanged.
- Given accepted target and independent enrollment, when qualifying, then every case proves persisted end-state, isolation, revocation, rotation, restart, restore and lost-result recovery before availability acceptance.

## Implementation Notes

2026-10-16 source delivery:

- Runtime effects require fresh affirmative typed request-bound absence. Inventory authorization binds provider/anchor identity and exact transition bytes; decision checks bind the complete canonical manifest, cohort and published profile. Revocation binds every envelope field/revision. Signing receives the shared canonical payload and retains original receipt identity. Empty affected-batch lists are valid key-block receipts. Proof/request streams are capped at 1 MiB within the existing call budget.
- Qualification hosts expose separate executor/observer/enrollment provider ports, an independent durable original-store port and accepted ES256 proof verification. Missing implementations deny with named inputs. Originals are retained before execution; repeat or uncertain execution uses lookup; client expected outcomes are excluded from provider invocation. The observer identifier index uses explicit persistent storage. These ports and templates do not constitute installed owner readers, signers, storage, scope authorization or isolated enrollment.
- Unapplied role-specific workloads, storage/credential references, default-deny Dapr ACL/isolation templates and an owner-input map are under `eng/ext-secrets-1/provisioning/`. The census preserves the original host facts and records no new cluster query.
- The source packet is now a template. `prepare_acceptance.py` writes a detached v2 candidate packet after a clean source commit, hashing actual P-256 PEM bytes and copying the exact public configuration. Live preflight compares packet pins with the authoritative RuntimeQualificationBinding, actual PEMs, complete plan, accepted target and exact owner command. No self-identifying tracked packet is required.

Source verification passed using EventStore source at `/home/administrator/projects/hexalith/eventstore` and fresh paths:

- `eng/verify-ext-secrets-1.ps1 -Mode Local`: 709/709 custody tests and 53/53 runtime/host tests; warning-free builds; XML audits reported zero errors, failures, skips or unrun cases. Evidence: `/tmp/ext-secrets-final-local-20261016-evidence/local-evidence.json`, `local-tests.xml`, `runtime-tests.xml` and `python-tests.log`.
- Follow-up runtime-only verification after separating profile validity from proof freshness: 54/54 tests passed with zero warnings/errors; `/tmp/ext-secrets-runtime-tests-final.xml` and `/tmp/ext-secrets-runtime-tests-final.log`. The 709-test custody evidence remains unchanged.
- Python discovery: 56/56 tests passed, including detached-candidate/real-anchor/substitution/dirty-checkout and zero-effect denials.
- Real-checkout preparation with the example references exited 2, produced only `/tmp/ext-secrets-preparation-blocked-20261016/blocked-inputs.json` and reported zero effects. The dirty checkout and missing installed identities/anchors correctly prevent a candidate packet here.

Candidate commit and detached final packet remain pending the clean candidate checkout. Owner acceptance of the exact candidate/configuration/plan/anchors/command, independently installed providers, role-specific signing/storage/caller scopes, complete all-copy/nonrollback authorities and isolated enrollment remain external inputs. No live effect, 234-case live qualification, availability acceptance, register/status change or downstream advancement has occurred. The final execution task remains open for those gates.

## Spec Change Log

## Review Triage Log

## Design Notes

No intent choices remain. Missing identities are provisioning dependencies. Source/candidate commit are reversible; live effects stay gated. The packet cannot belong to the commit it identifies.

## Verification

- Run `eng/verify-ext-secrets-1.ps1 -Mode Local` with fresh paths and EventStore source; require warning-free builds and complete passing XML.
- Run Python `test_*.py` and runtime/host regressions; verify zero-effect acceptance/enrollment denials.
- Retain exact-target qualification evidence and original journals; missing gates block downstream advancement.
