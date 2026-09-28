# Review — Adversarial divergence attack (update run 2, 2026-09-28)

**Verdict: FAIL.** No critical hole, but nine high-severity pairs remain in the rules this update added or changed. In each, two units one level down obey the spine to the letter and still cannot interoperate. The affected areas are the hosted Gateway and staging HTTP-01 certificates, the whole deputy-run DR path (hooks, synthetic verification, surviving-authority fences, lost-window review), the empty or degraded production path, asynchronous admission re-checks, service-account actors, and catalog retention across failed attempts. Most holes close with one tightened sentence in an existing AD or convention row, and no new AD number is needed. Eight findings touch user-settled decisions (C-04, C-09, C-16, C-25, C-31, C-38) or need a new owner contract, so they are marked **discuss**. None asks to reverse a settled decision.

**Counts:** critical 0 · high 9 · medium 10 · low 2 (21 findings). Dispositions: autofix 13 · discuss 8 · defer 0 · ignore 0.

**Scope.** I read the full working-tree spine (467 lines, `updated: 2026-09-28`, status draft), the memlog from "(event) Update run 2 started 2026-09-28" to the end, the r2 `findings.json` clusters C-01 to C-64, and the r2 adversarial report. Cross-checks:

- **Builds:** the README catalog-import and G-4 tool sections.
- **McpCli spine:** AD-4 (generated assembly manifest) and AD-17 (release and versioning).
- **EventStore architecture:** AD-25 deployment-catalog retirement and AD-29.
- **Platform PRD and addendum:** the deputy and lost-window text.
- **cert-manager:** the HTTP-01 Gateway API solver page.
- **Dapr:** the subscription-methods page.

This is a consistency review. It proves nothing about runtime or deployment, and I changed no file other than this one.

## Prior holes re-checked (r2 adversarial)

| r2 ID | Status | Evidence / residual |
| --- | --- | --- |
| ADV-1 Components and bootstrap Secrets had no writable namespace | Closed | AD-8 now reads: "The environment-layer identity writes the data namespace plus `components.dapr.io` objects and the named bootstrap Secrets in its application namespace." New residual: the Gateway and its certificates have no writable namespace (ADV-1 below). |
| ADV-2 Forward-only inputs had no expand/contract rule | Partial | The AD-15 expand-only list exists. It omits the surface map, exchange permissions, groups, Gateway listeners and HTTPEndpoints (ADV-16). |
| ADV-3 Immutable qualified sets | Closed for the working release | Qualification records exist. Candidates and recovery-point releases are not covered (ADV-11). |
| ADV-4 No path to production without a healthy baseline | Partial | Precondition 8 is replaceable. The promotion-stop clear rule still deadlocks the path (ADV-6). |
| ADV-5 Surface laundering | Closed for gateway-admitted chains | Chains that do not start at EventStore admission, and service-account actors, are open (ADV-8). |
| ADV-6 Eligibility carrier | Closed | Catalogs row. |
| ADV-7 Dynamic credentials had two writers | Closed on paper | Hooks have no executable locus (ADV-2). |
| ADV-8 Legacy hosts in compositions | Closed | AD-11 *Legacy surfaces*. The wording "outside hosted environments" has a residual (ADV-20). |
| ADV-9 Rollback generation content | Closed for one attempt | Retention is not carried across attempts (ADV-9). |
| ADV-10 Post-DR executor and records | Closed | Recovery executor is a named writer; step 7 re-provisions. |
| ADV-11 Promotion-stop classification | Closed | Records model. Writer acceptance for pointer and stop records is open (ADV-12). |
| ADV-12 Staging DNS zone | Closed by the C-18 option | A ClusterIssuer bypass remains (ADV-1). |
| ADV-13 Synthetic-flag shape | Closed | Identifier in the descriptor and realm-contract instance, attested on events. |

## New findings

### ADV-1 — The Gateway and its certificates have no lawful namespace, staging HTTP-01 cannot complete, and a shared ClusterIssuer bypasses "no DNS-01 in staging"

- **Severity:** high · **ADs:** AD-8, AD-3/Release tiers, Hosted interfaces, AD-15
- **Unit A — staging environment-layer Gateway story.** It obeys three rules:
  - AD-8: "The environment-layer identity writes the data namespace plus `components.dapr.io` objects and the named bootstrap Secrets in its application namespace. The application deploy identity holds none of these."
  - Release tiers: the environment layer holds "the environment's Gateway".
  - Hosted interfaces: "Staging holds no DNS-01 credential and obtains certificates only through HTTP-01."

  The data namespace is the only place where the environment-layer identity may create a Gateway and its listener Certificates, so Unit A puts them there.
