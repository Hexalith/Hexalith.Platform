# Validation report: Hexalith Platform architecture spine (validate r2, 2026-09-27)

**Gate verdict: FAIL.** The spine cannot stay final: an unrecorded AD-11 amendment dropped a still-binding memlog rule, the final PRD's deputy, synthetic-admission and qualification-timing changes never landed, and Builds already ships the runner and schema the spine assigns to Platform - 27 high clusters, no critical, and none requires reversing an adopted decision.

- **Can proceed now:** Source/package adoption (removing AD-4 file-existence selectors and sibling paths; Stack pin alignment that moves EventStore.Aspire and the Dapr toolkit together), isolated module tests without Platform (AD-5 first stage) and EventStore's extension-API work can proceed; the routing-catalog schema can proceed once C-07 adds the eligibility field.
- **Blocked:** Runner, Platform-tool and declaration-schema stories wait for C-01 and C-02; module enrollment epics wait for C-03 to C-08 and C-28 to C-31; hosted-environment and production-deployment stories wait for C-09 to C-23 and the medium hosted clusters; recovery and DR stories wait for C-24 to C-26 and C-49 to C-53; G1 also waits for C-27 and C-54.
- **Since the last run:** The 2026-09-27 update closed most earlier findings (reviewers confirmed the composed host, binding classes, rollback sets, separate executors, off-site monitor and per-environment data isolation), but the later AD-11 amendment regressed memlog V-48's deploy/route prohibition without a memlog entry, and the final PRD's deputy, synthetic-admission and timing changes were never carried. The added security, operability and brownfield lenses and the live-cluster checks surfaced the Builds-runner overlap, hardcoded Dapr names, DR-sequence gaps and trust-root findings.
- **Lint:** `lint_spine.py` returned ok=true with 0 findings.

Subject: [ARCHITECTURE-SPINE.md](../../ARCHITECTURE-SPINE.md) (status final, updated 2026-09-27, 15 ADs). 64 clusters merged from 154 source IDs (127 findings from 8 lenses plus 27 prior-run items still open). Clusters: 27 high, 30 medium, 7 low, no critical. Dispositions: 38 autofix, 24 discuss, 1 defer, 1 ignore. Machine-readable data: [findings.json](findings.json).

## Lenses

| Lens | Kind | Verdict | Critical / High / Medium / Low | Report |
| --- | --- | --- | --- | --- |
| Rubric walker (good-spine checklist) | floor | CONDITIONAL PASS | 0 / 4 / 9 / 3 | [review-rubric.md](review-rubric.md) |
| Reality and version | configured | CONDITIONAL PASS | 0 / 3 / 6 / 8 | [review-reality.md](review-reality.md) |
| Adversarial divergence | configured | FAIL | 0 / 8 / 4 / 1 | [review-adversarial.md](review-adversarial.md) |
| Input reconciliation and drift | ad-hoc | FAIL | 0 / 6 / 8 / 5 | [review-reconcile.md](review-reconcile.md) |
| Security and identity | user-selected | CONDITIONAL PASS | 0 / 7 / 6 / 1 | [review-security.md](review-security.md) |
| Operability and DR | user-selected | CONDITIONAL PASS | 0 / 7 / 8 / 1 | [review-operability.md](review-operability.md) |
| Brownfield ratification | user-selected | CONDITIONAL PASS | 0 / 2 / 3 / 1 | [review-brownfield.md](review-brownfield.md) |
| Pragmatism and bloat | user-selected | PASS WITH TRIMS | 0 / 3 / 9 / 14 | [review-pragmatism.md](review-pragmatism.md) |

- **Rubric walker (good-spine checklist):** Strong on declarations, source identity and the release model; four divergence points open: legacy hosts in compositions, deputy authority, synthetic admission against G1, and the McpCli publisher.
- **Reality and version:** Stack and upstream facts check out; read-only live checks contradict three premises (GitHub Free framing, retired ingress-nginx, privileged CI runner on the production node), and two Dapr 1.18 facts are wrong or missing.
- **Adversarial divergence:** Eight high divergence pairs: environment-layer secrets with no writable namespace, forward-only inputs without expand/contract, immutable qualified sets, no empty-baseline path, lost calling surface, no eligibility carrier, DR credential writers, and the AD-11 amendment.
- **Input reconciliation and drift:** The spine was amended after finalization without a memlog entry, dropped memlog V-48's deploy/route rule, and contradicts the final PRD on deputy recipients and authority, pre-G2 admission and qualification timing.
- **Security and identity:** The prior FAIL is closed; seven highs remain: legacy hosts reopened, surface lost across hops, admin authority in agent tokens, module code beside production credentials, unauthenticated Administrator records, missing deputy model, shared names in the staging DNS zone.
- **Operability and DR:** The release-attempt state machine now runs end to end with exact PRD numbers; DR executability, shared-infrastructure changes on one node and the deputy role carry seven highs.
- **Brownfield ratification:** Stack pins match the code; Builds already ships the runner, composition host, eventstore root and manifest schema the spine gives Platform, and every module hardcodes Dapr component names.
- **Pragmatism and bloat:** Problems are scatter, not size: release tiers, the legacy MCP/CLI policy and production preconditions each live in three to eight homes; net length stays about flat after consolidation.

## Themes

| Theme | Clusters | Summary |
| --- | --- | --- |
| T1 Legacy MCP/CLI amendment and McpCli seams | C-01, C-03, C-04, C-32, C-33, C-55 | The AD-11 amendment made proprietary MCP/CLI surfaces obsolete but dropped memlog V-48's rule that kept legacy hosts out of Platform compositions, widened 'CLI' to the Platform tool and Builds tooling, and was never recorded. McpCli's own spine still runs a second publisher. Restoring the deploy/route prohibition is a wording fix; scope, publisher and the admin cutover path need decisions. |
| T2 Recovery deputy authority | C-09, C-49 | The final PRD gives a named deputy alerts, independent recovery access and the right to recover and reopen, but the spine still routes every notification and recovery trigger through Administrator alone and forbids the realm writes DR needs. The trigger path under GitHub Free is the decision; recipients, Roles and the NFR-2 coverage clock are wording. |
| T3 First production entry (G1) path and release control | C-05, C-10, C-11, C-12, C-34, C-35, C-36, C-37, C-38, C-39, C-40, C-59 | Synthetic smoke actors cannot pass admission at G1, no path exists while production has no healthy baseline, and release controls and delivery are gated at G3 instead of the first production attempt. Preconditions, records, timers and 'breaking' are scattered or ambiguous. Most are autofixes; synthetic admission, record timing, staging reset and planned recovery need choices. |
| T4 Identity chain across modules | C-06, C-07, C-13, C-28, C-58 | AD-14's calling-surface model holds at the gateway but is lost across token exchange and asynchronous steps, has no carrier for surface eligibility, and rests on an EventStore attestation contract that does not exist. Agent-usable Administrator tokens can also carry realm-management authority. |
| T5 Environment layer and shared infrastructure | C-14, C-15, C-16, C-17, C-18, C-41, C-42, C-43, C-44, C-45, C-60 | The environment-layer boundary does not work as written: its Components and bootstrap Secrets have no writable namespace, its forward-only inputs have no expand/contract rule, immutable qualified sets break on every patch, and shared-infrastructure membership is described five different ways. Dapr 1.18 facts and the staging DNS zone need correcting. |
| T6 Disaster recovery sequence and operability | C-19, C-24, C-25, C-26, C-50, C-51, C-52, C-53 | The DR sequence cannot be executed as written: nothing reproduces the environment layer or deploys the release before hooks run, the fence targets dead authorities, module-owned credentials get two writers, and the posture after DR is undefined. Installation-wide outage triggers (certificate expiry, OpenBao seal, disk) have no owner or warning. |
| T7 Brownfield reality: Builds runner, Dapr names and module spines | C-02, C-08, C-29, C-30, C-31, C-46 | Builds already ships a runner, composition AppHost, eventstore root and manifest schema that the spine assigns to a Platform tool that does not exist, and every MVP module hardcodes the Dapr component names the spine forbids. Module-spine contradictions and the AD-4 submodule identity lack recorded overrides or migration steps. |
| T8 Supply chain and live-infra premises | C-20, C-21, C-22, C-23, C-27, C-47, C-48, C-54, C-61 | Read-only live checks contradict three premises: Platform main is unprotected, the shared ingress controller is the retired ingress-nginx, and a privileged CI runner sits on the production node. Security adds module code running beside production credentials, unauthenticated Administrator records and provenance gaps for module images and transitive actions. |
| T9 Drift and document hygiene | C-56, C-57, C-62, C-63, C-64 | Undefined terms, an AD-only capability map and long single-paragraph Rules make the spine hard for a small agent to navigate; several rules still have two to five homes; Stack and brownfield statements are stale. All are wording fixes except the downstream SPEC re-sync (deferred) and two owned brownfield residuals (ignored). |

## Decisions needed

Each question lists the recommended option first. Wording that applies under every option is already in the cluster's fix.

### C-01: What does AD-11's 'no proprietary MCP/CLI surface' rule cover?

*AD-11's no-proprietary-CLI rule now covers the Platform tool and Builds tooling* (high, gate: now)

1. **Module-operation access only (recommended).** Scope AD-11 to surfaces that expose module commands, queries or administration; the Platform tool, Builds tools, validators and recovery-hook executables stay outside it, matching the approved proposal's 'module presentation and operator tools' limit.
2. **Everything; Platform commands move into McpCli.** Keep the broad wording and plan run/teardown/debug and qualification commands as McpCli verbs; a large unplanned redesign of the Platform tool and Builds tooling.
3. **Broad rule with a named exemption list.** Keep the broad wording but list the exempt tools by name; easy to read, but every new tool needs a spine edit.

### C-02: Is Builds' `hexalith-module` the Platform runner and tool, or does Platform build its own?

*Builds already ships the runner, composition AppHost, eventstore root and manifest schema that the spine assigns to Platform* (high, gate: now)

1. **Ratify hexalith-module (recommended).** Platform keeps the declaration and lifecycle semantics; Builds implements and validates them in `hexalith-module`, as it already does for the release-record encoding; the Local tool convention names it and states how it meets AD-4's submodule-identity check; its EventStoreHost becomes the AD-13 composed-host path or is superseded by it.
2. **Freeze and supersede.** Freeze `hexalith-module`, its hosts and its schema before publication and name their supersession by a new Platform tool with Builds as owner; more new code, and Projects must re-target.

