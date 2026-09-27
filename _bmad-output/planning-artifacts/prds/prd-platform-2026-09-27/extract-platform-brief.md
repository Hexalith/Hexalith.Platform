# Source extraction: Hexalith Platform brief

Extracted 2026-09-27 for PRD creation. This is a faithful source digest, not a new product decision. No applicable `AGENTS.md` was found in the workspace ancestors or `_bmad-output` tree inspected.

## Sources and authority

- **B** — [Completed product brief](../../briefs/brief-platform-2026-09-27/brief.md), especially lines 10–65. Primary distilled product input.
- **A** — [Completed supporting addendum](../../briefs/brief-platform-2026-09-27/addendum.md), lines 10–113. User constraints, dated repository observations, unconfirmed proposals, and architecture questions.
- **M** — [Brief decision log](../../briefs/brief-platform-2026-09-27/.memlog.md), lines 6–38. Audit context distinguishes user decisions from provisional assumptions and superseded proposals.

Both source documents are complete (B:1–6; A:1–6). The user accepted the five MVP success outcomes and authorized brief finalization (M:34). Repository observations concern the working tree inspected on 2026-09-27, including uncommitted work; they do not establish deployed capability (A:25,29).

## Confirmed vision and qualitative intent

Provide **one common solution** for deploying the environments needed for development, testing, staging, and production across Hexalith. Platform references all Hexalith servers/components and owns shared hosting; modules retain domain behavior. It contains the Aspire host used for system testing and debugging (B:12–18,57; M:6,14).

The unifying purpose is the product's central intent. The user explicitly replaced an earlier inferred ranking of developer setup pain with this purpose. Do not recast reduced setup time, configuration drift, or duplicated hosting effort as validated primary problems or committed outcomes (M:14,18; A:87–88).

Platform must serve both the complete system and an individual module's development workspace, where it starts only the minimum needed components. It must use the module checkout being edited so changes and breakpoints affect the running code (B:14,24,33; A:76).

## Audience, stakes, and form factor

- **Confirmed:** developers work on their own machines with Aspire; other users access staging or production on Kubernetes. These contexts include McpCli (B:14,22,28–35; A:16,51; M:26).
- **Unconfirmed:** integration testers and environment operators as distinct personas, and their responsibilities. Internal planning was an earlier inferred purpose, not a user-confirmed rigor/audience classification (A:86; M:9).
- **Stakes evidenced by scope:** the product includes production deployment, automatic release promotion and rollback, application data, and credentials. No numeric service expectation or formal launch classification was supplied (B:37–41,63; A:90,99).
- **Form factor:** shared developer/orchestration and deployment platform with an Aspire host, module-repository Git-submodule integration, CLI/MCP access through McpCli, and Kubernetes-hosted staging/production. No dedicated Platform web/mobile/desktop UI was requested (B:12–14,22,28–35; A:45–51).

## Confirmed scope and boundaries

The MVP contains **EventStore, Tenants, Parties, Folders, Projects, McpCli, and Memories**, with supporting components needed to run them. It covers development/debugging, local tests, automated CI tests, staging, and production. This module set must work together and in the minimum composition needed for individual-module development. Broader Platform scope remains all Hexalith servers/components (B:20–35; M:21,23–24).

No independent list of product non-goals was supplied. Do not invent permanent exclusion of the other Hexalith modules, declare test doubles allowed, or decide that CI testing is outside scope. Architecture mechanisms are unresolved, not implied product exclusions (B:24,61–65; A:80).

## Supplied usage contexts and observable requirements

These are use cases and outcomes, **not narrated journeys**. The user supplied no named protagonist, chronological session, or detailed task flow; do not fabricate one.

