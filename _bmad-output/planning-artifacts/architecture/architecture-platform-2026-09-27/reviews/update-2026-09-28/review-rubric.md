# Rubric review: Platform architecture spine (update run 2, 2026-09-28)

**Verdict: CONDITIONAL PASS.** The spine binds most divergence points tightly and covers FR-1 to FR-12 and NFR-1 to NFR-3, but five high findings must be settled before the CI-tier, hosted and Tenants stories are built: how CI package mode materializes runnable services, Dapr patch transitions, Component reload with HotReload off, encryption at rest, and the scope of the tenant-creation ban.

**Counts:** 0 critical, 5 high, 14 medium, 8 low (27 findings). Dispositions: 19 autofix, 6 discuss, 2 defer.

- **Subject:** `ARCHITECTURE-SPINE.md` as of 2026-09-28 08:45 (status draft, AD-1 to AD-15, 467 lines). Line numbers below refer to that file.
- **Inputs:**
  - `.memlog.md`: run-2 entries from line 211 onward, plus the earlier history they cite
  - `prd.md` and `addendum.md`
- **Brownfield inspected:**
  - Platform `apphost.cs` and `global.json`
  - Builds `README.md`, `schemas/hexalith.module-manifest.v1.json`, `schemas/hexalith.module-descriptor.v1.json`, `src/hosts/*` and `Props/Directory.Packages.props`
  - the EventStore `src/` project list, the Tenants and Parties/Folders/Projects UI projects, and the Projects spine AD-28
- **Not re-raised (settled by the user):**
  - AD-1 Option 1 with the Helm fallback
  - the AD-9 Memories exceptions
  - AD-12 backup-restore
  - the Folders and Projects availability/RPO overrides
  - GitHub Free
  - C-18 staging HTTP-01
  - every batch 1–6 decision
  - PRD thresholds restated verbatim

  Findings that would change any of these are marked **discuss**.

---

## High

### RB-1: AD-4 package mode does not say how CI materializes the Platform composition or the runnable dependency services

- **Severity:** high
- **Location:** AD-4 *Mapping* (L97) and *Platform identity* (L100); AD-5; Owned work "Platform tool ratification" (L439)
- **Finding:** "Root-declared Hexalith dependencies build from source in Debug; all others resolve as packages at the Builds catalog version" and "In CI the Platform composition the tool loads carries its source commit, and the tool refuses to run unless it equals the submodule HEAD; an untagged or dirty submodule is allowed only in local source mode." The spine never says where the CI-mode Platform composition comes from. The publication workflow publishes only the Helm package, the composed image and McpCli, and no row publishes a Platform composition package. It also never says how a *server* dependency runs "as a package": a NuGet library is not a runnable host. Memlog V-08 assumed a Platform-versioned tool; C-02 made the tool Builds-versioned, which leaves this seam open. The spine also states no rule tying a `hexalith-module` version to the Platform commit it can load.
- **Failure scenario:** Builds implements CI mode by embedding a Platform-model snapshot in the `hexalith-module` package. Platform expects the tool to compile the tagged submodule. A Parties workspace then pins a tool whose embedded model disagrees with its submodule, and the identity check fails every CI run. Separately, the runner cannot start EventStore, Tenants and Memories "as packages". One implementer builds their submodule sources in Release, which contradicts "all others resolve as packages". Another pulls images by tag from an unnamed registry. The blocking Platform-runner CI tier (AD-5, AD-10) cannot be built consistently.
- **Proposed fix:** Add an AD-4 *Package mode* clause and pick one option.
  - **Option A (no new artifact):** "In package mode (CI, Release) the tool builds the Platform composition from the Platform submodule source, which must be at a Platform release tag. Root-declared server dependencies build from their submodule source in Release against package references for their libraries; all other dependencies resolve as packages at the Builds catalog version. The Platform composition declares the `hexalith-module` version range it supports, and the tool refuses a composition outside that range."
  - **Option B:** "The publication workflow also publishes a versioned Platform composition package. In CI, dependency servers run from intake-pinned images pulled from the retained registry with CI's read-only pull credential."
- **Disposition:** discuss. This completes the C-02 and C-31 decisions, and the choice is the user's.

### RB-2: Exact sidecar-patch equality has no transition, so a Dapr control-plane patch invalidates every rollback and recovery target

