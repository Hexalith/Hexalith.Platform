# Parties input reconciliation

Verdict: PASS. The CLI/MCP surface-eligibility clarification below was applied and verified on 2026-09-27. No essential architecture gap remains for this input. This verdict does not establish implementation or production qualification.

## Authority inspected

- `references/Hexalith.Parties/AGENTS.md`; the module resolves to the Platform superproject, whose root-declared AI.Tools baseline was already read.
- `references/Hexalith.Parties/_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md`, final, updated 2026-09-08: inherited Epic 7 contracts, technical producer ownership, I1–I20, remaining-work gates and Deferred.
- Current Platform `ARCHITECTURE-SPINE.md`, especially AD-1, AD-2, AD-3, AD-6, AD-9, AD-11, shared hosting/catalogs, migration, diagnostics and source integration.

## Resolved PA-1 — An authorized operation is not necessarily allowed on MCP

**Severity:** high; interface authorization boundary.

**Location:** Platform AD-11 and its Parties integration row.

Parties I7 fixes two erasure doors: Admin and self-scoped Consumer. It explicitly forbids an MCP erasure door: `delete_party` means `DeactivateParty` and returns `gdprErasurePerformed = false`. Expanding that surface requires the Parties I20 owner gate; an authorized administrator is not enough. Platform AD-11 currently defines connected availability as the intersection of bundled compatible Contracts, committed routing catalog and applicable authorization, then preserves module-specific **confirmation** restrictions. The erasure-door prohibition is broader: even a caller allowed to erase through another surface must not discover or invoke erasure through generic MCP execution.

**Smallest correction:** require module-declared surface eligibility in connected discovery **and** gateway execution, in addition to ordinary caller authorization. Explicitly retain Parties' MCP soft-deactivation/no-erasure rule in the integration boundary. No second schema or policy registry is needed: eligibility stays with module-owned contracts/security semantics and is enforced by the existing technical surfaces.

**Verified resolution:** AD-11 now includes module-declared surface eligibility in connected discovery, requires gateway enforcement of the permitted operation/surface contract, preserves those restrictions even for otherwise authorized users, and explicitly prohibits Parties MCP erasure operations.

## Reconciled boundaries

- **Technical producers and domain authority:** Platform is the approved composition owner; EventStore/shared technical modules retain hosting, projection/query and generic security mechanics. Parties keeps domain contracts, self-scoped authorization, GDPR policy/legal vocabulary and compatibility hooks. Keycloak environment realms and Dapr transport protection do not replace those policies. The Platform draft does not assign legal strings or aggregate erasure decisions to a shared engine.
- **Gateway and ingress:** The imported Parties I1 rule still controls internal gateway-only routes and deny-default Dapr ACL tuples, including environment-specific deployment copies. Platform route declarations/profile composition cannot create a public Parties domain-host API or unapproved ACL expansion. Its module authorization and qualified profile gates preserve this boundary.
- **Adapter-first migration:** The Migration convention retains producer-before-consumer sequencing, module source/package/deployed parity and applicable EventStore exact-subject removal authority. Parties' more specific I1a/I3/I16–I20 gates remain additional constraints: retained rollback seams, proof at the consumed dependency identity, referenced-deferral exit/reapproval and recorded owner authority. Generic Platform parity is not permission to remove its AppHost or guarded adapters.
- **Recovery and privacy:** Module-approved recovery inventory plus Parties I19 keeps event-derived projections distinct from non-replayable operational side-effect ledgers. AD-3 does not rewind events, key generations or durable authority. Parties self-scope, permanent erasure/tombstones, payload readability/redaction and PII-free evidence remain module-owned safety rules, not replaceable provider configuration.
- **Qualification:** The selected Parties representative export composition is a future qualification exercise, not evidence that its migration gates or current dependency approvals already pass. No source migration, deletion or upstream gate waiver is inferred from Platform enrollment.

No other essential architecture gap found for this input. No runtime, source, upstream or Git changes were made; only this report was written.