| Context/capability | Confirmed outcome | Source |
| --- | --- | --- |
| Complete local environment | Start the seven-module MVP plus required supporting components on a developer's own machine using Aspire for testing/debugging. | B:22,30,47 |
| Module workspace | Include Platform as a direct Git submodule; run/debug the module with only its required dependencies under the root-only submodule policy. Running code must reflect the active module checkout. | B:14,33,48,55; A:59–78 |
| Local testing | Provision the components required for a local test scenario, including the complete system when needed. | B:31,49 |
| CI testing | Provision environments required by automated CI tests through the same Platform, honoring CI build/dependency rules. Lifecycle/location are unresolved. | B:32,49,65; A:89 |
| Module access | McpCli provides CLI and MCP access to module operations through EventStore in local Aspire and hosted Kubernetes contexts. Hosted transport/process placement are unresolved. | B:22; A:49–53 |
| Staging | Run on the local Kubernetes installation at `192.168.1.30`, published under `hexalith.com`. | B:34; A:94–99 |
| Production | Run on that Kubernetes installation, published under `tache.ai`. | B:35; A:94–99 |
| Environment separation | Staging and production have separate application data and credentials despite the shared Kubernetes installation. | B:41,50; M:32 |
| Release promotion | Automatically deploy a release to production after it passes staging checks. Specific checks remain unspecified. | B:39,51; M:28 |
| Deployment recovery | Automatically roll back a failed production deployment to the previous working release. Failure criteria and recovery mechanism remain unspecified. | B:39,51; M:30 |

The five agreed MVP success criteria are demonstrable local seven-module startup; individual-module minimum-dependency debugging; local and CI test provisioning; accessible, separated hosted staging/production; and automatic promotion plus failed-deployment rollback. The user confirmed no essential outcome was missing from this set (B:43–51; M:33–34).

No quantitative targets were accepted. Startup time, developer setup effort, resource consumption, and deployment recovery time are only candidate measures; no baseline, target, counter-metric, observation window, or measurement owner was chosen (A:87–90).

## Established constraints

1. **Root-only submodules:** initialize only dependencies declared under `references/` by the active root repository; never initialize nested submodules. Platform-root work uses Platform's declarations. Module-root work uses that module's direct declarations for Platform and other dependencies; embedded Platform's nested references remain uninitialized (B:55; A:55–74; M:16–17).
2. **Environment-dependent assets:** local development/testing uses project references and Debug assets; CI/CD uses NuGet package references and Release assets. Both local and CI testing are confirmed in scope (B:49,57; A:78; M:19,21).
3. **Responsibilities:** shared hosting belongs in Platform and domain behavior stays in modules (B:57).
4. **Hosted placement and isolation:** specified Kubernetes address and domain mapping; separate application data and credentials. The address is not evidence of a single-node topology or any availability characteristic (B:34–41,65; A:94–103).

The source policy additionally prohibits recursive/remote submodule updates and calls for accidentally initialized nested submodules to be deinitialized (A:57). Preserve as repository workflow context; it does not require selecting a dependency manifest/source-resolution design in the PRD.

## Concerns the PRD must address

- Dependency completeness and minimality across full-system and individual-module configurations, including transitive/optional/supporting services (B:24,61; A:80).
- Correct use of the active module checkout with direct-root dependencies and different local/CI build inputs (B:55–57; A:74–80).
- CI environment provisioning lifecycle, ownership, hosting, isolation, and cleanup; only scope is confirmed (B:32,65; A:89).
- Production release gating, observable failure detection, automatic rollback, downtime, recovery, and operating responsibilities (B:39,62–63; A:90,103).
- Staging/production data and credential separation on shared infrastructure, permitted sharing of supporting services/capacity, and unassessed storage/backup arrangements (B:41,63; A:99–101).
- McpCli functionality and access in both local and hosted contexts; actual integration/enrollment readiness is not established (B:22,65; A:45–53).

## Technical/contextual detail for the PRD addendum

