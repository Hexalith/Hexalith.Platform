# Success Measures

These are acceptance targets; none has been achieved yet. IDs, scopes and thresholds are the PRD's, verbatim in substance.

- **Ownership:** Platform owns the combined acceptance evidence. Module developers supply their readiness, operation and critical-flow checks. Administrator owns the recovery evidence.
- **Recording:** every demonstration records the release or source revision, environment, result and supporting diagnostics.

| ID | Measure | Validates | When |
| --- | --- | --- | --- |
| SM-1 | **Complete local environment.** All seven MVP modules and required supporting resources become ready under the effective FR-4 startup deadline. CLI and MCP demonstrations execute enabled modules' agent-eligible commands and queries with the expected results. | CAP-1, CAP-12 | MVP acceptance |
| SM-2 | **Module workspace development.** Each domain-module workspace (Tenants, Parties, Folders, Projects) demonstrates its developer-defined minimum composition, and a breakpoint or source change in the active checkout affects the running module. EventStore, Memories and McpCli demonstrate the same source-debugging outcome from the Platform workspace. Only direct root-declared dependencies are initialized. | CAP-2, CAP-3 | MVP acceptance |
| SM-3 | **Test execution and lifecycle.** Every MVP module demonstrates its declared minimum composition and required local Aspire and CI integration checks from its CAP-2 workspace. Success, failure, startup-timeout, cancellation, collision and attachment scenarios demonstrate the CAP-4 ownership, retention and cleanup rules, leftover reporting and accessible diagnostics, without affecting another environment. | CAP-4, CAP-5 | MVP acceptance; after lifecycle changes |
| SM-4 | **Hosted access and isolation.** Staging and production expose declared supported interfaces and named agent-eligible McpCli operations. Allowed operations succeed and every CAP-10/CAP-11/CAP-12 and NFR-3 refusal check passes for users, workloads and automation. A controlled, explicitly admitted test user supplies positive production evidence before general user admission: the G1 synthetic grant ([sequencing.md](sequencing.md#g1--production-deployed-ingress-closed)). | CAP-10–CAP-12, NFR-3 | Before G2; after access changes, including the first G2 admission; during recovery verification |
| SM-5 | **Promotion and recovery.** Both release modes enforce exact-release staging E2E evidence for the complete enrolled composition. Automatic promotion rejects invalid compatibility evidence and requires a candidate-to-current-baseline rehearsal. Controlled deployment failures exercise the CAP-7 triggers and verify one CAP-8 recovery within its budgets, preserving NFR-1 state. Separate scenarios cover interruption, routing-only changes, first deployment, failed or unverified recovery, the durable promotion stop and Administrator-only resumption. GitHub delivery and the deputy's recovery access are verified. An incompatible approved release demonstrates its separately planned recovery acceptance. | CAP-6–CAP-8, NFR-1 | Before G3; after recovery-policy changes, which suspend automatic promotion until the affected rehearsals repeat |
| SM-6 | **Recoverable production data.** An isolated drill proves complete recovery sets, continuous RPO at most one hour and RTO at most four hours under declared coverage, including worst-case response, capacity, security, erasure, data and smoke-test checks. Backup cadence and retention, independent availability and freshness monitoring, monitor-silence detection and GitHub delivery to Administrator and the deputy are verified. Full outage duration and coverage status are recorded; recovery-point age is monitored between exercises. | CAP-9, NFR-2 | Before G2; monthly; after material backup or storage changes |

Spine carry-through, not new targets:

- SM-3 evidence counts only from runs owned by the `hexalith-module` runner; a technical module's own AppHost runs never count (AD-10).
- SM-5 fault rehearsals on a staged release may run before G2 in the Administrator-approved mode (RRA Release modes).

## Counter-metrics

| ID | Guard | Counterbalances |
| --- | --- | --- |
| SM-C1 | **Bypassed validation.** Zero releases in either mode promoted with failed, skipped, missing, incomplete, stale or mismatched E2E evidence. Zero automatic promotions without valid compatibility evidence for the current baseline and G3 qualification. Approval cannot waive common gates. | SM-5 |
| SM-C2 | **Unauthorized cross-environment access.** Zero successful production operations, data or secret access, or administrative changes in the staging-only user, workload and automation negative checks, including restored copies. Shared capacity savings must preserve isolation. | SM-4 |
| SM-C3 | **False recovery success.** Zero recoveries reported successful without the required readiness, smoke-test and data checks. A shorter recorded recovery time must not omit operator or provisioning time or hide data loss. | SM-5, SM-6 |
| SM-C4 | **Destructive or incomplete cleanup.** Zero deletions of another environment's resources. No cancelled or finished CI run is counted as cleaned up while resources it owns remain. | SM-3 |
| SM-C5 | **Hidden startup delay.** Actual startup duration and any explicit timeout overrides are recorded alongside readiness results. Raising a timeout is never reported as improved startup performance. There is no setup-time improvement target. | SM-1, SM-3 |

## Scope notes

- There is no numeric uptime, latency or throughput target. Administrator revisits this if external adoption or operating evidence calls for a stronger commitment.
- Acceptance is expressed as capability and acceptance scenarios. Module business journeys stay with the modules.
- Incremental enrollment supports implementation; final MVP acceptance still requires all seven modules in the agreed environments.
