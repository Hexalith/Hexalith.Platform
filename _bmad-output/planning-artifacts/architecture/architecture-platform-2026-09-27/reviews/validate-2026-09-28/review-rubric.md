# Architecture validation — good-spine rubric

Reviewed 2026-09-28. Target: `../../ARCHITECTURE-SPINE.md`, final, updated 2026-09-28. This review changes no architecture decision or source artifact.

**Verdict: CONDITIONAL PASS.** The spine covers the platform's capabilities, boundaries and operational envelope, and its existing implementation gaps have explicit owners and gates. Three remaining contract gaps affect Dapr configuration activation and the newly introduced in-place recovery path. Fix them before the affected deployment and recovery stories are finalized. Counts: **0 critical, 1 high, 2 medium, 0 low**; actions: **3 autofix**. No accepted risk or approved D1–D10 decision is reopened.

## Findings

### RB-1 — In-place recovery cannot reach the contract's recovery endpoint

- **Severity:** medium
- **Action:** autofix
- **Confidence:** high
- **Evidence:** `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:277`: “In production, Administrator or the deputy starts it on the production executor … a staging reset runs it on the staging executor.” The same row reuses “DR steps 3–6”. At `ARCHITECTURE-SPINE.md:314`, step 4 exposes the recovery hook on an endpoint “reachable only from the recovery executor”. At `ARCHITECTURE-SPINE.md:309`, the reuse is stated again: “in-place recovery reuses steps 3–6”.
- **Consequence:** An endpoint/network-policy team correctly admitting only the off-site recovery executor and an in-place recovery team correctly running on the production or staging executor cannot complete the same restore. The authorized incompatible-release recovery and staging-reset paths fail before module data reconstruction; using the off-site executor instead violates the chosen executor separation.
- **Mitigation already present:** AD-7 grants the production executor an operator-started recovery entry point; the attempt-record writers and epoch-takeover rule explicitly include in-place recovery. D5 in the memlog clearly settles this authority. This is a residual inconsistency in the shared steps, not a request to change D5.
- **Proposed correction:** Define “the executor owning the recovery attempt” in the shared recovery-hook contract: recovery executor for replacement-capacity DR, production executor for production in-place recovery, staging executor for a staging reset. Permit the internal recovery endpoint only to that environment's authorized recovery-attempt executor, using the same epoch and least-privilege hook authority. Apply that wording consistently to steps 3–6 and custody handoff descriptions; keep the off-site executor's replacement-capacity-only rule.

### RB-2 — Recovery-hook contract is scheduled after an earlier mandatory consumer

- **Severity:** medium
- **Action:** autofix
- **Confidence:** high
- **Evidence:** `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:441` assigns the recovery-hook contract the gate “First AD-12 drill”. At `ARCHITECTURE-SPINE.md:273`, “Each staging attempt first cuts a staging recovery point”; at `ARCHITECTURE-SPINE.md:274`, an incompatible unadopted candidate must be reset before the next staging attempt; and at `ARCHITECTURE-SPINE.md:277`, that reset uses DR steps 3–6. Those steps invoke the shared contract at `ARCHITECTURE-SPINE.md:314`.
- **Consequence:** Following the ownership table literally permits staging acceptance and a breaking candidate before the common recovery cut, quarantine, restore and result contract exists. Independent module teams can then ship incompatible early backup/restore hooks, or staging becomes unable to process its next candidate while waiting for work nominally due only at the G2 drill. The issue is the ordering of an owned gate, not the contract's present lack of implementation.
- **Mitigation already present:** Deferral explicitly cannot turn absent evidence into a pass; declarations carry recovery inventories and lifecycle tasks; the hook contract already lists the required fields. The owning team and contract scope are adequate. The generic statement that consumers wait for shared versions helps, but its concrete “Must precede” row misses the earlier named consumer and the spec copies that gate.
- **Proposed correction:** Move the recovery-point/cut subset before the first staging recovery point, and the complete restore/hook contract before the first staging reset or production in-place recovery, whichever comes first. The simplest conservative gate is first staging deployment. Keep the first AD-12 drill as the production drill's proof boundary. Reflect the corrected ordering in the spec's sequencing companion during Update.

### RB-3 — Dapr resources can change without activating the change in existing sidecars

