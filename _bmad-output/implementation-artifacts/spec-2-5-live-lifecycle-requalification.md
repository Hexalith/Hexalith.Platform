---
title: '2.5 Requalify the live local lifecycle with isolated Dapr'
type: 'chore'
created: '2026-10-28'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Configure a persistent HEXALITH_DAPR_HOME containing isolated Dapr binaries matching the current Builds catalog. Rerun story 2.5's live source lifecycle qualification, resolve failures within its existing scope, independently verify cleanup and peer isolation, retain logs and audit evidence, and update the story's qualification status. Preserve historical evidence, published pins, package/native qualification boundaries and existing owner decisions. Do not push.

</frozen-after-approval>

## Implementation Notes

- The catalog selects Dapr CLI 1.18.2, runtime 1.18.7 and Aspire 13.6.4. Docker and matching Aspire are available. No intent gap or irreversible operation is needed; the live fixture creates and removes its own resources.
- Use the existing live test and evidence conventions. Retain fresh inputs separately under `evidence/2-5/live-requalification-20261028/`, preserving prior failed prerequisites and the 950-case offline verification.
- Keep the story's sprint status at review pending human acceptance; record source live qualification separately. No staging, commit or push is needed for this operational qualification request.
- The fresh live run passed all four scenarios in 423.363 seconds. All three retained-failure scenarios needed the existing exact-down retry after HXR028; retry and repeated cleanup succeeded. The peer's four containers, three stable processes and Redis data were preserved throughout.
- The independent host scan verifies all five owners removed. Runtime/final-archive, immutable-result, source-fingerprint, retained-evidence and relocated-evidence audits pass. Keep both per-scenario and post-final-cleanup snapshots because cleanup/retention observations legitimately change on final replay. Earlier overly broad audit assertions remain as superseded evidence.
- All 234 source/configuration files stayed unchanged, including the 184 files from prior 950-case verification; those offline tests were not rerun. Installation provenance, full logs/XML, retries and audits are retained. Both Debug builds passed with zero warnings/errors. The main story records source-live-qualified; sprint tracking stays review.
- Independent review exposed evidence gaps. The qualification fixture now records the initial peer baseline and pre-down results, and the audit enforces complete inventories, historical failure identities, every retry, prior-source comparisons and script snapshots. Exact Git log retention and CRLF whitespace attributes are scoped to this change. All ten original findings and the follow-up observation correction are resolved; no new deferral.
- Final reviewed qualification passes 4/4 scenarios in 384.682 seconds with 261 unchanged selected source/configuration files and all 184 prior source fingerprints matching. The cleanup helper passes 1/1. Runtime/retained/relocated audits and all five negative controls pass. All five owners are cleaned and their durable empty ownership receipts are verified. The initial passing run and its complete host observations remain preserved. No product code changed; no staging, commit or push was performed.

## Review Triage Log

All findings and evidence are recorded individually in `evidence/2-5/live-requalification-20261028/review-triage.json`: B1–B3 and B6–B10 medium, patched; B4–B5 low, patched; F1 medium, patched; F2 false, first host snapshots were already backed up before replacement. No new deferral. The reviewer confirmed the remaining substantive corrections.
