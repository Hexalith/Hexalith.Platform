# Reconciliation review: r2 findings to the re-distilled spine (update run 2, 2026-09-28)

- Spine under review: `ARCHITECTURE-SPINE.md` (status draft, updated 2026-09-28, 467 lines)
- Baseline for regressions: `git show HEAD:.../ARCHITECTURE-SPINE.md` (status final, 2026-09-27, 328 lines)
- Findings rolled in: `reviews/validate-2026-09-27-r2/findings.json` (C-01..C-64)
- Decisions: `.memlog.md` entries after "(event) Update run 2 started 2026-09-28" (batches 1-6 plus the distillation event)
- Mode: read-only. Line numbers (Lnnn) refer to the new spine.

## Verdict: CONDITIONAL PASS

51 of 64 clusters landed as decided. The C-63 defer and C-64 ignore are handled correctly. No cluster is wholly missing. Ten clusters are partial and one is contradicted. The regression check found one lost owned-work row and several lost binding clauses that no memlog decision authorizes.

The spine passes once the autofixes below are applied and one discuss item is settled. That item is RR-1: the Dapr sidecar patch equality rule and the immutable release-record pin leave no compliant path for a Dapr patch update.

| Status | Count | Clusters |
| --- | --- | --- |
| landed | 51 | all others |
| partial | 10 | C-11, C-13, C-16, C-19, C-23, C-24, C-37, C-43, C-48, C-51 |
| contradicted | 1 | C-26 |
| missing | 0 | none |
| deferred-ok | 1 | C-63 |
| ignored-ok | 1 | C-64 |

## 1. Cluster reconciliation

The target for each cluster is its fix text as modified by the memlog decision. For C-18, C-23 and C-37, it is the fix as changed by the user's divergence, reframing or narrowing.

