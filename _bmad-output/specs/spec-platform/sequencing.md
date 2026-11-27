# Sequencing

These are the ordering constraints taken from the spine, which is authoritative, and from the PRD. Spine rows keep the spine's own names so they can be traced back; acceptance boundaries stay in the spine and are not restated. "Blocks" columns are derived from the spine's Capability → Architecture Map; if the spine changes, re-derive this file.

## Production entry gates

The gates run strictly in the order G1 → G2 → G3. The PRD's "before production use" means G2; "before enabling automatic production promotion" means G3.

**Release modes** (RRA Release modes). *Automatic* requires G3, complete compatibility evidence and a passing shared-infrastructure currency check. *Administrator-approved* uses a recorded Administrator decision in place of the automatic G3 and compatibility triggers and serves pre-G3 releases, SM-5 fault rehearsals before G2, empty or degraded production, and releases without demonstrated safe rollback. Both modes require their own current release-admission currency result under policy v1. An approved attempt with a failed result requires the complete known failing set to be named in its Administrator record. For approved retained-data attempts, require valid baseline compatibility or a qualified named data restore, plus a complete, usable FR-9 point cut and verified under the production lock before workload mutation. At G1, retained-data repair uses compatibility. Before G3, every production release uses the Administrator-approved mode. The staging gate, lock, provenance, verification and records apply in both modes.

### G1 — Production deployed, ingress closed

**Permits:** deployment into the production application namespace with ingress closed:

- Production hostnames admit only executor and probe sources.
- The human production-admission group is empty.
- GitHub delivery works, and the off-site probe runs from G1 onwards, sending pre-G2 notifications.

**Required first** (spine Owned work):

- **G1 rows:**
  - *Exposure, DNS and certificates:* exposure paths and ingress-closure mechanism, DNS zone owners, staging HTTP-01 and production ACME credentials, the internal executor verification endpoint, the retained registry's controls and off-site replica, the off-site monitor host, notification repository and dead-man workflow, actual GitHub issue delivery, and removal of public Keycloak admin, master-realm and cluster-console routes, verified by an external negative probe.
  - *Infrastructure currency:* supported Kubernetes current patch before staging; accepted 4.27 KubeSphere retirement and 4.28 private Rancher/agent/management-cluster qualification with native-access and initial restore evidence; every inventory pin current; renewal owners and monitor lead times for every expiring certificate and credential.
  - *Forgejo runner relocation* off the cluster node, never shared with an executor.
  - *Monitor and admission bounds:* the availability-probe stop bound and its locked-attempt treatment, identity-provider event-capture lag and two-way admission-check cadence; mismatch notification and refusal to clear or approve while records and live admission differ.
- **First production attempt row:** before the Story 6.7 foundation, Story 6.9 verifies production attempt-store access from the production executor and off-site monitor and records a successful authenticated, read-only probe from the designated Story 8.1 recovery host against the production store endpoint. The G1 deployment also needs provenance, interruption, epoch and timing controls, one recovery, the in-place recovery entry point, signed records and stale-attempt detection.
- **Before the first production release:** Story 6.14 delivers the Story 9.2 evaluator and release-admission integration before Story 6.23. It uses the ratified Story 4.9 inventory, deployed infrastructure and production providers from Stories 6.6 and 6.7, and Story 6.9's current lock and epoch. First release admission runs the evaluator before mutation and retains the result for that attempt, release mode and epoch.
- The G1 deployment runs as an approved first empty installation with no retained application data: an Administrator record names the reason, staging proves fresh installation, and the candidate is prepared and ready-validated without a baseline rollback set. Failure removes candidate workloads, keeps any created data and leaves ingress closed (RRA Empty or degraded production; AD-15).

**Checks at G1:** the NFR-3 negative checks that need neither an admitted user nor open ingress:

- staging pods cannot reach production services, sidecars or data;
- a staging release that declares a production hostname is rejected.

**SM-4 synthetic grant (after the G1 deployment, before G2):**

