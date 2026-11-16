---
title: '4.9 Ratify the hosted runtime and identity-event contracts'
type: 'feature'
created: '2026-11-16'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
story_key: '4-9-ratify-the-hosted-runtime-and-identity-event-contracts'
context:
  - '{project-root}/docs/contracts/ad26-preparation/README.md'
  - '{project-root}/docs/contracts/shared-infrastructure-currency-policy-v1.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 9.2 needs Story 4.9's actual ratified hosted inventory. The approved AD-26 draft design supplies neither that inventory nor its qualification and authority.

**Approach:** Establish the jointly owned template/binding implementation, then populate and qualify the inventory, hosted catalog, secret contract, renewal ownership and capture-lag decision. Retain the approved draft bytes. This readiness plan does not approve a replacement schema or technical target.

**Decisions (2026-11-16, user: “apply recommended”):** Prepare a focused EventStore 3.19 profile-schema/validator construction spec; consume an adopted owner implementation if identified before coding. Prepare a measured capture-lag proposal without assigning a numeric bound. Keep the existing 9.2 history intact. These select preparation work, not an infrastructure target, final encoding or production authority. Schema construction does not wait for the capture-lag decision or issue-step qualification.

**Subsequent mapping decision (2026-11-16, user: “apply recommended”):** Check the completed v1 against a standalone provider, bundled constituent and management/workload pair. Adopt only if clean; prepare focused owner v2 if a concrete gap is found. The node-level containerd mapping fails because v1 requires inapplicable namespace/configuration facts. Withhold v1 adoption and prepare the focused v2 construction spec; actual inventory and final contract adoption still follow owner implementation and review.

**Format adoption (2026-11-16, user: “adopt v2”):** Adopt `docs/contracts/hosted-profile-binding-v2.md` as the owner-based construction format for Platform 4.9, using EventStore v2 schema, canonical encoding, codec/validator and digest domains at reviewed source `57459e03bfe4cdbcbf0ecc1435c7aeb49d60812e`. `docs/contracts/hosted-profile-v2-adoption.json` binds the exact reviewed proposal and source identities. This resolves final construction-format selection only; actual technical selections, qualified inventory, authenticated AD-26 ratification, production authority and parent-story acceptance remain outstanding. Earlier pending-adoption statements are historical.

## Boundaries & Constraints

**Always:** Keep Platform's infrastructure digest (template plus provider/topology and deploy-tool pins) distinct from EventStore's complete resolved-profile digest. Publish accepted resolved bytes only at EventStore's `deploy/dapr/production-profile.yaml`. Retain actual component scopes, immutable identities, supported/qualified sets, renewal/currency owners, authoritative upstream sources and management/workload pairing. Credentials are references only. Reuse the owning catalog generator and codecs.

Schema/validator construction may precede issue-step qualification. Acceptance still requires qualified runtime/broker, mandatory append/erasure protection, restore posture and authenticated owner records under existing issuance/assurance rules. Draft approval and content hashes do not provide them.

**Never:** Invent pins, topology, qualification, numeric capture-lag policy or authentication. Modify the approved package, install infrastructure, issue authority, change release writers, implement the rejected standalone currency checker or mark 4.9/9.2 complete from fixtures.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Complete | Qualified inputs and adopted contracts | Exact inventory and linked acceptance evidence | Validate all required coverage |
| Draft only | Null target, empty inventory or unissued records | Preparation remains unaccepted | Name missing inputs |
| Wrong identity | Mixed digest domains, missing constituent or different pairing | Refuse acceptance | Identify affected input |
| Missing authority | No owner codec, role proof or issuance prerequisite | No publication authority | Preserve refusal |

</frozen-after-approval>

## Code Map