| ID | Status | Evidence in the new spine, or the gap |
| --- | --- | --- |
| C-01 | landed | AD-11 *Scope* L169: "This rule governs module-operation access surfaces: commands, queries and administration of module capabilities. The Platform tool, `hexalith-evidence`, and build, qualification, validation, deployment and recovery-hook tooling ... are outside it." L170: "frozen with no new operations". McpCli row L318: "Canonical target for Hexalith-owned module-operation CLI/MCP access". |
| C-02 | landed | Terms L36: "The **Platform tool** and **runner** are Builds' `hexalith-module`." Module declaration L216: "Builds implements them as `hexalith-module`'s `hexalith.module-manifest.v1`". Owned L439 (First tool publication): consts removed, "Builds.Module.AppHost launches the Platform model; Builds.Module.EventStoreHost becomes the AD-13 seed or is removed; ... make the Platform-runner CI tier blocking and retire the advisory module-AppHost tier". Migration L229. Residual option wording at L420, see RR-20. |
| C-03 | landed | L170: "They are not enrolled, deployed, routed, mapped in an enrolled host or issued a realm client in any Platform composition; declaration validation rejects an enrolled host that maps an MCP endpoint. Temporary compatibility use outside hosted environments needs a named migration record and removal gate." L171 covers new surfaces and transports. AD-14 L196: "Every externally reachable host that accepts bearer tokens derives the surface ... or accepts only tokens audienced to its own confidential client". L200: negative test for every client in the surface map. AD-11 is tagged [ADOPTED, AMENDED]. |
| C-04 | landed | L166: "is the sole publisher of stable versions, only after staging validation; McpCli's own pipeline publishes only prerelease versions". AD-2 L77: "the stable McpCli package hash". L318: "AD-17's stable publication (prerelease only)". L445: publication contract amendment. |
| C-05 | landed | L454 (First production attempt): "The attempt, lock and stop store reachable from all executors and the monitor; provenance; interruption, epoch and timing; one recovery ... SM-5 rehearsals and EventStore confirmations ... additionally gate G3". L455 (G1): "actual GitHub issue delivery". L458 (G2): deputy delivery and access. G2 L275: "SM-4 isolation and access checks pass, and recovery access, capacity, deputy alert delivery and response coverage are verified". L432: store row. L449: broker at First staging deployment. L452: staging evidence policy. Gate column added. L441: local mTLS row. |
| C-06 | landed | AD-14 *Chains* L199: per-pair downscoped exchange; re-check with fail-closed after revocation; attested originating surface; most-restrictive chain; service class; "consuming the originating confirmation"; "Until EventStore ships that attestation, service clients may call only agent-eligible operations." L197: UI-eligible tokens "never leave their server". |
| C-07 | landed | Catalogs L220: "each route entry carries the operation's contract-schema digest and surface eligibility, both covered by that digest. ... The gateway enforces eligibility from the active generation; its metadata endpoint serves eligibility and lists only executable entries." L167: "McpCli uses the served eligibility, not its bundled copy". L423. |
| C-08 | landed | AD-9 L148: "A logical name may remain an option default for isolated module use, but no `const` or attribute literal names a component, and subscriptions read the configured name." Owned L443. EventStore row L316: `openbao` becomes a logical role. The "owner confirmation" qualifier sits under "Retained", see RR-20. |
| C-09 | landed | Roles L34. AD-6 L116: "except that a recovery run by Administrator or the deputy restores the Keycloak database and re-applies recorded revocations and key rotations; recovery never grants admission or roles". AD-7 L128: trigger path from a pinned off-site copy. AD-12 L180: custodian alert and root-token revocation. Secrets L222: operator role in recovery runs. Diagnostics L228: "assigned to Administrator and the recovery deputy". L273. DR step 7 L308: "Administrator or the deputy reopens; the promotion stop stays set". G2 L458. |
| C-10 | landed | Synthetic identities L227: "admitted only through a standing, Administrator-granted synthetic-admission group limited to the synthetic tenant; the gateway never treats a synthetic flag as admission". G1 L275: "the human production-admission group is empty ... After the G1 deployment and before G2, Administrator may temporarily add one designated synthetic test identity ... this does not open G2". Ingress stays closed under G1's own condition. |
| C-11 | partial | L268 carries the fix text (Administrator record replaces precondition 8, fresh-install rehearsal, "remove workloads, keep data", manual change as an approved attempt). L431 moves record encoding to First staging deployment. **Gap:** the finding's stop-clearing deadlock remains, see RR-2. |
| C-12 | landed | Production preconditions L286-296 are a numbered checklist 1-9 that points to each home. |
| C-13 | partial | AD-6 L116-117 lands the distinct identity-administration principals, "Realm clients disable full scope", the agent-token ban on realm-management roles and admin audience, the declared Administrator path, MFA, sealed break-glass and no `offline_access`. **Gap:** nothing bounds the lifetime of hosted refresh material or states "never in a profile file", and explicit audience and role mapping is not stated. See RR-10. |
| C-14 | landed | AD-8 L135: "The environment-layer identity writes the data namespace plus `components.dapr.io` objects and the named bootstrap Secrets in its application namespace. The application deploy identity holds none of these". Secrets L222: "(seed: `vaultTokenMountPath` with `dapr.io/volume-mounts`)". L450: per-app token split. |
| C-15 | landed | AD-15 *Expand-only* L209 lists realm-contract items, Components, secret-contract entries and the Memories operator artifact. Lock list L269: "realm-contract or operator-artifact change". |
| C-16 | partial | Binding classes L219: "*Qualification records* are Platform-issued and extend one release's qualified environment-layer sets for one environment". L223: "issues a qualification record ... effective qualified sets (release record plus qualification records)". AD-2 L77 binds "Dapr sidecar patch". **Gap:** "which workloads pin" and the rule "an environment-layer change is breaking for any retained rollback target or recovery-point release it would put outside the defined sidecar skew" did not land (no "skew" anywhere in the spine). Only the production attempt issues a qualification record, so a staging environment-layer change leaves staging's releases unqualified. The patch-equality rule conflicts with the immutable pin. See RR-1. |
| C-17 | landed | Release tiers table L231-238, including shared-tier membership, "Named shared-infrastructure workflow under both locks after a complete recovery point; from G1 rehearsed first in the monthly drill environment on prepared capacity, except an urgent security patch ...", and "a failed verification is non-working, and a failed currency check blocks automatic promotion". L240: Keycloak production-critical. L467: residual risks and the in-place Kubernetes-minor outage. |
| C-18 | landed (as diverged) | Hosted interfaces L226: "Application routes may set only host, path, TLS and backend fields; controller-configuration injection is rejected at admission. Shared-infrastructure and production-trust names are reserved in the FQDN pattern and rejected at admission for staging, and the production realm issuer uses a production-controlled name. Staging holds no DNS-01 credential and obtains certificates only through HTTP-01". L224 keeps `registry.hexalith.com`. |
| C-19 | partial | AD-12 L180: manual unseal within declared coverage. Diagnostics L228: "seal-state, volume-headroom and expiry checks". L223: a named renewal owner for every time-bound item. L456: "monitor lead times". L467: accepted sealed-after-restart risk. **Gap:** the monitor rule does not say it warns a declared lead time before each expiry and at every drill. See RR-12. |
| C-20 | landed | AD-7 *Module-code sandbox* L126. AD-8 L135: "it is secret-equivalent for its namespace, so it is per-job and never co-resident with module code". |
| C-21 | landed | L219: "*Administrator records* are signed by an Administrator-held identity that no executor, workflow token, monitor or deputy holds; storage write access never authenticates a record". AD-7 L127: read-only permissions, no PAT, App or deploy key, signature verified. L228: "private notification repository, raised through an issues-only credential". |
| C-22 | landed | Workflows L224 describes the ruleset. L440 gates it at First publication. L467 reframes the accepted risk to "repository-level rulesets only, named publication-repository writers with Administrator-only bypass". |
| C-23 | partial (as reframed) | AD-1 L66: "Gateway API routes". L226: "Each environment's Gateway, Gateway API on Traefik, lists only that environment's hostnames and admits routes only from its application namespace". Tiers L236-237. L450: "Gateway API CRDs and the Traefik Gateway provider". L456: Traefik, Calico, Zot, Velero and patch currency. **Gap:** the reframed decision's clause is absent: "existing shared Ingresses (Keycloak, registries) stay on the nginx-public compatibility class until migrated as shared-infrastructure changes; Platform never renders routes on the compatibility class". The spine mentions `nginx-public` only in the Structural Seed note at L391. See RR-7. |
| C-24 | partial | DR step 2 L303 lands environment-layer reproduction, the Keycloak restore by Administrator or the deputy (DR-scoped, staging realm disabled), OpenBao into quarantine, deployment from the replica by digest with ingress closed and workers disabled, and "Recovery hooks may assume exactly this state". Step 4 L305 covers the broker and step 7 L308 the DNS cutover. **Gap:** the decided "restore Keycloak ... into quarantine" and "under custody" are missing, so restored Keycloak is not limited to the recovery executor before step 5 rotation and revocation replay. See RR-11. |
| C-25 | landed | Step 1 L302 revokes at "every authority that survives the failure — off-site backup store, tombstone mirror, external providers, Keycloak event-export sink, registry, record store and tenant-key store — and prove they fail there. Keep the old node isolated". Step 5 L306: realm signing keys rotated "unless the old issuer is proven unavailable" and the proof against restored instances. Lost window L281. Risks L467. |
| C-26 | contradicted | The hook contract (L429) and step 4 (L305) carry "backend-principal and dynamic-credential re-provisioning" before "rebuild-only replay". Step 5 L306 then says "Rotate every restored credential and application signing key, and module-owned dynamic namespaces only through the owning module's hook". That places dynamic-namespace rotation after step 4's rebuild-only replay, the "rebuild before rotate" order C-26 set out to remove, against the memlog's "before rebuild-only replay". See RR-3. |
| C-27 | landed | L457: "Move the privileged Forgejo runner to a separate machine or VM not shared with any executor; earlier if staging holds real data". AD-7 L124. |
| C-28 | landed | L425: attestation row gated "First asynchronous or confirmed cross-module step; FR-12 acceptance". L316: "Requested from EventStore: the versioned extension API; ... admission-time original-actor and originating-surface attestation extending EventStore AD-29". |
| C-29 | landed | AD-11 L168: single-source profile, OS credential store, actor refusal. L318: McpCli AD-10, AD-13, AD-14 and AD-16 overrides with "no new `Parties.Aspire`". L322: Projects AD-30 override. L317: Memories deploy assets are conformance inputs only and staging applies. L445: test AppHost migration. |
| C-30 | landed | L216: "each with exposure class (public, internal-only, disabled) and required surface class and authorization". L226: "Platform maps declared public-class interfaces ... internal-only and disabled interfaces are neither routed nor advertised". L32: "the servers its declaration's enabled server list specifies". |
| C-31 | landed | AD-4 L100: "Every workspace that runs the tool declares `references/Hexalith.Platform` directly ... In CI the Platform composition the tool loads carries its source commit, and the tool refuses to run unless it equals the submodule HEAD". L442: "(McpCli keeps its reference)". |
| C-32 | landed | L170 names `Hexalith.EventStore.Admin.Cli` and `.Admin.Mcp`. L171: new surfaces need a Platform AD with "surface class, Keycloak client, exposure and realm entries", and "EventStore Admin.Server and Admin.UI remain a confidential UI surface, and confirmation-required admin operations move to a confidential UI or are withdrawn". L433. |
| C-33 | landed | L170: full retirement evidence including "compatibility and acceptance evidence, and any applicable EventStore AD-22". L229: "Legacy MCP/CLI packages and routes retire under AD-11". The Deferred row is split into L446 and L462, and L462 says "each needs an AD admitting it under AD-14, and forwarded headers never establish the actor". |
| C-34 | landed | Promotion stop L274: "a failed pre-update health check, a probe failure beyond a declared bound, a recorded incident, or an Administrator or deputy declaration. The off-site monitor may set it but never clear it, and setting it never triggers a release search". |
| C-35 | landed | "Binding classes and records" L219 defines each record kind, its writer and the CAS store. Rollout L271: "before the Helm upgrade; a non-working terminal outcome invalidates it under EventStore's invalidation rule". Diagram L260. L454 and L316 cover the EventStore confirmation. |
| C-36 | landed | Timing L270: "t0 at its first mutation (catalog preparation), the rollout deadline t0 + 10 minutes, and one attempt grace no longer than the check timeout plus 30 seconds ... A retry completing within the grace is the check's latest result". L269 covers takeover with epoch increment and "An executor dying during verification leaves the lock held". L219 keeps lock and epoch with the records. |
| C-37 | partial (as narrowed) | Staging gate L266: "Each staging attempt first cuts a staging recovery point. ... After a candidate that production does not adopt and whose writes the baseline cannot read, staging is restored from the recovery point cut at the start of that candidate's attempt, as its own staging attempt, before the next gate." **Gap:** "Staging catalog and key retirements follow AD-15 relative to production's working baseline" did not land. See RR-8. |
| C-38 | landed | Release modes L267: "An incompatible release's record names, before the attempt, its separately planned recovery and acceptance checks. That attempt starts only after a complete recovery point cut after the lock; on a non-working outcome the executor sets the promotion stop, closes user ingress and stops; the named recovery runs as a DR entry under a new epoch". |
| C-39 | landed | AD-15 L208: "every idempotency and key entry of both the baseline and candidate generations, fixed at preparation ... including candidate-only ones, are non-executable retention entries". AD-3 L88: "with environment-current values and the prepared rollback generation ... commits that generation at readiness". |
| C-40 | landed | Module intake L218: "**Breaking** has one definition". AD-3 L90 and AD-15 L210 point to it with "(breaking per Module intake)". |
| C-41 | landed | Diagnostics L228: per-environment sinks, Platform-rendered exporter and egress, "outside the primary failure domain with a declared minimum retention". L451 gates the sink at First staging deployment, with retention before G2. |
| C-42 | landed | AD-8 L138: "WorkflowAccessPolicies match by app ID within the single-namespace workflow scope, and every workflow-hosting app has at least one scoped policy". L139: "including a staging app ID equal to a production one". L467: `v1alpha1` risk. |
| C-43 | partial | L223: template pins the "exact Dapr patch", and "The hosted sidecar patch equals the control-plane patch and the release's pinned patch in CI, staging and production; local runs report their patch and warn on mismatch". AD-8 L138: HotReload disabled. L448: "HotReload off" in qualification. Stack L339. **Gap:** HotReload behavior is not added to the rollback rehearsal (Staging gate L266). The equality rule also collides with C-16, see RR-1 and RR-13. |
| C-44 | landed | AD-1 L66: "external connection parameters carrying only non-secret endpoints ... the rendered chart holds no credential-bearing Secret or value". L448. |
| C-45 | landed | AD-8 L139 binds the full matrix, including secrets, identity administration, automation targets, restored copies, PV binding and "revoked principals after every admission change". L135: "PersistentVolumes are created only by the shared-infrastructure identity and pre-bound". |
| C-46 | landed | L422: ratification row. L447 (First staging deployment): "Build the composed host with zero or more extensions, register its lifecycle subject and obtain EventStore's ratification; package Folders adapters before Folders joins". AD-13 no longer contains the one-time step. The L316 "Requested" list is a pointer, not a duplicate rule. |
| C-47 | landed | Workflows L224 is the single provenance home: predicate, module-image attestations recorded in the intake manifest, full `uses:` closure pinned, "publication credentials cannot delete or overwrite retained artifacts", separate replica writer. L453 (First staging promotion). |
| C-48 | partial | L224: "`registry.hexalith.com` with anonymous read disabled and per-environment pull credentials". L76 and L187: `platform/eventstore`. AD-5 L106: read-only pull credential. L456: Zot. **Gap:** "registry.tache.ai is not a retained-artifact store" is absent. See RR-14. |
| C-49 | landed | Detection and response L278 carries the fix wording verbatim. |
| C-50 | landed | L219 and L224: "or the recovery executor for DR". Step 7 L308 re-provisions the executor allowlist and credentials. After DR L282 covers the degraded posture and "the promotion stop stays set until staging is re-established". |
| C-51 | partial | AD-7 L128: "standing credentials only for the prepared capacity plus read-only access to recovery points. Custodians release key and decryption material per job, destroyed at job end; its credentials rotate after every drill and DR". L279: drill and quarantine copies destroyed within a bound. **Gap:** "Restored artifacts are provenance-verified, with attestations replicated" did not land. Step 2 L303 deploys from the replica "by digest" only. See RR-9. |
| C-52 | landed | L277: "a tenant-key backup covering every key generation referenced at or before the cut, Keycloak event-export lag within its bound ... The monitor verifies tenant-key coverage without holding key material". L276: export bound. |
| C-53 | landed | L279: detection via the probe-to-issue latency, timed substitutions, "an unbounded substitution fails the RTO proof", timed rebuild-only replay wait, "A real DR entry aborts any running drill". L216 and L429 declare the reopen-before-replay choice. |
| C-54 | landed | L455 (G1): "remove Keycloak `/admin` and master-realm routes and the public cluster console, verified by an external negative probe". |
| C-55 | landed | The AD-11 heading is [ADOPTED, AMENDED]. L164 Prevents: "proprietary module MCP/CLI surfaces". L405: "legacy MCP/CLI retirement". Sources L15-17. `status: draft` at L8. L318: "until the upstream amendment merges, the committed McpCli PRD's EventStore Admin exclusion". |
| C-56 | landed | Terms L36. The capability map (L395-408) names conventions. Rules use labelled sub-bullets. The DR sequence is a numbered list (L298-308). Attempt diagram L246-262. The three rationale fragments ("Because `azp`", "`--atomic`", "co-scheduling") are gone. Foreign ADs are prefixed. |
| C-57 | landed | L229 absorbs the hosting-helper refusal. L461 holds the GitHub Team trigger. AD-7 duplicates are removed. AD-3 is the single recovery-render home. Accepted risks is the sole home for single-node and whole-site risk. The empty "Module service-level" row is deleted. AD-15 defines the set and generation. Memories digest sets keep a pointer in the L317 source row (acceptable). |
| C-58 | landed | L426: requester in the subject audience, per-client switch, refresh-token setting. L341: Keycloak observed 26.7.4. |
| C-59 | landed | L227: identifier "published in the environment descriptor and realm-contract instance and attested by EventStore on admitted events; modules exclude ... by that identifier". Also AD-6 L113 and AD-10 L157. |
| C-60 | landed | AD-8 L138: "Application-level caller allowlists key on namespace and app ID." |
| C-61 | landed | L228: "An hourly dead-man workflow in the notification repository, on an off-hour minute ... and the monitor alerts when the dead-man run is stale." |
| C-62 | landed | Stack header L327: "Seed from commit b9410d3 and the Builds catalog at 0610f78". L332-339 carry the rows, including Helm "4.2.0 floor | Exact version in the profile inventory, equal on every executor" and Keycloak "transitive via EventStore.Aspire". L442 covers the HS256 removal and the README floor. |
| C-63 | deferred-ok | L460 (Maintenance): "re-sync `specs/spec-platform/SPEC.md` and addendum wording after this update". L420: "once published, the Module declaration row links to it instead of enumerating fields". |
| C-64 | ignored-ok | No change required. L442 already removes file-existence selectors, and L441 ratifies the local ACL convention. |

