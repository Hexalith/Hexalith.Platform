---
title: Sprint Change Proposal — Unified Hexalith MCP and CLI ownership
date: 2026-09-27
status: approved — planning alignment applied; migration backlog open
scope: major, cross-repository
---

# Sprint Change Proposal: Hexalith.McpCli replaces proprietary module MCP and CLI surfaces

## 1. Issue summary

Administrator directed that any PRD or architecture topic about a Hexalith-owned MCP or CLI surface refer to `Hexalith.McpCli`; proprietary MCP servers and CLIs in other Hexalith modules are obsolete and will be replaced by it. The trigger is a product direction change, not a failure in one story. The current `Hexalith.McpCli` PRD already plans to replace six per-module MCP servers and freeze five CLIs, but expressly excludes `Hexalith.EventStore.Admin.Cli` and `.Admin.Mcp` and defers deletion. Other active module plans still describe their own adapters as target architecture. The platform architecture also contains a stale link to a sibling `mcpcli` path despite the root-declared `references/Hexalith.McpCli` submodule.

The change applies to Hexalith-owned module presentation and operator tools. Mentions of external tooling such as Aspire, Dapr, `dotnet`, Azure, and GitHub CLIs are outside this retirement. Domain and platform operations still need their current authorization, audit, response, and UX guarantees. Historical evidence may name old components if clearly labeled historical.

**Evidence:** [McpCli PRD](../../references/Hexalith.McpCli/_bmad-output/planning-artifacts/prds/prd-mcpcli-2026-09-21/prd.md) §2.2, §3, FR-21/FR-22, §8.3; [McpCli addendum](../../references/Hexalith.McpCli/_bmad-output/planning-artifacts/prds/prd-mcpcli-2026-09-21/addendum.md) §D; [platform PRD](prds/prd-platform-2026-09-27/prd.md) FR-12; [platform architecture](architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md) AD-11/AD-14. The working tree already had unrelated platform PRD edits and a staged McpCli submodule; this course correction preserved them.

## 2. Impact analysis

### Epic and story impact

`Hexalith.McpCli` Epic 4 is the existing migration entry point. Stories 4.1–4.4 inventory Parties, Folders, Projects/FrontComposer, and ChatBot/Memories; 4.5 gates coverage; 4.15 publishes the no-new-server rule. They need a definitive retirement target for both MCP and CLI, explicit non-Command/Query rows, and updated exclusions. The current sprint status has Epic 4 in `backlog`, so these edits can land before that work starts. Add a subsequent migration/retirement epic for contract gaps, EventStore admin surfaces, cross-module cutover, documentation, and removal after parity. Keep the initial Tenants/Parties McpCli release gate distinct from claiming all legacy surfaces have already been replaced.

Affected module stories include Folders 5.2–5.11 and 7.13; ChatBot 5.1–5.10; FrontComposer 5.1, 10.3, 11.3, and 14.1–14.3; Memories 7.1, 10.1, 25.5–25.6; Projects 5.10, 6.6, 8.4–8.5; Parties 8.8; and EventStore 5.4 and 7.5. Rework the still-open adapter stories as McpCli contract enrollment, migration, and parity work. Mark completed stories as historical implementation evidence and add successor stories; do not rewrite their acceptance history.

### Artifact conflicts

