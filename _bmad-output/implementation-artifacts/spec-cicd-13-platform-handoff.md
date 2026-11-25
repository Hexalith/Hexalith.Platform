---
title: 'CiCd Story 13 Platform handoff'
type: 'refactor'
created: '2026-11-25'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent">

## Intent

Works parity currently invokes two Platform verification suites and two Agents host checks outside the R1–R11 contract. Run all four in Platform CI with the authenticated Agents producer context and the two Release-mode host outcomes.

Remove the four command rows, Agents evidence and unreachable helper hashes from the Works ledger. Preserve the R1–R11 verifier and the independent 180-minute Dapr and Aspire job, including the inputs, failures and artifacts used for parity acceptance. Verify the Platform lanes and the Works parity path on the candidate.

</frozen-after-approval>

## Implementation Notes

- Platform tuple CI runs the moved lanes in `host-evidence`. Its verdict and release gates require the job. Both Agents lanes use one authenticated Release/package context and the signed Builds tool from the recorded producer. Platform retains the context and artifact identity with the lane evidence.
- The Works ledger retains only the legacy `profile.agentsConfiguration=Release` field for the deployed coordinator's profile check. The Agents context, result and command rows move to Platform together with the verifier-only tests.
- The Works proof definitions, live invocations, required artifacts and closure logic for the retained inputs were preserved. The Works workflow, action and platform snapshot exporter were byte-compared during verification.

## Review Triage Log

- Fixed the dependency ordering so the actual Works toolchain test can read the Builds catalog.
- Added `host-evidence` to the exact workflow job set enforced by the release gates and their fixtures.
- Kept the existing Agents Full refusal predicate of exit 1 with `DependencyNotAvailable: EXT-HOST-1`. Component suite outcomes remain visible in the structured evidence.
- Gated sibling lane steps on their shared setup so a lane failure leaves the other runnable checks eligible.
- Retained the candidate identity from bootstrap and bounded both Agents steps to 30 minutes so they can leave evidence before the job deadline.
- The handoff report records the candidate revision and the observed verification outcomes.
