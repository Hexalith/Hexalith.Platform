# Success Measures

These are acceptance targets; none has been achieved yet. IDs, scopes and thresholds are the PRD's, verbatim in substance.

- **Ownership:** Platform owns the combined acceptance evidence. Module developers supply their readiness, operation and critical-flow checks. Administrator owns the recovery evidence.
- **Recording:** every demonstration records the release or source revision, expected and loaded artifact identities when applicable, environment, result and supporting diagnostics.

| ID | Measure | Validates | When |
| --- | --- | --- | --- |
| SM-1 | **Complete local environment.** All seven MVP modules and required supporting resources become ready under the effective FR-4 startup deadline. CLI and MCP demonstrations execute enabled modules' agent-eligible commands and queries with the expected results. | CAP-1, CAP-12 | MVP acceptance |
| SM-2 | **Module workspace development.** Each domain-module workspace (Tenants, Parties, Folders, Projects) demonstrates its developer-defined minimum composition, and a breakpoint or source change in the active checkout affects the running module. EventStore, Memories and McpCli demonstrate the same source-debugging outcome from the Platform workspace. Only direct root-declared dependencies are initialized. | CAP-2, CAP-3 | MVP acceptance |
| SM-3 | **Test execution and lifecycle.** Every MVP module demonstrates its declared minimum composition and required local Aspire and CI integration checks from its CAP-2 workspace. Success, failure, startup-timeout, cancellation, collision and attachment scenarios prove CAP-4 ownership, retention, cleanup, leftover reporting and accessible diagnostics without affecting another environment. Attachment cases cover owner finish and cancellation before attachment end, attached failure and hold expiry. CI identifies the candidate revision and expected and loaded artifacts; substituted or uncommitted inputs are refused, and a baseline-only run cannot satisfy candidate acceptance. | CAP-4, CAP-5 | MVP acceptance; after lifecycle changes |
| SM-4 | **Hosted access and isolation.** Staging and production expose declared supported interfaces and named agent-eligible McpCli operations. Allowed operations succeed and every CAP-10/CAP-11/CAP-12 and NFR-3 refusal check passes for users, workloads and automation. The SM-4 admission test identity supplies positive production evidence through the human admission path before general admission; its grant expires by G2 and its revocation is followed by a denial check. | CAP-10–CAP-12, NFR-3 | Before G2; after access changes, including the first G2 admission; during recovery verification |
| SM-5 | **Promotion and recovery.** Both release modes enforce exact-release staging E2E evidence for the complete enrolled composition. Automatic promotion rejects invalid compatibility evidence, requires candidate-to-current-baseline rehearsal and is blocked by a failed shared-infrastructure currency check. Controlled failures prove CAP-7 triggers, one CAP-8 recovery within budget and NFR-1 preservation. Scenarios cover interruption, routing-only change, first installation, failed or unverified recovery, every stop trigger, per-cause Administrator clearance and recurrence after admission/lost-window review, approved degraded attempts and later-stop precedence, refusal when production is neither empty nor degraded, deputy limits, failed retained-data recovery preserving a post-cut admission revocation and acknowledged Memories erasure, shared-infrastructure forward revert, GitHub delivery and deputy recovery access. | CAP-6–CAP-8, CAP-10, NFR-1 | Before G3; after recovery-policy changes, which suspend automatic promotion until affected rehearsals repeat |
| SM-6 | **Recoverable production data.** An isolated drill proves complete recovery sets, continuous RPO at most one hour and RTO at most four hours under declared coverage, including worst-case response, capacity, security, erasure-safe key coverage, data, smoke and access checks and denial of admission revoked just before failure. It records all lost-window categories, grants to re-apply and reduced-recovery state with the stop set. Backup cadence and retention, independent availability and freshness monitoring, monitor-silence detection and GitHub delivery to Administrator and deputy are verified. | CAP-9, NFR-2 | Before G2; monthly; after material backup, storage or recovery-mechanism changes |

Spine carry-through, not new targets:

- SM-3 evidence counts only from runs owned by the `hexalith-module` runner; a technical module's own AppHost runs never count (AD-10).
- SM-5 fault rehearsals on a staged release may run before G2 in the Administrator-approved mode (RRA Release modes).

## Counter-metrics

| ID | Guard | Counterbalances |
| --- | --- | --- |
| SM-C1 | **Bypassed validation.** Zero releases in either mode promoted with failed, skipped, missing, incomplete, stale or mismatched E2E evidence. Zero automatic promotions without valid current-baseline compatibility evidence and G3 qualification. Approval cannot waive common gates. Zero degraded-path approvals when production is neither empty nor degraded, and zero stop clearances without an authenticated Administrator record or verified eligible approved attempt. | SM-5 |
| SM-C2 | **Unauthorized cross-environment access.** Zero successful production operations, data or secret access, or administrative changes in the staging-only user, workload and automation negative checks, including restored copies. Shared capacity savings must preserve isolation. | SM-4 |
| SM-C3 | **False recovery success.** Zero recoveries reported successful without the required readiness, smoke-test and data checks. A shorter recorded recovery time must not omit operator or provisioning time or hide data loss. | SM-5, SM-6 |
| SM-C4 | **Destructive or incomplete cleanup.** Zero deletions of another environment's resources, zero automatic-cleanup deletions while an attached run remains within its hold, and no cancelled or finished CI run counted as cleaned up while owned resources remain. | SM-3 |
| SM-C5 | **Hidden startup delay.** Actual startup duration and any explicit timeout overrides are recorded alongside readiness results. Raising a timeout is never reported as improved startup performance. There is no setup-time improvement target. | SM-1, SM-3 |

## Scope notes

- There is no numeric uptime, latency or throughput target. Administrator revisits this if external adoption or operating evidence calls for a stronger commitment.
- Acceptance is expressed as capability and acceptance scenarios. Module business journeys stay with the modules.
- Incremental enrollment supports implementation; final MVP acceptance still requires all seven modules in the agreed environments.
