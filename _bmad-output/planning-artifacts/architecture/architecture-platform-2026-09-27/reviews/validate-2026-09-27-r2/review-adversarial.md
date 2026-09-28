# Review — Adversarial divergence attack (validate r2, 2026-09-27)

**Verdict: FAIL.** No critical hole. Eight high-severity pairs remain in which two units, each obeying the spine to the letter, build a hosted environment that cannot load its secrets, cannot enter or leave a degraded production, lets a rollback target silently expire, or launders or loses the calling surface. Every hole closes with a tightened rule in an existing AD or convention row. None reopens a user-adopted decision. Two of the holes, ADV-6 and ADV-8, come from decisions recorded in the memlog (V-18 and V-48) that are missing from the current spine text.

Counts: critical 0 · high 8 · medium 4 · low 1 (13 findings).

Scope: I read the full spine (329 lines, `updated: 2026-09-27`, working tree) and its uncommitted diff against `HEAD`, which contains the AD-11 amendment, the McpCli source row and the legacy-retirement Deferred row. I also read both prior adversarial reports, the memlog entries for V-10, V-18, V-48, V-60, R-08, ADV-U8 and the gate fixes, and PRD FR-12 with its addendum. Module sources: EventStore `architecture.md` (AD-9, AD-11, AD-21–AD-33), Memories spine (AD-5, AD-6, AD-15, AD-19, AD-21), McpCli spine (ecosystem direction, AD-21, Decoration Package), and Projects spine (course correction, AD-25, AD-29, the action matrix). Code evidence comes from `Hexalith.FrontComposer.Mcp`, `Hexalith.Memories.Aspire`, `Hexalith.Memories.EventStore` and Dapr documentation. This is a consistency review. It proves nothing about runtime or deployment.

## Prior holes re-checked