### C-04: Who publishes the stable McpCli tool that users install?

*Two publishers for the McpCli tool* (high, gate: module-adoption)

1. **Platform publishes stable (recommended).** McpCli's pipeline publishes only prerelease or candidate versions; the Platform publication workflow is the sole publisher of the staging-validated stable tool, and the release record binds its package hash. Keeps AD-11 as adopted.
2. **McpCli publishes, Platform qualifies.** McpCli keeps semantic-release; Platform qualifies a named McpCli version by package hash in the release record and never rebuilds it. Changes AD-11's 'builds a candidate from intake Contracts'.

### C-06: How does a server-to-server step carry the originating calling surface?

*Cross-module hops launder or lose the calling surface and current revocation* (high, gate: module-adoption)

1. **Attested originating surface (recommended).** EventStore attests the originating surface with the actor; the chain's effective surface is the most restrictive; module service clients carry no confirmation eligibility; downstream confirmation-required steps consume the originating confirmation record.
2. **Deny by origin, no carrier.** A chain that starts on an agent-capable surface may call only agent-eligible downstream operations; confirmed UI flows that need downstream confirmation-required steps call them directly from the UI's confidential client.
3. **Least-privilege service class only.** Map every module service client to a least-privilege class and accept that confirmed UI flows cannot trigger downstream confirmation-required operations.

### C-08: May modules keep logical Dapr component names as defaults?

*Every MVP module hardcodes Dapr component names* (high, gate: module-adoption)

1. **Overridable defaults, always bound (recommended).** Logical names may stay as option defaults, but Platform always binds them through configuration; no `const` or attribute literal remains, and attribute-bound subscriptions read the configured name.
2. **No defaults.** Modules receive every component name from configuration and fail to start without it; stricter, with more migration work in every module.

### C-09: How does the recovery deputy start and run recovery without breaking the single-writer operations repository?

*Recovery deputy authority, trigger path and notification recipients are missing* (high, gate: hosted)

1. **Allowlisted recovery runs, no repository write (recommended).** The off-site recovery executor accepts recovery runs started under the deputy's own MFA identity from a pinned off-site copy of the recovery workflows, needing neither ops-repo write nor GitHub; the deputy's realm rights are DR-scoped to restore, revocation replay and rotation.
2. **Deputy as second ops-repo writer.** Grant the deputy write access, which triggers AD-7's GitHub Team adoption (cost, and a second writer on every deployment workflow).
3. **Administrator-only execution.** Keep execution Administrator-only and record deputy-led recovery as an accepted risk; this contradicts final PRD FR-8/FR-9 and needs a PRD change.

### C-10: How are synthetic smoke actors admitted in production?

*Synthetic smoke actors cannot pass admission while G1 requires an empty admission group; the PRD's pre-G2 SM-4 step is missing* (high, gate: hosted)

1. **Separate synthetic-admission group (recommended).** A standing Administrator-granted group scoped to the synthetic tenant; G1's 'empty' means the human production-admission group; plus the PRD's temporary SM-4 grant, recorded and revoked.
2. **Per-attempt temporary membership.** Synthetic actors join the production group only for each verification window; keeps one group but adds a grant/revoke step to every attempt.
3. **Read-only production smoke until G2.** Production smoke runs read-only until G2; no admission change, but write-path verification is lost.

### C-16: How does an environment-layer security patch keep existing releases deployable and restorable?

*Immutable qualified digest sets make every environment-layer patch invalidate existing releases* (high, gate: hosted)

1. **Qualification records extend the sets (recommended).** The environment-layer attempt's re-verification issues a Platform qualification record that extends the release's qualified sets for that environment; the release record stays immutable; effective sets are the record plus its extensions.
2. **Version constraints instead of digests.** Release records declare per-facet constraints (a minor line plus security patches); lighter, but releases then run on patch combinations nobody re-verified.
3. **Republish before any change.** A new release whose sets include the new digest is published before any environment-layer change; strict, but every patch needs a full release.

### C-17: How are shared-infrastructure changes qualified on the single node?

*The shared-infrastructure tier is undefined: membership, qualification venue and failure path* (high, gate: hosted)

1. **Tier table plus rehearsal on prepared capacity (recommended).** Define the tiers; rehearse shared changes on a production-profile copy on the prepared capacity before touching the node; record the two-environment outage of an in-place Kubernetes-minor upgrade as an accepted risk.
2. **Tier table, no rehearsal.** Define the tiers and the failure path, and record as an accepted risk that 'staging first' gives no qualification for shared components.
3. **Blue/green cluster replacement.** Stand up a new cluster on the prepared capacity for Kubernetes-minor and CNI changes and cut over; removes the in-place outage, costs capacity and a drill-like cutover each time.

### C-18: Where do shared-infrastructure and production-trust hostnames live?

*The staging DNS zone holds shared-infrastructure names, so the staging ACME credential can mint registry and issuer certificates* (high, gate: hosted)

1. **Separate zone or delegated subzone (recommended).** Move registry and issuer names to a zone or subzone that no environment credential can write; staging credentials cover only delegated staging names.
2. **Keep the names, restrict staging to HTTP-01.** Keep `registry.hexalith.com` but give staging no DNS-01 zone credential and reserve shared names in the FQDN pattern; cheaper, and relies on admission for the rest.
3. **Accept the risk.** Record staging-zone control over shared names as an accepted risk until the registry moves.

### C-19: How is OpenBao unsealed after a node restart?

*Whole-installation outage triggers (credential expiry, OpenBao seal, disk) have no owner or early warning* (high, gate: hosted)

1. **Manual unseal within coverage (recommended).** Administrator or the deputy unseals; outages outside coverage fall under the declared response arrangements; the monitor alerts on seal state. Keeps key custody with people, as already decided.
2. **Auto-unseal with an off-node key.** Use auto-unseal against a key held off the node; restarts self-heal, but add an availability and custody dependency on the key service.

### C-22: How is `main` of the publication repositories protected on GitHub Free?

*The GitHub Free accepted risk omits the publication-side trust root* (high, gate: hosted)

1. **Repository rulesets (recommended).** Before first publication, a repository ruleset on Platform `main` and release tags (PR required, no force-push or deletion, `.github/workflows/**` owned by Administrator, bypass only by Administrator), plus the same tightening of Builds bypass actors; free for public repositories.
2. **Single writer.** Reduce Platform and Builds to one writer, like the operations repository; strongest, but blocks the other two contributors.
3. **Accept as is.** Record three writers with bypass as an explicit accepted risk.

### C-23: What replaces the retired ingress-nginx controller?

*The shared ingress controller is the retired ingress-nginx, and shared-layer components are missing from the currency rules* (high, gate: hosted)

1. **Traefik through Gateway API (recommended).** Use the already-installed, current Traefik through Gateway API (the upstream recommendation), migrated before hosted-interface stories are written.
2. **Traefik with Ingress resources.** Keep the Ingress API on Traefik; a smaller change now and a second migration later.
3. **Keep ingress-nginx until G1.** Record an unsupported controller fronting the identity issuer as an accepted risk until G1.

### C-24: How is Keycloak restored in DR, given that AD-6 forbids automation holding master or cross-realm admin?

*The DR sequence omits the steps that make step 3 executable, and Keycloak restore authority is unresolved* (high, gate: recovery)

1. **Human, DR-scoped database restore (recommended).** Administrator or the deputy restores the whole Keycloak database as a DR-scoped operator step under custody, an explicit AD-6 exception; the staging realm is disabled on recovery capacity before reopening.
2. **Production-realm-only restore.** Back up and restore the production realm alone (realm export plus the admin/user event export), so no step ever needs master; more backup machinery and a realm-level export cadence.

### C-25: How are module-owned authorization revocations made after the recovery cut handled?

*The DR fence proves against authorities on the dead node, misses the surviving ones, and revocation replay is incomplete* (high, gate: recovery)

1. **Accepted RPO exception, reviewed before reopening (recommended).** List them in the DR report as an accepted RPO exception that Administrator reviews before reopening, like non-Memories erasures; no new infrastructure.
2. **Off-site revocation journal.** Modules journal authorization revocations off-site and DR re-applies them; closes the gap, adds a journal per module.

### C-27: What happens to the privileged Forgejo runner on the production node?

*A privileged CI runner shares the single production node* (high, gate: G1)

1. **Move it off the node before G1 (recommended).** Relocate the Forgejo CI runners to a separate machine or VM before G1, or earlier if staging holds real data.
2. **Rootless in a restricted namespace.** Keep it on the node but run it rootless and unprivileged in a `restricted` namespace with no host mounts; container builds need a rootless builder.
3. **Owner-signed accepted risk.** Name 'privileged CI runner on the production node' as an explicit accepted risk signed by Administrator.

### C-31: How does a module workspace get its AD-4 Platform identity?

*AD-4's Platform-submodule identity has no brownfield path, and owned work removes the only instance* (medium, gate: module-adoption)

1. **Direct Platform submodule everywhere (recommended).** Every workspace that runs the Platform tool (domain modules, McpCli, technical modules producing Platform evidence) declares `references/Hexalith.Platform` directly; McpCli keeps its reference.
2. **Tool-version identity for CI.** CI takes its Platform identity from the pinned tool version instead of a submodule; only local source mode needs the submodule.

### C-32: Who decides how non-gateway and infrastructure-admin capabilities reach McpCli, and what is the interim admin path?

*Non-gateway and infrastructure-admin cutover: decision form, owner and interim admin path are unclear* (medium, gate: hosted)

1. **Platform AD amending AD-11/AD-14 (recommended).** The cutover is recorded as a Platform AD (surface class, Keycloak client, gateway or ingress exposure, realm entries); confirmation-required admin operations move to a confidential UI or are withdrawn; EventStore Admin.Server/Admin.UI stays as a confidential UI surface meanwhile.
2. **McpCli decides, Platform ratifies.** McpCli's spine settles the transport and Platform ratifies it afterwards; faster for McpCli, but a surface can exist outside AD-14 until ratified.

### C-35: When is the production-promoted record written?

*The record model is undefined: kinds, writers, cardinality, promotion-stop home and production-promoted lifecycle* (medium, gate: hosted)

