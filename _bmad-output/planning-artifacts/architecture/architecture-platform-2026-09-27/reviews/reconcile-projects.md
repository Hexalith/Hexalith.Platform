# Projects input reconciliation

Verdict: compatible after one small runner-contract clarification. No user decision or new infrastructure mechanism is required.

## Authority inspected

- `references/Hexalith.Projects/AGENTS.md` and the permitted root-declared `references/Hexalith.AI.Tools/hexalith-llm-instructions.md`.
- Projects final spine, updated 2026-09-08: `references/Hexalith.Projects/_bmad-output/planning-artifacts/architecture/architecture-projects-2026-07-15/ARCHITECTURE-SPINE.md`.
- Projects PRD: `references/Hexalith.Projects/_bmad-output/planning-artifacts/prds/prd-Hexalith.Projects-2026-05-24/prd.md`, particularly NFR-1 through NFR-4 and A-7.
- Platform draft: `ARCHITECTURE-SPINE.md`, read after replacement of the initial scaffold on 2026-09-27.

## Finding P-1 — Pin the consumer-visible runner and shared-fixture boundary

**Severity:** medium; missing producer/consumer contract precision, not an incompatible architectural choice.

**Location:** Platform Consistency Conventions, “Module declaration authority” and “Local tool and readiness” (lines 124 and 129 at review time).

**Evidence:** Projects AD-25 and G-4 require a pinned independently consumable Platform .NET tool in the module's `.config/dotnet-tools.json`, a versioned non-secret manifest carrying the descriptor assembly and fixture profile, and stable repository-owned run/teardown commands. Projects AD-24 assigns reusable test fixtures to `EventStore.Testing(.Integration)`. The Platform draft promises a pinned consumable runner and thin fixtures but does not name that distribution contract or shared-fixture owner; its manifest fields omit the descriptor assembly and fixture profile. Independently built consumers could consequently choose incompatible runner packaging or introduce a competing shared test SDK.

**Smallest correction:** State that the Platform entry is a pinned consumable .NET tool recorded in each consumer's `.config/dotnet-tools.json`; reusable EventStore integration fixtures remain in `EventStore.Testing(.Integration)`, consuming Platform-owned topology/lifecycle. Include descriptor assembly and fixture profile in the declaration contract where required by a module. Existing thin Aspire tooling can implement this; no daemon, framework, DSL, or new registry is needed.

## Reconciled boundaries

- **Semantics and manifests:** AD-11 keeps operation schema ownership in module Contracts; the enrollment manifest describes composition and does not redefine Projects operations or security.
- **Migration:** The Migration convention preserves producer-before-consumer adoption and module-owned source/package/deployed parity gates. It does not authorize premature deletion of Projects runtime projects.
- **Authentication and MCP:** AD-6 retains actor/workload/delegation checks; AD-11, the Projects integration row, and the module-release convention preserve Projects operation/surface restrictions and release gates. Therefore A-7 interactive-session claims and AD-29/30 consequential-MCP acceptance remain required; catalog discovery and a valid bearer token do not enable them. Projects remains limited to permitted MCP reads/task controls before its gate, with autonomous confirmation excluded. No additional Platform-wide confirmation framework is requested.
- **Service versus disaster recovery:** The Projects integration row correctly preserves 99.9% availability, 15-minute service RTO with healthy dependencies, five-minute task recovery or truthful `NeedsAttention`, and committed-event RPO 0 within the configured primary-region durability domain. These are not a direct contradiction of four-hour site-disaster recovery. Neither requirement is claimed proven; the primary-domain durability boundary and evidence remain module qualification work.
- **Evidence and MVP scope:** The draft preserves module release evidence and missing-capability qualification without automatically adding all Projects sibling/presentation dependencies to the seven-module MVP. Platform composition does not change Projects' current readiness disposition or waive its Builds-owned evidence validator/terminal gates. The user's Folders-only operational override does not waive Projects contracts.

No source, Git state, runtime, environment, or upstream planning document was changed. This report is the only written artifact.