| Prior ID(s) | Status | Evidence in the current text |
| --- | --- | --- |
| validate ADV-1; update U7, U13, U15 (composed host builder, subject, source mode, extension limits) | Closed | AD-13: "composed by Platform…"; "Local and CI modes assemble the host through the AD-4 mapping"; "the composed image is a Platform subject whose release-available and production-promoted records are Platform-issued". EventStore ratification is still pending (Deferred). |
| ADV-2 (Dapr names and roles) | Partial | The declaration row now says "Platform assigns component names, namespaces, FQDNs and scopes". However, EventStore AD-24 and AD-26 still fix `openbao` and `statestore`, and the spine reserves neither name. Where Components and bootstrap Secrets live is a new hole (ADV-1). |
| ADV-3 (staging profile) | Closed | "Staging runs the same template, differing only in declared environment bindings"; promotion "requires equal staging and production profile digests". |
| ADV-4 (production-promoted issuer, rollback-target validity) | Closed | AD-13 and the Production profile row ("valid rollback target, only while … its record is valid for the active profile digest"). The new renewal rule conflicts with qualified sets (ADV-3). |
| ADV-5; U2; U16 (rollback catalog generation, retention entries, activation order) | Partial | AD-15 and the Catalogs activation order close the structure. Two things stay open: when the rollback generation's content is fixed, and which generation the recovery render carries (ADV-9). |
| ADV-6; U17; U18 (serialize every mutation path, lock epoch, shared-infrastructure identity) | Partial | The Attempt ownership lock list and epoch are in place. Realm-contract application and Memories operator-artifact changes are not in the list (ADV-2). |
| ADV-7; U6 (one version authority) | Closed | AD-3: "Each environment-layer object has one version authority, a profile-inventory facet". |
| ADV-8 (recovery classes co-located) | Closed | Backup coverage: "A backup unit holds one recovery class…". |
| ADV-9 (legacy second writer) | Closed | AD-1: "legacy deployments are never a second writer". |
| ADV-10; U28 (fixture ownership) | Closed in the spine; not propagated upstream | The McpCli spine AD-16 and its open question still build `tests/Hexalith.McpCli.AppHost` and require `Hexalith.Parties.Aspire`. The "Connected McpCli" Deferred row lists no harness amendment. |
| ADV-11 (two Platform identities, schema skew) | Closed | AD-4 submodule identity; declarations accept "the current and previous major". |
| ADV-12; U29 (startup-task scopes, recovery scope) | Closed | Startup task lifecycle row. |
| ADV-13 (Projects envelope) | Closed | Source Precedence, under explicit user authority. |
| ADV-14; U9; U10 (synthetic identities) | Closed | Synthetic identities row. Who owns the shape of the synthetic flag is new (ADV-13). |
| ADV-15; U11; U12 (compatibility key, candidate, `--actor`) | Closed, but eligibility is not carried | The per-operation digest is in AD-11. Memlog V-18's "read by both catalog generation and McpCli" was lost (ADV-6). |
| ADV-16; U27 (realm declaration and version check) | Closed for drift and pre-check | The realm contract has no expand/contract rule (ADV-2). |
| ADV-17 (check-suite contract) | Closed | First shared versions (Builds) and the check-suite digests in AD-2. |
| U1; U3; U4 (binding classes, render inputs, baseline identity) | Closed | Binding classes row. Where the promotion stop is classified and how many attempt records exist are new (ADV-11). |
| U5 (profile-digest preimage and renewal) | Partial | The preimage is defined. Renewal contradicts the immutable qualified sets (ADV-3). |
| U8 (cross-module actor) | Closed for the actor; open for the surface | Token exchange preserves `sub`, but the gateway derives the surface from the intermediate module's `azp` (ADV-5). |
| U14 (catalog per composition) | Closed | Catalogs: "One deterministic Platform generator produces each composition's catalog". |
| U19 (environment-layer definition) | Partial | AD-1 defines the environment layer, but the identity scopes give its Components and bootstrap Secrets no lawful home (ADV-1). |
| U20 (G1 ingress) | Closed | G1 closed-ingress definition. |
| U21 (release modes) | Partial | Approved mode still requires a healthy working baseline that G1 and post-failure states lack (ADV-4). |
| U22–U25 (drill substitutions, fence rewind, recovery executor, tenant-key store) | Closed | Drill evidence row; DR step 3 quarantine and step-4 repeated fence proof; AD-7 recovery executor; AD-12 tenant-key store. The post-DR handover is new (ADV-10). |
| U26 (cumulative classification) | Closed | Module intake row. |
| U30 (provenance per artifact class) | Closed | Workflows and provenance row. AD-2's accepted writer for attempt records omits the recovery executor (ADV-10). |

## New findings

### ADV-1 — Environment-layer Dapr Components and bootstrap Secrets have no namespace that any identity may write
- **Severity:** high
- **ADs:** AD-1, AD-3, AD-7, AD-8, Secrets row; EventStore AD-24 and AD-28
- **Unit A — the environment-layer workflow (per-environment executor):** It follows AD-1: "data-service-bound Components go with the environment layer". It follows AD-8: the data namespace holds "data-service instances, OpenBao instance, persistent volumes and bootstrap Secrets, outside the application deploy identity's scope". It runs with AD-7's "separate environment-layer identity for its data namespace". It therefore renders the state, pub/sub and `secretstores.hashicorp.vault` Components, plus the per-app OpenBao tokens and `APP_API_TOKEN` Secrets (Secrets row: "Its per-app tokens and any Kubernetes Secrets are documented bootstrap exceptions"), into the data namespace, the only namespace it can write.
- **Unit B — the application package and its sidecars:** The package deploys workloads into the application namespace with the "namespace-scoped application deploy identity". Dapr loads only Components deployed to the sidecar's own namespace ([component scopes](https://docs.dapr.io/operations/components/component-scopes/)). `secretKeyRef` bootstrap values and `dapr.io/app-token-secret` resolve against Secrets in that same namespace. EventStore AD-24 requires "DAPR components use `auth.secretStore: openbao` and `secretKeyRef`".
- **Incompatibility:** Every sidecar starts with no state store, pub/sub or secret store and no app-channel token. Secret-gated readiness ("Required generations gate readiness") then fails in every hosted environment. Only two alternatives exist, and each breaks a rule. If the environment-layer identity writes into the application namespace, it breaks AD-7's scope. If the application deploy identity writes the Components and Secrets, it breaks AD-8 ("outside the application deploy identity's scope") and AD-1 (Components would then ship with the rolled-back package).
- **Minimal rule:** Add to AD-7/AD-8: "The environment-layer identity also holds namespaced write on `components.dapr.io` and on the named bootstrap Secrets in its environment's application namespace. The application deploy identity holds neither. Data services, OpenBao and volumes stay in the data namespace."
- **Disposition:** autofix

