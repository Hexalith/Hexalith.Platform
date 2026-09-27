# Confirmation review — revised spine vs update-run gate findings

- **Subject:** `ARCHITECTURE-SPINE.md` (revised, 328 lines, status draft)
- **Authority:** `.memlog.md` from "(event) Update run started" to the end, including the Gate decision entries (RV-1, RV-7/R-10, ADV-U8), the Gate bundle (R-08, ADV-U21, RV-8, RV-3) and the "Gate fixes applied" event. Memlog decisions override reviewer recommendations.
- **Findings checked:** `review-reconcile.md` (13 partial, 3 dropped, 2 invented), `review-rubric.md` (R-01..R-29), `review-pragmatism.md` (PR-01..PR-38), `review-reality.md` (RV-1..RV-14), `review-adversarial.md` (ADV-U1..U30). Total 129.
- **Method:** read-only, except for writing this file. Each finding was checked against the current spine text.

## Status legend

| Status | Meaning |
| --- | --- |
| ADDRESSED | The substantive defect is fixed in the spine, or it is resolved per a memlog gate decision. "(partial)" marks cosmetic residue only; the residue is noted and does not affect consistency. |
| DEFERRED-OK | Moved to an owned row in Deferred and Qualification Work. |
| DECLINED-OK | The memlog records a decline, or the reviewer's disposition was keep/ignore. |
| NOT ADDRESSED | A substantive element that the reviewer or a memlog decision required is absent. |

## Counts

| Review | Items | ADDRESSED | of which partial | DEFERRED-OK | DECLINED-OK | NOT ADDRESSED |
| --- | --- | --- | --- | --- | --- | --- |
| Reconcile | 18 | 18 | 3 | 0 | 0 | 0 |
| Rubric | 29 | 29 | 1 | 0 | 0 | 0 |
| Pragmatism | 38 | 34 | 5 | 0 | 3 | 1 |
| Reality | 14 | 11 | 1 | 2 | 0 | 1 |
| Adversarial | 30 | 30 | 1 | 0 | 0 | 0 |
| **Total** | **129** | **122** | **11** | **2** | **3** | **2** |

There are also 12 new internal contradictions or dangling references introduced by the rewrite. None is critical. Four are medium: C-1, C-2, C-3 and C-4. See the section after the tables.

## Reconcile (partials, dropped, invented)

| ID | Finding | Status | Evidence / note |
| --- | --- | --- | --- |
| V-03 | HS256 retirement missing; "claim names and semantics" | ADDRESSED (partial) | AD-6: "The root HS256 development key is retired from Platform compositions." Residue: it still says "claim names", not "claim names and semantics". |
| V-05 | Projects AD-30 carve-out; G-1 outside MVP; NeedsAttention | ADDRESSED (partial) | Source Precedence: "Projects AD-30 release gates may not demand Platform HA or RPO-0 evidence"; "G-1 durable task engine, outside the MVP". Residue: the Projects row still omits "NeedsAttention". |
| V-09 | Change classification and rehearsal evidence bound in the release record | ADDRESSED | AD-2 list includes "effective change classifications … rehearsal evidence". This placement creates a new contradiction; see C-1. |
| V-15 | Rollback target valid only for the active profile digest; deployment-owner issuer | ADDRESSED | Production profile: "deployable, and a valid rollback target, only while … its record is valid for the active profile digest". AD-13 registers Platform as composed-subject issuer. |
| V-17 | Full creation-task rule; population/authority tasks operator-only | ADDRESSED | Startup task lifecycle row. |
| V-22 | Cross-environment invocation negative test | ADDRESSED | AD-8 negatives cover app ports, sidecars, workflows and actors. |
| V-23 | Name the custodians | ADDRESSED | Secrets row: "Administrator and the recovery deputy are the unseal and recovery-key custodians." |
| V-24 | Negatives run from a staging pod | ADDRESSED | AD-8: "Negative tests from staging users, credentials and pods". |
| V-25 | Staging declaring a production host is a negative test | ADDRESSED (partial) | The Hosted interfaces admission rule is present, and AD-8's negatives list "hostnames". Residue: the declare/claim test is implicit, not named. |
| V-31 | Independent watchdog outside the executor and cluster | ADDRESSED | Attempt ownership: "The off-site monitor notifies when a record stays non-terminal". |
| V-40 | Split frozen domain hosts from valid technical-module test roots | ADDRESSED | Hosting inventory row lists domain modules only; technical-module hosts "stay valid for their own tests". |
| V-56 | Records/evidence only from the deployment identity | ADDRESSED | AD-2: "records and evidence only from the environment's executor identity". The wording conflicts with the publication workflow writing the release record; see C-1. |
| V-61 | Pin the Aspire CLI | ADDRESSED | Stack: "Must equal the AppHost SDK, checked by the Platform tool". |
| D-1 | Notification channel and content | ADDRESSED | Diagnostics: GitHub issues are the single path; notifications carry environment, release and opaque references. Automatic recovery reports every outcome including success. |
| D-2 | No environment fallback; offline inspection vs availability | ADDRESSED | AD-11: "Offline contract inspection is distinct from availability"; "no environment fallback or implicit module enablement". |
| D-3 | Folders uses the common profile | ADDRESSED | Folders row: "no Folders-only state-store version or topology gate applies". |
| I-1 | Separate toolchain pins from environment-layer pins | ADDRESSED | Production profile: environment layer "and, separately, the deploy toolchain". |
| I-2 | "read models" should be "read-model journals" | ADDRESSED | AD-9 uses "read-model journals". |

