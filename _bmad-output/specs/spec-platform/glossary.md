# Glossary

Product terms come from the PRD. Architecture terms point to their spine definitions and are not redefined here.

## Product terms

**Scope and composition**

- **Platform:** the shared Hexalith environment-provisioning and hosting solution this spec defines.
- **Module:** a Hexalith component whose domain behavior stays in its own repository.
- **Module candidate:** a committed module source revision proposed for acceptance. Its CI evidence identifies both expected artifact content identities and the artifacts the environment actually loaded.
- **Domain module:** a module that provides domain behavior. Its minimum environment is at least EventStore, Tenants and Memories; Parties is one example. Technical modules and tools are classified in the spine's Design Paradigm.
- **MVP module set:** EventStore, Tenants, Parties, Folders, Projects, McpCli and Memories.
- **Complete environment:** the MVP module set plus the supporting components needed to run it.
- **Module workspace:** the active Tenants, Parties, Folders or Projects repository, used for that domain module's Platform development and testing.
- **Platform workspace:** the Platform repository used as the active root. EventStore, Memories and McpCli are run and debugged from source there.
- **Active root repository:** the repository work is performed from. Its direct submodule declarations decide which dependencies are initialized.
- **Minimum environment:** the components a module developer declares as required for development or testing, in addition to the module itself.
- **Module configuration / declaration:** the module-owned, versioned input that lists the servers Platform enables. The spine's Module declaration convention enumerates its fields.
- **Enabled module:** a module whose services are enabled in the selected environment.
- **Enrolled module:** a module with the declarations and qualification evidence needed for inclusion in a Platform composition at the relevant stage. Repository presence alone is not enough, and local or CI enrollment does not qualify a module for production.

**Access and environments**

- **Supported interface:** a module-declared external surface with explicit exposure and authorization rules that Platform qualifies for the selected environment.
- **Agent-eligible operation:** a module-declared command or query permitted through McpCli. UI-only and human-confirmation operations stay ineligible, even for a human CLI caller.
- **McpCli:** the caller-hosted CLI and stdio MCP client that exposes enabled modules' agent-eligible operations through EventStore, with selected-environment contract matching and authorization.
- **Staging:** the hosted environment under `hexalith.com`, where checks gate production promotion.
- **Production:** the hosted environment under `tache.ai`.
- **Production admission:** Administrator's explicit, recorded grant through the human production-admission group. Its revocation always survives restoration.
- **Production user:** a user explicitly admitted to production. Being a staging user never confers this status or any production permissions.
- **SM-4 admission test identity:** the designated synthetic identity temporarily admitted through the human production-admission path between G1 and G2. Its record expires by G2 and its revocation is followed by a denial check.
- **Synthetic check identity:** a standing identity admitted through the synthetic-admission group, restricted to the synthetic tenant and used for production-safe smoke checks.

**Testing**

- **Isolated test:** a module-owned unit or focused component test that runs without Platform and may use lightweight test doubles.
- **Readiness:** a service is initialized and able to handle its intended requests. A running process is not enough. Full business-flow validation belongs to integration and E2E tests.
- **Integration test:** a test that exercises a module against its configured real services in a Platform environment.
- **Critical business flow:** a module-owned business operation, or essential service or tool behavior, designated for mandatory E2E verification before production promotion.
- **E2E test:** an end-to-end test that verifies a business flow through the deployed services that deliver its outcome.
- **Smoke test:** a short, production-safe check, chosen and maintained by an enrolled module, that verifies essential behavior after deployment or rollback.
- **Attachment hold:** the finite, recorded period during which an accepted attached run defers automatic cleanup of its owner environment.
- **Withdrawn run:** an attached run ended because its environment was stopped or its hold expired. It is neither a pass nor a failure and is never valid integration evidence.

**Release and recovery**