- **Unit B — hosted admission story.** It obeys Hosted interfaces: the Gateway "admits routes only from its application namespace. Application routes may set only host, path, TLS and backend fields".
- **Incompatibility:**
  1. cert-manager's `gatewayHTTPRoute` solver creates its temporary HTTPRoute in the Certificate's namespace. Its documented example opens the port-80 listener with `allowedRoutes.namespaces.from: All`. Unit B's Gateway rejects the data-namespace solver route, so no staging certificate is ever issued. The other placement also fails. Certificates in the application namespace, referenced through a ReferenceGrant, need Certificate and ReferenceGrant writes there. Neither identity holds those writes, and no tier lists either kind. Every placement breaks one rule, and the first staging deployment is blocked.
  2. An HTTPRoute has no TLS field and must set `parentRefs`. The field whitelist, taken literally, rejects every application route and every solver route.
  3. **Unit C — shared certificate-issuer story.** The tier table puts the "certificate issuer" in shared infrastructure. Unit C ships a DNS-01 ClusterIssuer for shared and production names. Its credential lives in the cert-manager namespace, so "staging holds no DNS-01 credential" stays literally true. Hosted interfaces places the reserved-name check on routes and listeners. A staging Certificate for `registry.hexalith.com` that references the ClusterIssuer therefore passes admission and is issued through DNS-01. That is the exact threat C-18 closed.
- **Minimal rule (Hosted interfaces and AD-8):**

  > "Each environment's Gateway and its listener Certificates live in that environment's data namespace and are written only by its environment-layer identity. Its port-80 listener also admits ACME solver routes from that namespace. Application routes may set only hostnames, `parentRefs` to their environment's Gateway, path matches and same-namespace `backendRefs`. Staging Certificates and CertificateRequests reference only the staging HTTP-01 Issuer. Admission rejects any staging Certificate, listener or route that names a reserved or out-of-pattern host or references a ClusterIssuer."
- **Disposition:** autofix. This tightens C-18 and C-23 without reversing them.

### ADV-2 — Recovery hooks have no executable locus: the sandbox, quarantine and hook duties exclude each other

- **Severity:** high · **ADs:** AD-7, AD-11 *Scope*, DR steps 2, 4 and 5, Secrets, Startup task lifecycle
- **Unit A — Memories recovery-hook story.** Secrets says Memories' dynamic namespace has "only writer ... the owning module". DR step 5 says it rotates "only through the owning module's hook". Startup task lifecycle defines a "recovery (run in declared-dependency order)" scope. Unit A therefore implements purge and per-tenant principal re-provisioning as a recovery-scope task inside the restored `Memories.Server`, using its own OpenBao and Redis/FalkorDB principals. Step 2 says "Recovery hooks may assume exactly this state", meaning the deployed release.
- **Unit B — Platform DR executor story.** It applies DR step 4 literally: "Restore data into quarantine, reachable only from the recovery executor." It also applies the AD-7 sandbox rule literally: hooks run in a sandbox that "receives only short-lived synthetic-client tokens and the declared verification endpoint".
- **Incompatibility:** Unit A's in-workload hook cannot reach its quarantined stores, and rebuild-only replay by the restored workloads cannot either. A hook run by Unit B's executor can reach the data but holds only synthetic tokens with "no admin or cross-tenant grant". It cannot create per-tenant ACL principals, write the dynamic namespace or purge real tenants. Whichever locus Platform picks, one module's hook fails, and the G2 drill cannot pass. A hook shipped as an executable with purge verbs is also a module-administration CLI. AD-11 exempts only tooling "that expose[s] no module commands or queries".
- **Minimal rule (DR step 4 and AD-7):**

  > "Recovery hooks are recovery-scope tasks that run inside the owning module's restored workload under its own least-privilege principals. Quarantine admits the recovery executor and the restored release's workloads; user ingress stays closed and workers stay disabled. The recovery executor invokes each hook and awaits its result through the recovery hook contract, on a declared internal endpoint reachable only from the recovery executor. That endpoint is a recovery surface admitted here, not a module CLI or MCP surface. The sandbox rule governs module-supplied code that runs on an executor."
- **Disposition:** autofix.

### ADV-3 — DR verification has no synthetic credentials, and a deputy-run DR cannot rotate them

- **Severity:** high · **ADs:** Synthetic identities, AD-7 *Recovery executor*, DR steps 5–7
- **Unit A — recovery-executor verification story.** DR step 6 requires "restored-release smokes, SM-4 access outcomes and denial of revoked principals". AD-7 requires sandboxed smokes that use synthetic-client tokens. However, the recovery executor "holds standing credentials only for the prepared capacity plus read-only access to recovery points", and custodians release only "key and decryption material".
- **Unit B — production synthetic-credential story.** Synthetic identities says: "Administrator provisions production synthetic credentials, held only by the production executor and rotated through an Administrator attempt."
- **Incompatibility:** The recovery executor cannot mint synthetic tokens, so step 6 cannot run. The production executor is re-provisioned for the replacement capacity only at step 7, after verification. Step 5 must also "Rotate every restored credential", including the synthetic client secrets restored with the Keycloak database. That rotation needs an "Administrator attempt", which a deputy-run DR cannot perform. So neither an Administrator run nor a deputy run can verify and reopen.
- **Minimal rule (Synthetic identities and DR step 5):**

  > "In DR, step 5 rotates the restored synthetic clients' credentials under the recovery run's DR-scoped realm rights. Custodians release them to the recovery executor for that job only, and they are destroyed at job end. Step 7 hands fresh credentials to the production executor. Synthetic-credential rotation is an Administrator attempt or a DR run."