## Rubric

| ID | Finding | Status | Evidence / note |
| --- | --- | --- | --- |
| R-01 | AD-13 local/source mode | ADDRESSED | AD-13: "Local and CI modes assemble the host through the AD-4 mapping". |
| R-02 | Memories digest authority | ADDRESSED | AD-3 names one version authority; the digest sets are qualification inputs; Paradigm sentence removed. |
| R-03 | Exposure, DNS, ACME, verification path | ADDRESSED | Hosted interfaces clause; Deferred row "Exposure, DNS and certificates" (Before G1). |
| R-04 | Broker selection misframed | ADDRESSED | Shared runtime row selects the durable broker; row renamed "Broker change, …". |
| R-05 | Declaration omits workers, providers, resources | ADDRESSED | Module declaration row. |
| R-06 | Recovery hook contract | ADDRESSED | First shared versions row. |
| R-07 | Environment-layer home and sequencing; dangling AD-12 reference | ADDRESSED | AD-3: private operations repository; "applied forward-only and verified before the attempt". AD-7 recovery executor and AD-12 reference now agree. |
| R-08 | Environment layer in the application namespace | ADDRESSED | Per memlog decision: AD-8 separate data namespace; PSS compatibility in the Secrets Deferred row. |
| R-09 | GitHub plan prerequisites | ADDRESSED | Per memlog RV-1 decision: AD-7 enforcement at the executor, OIDC claims, accepted risk "GitHub Free". |
| R-10 | Probe host, cadence, dead-man | ADDRESSED | Per memlog RV-7 decision: off-site monitor with a 15-minute freshness check; hourly GitHub dead-man. |
| R-11 | Technical-module AppHost tests vs AD-10 | ADDRESSED | AD-10 non-exported fixture path. |
| R-12 | Overloaded "release workflow" | ADDRESSED | Workflows row defines the publication and deployment workflows. The new "production release workflow" term regresses this; see C-10. |
| R-13 | Ratify the Zot registry | ADDRESSED | Workflows row names `registry.hexalith.com`; staging cannot serve the registry hostname. |
| R-14 | Module classification | ADDRESSED | Design Paradigm classes; AD-11 names EventStore.Admin.Mcp. |
| R-15 | ServiceDefaults decider | ADDRESSED | Deferred row: "Platform, consulting EventStore and Commons". |
| R-16 | Memories transitional Redis | ADDRESSED | AD-9. |
| R-17 | Tool/submodule identity comparison | ADDRESSED | AD-4 embedded source commit. |
| R-18 | AD-7 vs single-node residual risk | ADDRESSED | AD-7 scoping sentence. |
| R-19 | Profile digest scope | ADDRESSED | Production profile. The preimage wording clashes with Binding classes; see C-9. |
| R-20 | Helm floor and rollback flag; no regeneration | ADDRESSED | Stack, AD-3, AD-2. |
| R-21 | Stack alignment rules | ADDRESSED | CommunityToolkit and CLI rows. |
| R-22 | Qualify external AD references | ADDRESSED | "EventStore AD-22"; "EventStore's … AD-28". |
| R-23 | AD-2 list vs profile row | ADDRESSED | Binding classes restructure; the profile row no longer lists record contents. |
| R-24 | Local mTLS rule inside Deferred | ADDRESSED | "Ratify the local mTLS …"; allow-defaults moved to Production profile. |
| R-25 | Override precedence; first outcome | ADDRESSED | Local tool row; AD-10. |
| R-26 | Patch-currency checkpoint | ADDRESSED (partial) | "checked at each production attempt and monthly drill". Residue: the reviewer's optional promotion-stop trigger is not adopted. |
| R-27 | FR-12 narrowing signposted | ADDRESSED | Capability map FR-12 row. |
| R-28 | Architecture owner role | ADDRESSED | Roles paragraph; AD-9 "Administrator acceptance". |
| R-29 | Define gateway | ADDRESSED | Hosted interfaces; AD-13 "which is the gateway". |

