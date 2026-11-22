---
id: SPEC-plat-es416-qh-q2-contract-implementation-r3
work_reference: PLAT-ES416-QH
revision: PLAT-ES416-QH-Q2-R3
date: '2026-11-22'
disposition: design-for-acceptance, owner receipts pending, implementation and publication gated
qualification_status: U1 partial, U2 pending, U3–U5 blocked, G-APPEND FAIL/BLOCKED
companions:
  - ci-lifecycle-inheritance.md
  - ../spec-plat-es416-qh-q2-contract-implementation/SPEC.md
  - ../spec-plat-es416-qh-q2-contract-implementation/implementation-plan.md
  - ../spec-plat-es416-qh-q2-contract-implementation/acceptance-checks.md
  - ../spec-plat-es416-qh-q2-contract-implementation/public-serialization.md
  - ../spec-plat-es416-qh-q2-contract-implementation/artifact-byte-contracts.md
  - ../spec-plat-es416-qh-q2-contract-implementation/selection-and-adoption.md
  - ../spec-plat-es416-qh-q2-contract-implementation/private-credentials.md
  - ../spec-plat-es416-qh-q2-contract-implementation/observation-report-rules.md
  - ../../../docs/contracts/test-environment-v1.md
  - ../../../docs/contracts/test-environment-v2.md
  - ../../../docs/contracts/candidate-and-attachment-policy-v1.md
  - ../../planning-artifacts/sprint-change-proposal-2026-11-09.md
sources:
  - ../../implementation-artifacts/spec-plat-es416-qh-qualification-handoff.md
  - ../../../tests/contracts/fixtures/eventstore-qualification-v2/public-binding-fragments.json
  - ../../../tests/contracts/fixtures/eventstore-qualification-v2/private-credential-metadata.json
---

# PLAT-ES416-QH — Q2 contract design R3

## Why

On November 11, published v1 clarified CI helper timeout, process termination, receipt recovery and the evidence required to report cleanup complete. The proposed v2 qualification handoff inherits the existing runner lifecycle. Platform, Builds and EventStore need one content-bound design that spells out how those rules apply to a v2 run, attachment and evidence consumer.

## Capabilities

- **CAP-1**
  - **intent:** Owners can exchange a versioned descriptor/result pair with the existing lifecycle bindings.
  - **success:** The paired v2 schemas pass RF-01–RF-03 with byte parity across delivery surfaces. Both pairs preserve the inherited first-terminal and cleanup semantics.
- **CAP-2**
  - **intent:** Suites and consumers can explicitly select a supported handoff before allocation.
  - **success:** Ordinary v1 and accepted v2 suites select their intended contracts. Unsupported or mixed versions refuse before allocation, credentials or launch (RF-04).
- **CAP-3**
  - **intent:** Owners can share immutable qualification identities and attributable observations through recovery.
  - **success:** Identity and transition mutations refuse. Finalization preserves the first terminal and prior observations. Unverified cleanup or an unavailable required observation earns zero lane credit (RF-05–RF-07, RF-10 and lifecycle cases).
- **CAP-4**
  - **intent:** Authorized clients can use credentials within their exact endpoint and control scope.
  - **success:** Cancellation, withdrawal and expiry close access. Bounded exact-owner reset and retry preserve that closure and strictly limit retained diagnostics (RF-08–RF-09 and lifecycle cases).
- **CAP-5**
  - **intent:** Owners can judge native completion against the required test set.
  - **success:** Exact identities, unique rows and counts reconcile. Helper `timedOut` and exit 124 remain runner evidence. Incomplete coverage and intended falsification failures remain non-passing (RF-11–RF-12 and lifecycle cases).
- **CAP-6**
  - **intent:** Platform, Builds and EventStore owners can accept the exact design and their work responsibilities independently.
  - **success:** Named R-P1, R-B1 and R-E1 receipts bind the same final revision, inventory and owning work. The design gate stays closed while any field or required decision is pending (RF-00).

## Constraints

- Carry forward R2's D1–D5 proposals and RF-00–RF-12. [CI lifecycle inheritance](ci-lifecycle-inheritance.md) governs how to apply their timeout, withdrawal, retention and cleanup wording. The R2 revision identifiers, checks and pinned baseline are historical records. [R3 owner acceptance](owner-acceptance.md) governs the current inventory and receipt slots.
- Keep the v1 public and private contracts, published schemas, candidate and attachment policy, and ordinary v1 handling. Proposed qualification v2 keeps the inherited terminal, attachment, retention and cleanup shapes. The CI receipt, job status and helper telemetry remain operational metadata.
- Preserve the EventStore commit contract and full AC1–AC11 denominator. Pending AC8/AC9 prerequisites, qualified custody, both separately authorized falsifications, Q3–Q5 infrastructure and canonical production-path proof retain their original gates.
- U1 stays partial, U2 pending, U3–U5 blocked and G-APPEND FAIL/BLOCKED. Story 4.16 stays in backlog pending a supported Dapr release that atomically rejects conflicting event writes. A release with that capability is a prerequisite for the existing conformance proof.

## Non-goals

- This revision prepares a design and checks its content. Implementation, publication, tool or consumer adoption, runtime qualification, service or fault execution, and custody or migration changes require their existing independent gates.
- R3 creates no new story or tracker entry. The completed preparation spec and the R2 packet retain their historical records.

## Success signal

Owners can verify the R3 raw-byte inventory and inspect the proposed v2 lifecycle against the November 11 v1 behavior. The resource-free checks specify the expected results for later implementation, and named acceptance for the final design and live qualification remain pending.

## Open questions

- Resolve D1–D5 with the named owners and supply complete synthetic closure examples in a final content-bound revision. Each owner still needs an authoritative work reference and matching receipt.
