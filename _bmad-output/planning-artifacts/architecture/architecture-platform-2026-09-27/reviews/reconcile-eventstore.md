# EventStore input reconciliation

Verdict: compatible at Platform altitude, with three small contract clarifications recommended before finalization. No new user decision or extra infrastructure tier is needed. Missing implementation or production evidence remains qualification work; this review does not certify readiness.

## Authority inspected

- `references/Hexalith.EventStore/AGENTS.md` and the permitted root-declared `references/Hexalith.AI.Tools/hexalith-llm-instructions.md`; the root `.gitmodules` declares the latter repository.
- EventStore canonical AD layer: `references/Hexalith.EventStore/_bmad-output/planning-artifacts/architecture.md`, especially AD-2, AD-9 through AD-12, AD-22 through AD-26, AD-28 and AD-33.
- Platform `ARCHITECTURE-SPINE.md`, read on 2026-09-27 with the accepted Folders operational override present.

## E-1 — Make consumer-removal authority explicit

**Severity:** medium; the current migration sentence can be read as parity being sufficient authorization.

**Locations:** Platform Consistency Conventions, “Migration”; EventStore AD-22.

**Evidence:** Platform requires the owning module's source/package/deployed parity and preserves independent release gates. EventStore AD-22 also requires an authenticated consumer-owner receipt bound to the exact removal subject, a complete consumer-removal manifest, and the shared validator. This is a separate transition from establishing capability parity. A module migration implementer could otherwise remove its infrastructure as soon as tests pass.

**Smallest correction:** Add: “Infrastructure retirement follows EventStore AD-22, including the complete consumer-removal manifest, validator and authenticated consumer-owner receipt for the exact removal subject; parity alone does not authorize removal.” Keep the detailed receipt and packet schema in EventStore.

## E-2 — Bound replacement of a ratified production profile

**Severity:** medium; clarification of an existing authority boundary.

**Locations:** Platform “Production profile authority” and “Shared runtime/profile evidence”; EventStore AD-26.

**Evidence:** Platform correctly leaves the current profile unratified and requires tested compatibility. Its statement that provider names/versions are “replaceable seeds” does not explicitly retain AD-26's rule that adding, replacing or retiring an authorizing profile requires an approved architecture change and a new digest derived from the retained canonical bytes. Initial provider choice is still open, but an eventual authorized profile must not be replaceable as ordinary deployment configuration.

**Smallest correction:** Qualify the seed sentence: “Initial provider choices remain seeds until qualification; changing an authorizing profile follows EventStore AD-26 architecture approval, a new canonical-byte digest, and renewed exact-subject evidence/authority.” This preserves the shared profile without importing any Folders-only PostgreSQL-v2 or stricter recovery gate.

## E-3 — Name the shared authentication producer contract

**Severity:** medium; prevents independently implemented host validation.

**Locations:** Platform AD-6 and “Secrets, identity, network and transport setup”; EventStore AD-10 and AD-28.

**Evidence:** Platform defines realms, issuer/audience checks, admission and operation authorization, and requires Dapr authenticated channels. EventStore AD-10 additionally assigns JWT validation and the host/config fingerprint inventory to `EventStore.ServiceDefaults`; every external or JWT-binding host must consume that contract and pass conformance. AD-28 defines a separate app-channel scheme. The composition contract should name these owners so an enrolled module cannot satisfy the summary by adding a locally implemented JWT validator.

**Smallest correction:** Add a short reference: “Enrolled hosts consume the EventStore AD-10 shared JWT contract and fingerprint conformance gate, with the distinct AD-28 app-channel authentication contract; Platform supplies environment bindings.” Do not copy algorithm lists or module-local middleware details into this spine.

## Reconciled boundaries

- **SDK and runtime:** Platform composes topology and retains technical-module ownership of hosting, persistence, projections, testing fixtures and diagnostics. The accepted Memories adapter exception does not grant raw domain-state access. Thin module hosts and UI contracts remain compatible with this boundary.
- **Secrets:** Platform explicitly inherits AD-24 and owns the single value-free OpenBao contract, scoped grants, readiness and acknowledged rotation. This reference retains sole composition of the OpenBao component and per-app configuration, startup-token rollout, and prohibition on premature digest-key retirement without duplicating them.
- **Routing:** Platform owns the content-bound catalog instance while EventStore.Contracts owns schema/codec. Shared registration, prepare/ready/commit, and owner-governed catalog restoration preserve AD-25/AD-33 generation continuity. McpCli discovery does not create a second routing authority.
- **Release:** Retained Helm/application identities supplement EventStore AD-11 authority; they do not replace Builds-owned OCI/package validation, separate release-owner/deployment-owner records, or exact-profile production proofs. These details can remain in EventStore because the spine preserves mandatory module publication/profile authority and exact-subject qualification.
- **Rollback and recovery:** AD-3 excludes persistent state, credentials, key generations and durable authority from application rollback, requires old code to read current state, and defers incompatible rollback procedures. AD-12 respects module-authoritative recovery boundaries and fences old writers. No broker-backlog recovery is inferred from database backup.
- **Evidence scope:** EventStore's unratified AD-26, append-fencing, Operations wiring/capture and host-conformance gaps remain qualification dependencies. A final Platform architecture is not evidence that they passed. The accepted Folders override changes its operational targets and provider preference, not EventStore's shared profile ratification or behavioral/idempotency proofs.

Validation was a local document comparison. No runtime, source, spine, memlog, Git state or upstream document was changed; this report is the only written artifact.