1. **Before the Helm upgrade (recommended).** Write it before the upgrade so no pod serves without it, and invalidate it on any non-working terminal outcome.
2. **At readiness, before commit.** Write it once candidate hosts are ready; pods briefly serve without it, and fewer records need invalidating.

### C-37: How is staging reset after a candidate that production does not adopt?

*Staging data from an unadopted candidate can block every later gate* (medium, gate: hosted)

1. **Restore from a staging recovery point (recommended).** Restore staging from a recovery point cut before the abandoned candidate, as its own staging attempt; keeps realistic long-lived data and exercises restore.
2. **Recreate with synthetic data.** Recreate staging with its creation tasks and synthetic data; simpler and deterministic, but loses long-lived data realism.

### C-38: How does a failed approved incompatible release recover?

*The planned recovery for an approved incompatible release has no execution rules* (medium, gate: hosted)

1. **Recovery point, stop, then DR-entry recovery (recommended).** The attempt starts only after a complete recovery point cut after the lock; on a non-working outcome the executor sets the stop, closes user ingress and stops; the named recovery runs as a DR entry under a new epoch by Administrator or the deputy, with the same verification.
2. **Executor runs the plan in-job.** The executor runs the planned recovery automatically in the same job and lock; faster, but automates a data restore with no human check.

### C-43: How are Dapr runtime changes controlled in hosted and local environments?

*Dapr patch pinning, sidecar skew and HotReload posture are undefined* (medium, gate: hosted)

1. **HotReload off, exact patch, local warns (recommended).** Disable HotReload in hosted Configurations so Dapr resource changes follow pod rollout; pin the exact patch per release across CI, staging and production; local runs report their patch and warn on mismatch.
2. **HotReload on, modelled.** Keep HotReload and model its restarts in the rollout, rollback and verification rules; local runs warn only.
3. **Keep V-47 as written.** Enforce the release patch on local runs too and keep HotReload off; strictest, and burdens every developer and agent sandbox.

### C-48: Is the retained-artifact registry publicly readable?

*The registry is anonymously readable, a second registry exists, and the composed image name collides with EventStore's* (medium, gate: hosted)

1. **Authenticated read on registry.hexalith.com (recommended).** Keep `registry.hexalith.com` for retained artifacts, turn off anonymous read so per-environment pull credentials mean something, and publish the composed image as `platform/eventstore`.
2. **Public read accepted.** Accept anonymous read (artifacts come from public repositories), drop pull credentials as a read control, and still rename the composed image.
3. **Use the private registry.tache.ai.** Move retained artifacts to the private registry; requires re-pointing module release defaults.

### C-50: Can anything be promoted while staging is gone after DR?

*The post-DR handover and degraded posture are undefined* (medium, gate: recovery)

1. **Stop until staging returns (recommended).** The promotion stop stays set until staging is re-established; urgent fixes wait for staging.
2. **Approved emergency mode with a temporary staging.** Allow an Administrator-approved emergency release rehearsed on a temporary staging on spare capacity during the degraded posture.

## High clusters

No cluster is critical. The high clusters follow in gate order.

### C-01: AD-11's no-proprietary-CLI rule now covers the Platform tool and Builds tooling

High · discuss · gate: now · theme T1 · touches AD-4, AD-11, Consistency Conventions, Source Precedence, Frontmatter/Memlog

- **Finding:** AD-11 now says "All proprietary Hexalith module and technical-module MCP hosts, plug-ins, and CLIs ... are obsolete migration sources; Platform admits no new alternate proprietary MCP/CLI surface", and the McpCli source row calls McpCli the "Canonical target for all Hexalith-owned CLI/MCP access". Read literally, this covers the pinned Platform .NET tool (AD-4, Local tool convention), Builds' `hexalith-module` and `hexalith-evidence` (Builds is a listed technical module) and any validator or recovery-hook executable, all of which the spine depends on. The approved sprint change proposal limited the change to "module presentation and operator tools" (L14); that limit did not land, and the spine does not say whether existing legacy surfaces are frozen until retirement.
- **Scenario:** The Platform-tool story reads AD-11 and either folds run/teardown/debug into McpCli (an unplanned redesign) or ships a non-compliant tool; a Builds maintainer treats `hexalith-evidence` as obsolete while the release-record validator depends on it; recovery hooks cannot ship as automation-invocable executables before the first drill because their cut-over waits on a deferred McpCli decision.
- **Fix:** Add to AD-11: "This rule governs module-operation access surfaces (commands, queries and administration of module capabilities). The Platform .NET tool (AD-4) and build, qualification, validation, deployment and recovery-hook tooling that expose no module commands or queries are outside it. Existing legacy surfaces are frozen (no new operations) until retired." Change the McpCli source row to "Canonical target for all Hexalith-owned module-operation CLI/MCP access". Record the scope and the proposal as authority in the memlog. Decision: see C-01 under Decisions needed.
- **Sources:** RC-6, BF-3 (reconcile, brownfield)
- **Notes:** ADV-8 (Pair B) raised the same scope problem; that ID sits in C-03. The answer also fixes how C-02 classifies the Builds tools.

### C-02: Builds already ships the runner, composition AppHost, eventstore root and manifest schema that the spine assigns to Platform

High · discuss · gate: now · theme T7 · touches AD-4, AD-5, AD-10, AD-13, Consistency Conventions, Deferred

- **Finding:** Builds' `hexalith-module` tool validates a strict `hexalith.module-manifest.v1`, "owns supported runner lifecycle" (run, down, test) and ships a Builds-owned Aspire AppHost that composes multi-module G-4 runs plus a Builds-owned `eventstore` composition root registered as app `eventstore`; Projects' spine already targets it. The Platform spine never names it. It gives the runner (AD-10), a pinned Platform tool that does not exist (Platform has no `.config/`), the declaration schema ("adopt or supersede Projects' `hexalith.module.v1`") and the composed host (AD-13) to Platform. The Builds manifest schema also hard-codes EventStore 3.109.0 and Dapr 1.18.2 as consts. AD-5's Builds "Platform-runner integration entry" has no owned row, and `domain-ci.yml` still runs module AppHosts in an advisory tier.
- **Scenario:** `hexalith-module` is published and Projects pins it under G-4; other modules follow. The Platform runner story builds a second runner and schema. The ecosystem ends up with two runners, two manifest schemas with conflicting pins and two `eventstore` roots: the "two lifecycle owners" AD-10 prevents and the second composition root AD-13 excludes.
- **Fix:** Record the decision with an owned-work row gated "before `hexalith-module` is first published or pinned". Under either option: change the First shared versions row to "adopt or supersede Builds `hexalith.module-manifest.v1` (the implementation of Projects' `hexalith.module.v1`)"; classify `Builds.Module.AppHost` and `Builds.Module.EventStoreHost` under AD-10 and AD-13; add the Builds Platform-runner CI entry and the retirement of the advisory module-AppHost tier to Owned work. Decision: see C-02 under Decisions needed.
- **Sources:** BF-1, prior:BRN-5, prior:BRN-11 (brownfield)
- **Notes:** Depends on C-01 (AD-11 must exclude Builds tooling either way). Interacts with C-31 (AD-4 identity for whichever tool runs) and C-62 (Builds holds two disagreeing version authorities).

### C-03: Legacy MCP/CLI hosts are no longer barred from Platform compositions

High · autofix · gate: module-adoption · theme T1 · touches AD-1, AD-11, AD-14, Consistency Conventions, Source Precedence, Deferred, Frontmatter/Memlog

- **Finding:** Today's AD-11 edit deleted "no module, FrontComposer or technical-module MCP host, including EventStore.Admin.Mcp, is deployed or routed in Platform compositions until an AD admits it under AD-14" and replaced it with "obsolete migration sources; Platform admits no new alternate proprietary MCP/CLI surface". "No new" exempts every existing host. Memlog V-48 (L182) still forbids deploying or routing them, and the approved proposal's "Legacy hosts and CLIs are not enrolled into new Platform compositions; temporary compatibility use requires a named migration record and removal gate" never landed. The policy now has three homes (AD-11, the McpCli source row, the Deferred row) that disagree on scope and retirement evidence. AD-14 derives the surface only at "gateways", so non-gateway hosts sit outside it.
- **Scenario:** A Memories enrollment lists `memories-mcp` (existing, so not "new"); Platform renders and routes it and it advertises operations outside the gateway metadata/digest check. `Parties.Mcp` forwards the raw Authorization header and sets `X-User-Id`. `FrontComposer.Mcp` mapped into a UI host lets agent traffic reach the gateway under the UI client's `azp`, so UI-only and confirmation-required operations become agent-executable. `EventStore.Admin.Mcp` with a copied static bearer calls the Admin Server directly, outside any `azp` check.
- **Fix:** Append to AD-11: "Legacy MCP hosts and CLIs are not enrolled, deployed, routed, mapped in an enrolled host or issued a realm client in Platform local, CI, staging or production compositions; declaration validation rejects an enrolled host that maps an MCP endpoint. Any new MCP/CLI surface or transport, including a McpCli HTTP transport, needs an AD admitting it under AD-14." Change AD-14's "gateways derive the surface" to "every externally reachable host that accepts bearer tokens derives the surface ..., or accepts only tokens audienced to its own confidential client", and extend the negative test to every client in the surface map. Make AD-11 the single home of the policy, tag it [ADOPTED, AMENDED], and add a memlog decision citing the approved proposal that supersedes only V-48's "until a later AD admits" clause.
- **Sources:** RB-1, RC-5, SEC-1, ADV-8, PR-102 (rubric, reconcile, security, adversarial, pragmatism)
- **Notes:** PR-102 marked the deploy/route question discuss; it is autofix here because memlog V-48 still binds and the approved proposal carries the same prohibition. The proposal's temporary-compatibility exception can be kept for local/CI only; SEC-1 recommends no hosted exception. The EventStore Admin.Server/UI status and the interim hosted admin path are decided in C-32; ADV-8's Pair B (scope) is decided in C-01.

### C-04: Two publishers for the McpCli tool

High · discuss · gate: module-adoption · theme T1 · touches AD-4, AD-10, AD-11, Consistency Conventions, Source Precedence, Deferred

