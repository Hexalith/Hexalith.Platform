# Review — Adversarial divergence attack
Verdict: FAIL — two critical holes (who builds and authorizes the shared `eventstore` host that must embed module intent adapters; who owns Dapr component names, namespaces, ACLs and topics) let fully compliant units produce hosted compositions that cannot run, and eleven high-severity pairs remain. Every one can be closed by tightening an existing AD or convention row.

Scope: spine read in full; memlog, PRD, the EventStore, Memories, McpCli and Projects spines, the Parties/Tenants/Folders excerpts cited below, root `apphost.cs`, `DaprComponents/`, and the module source and deploy assets named in the evidence. Holes the memlog explicitly closes are listed under "withstood".

## Findings

### ADV-1 — The shared `eventstore` host that must embed module intent adapters has no builder or release-authority owner
- Severity: critical
- Where: Consistency Conventions "Shared host and catalogs"; AD-2; EventStore AD-11/AD-25/AD-26
- Unit A: A Platform composition epic obeys "Compose module handler and idempotency-intent registrations once into the shared EventStore host". Intent adapters are in-process DI types (`services.AddIdempotencyIntentAdapter<TAdapter>()`), so the epic builds a composite `eventstore` image that references the Folders adapter assemblies.
- Unit B: The EventStore release owner obeys EventStore AD-11/AD-26. Production promotion requires authenticated records bound to the exact `eventstore` image digest EventStore published, and "any additional image first receives an explicit release identity". Platform AD-2 repeats: "Module publication/profile authority remains mandatory".
- Incompatibility: The composite image is not EventStore's released subject, so it has no `release-available` or `production-promoted` authority and can never be promoted. If Platform instead deploys EventStore's released image, the Folders adapters are missing and AD-25 readiness fails ("Readiness fails on missing entries"). No compliant hosted Folders command path exists. The same fork recurs in local mode, where the host is built from source, and in CI package mode.
- Evidence: spine L125; `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Commands/IdempotencyIntentAdapterServiceCollectionExtensions.cs` (in-process registration); EventStore `architecture.md` L168 ("The current release mapping contains only `eventstore`; any additional image first receives an explicit release identity"), L279 ("Each entry binds the trusted adapter"); memlog L155 records the seam but does not assign an artifact owner.
- Recommendation: Add to "Shared host and catalogs": "The shared host that embeds module intent adapters is exactly one Platform-composed artifact per release. It is built from EventStore's released host package plus the exact module adapter packages and receives its own EventStore AD-11 release identity and exact-subject authority chain. Local, CI and hosted compositions build it the same way. Otherwise adapters must be invocable out of process through a catalog-declared module endpoint. No module builds an `eventstore` image."
- Suggested disposition: discuss

### ADV-2 — Dapr runtime topology facts are neither declared by modules nor exclusively assigned; concrete name collisions exist
- Severity: critical
- Where: "Module declaration authority"; "Secrets"; "Production profile authority"; AD-8; AD-9
- Unit A: Memories obeys Memories AD-15 and Platform AD-9 ("secrets retain Dapr boundaries") and reads its secrets through the Dapr component `secretstore`. It ships its own `statestore` (state.redis), `pubsub` (pubsub.redis) and `secretstore` components. Memories.Aspire reuses whatever statestore, pubsub and secret store the consumer passes in.
- Unit B: The Platform deployment owner obeys "Follow EventStore AD-24". It composes the singleton OpenBao component named `openbao` and EventStore's `statestore` (state.postgresql v1, `actorStateStore: true`) into the environment's application namespace.
- Incompatibility: In every hosted environment, Memories cannot resolve `secretstore`, so it fails secret-gated readiness. Two `statestore` or `pubsub` Component objects cannot coexist in one namespace. If Memories moves to its own namespace instead, the ACL `namespace` fields and cross-namespace pub/sub change. The declaration list has no field for component roles, service-invocation callers/operations or topic publish/subscribe scopes. Platform therefore hand-writes ACLs and scopes, while modules such as Folders gate merges on their own policy YAML and kind negative tests. The spine also never says whether an environment is one application namespace or several.
- Evidence: `references/Hexalith.Memories/src/Hexalith.Memories.Server/Ingestion/EmbeddingSecretStore.cs:17` (`SecretStoreName = "secretstore"`); `references/Hexalith.Memories/deploy/dapr/components/{statestore,pubsub,secretstore}.yaml`; `Hexalith.Memories.Aspire/HexalithMemoriesServerExtensions.cs:25`; EventStore `architecture.md` L256 ("component named `openbao`"; "sole composer of the singleton component, every per-app DAPR `Configuration`"), L285; spine L94 and L124 (declaration contents); `DaprComponents/accesscontrol*.yaml` (`namespace: "default"`); memlog L70 proposed "one application namespace per environment", but AD-8 as adopted does not say so.
- Recommendation: Extend "Module declaration authority": "Modules declare each required Dapr capability by logical role (actor state, coordination state, pub/sub, secrets, configuration, conversation), with its recovery class, required invocation callers/operations and topic publish/subscribe needs. Platform assigns the concrete component names, the namespace (one application namespace per environment) and the scopes from one per-environment registry, and injects them into configuration. Module code does not hardcode component names. `openbao`, `statestore` and `pubsub` are reserved for the EventStore profile. A role that cannot be bound fails validation."
- Suggested disposition: autofix