- **Disposition:** autofix.

### ADV-4 — Deputy-run DR stops at an Administrator-only lost-window review

- **Severity:** high · **ADs:** Roles, DR step 6, Lost window and external effects, Detection and response
- **Unit A — deputy recovery runbook.** Roles: the deputy "may execute the documented recovery, verify restoration and reopen service". The PRD glossary adds "when Administrator is unavailable".
- **Unit B — DR reopen gate.** DR step 6: "Administrator reviews the lost-window exceptions". Lost window: "reviewed by Administrator before reopening".
- **Incompatibility:** The deputy is needed precisely when Administrator is unavailable, and in that case reopening waits for Administrator. The four-hour RTO clock keeps running through the wait, so deputy response coverage cannot be counted toward G2. Both rules come from user-settled decisions: C-09 (deputy reopens) and C-25 (Administrator review). The addendum's lost-window text asks only that "The recovery report states that window."
- **Minimal rule (recommended option):**

  > "The recovery runner (Administrator or the deputy) records the lost-window exceptions in the DR report before reopening. Administrator reviews them before clearing the promotion stop."

  Promotion resumption stays Administrator-only, as in the PRD.
- **Disposition:** discuss. It reconciles two settled decisions.

### ADV-5 — Fencing and re-issuing credentials at surviving module-owned authorities has no owner and runs in the wrong order

- **Severity:** high · **ADs:** DR steps 1 and 5, Secrets, AD-7 *Recovery executor*, Memories erasure continuity, Recovery point
- **Unit A — Platform DR fence story.** Step 1 says: "Revoke the old environment instance's credentials at every authority that survives the failure — ... tombstone mirror, external providers, Keycloak event-export sink, registry, record store and tenant-key store". Step 1 runs before any module workload exists on the replacement capacity, so the recovery executor must revoke at the mirror and at providers itself. That makes it a second writer of Memories' and Folders' credentials, and AD-7 grants it no authority there.
- **Unit B — Memories continuity story.** Memories is the sole writer of its credentials and acts through its hook (step 4). The mirror is "synchronous complete", and "No acknowledged tombstone may be lost."
- **Incompatibility:**
  - **Fence ordering.** Step 1 either needs module authority that Platform does not hold, or it waits for a hook that runs three steps later.
  - **No re-issue step.** After step 5, the restored OpenBao holds only the revoked mirror, provider, sink and registry credentials. No step issues the replacement instance new credentials at the surviving authorities. Step 5 rotates "restored" credentials, but nobody named holds authority there.
  - **Effects.** Memories' synchronous mirror writes fail, so erasure acknowledgements fail closed. Keycloak event export stops, so no post-DR recovery point is ever complete, because a complete point requires "event-export lag within its bound".
- **Minimal rule (recovery inventory and DR steps 1 and 5):**

  > "The recovery inventory names, for every surviving authority, its fence-and-reissue owner and procedure. Module-owned authorities (tombstone mirror, external providers) use a module-supplied fence hook run in the recovery sandbox with custodian-released authority for that authority only, or a module-owned manual procedure. Platform-owned authorities are handled by Administrator or the deputy. Step 5 issues the replacement instance's credentials through the same owner, and step 6 proves that new credentials work and old ones fail."
- **Disposition:** discuss. It extends C-25 with owners.

### ADV-6 — The promotion stop deadlocks the empty and degraded production path

- **Severity:** high · **ADs:** Production preconditions 1 and 8, Promotion stop, Empty or degraded production, Release modes, Automatic recovery
- **Unit A — precondition checker.** Precondition 1: "the promotion stop is clear". Promotion stop: "Set by every non-working terminal outcome ... Only an Administrator record naming the reason and a verified current working release clears it."
- **Unit B — degraded-path story.** Empty or degraded production: "an Administrator record may replace precondition 8". Release modes: the approved record "replaces only those two triggers".
- **Incompatibility:** "Last outcome non-working" always sets the stop, and a degraded production has no verified working release to name. The stop therefore can never be cleared, and the degraded path can never start. After "A failed first deployment keeps ingress closed and stops", production is blocked permanently. r2 C-11 named this deadlock, but the applied fix replaced only precondition 8.
- **Minimal rule (Empty or degraded production):**

  > "When production has no working baseline, or its last outcome was non-working, the Administrator record for that attempt names the reason and the attempt and lifts the promotion stop for that attempt only; its terminal outcome re-sets the stop unless working."
