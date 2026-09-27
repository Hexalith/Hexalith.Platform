# Architecture reconciliation — Platform PRD update

Date: 2026-09-27. Scope: read-only reconciliation of product documents against accepted architecture, including recovery and access consistency. Only this reconciliation report was written.

**Verdict: PASS for product-document alignment after the pre-G2 test-admission clarification.** No blocking PRD/addendum gap remains. Document reconciliation does not demonstrate implementation or production qualification. Existing numeric budgets, scope and stable FR/NFR/SM identifiers are preserved.

## Inputs and precedence

- [Current PRD](../prd.md) and [supporting addendum](../addendum.md).
- [Accepted architecture spine](../../../architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md), final update, AD-1 through AD-15.
- [Architecture decision log](../../../architecture/architecture-platform-2026-09-27/.memlog.md), especially accepted update themes and final gate decisions at lines 173–203.
- [Architecture update gate](../../../architecture/architecture-platform-2026-09-27/reviews/update-2026-09-27/gate-summary.md): document handoff PASS, explicitly distinct from production qualification.
- [PRD decision log](../.memlog.md): later explicit user choices for deputy authority and RTO response coverage govern older source wording.

The latest accepted spine governs its older exploratory log entries. In particular, the final named Memories adapters supersede the earlier FalkorDB-only exception; executor/target enforcement on GitHub Free supersedes earlier reliance on private GitHub Environments; and the off-site monitor plus hourly dead-man check supersedes GitHub scheduling as the primary monitor. The update uses these final decisions rather than combining incompatible historical alternatives.

## Applied alignment

| Accepted architecture input | Where captured | Reconciliation |
| --- | --- | --- |
| AD-1/AD-13: Platform composition and one composed EventStore host; module behavior stays module-owned (spine lines 25–27, 57, 129) | PRD scope/FR-1; addendum release identity and hosted decisions | Product outcomes stay in the PRD; host packaging, extension contracts, ordinary Aspire helpers and exporter fallback stay in the addendum. No live capability is inferred. |
| AD-2: retained artifacts and evidence identity (line 63); staging rules (168) | FR-6, SM-5, SM-C1; addendum release identity | Both modes require exact-release E2E evidence for the enrolled composition. Retained artifacts, suite/profile/configuration identity and evidence freshness are explicit. |
| AD-3/AD-15: current environment authority, prepared recovery combination, candidate-to-baseline rehearsal (69, 141, 150–154) | FR-7/FR-8, NFR-1, SM-5; addendum release/recovery sections | Recovery covers partial and routing-only changes, preserves current security/data and needs compatibility with candidate-written state/events. Retirement cannot invalidate recovery. Forward catalog mechanism remains technical context. |
| AD-4: active-root source resolution (75) | FR-2/SM-2; addendum workspace/build | Directly declared source dependencies and other catalog-pinned packages are distinguished; missing source and duplicate identities fail without alternate-checkout fallback. Submodule/tool identity is preserved in the addendum. |
| AD-5/AD-10: disposable CI and one run owner (81, 111, 156) | FR-4/FR-5/SM-3; addendum lifecycle | Fresh environments, success cleanup, failed-local retention, first-terminal-outcome precedence, compatible explicit attach, hosted-attach refusal, finite readiness and exact cleanup are captured. |
| AD-6/AD-8: separate identity and application state, shared capacity only with enforced boundaries (87, 99) | FR-10/FR-11/NFR-3/SM-4; hosted addendum | Administrator grants production admission; staging membership, clients or automation cannot confer production authority. Negative tests include pods, automation, direct access, restored copies and hostname claims. |
| AD-7: isolated execution authority, independent recovery executor and private operations (93, 155) | FR-4/NFR-3; addendum hosted decisions and owners | CI has no hosted credentials. Separate staging/production executors, per-environment credentials, public publication/private deployment and GitHub Free controls remain implementation constraints. |
| AD-9: bounded provider adapters and module state classes (105) | Addendum runtime integration and recovery classification | Named Memories exceptions and remaining coordination migration are captured without claiming a general direct-provider exemption or blocking local/CI enrollment on the G2 conformance gap. |
| AD-11/AD-14: caller-hosted McpCli, environment contract match and authenticated surface/actor (117, 135) | FR-1/FR-12, SM-1/SM-4; McpCli addendum | Offline discovery does not imply availability; empty/disabled/mismatched/ineligible operations cannot pass acceptance. Human CLI use does not bypass agent restrictions. Other MCP hosts remain outside MVP composition. |
| Two production modes and G1/G2/G3 (169, 176) | PRD release scope/FR-6/SM-5; addendum gate table | Approved mode preserves common gates; incompatible changes name recovery. G2 opens user service after recovery/isolation evidence; G3 enables automation after SM-5. Both documents now explicitly allow restricted synthetic test admission after initial G1 and before G2. |
| Attempt ownership/interruption/stop (170–175) | FR-6 through FR-8, SM-5; addendum recovery | Serialization, durable timers, no replenished recovery attempts, uncertain-state stop, durable promotion stop and verified working baseline are explicit. Administrator alone clears promotion stop. |
| AD-12 complete recovery set, prepared capacity and off-site custody (123, 177–181) | FR-9/NFR-2/SM-6; disaster recovery addendum | One declared cut, complete chains, integrity/decryption, independently available artifacts/keys, quarantined restoration and representative drills are captured. Backup schedule alone is never recoverability evidence. |
| Monitoring and GitHub delivery (159, 178–179; log 201) | FR-8/FR-9, SM-5/SM-6; notification and recovery addendum | Five-minute availability, 15-minute freshness and hourly silence detection are retained. Alerts survive primary loss. The later user decision adds deputy delivery. |
| Security/erasure and external effects (180–183; log 189–193) | FR-9/NFR-1/NFR-3; recovery authority addendum | Memories acknowledged tombstones and erased keys never resurrect; unknown lineage fails closed. Post-cut revocations, restored credential rotation and delayed destructive/external-effect resumption are included. The accepted non-Memories lost-window exception is explicit. |
| Module envelope precedence and owned qualification (187, 290–328) | NFR-2/downstream owners; addendum hosted/qualification sections | Stricter Projects/Folders infrastructure promises do not silently replace Platform targets. Functional, authorization, erasure and module release gates remain. No HA, standby or uptime percentage was added. |

