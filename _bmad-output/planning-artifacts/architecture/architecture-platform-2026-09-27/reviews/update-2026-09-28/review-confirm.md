# Confirmation review: update run 2 gate fixes (2026-09-28)

- **Subject:** `ARCHITECTURE-SPINE.md` after the gate fix pass (481 lines, status draft, updated 2026-09-28). Line numbers (Lnnn) refer to this file.
- **Reviews checked:** `review-rubric.md` (RB-1..27), `review-reality.md` (RV-1..15), `review-adversarial.md` (ADV-1..21), `review-reconcile-r2.md` (RR-1..20, covering r2 clusters C-01..C-64 and regressions G-1..G-13), `review-reconcile-inputs.md` (RI-1..20), `review-pragmatism.md` (PR-201..232).
- **Decisions checked:** `.memlog.md` batches 1–6 (after "Update run 2 started 2026-09-28") and gate decisions D1–D10, including D4 (second org owner accepted as a named writer; org base permission read or none).
- **Mode:** read-only. This file is the only output.

## Verdict: PASS WITH FIXES

Every high finding in the six reviews has landed, either as the proposed wording or through D1–D10. No decision from this run is reversed. The fix pass left two findings partial and introduced or exposed 20 wording conflicts (CF-1..CF-20: 0 critical, 0 high, 8 medium, 12 low). Each has a one-sentence replacement below. The medium ones matter most: a production precondition that no candidate can meet after an environment-layer change (CF-1), environment-layer inputs applied both before and during the attempt (CF-2), two clauses that still block the D5 deputy path (CF-3, CF-4), fence hooks that have no place to run (CF-5), and a dropped source-mode resolution rule (CF-7).

### Counts (135 findings)

| Status | Count | Findings |
| --- | --- | --- |
| addressed | 105 | all not listed below |
| addressed-by-decision | 20 | RB-1 (D2), RB-2 (D1), RB-4 (D3), RB-5 (D8), RB-15 (D9), RV-1 (D4), ADV-4 (D5), ADV-5 (D10), ADV-7 (D6), ADV-10 (D5), ADV-17 (D2), ADV-18 (D7), ADV-19 (D7), ADV-21 (D8), RR-1 (D1), RI-9 (D7), RI-13 (D2), RI-19 (D5), PR-204 (D1), PR-208 (D1) |
| partially addressed | 2 | ADV-11 (CF-1), RI-1 (CF-3, CF-4) |
| not addressed | 3 | PR-230, PR-231, PR-232 (all low; optional moves; no decision recorded) |
| declined-ok | 3 | PR-217, PR-228, PR-229 (parts b–e) |
| deferred-ok | 2 | RB-27 (except the assignee clause, which landed), RV-15 |

## 1. Finding classification

### Rubric (RB)