- `docs/contracts/ad26-preparation/design-approval.json` — binds six approved drafts and the preserved index; excludes technical selection and ratification. Preserve every bound byte.
- `docs/contracts/ad26-preparation/release-binding-contract.draft.md` — approved preparation draft with unset selections; the adopted structural format is recorded separately in `docs/contracts/hosted-profile-binding-v2.md`.
- `_bmad-output/planning-artifacts/epics.md`, Story 4.9 — owns inventory, catalog, secrets, renewal and capture lag; 6.14/8.17 callers remain backlog.
- `references/Hexalith.EventStore/_bmad-output/planning-artifacts/epics.md`, Story 3.19 — owns canonical profile/publication validation; explicitly separates construction from issuance. Consume its implementation; never fork its codec.
- `docs/contracts/hosted-profile-binding-v1.md`, `docs/implementation/hosted-profile-v1-mapping-assessment.md` — source-backed mapping against EventStore `54f83e3b6a6eca3c8cf0caeb734cbcad30e18e41` finds a node-scope gap; v1 is not adopted. The conditional decision is resolved; v2 construction is complete and its format is now adopted by the user; qualified inventory remains outstanding.
- `src/Hexalith.Platform.Composition/PlatformRoutingCatalog.cs`, `src/Hexalith.Platform.CatalogBuilder/` — existing declaration-driven generator; local/source results grant no hosted acceptance.

- `docs/contracts/hosted-profile-binding-v2.md`, `evidence/9-2/profile-v2-implementation-20261116.json` — adopted construction mapping and pre-adoption source hashes; `docs/contracts/hosted-profile-v2-adoption.json` records the format decision, while inventory acceptance remains outstanding.

## Tasks & Acceptance

**Execution, ordered by dependency:**
- [x] `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-3-19-production-profile-validation.md` — prepare the focused owner construction spec, preserving all publication/qualification gates.
- [x] `docs/implementation/identity-event-capture-lag-measurement-proposal.md` — prepare timing, measurement cases and decision path without fabricating observations or a bound.
- [x] `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-3-19-production-profile-v2-infrastructure-scope.md` — complete the approved focused owner scope revision at `57459e03bfe4cdbcbf0ecc1435c7aeb49d60812e`.
- [x] `docs/contracts/hosted-profile-binding-v2.md`, `docs/contracts/hosted-profile-v2-adoption.json` — record the user-adopted owner v2 schema/encoding/validator identities and digest domains after implementation and review. V1 remains unadopted; this completes format selection only.
- [ ] `deploy/dapr/hosted-profile-inventory.json` — proposed Platform inventory path; populate actual qualified providers/topology, management/workload pairing, support sources, deploy tools and owners once available.
- [ ] `deploy/dapr/eventstore-routing-catalog.json` — generate the hosted catalog through Story 3.3 with actual environment bindings; preserve retention entries.
- [ ] `deploy/dapr/openbao-secret-contract.yaml` — enumerate value-free environment/static/dynamic namespaces and sole writers from owning declarations.
- [ ] `docs/contracts/identity-event-capture-lag-v1.md` — record the finite policy decision after measurements, including breach notification to Administrator/deputy.
- [ ] `references/Hexalith.EventStore/deploy/dapr/production-profile.yaml` — publish complete resolved bytes through the owner flow only after qualification and authenticated ratification; no partial substitute.
- [ ] `_bmad-output/implementation-artifacts/evidence/4-9/` — retain owner-validator refusal/acceptance cases and actual qualified-input/authority references; resume 9.2 only against accepted inputs.

**Acceptance Criteria:**
- Given the ratified template and qualified actual inputs, when 4.9 is accepted, then provider/topology/tool pins, complete management inventory and both distinct profile identities are recorded and validated.
- Given the hosted composition, when catalog/secrets are generated, then the owner generator supplies the catalog and the value-free secret contract identifies environment, namespaces and sole writers.
- Given time-bound inputs and identity events, when the inventory is ratified, then every renewal owner and the finite capture-lag decision are recorded.
- Given missing qualification or authority, when acceptance is attempted, then it refuses with named missing evidence and leaves 4.9/9.2 incomplete.

## Implementation Notes