## Pragmatism

| ID | Finding | Status | Evidence / note |
| --- | --- | --- | --- |
| PR-01 | Drop PRD threshold restatement | DECLINED-OK | Memlog: declined; PRD reconciliation required exact wording. |
| PR-02 | Single release-record list | ADDRESSED | AD-2 plus Binding classes; other rows no longer re-list the contents. |
| PR-03 | Memories.Aspire digest homes | ADDRESSED | One home (AD-3 and Production profile). |
| PR-04 | Promotion-stop scatter; watchdog becomes a monitor check | ADDRESSED | Set/clear semantics consolidated in Automatic recovery; the watchdog is the off-site monitor. |
| PR-05 | Hostname admission outcome and built-in seed | ADDRESSED | Hosted interfaces. |
| PR-06 | Integrity verified at write | ADDRESSED | Recovery point row. |
| PR-07 | Egress declaration field | ADDRESSED | "external egress destinations". |
| PR-08 | AD-12 duplicates and incomplete creation rule | **NOT ADDRESSED** | The creation rule is fixed through the Startup row. AD-12 still repeats "Data backups are immutable", "drill restores are ephemeral and egress-denied" (belongs in DR evidence) and "Warm standby is deferred …" (already in the Deferred row). |
| PR-09 | Rule inside Deferred | ADDRESSED | Same as R-24. |
| PR-10 | AD-3 sentence | ADDRESSED (partial) | Rewritten as proposed. Residue: "A workload changed when" is missing "counts as". |
| PR-11 | AD-28 referent | ADDRESSED | AD-6. |
| PR-12 | AD-10 precedence clause | ADDRESSED | "first terminal outcome alone decides". |
| PR-13 | AD-11 sentence; OS credential store | ADDRESSED | AD-11 rewritten; credential-store detail removed. |
| PR-14 | Backup-unit sentence | ADDRESSED (partial) | Rewritten. Residue: "the owner", where the proposal said "the state's owner". |
| PR-15 | DR step 4 key referent; export outcome | ADDRESSED | DR steps 4–5; AD-6 "exported off-cluster for DR replay". |
| PR-16 | Interruption row | ADDRESSED | Verbatim. |
| PR-17 | AD-1 qualification step; fallback actor | ADDRESSED | AD-1; Aspire-to-Helm row. |
| PR-18 | Provenance outcome and seed | ADDRESSED | AD-2; Workflows row seed. |
| PR-19 | "whichever is longer" | ADDRESSED | AD-2. |
| PR-20 | AD-5 vs AD-10 overlap | ADDRESSED | AD-5 verbatim. |
| PR-21 | Tool-version rule duplicate | ADDRESSED | Local tool row references AD-4. |
| PR-22 | AD-7 clauses owned elsewhere | ADDRESSED (partial) | "machine or VM"; cluster-scoped clause removed. Residue: "Routine build and integration jobs stay on disposable hosted runners" still duplicates AD-5. |
| PR-23 | Notification path stated five times | ADDRESSED | Diagnostics is the single home; telemetry retention moved to Deferred. |
| PR-24 | "MCP hosts not deployed" stated four times | ADDRESSED (partial) | Memories and Parties clauses removed. Residue: the last Deferred row still restates the rule. |
| PR-25 | "Gates never waived" and "overrides settled" | ADDRESSED (partial) | Migration sentence and Deferred intro removed. Residue: the "Module service-level and functional qualification" row remains and assigns no work. |
| PR-26 | Tenants stated three times | ADDRESSED | |
| PR-27 | Merge lost-window and external-effects rows | ADDRESSED | |
| PR-28 | Source Precedence rows | ADDRESSED | Memories and McpCli rows verbatim; EventStore trimmed. |
| PR-29 | Structural Seed paragraph | ADDRESSED | |
| PR-30 | Toolchain vs environment-layer pins | ADDRESSED | |
| PR-31 | Secrets "its" referent | ADDRESSED | |
| PR-32 | "prepare/ready-validate" | ADDRESSED | |
| PR-33 | G2 qualifier | ADDRESSED | |
| PR-34 | "off the hour", "warns early" | ADDRESSED | |
| PR-35 | Currency rule location | ADDRESSED | Moved to Production profile. |
| PR-36 | Schema-origin note | ADDRESSED | Moved to First shared versions. |
| PR-37 | Tenant-key custody | DECLINED-OK | Reviewer: keep. |
| PR-38 | Full rotation on DR | DECLINED-OK | Reviewer: keep. |