### ADV-2 — Forward-only inputs derived from declarations have no expand/contract rule, so applying them before the attempt breaks the running baseline and the rollback target
- **Severity:** high
- **ADs:** AD-6, AD-15, AD-3, AD-1, Secrets row, Attempt ownership; Memories AD-5
- **Unit A — the realm-contract instance and Administrator procedure:** Parties release N renames role `parties-reader` to `parties.read`, or drops an audience. It is compliant because "modules declare required clients, audiences, roles and claims". Platform generates realm-contract v5 from the candidate's declarations. Administrator applies it forward-only because "The contract version is bound in the release record and applied forward-only by Administrator before the attempt that needs it". The pre-check passes: "the production realm reporting the required realm-contract version".
- **Unit B — the production working baseline and its AD-15 rollback combination:** Both still run N-1 code, which requires the old role. AD-15's protection covers only "catalog generations, idempotency and key generations, secret retirement, automatic rollback". Realm-contract application is not in the Attempt ownership lock list ("release, rollback, config-only change, rotation rollout, catalog activation, environment-layer or profile change and DR restore").
- **Incompatibility:** Applying v5 breaks production authorization outside any attempt, with no verification window and no recovery. The next pre-check then fails ("healthy production"). If the change lands inside an attempt, automatic recovery to N-1 fails verification. The same shape recurs twice:
  - Data-service-bound Components and their scopes are rendered from the candidate's declarations and applied forward-only ("Every environment-layer input a release needs is applied forward-only and verified before the attempt"). A renamed logical role, or an app ID dropped from scopes, strands N-1.
  - The Memories operator artifact's routing tuples are keyed by "(subscribing component, topic, authenticated publisher)". Its sole writer is the operator (Memories AD-5), and the spine says "Platform renders its operator artifact". A Platform-renamed component therefore removes N-1's tuple, and Memories routing fails closed.
- **Minimal rule:** Extend AD-15: "Every forward-only input derived from declarations stays expand-only relative to the working baseline and the prepared rollback combination until the candidate is recorded working. This covers realm-contract clients, roles, audiences and claims; environment-layer Components with their names, scopes and metadata; secret-contract entries; and the Memories operator artifact. Contraction happens in a later attempt." Add "realm-contract application and operator-artifact change" to the lock list.
- **Disposition:** autofix

### ADV-3 — Immutable qualified digest sets make every environment-layer patch invalidate the working release, despite the renewal rule
- **Severity:** high
- **ADs:** AD-2, AD-3, Production profile row, AD-15, Before production update, Infrastructure currency
- **Unit A — the environment-layer change workflow:** It applies an OpenBao or PostgreSQL security patch because "Inventory pins stay within upstream support and current on security patches". It also applies "Redis Stack 7.4 to Redis 8 through the Memories digest set". It follows "An environment-layer change is its own attempt, staging first; the production attempt ends by re-verifying the working release and issuing a renewed production-promoted record at the new digest".
- **Unit B — the production deployment workflow's deployability and rollback-target check:** "A release is deployable, and a valid rollback target, only while current environment-layer versions fall within its qualified sets and its record is valid for the active profile digest; otherwise the change is breaking." The qualified sets sit in the release record, "the single binding of a release's release-invariant values: … qualified environment-layer digest sets". That record is immutable and accepted "only from that workflow", the public publication workflow.
- **Incompatibility:** A new patch digest cannot be in a qualified set written before it existed. After the renewal, the working release holds a valid record but fails the qualified-set conjunct, so it is neither deployable nor a valid rollback target. AD-15's combination then cannot be recorded, and every later release routes to Administrator-approved mode. The environment-layer change itself counts as "breaking", yet no mode exists for a breaking environment-layer change. The renewal clause and the deployability clause disagree about the same release.
- **Minimal rule:** "Qualified sets are declared as version constraints per facet, such as a minor line plus 'security patches', rather than exact digests. The Platform-issued renewed production-promoted record, backed by re-verification evidence, extends a release's qualified set for that environment at the new digest. Deployability reads the release record's constraints or a valid renewal." An alternative, heavier fix is to require a new release publication whose sets include the new digest before any environment-layer change.
- **Disposition:** discuss