| ID | Status | Spine text that lands it |
| --- | --- | --- |
| RB-1 | by decision D2 | AD-4 *Package mode* L99: "The tool builds the Platform composition from the Platform submodule at a Platform release tag. The module under test builds in Release against NuGet library packages …; dependency services run from their released images at the catalog version, the same images staging runs". L101: "The identity pins the Builds catalog commit and a supported Platform-tool version range." (See CF-7 and CF-8.) |
| RB-2 | by decision D1 | Production profile L228 **Dapr skew**: "workloads pin the release's sidecar patch; within the template's Dapr minor, a release stays usable after a control-plane patch change once re-verified and qualified at the new patch; a Dapr minor change is breaking for releases pinned to the previous minor" |
| RB-3 | addressed | Release tiers L241: "a changed Component restarts every workload whose sidecar loads it, with the application deploy identity, before re-verification" |
| RB-4 | by decision D3 | Data protection L227: "node-level volume encryption with keys under Administrator and deputy custody … TLS where the provider supports it natively"; Source Precedence L321: "in-transit encryption beyond the Data protection row"; Accepted risks L481: "plaintext Redis and FalkorDB connections" |
| RB-5 | by decision D8 | Synthetic identities L232: "Production smoke suites never create or delete tenants; staging E2E suites may create and delete only run-scoped tenants carrying the synthetic exclusion marker …; run-owned local and CI environments are exempt" |
| RB-6 | addressed | Binding classes and records L223 names a writer for every record kind; AD-2 L79: "records only from their writers (Binding classes and records)" |
| RB-7 | addressed | First shared versions L442–444: "Profile template …" and "Attempt lock, record and promotion-stop store" both "First staging deployment"; Owned work L468: "Production instance of the attempt, lock and stop store" |
| RB-8 | addressed | Release tiers L241: "every Dapr Component, HTTPEndpoint and MCPServer" |
| RB-9 | addressed | Module declaration L220: "the configuration key through which the module reads its bound component name"; AD-9 L150 |
| RB-10 | addressed | Startup task lifecycle L221: "once per module per environment — when the environment is created or the module is first enrolled into it — … succeed idempotently against data objects that survived a removed first enrollment" |
| RB-11 | addressed | AD-4 L101: "Every workspace other than the Platform repository … in the Platform repository the identity is its HEAD" |
| RB-12 | addressed | AD-4 L97: "initializes only the active root's direct `references/*` submodules, never recursively, and fails naming any initialized nested submodule" |
| RB-13 | addressed | L220: "`hexalith.module-manifest.v1`, identified by its `schema` value, lacks most of these fields, so the Platform declaration ships as its next major"; L431 "the next `hexalith.module-manifest` major"; L451 "(EventStore, Dapr runtime and SDK, FrontComposer)" (see CF-17) |
| RB-14 | addressed | Recovery hook contract L441: "cut identity (the Platform-declared cut and the position each recovery class reaches at or before it, including EventStore position and Memories register sequence)" |
| RB-15 | by decision D9 | AD-8 *Volumes* L137: "created by the shared-infrastructure identity or by the storage provisioner through an environment-specific StorageClass; quotas deny …" |
| RB-16 | addressed | Paragraph after Release tiers L245: "… live in the operations repository; each attempt records their digest, and the pinned off-site recovery copy carries them". This restores the HEAD clause (RR-19 / G-11), so no new D entry was needed. |
| RB-17 | addressed | AD-8 L136: data namespace holds "Gateway and listener Certificates"; Hosted interfaces L231: "User-ingress admission — open, or executor and probe sources only — is environment-layer state on the Gateway"; G1 L469: "ingress-closure mechanism" |
| RB-18 | addressed | AD-14 *Classes* L199; AD-6 L114: "the surface-class vocabulary"; L438: "Realm contract with surface classes" |
| RB-19 | addressed | Capability map L407: "Module enrollment (any module)"; FR-7/FR-8 row names the sections; AD-7 in NFR-3; AD-2/AD-6 in FR-9; AD-4 in FR-12 (see CF-18) |
| RB-20 | addressed | AD-12 L181: "a raw volume or snapshot copy never counts toward a recovery point" |
| RB-21 | addressed | Terms L37 covers tiers, attempt, lock, epoch, rollback set, mode, gateway versus Gateway, CI and the Builds release record |
| RB-22 | addressed | Writers appear only in Binding classes (AD-2 points there); Attempt ownership L278: "Shared-infrastructure changes hold both locks (Release tiers)"; "one named change owner" moved to L242 |
| RB-23 | addressed | AD-3 L88: "`--rollback-on-failure`, `helm rollback` and any controller-driven rollback are forbidden; the only automatic recovery is the one in Automatic recovery" |
| RB-24 | addressed | (a) L231 "bypasses ingress"; (b) AD-6 L114 "in production by Administrator, in staging by the staging management client"; (c) L474 "`_bmad-output/specs/spec-platform/SPEC.md`" |
| RB-25 | addressed | AD-9 L149: "No module builds a custom or pluggable Dapr component only to route a named exception through Dapr." |
| RB-26 | addressed (defer, landed) | Owned work L467: "Image and dependency vulnerability policy" |
| RB-27 | deferred-ok | Only the assignee clause needed text: Diagnostics L233 "assigned to Administrator and mentioning the recovery deputy". The rest was flagged for the reality lens. |

### Reality (RV)

| ID | Status | Spine text that lands it |
| --- | --- | --- |
| RV-1 | by decision D4 | Roles L35: "**Named writers** of the operations repository are Administrator and the second Hexalith organization owner"; AD-7 L128: "whose only writers are the named writers; the organization base repository permission is read or none"; L453; L475 trigger; Accepted risks L481 |
| RV-2 | addressed | Hosted interfaces L231: "the port-80 listener also admits ACME solver routes from the Gateway's own namespace" |
| RV-3 | addressed | L463: "cert-manager Gateway API support with per-environment `gatewayHTTPRoute` issuers replacing the `nginx-public` solvers" |
| RV-4 | addressed | L231: "exact FQDNs, never wildcards …"; "the application deploy identity cannot create Ingress, `traefik.io` or `hub.traefik.io` objects" |
| RV-5 | addressed | AD-1 *Chart* L67; qualification L461: "HTTPRoutes emitted with a namespaced parent reference and no Gateway in the chart" |
| RV-6 | addressed | AD-1 L67: "never Aspire connection-string resources, which are always secret; … the rendered chart contains no Secret object"; L461 |
| RV-7 | addressed | L438: "the `downscope-assertion-grant-enforcer` policy on every requester, full scope off" |
| RV-8 | addressed | L451: "Versions through 4.27.4, already on NuGet, are not Platform-accepted …; correct the README's publication statement"; L431; Builds row L328 |
| RV-9 | addressed | Workflows L229: "Every writer to a retained repository holds create without update or delete, and registry garbage collection and retention never remove a retained digest" |
| RV-10 | addressed | G1 L469: "authenticated read with a declared per-environment credential mechanism" |
| RV-11 | addressed | Workflows L229: ruleset with code-owner review, CODEOWNERS for `.github/workflows/**`, tag ruleset |
| RV-12 | addressed | Secrets L226: "Dapr reads a token once at sidecar start, so tokens are renewed outside Dapr and a rotation restarts the pod" |
| RV-13 | addressed | L233: "stale beyond two scheduled intervals" |
| RV-14 | addressed | G1 L470 adds cert-manager, Gateway API CRDs, the storage provisioner and "KubeSphere current or removed"; Stack L350; L463 "Dapr sidecar drop-all-capabilities (live: off)" |
| RV-15 | deferred-ok | This is implementation detail under the G1 external negative probe (L469). |

### Adversarial (ADV)

