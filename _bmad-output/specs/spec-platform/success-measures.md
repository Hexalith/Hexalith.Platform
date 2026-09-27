# Success Measures

These are acceptance targets; none has been achieved yet.

- **Ownership:** Platform owns the combined acceptance evidence. Module developers supply their readiness, operation and critical-flow checks. Administrator owns the recovery evidence.
- **Recording:** every demonstration records the release or source revision, environment, result and supporting diagnostics.

| ID | Measure | Validates | When |
| --- | --- | --- | --- |
| SM-1 | **Complete local environment.** All seven MVP modules and their required supporting resources become ready within the effective startup deadline. CLI and MCP demonstrations run enabled modules' agent-eligible commands and queries with the expected results. | CAP-1, CAP-12 | MVP acceptance |
| SM-2 | **Module workspace development.** Each domain-module workspace (Tenants, Parties, Folders, Projects) shows its developer-defined minimum composition, and a breakpoint or source change in the active checkout affects the running module. EventStore, Memories and McpCli show the same source-debugging outcome from the Platform workspace. Only dependencies declared directly by the root are initialized. | CAP-2, CAP-3 | MVP acceptance |
| SM-3 | **Test execution and lifecycle.** Local Aspire and CI integration runs pass their module-defined checks. Controlled failure, startup-timeout and cancellation scenarios show the agreed retention and cleanup, with accessible diagnostics and no effect on another environment. | CAP-4, CAP-5 | MVP acceptance; after lifecycle changes |
| SM-4 | **Hosted access and isolation.** Staging and production expose the MVP interfaces and McpCli operations. Every isolation check passes: production rejects staging-only users and credentials and permits explicitly authorized production users. | CAP-10–CAP-12, NFR-3 | Completes during the G2 opening order ([sequencing.md](sequencing.md)); after access-configuration changes |
| SM-5 | **Promotion and recovery.** Every promoted release has passing results for all required staging E2E checks. Controlled failure rehearsals fire the CAP-7 triggers and show a successful rollback within budget, with data preserved (NFR-1). Separate scenarios show accurate reporting of failed or unverified recovery and of first-deployment failure, with no repeated attempts. GitHub delivery to Administrator is verified. | CAP-6–CAP-8, NFR-1 | Before G3; after recovery-policy changes |
| SM-6 | **Recoverable production data.** Drills show RPO ≤ 1 h and RTO ≤ 4 h, including data and smoke-test validation. Backup cadence and retention, and the GitHub failure and freshness alerts, are verified. Recovery-point age is monitored between drills. | CAP-9, NFR-2 | Before production use (G2); monthly; after material backup or storage changes |

## Counter-metrics

| ID | Guard | Counterbalances |
| --- | --- | --- |
| SM-C1 | **Bypassed validation.** Zero releases promoted with failed, skipped, missing or wrong-release E2E evidence. Promotion must never get faster by skipping checks. | SM-5 |
| SM-C2 | **Unauthorized cross-environment access.** Zero successful production operations or data accesses in the staging-only negative checks. | SM-4 |
| SM-C3 | **False recovery success.** Zero recoveries reported successful without the required readiness, smoke and data checks. Recorded recovery time includes operator and provisioning time and discloses any data loss. | SM-5, SM-6 |
| SM-C4 | **Destructive or incomplete cleanup.** Zero deletions of another environment's resources. No finished or cancelled CI run is counted as cleaned up while resources it owns remain. | SM-3 |
| SM-C5 | **Hidden startup delay.** Actual startup duration and any explicit timeout overrides are recorded alongside readiness results. Raising a timeout is never reported as faster startup. There is no setup-time improvement target. | SM-1, SM-3 |

## Scope notes

- There is no numeric uptime, latency or throughput target. Administrator revisits this if external adoption or operating evidence calls for a stronger commitment.
- Acceptance is expressed as capability and acceptance scenarios. Module business journeys stay with the modules.