- **Finding:** AD-11 says "Each Platform release builds a McpCli candidate from its intake Contracts; it runs the staging flows and is published only after staging validation." McpCli AD-17 binds its own semantic-release that pushes `Hexalith.McpCli` to nuget.org from a green `main` and "Prevents: a second release pipeline". The spine records no override and does not say which artifact users install. RC-12 adds two McpCli seams: its own test AppHost (forbidden by AD-10) and the planned removal of its Platform submodule reference, which leaves AD-4's CI identity check without an anchor (SPEC L116 asks architecture this directly).
- **Scenario:** McpCli pushes 1.8.0 to nuget.org built from its own Contracts pins; Platform's staging-validated candidate from the intake Contracts collides on package ID and version or ships elsewhere; users running `dotnet tool update` get the unvalidated build, whose Contracts digests don't match the environment catalog, so operations silently become non-executable in production.
- **Fix:** Record one publisher of record per version in the McpCli Source Precedence row and the Connected McpCli deferred row, bind the published package hash in the release record, and answer SPEC L116 (see C-31). Migrate McpCli's test AppHost to the AD-10 descriptor (C-29). Decision: see C-04 under Decisions needed.
- **Sources:** RB-4, RC-12 (rubric, reconcile)
- **Notes:** RC-12's test-harness part overlaps C-29 and its identity part overlaps C-31.

### C-05: Deferred gates are later than the PRD requires, or missing

High · autofix · gate: module-adoption · theme T3 · touches AD-2, Release and Recovery Acceptance, Deferred

- **Finding:** Owned work "Release state, checks and notifications" is gated "Before G3", but addendum L346 requires provenance, one recovery, concurrency/interruption controls and actual GitHub delivery "before the first applicable production attempt, including an approved pre-G3 attempt"; the spine's own Release modes row says lock, provenance and records apply to both modes. The PRD also wants GitHub notifications working at G1 and deputy delivery and access proven before G2; G2 omits "isolation and access checks pass" and verified recovery access, capacity, notifications and response arrangements. The lock/record/stop store shared by four hosts is unnamed. Broker selection ("Shared runtime and profile evidence") has no gate. The PRD rows for critical-flow removal review and staging rerun evidence (prd.md L337-338) and the evidence maximum age have no home. Owned work has no gate column, and "Ratify the local mTLS, per-receiver ACL and scoped-component convention before the first local enrollment" is buried in a hosted-readiness row.
- **Scenario:** The first Administrator-approved G1 deployment runs with unproven lock and epoch serialization; the staging executor, production executor, recovery executor and monitor each pick a different lock store; a module drops two critical flows and the gate accepts the smaller suite; the staging executor keeps the latest passing rerun and hides the earlier failure; an agent planning local enrollment misses the local mTLS ratification.
- **Fix:** Split the row: "Before the first applicable production attempt, including an Administrator-approved pre-G3 attempt: named CAS-capable attempt/lock/stop store reachable from all executors and the monitor; provenance; interruption and epoch; one recovery; GitHub delivery. SM-5 rehearsals and EventStore confirmations additionally gate G3." Move actual GitHub delivery to the Exposure row (before G1); add deputy delivery and independent access to the Recovery capacity row (before G2); extend G2 with the PRD's isolation/access and recovery-arrangement checks. Add First shared versions row "Attempt lock, record and promotion-stop store | Platform with Builds | all executors, monitor | first production attempt". Gate broker selection "before first staging deployment". Add "Critical-flow removal/remap review policy | module owners with Platform | before release-gate qualification" and "Staging triggers, rerun acceptance and evidence maximum age | Platform with Builds | before the first accepted staging evidence". Give Owned work a Gate column sorted by gate, and split the local mTLS ratification into its own row gated on the first local enrollment.
- **Sources:** RB-7, RC-4, RB-11, RC-13, PR-111 (rubric, reconcile, pragmatism)
- **Notes:** The store technology is delegated to the named owner, so no user decision is needed. RC-13's post-window incident item is in C-34; ADV-4's request to move record encoding before the first staging deployment is in C-11.

### C-06: Cross-module hops launder or lose the calling surface and current revocation

High · discuss · gate: module-adoption · theme T4 · touches AD-6, AD-11, AD-14, Source Precedence, Deferred

- **Finding:** AD-14: "gateways derive the surface from the authenticated client (`azp`)" and "synchronous steps carry the user through Keycloak standard token exchange by the calling module's confidential client". Keycloak sets the exchanged token's `azp` to the requester, so after one hop the gateway sees the calling module's confidential-client surface, not McpCli's. Asynchronous steps attest only the actor, not the surface, and nothing re-checks the original actor's current admission. Nothing requires tokens minted for UI-eligible confidential clients to stay server-side.
- **Scenario:** An agent invokes agent-eligible `project.create` through McpCli; Projects exchanges the token and calls a confirmation-required Folders operation; Folders sees `azp = projects` (confidential) and allows it, bypassing FR-12 in one hop. If Projects' client maps to a least-privilege class instead, confirmed UI flows such as `project-folder.replace` fail at Folders. A user removed from the production group keeps executing through a long-running attested task.
- **Fix:** Add to AD-14: "The effective surface of a server-to-server step is the originating request's surface, attested by EventStore at admission and carried in the exchanged or asynchronous context; a chain's effective surface is its most restrictive class. Module service clients map to a service class with no UI-only or confirmation-required eligibility of their own; a downstream confirmation-required operation executes only by consuming the originating module's confirmation record. Each exchange is permitted per (requester, target audience) pair and downscopes to the target's declared operations. Asynchronous steps re-check the original actor's current admission and permissions and fail closed after revocation. Tokens minted for UI-eligible clients never leave their server." Add the attested-surface field to First shared versions as an EventStore confirmation. Decision: see C-06 under Decisions needed.
- **Sources:** ADV-5, SEC-2, prior:U8 (adversarial, security)
- **Notes:** Depends on C-28: the EventStore attestation contract does not exist yet. ADV-5 asked for discussion, SEC-2 proposed an autofix; the option matters for Projects' confirmed flows. Needs EventStore owner confirmation.

### C-07: Surface eligibility has no carrier to the gateway, and retention entries look executable

High · autofix · gate: module-adoption · theme T4 · touches AD-11, AD-14, AD-15, Consistency Conventions, Deferred

- **Finding:** AD-11 makes an operation executable when "its Contracts-declared surface eligibility allows it" and AD-14 has the gateway derive the surface, but nothing carries eligibility to the gateway: the catalog schema ("per-operation schema digests and retention entries") has no eligibility field, extension packages "implement only the extension API", and the declaration does not list it. Memlog V-18 ("declared once in module Contracts and read by both catalog generation and McpCli") was lost. Eligibility sits outside the per-operation digest, and the metadata endpoint "derived from the environment's committed catalog" would list AD-15 retention entries with matching digests.
- **Scenario:** The gateway fails open (a direct `curl` with McpCli's token runs UI-only operations) or closed (FR-12's executable set is empty). A Contracts change that makes an operation UI-only leaves the digest unchanged, so a published McpCli keeps offering it. After automatic recovery commits the rollback generation, McpCli N shows the candidate's command as executable on the rolled-back N-1 host.
- **Fix:** Add to Catalogs: "Each catalog route entry carries the operation's surface eligibility (agent-eligible, UI-only, confirmation-required, step-up), derived by the Platform generator from Contracts and covered by the contract-schema digest. EventStore.Contracts owns the vocabulary; the gateway enforces from the active generation. The metadata endpoint serves eligibility and publishes only executable entries; McpCli uses the served eligibility, not its bundled copy." Add the eligibility field to the routing-catalog First shared versions row for EventStore confirmation.
- **Sources:** ADV-6, prior:ADV-15, prior:U11, prior:U12 (adversarial)
- **Notes:** Restores memlog V-18. Small wording change, but the catalog schema is a first shared version, so it must land before that schema is built.

### C-08: Every MVP module hardcodes Dapr component names

High · discuss · gate: module-adoption · theme T7 · touches AD-8, AD-9, Consistency Conventions, Deferred

- **Finding:** AD-9 says "Module code never hardcodes Dapr component names" and the Module declaration row says "Platform assigns component names". Tenants, Parties, Folders, Projects, Memories and EventStore hardcode `statestore`, `pubsub` or `secretstore` as `const` values or in subscription attributes (`[Topic(ProjectionChangeNotifierOptions.DefaultPubSubName, ...)]`), and EventStore AD-24 fixes a singleton `openbao` component. No owned-work row, owner or gate covers the migration. AD-8's per-module least-privilege principal on a shared instance needs one Component per module, and Component names are unique per namespace.
- **Scenario:** The Aspire-to-Helm qualification or the first hosted enrollment finds that Platform-assigned names break module startup and subscriptions; Platform either patches every module under deadline or keeps shared names and gives up AD-8's per-module principals.
- **Fix:** Add owned-work row "Dapr component-name injection | module owners, with EventStore for SDK defaults and attribute-bound subscriptions | before the Aspire-to-Helm qualification and hosted enrollment". State the naming rule chosen below and reconcile EventStore AD-24's singleton `openbao` with Platform-assigned names. Decision: see C-08 under Decisions needed.
- **Sources:** BF-2, prior:ADV-2 (brownfield, adversarial)
- **Notes:** The prior validate run's ADV-2 left the same residual open (EventStore AD-24/AD-26 fix `openbao` and `statestore`, and the spine reserves neither name).

### C-09: Recovery deputy authority, trigger path and notification recipients are missing

High · discuss · gate: hosted · theme T2 · touches AD-6, AD-7, AD-12, Design Paradigm, Consistency Conventions, Release and Recovery Acceptance, Deferred, Frontmatter/Memlog