- Planning only. Root baseline: `96383a27d1f79cb5d9e057e8acd8c07df444346f`; EventStore: `df976e0051b9e2eaa9d3592b3054a0317d83d0cb`. Both stories remain backlog; the canonical profile is absent. Broker/runtime, append-protection and restore qualification remain engineering dependencies, not facts a generic approval can supply.
- 2026-11-16: Applied the user's selected preparation path. Both prior Open Questions are resolved; final contract adoption, measured policy and qualified actual inputs remain future acceptance requirements. This parent readiness plan stays draft.
- 2026-11-16 prerequisite refresh from Platform `575aa1889b0dc654b36de2e6958878c5eadbc871`: EventStore's profile construction and publication-authority-validator specs are both done at `54f83e3b6a6eca3c8cf0caeb734cbcad30e18e41`. Prepared `docs/contracts/hosted-profile-binding-v1.md` for the next adoption decision without changing the approved package. Full execution-input proof, Builds codec integration, actual inventory/qualification, ratification and owner publication remain outstanding. This parent stays draft, and no acceptance task is newly marked complete.
- 2026-11-16: The subsequent “apply recommended” selects conditional adoption after the three-case mapping check. Its containerd case establishes a representation gap, so the binding proposal now withholds v1 adoption and the next construction spec targets focused v2 scope. The frozen block adds only this authorized decision; prior decisions and approved-package bytes are unchanged. No parent acceptance is claimed.
- 2026-11-16: User approved the focused v2 construction spec and requested a stop. Its status is `ready-for-dev` with reviewed body unchanged; implementation has not started. This parent remains draft/backlog and actual inventory/final adoption remain outstanding.

- 2026-11-16 resumed prerequisite execution: approved owner v2 construction is done at local EventStore commit `57459e03bfe4cdbcbf0ecc1435c7aeb49d60812e`. Three review layers completed; topology cardinality and namespace defects were fixed, regression coverage expanded, and all 235 profile cases, 25 authority tests and 48 genuine bridge controls pass. V1 golden bytes, fixed refusals and approval-bound preparation bytes remain unchanged. Four external/unverified follow-ups are recorded in the deferred-work ledger.
- Prepared `docs/contracts/hosted-profile-binding-v2.md` and `evidence/9-2/profile-v2-implementation-20261116.json` as the concrete next format-adoption package. It is proposed, not adopted. Actual qualified inventory, runtime/broker/append/restore proof, authenticated ratification, owner publication and admission/drill callers remain outstanding; no live currency checker was implemented. This parent remains draft/backlog, and owner parent 3.19 remains in-progress. The owner commit is local; no remote operation or superproject commit was performed.

- 2026-11-16: User said “adopt v2”. Recorded Platform's v2 construction-format adoption against the exact reviewed proposal and 56 owner-source identities, completed only the format-selection prerequisite, and retained the earlier readiness observation and approved preparation bytes. No component/provider selection, accepted inventory, capture-lag bound, qualification, authenticated ratification, production publication or live currency caller was created; parent story status remains draft/backlog.

- 2026-11-16 post-v2 investigation at Platform `71436c7c3dd9af88253b5cc46b9ffe3511a7f2a1` and EventStore `107e50e39cf2545cbc6546e60e02252251aca274`: actual qualified inventory, hosted catalog, secret contract, capture-lag evidence and authenticated publication remain outstanding. Proposed next engineering slice: `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-3-19-isolate-the-production-profile-bridge-build.md`. This draft isolates verified application build inputs, retains the authenticated build-proof refusal and needs its own checkpoint approval. It accepts no inventory or production target and completes none of this parent's open acceptance tasks. Existing frozen decisions and approved preparation bytes are preserved.

- 2026-11-17: The user approved and continued the focused bridge-isolation prerequisite; it is done at local EventStore commit `0c2c09ed4279473570d3243d5c01e48301c8e46e`. Verified private SDK-only snapshots preserve all 38 profile sources and both digest domains. Three-layer review, 15 snapshot/25 publication/43 lifecycle tests, 52 genuine bridge controls and preservation/lint checks pass; two separate SDK/diagnostic follow-ups are deferred. This engineering result accepts no inventory, establishes no full toolchain/qualification/ratification proof and creates no production profile. This parent remains draft/backlog with all actual-input and publication tasks outstanding. The owner commit is unpushed and the superproject gitlink is not committed.

## Spec Change Log

## Review Triage Log

## Verification

- Next, establish actual qualified inventory, hosted catalog and secret-contract inputs, renewal ownership, measured capture-lag policy and authenticated owner publication against the adopted v2 format. Full 4.9 acceptance remains outstanding; this parent is not ready for full implementation.
- Validate exact approved-package hashes and preserved intent, dependency paths, JSON parsing and `git diff --check` under `run-with-temp`. These checks establish preparation consistency only.