- **Severity:** high
- **Location:** Consistency Conventions / Production profile (L223); AD-2 *Record*; Release tiers, shared infrastructure row; G1 Infrastructure currency (L456); DR sequence step 2
- **Finding:** "The hosted sidecar patch equals the control-plane patch and the release's pinned patch in CI, staging and production", and the release record immutably binds the "Dapr sidecar patch". The control plane, however, is a shared-infrastructure pin that must stay "current on security patches" (G1: "the Dapr control-plane patch … current"). Memlog C-16 refers to a "defined sidecar skew" that the spine never defines.
- **Failure scenario:** A shared-infrastructure attempt patches the control plane from 1.18.2 to 1.18.x for a CVE. From then on:
  - The working release, pinned to 1.18.2, violates the equality rule, so it stops being deployable or a valid rollback target. The next attempt's AD-15 rollback set is invalid.
  - A candidate pinned to the new patch could not have passed staging before the shared change.
  - DR step 2 deploys "the recovery point's release … by digest" onto a control plane reproduced from the current inventory, which breaks the same rule.

  One executor will refuse; another will ignore the check.
- **Proposed fix:** Replace the Production-profile sentence with: "A release's pinned sidecar patch equals the control-plane patch at its staging and production attempts. A later control-plane patch change within the same Dapr minor keeps a release deployable, a rollback target and a recovery-point release once the shared-infrastructure attempt re-verifies it and issues a qualification record for the new control-plane patch. A control-plane minor change is breaking for releases pinned to the previous minor. Local runs report their patch and warn on mismatch."
- **Disposition:** discuss. This refines C-43 and C-16.

### RB-3: Environment-layer Component changes are not loaded before re-verification, because HotReload is off

- **Severity:** high
- **Location:** AD-8 *Dapr* (L138); Release tiers, environment-layer row (L236); Production profile ("the production attempt re-verifies the working release and issues a qualification record")
- **Finding:** "Hosted Configurations … disable HotReload", so sidecars read Components only at start. Environment-layer attempts change "data-service-bound Dapr Components" and then "re-verify the working release". No rule restarts the workloads whose sidecars load the changed Component.
- **Failure scenario:** The G1 move from Redis Stack 7.4 to Redis 8 changes the state-store Component's host in an environment-layer attempt. Running sidecars keep the old Component. Re-verification passes against the old Redis, and a qualification record attests a state that is not running. Days later a node restart or an OOM restarts some pods, which load the new Component. Actors are now split across two Redis instances, outside any lock, attempt or automatic recovery.
- **Proposed fix:** Change the environment-layer "Change path" cell to: "Its own attempt, staging first, applied forward-only by the environment-layer identity; when it changes a Dapr Component, the executor restarts every workload whose sidecar loads that Component, with the application deploy identity, before re-verification."
- **Disposition:** autofix. This implements C-43's intent that Dapr resource changes follow pod rollout.

### RB-4: Encryption at rest and data-service transport encryption are undecided, although Projects assigns both to Platform

- **Severity:** high
- **Location:** Operational envelope, security dimension (absent); Source Precedence (L312, Projects row L322)
- **Finding:** The spine never decides encryption at rest for hosted volumes or TLS on sidecar-to-data-service and adapter-to-data-service connections. The only encryption rules cover backup copies and ingress TLS; Dapr mTLS covers only sidecar-to-sidecar traffic. The Projects spine AD-28 Rule says "The platform AppHost owns … authenticated encryption in transit, managed encryption at rest, identity/KMS/secrets …". Source Precedence overrides only Projects' "AD-28 availability, RPO 0 and service RTO", so this obligation is neither accepted nor overridden.
- **Failure scenario:** Platform stories provision CloudNativePG, Redis and FalkorDB on the single node's local disks, unencrypted, and connect them over plaintext. Projects' G-5 gate ("encryption … evidence") then either blocks hosted enrollment or is silently waived. Staging and production data share one unencrypted disk that also leaves the site on hardware replacement.
- **Proposed fix:** Add a Consistency Conventions row **Data protection**:
  - "Hosted volumes on the node and on prepared capacity use node-level volume encryption with keys under Administrator and deputy custody."
  - "Sidecar and AD-9 adapter connections to data services use TLS issued per environment."

  Alternatively, record both as accepted risks and extend the Source Precedence override to "Projects AD-28 encryption at rest and in transit".
- **Disposition:** discuss. This is a new decision.

### RB-5: "Suites never create or delete tenants" is unscoped, which blocks Tenants' integration tests and critical-flow E2E

- **Severity:** high
- **Location:** Consistency Conventions / Synthetic identities (L227); AD-10 *Default run* (the descriptor supplies "the synthetic tenant identifier", so the row applies to run-owned environments too)
- **Finding:** "Suites never create or delete tenants, and smoke writes use only synthetic identities and data, without external effects."
- **Failure scenario:** Tenants' core business flow is `CreateTenant` (`Hexalith.Tenants.Contracts/Commands/CreateTenant.cs`). FR-6 requires every module to declare a non-empty critical-flow set with staging E2E, and FR-4 requires real-service integration tests. Under the rule as written, Tenants either drops its core flow from the gate or violates the rule in CI and staging. Other module teams apply it differently. The rule came from V-43 and was aimed at per-run tenant churn in production smoke suites.
- **Proposed fix:** Replace the sentence with: "Production smoke suites never create or delete tenants. Staging E2E suites may create and delete only run-scoped tenants that carry the synthetic exclusion marker, within a module-declared tenant-lifecycle critical flow. Run-owned local and CI environments are exempt. Smoke writes use only synthetic identities and data, without external effects."
- **Disposition:** discuss. This rescopes the prior accepted V-43 autofix.