- **Finding:** The final PRD chose a split deputy role: the deputy receives deployment-failure, recovery and backup alerts, holds independent recovery and key access, runs recovery under their own identity, verifies and reopens, but cannot clear the promotion stop or administer admission (prd.md L34, L203-204, L224-225; addendum L225: "Architecture follow-up is required"). The spine only says the deputy "backs Administrator for recovery and key custody". Four rules block the PRD model: Diagnostics ("GitHub issues assigned to Administrator are the single accepted notification path"); AD-7 (operations repository "writable only by Administrator", while dispatching a GitHub workflow needs write access); AD-6 ("production realm changes are Administrator-only") against DR steps 2-4 (restore Keycloak, re-apply revocations, rotate realm keys); and DR step 6, which names no reopen authority. Starting DR also depends on GitHub, which the accepted risk covers only for notification.
- **Scenario:** Administrator is on a flight when the disk fails inside declared coverage. The probe's issue goes only to Administrator; the deputy cannot dispatch the recovery workflow or replay revocations; the RTO is missed inside coverage, while every drill "passed" because Administrator ran it. Or the runbook hands the deputy realm-admin and ops-repo write "for recovery", giving standing authority over admission and stop clearance that the PRD reserves to Administrator and silently tripping AD-7's GitHub Team trigger.
- **Fix:** Under every option: Roles - "a named recovery deputy receives every deployment-failure, recovery, backup and monitor notification, holds independent recovery and key access, and may execute the documented recovery, verify restoration and reopen service; only Administrator clears the promotion stop, approves releases or administers production admission". Diagnostics, Automatic recovery and Detection rows name "Administrator and the recovery deputy". AD-6 adds "except that a recovery run by Administrator or the recovery deputy restores the realm and re-applies recorded revocations and key rotations; recovery never grants admission or roles and never clears the promotion stop". DR step 6: "reopen (Administrator or the deputy, after step 5 passes; the promotion stop stays set)". Custodian use of unseal or root material alerts, and any root token is revoked at recovery end. Extend the Memories operator role to recovery runs. Recovery capacity row: prove the deputy's identity, minimum permissions, key custody, alert delivery and rehearsed restore and reopen before G2. Memlog decision superseding the Administrator-only recipient clauses (L159, L191, L201). Decision: see C-09 under Decisions needed.
- **Sources:** RB-2, RC-1, RC-2, SEC-6, OPS-1 (rubric, reconcile, security, operability)
- **Notes:** Recipients and Roles are wording fixes under every option; only the trigger path needs the user. Decide together with the Keycloak restore authority in C-24.

### C-10: Synthetic smoke actors cannot pass admission while G1 requires an empty admission group; the PRD's pre-G2 SM-4 step is missing

High · discuss · gate: hosted · theme T3 · touches AD-6, Consistency Conventions, Release and Recovery Acceptance, Frontmatter/Memlog

- **Finding:** Every production attempt, including the first G1 deployment, needs passing smoke checks run by "per surface class, synthetic actor ... clients". AD-6 makes admission "membership in a named production group granted only by Administrator", and G1 requires "the production admission group is empty". The spine never says how synthetic actors pass admission. The PRD (L56) and addendum (L185) add a temporary step: Administrator may admit one designated synthetic test identity for SM-4 positive evidence and revokes it afterwards. The addendum says "Architecture must carry this restricted qualification step alongside its initial empty-admission-group rule". Neither rule is in the spine.
- **Scenario:** The first G1 deployment's authenticated smoke gets a 403; verification fails and the deployment "stops with ingress closed" and can never succeed. Or a builder adds synthetic actors to the production group, violating G1. Or the gateway exempts tokens carrying the synthetic flag: a permanent admission bypass keyed on a protocol-mapper claim that a later realm change can add to another client.
- **Fix:** Synthetic identities: "Synthetic actors are admitted only through an Administrator-granted synthetic-admission group limited to the synthetic tenant. The gateway never treats the synthetic flag as admission." G1: "the human production-admission group is empty. After the G1 deployment and before G2, Administrator may temporarily add one designated synthetic test identity to the production admission group solely for SM-4 positive evidence; ingress stays executor/probe-only, the grant is recorded and revoked after the check, drills use isolated identity copies, and this does not open G2." Add a memlog entry. Decision: see C-10 under Decisions needed.
- **Sources:** RB-3, RC-3, OPS-2, SEC-8 (rubric, reconcile, operability, security)
- **Notes:** Three of four lenses rated this autofix and converged on option 1; the rubric asked for a decision because it adds a production admission path. The temporary SM-4 step is already PRD policy and lands under any option.

### C-11: No compliant path to production while it has no healthy working baseline

High · autofix · gate: hosted · theme T3 · touches AD-2, AD-15, Release and Recovery Acceptance, Deferred

- **Finding:** Before production update requires "a healthy production, meaning its working baseline is ready and its smoke suite passes now", and the staging gate "deploys production's working baseline, upgrades to the candidate"; both apply to Administrator-approved mode. Before G1 no working baseline exists. After a failed first deployment or a failed recovery, the promotion stop clears only with "a verified current working release", which no workflow can then produce. The only escape, "A manual or DR change becomes the working baseline only after passing verification", has no defined workflow, mode, lock or provenance. "Release and attempt record encoding" only has to precede "Automated promotion", yet baselines are written from G1.
- **Scenario:** The precondition checker stops the first production deployment because no baseline exists; after a failed first deployment production is stuck for good, or someone makes an unrecorded out-of-workflow "manual change"; G1 baselines are written before the Builds encoding exists and later validators cannot read them.
- **Fix:** Add an empty-baseline and degraded-production entry: "When production has no working baseline, or its last outcome was non-working, an Administrator-approved record may replace the healthy-production precondition; the staging gate then rehearses a fresh install plus the candidate instead of baseline-to-candidate; the rollback set is 'remove workloads, keep data', as for a first enrollment. A manual change is always an Administrator-approved attempt under the lock and epoch." Move "Release and attempt record encoding" to "Must precede: first staging deployment".
- **Sources:** ADV-4, prior:U21 (adversarial)
- **Notes:** The record-encoding timing overlaps C-05.

### C-12: Production preconditions are scattered over about eight homes

High · autofix · gate: hosted · theme T3 · touches AD-2, AD-3, AD-6, Consistency Conventions, Release and Recovery Acceptance

- **Finding:** "Before production update" is one 68-word sentence. The other preconditions live in Binding classes (equal profile digests), Production profile (qualified sets), AD-2 (provenance), Release modes (trigger), Automatic recovery (stop clear), Attempt ownership (lock and epoch), Staging gate (exact-release, in-age evidence) and AD-3/AD-6 (environment-layer inputs and realm contract applied before the attempt).
- **Scenario:** The production deployment workflow and the release validator are written independently and check different subsets; a missed precondition is a production-safety defect.
- **Fix:** Rewrite the row as a numbered checklist that points to each home: (1) lock and new epoch held, promotion stop clear; (2) release-mode trigger met; (3) AD-2 provenance; (4) exact-release staging evidence within maximum age; (5) EventStore server package release-available; (6) equal staging and production profile digests, current environment-layer versions within the release's qualified sets, required realm-contract version, environment-layer inputs applied and verified; (7) valid non-empty readiness and smoke declarations; (8) working baseline ready and smoke passing now; (9) compatibility evidence, with missing, breaking or wrong-baseline evidence routing to approved mode. Delete the duplicated clauses from Binding classes and Production profile.
- **Sources:** PR-103 (pragmatism)
- **Notes:** No rule is missing; the high rating reflects divergence risk between two validators and could fairly be medium. Apply after C-10, C-11 and C-16 so the checklist includes their outcomes.

### C-13: Agent-usable tokens may carry Administrator authority, and Administrator authentication has no invariant

High · autofix · gate: hosted · theme T4 · touches AD-6, AD-11, AD-14, Deferred

- **Finding:** Administrator is also a McpCli user. With Keycloak's default full-scope client setting, an Administrator McpCli token carries realm-management roles and the admin-API audience, and the admin REST API accepts any realm token with those roles. AD-6 keeps only "admin consoles" off public ingress; the admin API reachable on the LAN is not named. MFA and break-glass exist only as a Deferred item. No rule binds refresh-token storage, lifetime or `offline_access` for hosted McpCli, and the McpCli spine keeps bearers in plaintext.
- **Scenario:** A prompt-injected agent holding the Administrator's McpCli token, or its refresh token read from disk, calls `/admin/realms/<prod>/groups/<admission>/members` and admits an attacker, bypassing "production realm changes are Administrator-only" without touching an executor.
- **Fix:** Add to AD-6/AD-14: "Identity-administration authority is held by principals distinct from every application principal used with McpCli, UIs or agents. Realm-contract clients disable full scope and map audiences and roles explicitly; tokens from agent-capable clients never carry realm-management roles or the admin-API audience. The Keycloak admin API is reachable only from a declared Administrator path. Every Administrator and deputy authority (GitHub, Keycloak administration, OpenBao, registry, DNS, backup store, custody) requires phishing-resistant MFA; break-glass credentials are sealed and alert on use. Public clients never receive `offline_access`; hosted refresh material has bounded idle and maximum lifetime and lives in an OS credential store, never in a profile file."
- **Sources:** SEC-3, prior:SEC-11 (security)
- **Notes:** The refresh-storage clause overlaps C-29, which restores memlog V-27.

### C-14: Environment-layer Dapr Components and bootstrap Secrets have no namespace any identity may write

High · autofix · gate: hosted · theme T5 · touches AD-1, AD-3, AD-7, AD-8, Consistency Conventions, Deferred

- **Finding:** AD-1 puts "data-service-bound Components" in the environment layer; AD-8 puts bootstrap Secrets in the data namespace "outside the application deploy identity's scope"; AD-7 scopes the environment-layer identity to "its data namespace". Dapr loads only Components in the sidecar's own namespace, and `secretKeyRef` and `dapr.io/app-token-secret` resolve in that namespace, so sidecars in the application namespace find no state store, pub/sub, secret store or app-channel token. The Dapr Vault component takes one `vaultToken` or `vaultTokenMountPath`; with EventStore AD-24's singleton `openbao` component, per-app tokens need per-pod token files mounted through `dapr.io/volume-mounts`, which is not recorded.
- **Scenario:** Every hosted sidecar starts without components and secret-gated readiness fails. The only workarounds break AD-7 (the environment-layer identity writes the application namespace) or AD-8/AD-1 (the application deploy identity writes Components and Secrets, which then roll back with the package). Implementers fall back to the shared OpenBao token observed today.
- **Fix:** Add to AD-7/AD-8: "The environment-layer identity also holds namespaced write on `components.dapr.io` and on the named bootstrap Secrets in its environment's application namespace; the application deploy identity holds neither. Data services, OpenBao and volumes stay in the data namespace." Record `vaultTokenMountPath` plus `dapr.io/volume-mounts` as the seed per-app token mechanism and qualify it in the Secrets owned-work row.
- **Sources:** ADV-1, RV-16, prior:U19 (adversarial, reality)
- **Notes:** RV-16 proposed defer for the token mechanism; it rides along as a seed note. Related: C-20 (the deploy identity can mount any Secret in its namespace) and C-44 (Aspire publisher output).