- **Dated implementation baseline:** current AppHost provides an opt-in, Development-only Works preview using a sibling Works checkout and nested EventStore paths. That composition includes EventStore/admin/Operations, Works, and Dapr. It is evidence of one preview, not general module selection or complete integration. Works remains the rollback composition until its migration parity gate; Agents wiring/live evidence belongs to an existing Agents story (A:29–38).
- **Inventory:** sixteen declared references are AI.Tools, Agents, Builds, ChatBot, Commons, Conversations, EventStore, Folders, FrontComposer, Memories, Parties, PolymorphicSerializations, Projects, Tenants, Timesheets, and Works. An entry is not proof of a server/runtime integration or a permanent coverage boundary. No inspected immediate module declarations contained Platform; requested Kubernetes/domain setup was not found in root implementation (A:38–43).
- **McpCli readiness:** canonical repository is sibling `../mcpcli`; it was absent from Platform's inspected root `.gitmodules`. It supplies the `hexalith` .NET tool, discovers decorated Contracts libraries, and routes commands/queries through EventStore. README documents stdio MCP, defers HTTP, and reports an empty catalog with enrollment pending. These are documentation findings, not verified runtime readiness or complete enrollment status (A:47–53).
- **Workspace mechanics:** illustrative direct-reference layout and delegated Git policy sources; architecture still chooses manifest format, component selection, and source resolution (A:57–80).
- **Hosted mechanics:** per-service hostnames/paths, ingress, DNS, certificates, deployment tooling, cluster topology, capacity/storage, secret/configuration mechanism, backup arrangements, rollback implementation, and relationship to Hexalith.Builds remain open (A:80,99–103).
- **Prior external research:** official Aspire sources discuss explicit project/resource references, Kubernetes publishing/deployment, and environment contexts; Kubernetes sources distinguish namespaces from isolation and make availability depend on actual topology. These are inputs for later decisions, not proof of installed-version/local-cluster compatibility or chosen architecture (A:105–113).

## Remaining questions, separated by decision type

**Product/operational clarification**

1. Who owns each module/scenario's minimum dependency definition; are real dependencies mandatory, and are test doubles permitted? What counts as a supported minimum? (B:61; A:80)
2. What staging checks permit automatic production deployment, and what observable conditions define failed deployment/rollback completion? (B:39,62; A:90,103)
3. What downtime, recovery-time, availability, or data-recovery expectations are acceptable, and who operates and recovers the environments? None are currently quantified. (B:63; A:88–90,99)
4. What CI environment lifecycle and user/automation responsibilities are required, including provisioning scope and cleanup? (B:65; A:89)
5. Which supporting services/capacity may staging and production share while keeping application data and credentials separate? (B:63; A:101)
6. What exact CLI/MCP tasks must hosted users accomplish, and what access behavior is required? Local/hosted support is already decided; transport is not. (B:22,65; A:49–53)
7. What measurable targets/counter-metrics and responsibilities should supplement the five observable MVP outcomes? Additional persona responsibilities and explicit stakes calibration are unconfirmed. (A:86–90)

**Architecture/readiness decisions to preserve without prematurely resolving**

Active-workspace source resolution; dependency manifest/selection; transitive/optional service composition; hosted McpCli transport/placement; CI hosting choice; release/deployment mechanism and use of other Hexalith components (including Builds); ingress/DNS/certificates; topology/capacity/storage; configuration/credential isolation; backup/rollback mechanisms; verification of module/McpCli enrollment and readiness (B:62–65; A:43,47–53,74–80,99–113).

## Audit cautions

- The earlier provisional fast path was superseded by the user's coaching-path choice during brief creation. Unaccepted inferred audience/benefit/measurement proposals were deliberately moved out of the brief; do not resurrect them as decisions (M:10,14–18).
- A generic reciprocal-submodule concern was superseded by the existing root-only policy. Nested initialization is already prohibited; this is not an unresolved design choice (M:12,16–17; A:74).
- Testing includes both local and automated CI; the MVP contains seven modules including Memories; McpCli local and hosted contexts are confirmed; automatic promotion, automatic rollback, and separate environment data/credentials are settled decisions (M:21,23–24,26,28,30,32).
- No deployment, release pipeline, module readiness, cluster availability, or precise performance target was verified by the brief (A:25,53,88,99,103,107).

## Current PRD intake supplied by the parent

After source extraction, the parent reported that the user confirmed using this brief as-is, classified the product as an internal team platform, and selected **Coaching → Vision + Features** for PRD creation. These current-run answers supersede the source-level uncertainty about stakes and working mode; they do not change the underlying brief. Continue from the confirmed vision and elicit one consequential unresolved capability at a time.