## Reality

| ID | Finding | Status | Evidence / note |
| --- | --- | --- | --- |
| RV-1 | GitHub Free plan | ADDRESSED | Per memlog decision: AD-7 executor allowlist, OIDC, Administrator-only private repository, no repository-secret credentials; GitHub Team trigger; accepted risk. |
| RV-2 | Attestations only in public repositories | ADDRESSED | Workflows row: public Platform repository, SHA-pinned Builds workflows; Aspire-to-Helm row "chart OCI attestation verification". |
| RV-3 | `azp` for public clients | **NOT ADDRESSED** | The confidential-client rule is in AD-14. The memlog gate bundle's "negative test per public client" appears nowhere. AD-8's negatives cover staging-to-production isolation only. |
| RV-4 | PSS `restricted` vs Dapr | ADDRESSED | Profile: "sidecar drop-all-capabilities enabled"; AD-1 security contexts; admission dry-run. |
| RV-5 | Workflow and actor ACLs; trust domain | ADDRESSED | AD-8 WorkflowAccessPolicies and namespace discriminator; negatives; EventStore actor confirmation deferred. |
| RV-6 | Keycloak event export | ADDRESSED | AD-6 event settings; Secrets row export channel; DR step 4; freshness metadata. |
| RV-7 | GitHub schedule limits | ADDRESSED | Per memlog decision: off-site monitor plus hourly dead-man; explicit issues. |
| RV-8 | Currency list; Kubernetes EOL | ADDRESSED | Per memlog decision: currency row extended; accepted risk; G1 requires a supported minor. |
| RV-9 | Helm floor, flag, SSA | ADDRESSED | Stack; AD-3; Aspire-to-Helm SSA. |
| RV-10 | Stack alignment | ADDRESSED | |
| RV-11 | Memlog citation | ADDRESSED | Memlog gate-reality entry records the package inspection. |
| RV-12 | Exporter evidence | ADDRESSED (partial) | Aspire-to-Helm row adds digest-pinned values. Residue: the memlog does not record the Aspire.Hosting.Kubernetes package-API evidence. |
| RV-13 | Runner update monitoring | DEFERRED-OK | Release state row. |
| RV-14 | Shared OpenBao token and expiry | DEFERRED-OK | Secrets Deferred row: "splitting its shared `openbao-runtime-bootstrap` token per app and renewing before its 2027-07-19 expiry". |

## Adversarial

