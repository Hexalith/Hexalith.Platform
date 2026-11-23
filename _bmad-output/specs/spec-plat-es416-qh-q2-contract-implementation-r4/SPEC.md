---
id: SPEC-plat-es416-qh-q2-contract-implementation-r4
work_reference: PLAT-ES416-QH
revision: PLAT-ES416-QH-Q2-R4
date: '2026-11-22'
disposition: owner review pending, implementation and publication gated
qualification_status: U1 partial, U2 pending, U3–U5 blocked, G-APPEND FAIL/BLOCKED
companions:
  - synthetic-closure-examples.md
  - ../spec-plat-es416-qh-q2-contract-implementation-r3/SPEC.md
  - ../spec-plat-es416-qh-q2-contract-implementation-r3/ci-lifecycle-inheritance.md
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

# PLAT-ES416-QH Q2 contract design R4

## Why

Platform, Builds and EventStore need a single content-bound design for reviewing a versioned qualification handoff. The R4 synthetic examples show how the proposed formats fit together and how the published v1 CI lifecycle governs v2 outcomes and recovery.

## Capabilities

- **CAP-1**
  - **intent:** Owners can exchange a paired v2 handoff with typed qualification and inherited lifecycle bindings.
  - **success:** Complete ready, failed and partial-startup snapshots reconcile, reject invalid shapes and preserve the v1 envelope. Later RF-01–RF-03 require byte parity across delivery surfaces.
- **CAP-2**
  - **intent:** Suites and consumers can explicitly select a supported handoff before allocation.
  - **success:** Ordinary suites use v1. Exact supported v2 selection binds the plan and the same public/private versions. Missing, drifted, ambiguous and mixed inputs refuse before allocation, credentials or launch (RF-04 and RF-10).
- **CAP-3**
  - **intent:** Owners can share attributable observations and immutable identities through recovery.
  - **success:** Identity and transition mutations refuse. The first terminal and original hold facts survive finalization. Unverified cleanup and unavailable required observations earn zero lane credit (RF-05–RF-07, RF-10 and LC-01–LC-06).
- **CAP-4**
  - **intent:** Authorized clients can use private credentials within exact endpoint and control scope.
  - **success:** Cancellation, withdrawal, revocation and expiry close access. Only bounded exact-owner reset can undo an admitted fault after closure. Retained diagnostics exclude private values (RF-08–RF-09 and LC-06).
- **CAP-5**
  - **intent:** Owners can judge native completion from attributed reports and the required test set.
  - **success:** Exact identities, unique rows and counts reconcile. Helper timeout telemetry stays independent of the owner terminal and native row outcomes. Missing coverage and intended falsification failures stay non-passing (RF-11–RF-12 and LC-01).
- **CAP-6**
  - **intent:** The three owners can independently accept the same design and own their delivery work.
  - **success:** All required D1–D5 decisions are resolved and named R-P1, R-B1 and R-E1 receipts bind the same final revision, aggregate, inventory and owning work (RF-00).

## Constraints

- The [R4 examples and decisions](synthetic-closure-examples.md) supply the missing closure inputs for R2 D1–D5 and R3 LC-01–LC-06. Historical pending-example wording refers to those revisions. R3 lifecycle inheritance governs CI interpretation. The [R4 receipt record](owner-acceptance.md) governs current intake.
- Keep the published v1 schemas, ordinary v1 and private-channel behavior, candidate and attachment policy, and inherited terminal, termination, retention and cleanup shapes. Keep the helper receipt, job status and timeout telemetry outside public bindings.
- Preserve the EventStore commit contract and full AC1–AC11 denominator, including pending AC8/AC9 prerequisites, qualified custody, both separately authorized falsifications, Q3–Q5 infrastructure and canonical production-path proof.
- U1 stays partial, U2 pending, U3–U5 blocked and G-APPEND FAIL/BLOCKED. EventStore story 4.16 stays in backlog pending a supported Dapr release that atomically rejects conflicting event writes. The existing conformance proof remains required.

## Non-goals

- R4 prepares and verifies review artifacts. Implementation, schema or tool publication, adoption, hosted or local qualification, fault execution, custody and migration retain their independent gates.
- R4 creates no story or tracker entry. R2, R3 and the completed preparation spec retain their historical records.

## Success signal

A reviewer can verify the full R4 inventory against the checkout, trace D1–D5 through complete synthetic inputs and expected results, and reproduce the CI recovery and refusal expectations without allocating services. The design gate requires the three exact-design receipts and resolution of the open choices below.

## Assumptions

- The fixtures use one fictional package-mode CI owner, two fictional writer hosts and fake producer references. Their values exercise proposed shapes and joins.

## Open questions

- Will the owners accept the proposed D1–D5 serialization, byte formats and producer assignments, selection rules, private transport and lifetime rules, and native accounting and observation semantics?
- Who owns the Platform, Builds and EventStore work, including the D4 credential and Security review and the EventStore test-set and 3.17 inventory? Which owning references will carry the formal receipts?