---

## Medium

### RB-6: Several record kinds have no named writer, but Deploy accepts records only from named writers

- **Severity:** medium
- **Location:** Binding classes and records (L219); AD-2 *Promotion*; Workflows and provenance (last sentence)
- **Finding:** AD-2 says "Deploy accepts artifacts, records and evidence only from the writers named in Workflows and provenance", but that row names writers only for artifacts, the release record, attempt records and evidence. The following have no named writer:
  - *Qualification records* ("Platform-issued")
  - Platform-issued *lifecycle records*, meaning the composed image's release-available record and the production-promoted record
  - the *promotion stop* and *latest-working pointer* "set and clear records" (only "the off-site monitor may set it" is stated)
- **Failure scenario:** Builds encodes the production-promoted record as publication-workflow-signed, while the executor writes it at rollout, so production preconditions reject it. Or an executor accepts any writer, and a staging executor writing to the shared record store can forge a production qualification or production-promoted record. The AD-8 negative tests list "records" but have no rule to test against.
- **Proposed fix:** Append to Binding classes and records: "**Writers:** qualification records, production-promoted records and latest-working moves are written by the environment's executor identity within an attempt, or the recovery executor for DR. The composed image's release-available record is written by the publication workflow after staging validation. Promotion-stop set records are written by any executor, the off-site monitor, an Administrator record or the deputy's authenticated declaration. Clear records are written only as Administrator records." Then end the Workflows row with: "Deploy accepts each record kind only from its writer in Binding classes and records."
- **Disposition:** autofix. The release-available writer is a proposed choice.

### RB-7: Two shared-version rows are gated later than their first consumer

- **Severity:** medium
- **Location:** First shared versions, "Attempt lock, record and promotion-stop store" (L432) and "Profile template and per-release binding split" (L430); Owned work, "First production attempt" (L454)
- **Finding:**
  - The lock and record store must precede only the "First production attempt". But Attempt ownership says "One per-environment lock covers every workload-affecting change", and the Staging gate needs "One serialized attempt".
  - The profile template must precede "Production profile proof", a gate that is defined nowhere. Staging "runs the same template".
- **Failure scenario:** The staging executor ships with an ad-hoc lock and file-based attempt records. The production store arrives later with different CAS semantics, and staging evidence ("results carry the serving release …") is bound to records the production workflow cannot read.
- **Proposed fix:**
  - Set both rows' "Must precede" to "First staging deployment".
  - Change the Owned-work cell to "The production instance of the attempt, lock and stop store reachable from the production executor, the recovery executor and the monitor; …".
- **Disposition:** autofix.

### RB-8: Dapr Components that are not bound to a data service have no tier or writer

- **Severity:** medium
- **Location:** Release tiers, environment-layer contents (L236), versus AD-8 *Namespaces* (L135)
- **Finding:** The tier table places only "data-service-bound Dapr Components" in the environment layer. The application package lists "Dapr Configurations, WorkflowAccessPolicies, Subscriptions and Resiliency" but no Components. AD-8 lets only the environment-layer identity write "`components.dapr.io` objects", and "the application deploy identity holds none of these".
- **Failure scenario:** A module declares a cron input binding or an HTTP middleware Component. The AD-1 generator puts it in the application chart, and the deploy fails on RBAC. Or it is dropped, and the module fails readiness. Two module epics resolve this differently.
- **Proposed fix:** In the environment-layer contents, replace "data-service-bound Dapr Components" with "every Dapr Component".
- **Disposition:** autofix. This aligns the table with C-14.

### RB-9: The declaration does not carry the configuration key that delivers each bound component name

- **Severity:** medium
- **Location:** AD-9 *Component names*; Module declaration, **Runtime** (L216)
- **Finding:** "Platform binds every name through configuration … subscriptions read the configured name." The declaration lists "Dapr capabilities by logical role with recovery class" but not where the module reads the name. EventStore, for example, reads `options.Value.StateStoreName` from its own options section.
- **Failure scenario:** Each module picks its own options section. The shared Platform helper cannot inject names uniformly, so the owners doing "Dapr component-name injection" (Owned work) each invent a mapping in the Platform model, which is exactly the per-module plumbing AD-1 prevents.
- **Proposed fix:** Change the Runtime field to: "Dapr capabilities by logical role, each with its recovery class (…) and the configuration key through which the module reads its bound component name."
- **Disposition:** autofix.

