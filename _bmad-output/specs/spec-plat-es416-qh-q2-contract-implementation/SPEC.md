---
id: SPEC-plat-es416-qh-q2-contract-implementation
work_reference: PLAT-ES416-QH
revision: PLAT-ES416-QH-Q2-R2
date: '2026-11-09'
disposition: design-for-acceptance; recommendations proposed; owner acceptance pending; implementation/publication gated
qualification_status: U1 partial; U2 pending; U3-U5 blocked; G-APPEND FAIL/BLOCKED
companions:
  - implementation-plan.md
  - owner-acceptance.md
  - acceptance-checks.md
  - public-serialization.md
  - artifact-byte-contracts.md
  - selection-and-adoption.md
  - private-credentials.md
  - observation-report-rules.md
  - ../../../docs/contracts/test-environment-v1.md
  - ../../../docs/contracts/test-environment-v2.md
  - ../../../docs/contracts/candidate-and-attachment-policy-v1.md
  - ../../planning-artifacts/sprint-change-proposal-2026-11-09.md
sources:
  - ../../implementation-artifacts/spec-plat-es416-qh-qualification-handoff.md
  - ../../../tests/contracts/fixtures/eventstore-qualification-v2/public-binding-fragments.json
  - ../../../tests/contracts/fixtures/eventstore-qualification-v2/private-credential-metadata.json
---

# PLAT-ES416-QH — Q2 contract implementation successor

> This kernel and its companions define the proposed implementation contract. They authorize review preparation only. The exact-design gate in `owner-acceptance.md` must pass before implementation or publication.

## Why

Platform's completed preparation tranche supplies an unpublished qualification design and synthetic fragments. Builds cannot yet select, produce or consume that v2 pair, and EventStore has not accepted consumer adoption. This successor makes the Q2 implementation boundary and acceptance tests reviewable without changing the completed preparation spec or mistaking Platform planning approval for either owner's commitment.

## Capabilities

- **CAP-1**
  - **intent:** Owners can exchange a versioned qualification descriptor/result pair while retaining existing runner bindings.
  - **success:** Paired closed v2 schemas pass the agreed fixture matrix; canonical, Builds mirror, embedded and packaged schema bytes match exactly, with v1 unchanged (RF-01–RF-03).
- **CAP-2**
  - **intent:** Suites and consumers can explicitly select a supported handoff before resources or tests start.
  - **success:** An accepted v2 suite selects the v2 pair; ordinary v1 suites retain v1; missing support, wrong-kind and mixed-version requests refuse with zero allocation/launch (RF-04).
- **CAP-3**
  - **intent:** Producers and EventStore consumers can share immutable qualification identity and attributable advancing observations.
  - **success:** Readers/writers and the adopted consumer agree on identity, references and host generations; mutations and contradictory events refuse atomically, and unavailable observations grant no lane credit (RF-05–RF-07, RF-10).
- **CAP-4**
  - **intent:** Authorized clients can receive private credentials for their exact endpoint and control scope without disclosure in retained material.
  - **success:** The accepted private v2 contract passes scope, file, expiry, withdrawal, client-confinement and exact-value scanning checks; v1 stays compatible; no module receives backend credentials or erasure signing keys (RF-08–RF-09).
- **CAP-5**
  - **intent:** Owners can judge native completion against the declared required test set.
  - **success:** Exact identities, unique native test rows and counts reconcile; exit zero cannot pass failed/skipped/missing/incomplete required rows; expected falsification failures remain failed (RF-11–RF-12).
- **CAP-6**
  - **intent:** Each owner can accept an inspectable design and their own work responsibility.
  - **success:** Separate named Platform, Builds and EventStore receipts bind the same final design revision, decisions and owning work references; missing fields remain pending and fail the gate (RF-00).

## Constraints