| ID | Status | Spine text that lands it |
| --- | --- | --- |
| ADV-1 | addressed | L231: Gateway and Certificates "live in that environment's data namespace and are written only by its environment-layer identity"; route field list; "reject any staging listener, route, Certificate or CertificateRequest that names a reserved or out-of-pattern host or references a ClusterIssuer" (see CF-16) |
| ADV-2 | addressed | DR step 4 L314: hooks "run as recovery-scope tasks inside the owning module's restored workload …"; step 2 L312: "Quarantine admits the recovery executor and the restored release's workloads"; AD-11 *Scope* L172 (fence hooks remain open, see CF-5) |
| ADV-3 | addressed | Step 5 L315: "Rotate the restored synthetic clients' credentials under the run's DR-scoped realm rights"; AD-7 L129: "synthetic-client material per job"; L232: "or a DR run" (see CF-11) |
| ADV-4 | by decision D5 | Lost window L290: "The recovery runner records them in the DR report before reopening, and Administrator reviews them before clearing the promotion stop." |
| ADV-5 | by decision D10 | Step 1 L311: "Each surviving authority's fence-and-reissue owner revokes …"; step 5: "Each surviving authority's owner issues the replacement instance's credentials there"; step 6; Backup coverage L285 (see CF-5, CF-10) |
| ADV-6 | addressed | Empty or degraded production L276: "lifts the promotion stop and precondition 8 for that attempt only; its terminal outcome re-sets the stop unless working"; Promotion stop L283: "except as Empty or degraded production allows" |
| ADV-7 | by decision D6 | L437: "Admission predicate and projection …"; AD-14 *Chains* L203: "against EventStore's admission projection, failing closed when unknown, stale or revoked"; AD-6 L117: "no application principal calls the Keycloak admin API" |
| ADV-8 | addressed | AD-14 L202: "Every server-to-server step carries a user actor; a module service client's own subject is never the actor … and holds no tenant membership"; L203: "a chain that starts outside EventStore admission has the `service` class" |
| ADV-9 | addressed | Catalogs L224: "plus the idempotency and key entries of the environment's currently committed generation, which persist as non-executable retention entries"; AD-15 L212 |
| ADV-10 | by decision D5 | In-place recovery L277; AD-7 *Operator-started recovery* L129; Binding classes L223: "the production executor for in-place recovery"; Release modes L275: "runs as an in-place recovery" |
| ADV-11 | partial (D1) | Production profile L228 issues staging and production qualification records per D1, and DR step 2 L312 uses "a later digest within the recovered release's effective sets". **Gap:** precondition 6 still requires production-effective sets before the attempt that issues them (CF-1). |
| ADV-12 | addressed | L223: "each accepted only when signed by its writer, never by store access"; the pointer is "moved only by the writer of a working terminal attempt of that environment with a higher epoch" |
| ADV-13 | addressed | Staging reset L274: "not adopted once a later candidate's staging attempt starts without an Administrator record reserving it … runs an in-place recovery" (see CF-9) |
| ADV-14 | addressed | Release tiers L241 and the paragraph at L245: "rendered from the union of the working baseline's and the candidate's release records" (see CF-2) |
| ADV-15 | addressed | AD-9 *Subscriptions* L151 |
| ADV-16 | addressed | AD-15 *Expand-only* L213 now lists the surface map, exchange permissions, admission groups, synthetic clients, HTTPEndpoints, MCPServers and Gateway listeners |
| ADV-17 | by decision D2 | AD-4 L101: "refuses a composition commit that differs from the submodule HEAD, a Builds submodule that differs from the pinned catalog, or a tool outside the range; source mode warns" |
| ADV-18 | by decision D7 | AD-11 L169: "builds a run-scoped, never-published McpCli whose Contracts resolve through the AD-4 mapping"; McpCli row L327: "source-mapped Contracts are accepted by assembly identity" |
| ADV-19 | by decision D7 | AD-11 L169: "McpCli's own pipeline publishes stable `Hexalith.McpCli.Abstractions` …, versioned by McpCli base and Platform release and never reusing a version" |
| ADV-20 | addressed | AD-11 L173: "Temporary compatibility use happens only outside every Platform composition" |
| ADV-21 | by decision D8 | L232: "Tenants owns the synthetic tenant aggregate through one idempotent creation task keyed by that identifier" |

### Reconciliation of r2 findings (RR)