| ID | Finding | Status | Evidence / note |
| --- | --- | --- | --- |
| U1 | Record binds per-environment values | ADDRESSED | Binding classes convention; route-content digest. See C-1 for the remaining baseline-bound items in AD-2. |
| U2 | Retention entries unservable by N-1 | ADDRESSED | AD-15 retention entries, codec version, additive rule, rehearsal command; EventStore confirmation deferred. |
| U3 | Rendered-input reversion | ADDRESSED | Binding classes; AD-3 "environment-current values". |
| U4 | Baseline identity | ADDRESSED | "working baseline" definition; Staging gate deploys production's baseline. |
| U5 | Profile digest preimage | ADDRESSED | Production profile. See C-9. |
| U6 | Dual version authority | ADDRESSED | Qualified sets; template fixes the Dapr minor; sidecar within skew. |
| U7 | Composed-image lifecycle subject | ADDRESSED | AD-13. |
| U8 | Cross-module actor | ADDRESSED | Per memlog decision: AD-14 token exchange and attested actor. See C-3. |
| U9 | Synthetic client surface | ADDRESSED | AD-14 map; AD-6 ownership; Synthetic identities; declaration "surface each exercises". |
| U10 | Per-job synthetic credentials | ADDRESSED | Synthetic identities. |
| U11 | McpCli candidate | ADDRESSED | AD-11; AD-2; diagram 2. |
| U12 | Actor spoofing | ADDRESSED | AD-11. |
| U13 | AD-13 vs AD-4 source mode | ADDRESSED | AD-13. |
| U14 | Per-composition catalog | ADDRESSED | Catalogs row. |
| U15 | Extension-package contract | ADDRESSED | AD-13; declaration row. See C-11. |
| U16 | Catalog activation order | ADDRESSED | Catalogs row; AD-3 "or a catalog generation was committed". |
| U17 | Lock fencing and takeover | ADDRESSED | Attempt ownership. |
| U18 | Shared-change executor and locks | ADDRESSED (partial) | AD-7 and Attempt ownership. Residue: namespaced environment-layer changes have no stated executor; see C-4. |
| U19 | Environment-layer definition | ADDRESSED | AD-1. |
| U20 | G1 ingress closed | ADDRESSED | G1 row. See C-6. |
| U21 | Pre-G3 and planned releases | ADDRESSED | Per memlog decision: Release modes row. See C-2. |
| U22 | Drill substitutions | ADDRESSED | DR evidence. |
| U23 | Restore rewinds the fence | ADDRESSED | DR steps 3–4. |
| U24 | Recovery executor | ADDRESSED | AD-7; AD-12; DR sequence. |
| U25 | Tenant-key store | ADDRESSED | AD-12; Backup row. |
| U26 | Cumulative classification | ADDRESSED | Module intake. |
| U27 | Realm-contract precondition | ADDRESSED | Before production update. |
| U28 | AD-10 vs Migration | ADDRESSED | AD-10. |
| U29 | Recovery lifecycle scope | ADDRESSED | Startup task lifecycle; DR step 3. |
| U30 | Provenance per artifact class | ADDRESSED | Workflows row. |

## New contradictions and dangling references introduced by the rewrite