### ADV-3 — The staging gate is not bound to the production profile
- Severity: high
- Where: AD-2; AD-5; "Production profile authority"; Staging gate
- Unit A: The Platform staging epic obeys AD-2 ("same package/images with separately versioned environment configuration") and AD-8. It binds staging to cheaper components: a non-persistent broker configuration, different resiliency, or an unratified runtime pin.
- Unit B: The production epic binds the canonical `production-profile.yaml` (runtime, components, ACLs, resiliency, catalog and secret identities).
- Incompatibility: Exact-release staging E2E passes on a runtime profile that production does not run. The gate then certifies package identity but not the runtime the release will face, which is the failure AD-2 is meant to prevent.
- Evidence: spine L58, L128 ("Bind tested runtime/components"; which environment performs the testing is not stated), L140; no memlog entry binds a profile to staging.
- Recommendation: Add to "Production profile authority": "Staging runs the canonical profile's runtime, components, scopes/ACLs, resiliency and catalog/secret-contract structure, differing only in declared environment bindings (namespace, instances, credentials, hosts, realm). Staging evidence binds the profile digest. Promotion requires that digest to equal the production environment's active profile digest."
- Suggested disposition: autofix

### ADV-4 — Automatic promotion and rollback are not reconciled with EventStore's per-subject deployment-owner authority
- Severity: high
- Where: AD-2; Deployment ownership; Automatic recovery; EventStore AD-11/AD-26
- Unit A: The Platform release workflow obeys FR-6/FR-8 and the Automatic recovery row. It promotes automatically after the staging gate and rolls back once to the recorded previous working release.
- Unit B: EventStore validators obey AD-11/AD-26. Production traffic requires an authenticated deployment-owner `production-promoted` record that binds the exact subject, the canonical profile digest, the environment and the deployment identity, and that carries expiry and revocation. Platform AD-2 adds that "a generic passing workflow is not authority".
- Incompatibility: The spine never says who issues that record during unattended promotion. Nor does it require the rollback target to hold an unexpired record for the currently active profile digest. The automatic flow is then either unauthorized or unable to run: a rollback target whose record has expired, or which predates a profile replacement, cannot legally receive traffic, and the "one rollback" is spent on a blocked action.
- Evidence: spine L58, L128 ("replacing an authorizing profile requires ... renewed exact-subject authority"), L145; EventStore `architecture.md` L170 (records bind "issuance, expiry, revocation"; "each `production-promoted` profile requires a separate authenticated deployment-owner record").
- Recommendation: Add to "Deployment ownership": "The trusted release workflow identity is the registered deployment-owner issuer for automatic promotion and issues the exact-subject record before traffic. A release qualifies as the previous working release only while it holds a valid record for the environment's active profile digest. Profile replacement or record expiry renews it, or records 'no rollback target', before the next attempt starts."
- Suggested disposition: discuss