| ID | Status | Spine text that lands it |
| --- | --- | --- |
| RR-1 | by decision D1 | L228 **Dapr skew** and **Qualification** per environment |
| RR-2 | addressed | L276 and L283 (as ADV-6) |
| RR-3 | addressed | Step 4 L314: "admission and purge, backend-principal and dynamic-credential re-provisioning and rotation, then rebuild-only replay"; step 5: "which only the owning module's hook rotates in step 4" |
| RR-4 | addressed | Owned work L452: "Runner lifecycle, ownership and resource isolation … Prove active-root mapping, finite readiness, exact cleanup … No custom DSL or environment service." |
| RR-5 | addressed | L273: "the production working baseline they were computed against"; L222: "names the working baseline it was computed against" |
| RR-6 | addressed | Catalogs L224 lists the activation steps; diagram L259: "candidate hosts validate prepared generation" |
| RR-7 | addressed | L231: "Platform never renders on the shared `nginx-public` compatibility class; existing shared Ingresses stay there only until migrated"; certificate rejection covers G-13 |
| RR-8 | addressed | L273: "Staging catalog, key and secret retirements follow AD-15 relative to production's working baseline"; L274: "Additive unadopted candidates need no reset" |
| RR-9 | addressed | AD-7 L129: "Restored artifacts and records are provenance-verified …"; step 2: "after verifying its replicated provenance" |
| RR-10 | addressed | AD-6 L118: "hosted refresh material has a bounded idle and maximum lifetime and lives only in the OS credential store"; L117: "map audiences and roles explicitly" |
| RR-11 | addressed | Step 2 L312: "into quarantine under custody" |
| RR-12 | addressed | L233: "warning a declared lead time before each expiry and listing upcoming expiries at every drill" |
| RR-13 | addressed | L273: "rehearses the AD-15 rollback set with HotReload off" |
| RR-14 | addressed | L229: "`registry.tache.ai` is not a retained-artifact store" |
| RR-15 | addressed | Verification L281: "Production runs only the immutable check-suite digests bound in the release record that passed staging …" |
| RR-16 | addressed | AD-1 L69: "neither they nor legacy deployments are ever a second writer" |
| RR-17 | addressed | AD-6 L117: "The Keycloak admin API and console, the master realm and cluster consoles are reachable only from a declared Administrator path" |
| RR-18 | addressed | L223: "readiness compares attempt-bound digests" |
| RR-19 | addressed | L232 "flagged in their tokens"; L278 "to perform its remaining single recovery"; L342 "checked by the Platform tool"; L245 operations-repository home; L35 "recovery owner" |
| RR-20 | addressed | L431 (the next major, per RB-13); L325 `openbao` now under "Requested from EventStore"; AD-2 and AD-5 tagged [ADOPTED, AMENDED] |

### Input reconciliation (RI)

| ID | Status | Spine text that lands it |
| --- | --- | --- |
| RI-1 | partial (D5) | In-place recovery L277 and AD-7 L129 land D5. **Gap:** L276 "A manual change is always an Administrator-approved attempt" and the L278 takeover rule can still block a deputy-run recovery (CF-3, CF-4). |
| RI-2 | addressed | Release modes L275: "When compatibility evidence is missing, stale, wrong-baseline, failed or breaking, the record names …"; precondition 9 L305 |
| RI-3 | addressed | AD-4 L98: "EventStore, Memories and McpCli are root-declared in Platform and are … qualified as Platform evidence only from the Platform workspace" |
| RI-4 | addressed | Precondition 7 L303: "with its production qualification evidence present"; L273: "including unchanged modules" |
| RI-5 | addressed | G3 L284; L452: "Repeat SM-3 lifecycle scenarios"; L473: "repeat whenever recovery mechanisms change" |
| RI-6 | addressed | L474: gate "Before recovery or deployment stories are finalized", with addendum items named |
| RI-7 | addressed | As RB-13 |
| RI-8 | addressed | L452: the AD-10 lifecycle list plus "resolve `HXR003`" |
| RI-9 | by decision D7 | AD-11 L169: "its first increment stays prerelease until the first Platform staging validation" |
| RI-10 | addressed | AD-11 L174: "No module-owned or other proprietary Hexalith MCP/CLI host is admitted, and Platform publishes no other such surface"; L476 owner "McpCli with Platform" |
| RI-11 | addressed | L232: "except the G1 SM-4 temporary grant"; G1 L284 carries the limits |
| RI-12 | addressed | Step 5 L315: "a compromise-driven restore rotates the Dapr trust root and realm signing keys without that exception" |
| RI-13 | by decision D2 | AD-4 L101 tool-range check; D2 records the supersession of addendum L34 |
| RI-14 | addressed | L233: "outcome status" |
| RI-15 | addressed | L287: "actual response time"; L288: "recovered-data age, coverage status, full elapsed time and every substitution" |
| RI-16 | addressed | L230: "with its justification … reported as an override … is a configuration error"; AD-10 L162: "an incomplete cleanup is never recorded as complete" |
| RI-17 | addressed | G2 L284: "tombstone and key continuity"; L273 baseline; L470: "retain evidence of actual configuration and versions" |
| RI-18 | addressed | AD-11 L172: "recovery hooks invoked only through the recovery hook contract"; L173: "audit, structured-output and exit-code parity" |
| RI-19 | by decision D5 | Roles L35: "whether or not Administrator is available"; L287: "deputy hours count toward coverage only after the G2 deputy proof" |
| RI-20 | addressed (defer, landed) | L451: "confirm the Platform AppHost form and Aspire testing-builder compatibility" |

### Pragmatism (PR)