- **Disposition:** autofix. It completes the accepted C-11 fix.

### ADV-7 — "Current admission" for asynchronous steps has no lawful data source and two predicates

- **Severity:** high · **ADs:** AD-14 *Chains*, AD-6 *Admission* and *Administration*, Synthetic identities, AD-8 negative tests
- **Unit A — Projects task-engine story.** AD-14 requires asynchronous steps to "re-check that actor's current admission and permissions, failing closed after revocation". Unit A re-checks group membership through the Keycloak admin API, using its service account with `view-users`. That account is not agent-capable, so the agent-token clause does not bar it.
- **Unit B — Keycloak exposure story.** It obeys AD-6: "Identity-administration authority belongs to principals distinct from every application principal ... The admin API and master realm are reachable only from a declared Administrator path."
- **Incompatibility:** Unit A's calls are blocked, so every asynchronous step fails closed. The other modules then diverge:
  - Folders keeps the user's refresh token and treats a refreshed token's groups claim as "current", with a different staleness.
  - A third module applies AD-6's first sentence, "membership in the human production-admission group". Synthetic smokes that include asynchronous steps then fail as "revoked", because synthetic actors are admitted by the synthetic-admission group limited to the synthetic tenant.

  A revoked user's asynchronous work stops in one module and continues in another. That breaks NFR-3 "denial after revocation".
- **Minimal rule (AD-14 and First shared versions, EventStore row):**

  > "EventStore owns one admission predicate: the human production-admission group, or the synthetic-admission group for the synthetic tenant only. It also owns one admission projection, fed from the realm's admin-event stream, with a declared maximum staleness. The gateway and every asynchronous re-check read that projection. No application principal calls the Keycloak admin API. Unknown or stale admission fails closed."
- **Disposition:** discuss. It needs a new EventStore contract.

### ADV-8 — Server-to-server calls may run as a service-account actor, and chains that start outside EventStore admission have no attester

- **Severity:** high · **ADs:** AD-14 *Actor and workload*, *Chains* and the interim default; Design Paradigm (module-owned transports)
- **Pair 1 (service-account actor):**
  - **Unit A — Projects background reconciler.** It is not a "task step". It calls Folders' agent-eligible `folder.create` with its own client-credentials token. AD-14 says "The actor is the token subject", and "Until EventStore ships that attestation, service clients may call only agent-eligible operations." Unit A complies with both.
  - **Unit B — Tenants membership story.** It grants that service account membership in every tenant so the reconciler works. Only *synthetic* clients are barred from cross-tenant grants.
  - **Result:** a cross-tenant actor with no user behind it, which user revocation never reaches. AD-14 "Prevents" exactly this: "cross-module calls losing or forging the original actor".
- **Pair 2 (entry outside EventStore):**
  - **Unit C — Tenants BFF story.** It is a module-owned read transport that "remain[s] valid". It calls Parties' UI-only person read with a token exchanged by its UI confidential client.
  - **Unit D — Parties authorization story.** Once the attestation ships, Parties requires EventStore's originating-surface attestation ("attested by EventStore").
  - **Result:** BFF-originated requests never pass EventStore admission. Either they are denied, or Parties also accepts the exchanged `azp`, and two surface derivations coexist.
- **Minimal rule (AD-14 *Actor and workload* and *Chains*):**

  > "Every server-to-server step carries a user actor: by token exchange in synchronous steps, or as the EventStore-attested original actor in asynchronous steps. A module service client's own subject is never the actor of another module's operation and holds no tenant membership. A chain that starts outside EventStore admission has the service class as its effective surface, so UI-only and confirmation-required downstream steps must originate through the gateway."
- **Disposition:** autofix. It tightens C-06 in its own fail-safe direction.

### ADV-9 — Deterministic catalog generation drops live retention entries after a failed attempt