### RB-10: Creation startup tasks never run when a module is first enrolled into an existing environment

- **Severity:** medium
- **Location:** Startup task lifecycle (L217); Automatic recovery ("rolling back a first module enrollment removes workloads, never data objects")
- **Finding:** "Creation tasks run only when creating a new environment, never on upgrade, rollback, restore or replacement-capacity recovery."
- **Failure scenario:** Folders enrolls incrementally after production exists, which the PRD explicitly supports. Its once-per-environment creation task never runs, so Folders fails readiness. Alternatively, a runner implementer treats first enrollment as creation, and after a first-enrollment rollback that kept the data objects, re-enrollment re-runs creation over surviving data.
- **Proposed fix:** "Creation tasks run once per module per environment, when the environment is created or the module is first enrolled into it. They never run on upgrade, rollback, restore or replacement-capacity recovery, and they succeed idempotently against data objects that survived a removed first enrollment."
- **Disposition:** autofix.

### RB-11: The AD-4 Platform-identity rule fails in the Platform workspace itself

- **Severity:** medium
- **Location:** AD-4 *Platform identity* (L100)
- **Finding:** "Every workspace that runs the tool declares `references/Hexalith.Platform` directly, and its submodule commit is the single Platform identity." The Platform repository has no such reference, yet FR-2, FR-3 and SM-2 require EventStore, Memories and McpCli to be debugged from source "in the Platform workspace". Memlog C-31 lists only domain modules, McpCli and technical modules.
- **Failure scenario:** The identity check implemented in "Platform tool ratification" rejects the Platform repository root, which blocks SM-2 for three MVP modules.
- **Proposed fix:** "Every workspace other than the Platform repository that runs the tool declares `references/Hexalith.Platform` directly, and its submodule commit is the single Platform identity; in the Platform workspace the identity is the repository's HEAD."
- **Disposition:** autofix.

### RB-12: The FR-2 rule on initializing nested submodules is not bound

- **Severity:** medium
- **Location:** AD-4 *Mapping* and *No fallback*; FR-2 and SM-2 coverage
- **Finding:** AD-4 governs source *resolution* ("no ancestor, sibling, recursive, nested-Platform or package fallback") but not *initialization*. The FR-2 testable consequence is "Dependency initialization follows only the active root repository's direct declarations under `references/`; nested submodules remain uninitialized", and SM-2 checks it.
- **Failure scenario:** The tool, or a workspace setup script such as Builds `Tools/builds-submodule-init.ps1`, runs `git submodule update --recursive`. The embedded Platform's references initialize, resolution still passes, and SM-2 fails, or goes unnoticed because no rule names the check.
- **Proposed fix:** Append to AD-4 *Mapping*: "The tool initializes only the active root's direct `references/*` submodules, never recursively, and fails naming any initialized nested submodule until it is deinitialized."
- **Disposition:** autofix.

### RB-13: The Module declaration row contradicts the Builds manifest it ratifies

- **Severity:** medium
- **Location:** Module declaration (L216); First shared versions row (L420); Owned work "Platform tool ratification" (L439)
- **Finding:** The row says "Each module owns its instance with `schemaVersion`", but `hexalith.module-manifest.v1.json` identifies itself with `"schema": {"const": "hexalith.module-manifest.v1"}` and sets `additionalProperties: false`. The deferred row says "adopt or supersede Builds `hexalith.module-manifest.v1`", which reopens C-02 ("becomes the Platform declaration schema implementation"). The Owned work says "Remove the manifest schema's hard-coded EventStore and Dapr version constants", but the schema also hard-codes `"frontComposerVersion": {"const": "4.5.0"}`.
- **Failure scenario:** A module team adds `"schemaVersion": 2`, and the strict validator rejects it as an unknown field (Builds already has `module-negative-unknown-field-output.json` evidence). The FrontComposer constant survives first publication and pins every module to FrontComposer 4.5.0, bypassing the catalog authority.
- **Proposed fix:**
  - Module declaration: "Each module owns its instance, identified by its `schema` value (`hexalith.module-manifest.v<major>`); Platform accepts the current and previous major."
  - Deferred row: "Declaration schema and validator: the next `hexalith.module-manifest` major carrying the Module declaration fields; …".
  - Owned work: "Remove the manifest schema's hard-coded `platform` version constants (EventStore, Dapr runtime and SDK, FrontComposer)".
- **Disposition:** autofix.

### RB-14: The recovery "declared cut" has no definition that modules can align to