- Administrator may temporarily add one designated synthetic test identity to the human production-admission group, solely for SM-4 positive evidence.
- That identity holds production permissions limited to the synthetic tenant and is exercised only from executor and probe sources while ingress stays closed.
- The grant record expires no later than G2, and its revocation record is followed by a denial check.
- Drills use isolated identity copies. This does not open G2.

**Blocks:** production-side rehearsal paths for CAP-7 and CAP-8; the production half of CAP-10, which stays closed.

### G2 — Users admitted, ingress open

**Permits:** admitting production users and opening production ingress.

**Required first:**

- The AD-12 drill passes, including Memories tombstone and key continuity (Memories is in every MVP composition). Real fence steps have been rehearsed on staging.
- SM-4 isolation and access checks pass, with positive evidence from the G1 synthetic grant.
- Recovery access, capacity, deputy alert delivery and response coverage are verified.
- **G2 rows** (spine Owned work):
  - *Recovery capacity and coverage:* capacity and its location; published response coverage; the recovery executor host with credential custody and the pinned off-site copy of recovery workflows and environment definitions; independent artifacts, access and keys; the key-custody mechanism; fence-and-reissue owners for every surviving authority; the deputy's identity, minimum permissions, key custody, alert delivery and rehearsed restore and reopen; Keycloak database backup; capacity budget and representative data sizes.
  - *Memories conformance and continuity:* tombstone and key continuity, tenant-key store, per-tenant principals and operator artifact, the tombstone-mirror fence hook, the adapter boundary, and migration of other Memories Redis coordination to Dapr (transitional until G2 under AD-9).
  - *Reduced-recovery operating policy:* whether automatic promotion may resume before return to G2 conditions, restore-exercise cadence while replacement capacity is consumed, and staging re-establishment.
- Telemetry sink retention and off-site durability (spine Owned work, Telemetry sink).

**Evidence:** SM-4 and SM-6 before opening.

**Opening order:**

1. Before G2, SM-4 passes: the G1 synthetic grant supplies positive production evidence, and the AD-12 drill verifies both access directions in the restored environment on isolated identity copies.
2. Open ingress and admit the first user (e.g. Administrator). This is an access-configuration change, so SM-4 access outcomes are re-verified (RRA Verification).
3. On the live production realm, run the SM-4 positive check with that user, and confirm that staging users and staging-issued tokens are rejected through public ingress.
4. Admit anyone else only after step 3 passes. If it fails, G2 aborts and the admitted user is removed.

**Blocks:** acceptance of CAP-9; CAP-10, CAP-11 and CAP-12 in production.

A candidate-specific named restore belongs to admission of the applicable retained-data release attempt. G2 uses the isolated disaster-recovery drill and the standing controls listed above. An attempt that relies on named restore needs the separately qualified in-place capability after G1 and the matching candidate qualification before rollout.

### G3 — Automatic promotion

**Permits:** automatic production promotion.

**Required first:**

- The SM-5 rehearsals pass.
- EventStore confirms the AD-15 retention entries, the catalog activation order, and production-promoted record invalidation and renewal (spine Owned work, First production attempt).
- The shared-infrastructure currency policy fixes the checked inventory, allowed lag and effect on approved attempts. Qualify the release-admission, drill and weekly triggers and the Story 9.6 currency rehearsals before G3.

**Suspension:** a change to verification, recovery or rollback-set policy suspends automatic promotion until the affected SM-5 rehearsals repeat.

**Evidence:** SM-5.

**Blocks:** the automatic path CAP-6 → CAP-7 → CAP-8.

### Around the gates

- The Kubernetes upgrade precedes staging; delay blocks staging and does not authorize using 1.34 past its 2026-10-27 end of life. Sequence 4.26 retirement qualification → 4.27 KubeSphere retirement → 4.1 supported native hop → 4.28 private Rancher qualification → staging. Public administrative closure remains independently urgent. VM procurement and Rancher availability do not block native retirement/upgrade; current backup, compatibility, consistency, smoke and exact procedure gates remain binding (spine Cluster management, Release tiers, Accepted risks).
- From G1, a pending shared-infrastructure change is first rehearsed on a production-profile copy on the prepared capacity during the monthly drill, except an urgent security patch applied in place with a recovery point and an Administrator record (spine Release tiers).
- After DR, production runs in a recorded reduced-recovery posture until new prepared capacity is identified, a drill repeats and Administrator records the return to G2 conditions; the promotion stop stays set until staging is re-established (RRA After DR).