### ADV-4 — No compliant path exists to production while it has no healthy working baseline: G1, after a failed first deployment, or after a failed recovery
- **Severity:** high
- **ADs:** Release modes, Before production update, Staging gate, Automatic recovery, Production entry gates, First shared versions
- **Unit A — the G1 deployment epic:** It deploys through Administrator-approved mode, which "serves pre-G3 releases" and "replaces only those two triggers [G3 and complete compatibility evidence] with an authenticated Administrator record", into the namespace with "ingress closed" (G1).
- **Unit B — the deployment workflow's precondition checker and the staging gate:** Production requires "a healthy production, meaning its working baseline is ready and its smoke suite passes now", and "A missing precondition stops without mutation". "One serialized attempt deploys production's working baseline, upgrades to the candidate…". Also "The staging gate, lock, provenance, verification and records apply to both".
- **Incompatibility:**
  - **G1:** Before G1 no working baseline exists, so both the staging gate and the pre-check stop the first production deployment.
  - **After a failed first deployment:** "A failed first deployment keeps ingress closed and stops" and sets the promotion stop. The stop clears only with "a verified current working release", which no workflow path can now produce.
  - **After a failed recovery:** The same deadlock follows.
  - **The only escape:** "A manual or DR change becomes the working baseline only after passing verification". A manual change has no defined workflow, mode, lock or provenance, which invites exactly the unrecorded out-of-workflow writer that U21 closed.
  - **Record ordering:** Records are needed from G1 onward, yet "Release and attempt record encoding" must only precede "Automated promotion" (G3). Baselines created at G1 are therefore written before the Builds encoding exists, and later validators cannot read them.
- **Minimal rule:** Define an empty-baseline and degraded-production entry. When production has no working baseline, or its last outcome was non-working, an Administrator-approved record may replace the healthy-production precondition. The staging gate then rehearses a fresh install plus the candidate instead of baseline-to-candidate. The rollback combination is "remove workloads, keep data", matching the first-enrollment rule. A "manual change" is always an approved-mode attempt under the lock and epoch. Move "Release and attempt record encoding" to "Must precede: first staging deployment".
- **Disposition:** autofix