| Owner | Active artifact conflict | Required correction |
| --- | --- | --- |
| Platform | PRD FR-12 and architecture AD-11 call McpCli the MVP route yet allow a later module MCP host architecture decision; architecture links to `../../../../../mcpcli/...`. | State the replacement policy, phase-out rule, migration gates, and canonical `references/Hexalith.McpCli` link. |
| McpCli | PRD §2.2/§8.3 excludes EventStore admin tools; FR-21 covers six servers and five CLIs; §4 confines execution to decorated domain Contracts through one gateway. | Expand retirement inventory to every Hexalith-owned module MCP/CLI; add a secure design decision for non-domain/admin operations and for features the generic v1 catalog cannot represent. |
| ChatBot | PRD FR81–FR86 and architecture project layout require `Hexalith.ChatBot.Cli` and `.Mcp`; M1 includes human-delegated decisions on machine surfaces. | Attribute operations to decorated Contracts and McpCli, reconcile M1 parity and confirmation rules with platform AD-14, and replace adapter-specific gates. |
| Folders | PRD FR48/FR49 and architecture require own CLI/MCP adapters over the Folders SDK and their parity oracle. | Preserve lifecycle semantics but route eligible operations through McpCli and EventStore; replace adapter build/distribution requirements. |
| FrontComposer | PRD FR14–FR21, architecture, and UX require a descriptor-driven MCP host, resources, and `frontcomposer` inspect/migrate tool. | Retire owned machine transports; retain the UI/descriptor source. Design generic representation or replacement for resources and non-domain migration commands before removal. |
| Memories | PRD and architecture name `Hexalith.Memories.Cli`/`.Mcp`, CLI-first onboarding, Dapr MCP service, and token-budget tools. | Define McpCli-backed onboarding and equivalent search/ingest/traversal behavior; preserve accessibility and tenant evidence. |
| Projects | PRD and architecture require FrontComposer Web/CLI/MCP composition and allow CLI/MCP confirmation after gates. | Use McpCli for eligible operations, keep human confirmation on confidential UI per platform AD-14, and revise parity/release gates. |
| Parties | Architecture sends MCP plumbing to the FrontComposer MCP host; legacy `.Mcp` still appears in build/cleanup stories. | Route eligible Contracts through McpCli; retire both old routes after inventory parity. |
| EventStore | Admin CLI/MCP remain active and explicitly excluded by McpCli, with destructive CLI confirmation and typed Admin client stories. | Inventory all admin operations and define a safe McpCli replacement path, including infrastructure operations outside current gateway Command/Query semantics. |
| Tenants, Works, Timesheets | Tenants architecture references `FrontComposer.Cli`; Works future roadmap and UX describe independent MCP/CLI adapters; Timesheets architecture allows later helper packages. | Point future Hexalith-owned machine access at McpCli; do not create new module CLI/MCP packages. |
| Agents, Conversations | No current proprietary machine adapter is committed in the inspected active spines; Conversations warns against bypass. | Keep the gateway/authorization constraint and name McpCli when future Hexalith-owned MCP/CLI access is discussed. |

The affected UX specifications are ChatBot `EXPERIENCE.md` (cross-surface attribution and Journeys 6–7), FrontComposer `EXPERIENCE.md` (MCP tools/resources/lifecycle), Memories `EXPERIENCE.md` (CLI/MCP outputs), and Works `EXPERIENCE.md` (future adapter flows). Update user-visible command names, setup, errors, accessibility, and capability availability once the migration contract is decided; retain the domain interaction and disclosure guarantees.

### Technical impact and risks

McpCli v1 has five generic MCP tools and a thin CLI over decorated Command/Query Contracts. The legacy inventory includes REST/Dapr-backed operations, FrontComposer resources/skill corpus, FrontComposer inspect/migrate commands, and EventStore infrastructure administration. These do not all map to the current gateway or catalog. A declaration rename alone cannot replace them. A cross-repository architecture decision must specify how McpCli handles these classes without module-specific runtime code, which parts become Gateway operations, and which existing operations are intentionally withdrawn with owner approval. Platform AD-14 still denies UI-only or human-confirmation operations to McpCli's public agent-capable client; migration must not weaken that rule.

The change is **major**. Effort is high across McpCli, EventStore, and at least six adapter-owning modules. The date impact is unknown until the operation inventories and transport decision are approved; release claims for complete unified access must be gated on those results. The main risks are lost operator capability, changed structured output/exit codes, authorization bypass, and misleading cross-surface parity claims. No rollback of completed domain behavior is recommended; keep legacy binaries only as explicitly versioned, time-bounded migration compatibility until their replacement passes evidence gates.