- **Severity:** high · **ADs:** Catalogs, AD-15 *Rollback generation* and *Expand-only*, Binding classes and records (working baseline), Automatic recovery; EventStore AD-25
- **Setup.** Candidate C1 adds idempotent command X, writes X records and fails verification. Recovery commits the rollback generation G_rb1, which holds the baseline routes plus X as a retention entry. The latest-working pointer stays on the pre-C1 attempt, because only "Record working; move latest-working pointer" moves it.
- **Unit A — catalog generator story.** "One deterministic Platform generator produces each composition's catalog from enrolled declarations and Contracts." Candidate C2 no longer declares X (C1's module release was withdrawn), so C2's generation has no X entry.
- **Unit B — rollback-preparation story.** It takes "the baseline ... generation" from "the working baseline", defined as "the release record plus the attempt record the latest-working pointer names". That is the pre-C1 generation, so C2's rollback generation also lacks X.
- **Incompatibility:** Committing either generation retires X while X records remain. EventStore AD-25 says "Retirement is refused while records ... remain" and "Readiness fails on missing entries". C2 fails, its one recovery fails too, and the attempt stops for intervention. Later candidates are blocked until someone hand-edits a catalog, which would be the forbidden second catalog. The same generator also retires baseline entries whenever a candidate drops an operation, which contradicts AD-15 expand-only.
- **Minimal rule (Catalogs and AD-15):**

  > "Every prepared generation, candidate or rollback, is generated from the enrolled declarations plus the idempotency and key entries of the currently committed generation. Those entries persist as non-executable retention entries until a later contraction attempt retires them under EventStore's retirement rule. 'Baseline generation' means the environment's currently committed generation."
- **Disposition:** autofix. EventStore confirmation is already a G3 row.

### ADV-10 — An incompatible release's named recovery has no executor that can run it

- **Severity:** medium · **ADs:** Release modes, DR sequence, AD-7 *Executors* and *Recovery executor*, Binding classes and records, After DR
- **Unit A — incompatible-release story.** Release modes: "the named recovery runs as a DR entry under a new epoch by Administrator or the deputy". The installation is healthy, so Unit A plans an in-place restore through the production executor, the only executor with live credentials. However, attempt records for DR are accepted only from "the recovery executor for DR".
- **Unit B — DR story.** "Run by the recovery executor", which "holds standing credentials only for the prepared capacity". Unit B executes the named recovery as the full sequence onto prepared capacity. That fences a healthy instance, restores Keycloak, cuts over DNS, consumes the prepared capacity and forces the post-DR degraded posture and a G2 re-drill, all for an ordinary failed release.
- **Minimal rule (Release modes):**

  > "An incompatible release's named recovery is an in-place DR entry. The production executor runs an allowlisted recovery workflow started by Administrator or the deputy under a new epoch. It restores the recovery point cut after the lock into the live data namespace and runs DR steps 3–6 without fence, Keycloak restore or cutover. It writes the DR attempt record as an accepted writer for in-place DR. Replacement-capacity DR stays with the recovery executor."
- **Disposition:** discuss. It concerns how C-38 is executed.

### ADV-11 — Qualification records are issued only for the working release, so staged candidates and recovery-point releases go stale

- **Severity:** medium · **ADs:** Production profile, Binding classes and records (*Qualification records*), Production precondition 6, DR step 2
- **Pair 1 (staged candidate):**
  - **Unit A — environment-layer attempt workflow.** Per Production profile, "the production attempt re-verifies the working release and issues a qualification record". Unit A issues records only for the working release.
  - **Unit B — promotion precondition checker.** Candidate R2 was published at digest D1 and re-staged at D2. Precondition 6 requires current versions "within the release's effective qualified sets (release record plus qualification records)". No record ever extends R2, so R2 must be republished as a new package and re-staged. The monthly patch cadence ("current on security patches") invalidates every in-flight candidate in the same way.
- **Pair 2 (recovery-point release):**
  - **Unit C — DR story.** It reproduces the current inventory D3. The recovery point's release R2 predates R3, which was renewed at D3; R2 was not.
  - **Result:** "deployable ... only while ... its production-promoted record is valid for the active profile digest" bars R2 at D3. The only alternative is reproducing an older, unpatched inventory, and the spine does not say which to do.
- **Minimal rule (Production profile):**

  > "A staging attempt that passes a release at the current staging profile digest issues that release a staging qualification record. When precondition 6 is met by staging evidence at the current production digest, the release's production attempt issues its production qualification record together with the production-promoted record. DR reproduces the environment layer at the digest recorded with the recovery point, or at a later digest within the recovery-point release's effective sets, and issues the DR attempt's production-promoted record at the reproduced digest."
- **Disposition:** discuss. It extends C-16's issuance.

### ADV-12 — Pointer and stop records have no accepted writers, so the monitor's store credential can repoint the working baseline

- **Severity:** medium · **ADs:** Binding classes and records, Promotion stop, Workflows and provenance
- **Unit A — monitor story.** Promotion stop says the monitor "may set it but never clear it". Records says the stop and pointer "change only through their own set and clear records", kept "in one named CAS-capable store". Unit A therefore receives a store write credential.
- **Unit B — executor precondition and rollback story.** Deploy accepts attempt records "only from the environment's executor identity, or the recovery executor for DR". No writer is named for pointer records, and "storage write access never authenticates a record" applies only to Administrator records. Unit B therefore verifies only the attempt record that a pointer names.
- **Incompatibility:** A pointer-set record written with the monitor's credential, naming an older working attempt with valid executor provenance, passes. The staging gate then rehearses an older baseline, and the rollback set renders an older release. That is the cycling through older releases that the spine forbids.
- **Minimal rule (Binding classes and records):**

  > "Each record kind has named writers, verified by signature, not by store access. The latest-working pointer moves only by the environment's executor for its own working attempt, or by the recovery executor for a verified DR attempt, and names a working terminal attempt of that environment with a higher epoch. Stop-set records are accepted from executors, the monitor, Administrator or the deputy. Clear records are Administrator records."
- **Disposition:** autofix.

### ADV-13 — The staging reset has no procedure and no trigger point

- **Severity:** medium · **ADs:** Staging gate, Memories erasure continuity, Startup task lifecycle, DR steps 3–4, Release modes
- **Pair 1 (procedure):**
  - **Unit A — staging reset story.** It performs a bare data restore of "the recovery point cut at the start of that candidate's attempt".
  - **Unit B — Memories continuity story.** Staging's tombstone mirror holds tombstones acknowledged after the cut, and "no erased key or tenant resurrected; unknown lineage fails closed" applies. Memories therefore fails closed in staging. Rebuild-only projections and the broker backlog are not restored either. The next gate fails for reasons unrelated to its candidate.
- **Pair 2 (trigger):**
  - **Unit C — staging scheduler.** It resets as soon as the next candidate queues, because the breaking candidate is "not adopted" yet.
  - **Unit D — Administrator.** Administrator plans an approved incompatible attempt for that breaking candidate. Its exact-release staging evidence then describes a staging state that no longer exists.
  - **Result:** If the scheduler instead waits, the next candidate's rehearsal runs on data the baseline cannot read and is falsely classified as breaking. "Does not adopt" has no decision point.
- **Minimal rule (Staging gate):**

  > "A candidate is not adopted once a later candidate's staging attempt starts without an Administrator record reserving it. A staging reset runs DR steps 3–6 in place against staging — tenant-key restore with tombstone re-application, quarantine restore, module recovery hooks, broker re-provisioning, credential rotation and verification — without fence, Keycloak restore or cutover. The staging realm regenerates from the realm contract."
- **Disposition:** autofix. It fills in the C-37 method.

### ADV-14 — Some Dapr resource kinds have no tier or writer, and environment-layer Components have two version authorities

- **Severity:** medium · **ADs:** AD-8, AD-3/Release tiers, AD-1 *Dapr resources*, AD-9, Module declaration
- **Pair 1 (kinds without a writer):**
  - **Unit A — module release.** It declares a cron input binding or an HTTP middleware Component, neither of which is data-service-bound. The tier table lists only "data-service-bound Dapr Components" in the environment layer, so the generator places the Component in the application package.
  - **Unit B — app deploy RBAC.** AD-8 says "The application deploy identity holds none of these", so it has no Component write. The Helm upgrade is forbidden and the attempt fails.
  - **Other kinds.** HTTPEndpoints (for declared external egress) and Dapr 1.18 MCPServers (memlog 2026-09-28 version entry) sit in no tier, although "Every deployed object belongs to exactly one tier".
- **Pair 2 (two version authorities):**
  - **Unit C — environment-layer attempt.** It renders Components from its stated version authority, the "Profile inventory environment facet".
  - **Unit D — chart generator.** It binds a new logical role's configured name from the candidate's declarations. No Component with that name exists, so readiness fails.
  - **Result:** If Unit C renders from declarations instead, the spine does not say which release's declarations to use: baseline, candidate, their union, or the recovery point's. DR and the staging reset need the same answer.
- **Minimal rule (Release tiers and AD-1):**

  > "Every Dapr Component, HTTPEndpoint and MCPServer is environment-layer and written only by the environment-layer identity. Environment-layer objects derived from declarations — Components with their names and scopes, HTTPEndpoints, Gateway listeners and bootstrap Secrets — are rendered from the union of the working baseline's and the candidate's release records during the candidate's preparation and applied as its forward-only input. In DR and in a staging reset they are rendered from the recovery point's release. The profile inventory versions provider versions and topology only."
- **Disposition:** autofix.

### ADV-15 — One subscription, two owners: attribute-bound code and rendered Subscription objects

- **Severity:** medium · **ADs:** AD-9 *Component names*, Release tiers (application package), Module declaration (*Integration*), Owned work (Dapr component-name injection)
- **Unit A — EventStore SDK story.** AD-9 says "no `const` or attribute literal names a component, and subscriptions read the configured name". Owned work names "attribute-bound subscriptions". Unit A keeps programmatic subscriptions that read the configured pub/sub name at startup.
- **Unit B — chart-generator story.** The application package tier lists "Subscriptions", and the declaration carries "topics with dead-letter policy". Unit B renders declarative Subscription objects from those declarations.
- **Incompatibility:** The same app, pub/sub and topic are subscribed twice, with different routes and dead-letter settings. Dapr's subscription-methods page documents no precedence between methods. The declared dead-letter policy is missing from the programmatic path. After a rollback, the rendered Subscription follows the package, but the code subscription follows the image.
- **Minimal rule (AD-9):**

  > "In Platform compositions, subscriptions for declared topics are declarative Subscription objects that Platform renders from declarations, carrying the configured component name and dead-letter policy. Module code registers no programmatic or streaming subscription for a declared topic outside isolated module use."
- **Disposition:** autofix, with EventStore confirmation. The alternative is to remove Subscriptions from the tier table and make the code path the only one.

### ADV-16 — The AD-15 expand-only list omits forward-only inputs that this spine introduced

- **Severity:** medium · **ADs:** AD-15 *Expand-only*, AD-6 (contract instance), AD-3 *Outside rollback*, Release tiers
- **Unit A — Projects N+1 realm story.** Projects N+1 no longer calls Folders, so realm contract v7 drops the `(projects-service → folders)` token-exchange permission. Administrator applies v7 before the attempt. This is allowed: "token-exchange permissions and preconditions" are part of the AD-6 contract instance, but AD-15 protects only "realm-contract clients, roles, audiences and claims", and v7 counts as "a compatible later" version.
- **Unit B — running baseline and AD-15 rollback target.** Projects N still exchanges tokens for Folders.
- **Incompatibility:** Projects N's Folders calls fail immediately. That happens before the attempt starts, outside any verification window, and it breaks the next precondition-8 health check. The same failure follows from:
  - reclassifying a client in the client-to-surface map (the baseline gateway derives the surface from the new map);
  - changing admission-group membership or synthetic clients;
  - removing a Gateway listener hostname or certificate for a retired interface (the baseline's routes detach).
- **Minimal rule (append to AD-15 *Expand-only*):**

  > "... client-to-surface map entries; token-exchange permissions and preconditions; admission groups and synthetic clients; Gateway listeners with their hostnames and certificates; HTTPEndpoints."
- **Disposition:** autofix.

### ADV-17 — The Platform-identity check leaves the tool version and the Builds catalog floating per workspace

- **Severity:** medium · **ADs:** AD-4 *Mapping*, *Mode* and *Platform identity*; Module declaration; AD-10
- **Pair 1 (tool version and catalog):**
  - **Unit A — Parties workspace.** Builds submodule at B1, `hexalith-module` 1.2, Platform at P.
  - **Unit B — Folders workspace.** Builds submodule at B2, `hexalith-module` 1.4, Platform at P.
  - **What each obeys.** Both pass the AD-4 check, which compares only "the Platform composition ... source commit" with "the submodule HEAD". Packages "resolve ... at the Builds catalog version". Per the Builds README, that catalog is the workspace's own `references/Hexalith.Builds/Props/Directory.Packages.props`. The tool is "pinned in the workspace's `.config/dotnet-tools.json`".
  - **Result:** One Platform identity composes different EventStore.Aspire, Dapr toolkit and Aspire versions and applies different validator behaviour and descriptor versions. The publication workflow uses a third catalog, Platform's own. Neither workspace's CI evidence matches what staging runs. This is the "silently substituting packages" that AD-4 prevents.
- **Pair 2 (Platform's own repository):** AD-10 makes the runner the only multi-module lifecycle owner, so Platform's own integration tier must use it. However, the Platform repository has no `references/Hexalith.Platform`, and the literal CI check refuses to run there.
- **Minimal rule (AD-4 *Platform identity*):**

  > "The Platform identity pins the Builds catalog commit and the Platform tool version. The tool refuses to run in CI when the workspace's Builds submodule or tool-manifest pin differs from those pins, and warns in local source mode. In the Platform repository, the repository HEAD is the Platform identity."
- **Disposition:** discuss. It adds lockstep-bump cost to C-31.

### ADV-18 — Local McpCli cannot enroll the active module's source Contracts

- **Severity:** medium · **ADs:** AD-11 *Tool* and *Availability*, AD-4 *Mapping*; McpCli AD-4
- **Unit A — McpCli enrollment story.** It obeys McpCli AD-4: "each production Contracts `PackageReference` in the tool project carries `HexalithContracts="true"`. An MSBuild target ... matches those package identities to `ReferenceCopyLocalPaths` by `NuGetPackageId`, fails if a flagged package contributes zero or multiple Contracts assemblies."
- **Unit B — Platform AD-4 mapping story.** "Root-declared Hexalith dependencies build from source in Debug; all others resolve as packages". AD-11 adds: "Locally the Platform tool builds and launches McpCli through the AD-4 mapping."
- **Incompatibility:** Two cases, depending on how the Parties workspace declares McpCli:
  - **McpCli not root-declared.** McpCli resolves as a prebuilt tool package, so it cannot be "built" with source Contracts. Any new or changed Parties operation has a contract-schema digest that differs from the local gateway's catalog and is "not executable".
  - **McpCli root-declared.** The mapping replaces `Parties.Contracts` with a project reference. McpCli's package-identity target then finds no flagged package, and either the build fails or Parties is silently dropped.

  In both cases the "McpCli candidate's flows" are first exercised in staging.
- **Minimal rule (AD-11 *Tool*):**

  > "In local and CI modes, the Platform tool builds a run-scoped McpCli, never published, whose flagged Contracts resolve through the AD-4 mapping. McpCli's manifest target accepts source-mapped Contracts by assembly identity."
- **Disposition:** discuss. It needs a McpCli AD-4 amendment.

### ADV-19 — Splitting McpCli publishers leaves the Decoration Package and the stable version sequence with no owner

- **Severity:** medium · **ADs:** AD-11 *Tool*; McpCli AD-17; Source Precedence (McpCli row)
- **Unit A — McpCli pipeline.** The spine says "McpCli's own pipeline publishes only prerelease versions". McpCli AD-17 says both packages, `Hexalith.McpCli.Abstractions` and `Hexalith.McpCli`, "share one version". Unit A therefore publishes Abstractions only as a prerelease.
- **Unit B — Parties Contracts release story.** Its stable Contracts reference the Decoration Package, so a stable package depends on a prerelease one (NU5104). Separately, the publication workflow builds a stable McpCli per Platform release. When two releases share one McpCli commit but differ in intake Contracts, a version derived from McpCli's own base collides on push. A version derived from Platform numbering diverges from the prerelease sequence.
- **Minimal rule (AD-11 *Tool*):**

  > "McpCli's pipeline also publishes stable `Hexalith.McpCli.Abstractions` versions. The publication workflow alone publishes the stable `Hexalith.McpCli` tool, versioned by McpCli base and Platform release, and never reuses a published version."
- **Disposition:** discuss. It follows up C-04.

### ADV-20 — Legacy compatibility "outside hosted environments" conflicts with "in any Platform composition" and a single realm contract

- **Severity:** low · **ADs:** AD-11 *Legacy surfaces*, AD-6 *Realms*
- **Unit A — EventStore migration record.** "Temporary compatibility use outside hosted environments needs a named migration record". Unit A declares `eventstore-admin` use against local Platform environments and adds its client to the realm contract.
- **Unit B — realm generator and validator.** Legacy surfaces are not "issued a realm client in any Platform composition", and "Local, CI, staging and production realms are generated from one versioned, value-free realm contract".
- **Incompatibility:** Unit B rejects the client. If it did not, one shared contract would place the legacy client in production realms too.
- **Minimal rule:**

  > "Temporary compatibility use happens only outside every Platform composition, for example in a technical module's own-repository AppHost."
- **Disposition:** autofix.

### ADV-21 — The synthetic tenant has two creators

- **Severity:** low · **ADs:** Synthetic identities, AD-10 *Default run*, Startup task lifecycle, Design Paradigm (domain ownership)
- **Unit A — Platform runner.** "Platform owns one pre-provisioned synthetic tenant per environment", and the runner supplies "the synthetic tenant identifier" after startup. Unit A therefore seeds the tenant itself in each fresh run.
- **Unit B — Tenants module.** Domain modules own their aggregates, so Tenants declares a once-per-environment creation task for the same identifier.
- **Incompatibility:** In a fresh local or CI run, the second create is rejected. The startup task fails, and so does the run's readiness.
- **Minimal rule (Synthetic identities):**

  > "Tenants owns the synthetic tenant's aggregate through one idempotent creation task keyed by the Platform-assigned identifier: once per environment locally and in CI, operator-only in hosted environments. Platform owns only the identifier and its publication."
- **Disposition:** autofix.

## Attacks attempted and not raised

- **EventStore AD-9 "one slice" across AppHost and deployment assets versus two Platform tiers.** AD-1 *Single definition* makes module deploy assets conformance inputs only.
- **Keycloak single server hostname versus per-environment issuers.** Realm frontend URLs allow a production-controlled issuer name. Hosted interfaces does not forbid it.
- **HTTP-01 and wildcard listeners.** A wildcard listener cannot pass HTTP-01, and per-environment hostname lists already exclude wildcards.
- **Production ACME with closed ingress (G1).** Production uses its own ACME credentials, not HTTP-01. That is consistent.
- **The AD-14 interim default blocking confirmed chains.** This is the intended fail-safe outcome (C-06).
- **Environment namespaces and trust domain missing from the environment-current list.** "Differing only in declared environment bindings" covers them. A literal-list reading is weak.
- **Platform ↔ McpCli circular submodules.** AD-4 already forbids nested-Platform fallback, and no rule makes the Platform model reference McpCli packages.
- **"Gateway" naming the Gateway API object and the `eventstore` host.** Cosmetic. Every rule that says "gateway" in the authorization sense names the composed host.
