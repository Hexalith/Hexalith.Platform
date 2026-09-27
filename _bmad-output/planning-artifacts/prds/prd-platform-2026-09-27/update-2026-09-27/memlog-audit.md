# PRD update: decision-log audit

Audited on 2026-09-27 against the updated [PRD](../prd.md), [addendum](../addendum.md), and the first 77 entries of [.memlog.md](../.memlog.md). Entry numbers below count each bullet in order; frontmatter is excluded. The audit is a source-accounting exercise, not runtime verification or a new product decision.

**Verdict:** all substantive decisions and changes have a current destination or an explicit historical/superseded disposition. No unaccounted product requirement was found. Later accepted decisions govern the differences identified below; they are not omissions to restore from the original brief.

## Decisions and changes

| Entry | Substantive content | Destination and disposition |
| --- | --- | --- |
| 2 | Use the brief and its addendum as source scope. | PRD Document purpose links both inputs; this audit and the two reconciliation reports account for them. Later coaching and accepted architecture supplement that source scope. |
| 3 | Internal team platform stakes. | PRD Document purpose, Vision, Target users and jobs; no external launch promises added. |
| 4 | Coaching mode, preserve decisions. | Set aside as workflow history; decisions remain in the canonical log and supporting options in the addendum. This is not a product requirement. |
| 5 | Vision + Features entry point. | PRD retains vision, users, grouped features and stable requirements. The coaching order itself is workflow history. |
| 6 | Seven modules, all environments, Platform hosting/module behavior ownership. | PRD Vision, Confirmed MVP scope, FR-1, FR-3–FR-6, FR-10, FR-12; addendum Hosted architecture questions. |
| 7 | Active checkout, direct root submodules, local/CI asset modes, hosting/domains, isolation, automatic promotion/recovery. | PRD FR-2, FR-6–FR-8, FR-10, NFR-1/NFR-3. Automatic release and source policies are qualified by later accepted architecture, not silently omitted; see entries 66 and 70–74. |
| 8 | Drafted sourced vision, scope, initial local FRs and outcomes; preserve dated technical context. | PRD Vision, scope, FR-1–FR-3 and SM-1–SM-6; addendum Existing implementation context. Early omissions reflected then-unresolved coaching, not current gaps. |
| 10 | Module developer owns minimum server list; domain baseline EventStore/Tenants/Memories. | PRD FR-3 and glossary; addendum Module-owned configuration example. |
| 11 | Parties root directly references EventStore/Tenants/Memories/Platform; configuration selects servers. | PRD FR-3; addendum Module-owned configuration example. Concrete schema was originally unresolved and is now architecture-owned qualification work. |
| 12 | Incorporated dependency ownership and baseline; doubles remained open. | PRD FR-3 and glossary capture the baseline; entry 15 resolves the historical test-double question. |
| 14 | Preserve three testing options and pragmatic recommendation. | Addendum Testing options for module dependencies preserves how each works, benefits, costs and selected recommendation. Proposal state superseded by entry 15. |
| 15 | Real Platform environments; module isolated tests may use doubles and cannot replace integration checks. | PRD FR-3–FR-5, MVP non-goals and glossary; addendum selected testing option 3. |
| 16 | One server list, isolated tests before integration, readiness, isolation/cleanup, asset rules, no generic mocking. | PRD FR-2–FR-5 and MVP non-goals; addendum testing MVP approach. |
| 17 | Adopt test policy; readiness/cleanup details still open at that time. | PRD FR-3–FR-5; entries 39, 51, 52 and accepted lifecycle architecture resolve the historical open details. |
| 18 | Green staging critical-flow E2E gate. | PRD FR-6, SM-5, SM-C1. Later architecture extends the same gate to both release modes. |
| 19 | Failed/skipped/missing release E2E results block promotion. | PRD FR-6 and SM-C1; wrong-release, stale and incomplete results are also rejected in the update. |
| 20 | Each submodule owns critical-flow definitions. | PRD staging-feature introduction, FR-6 and downstream module-declaration ownership. Technical modules declare essential behavior. |
| 22 | Delegate flow enumeration to modules and revisit on integration. | PRD FR-6 closing paragraph and downstream table; no central invented flow list. |
| 25 | Preserve proposed rollback options and thresholds. | Addendum Production rollback policy and rationale: comparison, failure thresholds, recovery and limits. Proposal state superseded by entries 26–28. |
| 26 | Readiness + safe module smokes; 10-minute rollout, 5-minute verification, 60-second unavailability, two failures 30 seconds apart. | PRD FR-7; addendum Failure triggers and initial thresholds. All accepted values preserved. |
| 27 | One previous-working recovery attempt; 10+5-minute recovery; stop promotions/report; manual intervention for first/failed/unverified recovery. | PRD FR-8, SM-5 and glossary; addendum Recovery and its limits. Current-security configuration and durable stops refine the original shorthand. |
| 28 | Safe smokes; preserve data/events/rotations; previous-version compatibility; defer traffic triggers; values are targets. | PRD FR-7, NFR-1, MVP non-goals and success-measure qualification; addendum smoke and recovery rationale. |
| 29 | Incorporate rollback policy and leave owner/disaster expectations open. | PRD FR-7/FR-8/NFR-1 capture policy; entries 30–38 resolve original owner and disaster-recovery questions. |
| 30 | Administrator is notification recipient and recovery owner. | PRD Target users, FR-8, glossary; extended by accepted deputy entry 71 without transferring Administrator-only powers. |
| 31 | Capture Administrator and ask notification channel. | PRD Target users/FR-8/glossary; channel question resolved in entry 32. |
| 32 | GitHub notifications; concrete account/delivery qualified before production. | PRD FR-8/FR-9, G1–G2, downstream readiness; addendum GitHub notification integration. Architecture selects assigned issues; actual delivery remains evidence work. |
| 33 | Capture channel and downstream configuration. | Same destinations as entry 32; no notification was sent by this document reconciliation. |
| 35 | Preserve disaster-recovery alternatives and proposed Option 2 targets/policy. | Addendum Disaster recovery approach and alternatives contains mechanisms, tradeoffs and recommendation. Proposal state superseded by entries 36–37 and coverage entry 72. |
| 36 | Select independent backups/tested restore; one-hour RPO and four-hour outage-to-restoration RTO including response/capacity. | PRD FR-9/NFR-2/SM-6; addendum selected disaster-recovery approach. **Partially superseded:** entry 72 makes four-hour applicability depend on declared coverage. The original outage clock, included work and continuous RPO remain. |
| 37 | 30-minute backups, 7-day frequent/30-day daily retention, module inventory, GitHub freshness alerts, drills and independent whole-site recovery. | PRD FR-9/SM-6, G2 and downstream recovery work; addendum selected disaster-recovery implementation scope. Accepted architecture supplies concrete independence and recovery-set semantics. |
| 38 | Incorporate FR-9/NFR-2 and defer feasibility to architecture/qualification. | PRD FR-9/NFR-2 and readiness table; addendum Selected approach and operational scope. Mechanisms are now accepted architecture; runtime feasibility still requires evidence. |
| 39 | Failed local environments retained; CI cleaned on completion/failure/cancellation. | PRD FR-4/SM-3; addendum testing and lifecycle rationale. |
| 40 | Include partial provisioning and environment-scoped cleanup; startup/cancellation open then. | PRD FR-4; entries 51–52 and accepted runner lifecycle resolve the earlier open questions. |
| 41 | Local integration uses Aspire; isolated tests remain independent. | PRD Local and CI testing, FR-4/FR-5; addendum Testing options. |
| 42 | Capture Aspire and leave lifecycle implementation/CI hosting to architecture. | PRD FR-4 and downstream runner qualification; addendum records accepted Platform runner and disposable GitHub-hosted CI hosting. Earlier design deferral is resolved. |
| 43 | Shared supporting infrastructure allowed; data/credentials isolated; explicit production users. | PRD scope, FR-10/FR-11/NFR-3; addendum hosted architecture table. |
| 44 | Capture hosting/access/isolation requirements. | PRD FR-10/FR-11/NFR-3, SM-4 and glossary. Mechanisms subsequently selected by architecture. |
| 45 | Reuse existing common Keycloak; preserve explicit production admission and environment isolation. | PRD hosted-feature narrative, FR-11/NFR-3; addendum Identity and admission. Separate realms are accepted architecture, not a newly inferred product choice. |
| 46 | Capture Keycloak and retain authorization requirement. | Same destinations as entry 45; former mechanism questions now addressed by architecture with implementation qualification outstanding. |
| 47 | McpCli exposes enabled module commands/queries with selected-environment permissions in all contexts. | PRD FR-12, SM-1/SM-4 and McpCli glossary; accepted entry 66 limits execution to agent-eligible operations. |
| 48 | Capture McpCli capability and contexts. | PRD FR-12 and addendum McpCli context; transport/placement are now accepted caller-hosted CLI/stdio architecture. |
| 50 | Preserve readiness/cancellation options with recommendation and retained diagnostics. | Addendum Test readiness and local cancellation rationale preserves options, mechanisms and tradeoffs. Proposal state superseded by entries 51–52. |
| 51 | Module readiness, default finite 10-minute startup, explicit finite overrides, missing evidence fails, unready diagnostics. | PRD FR-4/SM-1/SM-3/SM-C5; addendum Readiness option 3. Production budgets remain separately defined. |
| 52 | Local cancellation cleans owned resources; independent Aspire untouched; failures retained; CI cleans; logs survive. | PRD FR-4/SM-3/SM-C4; addendum Selected behavior and Cancellation option 1. First terminal outcome clarifies conflict resolution. |
| 53 | Adopt startup/cancellation, SMs/counter-metrics and owned downstream decisions. | PRD FR-1/FR-4, Success measures and downstream readiness table; addendum lifecycle rationale. |
| 54 | Capability acceptance instead of invented journeys; no uptime/latency/throughput target; revisit with Administrator. | PRD downstream closing paragraphs and capability-based success measures; module journeys remain module-owned. |
| 56 | Select quality rubric and focused recovery/access review for original finalize. | Set aside as historical review instruction fulfilled by prior review artifacts; current user selection in entry 75 requests all update checks. No product requirement arises from review menu choice. |
| 59 | Fail closed on missing/empty critical-flow/smoke declarations; require demonstrated rollback success and failure reporting; restored-release checks. | PRD FR-6–FR-8 and SM-5; addendum failure/recovery rationale. |
| 60 | Recovery inventory includes shared Keycloak/production authority; dependency owners; restore access positive/negative checks. | PRD FR-9/NFR-3/SM-6 and downstream inventory ownership; addendum recovery scope/authority and hosted table. |
| 61 | Defer successful local-test cleanup to architecture; preserve settled lifecycle rules. | **Resolved deferral:** accepted AD-5/AD-10 now select automatic cleanup after local success; PRD FR-4 and addendum testing/selected behavior capture it. Not a contradictory new product choice. |
| 63 | Structure/prose polish without changing policy or option depth. | PRD scope/non-goals before features and glossary at end; addendum selected policies plus retained alternatives. Historical editorial action, substantively preserved. |
| 66 | Accepted architecture alignment: McpCli eligibility, domain workspaces vs technical sources in Platform, named deputy. | PRD FR-2/FR-12, roles/glossary, SM-1/SM-2; addendum workspace/McpCli/recovery context. **Overrides broader original shorthand** for all-module workspaces and unrestricted module-operation exposure. |
| 67 | Apply entry 66 to stable FR/SMs and addendum. | Same current destinations as entry 66. Subsequent detailed deputy authority is now explicit under entry 71. |
| 70 | Align with validation/architecture, preserve IDs and keep implementation out of main requirements. | PRD Document purpose, unchanged FR/NFR/SM numbering and capability-level consequences; addendum carries architecture mechanisms and history. |
| 71 | Deputy alerts, own access, restore/verify/reopen; Administrator resumes promotion and retains production-user administration. | PRD Target users, FR-8/FR-9/FR-11, SM-5/SM-6 and glossary; addendum Recovery deputy authority compares options and records selected recommendation. Architecture-access/notification alignment is an explicit owned downstream item. |
| 72 | Four-hour RTO within declared coverage; full outage clock always; continuous RPO/backup monitoring; declared hours/acknowledgement proved before G2. | PRD NFR-2/FR-9/SM-6/G2 and downstream table; addendum Response coverage and the four-hour target. Explicitly supersedes the unconditional reading of entry 36. |
| 73 | Update release/enrollment/gates, approved mode, compatibility, lifecycle, interruption/stops, security recovery, off-site sets/erasure, automation, McpCli, source and chosen recovery policy. | PRD release-scope table, FR-2/FR-4/FR-6–FR-12, NFR-1–NFR-3 and SMs; addendum architecture/recovery/lifecycle sections. All remain qualification requirements, not deployed claims. |
| 74 | Reconcile proposals rather than adopt all; retain declared-cut age, G2 vs G3, incompatible-release recovery; avoid invented commitments. | PRD FR-9/NFR-2 declared cut, production-entry gates, FR-6/FR-8 approved recovery, downstream no-uptime statement. No arbitrary timeout cap, quota or extra channel added. |
| 75 | Align addendum, preserve old options and selected deputy/RTO alternatives; run all final reviews. | Addendum updated source/lifecycle/transport/identity/release/recovery sections and two explicit option tables. All-check instruction is current workflow scope; completion belongs to the parent finalization pass. |
| 76 | Distinguish technical-module enrollment from repository state, require per-module minimum evidence, restrict pre-G2 synthetic admission and own declaration-removal review. | PRD FR-2/SM-2, production-entry gate explanation, FR-6 and downstream qualification table; addendum historical context remains qualified. These clarify acceptance and evidence without changing IDs or thresholds. |
| 77 | Assign implementation-evidence owners and revisit gates; distinguish them from unresolved product policy or proven readiness. | PRD downstream table and corresponding FR/G1–G3 clauses; addendum Remaining qualification and ownership. Concrete schedule/access/capacity/settings remain implementation prerequisites. |