## 3. Recommended approach

Use a hybrid of **MVP review and direct backlog adjustment**. Keep the existing McpCli initial release with its demonstrated Tenants/Parties scope, but do not present that release as completion of the ecosystem migration. Immediately set the planning rule that McpCli owns future Hexalith MCP/CLI access and no new module-specific MCP server or CLI is planned. Follow with a migration epic that inventories every legacy operation, resolves missing capability classes, proves parity/security per module, then removes the obsolete transport and distribution references. Reconcile affected module MVP gates where they currently require a proprietary adapter or machine confirmation forbidden by AD-14.

Potential rollback is not selected: deleting current adapters before equivalent behavior exists would lose required operations. Direct text replacement alone is not viable because the current McpCli contract omits several capability classes. The product owners and architect must settle those classes before developers implement cutover.

## 4. Detailed edit proposals (batch)

The OLD excerpts below identify the exact current requirement or decision; NEW text is proposed replacement language, subject to owner review in the owning repository. Keep the named FR and story IDs stable unless an owning team approves renumbering.

### PRD changes

1. **McpCli PRD §1, §2.2, §3, FR-21/FR-22, §8.3; addendum §D.** OLD: “replaces the six Legacy Server packages”; “EventStore infrastructure administrators ... stay with `Hexalith.EventStore.Admin.Cli` and `.Admin.Mcp`”; addendum: “Not targets: `Hexalith.EventStore.Admin.Mcp` and `.Admin.Cli`.” NEW: “`Hexalith.McpCli` is the target Hexalith-owned CLI and MCP surface for domain, technical, and administrative module capabilities. All existing proprietary module CLIs and MCP servers are migration sources, including EventStore Admin. Inventory each operation and classify its replacement, approved withdrawal, and required McpCli/catalog/transport work. Existing binaries remain migration compatibility only until owner-approved parity and security gates pass.” **Rationale:** the explicit admin exclusion and partial server count conflict with the new directive. Keep the v1 Tenants/Parties scope labeled as the first increment.

2. **Platform PRD FR-12 and glossary.** OLD: “Additional module or technical-module MCP hosts require a later architecture decision.” NEW: “Hexalith.McpCli is the target MCP and CLI owner for all Hexalith modules. Existing module or technical-module MCP/CLI binaries are obsolete migration sources; Platform does not add or publish another proprietary MCP/CLI surface. Each source is retired only after the approved operation inventory, authorization, compatibility, and acceptance evidence is complete.” **Rationale:** remove the future alternate-host option while stating the safe cutover condition. Preserve current FR-12 agent eligibility and UI-only refusal.

3. **ChatBot PRD FR82–FR86 and M1 scope.** OLD: “Authorized CLI users can perform the singular M1 parity set” through a ChatBot CLI, with an MCP adapter and human-delegated parity decisions. NEW: “The M1 machine-access set is published through Hexalith.McpCli from decorated ChatBot Contracts where gateway-ready and agent-eligible. The same domain command pipeline, attribution, and audits apply. Human confirmation and UI-only decisions use the confidential ChatBot UI; McpCli denies them. M1 acceptance names included, excluded, and deferred operations and does not claim parity for a capability the shared catalog cannot yet serve.” **Rationale:** preserve governance while removing proprietary transport and resolving platform AD-14.

4. **Folders PRD FR48/FR49.** OLD: “CLI users can perform every C13-required CLI cell” and “MCP clients can perform every C13-required MCP cell” through Folders adapters. NEW: “Hexalith.McpCli CLI and MCP heads execute the approved Folders operation inventory through decorated Contracts and the EventStore gateway. C13 tests compare permitted lifecycle outcomes, identity, tenant authorization, idempotency, status, errors, and audit; unsupported legacy-only cells block retirement or have explicit owner-approved exclusions.” **Rationale:** move transport ownership without losing the behavioral bar.