### ADV-5 — The server-to-server clause launders or loses the originating calling surface
- **Severity:** high
- **ADs:** AD-14, AD-6 (client-to-surface map), AD-11; Projects AD-20, AD-29 and the action matrix
- **Unit A — a Projects cross-module step:** It follows AD-14: "synchronous steps carry the user through Keycloak standard token exchange by the calling module's confidential client; asynchronous task steps use the original actor attested by EventStore at admission". An McpCli call to an agent-eligible Projects operation (for example `project.create`) triggers a Folders operation. Keycloak sets `azp` of the exchanged token to the requesting client (memlog reality evidence: "Keycloak sets azp to the requesting client_id").
- **Unit B — the Folders gateway authorization:** It follows AD-14: "gateways derive the surface from the authenticated client (`azp`) through that map", where each client is "mapped to exactly one surface class".
- **Incompatibility:** The gateway sees the surface class of Projects' confidential service client, not McpCli's. The outcome depends on how the realm contract maps that client:
  - **Mapped to a class eligible for confirmation-required or UI-only operations** (it is confidential and server-side, as AD-14 requires): an agent-originated chain executes Folders operations that AD-14 denies to agents, which is the "agent-held tokens executing UI-only operations" that AD-14 "Prevents".
  - **Mapped to a least-privilege class:** legitimately confirmed UI flows (for example Projects' `project-folder.replace`) fail at Folders.

  Asynchronous steps have the same problem, because only the actor, not the surface, is attested. Projects requires "Delegated service/workflow callers … never gain end-user confirmation authority", and no surface value exists that carries this.
- **Minimal rule:** Add to AD-14: "The effective surface of a server-to-server step is the originating request's surface, which EventStore attests at admission and carries with the exchanged or asynchronous context. It is never the intermediate module's client surface. Module service clients map to a service class that grants no UI-only or confirmation-required eligibility of its own. A downstream confirmation-required operation executes only by consuming the originating module's confirmation record."
- **Disposition:** discuss

### ADV-6 — Surface eligibility is declared on one side and enforced on the other with no carrier between them, and the metadata endpoint cannot tell retention entries from executable ones
- **Severity:** high
- **ADs:** AD-11, AD-14, AD-15, Catalogs row, First shared versions; memlog V-18; PRD FR-12
- **Unit A — module Contracts and McpCli:** They follow AD-11: executable when "its Contracts-declared surface eligibility allows it". Modules decorate their Contracts with the only decoration package that exists, McpCli's `Hexalith.McpCli.Abstractions`, and McpCli filters on its bundled copy.
- **Unit B — the EventStore gateway and catalog schema:** They follow AD-14, where the gateway derives the surface, and PRD FR-12: "using a public client's token for a direct gateway call must not bypass UI-only or human-confirmation restrictions". The catalog schema they build is exactly the first shared version listed, "Routing catalog schema with per-operation schema digests and retention entries". Catalogs: "EventStore.Contracts owns the routing schema and codec, including per-operation contract-schema digests".
- **Incompatibility:**
  1. **No eligibility input at the enforcement point.** The gateway has none: the catalog carries none, extension packages "implement only the extension API", and the declaration row does not enumerate eligibility. The gateway therefore fails either open (a direct `curl` with McpCli's token runs UI-only operations) or closed (FR-12's executable set is empty). Each side can assume the other enforces. Memlog V-18 decided "Surface eligibility is declared once in module Contracts and read by both catalog generation and McpCli". The spine no longer says so.
  2. **Eligibility is outside the per-operation digest.** A Contracts change that makes an operation UI-only leaves the digest unchanged, so a published McpCli keeps advertising the operation as executable.
  3. **Retention entries look executable.** After automatic recovery commits the rollback generation, the metadata endpoint "derived from the environment's committed catalog" lists AD-15 retention entries, which "non-executable retention entries" describes, with matching digests. McpCli N shows the candidate's command C as executable on the rolled-back N-1 host. That is the "discovery advertising operations the selected environment cannot run" that AD-11 "Prevents".
- **Minimal rule:** "Each catalog route entry carries the operation's surface eligibility (agent-eligible, UI-only, confirmation-required, step-up), which the Platform generator derives from Contracts. The contract-schema digest covers it. EventStore.Contracts owns the vocabulary and the gateway enforces from the active generation. The metadata endpoint serves eligibility and publishes only executable entries, and McpCli uses the served eligibility rather than its bundled copy." EventStore must confirm the schema field.
- **Disposition:** autofix

### ADV-7 — DR gives module-owned dynamic credentials two writers and gives the rebuilt data services no principal re-provisioning step
- **Severity:** high
- **ADs:** AD-12, Disaster recovery sequence, Secrets row, Startup task lifecycle, Memories erasure continuity, First shared versions (recovery hook contract); Memories AD-6 and AD-15
- **Unit A — the Platform recovery-executor DR workflow:** It follows "4. Rotate every restored credential and application signing key" and "Platform owns scopes and acknowledged rotations". It rotates every OpenBao entry directly, including the "module-owned dynamic namespaces such as Memories per-tenant credentials". It invokes only the hooks defined in "Recovery hook contract: quarantine admission, purge, rebuild, integrity, external-effect reconciliation, result shape".
- **Unit B — Memories:** AD-6 "is its sole writer" of "backend identities and credentials, per-tenant grants". Its AD-15 says "rotation of a tenant data-plane credential is an AD-6 lifecycle operation verified by confirming the prior credential no longer authenticates". Per the spine, "Shared Redis/FalkorDB projections and tenant-keyed coordination have no operational backup-restore path; they rebuild", so on replacement capacity Redis and FalkorDB start empty, with no per-tenant ACL principals or graphs.
- **Incompatibility:**
  - **Two writers.** Platform's direct rotation creates a second writer, so Memories' lifecycle evidence and generations disagree with OpenBao.
  - **No step creates the principals.** Step 3's "rebuild-only replay" needs per-tenant principals that no step creates. They are not a hook, and "Creation tasks run only when creating a new environment — never on … replacement-capacity recovery".
  - **Ordering.** Step 3 rebuilds before step 4 rotates, so rebuild connections are established under credentials that Memories must then tear down.

  Step 5's restored-release smokes on the synthetic tenant fail, or pass only after an unplanned operator repair that sits outside the measured four-hour RTO and the G2 drill.
- **Minimal rule:** Add "backend-principal and dynamic-credential re-provisioning" to the recovery hook contract. DR step 4 rotates module-owned dynamic namespaces only by invoking the owning module's hook. That hook runs after admission and purge and before rebuild-only replay, in declared-dependency order.
- **Disposition:** autofix

### ADV-8 — Today's AD-11 amendment removed the exclusion of legacy MCP hosts from Platform compositions and widened "CLI" to every automation tool
- **Severity:** high
- **ADs:** AD-11 (amended), AD-14, AD-1, AD-2, AD-12, Source Precedence (McpCli row), the Deferred legacy-retirement row; memlog V-48
- **Pair A (surface exposure):**
  - **Unit A — a Projects or Parties enrollment story:** It enrolls the module's existing hosts. Its MCP plug-in or host is "obsolete" but not yet retirable: "Retire each source only after its owner-approved operation inventory and replacement or withdrawal evidence", with retirement "Outside the first increment". It is not "new", and only "no new proprietary module MCP/CLI host is admitted". `FrontComposer.Mcp` is mapped into the web UI host (`MapFrontComposerMcp` in `samples/Counter/Counter.Web/Program.cs`), and its identities are static API keys (`FrontComposerMcpApiKeyIdentity(TenantId, UserId)`). `Parties.Mcp` and `Memories.Mcp` are hosted HTTP MCP servers.
  - **Unit B — the Platform realm-contract and ingress epic:** It maps the UI host's confidential client to the UI surface class, per AD-14.
  - **Incompatibility:** Agent traffic enters the in-process MCP endpoint and reaches the gateway under the UI client's `azp`, so UI-only and confirmation-required operations become agent-executable. The previous text, recorded as V-48 ("neither deployed nor routed in Platform local, CI, staging or production compositions until a later AD admits each"), closed this. The amended text keeps only "McpCli stdio is the only MVP MCP surface", which contradicts "retire only after evidence" for sources that exist during the MVP. The memlog has no entry superseding V-48.
- **Pair B (scope):**
  - **Unit A — Builds, a technical module:** It ships the check-suite runner and the release-record validator as pinned CLIs. EventStore AD-11: "The SHA-pinned shared Builds publisher/validator owns … bounded smoke contract". Projects names the `hexalith-evidence validate` capability. Modules also need automation-invocable recovery hooks before the first drill.
  - **Unit B — the McpCli owner:** It applies "All proprietary Hexalith module and technical-module … CLIs … are obsolete migration sources; Platform admits no new alternate proprietary MCP/CLI surface" together with Source Precedence ("Canonical target for all Hexalith-owned CLI/MCP access", which also covers the Platform .NET tool).
  - **Incompatibility:** Validators, the Platform tool and hook executables become obsolete or inadmissible. Their cut-over is also barred until "A generic McpCli contract and transport decision", which is deferred "Outside the first increment". Meanwhile "Recovery hook contract … Must precede First AD-12 drill", which is before G2.
- **Minimal rule:**
  - Restore: "Until an AD admits it under AD-14, no obsolete MCP/CLI source is deployed, routed, mapped in an enrolled host, or issued a realm client in Platform compositions. Declaration validation rejects an enrolled host that maps an MCP endpoint."
  - Scope AD-11 to "interactive operation-access surfaces over module capabilities". Build, validation, local-environment, deployment and recovery-hook executables are governed by AD-2, AD-4, AD-10 and AD-12, and the recovery hook contract fixes a non-interactive transport such as a job image or a Dapr-invoked endpoint.
  - Record the amendment in the memlog.
- **Disposition:** autofix (Pair A restores a recorded decision); discuss (Pair B scope wording)

### ADV-9 — The rollback generation is fixed before the candidate writes, and the recovery render reads the committed candidate generation
- **Severity:** medium
- **ADs:** AD-15, Catalogs row, Binding classes, Automatic recovery, Verification; EventStore AD-25 and AD-33
- **Unit A — rollout activation:** It follows Catalogs: "prepare the candidate and rollback generations; ready-validate the rollback generation on the running baseline hosts; Helm upgrade …; commit at readiness". The rollback generation holds "the baseline's routes plus every still-referenced idempotency and key entry", evaluated at preparation, when no record references candidate-only command C. Smoke writes then create C records ("Smoke writes use only synthetic identities and data").
- **Unit B — recovery and the EventStore catalog authority:** Recovery "render[s] the working baseline's package with environment-current values" and environment-current catalog generations are "read from the target environment at … rollback". That committed generation is the candidate's. EventStore AD-25: "Retirement is refused while records … or catalog references remain"; "Readiness fails on missing entries".
- **Incompatibility:** Two things go wrong:
  1. Committing the prepared rollback generation drops C's entry while C records exist. EventStore refuses the commit, and every staging rehearsal that exercises "an idempotent command new in the candidate" fails, which makes every such release "breaking".
  2. Taken literally, the recovery render passes the committed candidate generation to baseline hosts, which fail readiness. The single recovery is spent.
- **Minimal rule:** "The rollback generation contains every idempotency and key entry of both the baseline and the candidate generations, with candidate-only entries as retention entries, fixed at preparation. Recovery mirrors activation: the baseline render carries the prepared rollback generation, and the commit happens at readiness."
- **Disposition:** autofix (confirm with EventStore; already a Deferred confirmation item)

### ADV-10 — After DR, no executor may operate production and no accepted writer exists for its attempt records
- **Severity:** medium
- **ADs:** AD-7, AD-2, Attempt ownership, Automatic recovery, DR sequence
- **Unit A — the recovery executor:** It "runs only recovery workflows with pre-provisioned credentials for the prepared capacity" and performs DR, which "takes a new epoch". It writes the attempt record whose verified outcome makes it the working baseline ("A manual or DR change becomes the working baseline only after passing verification").
- **Unit B — the production executor and deploy verifier:** The executor has "access to the designated Kubernetes API" and credentials "only for its own environment". Its deployment credentials are revoked in DR step 1. The verifier accepts "attempt records and evidence only from the environment's executor identity".
- **Incompatibility:** After DR, the replacement cluster's baseline was written by an identity the deploy verifier rejects. The production executor cannot reach the replacement API. The recovery executor refuses routine release, rotation and environment-layer workflows. The next attempt stops on an unreadable or unaccepted baseline, and production stays frozen until an undefined handover.
- **Minimal rule:** "The recovery executor is an accepted attempt-record writer for DR attempts. DR step 6 includes re-provisioning the production executor's allowlist and credentials for the replacement capacity, recorded as part of the DR attempt before promotion resumes."
- **Disposition:** autofix

### ADV-11 — "Promotion stop" is classified as an attempt-bound field, and the number of attempt records is ambiguous
- **Severity:** medium
- **ADs:** Binding classes, Attempt ownership, Automatic recovery, Production profile row; First shared versions (Builds encoding)
- **Unit A — Builds' "Release and attempt record encoding":** It follows "Attempt-bound values — … lock epoch, timers, outcome and promotion stop — live in the per-environment attempt record" and encodes `promotionStop` in the attempt record.
- **Unit B — later non-release attempts:** Rotation rollouts, config-only changes and environment-layer attempts ("the production attempt ends by re-verifying the working release") each write their own record with outcome `working`. "The working baseline is the release record plus the attempt record of the last working attempt."
- **Incompatibility:** The failure depends on how the "per-environment attempt record" is read:
  - **One record per attempt:** the latest record shows no stop, so a rotation clears a stop that "only an authenticated Administrator record … clears".
  - **One mutable record per environment:** the next attempt overwrites the last working attempt's record, which AD-2 must keep for "their life as a rollback target".
- **Minimal rule:** "The promotion stop is durable per-environment state with its own set and clear records. Attempt records are one per attempt and immutable once terminal. The latest-working pointer is per-environment state."
- **Disposition:** autofix

### ADV-12 — The staging environment and the shared registry share one DNS zone, so staging's ACME credential can mint the registry's certificate
- **Severity:** medium
- **ADs:** Hosted interfaces, Workflows and provenance, AD-8
- **Unit A — the Exposure/DNS/certificates epic:** It follows "Staging uses `hexalith.com`", "each DNS zone has one named owner, and certificates use one declared ACME challenge with per-environment credentials". It chooses DNS-01 with staging's `hexalith.com` zone credential, held for a namespaced issuer in staging.
- **Unit B — shared infrastructure:** "Retained artifacts live in the Builds registry `registry.hexalith.com`". "The staging namespace can neither serve production or shared-infrastructure hostnames (registry, identity issuers) nor obtain their certificates."
- **Incompatibility:** A zone-wide DNS-01 credential issues a publicly trusted certificate for `registry.hexalith.com`, or for any identity issuer placed under `hexalith.com`, outside the cluster. The admission-policy seeds cannot stop that. The zone therefore has two owners, staging and shared infrastructure. The single FQDN pattern can also map a module's logical name onto `registry`.
- **Minimal rule:** "Shared-infrastructure hostnames live in a zone or delegated subzone that no environment credential can write. Each environment's ACME credential is limited to its delegated names. The FQDN pattern reserves shared names."
- **Disposition:** autofix

### ADV-13 — The synthetic-tenant flag has no owning shape that asynchronous aggregators can read
- **Severity:** low
- **ADs:** Synthetic identities, AD-6
- **Unit A — the Platform realm-contract epic:** It flags synthetic principals as "synthetic actor and workload clients flagged in their tokens", with a claim whose name EventStore owns.
- **Unit B — Projects' analytics:** Projects' rolling 30-day outcome aggregates, and Memories access telemetry, consume events keyed by tenant, never tokens. They must keep "synthetic data … out of real-tenant views and aggregates".
- **Incompatibility:** The tenant-level flag ("flagged synthetic tenant") has no owner or shape: it could be a Tenants attribute, a realm-contract value or a Platform list. Aggregators either cannot exclude smoke writes and so pollute SM-7 and SM-8, or each module invents its own marker.
- **Minimal rule:** "The synthetic tenant identifier per environment is published in the environment descriptor and the realm-contract instance, and EventStore attests it on admitted events. Modules exclude by that identifier."
- **Disposition:** autofix

## Attacks attempted and not raised

- **Memories extension in the composed host carrying its SET NX secret.** `Hexalith.Memories.EventStore` is referenced only by `Memories.Server`, so it is not an AD-13 extension.
- **Projects' separate CLI and MCP eligibility against AD-14's single McpCli surface.** The only differing rows are confirmation-required, and those are not executable through McpCli in the MVP.
- **EventStore AD-26's profile-digest preimage against the Platform definition.** This is already the owned first shared version "Profile template and per-release binding split".
- **Intake-manifest and release-record double binding.** The publication workflow verifies intake pins and writes the record in one direction.
- **Lock takeover races between Administrator and a replacement job.** The monotonic epoch settles them.
- **Staging-first qualification for the shared Keycloak and Dapr control plane.** It remains weak, but "both locks, staging first" plus the per-release sidecar skew rule bound it. It is left to the operability lens.