- **Severity:** high
- **Action:** autofix
- **Confidence:** high
- **Evidence:** `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:140` requires hosted Dapr Configurations to “disable HotReload”. The environment tier at `ARCHITECTURE-SPINE.md:241` includes “every Dapr Component, HTTPEndpoint and MCPServer”, but its activation clause covers only “a changed Component restarts every workload whose sidecar loads it”. The application tier at `ARCHITECTURE-SPINE.md:240` owns Configurations, WorkflowAccessPolicies, Subscriptions and Resiliency, without an equivalent consumer-restart rule.
- **External verification:** [Official Dapr resource-update documentation](https://docs.dapr.io/operations/components/component-updates/), checked 2026-09-28, says disabled hot reload requires a sidecar restart to consume updated resources; its documented resource list includes Configurations, WorkflowAccessPolicies, Resiliency, Subscriptions and HTTPEndpoints. No claim about current live deployment behavior is made here.
- **Consequence:** A chart/helper implementation that changes only a Dapr policy or Subscription object, and an environment-layer implementation that restarts only Component consumers, both satisfy the literal rules. Existing pods can continue using old authorization, subscriptions or HTTP endpoint configuration while the retained chart and attempted configuration report the new objects. This breaks the release-to-runtime identity that AD-2 and AD-15 rely on, and a security-policy change can remain inactive until an unrelated restart.
- **Mitigation already present:** AD-3 counts any changed release-owned rendered object as a changed workload for recovery; required readiness compares attempt-bound digests; every attempt is re-verified and Dapr HotReload behavior is a qualification topic. Those controls can detect individual consequences, but they do not require a restart when only a non-Component Dapr object changes. The existing Component restart rule demonstrates the intended pattern.
- **Proposed correction:** State one activation invariant for every Dapr resource with HotReload disabled: its changed effective content or binding causes all affected consuming sidecars to restart under the owning attempt before readiness or qualification can pass. Apply it to both application-package and environment-layer resources and include affected consumers when computing rendered workload identity. Keep checksum annotations or equivalent rollout plumbing as implementation seed; qualify at least one policy-only and one HTTPEndpoint-only update, without changing application images.

## Good-spine checklist

| Element | Result | Evidence and judgment |
| --- | --- | --- |
| Fixes real divergence points for the level below | Pass with RB-1/RB-3 | AD-1–AD-15 bind ownership, source identity, release artifacts, state mutation, executor trust, identity, Dapr access, run lifecycle, tool surfaces, recovery, host extension and rollback rules. The remaining gaps concern shared recovery actors and activation semantics. |
| Every Rule is enforceable and prevents its stated divergence | Pass with RB-1/RB-3 | Rules usually name authorities, immutable identities, negative tests, fail-closed behavior and owning implementation gates. The endpoint restriction conflicts with an authorized executor, and Dapr consumer restart coverage is incomplete. |
| Deferred contains no unsafe divergence | Pass with RB-2 | First shared versions name producers/consumers and fail closed before use. Exact provider and custody mechanisms can be settled under those contracts. The recovery-hook due point needs to precede its early staging consumer. |
| Named technology is verified-current and fits | Evidence present; dedicated reality lens remains authoritative | The memlog has dated primary-source checks and read-only reality observations through 2026-09-28, and Stack explicitly distinguishes existing pins from upgrades. The current review independently checked the Dapr activation claim. It did not repeat every package, cluster or registry check. |
| Ratifies brownfield reality without inventing compliance | Pass | Root `apphost.cs`, `.gitmodules` and `global.json` match the recorded Works preview, source declarations and stack seed. No root `.config/dotnet-tools.json` exists; the spine correctly puts the first accepted tool/pin behind ratification. Existing code that lacks the target design is explicitly owned migration work. |
| Covers the driving spec's capabilities | Pass | Current spec CAP-1–CAP-12 map to FR-1–FR-12; the spine's capability map covers all FRs/NFRs. Source/debugging, test lifecycle, release modes, G1–G3, deputy recovery, access isolation, McpCli and recovery envelope are represented. No capability was silently dropped. |
| No inherited invariant is weakened | Pass within stated authority | This is an initiative spine, with no parent spine claimed. Named module contracts remain binding except the specifically authorized Folders/Projects infrastructure and McpCli surface/source overrides. The source precedence table and memlog distinguish those overrides from the still-required module release, data-safety and authorization evidence. |
| Every owned structural dimension is decided, deferred or open | Pass | Composition, dependency/runtime boundary, state ownership, release/provenance, identity, network/storage, operations, environment tiers, observability, recovery, capacity, key custody and migration have explicit homes. No whole environmental or operational dimension is absent. No invented HA, uptime, scale or cost commitment is needed. |

## AD enforceability sweep

| AD | Assessment |
| --- | --- |
| AD-1 | Enforceable composition and deployment ownership; exporter failure has an explicit fallback and qualification gate. |
| AD-2 | Digest-retained artifact and writer/provenance rules are concrete; runtime activation has RB-3. |
| AD-3 | Tier boundary, forward inputs, changed-workload identity and a single recovery mechanism are clear. |
| AD-4 | Source/package mapping, direct submodules, identity pinning and failure behavior are explicit and preserve accepted source-mode warnings. |
| AD-5 | CI isolation and blocking integration tier have a defined runner and credential boundary. |
| AD-6 | Realm, admission, administration and credential rules are explicit; recovery exception preserves deputy authority. |
| AD-7 | Distinct executor purposes, workflow trust and module-code sandbox are explicit; shared-step endpoint wording needs RB-1. |
| AD-8 | Environment state, principals, volumes, network and Dapr isolation have a concrete negative-test matrix; Dapr activation needs RB-3. |
| AD-9 | Portable defaults and named exceptions prevent uncontrolled provider access; declaration-based component naming/subscriptions can be validated. |
| AD-10 | Ownership, terminal outcome, retained debugging, attach and cleanup behavior are deterministic. |
| AD-11 | Publication, executable catalog eligibility, actor attribution and legacy retirement rules preserve the approved McpCli direction. |
| AD-12 | Recovery classes, key separation, erasure continuity and independent capacity are coherent targets with qualification gates; early consumer ordering needs RB-2. |
| AD-13 | Shared-host owner, extension compatibility and exact release subjects are explicit; requested upstream contracts are gated. |
| AD-14 | Client-derived surfaces and user-actor chains are explicit. EventStore current-authorization requirements remain binding; its requested attestation has an explicit safe interim limitation. |
| AD-15 | Baseline/candidate retention and expand-only dependencies are comprehensive; resource activation must match their desired state under RB-3. |

## Source alignment and positives

- The current spec was re-synced on 2026-09-28. Its constraints, acceptance and sequencing companions carry the accepted deputy, release modes, source identity and production gate refinements. The spine's residual spec re-sync work item is stale housekeeping, not a blocking architecture finding.
- The PRD addendum still contains older single-writer and embedded-tool-commit wording. The spec's source-alignment table records those exact discrepancies, and both the spine's precedence rules and accepted memlog decisions settle them. This review does not recast known upstream maintenance as a new architecture ambiguity.
- Production entry and DR proof are separated from a document being final. Missing implementation evidence remains gated; unsupported live infrastructure, migration work and a prerelease exporter are not presented as production qualification.
- All seven MVP modules, the module-owned checks and data inventories, and the approved generic McpCli migration are carried through. The platform avoids assigning itself domain semantics or a second event ledger.
- Records have named authenticated writers and durable stores; environment-current authority is kept distinct from application rollback; recovery authority is distinct from admission and promotion-stop authority.
- The accepted single-node topology, manual unseal, bounded response coverage, GitHub notification dependence, two named operations writers, plaintext in-namespace Redis/FalkorDB transport and ordinary-data RPO exceptions were treated as binding accepted choices.

## Scope and limitations

Read all 481 lines of the current spine, its decision history relevant to the latest update, current SPEC and sequencing/acceptance context, the driving PRD and its addendum, the approved McpCli course correction, the previous confirmation review and representative root source/configuration. Mechanical lint was reported as zero findings by the parent gate. This review ran no app, test suite, executor, cluster, restore or repository-setting operation. It validates architectural consistency, not actual isolation, backup usability, timing performance or deployed conformance. Version completeness is intentionally left to the independent reality reviewer, while security and recovery specialists may find additional issues beyond this rubric sweep.