### ADV-5 — Rolling back to the previous catalog generation collides with idempotency retirement rules
- Severity: high
- Where: AD-3; AD-2 (release record binds routing identities); "Shared host and catalogs"; EventStore AD-25/AD-33
- Unit A: The release workflow obeys AD-2 and the Automatic recovery row. Release R2 adds an idempotent command C. Its production smoke writes synthetic data, which creates admission records for C. Rollback restores R1's recorded configuration, including R1's routing/idempotency catalog generation, which has no entry for C.
- Unit B: EventStore obeys AD-25: "Retirement is refused while records, tombstones, aliases ... or catalog references remain". Activating a generation also fails on unsupported key generations when a rotation happened after R1.
- Incompatibility: The only rollback candidate is refused, so FR-8 recovery fails structurally after any release that introduced an exercised idempotent command. AD-3 ("never rewind key generations or durable authority") forbids the naive restore. The alternative is a new generation made of R1's routes plus the retained C and key entries, and nobody owns that generation. It was never staged, and no release record binds it.
- Evidence: spine L64, L125, L145; EventStore `architecture.md` L279, L331 (prepare/ready/commit rolls back only on activation failure).
- Recommendation: Add to AD-3: "Application rollback never reactivates an older catalog generation. It commits a new forward generation composed of the restored release's routes plus every still-referenced idempotency/key entry and the current key generations. The attempt record binds that generation. Staging proves the N-to-N-1 rollback generation before promotion."
- Suggested disposition: autofix

### ADV-6 — Only release attempts are serialized; other paths mutate the same workloads unserialized and without staging
- Severity: high
- Where: Deployment ownership; AD-3; AD-8; "Secrets"; "Production profile authority"
- Unit A: The release epic serializes release attempts per environment and runs the five-minute verification window.
- Unit B: The secrets epic obeys EventStore AD-24, where app-channel token rotation "requires a controlled sidecar/workload rollout" and credential rotation sits outside rollback (AD-3). Catalog activation commits generations separately. The infrastructure owner upgrades the cluster-wide Dapr control plane or sidecar injector, OpenBao or operators as "shared supporting infrastructure" (AD-8).
- Incompatibility: A rotation rollout inside a verification window trips the 60-second or twice-failed smoke trigger and causes a false rollback. A Helm rollback re-renders pod templates and undoes the rotation rollout. The control plane is cluster-wide and serves both environments, so staging-first qualification of a runtime change is impossible. Injected sidecars silently diverge from the profile's "exact Dapr runtime image" at the next pod restart, including restarts during a rollback. OpenBao server values live in the Memories repository, so the change owner is ambiguous.
- Evidence: spine L64, L94, L128, L141; EventStore `architecture.md` L256; `references/Hexalith.Memories/deploy/openbao/{namespace.yaml,values.yaml}` (shared `openbao` namespace, 3-replica raft); memlog L65 (one Dapr 1.18.1 control plane observed).
- Recommendation: Add to "Deployment ownership": "Every mutation of an environment's application workloads or bound configuration takes the same per-environment attempt lock and record: release, rollback, credential/key rotation rollout, catalog activation, profile/shared-infrastructure change and recovery. Each shared cluster service has one named change owner. Changes to it are profile changes, applied only while neither environment has an open attempt. Workloads pin their sidecar runtime image so a control-plane change cannot alter a release's runtime."
- Suggested disposition: autofix