The addendum preserves original testing, rollback and disaster recovery option comparisons and labels historical implementation observations. Its old statements that architecture must choose already-settled mechanisms have been replaced. Scope remains all seven MVP modules; incremental qualification does not redefine final MVP acceptance.

## Later user decisions and intentionally superseded wording

### Deputy restores service; Administrator resumes promotions

The latest user decision grants the named deputy independent recovery/key access, alert receipt, documented recovery execution, verification and reopening. Only Administrator clears the promotion stop or administers production admission. PRD roles, FR-8, SM-5, downstream work and glossary agree; the addendum preserves the alternative of broader deputy release authority as an unselected option.

This deliberately extends older architecture wording that merely names the deputy for backup/key custody and routes all notifications to Administrator. The addendum and PRD assign the architecture access/notification amendment before dependent stories and reliance on deputy coverage. This is an owned source-alignment task, not a reason to erase the selected product decision. Recovery reopening must not wait for Administrator to clear the promotion stop: reopening restores service, while the stop continues to prohibit new promotion.

The choice grants neither general operations-repository write access nor production-user administration. If implementing deputy access introduces a second operations-repository writer, the accepted GitHub Team review is triggered. Existing minimal identity, permission and runbook mechanisms are sufficient; no custom workflow system is required.

### Four hours for outages beginning within declared response coverage

The latest decision makes the architecture's declared response coverage explicit in NFR-2: the four-hour objective applies when an outage begins within coverage and remains applicable if coverage ends during that incident. Detection, worst-case acknowledgement/response, capacity, restoration and validation all consume the original outage clock. Outside coverage no four-hour commitment is made, but complete duration and coverage status remain reported. RPO, backup protection and monitoring are continuous.

This supersedes any reading of the earlier unqualified four-hour PRD target as continuous response coverage. It does not reset, pause or start the measurement at operator arrival. Addendum Option 1 is selected and continuous-response Option 2 remains an alternative. Actual hours, time zone and acknowledgement bounds remain owned pre-G2 qualification details.

## Focused recovery and access consistency