5. **FrontComposer PRD FR14–FR21 and package/distribution section.** OLD: “The MCP server exposes generated commands and projections to agents”; `Hexalith.FrontComposer.Mcp` and `.Cli` remain product packages. NEW: “FrontComposer supplies UI and descriptors; Hexalith.McpCli supplies Hexalith-owned MCP/CLI access. The migration inventory covers commands, projection and skill resources, lifecycle polling, `inspect`, and `migrate`; each receives an approved McpCli representation or an explicit product withdrawal before the old packages are retired.” **Rationale:** generic McpCli v1 lacks resource and migration-command equivalents.

6. **Memories PRD Phase 1/1.5 scope and CLI specification.** OLD: “Developer Tool / API Backend (NuGet packages + DAPR service + CLI + MCP server)” and `Hexalith.Memories.Cli`/`.Mcp` deployment. NEW: “Memories publishes domain and operator contracts for Hexalith.McpCli; the shared CLI and MCP heads are the target user and agent access. Keep phase-specific search/ingest/traversal, tenant isolation, token-budget, result, accessibility, and onboarding acceptance requirements, mapped to the approved inventory. Legacy packages are migration compatibility only.” **Rationale:** replace transport ownership without dropping product behavior.

7. **Projects PRD §2.1, FR-22/FR-24, A-7 and confirmation clauses.** OLD: Tenant operators use “Web/CLI/MCP” for consequential actions, with CLI/MCP confirmation after gates. NEW: “Web provides confidential human confirmation and selection. Hexalith.McpCli exposes only agent-eligible Projects operations through both shared heads; it cannot mint Selection Evidence or execute confirmation-required actions. Safe reads, task control, and export are included only when the owning contract and gateway authorization permit them. The inventory records every previous CLI/MCP capability and its outcome.” **Rationale:** align with platform AD-14 and shared transport.

8. **Works PRD §12, Timesheets future-surface text, Tenants FrontComposer CLI reference, and EventStore PRD FR26-C4.** OLD: future MCP/CLI adapters or existing `FrontComposer.Cli`/EventStore Admin CLI are treated as module-owned products. NEW: “Any Hexalith-owned CLI or MCP access is provided by Hexalith.McpCli after contract enrollment and authorization qualification; historical tools remain migration sources. EventStore destructive/admin operations require a separate approved safety contract before cutover.” **Rationale:** apply the rule to future and infrastructure modules without rewriting historical results.

### Architecture changes

9. **Platform AD-11 and McpCli-spine link.** OLD: “no module, FrontComposer or technical-module MCP host ... is deployed or routed ... until an AD admits it under AD-14”; linked path `../../../../../mcpcli/...`. NEW: “Hexalith.McpCli is the sole target proprietary CLI/MCP implementation. Legacy hosts and CLIs are not enrolled into new Platform compositions; temporary compatibility use requires a named migration record and removal gate. No later module-owned proprietary MCP/CLI host is admitted.” Point the link to `../../../../references/Hexalith.McpCli/_bmad-output/planning-artifacts/architecture/architecture-mcpcli-2026-09-22/ARCHITECTURE-SPINE.md`. **Rationale:** make the target permanent and repair the canonical source link.

10. **McpCli architecture AD-1/AD-10/AD-21 and dependency/transport decisions.** OLD: “One core that knows Contracts types ... Two adapters ... the gateway client”; admin operations are outside the PRD. NEW: “Retain one shared MCP/CLI core and generic catalog. Add an architecture decision for gateway-incompatible administrative commands, resource-like reads, and migration diagnostics, with authenticated transport, contract versioning, authorization class, audit, and generic registration. No module-specific branch may be added to McpCli. The existing v1 design remains valid for its declared domain subset until the extension is approved.” **Rationale:** prevent an implementation from assuming `Command`/`Query` decoration covers all old capabilities.