### ADV-7 — Data-service instances have no version authority and no rollback class
- Severity: high
- Where: AD-2; AD-3; AD-8 ("Compatible modules may share an instance"); Design Paradigm ("Memories.Aspire supplies its qualified dependency image digests"); "Production profile authority"
- Unit A: The Platform Helm epic packages each environment's application data services (Redis Stack, FalkorDB, PostgreSQL, broker) as StatefulSets inside the application package, using Memories.Aspire digests, which Memories AD-19 makes "the single owner of qualified container image digests".
- Unit B: The profile owner binds data-service versions in the canonical profile, which only an EventStore AD-26 owner-ratified change can alter. Rollback obeys FR-8: "restore application components changed by the attempt".
- Incompatibility: (a) A shared instance has two image authorities, the Memories.Aspire digest set and the EventStore-ratified profile. Every Memories digest bump then needs EventStore ratification, or runs outside the profile. (b) After a release that upgraded FalkorDB or PostgreSQL, rollback re-deploys the older image over the upgraded on-disk format, which risks data loss. AD-3 excludes "persistent data" and "shared infrastructure", but per-environment application data services are neither, so their class is undefined.
- Evidence: spine L25, L58, L64, L94, L128; Memories spine L198–L202 (AD-19); memlog L65 (observed pinned data-service digests).
- Recommendation: Add to AD-3: "Application data-service instances and brokers are not application components. They are deployed and upgraded by forward-only, separately planned procedures outside the application Helm release and are never changed by rollback. Each instance has exactly one version authority, its profile facet. Module digest sets such as Memories.Aspire are qualification inputs. An instance may be shared only when its digest is in every consumer's qualified set." Separately decide whether the profile gets per-facet ratifiers.
- Suggested disposition: discuss

### ADV-8 — State with conflicting recovery classes is co-located in one backup unit
- Severity: high
- Where: AD-8; AD-12; Backup coverage; Memories erasure continuity
- Unit A: Memories.Aspire reuses the consumer's shared EventStore statestore by default. Memories' projection checkpoints, leases, fences and mappings therefore live in the environment's single PostgreSQL `statestore` ("compatible modules may share an instance").
- Unit B: The Platform backup epic obeys AD-12 and Backup coverage. It PITR-backs up and restores that PostgreSQL instance as EventStore's authoritative store (Barman base+WAL seed).
- Incompatibility: Disaster restore rewinds Memories coordination state, which Memories says has "no operational backup-restore path". Restored checkpoints report `Indexed` while Redis/FalkorDB are rebuilt empty through replay, so queries return incomplete results labeled complete. That violates Memories AD-3's requirement to write explicit `reprojectionRequired` records. The Memories kustomize deployment keeps the same state in its own Redis `statestore`, so local/CI and hosted behavior also diverge.
- Evidence: `Hexalith.Memories.Aspire/HexalithMemoriesServerExtensions.cs:25` ("reuses the stateStore ... typically the shared EventStore state store"); Memories spine AD-2 L90, AD-3 L96; spine L94, L146, L149.
- Recommendation: Add to "Backup coverage": "Each declared state binding carries a recovery class: authoritative-restore, rebuild-only or live-authority-only. Platform never places different classes in one backup unit (instance, database or table covered by one PITR). If co-location is unavoidable, the restore sequence invalidates rebuild-only state as its owner prescribes (Memories `reprojectionRequired`) before reopening service."
- Suggested disposition: autofix

### ADV-9 — Module-owned and legacy hosted deployment definitions coexist with the Platform package, and none is named the deployment of record
- Severity: high
- Where: AD-1; AD-2; Migration; Design Paradigm ("Technical modules may own reusable hosting capabilities")
- Unit A: Memories obeys its own spine ("AppHost, ServiceDefaults, deployment manifests, and reusable hosting integrations are therefore intentional platform responsibilities"; AD-19 names `deploy/kubernetes` as a consumer). It keeps evolving `deploy/kubernetes/overlays/production` (namespace `hexalith-memories`), `deploy/dapr` and `deploy/openbao`. Parties keeps its AppHost as a "migration rollback surface" and ships `aspirate` for manifest generation.
- Unit B: The Platform release epic obeys AD-1/AD-2 and deploys all enrolled modules from the retained Helm package. The Migration row explicitly keeps "legacy module hosting" until parity and consumer-removal authority exist.
- Incompatibility: Two deployment definitions target Memories in production. One of them uses Redis-backed components and a separate namespace, the other uses Platform components. Both can run as writers of one tenant population and its AD-21 register lineage, which is a second writer of authority that Memories forbids. The spine only says existing Memories resources "require environment classification".
- Evidence: `references/Hexalith.Memories/deploy/kubernetes/overlays/production/kustomization.yaml:3`; `references/Hexalith.Parties/.config/dotnet-tools.json` (`aspirate`); Parties epic-8 spine L93, L153; spine L25, L132, L207.
- Recommendation: Add to AD-1: "In staging and production, the retained Platform package is the only deployment definition for enrolled modules' workloads, Dapr resources and data services in Platform environments. Module deploy assets are qualification inputs only. Legacy deployments kept under the Migration row stay in their existing namespaces and are never a second writer to a Platform environment's data or tenant population. Cutover is a planned attempt that fences the legacy writer."
- Suggested disposition: autofix