## 2. Memlog decision entries not fully reflected

All nine decision entries (batches 1-6 and the batch-6 autofixes) and the three version or event entries were checked. Most content landed, including every batch-1 and batch-2 option, the C-18 divergence, the C-23 reframing to Gateway API on Traefik, the C-37 narrowing, the C-17 monthly-drill venue, the C-43 local warning, the C-63 defer and the C-64 ignore. The following decision content is missing or altered:

| Memlog entry | Decision content not reflected | Finding |
| --- | --- | --- |
| Batch 2 (C-13) | "hosted refresh material bounded" | RR-10 |
| Batch 3 (C-19) | "the monitor warns a declared lead time before expiry and at every drill" | RR-12 |
| Batch 3 (C-24) | "restore Keycloak and the environment OpenBao into quarantine"; Keycloak restore "under custody" | RR-11 |
| Batch 3 (C-26) | "module dynamic namespaces rotate only through the owning module's hook after admission/purge and before rebuild-only replay" (step order contradicts) | RR-3 |
| Batch 3 (C-51) | "provenance-verified restores" | RR-9 |
| Batch 4 (C-37) | "staging catalog and key retirements follow AD-15 relative to production's working baseline" | RR-8 |
| Batch 5 (C-16) | "an environment-layer attempt's re-verification ... for that environment" (the spine issues it only in production); "each release record binds the Dapr sidecar patch, which workloads pin; an environment-layer change is breaking for any retained rollback target or recovery-point release it would put outside the defined sidecar skew" | RR-1 |
| Batch 5 (C-43) | "HotReload behavior added to ... rollback rehearsal" | RR-13 |
| Batch 6 (C-23) | "existing shared Ingresses (Keycloak, registries) stay on the nginx-public compatibility class until migrated as shared-infrastructure changes; Platform never renders routes on the compatibility class" | RR-7 |
| Batch 6 (C-48) | "registry.tache.ai is not a retained-artifact store" | RR-14 |
| Batch 1 (C-08) | EventStore AD-24 `openbao` as a logical role is "(owner confirmation)", but the EventStore row lists it under "Retained" rather than "Requested" | RR-20 |
| Distillation event | "AD-11 and AD-14 amended in place, AD-1/AD-3/AD-6/AD-7/AD-8/AD-12/AD-15 tightened". AD-2 (C-04 McpCli hash, C-16 sidecar patch, C-48 `platform/eventstore`) and AD-5 (C-48 read-only pull credential) changed substantively but stay [ADOPTED] | RR-20 |

