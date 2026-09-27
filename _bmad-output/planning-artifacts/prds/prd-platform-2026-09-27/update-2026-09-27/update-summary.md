# Platform PRD update — 2026-09-27

Status: review in progress.

The product owner requested alignment with the latest PRD validation and accepted architecture. This update preserves FR-1–FR-12, NFR-1–NFR-3, SM-1–SM-6 and SM-C1–SM-C5, the seven-module MVP, and existing numeric rollout, backup and recovery targets.

## Requirements updated

- Defined complete release and enrollment scope, required evidence, approved and automatic release modes, and G1/G2/G3 production gates.
- Connected compatibility evidence and staging rehearsal to promotion; made interruptions, current security state, routing changes and durable promotion stops explicit.
- Aligned active-source resolution, technical-module workspaces, local success cleanup, explicit attachment and CI ownership with the accepted architecture.
- Propagated McpCli agent eligibility, connected contract matching, calling-surface enforcement and concrete acceptance evidence.
- Extended isolation to automation, workloads, administration and restored production copies.
- Defined complete recovery sets, independent monitoring, recovery access, revocation/erasure continuity and qualification evidence.
- Replaced stale architecture questions in the addendum with accepted decisions and named implementation prerequisites; preserved original options and rationale.

## Product-owner choices made during this update

1. **Deputy authority:** the named deputy receives alerts and may restore, verify and reopen service through the documented procedure. Administrator alone resumes promotions and administers production-user admission.
2. **RTO coverage:** four-hour recovery applies to outages beginning within declared response coverage. Covered incidents remain covered through restoration. Every incident reports full outage duration without pausing or restarting the clock. The one-hour RPO, backup protection and monitoring remain continuous.

The addendum records how both alternatives work, their advantages and costs, and the recommendations selected by the product owner.

## Downstream alignment and qualification

Architecture/spec owners must carry the deputy access and notification policy, restricted pre-G2 synthetic test admission and precise RTO coverage into downstream controls before affected stories are finalized. Administrator must name the deputy and declare coverage/time zone/acknowledgement bounds, and the team must demonstrate actual access, isolation, replacement capacity and recovery before the corresponding production gate.

Final document status will not establish runtime qualification. The original [validation report](../validation-report.md) remains historical evidence; the reconciliation and reviewer files in this folder record its disposition for this revision.

## Verification

Pending completion of source reconciliation, rubric and focused reviews, editorial passes and final document checks.
