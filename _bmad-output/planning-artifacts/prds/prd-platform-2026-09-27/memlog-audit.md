# Decision-log audit

Audited on 2026-09-27 during PRD finalization. Entry numbers below count the append-only bullets in `.memlog.md`; historical proposals are not current requirements unless subsequently accepted.

| Entries | Disposition | Location or reason |
| --- | --- | --- |
| 1–5 | Captured; workflow details set aside from product requirements | PRD purpose, internal-team scope, capability-first organization; activation and coaching choices remain in the log. |
| 6–7 | Captured | Vision, MVP scope, FR-1–FR-3, FR-6, FR-8, FR-10; addendum preserves source/build and hosting context. |
| 8–9 | Supporting history | Drafting and research events; `extract-platform-brief.md` and `research-landscape.md` preserve supporting work. |
| 10–12 | Captured | FR-3 and the Parties configuration example in the addendum; configuration schema remains downstream. |
| 13–14 | Superseded proposal, rationale retained | Real-service versus test-double alternatives in the addendum; entries 15–16 select the policy. |
| 15–17 | Captured | FR-3–FR-5, MVP non-goals, and testing rationale in the addendum. |
| 18–20 | Captured | FR-6 requires module-owned critical-flow E2E checks for the promoted release. |
| 21–23 | No new requirement | Ambiguous message `3` was explicitly withdrawn; module flow ownership remains captured in FR-6. |
| 24–25 | Superseded proposal, rationale retained | Production rollback alternatives in the addendum; entries 26–28 select the policy. |
| 26–29 | Captured | FR-7–FR-8, NFR-1, rollback rationale and accepted thresholds. |
| 30–33 | Captured | Administrator recovery ownership and GitHub notifications in roles, glossary, FR-8–FR-9, and addendum; account/delivery configuration assigned downstream. |
| 34–35 | Superseded proposal, rationale retained | Disaster recovery alternatives in the addendum; entries 36–37 select Option 2. |
| 36–38 | Captured | FR-9, NFR-2, SM-6, and backup/restore rationale; targets remain requirements awaiting infrastructure evidence. |
| 39–42 | Captured | FR-4 local Aspire, failed-local retention, CI cleanup, and lifecycle implementation handoff. |
| 43–44 | Captured | FR-10–FR-11 and NFR-3 permit shared supporting infrastructure with isolated application data/credentials and explicit production access. |
| 45–46 | Captured | FR-11, NFR-3, and hosted architecture handoff require reuse of the existing common Keycloak server. |
| 47–48 | Captured | FR-12 and McpCli context cover enabled module commands/queries with selected-environment permissions. |
| 49–50 | Superseded proposal, rationale retained | Test readiness/cancellation alternatives in the addendum; entries 51–52 select the policy. |
| 51–52 | Captured | FR-1/FR-4 and addendum: module readiness, bounded startup, finite overrides, run-owned cancellation cleanup, retained diagnostics. |
| 53 | Captured | SM-1–SM-6, countermetrics, downstream owners and revisit conditions formalize accepted requirements without new performance targets. |
| 54 | Captured | Capability-based structure; module business journeys remain module-owned; no inferred uptime or throughput commitment. |
| 55 | Workflow history | Finalization sequence, retained in the log rather than product requirements. |

All product decisions through entry 55 have a destination. Open implementation mechanisms and readiness evidence are assigned in the PRD's downstream table; neither this audit nor final document status establishes that the platform has been implemented or verified.

Subsequent finalization entries record the user's selection of both reviews, clean source reconciliations, review corrections, the assigned successful-local-cleanup decision, editorial polish, and final document checks. Product changes are captured in FR-6–FR-9, SM-5, the downstream table, and the addendum; `review-resolutions.md` records their disposition. Review/workflow events remain audit history rather than product requirements.
