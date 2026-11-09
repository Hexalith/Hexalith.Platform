---
title: 'PLAT-ES416-QH: offline v2 contract preparation'
type: 'chore'
created: '2026-11-09'
status: 'in-progress'
route: 'oneshot'
review_loop_iteration: 0
work_reference: 'PLAT-ES416-QH'
baseline_commit: '5cf3e8cbbea06cebcb8fc6ed2b8489ace1934732'
planning_acceptance: 'Administrator accepted Q1-Q6 planning on 2026-11-09'
tranche_decision: 'User selected recommended Platform-only contract preparation with apply recommended'
qualification_status: 'U1 partial; U2 pending; U3-U5 blocked; G-APPEND FAIL/BLOCKED'
course_correction: '../planning-artifacts/sprint-change-proposal-2026-11-09.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The accepted EventStore 4.16 handoff lacks a concrete Platform-owned contract design that Builds and EventStore can review before implementation/adoption.

**Decision:** The user selected the recommended Platform-only, resource-free contract preparation tranche. Prepare an explicitly unpublished v2 design and synthetic fixtures. Builds/EventStore acceptance and implementation, independent hosts, controls and execution remain pending. Completion of this tranche does not complete the accepted handoff.

**Deliver:** Create `docs/contracts/test-environment-v2.md` describing paired descriptor/result v2, private credential v2, typed profile/hosts/operations/controls/observations, explicit negotiation, immutable identity versus advancing observations, refusal cases, credential separation and full remaining intake. Add `tests/contracts/fixtures/eventstore-qualification-v2/public-binding-fragments.json` and `private-credential-metadata.json` as review examples, clearly marked synthetic fragments rather than valid complete descriptors/results/credential files. Use no credential values or claimed supported tuple. Check JSON syntax, reference integrity, identity agreement, unavailable observations and separation with offline checks; review the resulting documentation.

**Preserve:** [Accepted Q1–Q5, AC1–AC11 and U1–U5](../planning-artifacts/sprint-change-proposal-2026-11-09.md#q1--accept-the-owning-work-and-complete-the-intake) remain authoritative and outstanding. Q6 planning amendments already exist. Reviewed proposal SHA-256: `ec27e5993acc5cdd6227add69154d1207411f5b2cd4f40f97e9b1a2a104d15f4`. This tranche publishes no schema and adds no runtime support, adoption or qualification. Preserve current v1 schemas/private channel, candidate policy, existing planning edits, pins/tracking/readiness and 2.8/2.9/2.10/3.9 boundaries. No Builds/EventStore edits, external messages, workload/fault execution, falsification, keys, migration or production changes. U1 remains partial, U2 pending and U3–U5 blocked; the recorded negative capability finding and G-APPEND FAIL/BLOCKED remain unchanged.

</frozen-after-approval>

## Implementation Notes

- Scope answer resolves the sole open question. Three documentation/example files have no production consumer, external side effects or unresolved user choices; the workflow's small-change gate selects `oneshot`.
- Existing planning files were already modified before this tranche. Their byte identities and the original index were captured in `/tmp/platform-es416-contract-prep-i1yvf9ta/` for preservation checks; only this spec and the three new deliverables belong to the tranche.

- Added the unpublished design note and both synthetic fixture files. Public fragments preserve immutable pair/scope bindings and unavailable producers; the separate private sketch contains no credential value or accepted kind. Draft sample field names introduce no public API.
- Offline content checks passed for strict JSON, pair/scope agreement, owned endpoint/host/control references, expiry bounds, credential separation, unavailable observations and local links/fragments. Existing v1 schema-mirror test and whitespace checks passed. No product build, runtime test or qualification execution was needed for these documentation-only changes.