- **Severity:** medium
- **Location:** Recovery point and freshness (L277); First shared versions, "Recovery hook contract" (L429)
- **Finding:** "A usable recovery point is the set of inventory artifacts sharing one declared cut", but the hook contract lists only "quarantine admission, purge, backend-principal and dynamic-credential re-provisioning, rebuild, integrity, external-effect reconciliation, reopen-before-replay declaration, result shape". What a cut is, and how each recovery class aligns to it, is neither decided nor assigned.
- **Failure scenario:** Each module defines the cut differently:
  - EventStore uses a global event position.
  - Folders' PostgreSQL PITR uses a timestamp.
  - Memories uses its register sequence.
  - Keycloak uses database backup time.

  The drill cannot prove "one declared cut", and module hooks reconcile against incompatible cut notions.
- **Proposed fix:** Add to the Recovery hook contract row: "cut identity: the Platform-declared cut and how each recovery class records the position it reached at or before it, including EventStore position and Memories register sequence".
- **Disposition:** autofix.

### RB-15: The rule that only the shared-infrastructure identity creates PVs contradicts the storage provisioner and CloudNativePG

- **Severity:** medium
- **Location:** AD-8 *Namespaces* (L135); Release tiers, shared-infrastructure row ("storage provisioner"); G1 row (CloudNativePG)
- **Finding:** "PersistentVolumes are created only by the shared-infrastructure identity and pre-bound to their environment's claims."
- **Failure scenario:** CloudNativePG creates one PVC per instance (`<cluster>-<n>`, with a new serial when an instance is recreated). Pre-bound static PVs need predictable claim names, so an instance replacement, or the DR environment step, leaves the PVC Pending. The only fix is a shared-infrastructure workflow under both locks, after a complete recovery point that may itself depend on the stuck database. Alternatively, implementers keep the dynamic provisioner and silently violate AD-8.
- **Proposed fix:** "PersistentVolumes are created only by the shared-infrastructure identity or by the storage provisioner through an environment-specific StorageClass; ResourceQuota denies each namespace every other environment's StorageClass, and no claim binds a PV of another environment."
- **Disposition:** discuss. This refines the user-accepted C-45.

### RB-16: Environment-layer and shared-infrastructure definitions have no home, artifact identity or off-site copy

- **Severity:** medium
- **Location:** AD-1 *Dapr resources*; Release tiers (L236–237); AD-7 *Recovery executor*; DR sequence step 2
- **Finding:** Environment-layer Dapr resources are "Rendered from the canonical profile and declarations and placed per Release tiers". The environment layer's version authority is the "Profile inventory environment facet". Neither the rendered environment-layer objects, the data-service charts or manifests, nor the profile inventory, realm-contract instance or secret contract has a stated home or retention. The recovery executor needs "neither operations-repository write nor GitHub", yet only "a pinned off-site copy of the recovery workflows" is guaranteed.
- **Failure scenario:** One Platform story renders environment-layer Components into a second chart in the public Platform repository. An Administrator story keeps data-service manifests in the operations repository, with no digest recorded per attempt. During a DR with GitHub unreachable, step 2 ("Reproduce the environment layer and shared infrastructure from the profile inventory") has no inventory, realm contract or secret contract to work from.
- **Proposed fix:** Add below Release tiers: "Environment-layer and shared-infrastructure definitions — pinned upstream charts or manifests with values, the rendered environment-layer Dapr resources, the profile inventory, the realm-contract instance and the secret contract — live in the operations repository. Each attempt records their digest, and the pinned off-site recovery copy carries them at every retained recovery point's configuration identity."
- **Disposition:** discuss. The home is a new choice.

### RB-17: The environment Gateway's namespace, its writer and the ingress-closed mechanism are undefined

- **Severity:** medium
- **Location:** AD-8 *Namespaces* (L135); Hosted interfaces (L226); Release tiers, environment layer ("the environment's Gateway"); G1; Release modes ("closes user ingress"); DR step 2
- **Finding:** C-23 gives each environment "one Gateway owned by its environment layer" that "admits routes only from its application namespace". But AD-8 lets the environment-layer identity write in the application namespace only "`components.dapr.io` objects and the named bootstrap Secrets". Separately, G1 ("production hostnames admit only executor and probe sources"), first-deployment failure, incompatible-release failure and DR all require closed ingress, but application routes "may set only host, path, TLS and backend fields". No tier, writer or mechanism owns that state.
- **Failure scenario:**
  - If the Gateway lives in the application namespace, the environment-layer identity cannot write it.
  - If it lives in the data namespace, the cert-manager gateway shim creates the Certificate and its HTTP-01 solver HTTPRoute in that namespace, and the Gateway's allowedRoutes rejects the route, so staging certificates never issue.
  - The executor that must "close user ingress" has nothing to toggle, and a Traefik entryPoint allowlist would close staging too.
- **Proposed fix:**
  - AD-8: "The environment-layer identity writes the data namespace plus `components.dapr.io` objects, the named bootstrap Secrets, the environment's Gateway and its certificates in its application namespace."
  - Hosted interfaces: "User-ingress admission (open, or executor and probe sources only) is environment-layer state on the environment's Gateway, changed only by the environment-layer identity within an attempt."
  - Owned work G1 "Exposure, DNS and certificates": add "the ingress-closure mechanism".