| # | Sev | Where | Contradiction | Fix (replacement text) |
| --- | --- | --- | --- | --- |
| C-1 | medium | AD-2 vs Binding classes, Module intake, Staging gate, Workflows | AD-2 lists "effective change classifications … and rehearsal evidence" as **release-invariant** record values. The Module intake row computes the classification against *production's working baseline*, and the staging executor produces rehearsal evidence later, against that baseline. Both are baseline-bound, not identical in every environment. AD-2 also accepts "records … only from the environment's executor identity", yet the Workflows row says the publication workflow "writes the release record". | AD-2: replace "realm-contract version, effective change classifications, check-suite digests and rehearsal evidence, each linked to its evidence" with "realm-contract version and check-suite digests, each linked to its evidence; effective change classifications and rehearsal evidence are staging evidence linked to the record and naming the working baseline they were computed against". Replace "and records and evidence only from the environment's executor identity" with "the release record only from that workflow, and attempt records, promoted records and evidence only from the environment's executor identity". |
| C-2 | medium | Staging gate vs Release modes, Before production update, AD-15 | The Staging gate includes the AD-15 rollback rehearsal and says "failed … results block promotion". Release modes says the staging gate applies to both modes, and that the approved mode serves incompatible releases. An incompatible release fails the rehearsal by definition, so it can never be promoted. AD-15 also demands a prepared rollback combination unconditionally, while Release modes lets a planned recovery replace automatic rollback. | Staging gate: replace "Missing, skipped, failed, incomplete, stale or wrong-release results block promotion." with "Missing, skipped, failed, incomplete, stale or wrong-release E2E results block promotion; a failed rollback rehearsal is breaking compatibility evidence (Release modes)." AD-15: prefix the rule with "Unless an Administrator-approved record names a separately planned recovery, before production rollout …". |
| C-3 | medium | AD-11 vs AD-14 | AD-11: "In hosted environments the actor is the token subject … the gateway rejects a differing Contracts-declared actor property." AD-14: "asynchronous task steps use the original actor attested by EventStore at admission". In an async step the token subject is the calling module's client, so the gateway must reject exactly the call AD-14 authorizes. | AD-11: "In hosted environments the actor is the token subject, or for an AD-14 asynchronous task step the EventStore-attested original actor: McpCli refuses `--actor`, and the gateway rejects a Contracts-declared actor property that differs from that actor." |
| C-4 | medium | AD-7 vs AD-8, AD-7 itself, Synthetic identities | AD-7: "Each executor holds only its own environment's namespace-scoped deploy identity". This conflicts with three other statements: AD-8 "environment-layer changes use a separate identity" for the data namespace; AD-7's own "separate per-job cluster-scoped identity" on the production executor, with no stated source on GitHub Free and no repository secrets; and Synthetic identities "held only by the production executor". Staging data-namespace changes also have no executor. | AD-7: "Each executor holds, or obtains per job through GitHub OIDC claim checks, only its own environment's credentials — the namespace-scoped application deploy identity, the separate AD-8 data-namespace identity and, for production, the synthetic credentials — none with bind, escalate or impersonate …. Namespaced environment-layer changes run on that environment's executor under its lock; shared and cluster-scoped changes run as a named workflow on the production executor with a cluster-scoped identity obtained per job through OIDC claim checks." |
| C-5 | low-med | Workflows row vs Module intake | The publication workflow "builds and attests … module images". The Module intake row pins module package **and image digests** from module releases, so the images already exist. | Replace with: "it builds and attests the application package, composed image and McpCli candidate, verifies the provenance of intake-pinned module packages and images, and writes the release record". |
| C-6 | low-med | Deferred "Recovery capacity and coverage" vs G1, Diagnostics | "off-site monitor … hosts" is due "Before G2", but G1 says "the probe runs from G1", and the off-site monitor runs the probe. | Move "the off-site monitor host and its dead-man check" into the "Exposure, DNS and certificates" row (Before G1). Keep only "recovery executor host" in the Before-G2 row. |
| C-7 | low-med | Binding classes vs Secrets, AD-8 | Environment-current values "passed as chart values" include "credentials". This puts secrets in Helm values and release Secrets in the application namespace, which contradicts OpenBao-only secrets, the bootstrap-exception rule and AD-8's bootstrap Secrets held outside the deploy identity's scope. | "…hostnames and credential references (values stay in OpenBao or the documented bootstrap Secrets)". |
| C-8 | low | Hosted interfaces | "nothing bypasses it [ingress] to reach pods. Executors verify through an internal endpoint". Unless that endpoint is an ingress, it is a bypass. Dapr invocation also reaches pods directly. | "…and no external caller bypasses it to reach pods; executors verify through the declared internal ingress endpoint with the same authentication." |
| C-9 | low | Production profile vs Binding classes | The profile digest "covers the template plus environment-layer pins, excluding environment-current … values", yet Binding classes lists "environment-layer versions" as environment-current. "Promotion compares release-invariant digests" also omits the required profile-digest equality. | Profile: "The profile digest covers the template plus the inventory's environment-layer pins; it excludes all other environment-current values and all release-invariant values." Binding classes: "Promotion compares release-invariant digests and requires equal staging and production profile digests; readiness compares attempt-bound digests." |
| C-10 | low | Release modes vs Workflows row | "One production release workflow has two modes" uses an undefined term, which regresses R-12. Only the "Platform publication workflow" and "Deployment workflows" are defined. | "The production deployment workflow has two modes." |
| C-11 | low | AD-13 | "declare no secrets … except a named AD-9 exception with its own declared least-privilege secret" is self-contradictory as parsed. | "Extension packages implement only the extension API and declare no Dapr roles; they declare no secrets and reference no provider SDK or Dapr client, except that a named AD-9 exception may reference its provider SDK with its own declared least-privilege secret." |
| C-12 | low | G1 | "deployment into the production namespace" dangles after R-08 split each environment into application and data namespaces. | "deployment into the production application namespace". |

### Observations (not counted)

- "canonical profile" (AD-1) is the only use of that term. Elsewhere the spine says "profile template" and "profile inventory". Consider "rendered from the profile template, inventory and declarations".
- Binding classes lists "promotion stop" among attempt-bound values. Attempt ownership and Automatic recovery treat it as a durable per-environment field that outlives attempts and that only an Administrator record clears. The Production profile sentence "promotion stays stopped until that record exists" uses the same word for a different, self-clearing condition. Consider "promotion is blocked until that record exists".
- The memlog "Gate fixes applied" event says "Pragmatism de-duplication applied except PR-01". PR-08 and the partial residues of PR-22, PR-24 and PR-25 show that claim is not fully accurate.

## Checked and clean

- All AD cross-references (AD-1..AD-15 and the qualified EventStore/Projects ADs) resolve to matching content. The capability map covers every AD.
- No residue of removed concepts remains: protected environments, runner groups, environment secrets, a GitHub-hosted probe or freshness monitor, the watchdog, "off the hour", or an unqualified "Platform release workflow". Remaining "protected ref" uses refer to the public Platform repository, where branch protection is available on Free.
- The Structural Seed diagram matches the revised text: app and data namespaces, the recovery executor, the off-site monitor and the dead-man.
