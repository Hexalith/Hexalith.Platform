# Tenants input reconciliation

Verdict: compatible. No essential Platform boundary is missing from the current draft, and no additional architecture decision is required.

## Authority inspected

- `references/Hexalith.Tenants/AGENTS.md` and the permitted root-declared `references/Hexalith.AI.Tools/hexalith-llm-instructions.md` baseline already read in this reconciliation run.
- `references/Hexalith.Tenants/_bmad-output/planning-artifacts/architecture.md`, using AD-1 through AD-14 as its explicit precedence layer and the adjoining conformance findings as implementation status.
- Platform `ARCHITECTURE-SPINE.md`, read on 2026-09-27 after the EventStore authority/authentication clarifications.

## Reconciled boundaries

| Boundary | Result |
| --- | --- |
| Transport and metadata | Platform's diagram note, Hosted interfaces convention and Tenants integration row retain BFF-to-Tenants direct REST reads, separate EventStore commands and metadata fidelity. This preserves Tenants AD-5/AD-6/AD-8; McpCli's generic gateway does not reroute the UI read path. The producer-first migration rule applies: expose distinct Tenants-query/EventStore-command service references and qualify ETag/cursor/freshness propagation before changing the BFF. |
| Hosting ownership | Platform preserves domain-owned UI and thin hosts while taking orchestration ownership. Tenants AD-13's UI remains a publishable application/container; Platform's Release/NuGet mode for consumed libraries does not turn that app into a UI package. Transitional Tenants.AppHost retirement remains subject to capability parity and applicable removal authority. |
| Authentication and data authority | Platform AD-6 retains shared EventStore JWT conformance, environment admission and module/tenant authorization. Tenants retains its server-side token relay and gateways, authoritative read hydration and support-safe output. Environment admission does not supply Tenants domain permissions, and the module's BFF remains responsible for authorization filtering. |
| Operational services and scaling | Shared health/telemetry, external secret configuration, usable module readiness and persistent truth outside application rollback align with Tenants AD-14. The explicit prohibition on scaling InteractiveServer beyond one replica without key/session/cursor evidence preserves the relevant deployment gate. |
| Memories dependency | Minimum composition includes Memories without assigning it authoritative Tenants state. Tenants AD-10 retains index-only search, authoritative hydration and outage fallback. Those module behavior details need not be duplicated in the Platform spine. |
| Release and qualification | Required module flows, real-service E2E, exact release evidence and module-owned safety gates keep the known Tenants implementation divergences open until proved. Finalizing Platform does not certify its query-metadata cutover, opaque search cursor, shared host operations or multi-replica readiness. |

The UI information architecture, command posture and local conformance tests remain in Tenants; they do not require further Platform invariants. Exact endpoint shapes, authentication middleware, DataProtection storage and session-routing implementation can remain producer-owned qualification work within the existing contracts.

Validation was a local document comparison. Only this report was written; no source, spine, memlog, Git state, runtime or upstream document was changed.