- **Disposition:** autofix. This follows C-23.

### RB-18: The surface-class vocabulary and the class-to-eligibility matrix have no single home or owner

- **Severity:** medium
- **Location:** AD-14 *Surface*, *Client types* and *Chains*; Module declaration **Surfaces** ("required surface class"); Catalogs (EventStore owns only the per-operation eligibility vocabulary)
- **Finding:** AD-14 mentions surface classes indirectly ("mapped to exactly one surface class", "agent-capable", "a service class") but never lists them or states which eligibilities each class admits, and no one owns the class vocabulary. Trigger row: step-up flows are outside the MVP.
- **Failure scenario:** Three parties name the classes three different ways: Platform's realm contract uses `ui/agent/service`, the EventStore gateway implements `interactive/public/internal`, and module declarations write `confidential`. The gateway admits step-up operations for UI clients while the realm contract assumes none, and the AD-14 negative tests pass against the wrong matrix.
- **Proposed fix:** Add an AD-14 clause: "*Classes:* Platform owns the surface-class vocabulary in the realm contract:
  - `ui`, for confidential clients: admits agent-eligible, UI-only and confirmation-required operations.
  - `agent`, for public clients: admits agent-eligible operations.
  - `service`, for confidential clients: admits agent-eligible operations, plus a downstream confirmation-required step that consumes the originating confirmation.

  No class executes step-up operations in the MVP." Also add "surface-class vocabulary" to the realm-contract row in First shared versions.
- **Disposition:** autofix.

### RB-19: A small agent given a module-enrollment story has no entry point in the capability map

- **Severity:** medium
- **Location:** Capability → Architecture Map (L395–408)
- **Finding:** The map is organized by FR only. The most common story shape for independent teams, "enroll module X", spans:
  - ADs: AD-4, AD-9, AD-10, AD-11, AD-13 and AD-14
  - Conventions rows: Module declaration, Startup task lifecycle, Module intake, Secrets, Synthetic identities, Diagnostics, and Migration and coexistence

  The FR-1 to FR-5 rows reach only some of these. The FR-7/FR-8 row cites "attempt rows", which is not a section name. NFR-3 omits AD-7 (automation identities), FR-9 omits AD-2 and AD-6 (retention, Keycloak restore and revocation replay), and FR-12 omits AD-4 (local McpCli launch).