### C-15: Forward-only inputs derived from declarations have no expand/contract rule

High · autofix · gate: hosted · theme T5 · touches AD-1, AD-3, AD-6, AD-15, Consistency Conventions, Release and Recovery Acceptance

- **Finding:** The realm contract is "applied forward-only by Administrator before the attempt that needs it", and "Every environment-layer input a release needs is applied forward-only and verified before the attempt". AD-15's protection covers only catalog, idempotency, key and secret state. Realm-contract application and Memories operator-artifact changes are not in the Attempt ownership lock list. Nothing keeps these inputs compatible with the running baseline and its rollback set.
- **Scenario:** Parties N renames role `parties-reader` to `parties.read`; Platform generates realm contract v5 and Administrator applies it forward-only; production N-1 loses authorization outside any attempt, with no verification window or recovery, and the next pre-check fails "healthy production". The same happens with a renamed Component or dropped scope, and with Memories operator-artifact routing tuples keyed by component name.
- **Fix:** Extend AD-15: "Every forward-only input derived from declarations - realm-contract clients, roles, audiences and claims; environment-layer Components with their names, scopes and metadata; secret-contract entries; the Memories operator artifact - stays expand-only relative to the working baseline and the prepared rollback set until the candidate is recorded working; contraction happens in a later attempt." Add "realm-contract application and operator-artifact change" to the Attempt ownership lock list.
- **Sources:** ADV-2, prior:ADV-6, prior:U17, prior:U18 (adversarial)

### C-16: Immutable qualified digest sets make every environment-layer patch invalidate existing releases

High · discuss · gate: hosted · theme T5 · touches AD-2, AD-3, AD-15, Consistency Conventions, Release and Recovery Acceptance, Deferred

- **Finding:** The release record binds "qualified environment-layer digest sets" and is immutable. Production profile: "A release is deployable, and a valid rollback target, only while current environment-layer versions fall within its qualified sets ... otherwise the change is breaking." An environment-layer attempt "ends by re-verifying the working release and issuing a renewed production-promoted record at the new digest", but a patch digest cannot be in a set written before it existed. The sidecar patch is neither bound in the release record nor pinned per workload; memlog V-13 ("workloads pin the Dapr sidecar runtime image") is missing from the spine.
- **Scenario:** OpenBao 2.6.3 is applied; working baseline R7 was qualified on 2.6.2. Executor A refuses R7 as rollback target and forces R8, and every later candidate, into approved mode; executor B promotes R8 automatically. Both claim compliance. A control-plane patch changes the working release's sidecars at the next pod restart without re-verification.
- **Fix:** Record the chosen option. Add: "Each release record binds the Dapr sidecar patch, which workloads pin; an environment-layer change is breaking for any retained rollback target or recovery-point release it would put outside the defined sidecar skew (see C-43)." Fold into the EventStore "renewed production-promoted records" confirmation (Owned work L314). Decision: see C-16 under Decisions needed.
- **Sources:** ADV-3, OPS-4, prior:U5 (adversarial, operability)

### C-17: The shared-infrastructure tier is undefined: membership, qualification venue and failure path

High · discuss · gate: hosted · theme T5 · touches AD-1, AD-3, AD-6, AD-7, AD-8, Consistency Conventions, Release and Recovery Acceptance, Deferred, Frontmatter/Memlog

- **Finding:** The environment layer is "a separate per-environment definition" (AD-1), includes "CRDs and other cluster-scoped objects" (AD-3), pins Keycloak and the Dapr control plane (Production profile) and is changed by a data-namespace identity (AD-7/AD-8), while shared-infrastructure changes hold both locks with a cluster-scoped identity. "Staging first" qualifies nothing for components both environments share (Keycloak, Dapr control plane, ingress controller, certificate issuer, CNI, storage provisioner, the node). Kubernetes minor, node OS, CNI, ingress, cert-manager, OpenEBS and operators have no inventory pin, so their changes bypass the profile digest, staging-first and re-verification. A failed re-verification after a forward-only change yields only "retain and report". The shared Keycloak server (one process, database and admin API for both realms) is not classified as production-critical, and nothing stops a whole-server restore to repair staging.
- **Scenario:** The Kubernetes 1.34 to 1.35 upgrade required before G1 brings a CNI change that alters NetworkPolicy enforcement; nothing re-runs the NFR-3 negative tests and isolation is silently lost. A Dapr control-plane patch breaks injection and production stays down, "retained and reported". A staging realm repair from yesterday's Keycloak database backup re-admits a user revoked from production today.
- **Fix:** Add a Release tiers table under AD-3 (application package, environment layer, shared infrastructure, outside every release) with contents, version authority, change path and rollback, and point AD-1, AD-3 and Production profile at it. Shared infrastructure: every cluster-level component is an inventory pin with one version authority; changes run as a named shared-infrastructure workflow under both locks after a complete recovery point, re-run the working release's smokes and the NFR-3 negative tests, and a failed verification is non-working (forward revert or DR entry); a failed currency check blocks automatic promotion. The Keycloak server, database and backups are production-critical with no staging write, backup or restore access; staging realm recovery regenerates from the realm contract; a whole-server restore is DR-class under both locks with revocation replay. List the shared Keycloak server, Dapr control plane and ingress controller as accepted residual risks. Record Keycloak and control-plane placement in the memlog. Decision: see C-17 under Decisions needed.
- **Sources:** PR-101, OPS-3, SEC-11 (pragmatism, operability, security)
- **Notes:** Kubernetes 1.34 reaches end of life on 2026-10-27, so this is urgent. C-23 adds the ingress-controller replacement to the same tier.

### C-18: The staging DNS zone holds shared-infrastructure names, so the staging ACME credential can mint registry and issuer certificates

High · discuss · gate: hosted · theme T5 · touches AD-8, Consistency Conventions, Deferred

- **Finding:** Hosted interfaces: staging uses `hexalith.com`, each zone has one owner, certificates use per-environment ACME credentials, and "The staging namespace can neither serve production or shared-infrastructure hostnames (registry, identity issuers) nor obtain their certificates". But the registry is `registry.hexalith.com`, inside the staging zone; a namespaced Issuer must hold its DNS-01 credential as a Secret in the staging namespace; and the production issuer host is no longer bound to a production-controlled name (V-25 was widened to "shared"). Hostname admission does not stop controller-configuration injection (snippet annotations) on the shared ingress controller that holds both environments' TLS Secrets.
- **Scenario:** A staging compromise uses the zone-wide DNS-01 token to issue a publicly trusted certificate and repoint DNS for the registry or an issuer host outside the cluster, where admission policy cannot see it; production login is phished, or JWKS is served to gateways resolving through public DNS and production tokens are forged.
- **Fix:** "Shared-infrastructure and production-trust hostnames live in a zone or delegated subzone whose DNS and ACME credentials no staging identity, namespace or executor holds; staging ACME credentials cover only delegated staging names; the production realm issuer uses a production-controlled name; the FQDN pattern reserves shared names; application-namespace Ingress objects may set only host, path, TLS and backend fields, and controller-configuration injection is rejected at admission." Decision: see C-18 under Decisions needed.
- **Sources:** SEC-7, ADV-12, prior:SEC-6 (security, adversarial)
- **Notes:** SEC-7 is high only if an identity-issuer host sits under `hexalith.com`; otherwise medium. Today the issuer is `auth.tache.ai` and the registry is under `hexalith.com`. The ingress-controller choice (C-23) sets the admission rule for configuration injection.

### C-19: Whole-installation outage triggers (credential expiry, OpenBao seal, disk) have no owner or early warning

High · discuss · gate: hosted · theme T6 · touches AD-7, Consistency Conventions, Deferred

- **Finding:** The off-site monitor covers only the probe, freshness and stale attempts. Time-bound trust material has no inventory, owner or expiry check: Dapr Sentry root and issuer certificates (one year by default; expiry breaks mTLS in both environments), ACME certificates and DNS credentials, Kubernetes PKI, realm keys, runner registrations, synthetic and recovery-executor credentials, domain registrations. The unseal custodians are people, so after any node restart both OpenBao instances stay sealed until someone acts; the spine neither chooses auto-unseal nor records manual unseal as accepted, and nothing monitors seal state. Single-node local storage has no headroom warning.
- **Scenario:** The generated Dapr root certificate expires; every sidecar in staging and production loses mTLS; the probe opens an issue within five minutes and an operator discovers the renew-and-restart procedure under outage pressure. A kernel-patch reboot outside coverage leaves production down until someone unseals.
- **Fix:** "Every time-bound certificate, credential, token and domain registration in the recovery inventory and the profile has a named renewal owner; the off-site monitor warns a declared lead time before each expiry and at every drill. Each environment declares its OpenBao seal policy; the monitor reports seal state and node volume headroom." Decision: see C-19 under Decisions needed.
- **Sources:** OPS-7 (operability)
- **Notes:** The expiry inventory and monitor checks are wording fixes under either option.

### C-20: Module-supplied code runs inside jobs that hold production deploy, environment-layer or recovery credentials

High · autofix · gate: hosted · theme T8 · touches AD-7, AD-8, Consistency Conventions, Release and Recovery Acceptance

- **Finding:** AD-7 says test code "never runs on an executor ... that holds or held production credentials", yet the production executor runs "digest-identified smoke suites that passed staging" (module-authored code with NuGet closures) inside the attempt job that holds the deploy identity; environment-layer and shared-infrastructure attempts also re-verify; the recovery executor runs module hooks and smokes. Passing staging is not a trust property. Any identity that can create pods in the application namespace can mount every Secret there, so AD-8's "outside the application deploy identity's scope" is not achievable as written.
- **Scenario:** A compromised test dependency in a module's smoke suite activates only when the hostname ends in `tache.ai`, reads the job's kubeconfig or OIDC token, creates a pod that mounts the OpenBao token Secrets and exfiltrates production application secrets.
- **Fix:** Add to AD-7: "Module-supplied code - smoke suites, E2E, recovery hooks - runs in an isolated sandbox (separate OS user or container) with no access to the job's credential material, runner workspace, job token or container runtime. The sandbox receives only short-lived tokens minted from the synthetic clients and the declared verification endpoint; deploy, environment-layer, cluster-scoped and custody credentials never enter it." Add to AD-8: "The application deploy identity is secret-equivalent for its namespace, which is why it is per-job and never co-resident with module code."
- **Sources:** SEC-4 (security)