| ID | Status | Spine text, or reason |
| --- | --- | --- |
| PR-201 | addressed | AD-3 L88 (as RB-23) |
| PR-202 | addressed | L234: "Legacy MCP/CLI sources retire under AD-11." |
| PR-203 | addressed | Settled through RB-13 (next major); the Projects `hexalith.module.v1` chain is gone |
| PR-204 | by decision D1 | L228: "an environment-layer or shared change's attempt in each environment …"; L223: "qualified environment-layer and shared sets" |
| PR-205 | addressed | L326: "which omits staging, are conformance inputs only (AD-1); the Platform staging gate applies to Memories" |
| PR-206 | addressed | L33: "existing ones follow Migration and coexistence" |
| PR-207 | addressed | L228: "changes only through EventStore AD-26" |
| PR-208 | by decision D1 | Superseded by the D1 skew wording; the "hosted sidecar … in CI" text is gone |
| PR-209 | addressed | L412 |
| PR-210 | addressed | L325: "Requested from EventStore: its First shared versions rows …" |
| PR-211 | addressed | L229: "**Deployment workflows** (AD-7) only verify and deploy"; writer acceptance moved to AD-2 L79 |
| PR-212 | addressed | L234: "Builds.Module.AppHost launches the AD-1 model"; the technical-module AppHost sentence is gone |
| PR-213 | addressed | AD-3 L87 keeps only "Persistent objects survive package removal." |
| PR-214 | addressed | AD-15 *Rehearsal* L214 |
| PR-215 | addressed | AD-9 *Default* L148 no longer restates the SDK mutation rule |
| PR-216 | addressed | L57 |
| PR-217 | declined-ok | AD-8 L136 keeps "secret-equivalent … never co-resident with module code", which is C-20 decision text |
| PR-218 | addressed | The AD-8 sentence is gone; L231 carries the validator and admission check |
| PR-219 | addressed | The seed moved to L463; the custodian sentence moved to AD-12 L183 |
| PR-220 | addressed | Both sentences are gone from L285 |
| PR-221 | addressed | L231 |
| PR-222 | addressed | L232 |
| PR-223 | addressed | AD-1 L67 (rewritten per RV-5) |
| PR-224 | addressed | Edits 2 and 3 are applied (L460, L476). Edit 1 was reasonably declined because it conflicts with RB-7's production-instance wording at L468. |
| PR-225 | addressed | L321, L326, L328 |
| PR-226 | addressed | L336, L350, L401 |
| PR-227 | addressed | AD-12 L181 is trimmed. The DR intro L309 was kept because it now carries the D5 "in-place recovery reuses steps 3–6" pointer. |
| PR-228 | declined-ok | Superseded by PR-204 and D1 |
| PR-229 | declined-ok (partly applied) | Edits a (L223) and f (L117) are applied. The rest was reasonably declined: b conflicts with the RR-6 activation rewrite, c trims C-35 wording, d conflicts with RB-24(b), and e conflicts with RV-7 and RB-18. |
| PR-230 | not addressed | Low. HotReload stays in AD-8 *Dapr*. Leaving it is acceptable, because all Configuration settings stay in one clause. |
| PR-231 | not addressed | Low. This discuss item is not in D1–D10, and the MFA sentence stays in AD-6. |
| PR-232 | not addressed | Low. This discuss item is not in D1–D10, and the row grew with RV-3 and RV-14 content. Record the choice in the memlog. |

## 2. Memlog decision check (batches 1–6, D1–D10)

No D-decision is contradicted. D1–D10 each landed as recorded:

- **D1:** L228.
- **D2:** AD-4 L97–101 (but see CF-7 and CF-8).
- **D3:** L227, L321 and L481.
- **D4:** L35, L128, L453, L475 and L481. There is no residual "single-writer" or "Administrator's devices" text. The notification repository uses "the operations repository's writer set" (L233).
- **D5:** L129, L223, L275, L277, L290, L287 and L295. Precondition 1 is scoped to "A production release attempt".
- **D6:** L117, L203 and L437.
- **D7:** L169, L327 and L458.
- **D8:** L232.
- **D9:** L137.
- **D10:** L285 and L311–316.

Batch decisions still hold, with three residues:

1. **Batch 5 C-18.** The C-18 clause "controller-configuration injection (snippet annotations) is rejected at admission" is no longer explicit. The new HTTPRoute field list implies the rule but does not state it (CF-16).
2. **Batch 6 C-20.** C-20 listed "recovery hooks" among sandboxed module code. The ADV-2 fix moved hooks into module workloads, so AD-7 L127 now names only "smoke suites and E2E". This is consistent, but the memlog should record that ADV-2 supersedes that part of C-20. Fence hooks were left with no execution locus (CF-5).
3. **Batch 1 C-02.** C-02 said "`hexalith.module-manifest.v1` becomes the Platform declaration schema implementation". The spine now ships "its next major" (L220). This follows the RB-13/RI-7 evidence that v1 is closed and lacks the fields, so the schema family is still ratified. Record it in the memlog as a refinement of C-02 (see also CF-17).

RB-16 (the operations-repository home for environment-layer definitions) and RV-2 (the solver-route listener) were raised as discuss items. They landed without D entries. Both restore or narrowly extend prior decisions (G-11 and C-23), so no contradiction arises.

## 3. Findings (internal contradictions and wording conflicts)

### CF-1: Precondition 6 and "deployable" cannot hold for any candidate before its own attempt

- **Severity:** medium
- **Location:** Production profile **Qualification** (L228); precondition 6 (L302); Rollout (L280)
- **Problem:** These rules cannot all hold at once:
  - L228 says "a production release attempt issues the release's production qualification record with its production-promoted record" and "A release is deployable in an environment … only while current versions fall within its effective qualified sets (release record plus that environment's qualification records) and, in production, its production-promoted record is valid for the active profile digest."
  - Precondition 6 requires "current environment-layer and shared versions fall within the release's effective qualified sets for production" before any mutation.
  - Rollout writes the production-promoted record only after the preconditions pass.

  So a candidate published before an environment-layer or shared change never passes precondition 6. Only its own attempt can issue its production qualification record, and it has only a staging record. Read literally, "deployable only while its production-promoted record is valid" also blocks every first production deployment. This leaves ADV-11 pair 1 open.