- **Release:** an identified complete composition of enrolled modules, retained application artifacts, compatible configuration and required check suites, including unchanged modules and dependencies.
- **Working release:** an identified release, with compatible versioned configuration, that has passed production readiness and smoke verification.
- **Working baseline:** the retained release identity plus the verified attempt record of the current working application. Recovery combines its application artifacts with compatible current environment authority. Spine form: the release record plus the attempt record the latest-working pointer names.
- **Empty or degraded production:** production is empty without a working baseline. It is degraded when the last outcome was non-working or a recorded incident, over-bound probe failure or failed pre-update health check establishes that it is no longer working. Reduced-recovery state is distinct.
- **Promotion stop:** the per-environment durable block on promotion. Each new cause advances its revision; only Administrator clears an observed revision after naming every cause resolved or accepted and confirming a verified current working release and admission reconciliation. An eligible verified empty or degraded attempt may clear only the stop it observed.
- **Verification window:** the five minutes after rollout readiness, during which availability and smoke results are checked before deployment or recovery is declared successful.
- **Recovery owner:** Administrator, the product owner. Receives GitHub notifications and intervenes when automatic recovery fails or cannot be verified.
- **Recovery deputy:** the named person with independent recovery and key access who receives every notification and can declare a stop, restore, verify and restore the recorded pre-incident ingress state. Cannot approve releases, clear stops or grant or revoke production admission.
- **Manual recovery:** an in-place recovery started by Administrator or the deputy that re-deploys the working baseline or restores an approved recovery point without a release attempt; the stop stays set.
- **Named data-restore recovery:** the approved in-place Recovery sequence used when retained production data lack valid compatibility evidence for a candidate. Its approval records acceptance checks, maximum duration and a usable post-lock recovery point.
- **Recovery point:** a complete, verified set of module and shared-dependency recovery artifacts at one declared cut, with compatible release and configuration and the required security and erasure context. The spine's RRA "Recovery point and freshness" row gives the full definition.
- **Reduced-recovery state:** the state after a verified disaster restore, lasting until replacement capacity, a repeat exercise and Administrator's return to G2 conditions are recorded. It is not degraded production.
- **RPO:** the maximum target age of recoverable data at the time of failure.
- **RTO:** the maximum target duration from outage to verified restoration, including detection, response, capacity, restore and validation.
- **Response coverage:** the published hours and time zone in which the primary and deputy commit to responding within a declared acknowledgement bound. It decides whether the four-hour RTO applies, without pausing the outage clock.
- **Shared-infrastructure currency check:** comparison of deployed shared components with the supported, security-current profile inventory. Failure blocks automatic promotion.

## Architecture terms (spine)

**Roles**

- **Administrator:** the production authority, recovery owner and Platform architecture owner; the only approver of releases, clearer of the promotion stop and administrator of production admission (spine Roles).
- **Recovery deputy:** receives every deployment-failure, recovery, backup and monitor notification and may execute documented recovery, verify and reopen (spine Roles).
- **Named writers:** the only writers of the operations repository: Administrator and the second organization owner (spine Roles; AD-7).

**Tooling and composition**

- **Platform tool, runner:** Builds' `hexalith-module` (spine Terms; AD-4, AD-10). The Builds package "release record" is distinct from this spine's release record.
- **Mode:** source (local, Debug) or package (CI, Release); AD-4.
- **Platform identity:** AD-4 "Platform identity".
- **Environment descriptor and result contract:** AD-5 and AD-10; carries run and owner identity, expected and loaded artifacts, candidate revision or baseline-only label, attachment holds and withdrawn outcomes.
- **Composed `eventstore` host, gateway:** AD-13. The *gateway* is the host's authenticated endpoint; a *Gateway* is an environment's Gateway API object (spine Terms; Hosted interfaces).
- **Extension package:** AD-13.
- **Intake manifest, breaking change:** spine "Module intake".
- **Catalog generation:** spine "Catalogs".
- **Legacy surfaces, new surfaces:** AD-11.

**Release tiers and records**

- **Release tiers** (application package, environment layer, shared infrastructure, outside every release): spine "Release tiers".
- **Release record:** AD-2.
- **Binding classes; attempt, qualification, production-promoted and Administrator records; latest-working pointer; promotion-stop records:** spine "Binding classes and records".
- **Attempt, lock, epoch:** RRA "Attempt ownership".
- **Rollback set, rollback generation:** AD-15.
- **Production preconditions:** spine "Production preconditions".

**Release modes and recovery**

- **Release modes (automatic, Administrator-approved):** RRA "Release modes".
- **Empty or degraded production:** RRA "Empty or degraded production".
- **In-place recovery:** RRA "In-place recovery".
- **Staging reset:** RRA "Staging reset".
- **Promotion stop:** RRA "Promotion stop".
- **Recovery sequence, recovery executor:** spine "Recovery sequence"; its in-place forms cover data restore, and its replacement-capacity forms cover disaster recovery. AD-7.
- **Reduced-recovery posture:** RRA "After DR".

**Identity and gates**

- **Surface classes (`ui`, `agent`, `service`):** AD-14.
- **Human production-admission group, synthetic-admission group, synthetic tenant:** AD-6; spine "Synthetic identities".
- **G1, G2, G3:** RRA "Production entry gates"; see [sequencing.md](sequencing.md).
- **Owned work gates:** spine "Owned work"; see [sequencing.md](sequencing.md#owned-work-by-gate).

## Cluster management terms

- **Management/local cluster:** the dedicated private single-node K3s cluster hosting Rancher and its management backup operator, distinct from the existing workload cluster, with no HA claim.
- **Registered/imported/downstream cluster:** the existing kubeadm workload cluster registered in Rancher, retaining native lifecycle and external-etcd recovery. Import is not a distribution migration or cluster rebuild.
- **Independent native admin path:** private Kubernetes/SSH access with independently retained identity/key custody that works without either management console. A Rancher-proxy kubeconfig is not this path.