### C-21: "Authenticated Administrator record" has no defined authentication, and ops-repo write paths are unbounded

High · autofix · gate: hosted · theme T8 · touches AD-7, Consistency Conventions, Release and Recovery Acceptance

- **Finding:** Administrator records gate approved-mode releases, promotion-stop clearance and attempt takeover, but the spine never says what authenticates them. On GitHub Free the private operations repository has no branch protection or environments, so "writable only by Administrator" depends on every credential that can write it: workflow job tokens with `contents: write`, the monitor's PAT or App for issues, deploy keys. The commit author is free text. The notification and dead-man repository is unnamed; the public Platform repository would expose issue content and auto-disable schedules after 60 days.
- **Scenario:** The off-site monitor's classic PAT (repo scope, needed to create issues) leaks from the internet-facing host; the attacker commits a "cleared" promotion stop authored as Administrator, or edits a deployment workflow that the production executor then runs with production credentials. A job token read by a smoke suite (C-20) does the same.
- **Fix:** "Every Administrator record is signed by an Administrator-held identity that no executor, workflow token, monitor or deputy holds; executors verify the signature before acting; write access to storage never authenticates a record. Ops-repo workflows run with read-only repository permissions; no PAT, App or deploy key outside Administrator's devices can write the operations repository. Notifications and the dead-man check use a private repository through an issues-only credential."
- **Sources:** SEC-5 (security)
- **Notes:** The signing mechanism is an implementation seed. C-61 covers the dead-man schedule caveats.

### C-22: The GitHub Free accepted risk omits the publication-side trust root

High · discuss · gate: hosted · theme T8 · touches AD-2, AD-7, Consistency Conventions, Deferred, Frontmatter/Memlog

- **Finding:** Deploy trust rests on attestations minted in the public Platform repository, but the accepted risk ("GitHub Free with a single-writer operations repository") reasons only about the operations repository. Live checks today: Platform has three writers/admins; `main` has no branch protection and no rulesets; the Builds `Protect main` ruleset lets all three bypass; organization rulesets are refused on Free, while repository rulesets are available for public repositories on Free. An attestation proves the named workflow ran at the named ref, not that the ref was reviewed. AD-2 speaks of "the protected publication workflow and ref"; the ref is not protected today.
- **Scenario:** Any of the three accounts, or a stolen token, pushes a modified publication workflow to Platform `main` and mints attestations that pass `--signer-workflow`/`--source-ref` verification; the production executor deploys the output.
- **Fix:** Add a before-first-publication owned-work row for the chosen control; reframe the accepted risk to "GitHub Free: repository-level rulesets only; publication repositories have N writers and named bypass actors; operations repository single-writer"; record the observed state in the memlog. Decision: see C-22 under Decisions needed.
- **Sources:** RV-1 (reality)

### C-23: The shared ingress controller is the retired ingress-nginx, and shared-layer components are missing from the currency rules

High · discuss · gate: hosted · theme T8 · touches AD-8, Consistency Conventions, Deferred, Frontmatter/Memlog

- **Finding:** The live IngressClass `nginx-public` names controller `k8s.io/ingress-nginx`, which Kubernetes SIG Network retired in March 2026 (no further releases or security fixes; repository archived). It fronts `auth.tache.ai` (the only identity issuer for both environments), both registries and `kube.hexalith.com`. Traefik v3.7.13 is also installed. Calico v3.31.3 (the NetworkPolicy enforcement point), Zot v2.1.20, Velero v1.18.2 and Kubernetes patch 1.34.9 are behind and absent from the Infrastructure currency row, which breaks the spine's own "within upstream support and current on security patches" rule.
- **Scenario:** The ingress that terminates TLS for the production issuer and gateway gets no security fixes, and the Kubernetes minor upgrade due before 2026-10-27 needs CNI and controller changes nobody has scheduled.
- **Fix:** Add the ingress controller, CNI, registry and backup tooling to Infrastructure currency with a G1 gate; record the replacement as an Administrator-owned shared-infrastructure change in C-17's tier; record the observations in the memlog. Decision: see C-23 under Decisions needed.
- **Sources:** RV-2 (reality)
- **Notes:** The controller choice sets the admission rule for configuration injection in C-18.

### C-24: The DR sequence omits the steps that make step 3 executable, and Keycloak restore authority is unresolved

High · discuss · gate: recovery · theme T6 · touches AD-6, AD-12, Consistency Conventions, Release and Recovery Acceptance

- **Finding:** No DR step reproduces the environment layer on the prepared capacity (cluster, CNI, Dapr control plane and trust root, OpenBao unseal, data services, broker, ingress, issuer) or deploys the recovery point's release, yet recovery hooks are recovery-scoped startup tasks that run inside deployed workloads. Keycloak, the environment OpenBao and the registry replica have no place in the sequence. Keycloak is one server holding master, staging and production realms; restoring its database is a master-level, cross-realm operation that AD-6 bars ("no automation, executor or fixture holds master or cross-realm admin"), and it brings the staging realm onto production recovery capacity. The observed Keycloak database has no backup today (memlog L129). No step re-provisions the broker or catches subscribers up, though "database backup alone does not recover broker backlog". DNS and certificate cutover is unplaced.
- **Scenario:** Parties writes its hook assuming Dapr state and OpenBao are up; Memories assumes its hook runs before any workload starts; the executor deploys after step 3 and the Parties hook fails. The order is settled ad hoc during the first drill, on RTO time.
- **Fix:** Add step 1b: "Reproduce the environment layer from the profile inventory on the prepared capacity; restore Keycloak and the environment OpenBao into quarantine; deploy the recovery point's release from the off-site registry replica by digest, with ingress closed, promotion stopped and external-effect and destructive workers disabled. Recovery hooks may assume exactly this state." "Broker re-provisioning and subscriber catch-up from restored checkpoints precede step 5; DNS cutover belongs to step 6." Record the Keycloak restore authority chosen below. Decision: see C-24 under Decisions needed.
- **Sources:** OPS-6 (operability)
- **Notes:** Decide together with the deputy carve-out (C-09) and the Keycloak classification in C-17.

### C-25: The DR fence proves against authorities on the dead node, misses the surviving ones, and revocation replay is incomplete

High · discuss · gate: recovery · theme T6 · touches AD-6, AD-12, Release and Recovery Acceptance

- **Finding:** Step 1 revokes the old environment's "database, broker, OpenBao, backup-write and deployment credentials" and proves old credentials fail, but with the node dead those authorities are unreachable and the proof is vacuous. The fence never covers authorities a returning node can still write: the off-site tombstone mirror, external providers, the Keycloak event-export sink, the attempt-record store, the registry and tenant-key store access. Realm signing keys rotate only in a compromise-driven restore, so a partly alive old Keycloak keeps minting tokens the restored gateways accept. Replay covers Keycloak events only; module-owned authorization revocations acknowledged inside the RPO window (for example Tenants membership removals) are neither re-applied nor listed as an accepted exception, while FR-9 requires "denial of revoked principals".
- **Scenario:** The old node boots three hours into DR; its Folders workers resume provider mutations with still-valid production credentials; its Memories instance appends to the off-site mirror, two lineages write, and Memories fails closed after reopening. A user removed from a tenant 40 minutes before the failure regains access, and the DR report says nothing.
- **Fix:** "The fence revokes the old environment instance's credentials at every authority that survives the failure - off-site backup store, tombstone mirror, external providers, Keycloak event-export sink, registry, record store and tenant-key store - and rotates realm signing keys unless the old issuer is proven unavailable. The old node stays isolated from network and DNS until re-imaged. The fence proof runs against those surviving authorities before step 2; old in-environment credentials are invalidated by step 4's rotation and proven against the restored instances." Add the revocation rule chosen below. Decision: see C-25 under Decisions needed.
- **Sources:** OPS-5, SEC-9, prior:OPS-12, prior:SEC-5 (operability, security)

### C-26: DR gives module-owned dynamic credentials two writers and has no principal re-provisioning step

High · autofix · gate: recovery · theme T6 · touches AD-12, Consistency Conventions, Release and Recovery Acceptance, Deferred

- **Finding:** DR step 4 says "Rotate every restored credential" and Platform "owns scopes and acknowledged rotations", so the recovery executor rotates Memories' per-tenant dynamic credentials directly, although Memories AD-6 is their sole writer and verifies rotation through its own lifecycle. On replacement capacity Redis and FalkorDB rebuild empty, with no per-tenant ACL principals; no hook creates them (the hook contract lists "quarantine admission, purge, rebuild, integrity, external-effect reconciliation"), and creation tasks never run on replacement-capacity recovery. Step 3 rebuilds before step 4 rotates.
- **Scenario:** Memories' lifecycle evidence disagrees with OpenBao; rebuild-only replay has no principals; the step-5 smokes on the synthetic tenant fail, or pass only after an unplanned operator repair outside the measured four-hour RTO.
- **Fix:** Add "backend-principal and dynamic-credential re-provisioning" to the recovery hook contract. DR step 4 rotates module-owned dynamic namespaces only by invoking the owning module's hook, which runs after admission and purge and before rebuild-only replay, in declared-dependency order.
- **Sources:** ADV-7 (adversarial)

### C-27: A privileged CI runner shares the single production node

High · discuss · gate: G1 · theme T8 · touches AD-7, AD-8, Deferred, Frontmatter/Memlog

- **Finding:** The live namespace `forgejo-runner` is labelled `privileged` and runs the Forgejo runner with Docker-in-Docker containers set `privileged: true` on node1, the only node, which also hosts Keycloak, OpenBao, the Dapr control plane and Memories. A privileged container is root on the node. AD-7 and AD-8 accept co-scheduling as a "single-node shared kernel" residual risk, a premise that assumed `restricted` pods.
- **Scenario:** Anyone who can run a Forgejo Actions job on `repository.tache.ai` gets node root and with it every Secret, OpenBao volume and sidecar identity of both environments.
- **Fix:** Record the observation in the memlog, and record the chosen disposition as an owned-work row or an accepted risk. Decision: see C-27 under Decisions needed.
- **Sources:** RV-4 (reality)

## Medium and low clusters

Full finding, scenario and fix text for each is in [findings.json](findings.json).