11. **ChatBot, Folders, Memories, Projects, Parties, and FrontComposer active architecture.** OLD examples: `Hexalith.ChatBot.Cli/` and `.Mcp/`; `Hexalith.Folders.Cli`/`.Mcp` wrap its SDK; `Hexalith.Memories.Cli/` and `.Mcp/`; “FrontComposer Web / CLI / MCP hosts”; “MCP plumbing → FrontComposer MCP host.” NEW: “The module owns Contracts, domain semantics, UI where applicable, and conformance vectors. Hexalith.McpCli owns Hexalith CLI/MCP presentation, catalog, profiles, and transport. The module's legacy adapter is a compatibility asset pending its approved inventory and retirement gate.” Revise diagrams, package graphs, deployment rows, test topology, and CI gates accordingly. **Rationale:** remove conflicting target architecture while preserving domain ownership.

12. **EventStore architecture Admin layout.** OLD: `Hexalith.EventStore.Admin.*` includes CLI and MCP as operator surfaces. NEW: “Admin CLI/MCP are obsolete migration sources. EventStore owns the administration semantics and security checks; an approved McpCli admin contract and transport decision must replace each supported operation before those binaries are removed. Destructive actions retain their confirmation and role gates and remain unavailable through McpCli until that proof passes.” **Rationale:** the admin plane cannot safely be mapped by name alone.

### Epic and story changes

13. **McpCli Epic 4 Stories 4.1–4.5 and 4.15.** OLD: six Legacy Servers, five Frozen CLIs, with EventStore Admin excluded; no-new-server rule covers per-module agent CLI. NEW: inventory all Hexalith-owned proprietary MCP/CLI surfaces, including EventStore Admin and FrontComposer inspect/migrate, recording operation kind, gateway fit, surface eligibility, owner approval, replacement/withdrawal, evidence, and cutover dependency. Keep the existing per-module story IDs and add EventStore/technical-tool inventory stories; amend 4.15 so the authoritative instruction prohibits new module-specific proprietary MCP **and CLI** implementations. **Rationale:** make the retirement target testable.

14. **New McpCli migration epic after Epic 4.** OLD: no epic owns ecosystem retirement after the initial paired release. NEW: “Migrate and retire legacy Hexalith MCP/CLI surfaces”: decide catalog/transport extensions; deliver required Contracts and authorization; prove CLI/MCP parity and negative tests; update installation/UX docs; deprecate, remove, and verify each old package and Platform route. Add backlog entries to `sprint-status.yaml` after approval and story creation. **Rationale:** the current v1 epic cannot absorb this cross-repository work without misrepresenting release scope.

15. **Module adapter stories listed in §2.** OLD: completion is an owning-module CLI/MCP binary or FrontComposer-hosted MCP plug-in. NEW: completion is module Contracts and conformance vectors enrolled in McpCli, or a documented successor story where old work is already done; acceptance proves matching behavior, security, audit, outputs, and the retirement gate. **Rationale:** align the backlog without erasing completed history.

### UX and supporting artifacts

16. **ChatBot, FrontComposer, Memories, and Works EXPERIENCE documents.** OLD: flows instruct users to launch module-specific CLIs/MCP hosts or call typed module MCP resources/tools. NEW: setup names the `hexalith` tool and its McpCli stdio server, with eligible operation discovery, the selected environment, authorization/refusal states, and module-specific outcome wording. Keep visual UI flows; rework unsupported resource/confirmation journeys only after their product decision. **Rationale:** a transport change also changes setup and error recovery.

17. **Operator guides, package manifests, CI, and platform release evidence.** OLD: publish or deploy per-module MCP/CLI packages. NEW: publish the versioned McpCli candidate, test the approved per-module inventory against the selected gateway, and remove obsolete package/route declarations only after parity evidence. **Rationale:** prevent documentation-only convergence.

## 5. Implementation handoff