The C-10 clause "ingress stays executor/probe-only" is not restated in the SM-4 sentence, but G1's own condition carries it. No action is needed.

## 3. Regression check (HEAD final spine to new spine)

Moved text was traced to its new home and is not listed. Changes authorized by a memlog decision are not listed either. Examples: AD-9's option-default names (C-08), AD-4's composition-carried commit (C-31), local Dapr patch warnings (C-43), bootstrap Secrets moving to the application namespace (C-14), HS256 to owned work (C-62), MFA and break-glass from Deferred into AD-6 (C-13), and deletion of the "Module service-level" row (C-57). The following binding content disappeared or was weakened with no authorizing decision:

| # | Lost or weakened clause (HEAD) | Where it went | Finding |
| --- | --- | --- | --- |
| G-1 | Owned row "Runner, ownership records and resource isolation": "Prove active-root mapping, finite readiness, exact cleanup, retained-run listing and CI package parity. Reconcile fixed Dapr ports and volumes, dapr-init Redis, the fixture lock, fixed-port Keycloak and the shared certificate directory. No custom DSL or environment service." | Nowhere. The L439 Platform tool ratification row covers schema, AppHost, identity check and CI tier, not these proofs. Memlog L115 and V-66 still depend on it. | RR-4 |
| G-2 | AD-2: "Staging evidence and effective change classifications name the working baseline they were computed against." | Nowhere. Precondition 9 (L296) still routes "wrong-baseline evidence", which now has no field to check. | RR-5 |
| G-3 | Catalogs activation: "... Helm upgrade, candidate hosts validating their prepared generation; commit at readiness" | Replaced by "Activation follows the production attempt diagram" (L220). The diagram (L250-254) omits candidate-host validation and covers only production. | RR-6 |
| G-4 | AD-7: the production executor runs "digest-identified smoke suites that passed staging" (memlog V-44: immutable digest-identified suites executed without source builds) | Dropped with the sandbox rewrite (L124-126). The AD-2 record still binds check-suite digests, but nothing requires production to run exactly those. | RR-15 |
| G-5 | AD-1: "legacy deployments are never a second writer" (memlog L176) | Narrowed to "module deploy assets ... never a second writer" (L68). Existing module-deployed resources on the cluster lose the rule. | RR-16 |
| G-6 | AD-6: "master realm and admin consoles stay off public ingress" (memlog L184, V-series) | Narrowed to "The admin API and master realm" (L116). Cluster consoles keep only a one-time G1 removal (L455), not a standing invariant. | RR-17 |
| G-7 | Binding classes: "readiness compares attempt-bound digests" | Dropped. C-12 authorized removing only duplicated production preconditions. | RR-18 |
| G-8 | Synthetic identities: synthetic actor and workload clients "flagged in their tokens" | Dropped, although L227 still refers to "a synthetic flag". | RR-19 |
| G-9 | Attempt ownership: a replacement job may take over only "to perform its remaining single recovery within the grace" | Broadened to "resuming the same attempt within the grace" (L269). | RR-19 |
| G-10 | Stack: Aspire CLI "Must equal the AppHost SDK, checked by the Platform tool" | "Equals the AppHost SDK" (L333). The enforcement owner is lost. | RR-19 |
| G-11 | AD-3: the environment layer is "versioned in the private operations repository" | Dropped. The tier table names a version authority but no home. | RR-19 |
| G-12 | Roles: Administrator is "recovery owner" | Dropped from L34. C-09 added the deputy's execution rights but did not remove Administrator's ownership, and L279 still has Administrator owning drills. | RR-19 |
| G-13 | Hosted interfaces: staging can neither serve shared or production hostnames "nor obtain their certificates" | Only the serving half survives, through admission and HTTP-01 (L226). The certificate half is not explicit while `nginx-public` stays shared. | RR-7 |