| ID | Title | Severity | Disposition | Gate | Sources |
| --- | --- | --- | --- | --- | --- |
| C-28 | EventStore AD-29 is credited with an asynchronous original-actor attestation it does not define | medium | autofix | module-adoption | RB-9, RV-9 |
| C-29 | Module-spine contradictions left unrecorded: McpCli settings and harness, Projects AD-30, Memories deploy assets | medium | autofix | module-adoption | RB-10, RC-11, prior:ADV-10, prior:U28 |
| C-30 | The module declaration omits exposure class and required authorization, and FR-2's 'module configuration' is not mirrored | medium | autofix | module-adoption | RB-5, RC-18 |
| C-31 | AD-4's Platform-submodule identity has no brownfield path, and owned work removes the only instance | medium | discuss | module-adoption | BF-4, prior:BRN-13 |
| C-32 | Non-gateway and infrastructure-admin cutover: decision form, owner and interim admin path are unclear | medium | discuss | hosted | RC-9, RC-16 |
| C-33 | Legacy retirement evidence is narrower than FR-12, and the Deferred row lost the MVP scope and the AD-14 gate for new transports | medium | autofix | hosted | RC-7, RC-8 |
| C-34 | Incidents after the verification window do not set the promotion stop | medium | autofix | hosted | RB-6, OPS-8 |
| C-35 | The record model is undefined: kinds, writers, cardinality, promotion-stop home and production-promoted lifecycle | medium | discuss | hosted | PR-106, ADV-11, OPS-14 |
| C-36 | The grace, timing anchors and takeover are under-specified | medium | autofix | hosted | OPS-9 |
| C-37 | Staging data from an unadopted candidate can block every later gate | medium | discuss | hosted | OPS-10 |
| C-38 | The planned recovery for an approved incompatible release has no execution rules | medium | discuss | hosted | OPS-11, RC-15 |
| C-39 | The rollback generation's content and the recovery render are mis-specified | medium | autofix | hosted | ADV-9, prior:ADV-5, prior:U2, prior:U16 |
| C-40 | "Breaking" is defined in five places, and the module-facing rule is hidden in AD-3 | medium | autofix | hosted | PR-104 |
| C-41 | Observability has no environment-isolation or failure-domain rule, and its gate is late | medium | autofix | hosted | RB-13, OPS-16 |
| C-42 | WorkflowAccessPolicy is alpha, matches callers by app ID only, and is open until a policy is loaded | medium | autofix | hosted | RV-5, prior:RV-5 |
| C-43 | Dapr patch pinning, sidecar skew and HotReload posture are undefined | medium | discuss | hosted | RV-6, RV-13, PR-122 |
| C-44 | The Aspire Kubernetes publisher renders connection strings into Secrets and Helm values | medium | autofix | hosted | RV-7 |
| C-45 | AD-8's negative-test list does not bind the NFR-3 matrix | medium | autofix | hosted | SEC-12 |
| C-46 | Composed-host deliverables are gated on Folders, and a one-time ratification is written into AD-13 | medium | autofix | hosted | RB-8, PR-125 |
| C-47 | Provenance chain gaps: module images unattested, predicate inconsistent, transitive actions tag-pinned | medium | autofix | hosted | BF-5, SEC-13, RV-17, PR-117 |
| C-48 | The registry is anonymously readable, a second registry exists, and the composed image name collides with EventStore's | medium | discuss | hosted | RV-8 |
| C-49 | The NFR-2 response-coverage rule is loose | medium | autofix | recovery | RB-12, RC-10 |
| C-50 | The post-DR handover and degraded posture are undefined | medium | discuss | recovery | ADV-10, OPS-15 |
| C-51 | Custody classes converge on the persistent recovery executor | medium | autofix | recovery | SEC-10 |
| C-52 | A complete recovery point omits tenant-key coverage and event-export lag | medium | autofix | recovery | OPS-12 |
| C-53 | The drill cannot prove the RTO as written | medium | autofix | recovery | OPS-13 |
| C-54 | Keycloak admin and master-realm endpoints, and a cluster console, are on public ingress today | medium | autofix | G1 | RV-3 |
| C-55 | The AD-11 amendment is unrecorded: no memlog entry, no [AMENDED] tag, proposal missing from sources, status still final | medium | autofix | none | RB-14, RC-14, RC-17 |
| C-56 | Build-substrate findability: undefined terms, an AD-only capability map, single-paragraph Rules and rationale inside Rules | medium | autofix | none | PR-110, PR-113, RB-15, PR-109, PR-112, PR-120 |
| C-57 | Duplication residue: rules restated in two to five homes | medium | autofix | none | PR-105, PR-107, PR-108, PR-114, PR-115, PR-116, PR-118, PR-119, PR-121, PR-123 |
| C-58 | The realm contract omits token-exchange preconditions, and the memlog's Keycloak version is stale | low | autofix | module-adoption | RV-14 |
| C-59 | The synthetic-tenant flag has no owning shape that aggregators can read | low | autofix | hosted | ADV-13 |
| C-60 | Application-level caller allowlists still key on bare app IDs | low | autofix | hosted | SEC-14 |
| C-61 | The dead-man check's repository and schedule are unspecified | low | autofix | hosted | RV-15 |
| C-62 | Stack and brownfield statements no longer match the code | low | autofix | none | BF-6, RV-10, RV-11, RV-12, RB-16, PR-126, prior:BRN-10 |
| C-63 | Downstream documents and the declaration companion trigger need a re-sync | low | defer | none | RC-19, PR-124 |
| C-64 | Brownfield residuals already covered by owned work | low | ignore | none | prior:BRN-3, prior:BRN-12 |

## AD impact map

| Decision or section | Clusters | Max severity | Cluster IDs |
| --- | --- | --- | --- |
| AD-1 | 9 | high | C-03, C-14, C-15, C-17, C-29, C-42, C-43, C-44, C-56 |
| AD-2 | 10 | high | C-05, C-11, C-12, C-16, C-22, C-35, C-47, C-48, C-50, C-57 |
| AD-3 | 9 | high | C-12, C-14, C-15, C-16, C-17, C-40, C-43, C-56, C-57 |
| AD-4 | 5 | high | C-01, C-02, C-04, C-30, C-31 |
| AD-5 | 2 | high | C-02, C-57 |
| AD-6 | 15 | high | C-06, C-09, C-10, C-12, C-13, C-15, C-17, C-24, C-25, C-52, C-54, C-56, C-58, C-59, C-62 |
| AD-7 | 12 | high | C-09, C-14, C-17, C-19, C-20, C-21, C-22, C-27, C-50, C-51, C-56, C-57 |
| AD-8 | 14 | high | C-08, C-14, C-17, C-18, C-20, C-23, C-27, C-41, C-42, C-44, C-45, C-56, C-57, C-60 |
| AD-9 | 1 | high | C-08 |
| AD-10 | 5 | high | C-02, C-04, C-29, C-31, C-57 |
| AD-11 | 12 | high | C-01, C-03, C-04, C-06, C-07, C-13, C-28, C-29, C-32, C-33, C-55, C-56 |
| AD-12 | 7 | high | C-09, C-24, C-25, C-26, C-51, C-52, C-57 |
| AD-13 | 5 | high | C-02, C-35, C-46, C-48, C-56 |
| AD-14 | 10 | high | C-03, C-06, C-07, C-13, C-28, C-29, C-32, C-33, C-56, C-58 |
| AD-15 | 9 | high | C-07, C-11, C-15, C-16, C-37, C-38, C-39, C-40, C-57 |
| Consistency Conventions | 36 | high | C-01, C-02, C-03, C-04, C-07, C-08, C-09, C-10, C-12, C-14, C-15, C-16, C-17, C-18, C-19, C-20, C-21, C-22, C-23, C-24, C-26, C-30, C-33, C-35, C-37, C-39, C-40, C-41, C-43, C-44, C-47, C-48, C-56, C-57, C-59, C-61 |
| Release and Recovery Acceptance | 28 | high | C-05, C-09, C-10, C-11, C-12, C-15, C-16, C-17, C-20, C-21, C-24, C-25, C-26, C-34, C-35, C-36, C-37, C-38, C-39, C-40, C-43, C-49, C-50, C-51, C-52, C-53, C-56, C-57 |
| Source Precedence | 9 | high | C-01, C-03, C-04, C-06, C-28, C-29, C-46, C-55, C-57 |
| Stack | 2 | medium | C-43, C-62 |
| Deferred | 36 | high | C-02, C-03, C-04, C-05, C-06, C-07, C-08, C-09, C-11, C-13, C-14, C-16, C-17, C-18, C-19, C-22, C-23, C-26, C-27, C-28, C-29, C-31, C-32, C-33, C-41, C-42, C-44, C-46, C-47, C-48, C-54, C-57, C-58, C-62, C-63, C-64 |
| Frontmatter/Memlog | 13 | high | C-01, C-03, C-09, C-10, C-17, C-22, C-23, C-27, C-54, C-55, C-56, C-58, C-62 |
| Design Paradigm | 4 | high | C-09, C-30, C-56, C-57 |
| Capability Map | 2 | medium | C-55, C-56 |

## Notes

- lint_spine.py returned ok=true with 0 findings.
- The reality reviewer verified some facts against the live Kubernetes cluster read-only (kubectl get), plus read-only gh api calls and anonymous registry GETs; it changed no cluster, Git or registry state.
- This validation changed no spine, memlog, PRD, source or submodule file; only findings.json and validation-report.md were written in reviews/validate-2026-09-27-r2/.
- Several reviewers describe the AD-11 amendment as uncommitted; it was committed in f733aa9 (2026-09-27 22:52), but the memlog still ends at '(event) spine finalized', so the provenance findings stand.
- Sources include the 127 findings of this run plus 27 prior-run items that reviewers marked partial or still open, prefixed 'prior:' (prior validate/update adversarial IDs, including the U-series; prior security, operability and reality IDs; brownfield BRN-n). Reconcile's drift-table rows A-1 to A-15 are not separate sources; they map into RC findings.
- Gate earliness used for ordering: now, module-adoption, hosted, recovery, G1, G2, G3, none.
- Declined items PR-01, PR-37 and PR-38 and the accepted deferrals RV-13 and RV-14 from the update run were not re-raised.
- Possible overstatement: C-12 (PR-103) changes no rule and could be medium; C-18 (SEC-7) is high only if an identity-issuer host sits under hexalith.com.
- ad_map rows Design Paradigm and Capability Map are added beyond the requested sections because several clusters touch Roles, Terms and the FR map.