**Classification: Major — Product Manager and Solution Architect lead the replan.** McpCli and EventStore maintainers own the capability/transport decision and generic migration contract. Module maintainers own operation inventories, inclusion/exclusion approvals, contract decoration or admin contract work, and conformance vectors. Product owners update release scope, epics, and UX claims. Developers update code and retirement packaging only after the affected contracts and backlog are approved. Platform maintainer owns the canonical submodule link, release composition, candidate evidence, and no-new-surface policy. The approved planning edits and McpCli successor backlog are now in the working tree; runtime migration remains backlog work.

Success criteria: every active architecture/PRD machine-surface topic names McpCli as target; no active plan introduces a new proprietary module CLI/MCP server; every old operation has an approved disposition; the shared CLI and MCP heads pass positive/negative conformance on the chosen environment; prohibited UI-only or confirmation actions remain denied; legacy packages and deployment routes are removed only after equivalent approved behavior or approved withdrawal; release documentation reports actual rather than intended readiness.

## Checklist status

| Checklist | Status | Finding |
| --- | --- | --- |
| 1.1 triggering story | N/A | Administrator's direction is the trigger; no single story exposed it. |
| 1.2–1.3 problem and evidence | Done | Cross-module PRD/architecture contradictions and McpCli exclusions cited above. |
| 2.1–2.5 epic impact, order, dependencies | Done | McpCli Epic 4 changes; new successor migration epic; module stories and sequencing listed. |
| 3.1 PRD | Done | Current MVP claims and explicit exclusions identified. |
| 3.2 architecture | Done | Adapter owners, gateway boundary, AD-14, diagrams, and stale link identified. |
| 3.3 UX | Done | Machine setup, resources, confirmations, outputs, and accessibility impacted. |
| 3.4 other artifacts | Done | Inventories, package/deployment routes, CI, release evidence, and instructions impacted. |
| 4.1 direct adjustment | Viable in part | Low effort for policy text; high effort for full capability migration. |
| 4.2 rollback | Not viable | No evidence that rollback simplifies safe migration. |
| 4.3 MVP review | Viable | Reconcile ChatBot, Projects, FrontComposer, and Memories machine-surface release gates. |
| 4.4 selected path | Done | MVP review plus backlog and architecture adjustment. |
| 5.1–5.5 proposal and handoff | Done | This document states edits, impact, risks, sequencing, and owners. |
| 6.1–6.2 review | Done | Proposal checked against the cited active artifacts; owner-level inventories remain implementation work. |
| 6.3 approval | Done | Administrator approved the proposal in this conversation on 2026-09-27. |
| 6.4 sprint status | Done | McpCli Epic 5 and six successor stories added as backlog. |
| 6.5 handoff | Done | Responsibilities and gates recorded in §5 and the implementation record below. |

**Planning-artifact availability:** Platform has a current PRD and architecture but no platform epics file. This assessment uses `Hexalith.McpCli`'s active epics and sprint status plus the affected module epics. Exact owner inventories and release dates need subsequent planning; their absence is recorded rather than inferred.

## Approval and implementation record

Administrator approved this proposal on 2026-09-27. The platform PRD, addendum, and architecture now state the McpCli target and the canonical architecture link. The McpCli PRD, addendum, architecture, epics, and sprint status now distinguish the initial gateway-only release from Epic 5 migration and retirement. Active MCP/CLI topics in the inspected module PRDs and architecture identify `Hexalith.McpCli` as the target; affected epics and machine-facing UX documents carry the migration direction. The authoritative Hexalith.AI.Tools instruction file has the no-new-proprietary-MCP/CLI rule in its working tree.

**Handoff:** Product Manager and Solution Architect: reconcile affected release scopes and decide the generic non-gateway/admin contract (McpCli Story 5.2). McpCli, EventStore, FrontComposer, and module maintainers: approve per-operation inventories (Story 5.1), enroll replacements (5.3), prove parity and denial (5.4), then remove legacy packages and routes (5.5). Product owners: confirm UX and release claims (5.6). No legacy runtime was removed by this planning change. The AI.Tools rule and cross-repository edits are uncommitted working-tree changes; merge/publication evidence remains an Epic 4/5 gate.
