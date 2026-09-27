# Memories input reconciliation

Verdict: PASS. The selected Dapr and recovery architecture is compatible with Memories' retained safety contracts. The technical-module ownership clarification below was applied and verified on 2026-09-27; no essential architecture gap remains for this input. This verdict does not establish implementation or production qualification.

## Authority inspected

- `references/Hexalith.Memories/AGENTS.md`; the module resolves to the Platform superproject, whose root `.gitmodules` declares the previously read `references/Hexalith.AI.Tools/hexalith-llm-instructions.md` baseline.
- `references/Hexalith.Memories/_bmad-output/planning-artifacts/architecture/architecture-memories-2026-09-09/ARCHITECTURE-SPINE.md`, updated 2026-09-12; particularly AD-1, AD-2, AD-5, AD-6, AD-8, AD-15, AD-16, AD-19, AD-20, AD-21 and Deferred.
- Current Platform `ARCHITECTURE-SPINE.md`, including AD-6, AD-8, AD-9, AD-12, the Memories erasure-continuity acceptance row, source integration and qualification work.

## Resolved M-1 — Limit the domain-host prohibition to domain modules

**Severity:** medium; small ownership clarification, not a new architecture decision.

**Location:** Platform Design Paradigm, the final sentence of its MVP paragraph (line 25 at review time).

The text says “Modules ... do not own AppHost, Aspire, or ServiceDefaults infrastructure.” Memories explicitly identifies itself as a **technical platform module**; its reusable hosting packages and adapter/composition responsibilities are intentional (Memories AD-1 and Design Paradigm). Its `Hexalith.Memories.Aspire` package owns the canonical qualified container digest set (AD-19), which consumers must use. Reading the Platform sentence as a prohibition on every module could remove an intended producer or duplicate that ownership in Platform.

**Smallest correction:** say **Domain modules** in that sentence. Platform remains the application composition root; technical modules continue supplying reusable hosting/SDK capabilities. Retain Memories' canonical digest producer when selecting the Platform profile; profile composition does not create a second independently maintained Memories digest set.

**Verified resolution:** the paragraph now explicitly restricts **Domain modules**, allows technical-module reusable hosting capabilities, and names `Memories.Aspire` as the qualified dependency image-digest producer consumed consistently by Platform.

## Reconciled safety and deployment boundaries

- **Dapr and FalkorDB:** Platform AD-9 preserves Dapr wherever it provides the required runtime capability, domain mutation through the EventStore SDK, and the expressly accepted bounded FalkorDB adapter. It does not grant blanket exemption to existing direct Redis coordination. Memories' older finite direct-Redis registry must be reconciled against this user-selected rule; an additional unsupported capability requires the named owner and bounded surface already required by AD-9. Current SDK spread is implementation work, not a reason to reject the accepted adapter boundary.
- **Tenant isolation:** AD-8 preserves module tenant isolation despite allowing compatible modules to share an environment instance. AD-6 and the module integration row retain actor/workload and per-tenant authority; environment namespaces or a single shared backend credential cannot substitute for Memories AD-5/6/15. Qualification remains outstanding rather than assumed.
- **Projection and coordination recovery:** The explicit Memories recovery row correctly excludes shared Redis/FalkorDB projections and tenant-keyed coordination from operational backup/restore and requires authoritative replay. Native backup tooling in the general recovery plan does not authorize blanket PVC restoration. Moving a coordination record behind Dapr does not remove its erasure obligation.
- **Erasure and key custody:** AD-12, the erasure row and the imported module rules preserve tenant-key destruction and refuse resurrection through restored secrets, ordinary data backups or re-import. Memories AD-16's tenant-exclusive protection of retained authoritative backups remains binding; global backup encryption alone is insufficient. The ordinary one-hour RPO does not permit loss of an acknowledged tombstone.
- **Live authority and the four-hour target:** The accepted finite disaster objective activates Memories' deferred independently durable synchronous **complete tombstone mirror** and qualified lineage protocol. Platform states that exact prerequisite, rejects a copied register as live authority, and fails closed when lineage is unknown. This is a valid implementation prerequisite rather than a claim that an ordinary restored database already satisfies AD-21. No implementation or new distributed system needs to be designed in this reconciliation.
- **Qualification:** Platform explicitly distinguishes target architecture from deployed capability and retains module functional, authorization, data-safety and release gates. Memories AD-20 still requires current re-runnable evidence; owner/date/risk acceptance and existing pods supply no gate credit. The Folders-only operational override does not waive Memories' erasure, isolation or production evidence requirements.

No other essential architecture gap found for this input. No implementation audit, runtime operation, source edit, upstream edit or Git mutation was performed; only this report was written.