- **Replacement (L228 sentence):** "A deployed release, and a rollback target, stays valid in an environment only while current versions fall within its effective qualified sets (release record plus that environment's qualification records) and, in production, its production-promoted record is valid for the active profile digest; a candidate's production qualification and production-promoted records are issued by its production release attempt."
- **Replacement (precondition 6):** "6. Staging and production profile digests are equal; current environment-layer and shared versions fall within the release record's qualified sets, or within its staging qualification record at that equal digest; the production realm reports the required realm-contract version or a compatible later one; environment-layer inputs are applied and verified."

### CF-2: Environment-layer inputs are applied both "before the attempt" and "during the candidate's preparation"

- **Severity:** medium
- **Location:** The paragraph after Release tiers (L245); AD-3 *Tiers* (L87); precondition 6 (L302); Timing (L279); the environment-layer change path (L241)
- **Problem:** The rules place the same work at two different times:
  - L245 says declaration-derived environment-layer objects "are rendered from the union of the working baseline's and the candidate's release records during the candidate's preparation and applied as its forward-only input".
  - AD-3 says "Every environment-layer or shared input a release needs is applied forward-only and verified before the attempt".
  - Precondition 6 says "environment-layer inputs are applied and verified" before any mutation.
  - Timing makes "catalog preparation" the attempt's first mutation (t0).
  - L241 gives the environment layer "Its own attempt, staging first".

  One executor will apply Components inside the release attempt, after t0, under the application-attempt identity and timer. Another will run a separate environment-layer attempt first.
- **Replacement (L245):** "Environment-layer objects derived from declarations — Components with their names and scopes, HTTPEndpoints, MCPServers, Gateway listeners and bootstrap Secrets — are rendered from the union of the working baseline's and the candidate's release records and applied as the candidate's forward-only input by an environment-layer attempt that completes before the candidate's release attempt (precondition 6); DR and in-place recovery render them from the recovered release."

### CF-3: "A manual change is always an Administrator-approved attempt" blocks the D5 deputy recovery

- **Severity:** medium
- **Location:** Empty or degraded production (L276), compared with In-place recovery (L277) and Roles (L35)
- **Problem:**
  - L276 says "A manual change is always an Administrator-approved attempt under the lock and epoch".
  - L277 says "In production, Administrator or the deputy starts it on the production executor … It either re-deploys the recorded working baseline after a failed, unverified or interrupted recovery …".
  - D5 says "The deputy may act whether or not Administrator is available".

  An executor or reviewer can classify the deputy's manual re-deploy as a "manual change" and demand an Administrator record, which the deputy cannot produce (L223).
- **Replacement (L276 sentence):** "Any other manual change is an Administrator-approved attempt under the lock and epoch; an In-place recovery needs no Administrator record. Either becomes the working baseline only after passing verification."

### CF-4: The lock takeover rule does not admit the deputy's recovery entry

- **Severity:** medium
- **Location:** Attempt ownership (L278), compared with In-place recovery (L277), AD-7 *Operator-started recovery* (L129) and D5
- **Problem:**
  - L278 says "Only an Administrator record, or a replacement job of the same workflow resuming the same attempt within the grace …, may take it over, incrementing the epoch; recovery entry takes a new epoch. An executor dying during verification leaves the lock held and stops for intervention."
  - D5's case (a) is exactly that interrupted state. In it the deputy starts an in-place recovery "under a new epoch", but no clause lets a recovery entry take over a held lock. "Recovery entry takes a new epoch" says how the epoch changes, not who may take the lock.
- **Replacement (L278):** "Only an Administrator record, an In-place recovery or DR entry started by Administrator or the deputy, or a replacement job of the same workflow resuming the same attempt within the grace to perform its remaining single recovery, may take it over, incrementing the epoch."

### CF-5: Fence hooks have no execution locus

- **Severity:** medium
- **Location:** DR step 1 (L311); AD-7 *Module-code sandbox* (L127); Recovery hook contract (L441); step 4 (L314)
- **Problem:** Each possible locus for a fence hook breaks a rule:
  - Step 1 fences module-owned authorities "through a module fence hook holding custodian-released authority for that authority only". Step 1 runs before step 2 deploys anything, so no module workload exists to host the hook.
  - Step 4 and the hook contract have hooks run "inside the owning module's restored workload … on a declared internal endpoint" (the contract lists "fence hooks" and "internal recovery endpoint"). That workload does not exist at step 1.
  - The only other locus is the recovery executor. There AD-7 says module-supplied code "receives only short-lived synthetic-client tokens and the declared verification endpoint", which forbids the custodian-released authority the hook needs.
- **Replacement (AD-7 L127 first sentence):** "Module-supplied code that runs on an executor — smoke suites, E2E and fence hooks — runs in an isolated sandbox, a separate OS user or container, with no access to the job's credential material, workspace, job token or container runtime; it receives only short-lived synthetic-client tokens and the declared verification endpoint, except that a fence hook receives the custodian-released authority for its one surviving authority, destroyed at job end."
- **Replacement (step 1 clause):** "module-owned authorities through a module fence hook run in the recovery executor's sandbox with custodian-released authority for that authority only, or a module-owned manual procedure".

### CF-6: The recovery executor must write records but holds no credential to do so

- **Severity:** medium
- **Location:** AD-7 (L129), compared with Binding classes and records (L223) and DR step 1 (L311)
- **Problem:** The recovery executor has records to write but no credential to write them:
  - AD-7 says "The recovery executor holds standing credentials only for the prepared capacity plus read-only access to recovery points; custodians release key, decryption and synthetic-client material per job".
  - L223 makes it the writer of DR attempt records, "kept with the lock and epoch in one named CAS-capable store outside the target cluster". It also moves the latest-working pointer ("the writer of a working terminal attempt").
  - Step 1 has it "Set the promotion stop".
  - L468 makes the store "reachable from the … recovery executor", but no credential authorizes the writes or the record signing.