| Check | Result |
| --- | --- |
| Deputy can reopen without waiting for Administrator | Pass. FR-8 explicitly permits verified reopening while leaving the durable promotion stop set. Addendum authority split agrees. |
| Reopening cannot grant new production admission | Pass. FR-11 and both deputy descriptions reserve production admission administration to Administrator. Reopening restores previously authorized service. |
| No blind rewind of security or durable data | Pass. FR-8/NFR-1 distinguish prior application from current environment authority; FR-9 replays revocations and rotates restored credentials before reopening. |
| Memories recovery does not use ordinary RPO to lose erasures | Pass. FR-9/addendum make acknowledged tombstones/key destruction stronger than ordinary data RPO and fail closed on unknown lineage. |
| Other modules' lost-window exception is visible | Pass. The exception is stated in FR-9/addendum and requires reporting/reconciliation before destructive retention or external effects resume. |
| Isolated drill cannot mutate live production or shared authority | Pass. FR-9 requires isolated copies and recorded substitutions; addendum requires the same. |
| RTO coverage is measurable and does not hide outage duration | Pass. NFR-2 defines start-time applicability and continuing coverage for an incident; full duration is always recorded and RPO/monitoring remain continuous. |
| Positive access verification can happen before G2 without general admission | Pass after clarification. G1 initially has no admitted users; Administrator may then temporarily admit the designated synthetic test identity while ingress remains executor/probe-only. Its grant is revoked after verification, general users remain excluded, and drills use isolated identity copies. |

## Resolved finding and remaining source alignment

### A-1 — Restricted pre-G2 positive-admission test — resolved

**Initial priority: high for handoff consistency; resolved by a clear wording fix.** Sources: PRD release-scope G1/G2 table and following paragraph, FR-11, SM-4, downstream qualification; addendum G1/G2 table and following paragraph; architecture production gates (line 176) and synthetic identities (158).

The initial draft required an explicitly admitted positive production test before general access without saying when that admission was allowed under G1's group-empty rule. Different implementers could have omitted positive verification or opened ordinary admission prematurely.

The parent added and this reconciliation verified matching text in both product documents: G1 begins empty; Administrator temporarily admits a designated synthetic identity for SM-4; ingress stays restricted to executor/probe sources; no general users are admitted; the test grant is revoked afterward; recovery drills use isolated identity copies. This resolves the product-document gap without bypassing G2.

Two source amendments remain owned by Platform architecture with Administrator before dependent implementation stories are finalized:

1. Update architecture operational access and notification recipients for the selected deputy recovery/reopening authority while preserving Administrator-only promotion resumption and production admission administration.
2. Carry the restricted pre-G2 synthetic-admission step into the architecture beside its initial empty-group G1 rule, preserving closed ingress and isolated drill authority.

These are explicitly assigned source-alignment tasks, not unresolved product decisions. The source update should also carry NFR-2's precise start-of-outage coverage rule so implementation uses one definition.

## Remaining evidence and gates

- **Before enrollment/local acceptance:** declaration and shared contract versions, active-root mapping, descriptor/runner lifecycle, composed-host extension API and source/package parity. Responsible: Platform, Builds and module owners.
- **Before hosted qualification/G1:** supported infrastructure, current security patches, protected ingress/DNS/certificates, environment-specific identity/data/secrets, executor controls and independent monitoring with actual delivery. Responsible: Administrator, Platform and dependency owners.
- **Before G2:** response coverage and deputy access, prepared replacement capacity and independent custody, complete recovery inventory, tested security/isolation, Memories continuity and a representative timed drill. Responsible: Administrator/deputy, Platform, Memories/EventStore and dependency owners.
- **Before applicable releases/G3:** exact retained artifacts, approved check/evidence contracts and finite age policy, current-baseline compatibility, EventStore retention/catalog/lifecycle confirmations, interruption/serialization, one recovery, durable stop and notifications. Responsible: Platform, Builds, EventStore and Administrator.
- **Ongoing:** monthly recovery drills, material-change repeats, continuous backup/freshness protection, supported profile currency and module service qualification. Final document status passes none of these gates by itself.

No architectural mechanism was silently converted into a claimed deployment. No runtime, cluster, source-code or architecture change was performed by this reconciliation.