## Events and superseded workflow states

| Entries | Disposition |
| --- | --- |
| 1, 9 | Activation/extraction/research provenance retained in the log and original artifacts; not new requirements. Historical repository observations remain labeled in the addendum. |
| 13, 24, 34, 49 | Requests for pragmatic alternatives fulfilled in addendum testing, rollback, disaster recovery, and readiness/cancellation comparisons. No unanswered product choice remains from these events. |
| 21, 23 | Withdrawn message “3” correctly has no product consequence. |
| 55, 57, 58, 62 | Prior finalization/audit/reconciliation/review progress is history. It does not substitute for this update's review. |
| 64, 65 | Prior final status and checks refer to the earlier revision. Current draft status during update is intentional; finalization must record fresh completion. |
| 68 | Validation report is a critique source. Current corrections reconcile it with accepted architecture; a proposed finding does not automatically override decisions. |
| 69 | Update activation and clarification are workflow history; entry 70 supplies the change mandate. |

## Follow-through already assigned

No missing PRD requirement was identified. Existing downstream work remains visible: amend architecture operational access/notification wording for the deputy and synchronize spec wording; declare coverage and acknowledgement limits; then prove the relevant G1–G3 evidence. These are assigned implementation/document maintenance prerequisites, not grounds to resurrect superseded requirements or claim the production gates have passed.