### ADV-10 — Integration-environment provisioning has competing owners
- Severity: high
- Where: AD-10; "Local tool and readiness" ("Shared reusable fixtures remain owned by `EventStore.Testing(.Integration)`")
- Unit A: EventStore, as owner of shared fixtures, evolves `AspireTopologyFixtureBase<TAppHost>`. It boots each consumer's own AppHost type, waits for `Running`/`/alive` ("verifies process liveness, not full readiness"), defaults to a 6-minute timeout, uses fixed placement/scheduler ports and serializes on a global lock file. McpCli AD-16 builds `tests/Hexalith.McpCli.AppHost` on that fixture and requires Parties to publish a `Hexalith.Parties.Aspire` package.
- Unit B: The Platform tool team obeys AD-10 and the "Local tool and readiness" row: a run-owned composition from declarations, module readiness plus startup tasks, a 10-minute deadline, ownership records and concurrent isolation.
- Incompatibility: Module test suites legitimately pick either fixture. One path bypasses declarations, ownership records and readiness semantics. It revives module-owned AppHosts and domain Aspire packages that the Design Paradigm forbids. The two paths produce different composition graphs and different evidence for the same FR-4 claim.
- Evidence: `references/Hexalith.EventStore/src/Hexalith.EventStore.Testing.Integration/AspireTopologyFixtureBase.cs` (class docs, `StartupTimeout => 6 min`, `FixtureLockName`); McpCli spine L152 and L331; spine L25, L106, L129; the McpCli deferred row (spine L235) amends only availability and package/source mode, not the harness.
- Recommendation: Add to AD-10: "Only the Platform runner provisions, readiness-waits for, records ownership of and cleans integration environments. EventStore.Testing(.Integration) fixtures and module and McpCli test projects consume the runner-supplied endpoints and composition identity. They never compose or start their own AppHost or require domain Aspire packages."
- Suggested disposition: autofix

### ADV-11 — A module workspace can hold two Platform identities, and modules can pin skewed schema versions
- Severity: high
- Where: AD-4; "Local tool and readiness"; "Module declaration authority"; Deferred enrollment row
- Unit A: Parties follows PRD FR-2 ("A module workspace includes Platform as a direct Git submodule") and AD-4 (source identity, "no ... package fallback"). It runs Platform from its submodule at commit C.
- Unit B: Projects follows Projects AD-25 and the Platform tool row ("pinned consumable Platform .NET tool ... without edits to its repository"). It runs tool version X from NuGet. Separately, each module pins a different tool and schema version.
- Incompatibility: A workspace holding both identities has no rule for which one composes. Across modules, the complete-local and hosted compositions validate all seven declarations with one validator. A module pinned to schema N-1 or N+1 then passes in its own workspace and fails, or is silently misread, in the composite. The deferred row fixes only the first shared version, not ongoing skew.
- Evidence: PRD L58; Projects spine L272; spine L70, L124, L129, L232.
- Recommendation: Add to "Local tool and readiness": "A module workspace consumes exactly one Platform release identity. When both a submodule and a pinned tool exist, they must identify the same release, and the tool fails on mismatch. Declarations carry `schemaVersion`. Platform publishes a supported compatibility window. Composite compositions reject out-of-window declarations and name the module."
- Suggested disposition: autofix