## Owned work by gate

Spine Owned work, in gate order. The spine holds each row's acceptance boundary.

| Gate | Work | Owner | Blocks |
| --- | --- | --- | --- |
| First Platform-accepted tool version | Platform tool ratification (`hexalith-module`; versions through 4.27.4 not accepted) | Builds with Platform | Any workspace pin; CAP-1–CAP-5 |
| Before accepting local/CI composition and testing evidence | Runner lifecycle, ownership and resource isolation | Builds with Platform and module owners | CAP-4 evidence (SM-3) |
| Before accepting CI integration evidence | Runner adoption and integration qualification under candidate and attachment policy v1 | Platform with Builds | Candidate and attachment evidence for CAP-4 (SM-3) |
| First publication | Publication and operations repository controls | Administrator | CAP-6–CAP-8 |
| First local enrollment | Local Dapr and hosting conventions (local mTLS, per-receiver ACL, scoped components; canonical ServiceDefaults and Aspire Dapr package set) | Platform, consulting EventStore and Commons | CAP-1–CAP-4 |
| Module adoption | Source/package adoption (direct declarations including `references/Hexalith.Platform`; canonical McpCli enrollment; fallback removal) | Platform, Builds, module owners | CAP-2, CAP-3 (SM-2) |
| Module adoption | Dapr component-name injection and declarative subscriptions | Module owners, with EventStore | Aspire-to-Helm qualification; hosted enrollment (CAP-10) |
| Module adoption | Hosting inventory | Module owners with Platform | Legacy hosting retirement |
| Module adoption | Connected McpCli | McpCli, EventStore Contracts, Platform | CAP-12 (FR-12 acceptance), SM-1 |
| Module adoption | Legacy MCP/CLI migration and retirement; generic administration and resource contracts | McpCli, EventStore, FrontComposer, module owners, with Platform | Legacy surface retirement (AD-11) |
| First staging deployment | Composed host (Folders adapters packaged before Folders joins) | EventStore, Platform; Folders | CAP-1, CAP-6–CAP-8, CAP-12 |
| Before hosted use | Story 4.5 qualifies the selected maintained chart on the Parties, EventStore, Tenants and Memories graph. All three application modes contain zero Dapr Components. The environment writer applies the eight reference Components from the baseline/candidate union. Exercise install, upgrade, recovery and removal with the intended identities. | Platform | CAP-6, CAP-10 |
| First publication | Story 4.12 verifies the published package’s OCI digest and attestation. | Platform and Builds | CAP-6 |
| Off-site retention | Story 4.13 retrieves the same package by digest from the replica and verifies its attestation for recovery. | Platform and Administrator | CAP-8, CAP-9 |
| First staging deployment | Shared runtime and profile evidence (profile ratification; durable broker, Redis pub/sub excluded). Before the Story 4.22 reference release, use the Story 4.9 Dapr inventory that passed the Story 4.5 chart checks, Story 4.16 staging envelope checks and Story 4.17 broker qualification for the same runtime candidate. | EventStore and Platform with consumers | CAP-6–CAP-8, CAP-10 |
| First staging deployment | Secrets, identity, network and transport | Platform, Administrator, module owners | CAP-10–CAP-12 |
| First staging deployment | Staging data restore and takeover fencing | Platform, Builds | Staging reset; CAP-6 and CAP-8 recovery qualification |
| First staging deployment | Telemetry sink (retention and off-site durability before G2) | Platform | CAP-7–CAP-9 diagnostics |
| First staging evidence | Qualify enforcement of staging evidence policy v1 | Platform with Builds, module owners | CAP-6 |
| First intake for publication | Story 4.8 verifies each module image digest, owner repository, workflow, protected ref and pinned Builds workflow identity. Missing or mismatched attestations refuse admission before Story 4.12 writes the release record. | Builds, EventStore, Memories and applicable owners supply attestations. Platform verifies them. | CAP-6 |
| Each production admission in either mode | Story 5.5 supplies the decision-bound full-inventory scan. Stories 5.11 and 6.14 require the latest scan issued for the pending decision to be complete. A newer incomplete scan blocks fallback to an earlier pass. Check each image’s independent age and current finding exceptions under the [adopted vulnerability policy](../../../docs/contracts/image-and-dependency-vulnerability-policy-v1.md). | Platform with Builds | CAP-6, CAP-7 |
| First production attempt | Release state, checks and notifications (SM-5 and EventStore confirmations also gate G3) | Platform, Builds, Administrator | CAP-7, CAP-8; G1 deployment |
| First approved retained-data attempt | Complete Story 8.7 recovery-point capability with Stories 8.4–8.6 inputs, plus compatibility or named data restore | Platform with Builds and Administrator | Approved retained-data CAP-8 attempt, including compatible G1 repair |
| Before the first production release | Shared-infrastructure currency at release admission (Stories 6.14 and 9.2) | Platform with Administrator | Story 6.23, subsequent production release admissions in both modes |
| G1 | Monitor and admission bounds | Platform with Administrator | G1 |
| G1 | Exposure, DNS and certificates | Administrator | G1 |
| G1 | Infrastructure currency | Administrator with dependency owners | G1 |
| G1 | Forgejo runner relocation | Administrator | G1 |
| Working production and first DR drill | Weekly checks start at working production (Story 6.23), independently of the first and monthly drill checks (Story 8.17) | Platform with Administrator | Weekly operations and drill evidence |
| G2 | Recovery capacity and coverage | Administrator with deputy and dependency owners | CAP-9; G2 |
| G2 | Reduced-recovery operating policy | Administrator with Platform architecture | G2 |
| G2 | Memories conformance and continuity | Memories, EventStore, Platform | CAP-9; G2 |
| G3 | Qualify enforcement of shared-infrastructure currency policy v1 | Platform with Administrator | G3 |
| Upstream documentation maintenance | Folders, Projects and McpCli override records | Respective module documentation owners | Platform completed the [October 1 reconciliation](#source-document-alignment) for CAP-6–CAP-9 |
| Trigger | GitHub Team controls; additional McpCli transports and other AD-gated items; broker change, standby, scale or universal in-transit TLS | Per spine row | Outside the MVP |

## First shared versions

Module adoption epics wait for the rows they consume (spine First shared versions). The "Blocks" column is derived.

| Artifact | Owner | Consumers | Must precede | Blocks |
| --- | --- | --- | --- | --- |
| Declaration schema and validator: the next `hexalith.module-manifest` major carrying the Module declaration fields | Platform semantics, Builds implementation | All modules | First Platform-accepted `hexalith-module` version or any workspace pin; any module enrollment | Every CAP |
| EventStore extension API with supported majors | EventStore | Folders, Agents, modules with extensions | AD-13 composed host | CAP-1, CAP-6–CAP-8, CAP-12 |
| AD-13 ratification and composed-subject issuer registration | EventStore | Platform | Composed host release-available | CAP-6–CAP-8 |
| Routing catalog schema with per-operation schema digests, surface eligibility and retention entries | EventStore.Contracts | Platform, gateway, McpCli | Catalog generator, McpCli discovery | CAP-1, CAP-8 (AD-15), CAP-12 |
| Gateway metadata endpoint | EventStore | McpCli | FR-12 acceptance | CAP-12 |
| Admission-time original-actor and originating-surface attestation, extending EventStore AD-29 | EventStore | Projects, gateway, McpCli, modules with cross-module steps | First asynchronous or confirmed cross-module step; FR-12 acceptance | CAP-12 |
| Admission predicate and projection (human group, or synthetic group for the synthetic tenant only, including the staging-only lifecycle clause; declared maximum staleness) | EventStore | Gateway, modules with asynchronous steps | First hosted predicate enrollment; first asynchronous cross-module step for projection | CAP-11, CAP-12 |
| Realm contract with surface classes, client-to-surface map, token-exchange permissions and preconditions, and synthetic tenant identifier | EventStore claims, Platform instance | All modules, McpCli | Local realm and hosted enrollment | CAP-1, CAP-10–CAP-12 |
| Test environment descriptor and result contract: run/owner identity, expected and loaded artifact identities, candidate revision or baseline-only label, attachment holds and withdrawn outcomes | Platform, implemented in `hexalith-module` | EventStore.Testing, module and McpCli tests | Fixture migration and candidate or attachment evidence | CAP-4 |
| Check-suite invocation and result contract | Builds | Modules, executors | Staging gate | CAP-6–CAP-8 |
| Recovery hook contract (cut and recovery-class positions, recovery mode, quarantine admission, purge, re-provisioning and rotation, fence hooks, rebuild, integrity, external-effect reconciliation, reopen-before-replay, attempt/epoch-bound internal endpoint and task commits, result shape) | Platform | All modules | First staging deployment; first AD-12 drill proves it | CAP-8, CAP-9 |
| Profile template and per-release binding split | EventStore with Platform | Deployment workflows | First staging deployment | CAP-6, CAP-7, CAP-10 |
| Release, attempt, qualification and Administrator record encoding | Builds | Publication and deployment workflows | First staging deployment | CAP-6–CAP-8 |
| Attempt lock, record and promotion-stop store | Platform with Builds | All executors, monitor | First staging deployment | CAP-6–CAP-8 |
| McpCli migration inventory schema and generic administration/resource contract | McpCli with EventStore | FrontComposer, module owners | Any legacy MCP/CLI retirement | Legacy surface retirement (AD-11) |

## Other ordering rules

**Build and release order (AD-13, AD-11)**

1. EventStore extension API and Contracts codec
2. Module Contracts and extension packages
3. Platform intake manifest
4. Composed `platform/eventstore` image and McpCli candidate
5. Catalog generation
6. Release record and package
7. Staging validation
8. Stable McpCli publication by Platform

**Lifecycle states**

- Staging accepts candidates whose EventStore server package is evidence-validated.
- A production release attempt requires the server package and the composed `platform/eventstore` image to be release-available (spine Production preconditions 5).

**Staging attempt order** (RRA Staging gate, Staging reset, Empty or degraded production)

- Cut a staging point before candidate writes and run the complete exact-release E2E suite on every path. The approved empty or degraded exception requires exact-candidate fresh-install evidence. A compatible working baseline takes the AD-15 rollback rehearsal with HotReload off. An approved retained-data attempt without valid compatibility rehearses the named data restore with the retained inputs, procedure, authority, checks, maximum duration and post-cut treatment. It prepares no automatic rollback set. Either approved retained-data branch also requires a complete, usable production point cut and verified under the production lock before workload mutation.
- Unreadable or unproven candidate writes, including writes with no production baseline, require a verified staging reset before a same-candidate rerun or a later candidate starts. Restore the earliest point since the last verified reset taken before any of that candidate’s writes. Keep the point and its artifacts while the candidate can require reset or a recovery, reset or named recovery references them, even after evidence expiry. A later point containing candidate writes is ineligible. A failed reset keeps the attempt blocked. Proven baseline-readable writes require no reset.
- Under [staging evidence policy v2](../../../docs/contracts/staging-evidence-policy-v2.md), after every production check passes and before any rollout effect, the authorized production writer conditionally reserves the pending decision for the exact release, attempt and epoch. A conflicting staging start or reset uses the same durable ordering point. If staging wins, production refuses before mutation. If admission wins, staging waits. Verified success completes the decision with the working-pointer transition. A terminal non-working outcome releases the reservation and leaves the decision pending under the existing stop and recovery rules. Uncertainty retains the reservation until fenced takeover or terminal reconciliation. Queued staging work reevaluates the decision, baseline and recovery state. Replays are idempotent and stale epochs fail. An explicit Administrator reservation remains distinct. Builds and Platform must explicitly adopt the exact committed Platform revision and the v2 policy bytes together with the pinned v1 base and qualify both changed cases and existing refusals before using it. Each result keeps its policy identity. V1 evidence retains its original meaning.

**Environment layer and shared infrastructure** (spine Release tiers)

- Environment-layer objects the candidate needs are applied by their own attempt, staging first, before the candidate's release attempt. Precondition 6 compares infrastructure digests across environments and verifies exact resolved profile identities. An affected working production baseline needs both renewed production records. The candidate receives its own production records at rollout.
- After the staging foundation is accepted, Stories 6.8 and 6.9 supply the production executor and off-cluster attempt and lock store. Pull forward Story 8.1's off-site host selection, reservation, hardening and store network path. Story 6.9 requires a successful read of a designated non-sensitive record from that host against the production store endpoint, through the intended network path and TLS trust, using an authenticated short-lived identity scoped to the probe. Retain the host, endpoint, time, scope and result. Missing or failed proof blocks Story 6.7. Repeat it if the host, endpoint, network path or trust changes before the foundation uses it. Story 8.9 commissions the host after usable Story 8.7 recovery points and the other recovery inputs exist.
- Under FR-10, before production has workload data or a working baseline, Story 6.7 bootstraps the exact production-only foundation with Story 6.22's named create-only procedure. It holds both locks, records an attempt per environment, and requires Administrator authorization and current verified native recovery evidence. The shared-infrastructure identity creates the production namespaces, StorageClass and PriorityClass. Administrator establishes the reviewed environment-layer grants before the environment writer applies its resources. Preserve the working staging release and isolation and keep production user ingress closed. Story 6.23 owns the subsequent application deployment.
- Ordinary shared-infrastructure changes run as a named workflow under both locks after a complete recovery point. The first-production foundation scope excludes existing staging and shared service objects, existing grants and cluster defaults.
- A staging failure blocks only that candidate.

**Before SM-2 can be demonstrated**

- A Platform-accepted `hexalith-module` version exists and is pinned.
- Each domain module (Tenants, Parties, Folders, Projects) adds Platform as a direct submodule and a module declaration.
- McpCli is already declared in Platform at `references/Hexalith.McpCli` ([brownfield.md](brownfield.md)); its Platform-workspace path still needs canonical McpCli enrollment (Owned work, Module adoption).

**Before a module is enrolled in its environment and release checks (PRD)**

The module developer supplies:

- the server list and readiness checks;
- timeout overrides;
- supported surfaces with agent eligibility;
- critical-flow E2E suites and production-safe smoke suites;
- the authoritative, rebuild-only and erasure inventories and integrity hooks.

**PRD downstream-owner rows** (each keeps the PRD's own boundary; spine rows supply the mechanism)

- **Module declarations, surfaces, suites, inventories and hooks:** each module developer, before qualification for each target environment.
- Before release-gate qualification, the module owner and Platform approve removals or remaps against the exact before/after coverage. Tenant-lifecycle changes also require Tenants review under the [staging evidence policy v1](../../../docs/contracts/staging-evidence-policy-v1.md).
- **Tenant lifecycle:** Tenants owner with module owners and Platform reviews tenant-lifecycle flows, qualifies cleanup or reporting of leftover synthetic staging tenants before the release gate and provisions the production synthetic tenant before first production verification.
- Before accepting the first staging release evidence, Platform and Builds qualify attempt triggers, full reruns, retained history and the 24-hour freshness check in the [staging evidence policy v1](../../../docs/contracts/staging-evidence-policy-v1.md).
- **Root-source mapping, declaration and export contracts, runner ownership and CI lifecycle:** Platform with Builds and module maintainers, including candidate loaded-identity and attachment rules, before accepting local/CI composition and testing evidence.
- Builds adopts the [candidate evidence and attachment policy v1](../../../docs/contracts/candidate-and-attachment-policy-v1.md) in its runner and workflows. Platform accepts the tool version and qualifies decision binding, the refusal of cross-decision evidence reuse and enforcement of finite attachment holds before accepting CI integration evidence.
- **McpCli enrollment, contract matching, surface and actor authorization, named demonstrations:** Platform, McpCli and EventStore, before FR-12, SM-1 and SM-4 acceptance.
- **Identity and state separation, automation permissions, network isolation, explicit admission:** Platform with Administrator and module maintainers, before G2 and after access changes; controlled test admission precedes general opening.
- **Supported infrastructure, ingress, DNS and certificates, executor and probe access, monitoring, GitHub delivery:** Administrator with Platform, before G1.
- **Retained artifacts, evidence age, attempt serialization, compatibility rehearsal, interruption and recovery bounds, notification delivery, promotion-stop clearance:** Platform with Builds, EventStore and Administrator, before applicable production attempts; SM-5 before G3.
- Before G1, Platform and Administrator decide the probe-stop bound, handling of failures during a locked attempt and two-way admission check cadence in Story 6.2. Story 4.19's isolated export slice supplies representative measurement before Story 4.9 ratifies the bound. Story 4.19 then qualifies staging export against that bound.
- Platform and Administrator qualify release admission under the [shared-infrastructure currency policy v1](../../../docs/contracts/shared-infrastructure-currency-policy-v1.md) before Story 6.23. Start weekly checks against active production using the Story 6.4 private issue channel as soon as 6.23 establishes production. Story 8.17 then uses the evaluator in the first and monthly drills once its recovery inputs are ready. Full qualification and the Story 9.6 policy v1 rehearsals gate G3. Story 9.8 audits each automatic rollout against its signed final passing admission evidence. [Hosted-profile delivery order](../../planning-artifacts/sprint-change-proposal-2026-11-27.md#hosted-profile-v2-delivery-order)
- **Approved retained-data qualification:** Before the first applicable attempt, Platform with Builds and Administrator makes Story 8.7 available with the recovery inventory, backup chains and custody inputs in Stories 8.4–8.6. Prove refusal without qualifying compatibility or named recovery and refusal without a complete usable point cut under the attempt's own lock. Also prove candidate removal on failure, prevention of automatic rollback on the approved empty/degraded path, later-stop preservation, and admission-revocation and Memories-erasure continuity across the named restore.
- **Recovery inventory, independent backups, artifacts and keys, deputy access, response coverage, prepared capacity, representative volume:** Administrator with deputy and dependency owners, before G2.
- **Memories tombstone and key continuity, module recovery reconciliation:** Memories and EventStore with module owners and Platform, before G2 and whenever recovery mechanisms change.
- **Reduced-recovery operations:** Administrator with Platform architecture fixes automatic-promotion resumption, restore-exercise cadence while capacity is consumed and staging re-establishment before G2.

**Legacy retirement** (spine Migration and coexistence; AD-11)

- Domain-module AppHosts stay frozen, with no new cross-module wiring, until the owning module shows source, package and deployed parity and holds EventStore AD-22 authority. Producers are adopted before consumers are retired.
- Legacy MCP/CLI sources stay frozen and outside every Platform composition until their AD-11 retirement evidence passes; the McpCli migration inventory schema precedes any retirement.

## Source-document alignment

Recovery and deployment story authors use the synchronized Platform spec. The remaining upstream documentation work does not gate module enrollment.

- **Spec package:** synchronized on 2026-10-01 with the finalized PRD, addendum, final architecture update 4 and approved Rancher correction, including the retained-data compatibility or named-recovery rule and management authority/recovery boundary. CAP-1–CAP-12, FR/NFR/SM IDs, AD-1–AD-15 references and numeric targets remain stable.
- **PRD and addendum:** architecture adoption, update-4 confirmation and the approved 2026-10-01 Rancher decision are recorded; no stale spec-owner correction remains.
- **Still owned upstream:** Folders, Projects and McpCli documentation override records remain with their module documentation owners under the spine's Source-document alignment row.