## Findings

### RR-1: Dapr sidecar patch equality has no compliant update path (C-16/C-43)

- **Severity:** high. **Disposition:** discuss.
- **Location:** Production profile L223, AD-2 L77, Binding classes L219.
- **Problem:** The release record, which is immutable, binds the Dapr sidecar patch. L223 then requires "The hosted sidecar patch equals the control-plane patch and the release's pinned patch in CI, staging and production". A control-plane patch is a shared-infrastructure change that the currency rule requires ("current on security patches"). Once applied, the working release and every retained rollback target violate the equality, and a qualification record extends only "qualified environment-layer sets", not the pinned patch.
  - C-16's "defined sidecar skew" breaking rule and "which workloads pin" never landed.
  - C-16 also decided qualification records "for that environment", but L223 issues one only in the production attempt, so staging's releases fall outside their sets after a staging environment-layer change.
- **Options:**
  - **(A, recommended; mirrors the C-16 decision).** Replace the L223 sentence with: "Workloads pin the release's Dapr sidecar patch. The defined sidecar skew is: within the template's Dapr minor, the hosted sidecar patch equals the control-plane patch, or equals the release's pinned patch while a qualification record for that environment names the current control-plane patch; CI and staging otherwise run the release's pinned patch. An environment-layer or shared change is breaking for any retained rollback target or recovery-point release it would put outside that skew. Local runs report their patch and warn on mismatch."
    - Also change L223 "the production attempt re-verifies the working release and issues a qualification record" to "each environment's attempt re-verifies that environment's working release and issues a qualification record for that environment; the production attempt also issues a renewed production-promoted record at the new digest".
  - **(B).** Keep strict equality. Every Dapr patch ships with a new application release pinning it, applied in the same shared-infrastructure attempt. Retained releases pinning the old patch become breaking rollback targets and are replaced by re-published releases before the change.