### ADV-12 — Startup tasks have no lifecycle scope, so recovery can re-run population genesis
- Severity: high
- Where: "Module declaration authority" (readiness/startup tasks); AD-10; AD-12
- Unit A: Memories declares its AD-21 platform-bootstrap step, which creates the register genesis marker and `populationId`, as an enrollment startup task. It is required before tenant provisioning, and readiness depends on the marker.
- Unit B: The Platform hosted and DR epics run declared startup tasks on each composition start (AD-10 "successful startup tasks"). They use Helm hooks on install, upgrade and rollback, and when bringing up prepared replacement capacity.
- Incompatibility: On replacement capacity whose restored store lacks a live lineage, the task mints a new population. Memories: "Starting a new population is not recovery ... cannot admit the lost population's restores". Every tenant is permanently stranded. The spine never separates per-start verification from once-per-environment creation.
- Evidence: Memories spine L215–L216; spine L106, L118 ("establish trustworthy ... deletion authority"), L124.
- Recommendation: Add to "Module declaration authority": "Each startup task declares its lifecycle scope (per-start idempotent verify, once per environment or population creation, or operator-only) and its authority class. Platform runs creation tasks only when creating a new environment (fresh test runs, first hosted install). It never runs them on upgrade, rollback, restore or replacement-capacity recovery. Population- or authority-creating tasks are operator-only in hosted environments."
- Suggested disposition: autofix

### ADV-13 — Projects assigns its operational envelope to Platform, and Platform assigns it back
- Severity: high
- Where: Source Precedence (Projects row); Deferred "Module service-level and functional qualification"; AD-12
- Unit A: Projects obeys Projects AD-28: "The platform AppHost owns ... managed encryption at rest ... identity/KMS ... The platform configuration and evidence must enforce 99.9% ... and committed-event RPO 0 inside the configured primary-region durability domain." Projects AD-30 fails closed without that evidence.
- Unit B: Platform obeys its spine. It keeps the Projects gate but assigns it to "Respective module owners with Platform". It runs one node with local storage and a one-hour RPO, defers standby, and never mentions encryption at rest.
- Incompatibility: Domain events of all modules share the EventStore store, so committed-event RPO 0 and encryption at rest can only be delivered as Platform-wide profile properties. Projects owners cannot supply them, and each side treats the other as the owner. As a result Projects can never pass its own production gate, or Platform enrolls it in breach of that gate. This is the same conflict the user resolved for Folders only.
- Evidence: Projects spine L286–L290 (AD-28); spine L163, L241; memlog L149 ("still need owner reconciliation"), L153 (override scoped to Folders).
- Recommendation: Record an explicit disposition in Source Precedence. Either the canonical profile provides a declared synchronous committed-event durability domain and managed encryption at rest, owned by Platform, or the Projects owner or user reconciles AD-28 as was done for Folders. Until then, state that Projects production enrollment is blocked by its own gate.
- Suggested disposition: discuss

### ADV-14 — Smoke and E2E execution identity and synthetic tenant are unowned
- Severity: medium
- Where: Before production update ("Smoke writes use synthetic data"); AD-6
- Unit A: The release workflow runs smoke suites with the deployment runner's workload service account, as a workload-only principal.
- Unit B: Projects, per AD-6 "Preserve required actor plus workload/delegation checks", requires an actor as well. Its smoke suite, and Tenants' suite, each create their own synthetic tenant on every run.
- Incompatibility: Projects smoke fails in production on every release, which triggers a false rollback. Per-run tenant creation provisions Memories tenant resources on each deploy, and cleanup by deletion writes irreversible AD-21 tombstones that consume tenant IDs permanently.
- Evidence: spine L82, L142; Memories spine AD-21 L218 ("permanently retires its tenant identifier").
- Recommendation: Add to "Before production update": "Platform owns one pre-provisioned synthetic tenant and explicitly enrolled synthetic actor and workload identities per environment, used by all smoke/E2E suites. Suites never create or delete tenants."
- Suggested disposition: autofix

### ADV-15 — The per-operation compatibility identity between McpCli and the catalog is undefined
- Severity: medium
- Where: AD-11; Deferred "Connected McpCli metadata" (three co-owners)
- Unit A: McpCli computes compatibility from the exact Contracts package ID and version, its AD-16 notion.
- Unit B: EventStore records a per-`(Domain, MessageType)` "contract version", today a coarse `v1` string (`EventStore__DomainServices__Registrations__...__Version = v1` in `apphost.cs`).
- Incompatibility: The join key is undefined. Either every operation is "unknown", so nothing is executable and FR-12 fails, or `v1` is treated as matching any package, which falsely advertises executable operations.
- Evidence: spine L112, L235; EventStore `architecture.md` L329; McpCli spine L152; root `apphost.cs`.
- Recommendation: Add to AD-11: "EventStore.Contracts defines the per-operation compatibility key once in the catalog schema, as a contract-schema digest computed by the shared codec. McpCli derives the same key from its bundled types. Package versions and domain `vN` strings are not compatibility evidence."
- Suggested disposition: autofix