- Deliver only this separate successor and packet now. The preparation spec remains `done`, including its frozen intent; all existing source/design/fixture/planning files remain unchanged.
- R2 recommends concrete D1–D5 resolutions in the five design companions; none is owner-accepted. Final closure examples, names/work references and matching exact-design receipts remain pending before implementation. R1's inventory remains historical; this revision cannot inherit acceptance from planning or another hash. Publication additionally requires verified delivery, explicit adoption and the owners' release controls. No conditional receipt or planning approval opens either gate.
- Extend paired `hexalith.test-environment.v2` / `hexalith.test-result.v2` together and explicitly adopt private `hexalith.test-endpoint-credentials.v2`. Preserve v1 schemas, APIs, private channel and `hexalith.platform.candidate-and-attachment-policy.v1`; no silent upgrade/downgrade. The proposed compiled registry and internal plan v2 are explicit D3 changes for acceptance; no new manifest field or CLI flag is introduced.
- Retain strict parsing, closed nested objects, bounded local-file input, format/identity rules, atomic metadata-only refusal, immutable pair identity, original deadlines/first outcome, retention and exact-owner cleanup. `bindings.qualification` is required for qualification executions; the exact D1 tables and D4/D5 new types are proposals, not an accepted or published API.
- Synthetic fragments are neither complete valid contracts nor supported tuples. Expected pins, labels, URLs and startup success cannot replace loaded observations, qualified capability, logical readback or authenticated tool/candidate acceptance.
- U1 remains partial; U2 pending; U3–U5 blocked; G-APPEND FAIL/BLOCKED. Q2 completion alone cannot satisfy U2. Platform's historical readiness CONCERNS and EventStore's readiness/baseline FAIL/BLOCKED retain their distinct subjects.
- Preserve pins, locks, epic digests, tracking and readiness, including 2.8 review, 2.9 done, 2.10 ready-for-dev, 3.9 backlog and 4.9 backlog. `PLAT-ES416-QH` remains unnumbered; no new story or tracker entry is created.
- Keep the complete EventStore commit contract and AC1–AC11 denominator. Missing/skipped/withdrawn/unsupported lanes are non-passing; prerequisite-dependent AC8/AC9 rows remain pending. CAS supplies no activation fencing.

## Non-goals

- No Builds/EventStore edits or external messages in this tranche; their owners undertake later changes only under accepted work.
- No schema publication, product implementation, tool release/repin, runtime execution, service provisioning, control/fault execution, falsification or capability re-screen now.
- No Q3 host/control topology, Q4 semantic campaign or Q5 lifecycle campaign implementation here; no provider access in module fixtures, module-owned AppHost, standing fault service, new erasure issuer or evidence ledger.
- No key distribution, migration, restore execution, production change, private runtime/provider exception, new activation authority or weakening of application semantics.

## Success signal

The separate successor, R2 exact revision inventory, concrete proposed D1–D5 tables, independently pending owner receipts and focused resource-free acceptance matrix are reviewable on disk, with the preparation spec and protected baseline preserved. A later accepted implementation must pass RF-00–RF-12 and retain v1 compatibility; that proves Q2 delivery only, never append prevention, U2 completion, tool/candidate qualification or final conformance.

## Assumptions

- D1–D5 recommendations, including new serialization names/limits, artifact encoding, registry/internal plan v2, private kinds and native count mapping, are author proposals for acceptance; they are not owner decisions. The five design companions contain their exact scope.

## Open Questions

- D1–D2: Will the owners accept the proposed closed serialization/byte contracts, and provide the named producers and complete synthetic closure examples for a final hashed revision?
- D3: Will Builds/Platform accept the proposed registry and internal plan v2, and EventStore the exact consumer support mapping? Actual entries, adopted revisions and releases remain pending.
- D4: Will credential/Security owners accept the proposed bounded private file, finite kinds and scope/expiry/reset contract? Named review and qualified custody remain pending.
- D5: Will control/native-report/Test owners accept the proposed transition, diagnostic, identity and count tables? Complete adapter/closure examples remain pending.
- Who are the named Platform executing/contract, Builds and EventStore consumer/assertion accepting maintainers, and what owning work references and same-revision receipts will they supply? All remain pending; the packet records the known planning receipt separately.