### RR-2: No path out of a set promotion stop when production has no working release (C-11 residual)

- **Severity:** high. **Disposition:** autofix.
- **Location:** Empty or degraded production L268, Promotion stop L274, precondition 1 L288.
- **Problem:** After a failed first deployment, or a failed recovery ("Stop for intervention; promotion stop set"), the stop is set. L274 clears it only with "a verified current working release", which does not exist. Precondition 1 requires the stop to be clear before any attempt, and L268 lets the Administrator record replace only precondition 8. The C-11 finding named this deadlock, and it persists.
- **Fix:**
  - L268: change "an Administrator record may replace precondition 8" to "an Administrator record may replace precondition 8 and, for that one attempt, the clear-stop part of precondition 1; the promotion stop clears only when that attempt is recorded working".
  - L274: append ", except as Empty or degraded production allows".

### RR-3: DR rotation order puts module dynamic credentials after replay (C-26 contradicted)

- **Severity:** medium. **Disposition:** autofix.
- **Location:** DR sequence steps 4-5, L305-306.
- **Problem:** Step 5 rotates "module-owned dynamic namespaces only through the owning module's hook" after step 4 has already run rebuild-only replay. The decision puts rotation after admission and purge and before rebuild-only replay.
- **Fix:**
  - Step 4: "... admission and purge, backend-principal and dynamic-credential re-provisioning and rotation, then rebuild-only replay."
  - Step 5 opening: "Rotate every restored credential and application signing key except module-owned dynamic namespaces, which only the owning module's hook rotates in step 4."