- **Replacement (AD-7 L129 sentence):** "The recovery executor holds standing credentials only for the prepared capacity, its record-signing identity and write access to the attempt, lock and stop store, plus read-only access to recovery points and the off-site registry replica; custodians release key, decryption and synthetic-client material per job, destroyed at job end, and its credentials rotate after every drill and DR."

### CF-7: The fix pass dropped the source-mode rule for non-root-declared dependencies

- **Severity:** medium
- **Location:** AD-4 *Source mode* (L98) and *Mode selection* (L100)
- **Problem:**
  - HEAD and the reviewed draft read "Root-declared Hexalith dependencies build from source in Debug; all others resolve as packages at the Builds catalog version". The D2 rewrite moved package resolution into *Package mode* and left source mode as "Root-declared Hexalith dependencies build from source in Debug."
  - L100 then says "no … package fallback". A domain workspace in source mode now has no rule for dependencies it does not root-declare, such as Commons, PolymorphicSerializations, or a dependency server it does not debug. A literal reader will treat package resolution as a forbidden fallback.
- **Replacement (L98 first sentence):** "Root-declared Hexalith dependencies build from source in Debug; every other dependency resolves through the mapping as a NuGet package or released image at the Builds catalog version, which is never a fallback for a root-declared one."

### CF-8: Package mode names a Platform submodule that the Platform repository lacks

- **Severity:** low
- **Location:** AD-4 *Package mode* (L99), compared with *Platform identity* (L101)
- **Problem:** L99 says "The tool builds the Platform composition from the Platform submodule at a Platform release tag", but L101 says "in the Platform repository the identity is its HEAD". The Platform repository's own CI, which counts as CI under Terms, runs package mode, and it has no Platform submodule and no release tag on pull-request commits.
- **Replacement (L99 first sentence):** "Outside the Platform repository the tool builds the Platform composition from the Platform submodule at a Platform release tag; in the Platform repository it builds from HEAD."

### CF-9: Staging reset uses in-place recovery outside its two defined cases, and its trigger order is circular

- **Severity:** low
- **Location:** Staging reset (L274), compared with In-place recovery (L277)
- **Problem:**
  - L274 says "staging runs an in-place recovery from the recovery point cut at the start of that candidate's attempt, as its own staging attempt before the next gate". In-place recovery allows only two cases: "It either re-deploys the recorded working baseline … or runs an approved release's named recovery: DR steps 3–6 … from the recovery point cut after the lock". A staging reset is neither case, and its recovery point was not cut after its own lock.
  - The trigger is circular. Non-adoption is decided "once a later candidate's staging attempt starts", but the reset must run "before the next gate".
- **Replacement (L277 second sentence):** "It re-deploys the recorded working baseline after a failed, unverified or interrupted recovery, or runs DR steps 3–6 against the live environment, without fence, Keycloak restore or cutover, from a named recovery point: for an approved release's named recovery, the point cut after that release attempt's lock; for a staging reset, the point cut at the start of the unadopted candidate's attempt."
- **Replacement (L274 second sentence):** "When a later candidate's staging attempt is requested and an unadopted candidate's writes cannot be read by production's working baseline, staging first runs an in-place recovery as its own staging attempt, and the later candidate's attempt starts after it; the staging realm regenerates from the realm contract."

### CF-10: The tenant-key store is both a surviving authority and a restored store, and step 3 needs the mirror before re-issue

- **Severity:** medium
- **Location:** DR steps 1, 3 and 5 (L311, L313, L315)
- **Problem:**
  - Step 1 fences credentials at "… record store and tenant-key store", so the store is treated as surviving the failure.
  - Step 3 says "Restore the tenant-key store", so the same store is also treated as lost and restored. Owned work (L463) makes the tenant-key store per-environment.
  - Step 3 re-applies "tombstone key destruction". That needs the post-cut tombstones held by the surviving tombstone mirror. The replacement instance's credentials at the mirror are issued only in step 5 ("Each surviving authority's owner issues the replacement instance's credentials there"). No step says who reads the mirror at step 3.
- **Replacement (step 1 list):** "off-site backup store, tombstone mirror, external providers, Keycloak event-export sink, registry, record store and tenant-key backup custody".
- **Replacement (step 3):** "3. **Keys.** Restore the tenant-key store and re-apply tombstone key destruction from the surviving tombstone mirror, read through its fence-and-reissue owner for this run."

### CF-11: Production synthetic credentials are "held only by the production executor"

- **Severity:** low
- **Location:** Synthetic identities (L232), compared with AD-7 (L129) and step 5 (L315)
- **Problem:** L232 says "held only by the production executor and rotated through an Administrator attempt or a DR run", but L129 and step 5 give the recovery executor synthetic-client material "for that job only".
- **Replacement:** "Administrator provisions production synthetic credentials, held only by the production executor, and by the recovery executor for one DR job, and rotated through an Administrator attempt or a DR run."

### CF-12: The composed image's release-available record is written but never checked