### ADV-16 — Realm clients, audiences and claims have no declaration source and no release binding
- Severity: medium
- Where: AD-6; AD-3 (Keycloak outside rollback); "Module declaration authority"
- Unit A: Module teams configure host audiences and clients from their development realm exports, which differ per module (memlog L46).
- Unit B: The Platform identity epic hand-configures the staging and production realms separately.
- Incompatibility: The staging gate validates the staging realm only. Drift in the production realm (a missing audience, role mapper or actor/workload claim) surfaces as a production smoke failure. Rollback cannot fix it because the realm is outside rollback ownership. The release record never binds the identity configuration a release needs.
- Evidence: spine L64, L82, L124; memlog L46, L53.
- Recommendation: Add to AD-6: "One versioned, value-free identity declaration (clients, audiences, roles/claim mappers, service accounts, required actor/workload claims) is composed from module declarations. Both realms are applied from the same version, differing only by environment. The release record binds that version, and promotion checks realm conformance. Realm changes are forward-only and are applied before the attempt that needs them."
- Suggested disposition: autofix

### ADV-17 — Check suites have no artifact, retention or result contract
- Severity: medium
- Where: AD-2 (record binds "check-suite versions"); Staging gate; Automatic recovery ("using that release's checks")
- Unit A: Folders runs its smoke suite from tagged source with `dotnet test` on the deployment runner.
- Unit B: Projects publishes its suite as a container image and emits results without release identity.
- Incompatibility: The workflow needs heterogeneous executors, and results cannot be bound to the exact release. Rollback verification must rebuild old source on a private-network runner and may become "unverifiable". AD-2 retains only the Helm package and images.
- Evidence: spine L58, L140, L145.
- Recommendation: Add to AD-2: "Critical-flow and smoke suites are immutable, digest-identified artifacts, executable on the deployment runner without source builds, through one Platform-owned invocation and result contract. Results carry the release, environment and suite digests. The release record retains the suite digests alongside the package."
- Suggested disposition: autofix

## Attacks attempted that the spine withstood
- A second module wrapper under the `eventstore` app ID, or a hand-maintained McpCli catalog, is forbidden ("Shared host and catalogs").
- Duplicate or conflicting app IDs, routes or declarations fail validation. Conflicting shared-module configuration surfaces as validation failure in composite compositions.
- A module implementing the enrollment schema before a shared version exists is blocked by the Deferred row ("one shared version before consumers implement").
- Test cleanup cannot reach attached developer sessions or other runs (AD-10). CI always cleans. Diagnostics survive cleanup.
- Staging users, credentials or membership cannot reach production (AD-6, AD-8), and promotion never grants access.
- Application rollback rewinding data, events or credentials is barred (AD-3, NFR-1). Rollback never cycles to older releases, and a retried job gets no second rollback allowance (Deployment ownership).
- Folders `state.postgresql` v2 versus EventStore v1, and the stronger Folders RPO and topology, are closed by the recorded user override plus shared-profile qualification.
- Legacy hosting retirement on parity alone is barred (Migration row plus EventStore AD-22).
- A copied Memories register or application-restored lineage is not treated as authority. The restore-artifact `populationId`/sequence stamping falls under the named Memories/EventStore lineage qualification.
- A new provider SDK path, such as a KMS adapter for EventStore payload protection, needs an explicit AD-9 exception naming the capability, owner and surface.
- Tenants' BFF REST read transport versus the McpCli gateway route, the InteractiveServer scale-out gate, Parties' ban on MCP erasure and the Projects MCP confirmation limits are explicitly preserved.
- Restored external-provider state is reconciled before provider mutations resume ("External effects").
- Multiple releases in flight in one environment are serialized. Profile replacement through Dapr API portability is barred.