### RR-4: The runner proof obligations were dropped (regression G-1)

- **Severity:** medium. **Disposition:** autofix.
- **Location:** Owned work L437-463.
- **Fix:** Add the row "First tool publication | Runner ownership and resource isolation | Builds with Platform and module owners | Prove active-root mapping, finite readiness, exact cleanup, retained-run listing and CI package parity; reconcile fixed Dapr ports and volumes, dapr-init Redis, the fixture lock, fixed-port Keycloak and the shared certificate directory; no custom DSL or environment service."

### RR-5: Evidence no longer names its baseline (regression G-2)

- **Severity:** medium. **Disposition:** autofix.
- **Location:** Staging gate L266, Module intake L218.
- **Fix:**
  - L266: change "Results carry the serving release and the suite, profile and configuration digests" to "Results carry the serving release, the production working baseline they were computed against, and the suite, profile and configuration digests".
  - L218: append "and names the working baseline it was computed against".

### RR-6: Candidate-host catalog validation dropped (regression G-3)

- **Severity:** medium. **Disposition:** autofix.
- **Location:** Catalogs L220, diagram L253.
- **Fix:**
  - L220: replace "Activation follows the production attempt diagram" with "Activation, in every environment: prepare the candidate and rollback generations; ready-validate the rollback generation on the running baseline hosts; Helm upgrade, with candidate hosts validating their prepared generation; commit at readiness; verify (production attempt diagram)".
  - Diagram node `Upgrade`: "Helm upgrade retained package by digest;<br/>candidate hosts validate prepared generation".

### RR-7: The compatibility-class rule is missing (C-23 partial, G-13)

- **Severity:** medium. **Disposition:** autofix.
- **Location:** Hosted interfaces L226.
- **Fix:** Append: "Platform never renders routes or Ingresses on the shared `nginx-public` compatibility class, and application namespaces may not create objects on it; existing shared Ingresses (Keycloak, registries) stay on it only until migrated to Gateway API as shared-infrastructure changes. Staging can neither serve reserved names nor obtain their certificates."

### RR-8: Staging retirements are not tied to the production baseline (C-37 partial)

- **Severity:** medium. **Disposition:** autofix.
- **Location:** Staging gate L266.
- **Fix:** Append: "Staging catalog, key and secret retirements follow AD-15 relative to production's working baseline, not staging's latest candidate; additive unadopted candidates need no reset."

### RR-9: Restores are not provenance-verified (C-51 partial)