- **Failure scenario:** An agent handed "Enroll Folders in local and CI compositions" reads the FR-1 to FR-5 rows. It misses AD-13 (Folders' idempotency adapters must be extension packages), AD-14 surface classes and the startup-task scopes, and ships a Folders-owned `eventstore` project.
- **Proposed fix:**
  - Add the row "Module enrollment (any module) | Module owner's declaration, Platform validator | AD-4/AD-9/AD-10/AD-11/AD-13/AD-14; Module declaration, Startup task lifecycle, Module intake, Secrets, Synthetic identities, Diagnostics and notification, Migration and coexistence".
  - Replace "attempt rows" with "Attempt ownership, Timing and interruption, Rollout, Verification, Automatic recovery, Promotion stop".
  - Add AD-7 to NFR-3, AD-2 and AD-6 to FR-9, and AD-4 to FR-12.
- **Disposition:** autofix.

---

## Low

### RB-20: AD-12 Prevents "raw volume copies", but the binding clause sits in G2 Owned work

- **Severity:** low
- **Location:** AD-12 *Prevents* (L176) and *Model*; Owned work G2 (L458)
- **Finding:** The Prevents line lists "raw volume copies … being treated as complete … recovery". The Rule says only "module-approved recovery sets". The binding clause, "Native database backup and allowlisted metadata/file backup are seeds, not blanket volume restore", lives in an Owned-work acceptance cell.
- **Failure scenario:** A backup story uses Velero file-system backup (Velero is already in the profile inventory) of live PostgreSQL volumes every 30 minutes and counts them as recovery points. The crash-inconsistent copies surface only at the drill.
- **Proposed fix:** Append to AD-12 *Model*: "Recovery units use native database backup or module-allowlisted file and metadata backup; a raw volume or snapshot copy never counts toward a recovery point." Then trim the G2 cell to "Native database backup and allowlisted metadata/file backup are the seeds."
- **Disposition:** autofix.

### RB-21: Load-bearing terms are used before they are defined or indexed, and two collide with brownfield names

- **Severity:** low
- **Location:** Design Paradigm, **Terms** (L36)
- **Finding:** The Terms paragraph indexes binding classes and records only. Undefined or unindexed terms:
  - "environment layer", "shared infrastructure" and "application package", used from AD-1 onward but defined only in Release tiers
  - "attempt", "lock" and "epoch"
  - "rollback set", used in AD-3 before AD-15
  - "mode" in AD-4 and "artifact mode" in AD-10, never defined
  - "gateway", the `eventstore` host, versus "Gateway", the Gateway API object; the diagram labels the latter "Environment Gateway"
  - "CI" in AD-5 ("CI's only retained-registry access is a read-only pull credential"), versus the publication workflow, which also runs on hosted runners and needs push access

  The Builds README also says "The semantic-release version and package hashes are the release record", which collides with this spine's release record.
- **Failure scenario:** A small agent implementing AD-5 denies the publication workflow its push credential, or grants CI one. An EventStore implementer reads "the selected gateway's metadata endpoint" as a Traefik Gateway feature.
- **Proposed fix:** Append to Terms: "**Release tiers** (application package, environment layer, shared infrastructure) are defined in Release tiers; **attempt, lock and epoch** in Attempt ownership; the **rollback set and rollback generation** in AD-15. **Mode** is source (local, Debug) or package (CI, Release). The **gateway** is the composed `eventstore` host's authenticated endpoint; a **Gateway** is the environment's Gateway API object. **CI** means module and Platform test workflows, never the publication workflow. The Builds package 'release record' is distinct from this spine's release record."
- **Disposition:** autofix.

### RB-22: Several rules still have two or three homes

- **Severity:** low
- **Location:**
  - Record writers: AD-2 *Promotion*, Binding classes and records, and the last sentence of Workflows and provenance
  - Environment-layer change path: the Release tiers change-path cell, and Production profile ("An environment-layer change is its own attempt, staging first; …")
  - Shared-infrastructure change path: Release tiers, and Attempt ownership ("Shared-infrastructure changes hold both locks, staging first, … under one named change owner")
- **Finding:** C-57's de-duplication left these rules with more than one home, and the copies differ slightly: only Attempt ownership has "named change owner", and only Release tiers has "after a complete recovery point".
- **Failure scenario:** A later edit updates one copy. For example, adding the RB-3 restart to Release tiers but not to Production profile leaves a small agent reading the stale copy.
- **Proposed fix:**
  - Keep the writers only in Binding classes and records (see RB-6). AD-2 and Workflows point there.
  - Keep both change paths only in Release tiers. Move "under one named change owner" there, and delete the duplicate sentences from Production profile and Attempt ownership, leaving "Shared-infrastructure changes hold both locks (Release tiers)".
- **Disposition:** autofix.

### RB-23: AD-3's "any other automatic rollback are forbidden" reads as forbidding FR-8's automatic recovery, and it does not name `helm rollback`

- **Severity:** low
- **Location:** AD-3 *Recovery render* (L88)
- **Finding:** "Helm `--rollback-on-failure` and any other automatic rollback are forbidden."
- **Failure scenario:** An executor implementer reads this literally against the "Automatic recovery" row. Or, since only the flag is named, the implementer uses `helm rollback <rev>`, which replays the old revision's values from in-cluster history, including stale secret references and key and catalog generations. That rewinds environment-current authority, which AD-3 forbids.
- **Proposed fix:** "Helm `--rollback-on-failure`, `helm rollback` and any controller-driven rollback are forbidden; the only automatic recovery is the one in Automatic recovery."
- **Disposition:** autofix.

### RB-24: Three minor wording defects

- **Severity:** low
- **Location:** Hosted interfaces (L226); AD-6 *Realms* (L113); Owned work, Maintenance (L460)
- **Findings and fixes:**
  - **(a)** "ingress terminates TLS and routes but makes no authorization decision, and no external caller bypasses it to reach pods." Here "it" can bind to the gateway, which would forbid module UI and Tenants BFF routes. Replace "bypasses it" with "bypasses ingress".
  - **(b)** "The contract version is bound in the release record and applied forward-only by Administrator before the attempt that needs it" conflicts with "staging provisioning uses a staging-only management client". Replace with "applied forward-only before the attempt that needs it — in production by Administrator, in staging by the staging management client".
  - **(c)** "re-sync `specs/spec-platform/SPEC.md`" points to a path that does not exist. The file is `_bmad-output/specs/spec-platform/SPEC.md`.
- **Failure scenario:** For (a), a small agent treats the UI routes as a bypass and rejects them at admission. For (b), staging realm application is blocked on Administrator.
- **Disposition:** autofix.

### RB-25: AD-9 Prevents "custom Dapr components built only for uniformity", but no Rule clause says so

- **Severity:** low
- **Location:** AD-9 *Prevents* (L144) and *Exceptions*
- **Finding:** The Rule makes the Memories exceptions permitted but not required, so nothing stops a team from building a pluggable Dapr component for FalkorDB "to comply".
- **Failure scenario:** Memories and a later graph-using module each build a different pluggable FalkorDB component, which adds an unowned runtime with no profile pin.
- **Proposed fix:** Append to *Exceptions*: "No module builds a custom or pluggable Dapr component only to route a named exception through Dapr."
- **Disposition:** autofix.

### RB-26: No vulnerability policy for application images and dependencies

- **Severity:** low
- **Location:** Operational envelope, security dimension; Module intake; Workflows and provenance
- **Finding:** Supply-chain rules cover provenance, attestation and pinning, and infrastructure pins must stay "current on security patches". Nothing decides, defers or accepts a risk for vulnerability scanning of module images, the composed `platform/eventstore` image or NuGet dependencies at intake or promotion. Builds has `codeql.yml` and `dependency-review.yml`, but no rule binds them.
- **Failure scenario:** One module gates its releases on critical CVEs and another does not. The composed image inherits both, and production promotion has no check.
- **Proposed fix:** Add an Owned work row: "First staging promotion | Image and dependency vulnerability policy | Builds with Platform | Scan module and composed images at intake, set the severity that blocks promotion, and record exceptions." Alternatively, list it under Accepted risks.
- **Disposition:** defer.

### RB-27: Named technology to confirm through the reality lens

- **Severity:** low
- **Location:** Stack; AD-8; Hosted interfaces; Diagnostics and notification
- **Finding:** These items are flagged only, not verified here:
  - `Aspire.Hosting.Kubernetes 13.5.4-preview.1.26464.4` Gateway API emission
  - Helm "4.2.0 floor", and `helm upgrade` of an OCI chart by `@sha256` digest
  - the Traefik 3.7 Gateway provider coexisting with the `kubernetesingressnginx` provider on one DaemonSet
  - a Keycloak 26.7 per-realm frontend URL giving the production issuer a production-controlled name on a shared server
  - the Dapr 1.18 `WorkflowAccessPolicy` `v1alpha1` single-namespace scope
  - multiple issue assignees in a *private* notification repository on GitHub Free. The PRD requires delivery to both Administrator and the deputy. If only one assignee is allowed, the fallback is one issue per recipient or an @-mention of both.
- **Failure scenario:** If the private-repository assignee limit applies, the deputy is never assigned, and FR-8 and G2 deputy delivery fail silently.
- **Proposed fix:** Hand these to the reality lens. If the GitHub assignee limit applies, change Diagnostics to "assigned to Administrator and mentioning the recovery deputy".
- **Disposition:** defer.

---

## Appendix: checklist walk

| # | Rubric item | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Fixes the real divergence points for the level below | Partial | Strong on source identity, the runner lifecycle, the composed host, catalogs, the record model and executors. Gaps: CI package mode (RB-1), the tenant-creation scope (RB-5), record writers (RB-6), the component-name config key (RB-9), surface classes (RB-18) and the recovery cut (RB-14). |
| 2 | Rules are enforceable and prevent the stated divergence | Partial | Most clauses are validator- or admission-checkable. AD-4 fails in the Platform workspace (RB-11). The AD-8 PV clause conflicts with the provisioner (RB-15). AD-12 and AD-9 have Prevents without a matching Rule clause (RB-20, RB-25). AD-3's wording is ambiguous (RB-23). |
| 3 | Nothing deferred lets units diverge | Partial | Two gates are late (RB-7). The deferred schema row reopens C-02 (RB-13). A binding clause sits in G2 Owned work (RB-20). |
| 4 | Named tech verified-current (flag only) | Flagged | Stack matches `apphost.cs`, `global.json`, the Builds catalog at `0610f78` and EventStore v3.109.0. Items for the reality lens are in RB-27. |
| 5 | Ratifies the brownfield | Mostly | `hexalith-module`, `Builds.Module.AppHost`, `UiHost`, `EventStoreHost` and `runtime-toolchain-baseline.json` exist as cited, and the per-module UI web apps match "Domain modules keep domain UIs". Mismatches: `schemaVersion` versus the manifest's `schema` field, and the FrontComposer constant (RB-13). |
| 6 | Covers FR-1 to FR-12 and NFR-1 to NFR-3 | Mostly | Every row is present. Gaps: FR-2 nested-submodule initialization (RB-12), FR-4 and FR-6 for Tenants (RB-5), and mapping omissions (RB-19). |
| 7 | Operational envelope decided, deferred or open | Partial | Deployment, environments, recovery and notification are thorough. Gaps: encryption at rest and in transit (RB-4), the environment-layer definition home and DR inputs (RB-16), the ingress-closure owner (RB-17), and vulnerability policy (RB-26). |
| 8 | Build-substrate quality | Partial | The ADs are terse, with labelled clauses and no rationale. Gaps: the Terms index (RB-21), duplicate homes (RB-22), no enrollment entry point (RB-19), and several Conventions cells of 200–330 words that would read better as labelled sub-bullets, like the ADs. |
