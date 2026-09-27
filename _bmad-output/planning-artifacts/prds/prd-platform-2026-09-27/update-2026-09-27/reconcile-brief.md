# PRD update: original brief reconciliation

**Input:** [Product Brief: Hexalith Platform](../../../briefs/brief-platform-2026-09-27/brief.md).

**Targets:** updated [PRD](../prd.md) and [addendum](../addendum.md), checked on 2026-09-27.

**Verdict:** no material omission or unresolved contradiction with the original brief. Broader original statements are appropriately qualified by later accepted coaching and architecture decisions. Requirements remain acceptance targets; the update does not claim deployed capability.

| Source concern | Current destination | Reconciliation |
| --- | --- | --- |
| Common solution for development, testing, staging and production; shared hosting in Platform | PRD Vision, scope and features; addendum hosted architecture table | Preserved. Domain behavior stays module-owned. |
| Aspire host and complete local environment | PRD FR-1, FR-4, SM-1 | Preserved for all seven modules and supporting resources. Caller-hosted McpCli demonstrates availability through connection and invocation. |
| Existing optional Works preview is insufficient | PRD Vision; addendum Existing implementation context | Preserved as historical baseline, without claiming a fresh inspection or completed integration. |
| EventStore, Tenants, Parties, Folders, Projects, McpCli, Memories MVP; longer-term all Hexalith | PRD Confirmed MVP scope and FR-1 | Preserved. Incremental enrollment does not reduce final seven-module acceptance. |
| Minimum module workspace with Platform direct submodule and active checkout debugging | PRD FR-2/FR-3, SM-2; addendum workspace constraints | Preserved with accepted workspace qualification: domain modules use their own workspace; EventStore/Memories/McpCli use source in Platform. This is the later explicit decision in log entry 66, not a brief omission. |
| Root-only direct references; no nested initialization | PRD FR-2; addendum Workspace and build constraints | Preserved. Architecture resolves source mapping and rejects fallback/identity conflicts. |
| Local Debug/project references; CI Release/NuGet | PRD FR-2/FR-4; addendum workspace constraints | Preserved for active and directly declared source. Accepted architecture uses catalog packages for other dependencies; this qualification is not a return to the preview's sibling/nested resolution. |
| Local and automated CI tests use Platform environments | PRD FR-4/FR-5/SM-3 | Preserved with later real-service, readiness, ownership, cleanup and independent-isolated-test policies. |
| Kubernetes at 192.168.1.30; staging hexalith.com; production tache.ai | PRD scope, FR-10 and glossary; addendum hosted table | All literal location/domain constraints retained. Address is not evidence of topology or availability. |
| Separate staging/production data and credentials | PRD FR-10/FR-11/NFR-3/SM-4 | Preserved and made testable for users, workloads, automation and recovery copies. Shared Keycloak remains permitted. |
| Automatic staging-to-production release and previous-working rollback | PRD release modes/G1–G3, FR-6–FR-8/NFR-1, SM-5 | Preserved for qualified compatible automatic releases. Accepted architecture adds Administrator-approved pre-G3/incompatible paths and first-deployment/no-mutation cases, while keeping the staging gate in both modes. The original universal shorthand is superseded, not an unresolved contradiction. |
| McpCli accesses module operations locally and hosted through EventStore | PRD FR-12, SM-1/SM-4; addendum McpCli context | Preserved with accepted agent-eligibility, connected contract matching and caller-hosted CLI/stdio placement. UI-only/confirmation operations remain in their module UI. |
| Five demonstrable success outcomes | PRD SM-1–SM-5 | All covered. SM-6 captures the subsequent accepted disaster-recovery outcome; counter-metrics guard false success. |
| Open questions: minimum dependencies/doubles, release checks/Builds, recovery/sharing, source/deployment mechanisms, CI lifecycle and McpCli hosting | PRD relevant FR/NFRs and downstream readiness; addendum accepted mechanisms/options | Product decisions are resolved; architecture mechanisms recorded; concrete qualification evidence stays owned and gated. |

## Gaps and dispositions

No actionable source-reconciliation gap. No qualitative vision, simplicity preference, user context, environment or scope item was dropped. The existing downstream row to synchronize architecture/spec with the newly selected deputy and RTO policy remains appropriate; it is not missing content from this brief.

External pages and infrastructure were not revalidated in this document-to-document extraction.