- **Severity:** medium. **Disposition:** autofix.
- **Location:** AD-7 *Recovery executor* L128, DR step 2 L303, Workflows L224.
- **Fix:**
  - L128: append "Restored artifacts and records are provenance-verified against attestations replicated with the off-site registry replica."
  - L303: change "Deploy the recovery point's release from the off-site registry replica by digest" to "... by digest after verifying its replicated provenance".

### RR-10: Hosted refresh material is unbounded (C-13 partial)

- **Severity:** medium. **Disposition:** autofix.
- **Location:** AD-6 *Authentication* L117 (or AD-11 L168).
- **Fix:**
  - Replace "Public clients never receive `offline_access`." with "Public clients never receive `offline_access`; hosted refresh material has a bounded idle and maximum lifetime set in the realm contract and lives only in the OS credential store, never in a profile file."
  - L116: change "Realm clients disable full scope" to "Realm clients disable full scope and map audiences and roles explicitly".

### RR-11: Keycloak restore is not quarantined (C-24 partial)

- **Severity:** low. **Disposition:** autofix.
- **Location:** DR step 2 L303.
- **Fix:** Change "Administrator or the deputy restores the Keycloak database, DR-scoped, with the staging realm disabled" to "Administrator or the deputy restores the Keycloak database into quarantine under custody, DR-scoped, with the staging realm disabled".

### RR-12: Expiry warnings lack lead time and drill reporting (C-19 partial)

- **Severity:** low. **Disposition:** autofix.
- **Location:** Diagnostics L228.
- **Fix:** Change "seal-state, volume-headroom and expiry checks" to "seal-state, volume-headroom and expiry checks, warning a declared lead time before each expiry and listing upcoming expiries at every drill".

### RR-13: HotReload is not in the rollback rehearsal (C-43 partial)

- **Severity:** low. **Disposition:** autofix.
- **Location:** Staging gate L266.
- **Fix:** Change "then rehearses the AD-15 rollback set" to "then rehearses the AD-15 rollback set with HotReload off, confirming that Dapr resource changes and their reversal take effect only through pod rollout".

### RR-14: registry.tache.ai is not excluded (C-48 partial)

- **Severity:** low. **Disposition:** autofix.
- **Location:** Workflows L224.
- **Fix:** After "publication credentials cannot delete or overwrite retained artifacts", add "`registry.tache.ai` is not a retained-artifact store."

### RR-15: Production smoke suites are no longer digest-bound (regression G-4)

- **Severity:** medium. **Disposition:** autofix.
- **Location:** Verification L272 (or AD-7 L126).
- **Fix:** Prefix L272 with: "Production runs only the immutable check-suite digests bound in the release record that passed staging, without source builds, in the module-code sandbox."

### RR-16: Legacy deployments lost the second-writer ban (regression G-5)

- **Severity:** low. **Disposition:** autofix.
- **Location:** AD-1 *Single definition* L68.
- **Fix:** Change "module deploy assets are declaration or conformance inputs, never a second writer" to "module deploy assets are declaration or conformance inputs, and neither they nor legacy deployments are ever a second writer".

### RR-17: Admin consoles lost the standing public-ingress ban (regression G-6)

- **Severity:** low. **Disposition:** autofix.
- **Location:** AD-6 *Administration* L116.
- **Fix:** Change "The admin API and master realm are reachable only from a declared Administrator path" to "The Keycloak admin API and console, the master realm and cluster consoles are reachable only from a declared Administrator path".

### RR-18: Readiness no longer compares attempt-bound digests (regression G-7)

- **Severity:** low. **Disposition:** autofix.
- **Location:** Binding classes and records L219.
- **Fix:** After the attempt-bound sentence, add "Readiness compares attempt-bound digests."

### RR-19: Minor lost clauses (regressions G-8 to G-12)

- **Severity:** low. **Disposition:** autofix.
- **Fixes:**
  - L227: "synthetic actor and workload clients are flagged in their tokens and carry no admin or cross-tenant grant".
  - L269: "resuming the same attempt within the grace to perform its remaining single recovery".
  - L333: "Equals the AppHost SDK, checked by the Platform tool".
  - Release tiers L236 change path: "Its own attempt, staging first, versioned in the private operations repository and applied forward-only by the environment-layer identity".
  - L34: "**Administrator** is the production authority, recovery owner and Platform architecture owner".

### RR-20: Consistency residue (C-02, C-08, AD tags)

- **Severity:** low. **Disposition:** autofix.
- **Fixes:**
  - L420: replace "adopt or supersede Builds `hexalith.module-manifest.v1`" with "Builds `hexalith.module-manifest.v1`, ratified as the declaration schema implementation (Terms)". C-02 chose ratification.
  - L316: move "fixed component names such as `openbao` become logical roles that Platform binds" from "Retained" to "Requested from EventStore".
  - Tag AD-2 and AD-5 [ADOPTED, AMENDED]. Both changed in this run (C-04, C-16, C-48).