- **Severity:** low
- **Location:** Binding classes (L223) and AD-13 (L192), compared with precondition 5 (L301)
- **Problem:** L223 adds "the composed image's *release-available record* by the publication workflow after staging validation", but precondition 5 checks only "The EventStore server package is release-available."
- **Replacement:** "5. The EventStore server package and the composed `platform/eventstore` image are release-available."

### CF-13: "Degraded" means two different things

- **Severity:** low
- **Location:** Empty or degraded production (L276) and Release modes (L275), compared with After DR (L291)
- **Problem:** In L276 degraded production means that "its last outcome was non-working", and that state lifts the stop through an Administrator record. After DR (L291) says "Production then runs in a recorded degraded posture until new prepared capacity is identified", even though its last outcome is working. An agent may apply the Empty-or-degraded stop lift after DR.
- **Replacement (L291):** "Production then runs in a recorded reduced-recovery posture, which is not Empty or degraded production, until new prepared capacity is identified …".

### CF-14: "Promotion stopped" is not the promotion stop

- **Severity:** low
- **Location:** Production profile (L228), compared with Promotion stop (L283)
- **Problem:** L228 says "in production also renews the production-promoted record at the new digest, with promotion stopped until both exist". Environment-layer and shared changes are not on the L283 set list, and only an Administrator record clears a set stop. The sentence therefore either sets an unlisted stop that nothing clears automatically, or it means only a precondition block.
- **Replacement:** "…, and in production also renews the production-promoted record at the new digest; no release attempt passes precondition 6 until both exist."

### CF-15: Staging "has no write access" to Keycloak but applies its realm contract

- **Severity:** low
- **Location:** Keycloak note (L247), compared with AD-6 L114 and L117
- **Problem:** L247 says "staging has no write, backup or restore access to its server or database", but AD-6 applies the staging realm contract "in staging by the staging management client".
- **Replacement (L247 clause):** "staging has no backup or restore access to its server or database and no write access beyond the staging realm through the staging management client".

### CF-16: The route field list no longer states C-18's injection ban

- **Severity:** low
- **Location:** Hosted interfaces **Routes** (L231); memlog batch 5, C-18
- **Problem:** C-18 decided that "controller-configuration injection (snippet annotations) is rejected at admission". L231 now says "application namespaces hold only HTTPRoutes setting hostnames, a parent reference …, path matches and same-namespace backends", which does not exclude HTTPRoute `filters` or `ExtensionRef`.
- **Replacement:** "application namespaces hold only HTTPRoutes that set nothing but hostnames, a parent reference to their environment's Gateway, path matches and same-namespace backends, with no filters or extension references;"

### CF-17: "Current and previous major" would accept v1, which lacks the fields

- **Severity:** low
- **Location:** Module declaration (L220)
- **Problem:** L220 says "The current `hexalith.module-manifest.v1` … lacks most of these fields, so the Platform declaration ships as its next major. … Platform accepts the current and previous major." Once the next major ships, v1 becomes the "previous major", yet it cannot carry readiness, smoke or critical-flow declarations (precondition 7).
- **Replacement (last clause):** "Platform accepts the current and previous Platform declaration major, starting with the first major that carries these fields; v1 is never accepted for enrollment."

### CF-18: The capability map cites section names that do not exist

- **Severity:** low
- **Location:** Capability map, FR-9 row (L413)
- **Problem:** The row cites "Backup coverage, Recovery point". The actual rows are "Backup coverage and cadence" (L285) and "Recovery point and freshness" (L286).
- **Replacement:** "Disaster recovery sequence, Backup coverage and cadence, Recovery point and freshness, Data protection".

### CF-19: Shared-infrastructure re-verification would run staging code on the production executor

- **Severity:** low
- **Location:** Production profile (L228) and Release tiers shared row (L242), compared with AD-7 (L126–127)
- **Problem:**
  - Shared changes run as one "Named shared-infrastructure workflow under both locks" that "re-runs the working release's smokes". L228 has the attempt "in each environment" re-verify that environment's working release.
  - Only "the production executor, for named shared-infrastructure workflows," gets the cluster-scoped identity.
  - AD-7 also says "Staging, test or PR code never runs on an executor that holds or held production credentials".

  The rules never say which executor runs staging's re-verification.
- **Replacement (L242 change-path clause):** "… re-runs each environment's working-release smokes on that environment's executor, and the NFR-3 negative tests".

### CF-20: DR step 7 has a garbled clause

- **Severity:** low
- **Location:** DR step 7 (L317)
- **Problem:** "re-provision its allowlist for the replacement capacity in the DR attempt record" can be read as the allowlist living inside the record.
- **Replacement:** "hand fresh synthetic and deployment credentials to the production executor, re-provision its allowlist for the replacement capacity and record both in the DR attempt record, and report the lost window."

## Not raised

- **AD tags:** All 15 ADs carry [ADOPTED, AMENDED], which is consistent with RR-20 and the distillation event.
- **Precondition numbering:** L276 "precondition 8" and D5's "Precondition 1" match the list at L297–305. The DR cross-references ("steps 3–6", "in step 4") match the numbered sequence.
- **Record writers and the store:** Every record kind at L223 names a writer, and the store row (L444) lists all executors and the monitor. The one exception is the recovery executor's missing credential (CF-6).
- **D4 wording:** No residual "writable only by Administrator", "single-writer" or "Administrator's devices" text remains. Workflows L229 keeps "Administrator-only bypass", which is consistent with D4, because the accepted risk (L481) records that owners and admins can edit rulesets.
