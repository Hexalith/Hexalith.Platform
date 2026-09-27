# Validation Report: Hexalith Platform Architecture Spine

Spine: [ARCHITECTURE-SPINE.md](../../ARCHITECTURE-SPINE.md) (status final). Validated 2026-09-27. Machine-readable results: [findings.json](findings.json).

**Verdict: FAIL.** 2 critical and 36 high clusters block handoff to epics. The critical blockers are ownership of the shared EventStore host and the trust boundary of the deployment runner. The user must decide 24 discuss items before a bmad-architecture Update applies 41 autofixes. Structural lint reports zero findings.

The seven lenses produced 115 findings. They merge into 67 clusters:

- By severity: 2 critical, 36 high, 22 medium, 7 low.
- By disposition: 41 autofix, 24 discuss, 2 defer, 0 ignore.
- Verification: 1 disputed (V-22); 7 clusters touch settled decisions.

## Lenses

| Lens | Configured | Verdict | C | H | M | L | Report |
| --- | --- | --- | --: | --: | --: | --: | --- |
| Rubric walker | yes | PASS WITH FINDINGS | 0 | 9 | 8 | 3 | [review-rubric.md](review-rubric.md) |
| Reality and version check | yes | PASS WITH FINDINGS | 0 | 3 | 5 | 2 | [review-reality.md](review-reality.md) |
| Adversarial divergence attack | yes | FAIL | 2 | 11 | 4 | 0 | [review-adversarial.md](review-adversarial.md) |
| Brownfield code ratification | added | PASS WITH FINDINGS | 0 | 1 | 8 | 4 | [review-brownfield.md](review-brownfield.md) |
| Security and isolation | added | FAIL | 2 | 8 | 6 | 1 | [review-security.md](review-security.md) |
| Cross-module seams | added | FAIL | 1 | 6 | 10 | 2 | [review-seams.md](review-seams.md) |
| Operability and recovery | added | FAIL | 2 | 10 | 5 | 2 | [review-operability.md](review-operability.md) |

The operability report's header states 11 high and 4 medium findings. The findings themselves are marked 10 high and 5 medium, and the table uses the marked counts.

## Themes

- **Shared composition artifacts and deployment of record** (V-01, V-06, V-16, V-20, V-36, V-40, V-49, V-53, V-64): Platform-composed artifacts (eventstore host, Dapr topology, profile bindings, data services) and parallel module deployments lack a single owner, identity and order.
- **Staging/production trust boundary on the shared installation** (V-02, V-11, V-22, V-23, V-24, V-25, V-26, V-43, V-56, V-57, V-58, V-59, V-65): Runner, Dapr plane, OpenBao, pods, ingress, Keycloak admin and providers are shared; AD-7/AD-8 state intent but name no enforcing mechanism or negative test.
- **Identity, surface and McpCli authority contracts** (V-03, V-10, V-18, V-27, V-48): Realm claims, admission, surface identity, delegation, token-environment binding and discovery compatibility are asserted in AD-6/AD-11 but have no owned contract.
- **Release attempt state machine and rollback safety** (V-09, V-12, V-13, V-14, V-15, V-29, V-30, V-31, V-42, V-44, V-60, V-67): Numbers match the PRD, but verdict totality, interruption, stop/baseline, serialization, compatibility evidence, catalog composition and authority records are open.
- **Recovery, erasure continuity and key custody** (V-17, V-19, V-21, V-28, V-32, V-33, V-34, V-35, V-39): AD-12 sequence lacks definitions for recovery point, fencing, detection, quarantine, revocation continuity and key-bearing backups; Memories mirror silently gates all production.
- **Tensions with settled decisions and module envelopes** (V-05, V-07, V-08, V-37, V-38, V-55): Projects envelope, Memories Redis, submodule-versus-tool, Folders override follow-through, Aspire export path and infrastructure currency need user decisions.
- **Brownfield adoption, tooling and stack evidence** (V-04, V-41, V-45, V-46, V-47, V-50, V-51, V-52, V-54, V-61, V-62, V-63, V-66): Fixtures, build modes, CI tier, raw state use, ServiceDefaults and stack pins diverge from AD-4/AD-5/AD-9/AD-10; mostly mechanical once owners are named.

## Critical and high clusters

### V-01: Shared EventStore host embedding module adapters has no builder or release identity

**Critical**, disposition **discuss**.

- Sources: RUB-1, ADV-1, SEAM-1 (lenses: rubric, adversarial, seams).
- Targets: Conventions: Shared host and catalogs; Conventions: Module declaration authority; AD-2; Deferred: Shared runtime/profile evidence.
- Verification: confirmed. Checked spine L125, Folders architecture L1651 (ContainerRepository=eventstore carrying A-9 adapters), Folders Program.cs AddFoldersIdempotencyIntentAdapters, EventStore AddIdempotencyIntentAdapter DI extension, EventStore architecture L168.

**Defect.** The spine requires module handler and idempotency-intent registrations to be composed once into the shared eventstore host, but intent adapters are in-process DI types, so that host must be built from module code. Nobody is named to build the composed host project or image, give it an EventStore AD-11 release identity and AD-26 authority chain, or govern adapter-versus-host version skew; Folders ships its own eventstore image today, which the spine forbids.

**Failure scenario.** Platform deploys EventStore's released eventstore image and every Folders command fails AD-25 readiness because the trusted adapters are missing; or Platform builds its own composite image, which has no release-available or production-promoted record and can never be promoted.

**Recommendation.** Name the owner of the composed host (a Platform composition project or an EventStore host-composition contract). Modules publish adapters as packages against a versioned extension API. The composed image gets its own AD-11 release identity ratified by EventStore, or the adapters move out of process. Fix the build order: extension contract, adapter packages, composed host, catalog generation, release binding.

### V-02: Persistent deployment runner mixes staging test code with production authority

**Critical**, disposition **autofix**.

- Sources: SEC-1, RUB-4, REAL-8 (lenses: security, rubric, reality).
- Targets: AD-7; Release & Recovery: Staging gate; Deferred: Secrets, identity, network and transport setup.
- Verification: confirmed. gh repo view reports PUBLIC for Hexalith.Platform and Hexalith.Builds; spine AD-7 L88 names no workflow, event, ephemerality or RBAC restriction.

**Defect.** AD-7 permits one persistent self-hosted runner that executes module-owned staging E2E code and then production rollout with production credentials. It binds nothing about which repositories, workflows, refs or events may target the runner. Platform and Builds are public repositories, where GitHub advises against self-hosted runners, and credential scope (namespace versus cluster-admin), ephemerality and runner update enforcement are unbound.

**Failure scenario.** A compromised NuGet dependency in a staging E2E suite plants persistence on the runner, and the next production job hands it production kube credentials; or a fork PR targets the runner label and exfiltrates a cached kubeconfig.

**Recommendation.** Add to AD-7: staging, test and PR code never runs where production credentials are or were present (ephemeral per-job runners, or a production-only runner). The runner group accepts only named release workflows on protected refs, never pull_request or fork events, with per-job credentials from protected GitHub Environments. Each environment's deploy identity is namespace-scoped with no bind, escalate or impersonate, and cluster-scoped prerequisites stay outside the application package. JIT runners, OIDC federation and update monitoring remain seed.

### V-03: Keycloak client, audience, claim and admission contract has no owner

**High**, disposition **discuss**.

- Sources: SEAM-5, RUB-6, ADV-16, SEC-11 (lenses: seams, rubric, adversarial, security).
- Targets: AD-6; AD-3; Conventions: Module declaration authority; Deferred: Secrets, identity, network and transport setup.
- Verification: confirmed. Parsed ten hexalith-realm.json exports: mappers differ (Memories tenants only; Parties party_id/roles; Folders lacks eventstore:domain). apphost.cs uses HS256 DevOnlySigningKey with issuer hexalith-dev.

**Defect.** AD-6 relies on issuer, API audiences, explicit environment admission and module permissions, but nobody owns the client, audience, claim-mapper and role contract each realm implements. Modules have no declaration field for identity needs, the local/CI issuer is unfixed, admission is undefined (realm membership or a named group), and realm configuration is not bound to releases. Module dev realms already diverge: Memories emits only tenants, Parties adds party_id and roles, Tenants and Projects add eventstore:current-tenant, and the root AppHost uses an HS256 dev key.

**Failure scenario.** The complete environment imports one realm, and Memories or Parties fails closed on missing claims; a production-only realm drift (missing audience or mapper) surfaces as a production smoke failure that rollback cannot fix because Keycloak is outside rollback.

**Recommendation.** Name one owner of a versioned, value-free realm contract (for example EventStore AD-10/AD-27 owns claim names, and Platform owns the per-environment realm instances, including local and CI). Add required clients, audiences, roles and claims to the declaration, apply both hosted realms from the same version, bind it in the release record, and apply it forward-only before the attempt that needs it. Define admission as a named production-realm group, never granted by default, IdP mapping or first login, and validated by every host's AD-10 fingerprint. Record Administrator MFA, break-glass and a recovery deputy in Deferred.

### V-04: Test-environment lifecycle has two owners; existing fixtures contradict AD-10

**High**, disposition **autofix**.

- Sources: ADV-10, RUB-17, SEAM-9, BRN-7 (lenses: adversarial, rubric, seams, brownfield).
- Targets: AD-10; Conventions: Local tool and readiness; Deferred: Source/package adoption and declarations; Deferred: Enrollment schema/validator, pinned runner and resource ownership records.
- Verification: confirmed. AspireTopologyFixtureBase.cs: 6-minute StartupTimeout (L74), cross-process lock (L80), DisposeAsync on failure (L160-166), liveness-only docs (L23); DaprComponents state and pub/sub use localhost:6379.

**Defect.** The spine gives lifecycle policy to the Platform runner but keeps the shared fixtures in EventStore.Testing(.Integration), with no handoff contract between them. The existing AspireTopologyFixtureBase<TAppHost> starts and disposes its own project-typed AppHost (which a file-based apphost.cs cannot supply), uses a 6-minute timeout, checks only liveness and disposes on failure. Local compositions also share the dapr-init Redis, a global fixture lock, fixed Keycloak ports and one cert directory.

**Failure scenario.** Parties tests derive from the EventStore fixture and start their own topology with a 6-minute deadline and no retention, while Projects uses the Platform runner. The same FR-4 claim then yields different graphs, cleanup records and evidence, and two runs still share Redis state.

**Recommendation.** State in AD-10 that only the Platform runner provisions, readiness-waits, records ownership of and cleans integration environments. EventStore.Testing(.Integration), module and McpCli test projects consume a versioned Platform-owned environment descriptor and never start their own AppHost. Extend the Deferred reconciliation list to the dapr-init Redis, the fixture lock, fixed-port Keycloak and the shared cert directory.

### V-05: Projects 99.9%, RPO-0 and durable-task envelope owned by neither side

**High**, disposition **discuss**.

- Sources: RUB-8, SEAM-7, ADV-13, OPS-16 (lenses: rubric, seams, adversarial, operability).
- Targets: Source Precedence; Deferred: Module service-level and functional qualification; AD-12.
- Verification: confirmed. Projects spine L290 AD-28 text confirmed; G-1 at L483; spine L163/L241 hand qualification back to module owners; memlog L149 notes owner reconciliation still pending.
- Settled decision: Resolution needs a Folders-style override (user authority) or HA beyond the adopted AD-12 backup-restore design on the single designated installation.

**Defect.** Projects AD-28 says the platform configuration must enforce 99.9% monthly availability, a 15-minute service RTO, five-minute task recovery and committed-event RPO 0, and Projects G-1 expects an EventStore/platform durable task engine. The spine says it preserves these but hands qualification back to module owners. It runs one node with local storage and backup-restore recovery, and records neither an override (as it did for Folders) nor an owner.

**Failure scenario.** Projects' fail-closed AD-30 release gate asks for availability and RPO-0 evidence that no Platform component produces, so Projects production enrollment is blocked indefinitely or the gate is silently waived.

**Recommendation.** Record an explicit decision in Source Precedence: either a Folders-style user override of the Projects AD-28 infrastructure clauses, or a named owner for HA and synchronous durability, which goes beyond the adopted AD-12. Name an owner for G-1 (EventStore, or explicitly out of MVP). Make Platform the owner of availability measurement and of a release attempt's downtime budget.

### V-06: Enrollment declaration lacks Dapr roles, topics, ACLs, secrets and eligibility

**High**, disposition **autofix**.

- Sources: ADV-2, SEAM-4, RUB-18 (lenses: adversarial, seams, rubric).
- Targets: Conventions: Module declaration authority; Conventions: Secrets; Conventions: Production profile authority; AD-8; AD-9; AD-11.
- Verification: confirmed. Memories EmbeddingSecretStore.cs L17 hardcodes secretstore; EventStore architecture L256 names openbao; spine L124 lacks ACL/topic/secret/eligibility fields. Downgraded: the name clash is partly brownfield non-conformance to AD-24.

**Defect.** Platform composes the secret contract, component scopes, ACLs, topics and surface eligibility. Yet the declaration has no fields for Dapr component roles, inbound caller tuples, topics with poison policy, logical secrets, interface hosts, smoke suites or eligibility, so Platform would hand-maintain per-module data. There are concrete clashes: Memories hardcodes the secret store name secretstore while EventStore AD-24 composes openbao, and the spine never says whether an environment is a single application namespace.

**Failure scenario.** Folders publishes to the Memories ingestion topic, which no declaration names, so Platform's default-deny scopes drop it and indexing silently stops; Memories fails secret-gated readiness because no secretstore component exists.

**Recommendation.** Make the declaration one enumerated list of every module input the spine references: Dapr capabilities by logical role with a recovery class, inbound callers and operations, topics with dead-letter policy, logical secrets, interface names and routes, smoke suites, the startup override and per-operation surface eligibility. Platform assigns concrete component names, the namespace (one application namespace per environment) and scopes from a per-environment registry. Module code does not hardcode component names, and module-authoritative files such as the Parties I1 ACL file become the declaration source.

### V-07: AD-9 accepts only FalkorDB; Memories direct Redis usage unresolved

**High**, disposition **discuss**.

- Sources: BRN-1, RUB-2, SEAM-13 (lenses: brownfield, rubric, seams).
- Targets: AD-9; Release & Recovery: Memories erasure continuity; Deferred: Memories recovery continuity and implementation gaps; Source Precedence.
- Verification: confirmed. Memories.Server.csproj L56-58 references NFalkorDB, NRedisStack and StackExchange.Redis; 89 Server files import Redis; Memories spine L249-255 lists the registry with the SET NX row.
- Settled decision: The user amended AD-9 to accept only the Memories FalkorDB adapter and authorized no further exception (memlog L101-105).

**Defect.** Memories AD-8 permits an Adapters.Redis search/vector adapter and a Direct Redis Exception Registry with a SET NX preflight-dedup row, and 89 Memories Server files import StackExchange.Redis or NRedisStack. Platform AD-9 accepts only Memories FalkorDB, reserves coordination state to Dapr and names no approver for further exceptions. The memlog constraint to reconcile the registry individually never reached the spine.

**Failure scenario.** A Platform AD-9 dependency guard flags NRedisStack in Memories and blocks the minimum composition of every domain module; or Memories reads the plural 'specialized bounded adapters' as approval and keeps adding registry rows.

**Recommendation.** Decide with the user whether the Memories Redis search/vector adapter is accepted on the same capability basis, and whether the SET NX dedup row stays a named bounded exception or moves to Dapr state with an owner and deadline. Record the outcome in AD-9. New Memories registry rows need acceptance by the Platform architecture owner before the enrollment validator enforces AD-9.

### V-08: Module workspace has two Platform version authorities and no schema-skew rule

**High**, disposition **discuss**.

- Sources: ADV-11, RUB-9, SEAM-8 (lenses: adversarial, rubric, seams).
- Targets: Conventions: Local tool and readiness; Conventions: Module declaration authority; AD-4; AD-2; Deferred: Enrollment schema/validator, pinned runner and resource ownership records.
- Verification: confirmed. PRD L58 requires Platform as a direct Git submodule; spine L129 requires a pinned tool; Deferred L232 fixes only the first shared schema version.
- Settled decision: PRD FR-2's submodule requirement is an inherited, settled product constraint (memlog L13-14); making the tool the sole identity may change it.

**Defect.** PRD FR-2 makes Platform a direct Git submodule of each module workspace, while the spine makes it a pinned .NET tool in .config/dotnet-tools.json; nothing says which one composes or what happens when they differ. The enrollment schema has only a first-version rule and no compatibility window across seven modules. No intake rule says how module releases enter the single Platform Helm package.

**Failure scenario.** Parties pins tool 1.3 while its Platform submodule sits at 1.5, reproducing AD-4's debug-one-run-another failure for Platform itself; Projects moves to schema v2 while Parties stays on v1, and the complete-environment validator rejects one of them.

**Recommendation.** Give each workspace one Platform release identity: the tool fails if it doesn't match the submodule, or the submodule is used only in an explicit Platform-source debug mode. Require schemaVersion in declarations and publish a compatibility window. Add a versioned module-intake manifest to the Platform repo that pins module package and image digests and requires module release evidence.

### V-09: NFR-1 rollback-compatibility evidence is restated, not defined or gated

**High**, disposition **autofix**.

- Sources: RUB-3, OPS-5, SEAM-14 (lenses: rubric, operability, seams).
- Targets: AD-3; Release & Recovery: Before production update; Conventions: Module declaration authority; Deferred: Release attempt state, checks and notifications.
- Verification: confirmed. PRD L238: the architecture must define the compatibility evidence; spine L140-142 have no compatibility row; Deferred L236 says only 'prove schema/event compatibility'.

**Defect.** PRD NFR-1 says the architecture must define the compatibility evidence and the rollback mechanism. The spine defines the mechanism but only restates that previous code must read current schemas and events, and it defers the proof. There is no module-declared evidence artifact, no gate row, no release-record binding, and no rule that evidence targets production's recorded working release rather than staging's predecessor.

**Failure scenario.** Release N writes a new event type. Rollback to N-1 succeeds at Helm readiness, then N-1 projections or smoke checks poison on the events written during the window.

**Recommendation.** Add a Before-production-update gate. Each module supplies a change classification (none, additive or breaking) plus an automated staging rehearsal in which production's recorded working release reads state and events written by the candidate. Missing, breaking or wrong-baseline evidence disables automatic promotion and routes the release to the separately planned procedure. Add the field to the declaration and bind the evidence in the release record.

### V-10: MCP surface and delegation restrictions rely on client-asserted identity

**High**, disposition **discuss**.

- Sources: SEC-10, RUB-5, SEAM-10 (lenses: security, rubric, seams).
- Targets: AD-11; AD-6; Deferred: Connected McpCli metadata and contract parity.
- Verification: confirmed. McpCli spine L243 StaticBearerTokenHandler and AD-13/14 single profile token; spine AD-11 L112 claims gateway surface enforcement with no authenticated surface signal.

**Defect.** AD-11 says the gateway enforces module surface eligibility and confirmation restrictions (Parties has no MCP erasure, Projects no MCP confirmation), and AD-6 keeps actor plus workload/delegation checks. But McpCli's CLI and MCP heads share one static user bearer token, so the gateway has no authenticated surface or workload signal, and no delegation issuer is named.

**Failure scenario.** A prompt-injected agent holding an administrator token calls EraseParty through the CLI head or curl, and the gateway sees an ordinary authorized call. Conversely, every Projects call through McpCli fails closed because no workload credential is ever presented.

**Recommendation.** Bind the surface to authenticated client identity: per-environment Keycloak clients per surface, with azp mapped to the surface. Agent-host credentials are delegated or token-exchanged and carry a workload marker. UI-only or human-confirmation operations require the owning UI client or a fresh step-up claim that an agent host cannot mint. Name the EventStore gateway as the delegation issuer. Until this exists, such operations are not executable with McpCli tokens.

### V-11: Staging gate is not bound to the production runtime profile

**High**, disposition **autofix**.

- Sources: ADV-3, RUB-7 (lenses: adversarial, rubric).
- Targets: AD-2; Conventions: Production profile authority; Release & Recovery: Staging gate.
- Verification: confirmed. Spine L58 and L128 tie the profile to production only; Staging gate row L140 names no profile; EventStore AD-26 treats Redis as dev/test only.

**Defect.** Only the canonical production-profile.yaml binds runtime, components, ACLs and resiliency. AD-2 allows separately versioned environment configuration without limiting what it may change, so staging can produce exact-release E2E evidence on a different state store, broker or runtime from production.

**Failure scenario.** To save resources on the single node, staging runs Redis state and pub/sub while production runs PostgreSQL actor state and a durable broker. E2E passes, and redelivery and actor-state defects appear only in production.

**Recommendation.** Add: staging runs the canonical profile's runtime, component types, scopes and ACLs, resiliency and catalog/secret-contract structure. It may differ only in declared environment bindings (namespace, instances, credentials, hosts, realm and replica scale). Staging evidence binds the profile digest, and promotion requires that digest to equal production's active profile digest.

### V-12: Verification window verdict is not total and inputs are unmeasurable

**High**, disposition **autofix**.

- Sources: OPS-1, RUB-19 (lenses: operability, rubric).
- Targets: Release & Recovery: Verification; Release & Recovery: Automatic recovery; AD-12.
- Verification: confirmed. Spine L144 fails on three triggers and declares working only when the predicate holds; no outcome for neither. Downgraded from critical: single Platform-owned workflow, and a conservative reading exists.

**Defect.** The Verification row has three failure triggers and a working predicate, but no outcome when the predicate is false at window end and no trigger fired. Check cadence, per-attempt timeout, the unavailability probe interval and release attribution are undefined. Three PRD details were dropped: a single failed probe or restart does not by itself trigger rollback, checks must identify the intended release, and SM-4 re-verification is required after access-configuration changes.

**Failure scenario.** The single-replica Tenants UI pod restarts at 4:50; implementation A rolls back, B extends the window, and C waits and declares the release working. A smoke check that passes against a surviving old replica marks a broken release as working.

**Recommendation.** Make the verdict total: working only if the predicate holds at window end, otherwise failed, with one bounded grace period for an in-flight or pending +30 s retry. Smoke checks have stable declared IDs, run at window start and then at a fixed interval, and a timeout counts as a failure. Every result carries the served release, and results from other releases count as missing. Sample unavailability at 10 s or less. Bind SM-4 re-verification, and suspend promotion while any recovery is in progress.

### V-13: Only release attempts are serialized; rotations, catalog commits, DR race

**High**, disposition **autofix**.

- Sources: ADV-6, OPS-8 (lenses: adversarial, operability).
- Targets: Release & Recovery: Deployment ownership; AD-3; AD-8; Conventions: Secrets; Conventions: Production profile authority.
- Verification: confirmed. Spine L141 scopes serialization to release attempts; EventStore architecture L256 AD-24 requires a controlled rollout for token rotation; memlog L65 records one shared Dapr 1.18.1 control plane.

**Defect.** The per-environment lock covers release attempts only. Credential rotation rollouts (EventStore AD-24 requires a controlled sidecar/workload rollout), catalog commits, config-only changes, control-plane upgrades and DR restores all mutate the same workloads. The one cluster-wide Dapr control plane serves both environments, so runtime changes cannot be qualified in staging first, and sidecars can drift from the profile at the next restart.

**Failure scenario.** The Administrator rotates the app-channel token during release N's verification window. The pod restarts fire the 60-second trigger, and recovery restores N-1 with an unacknowledged generation, so readiness fails.

**Recommendation.** Put every workload-affecting change under one per-environment attempt lock and record: release, rollback, config-only change (as a release attempt), rotation rollout, catalog activation, profile or shared-infrastructure change and DR restore. Entering DR sets the promotion stop. Each shared cluster service has one named change owner, changes apply only while no attempt is open in either environment, and workloads pin their sidecar runtime image.

### V-14: Rollback target catalog, key generations and authority are not composed

**High**, disposition **discuss**.

- Sources: ADV-5, OPS-6 (lenses: adversarial, operability).
- Targets: AD-3; AD-2; Conventions: Shared host and catalogs; Conventions: Secrets; Release & Recovery: Automatic recovery.
- Verification: confirmed. EventStore architecture L279: the idempotency facet binds active/reader digest-key generations and refuses retirement while referenced; L331 rolls back only on activation failure.

**Defect.** EventStore AD-33 rolls back catalog generations only when activation fails, and AD-25 refuses retirement while idempotency records or key generations are still referenced. Once a committed release has added a route or key generation, re-activating N-1's catalog is refused and AD-3 forbids rewinding keys. The only valid target is then an unvalidated forward generation nobody owns, and secret retirement or expired authority records can also invalidate the recorded target.

**Failure scenario.** Release N adds an idempotent command, smoke writes create admission records, and verification fails. Re-committing N-1's catalog is refused, keeping N's catalog fails N-1's root-digest check, and a merged generation was never validated, so every path ends in recovery failed.

**Recommendation.** Before rollout, record and prepare/ready-validate the rollback combination: previous package and configuration, current secret references, and a forward catalog generation with the previous routes plus every still-referenced idempotency and key entry. Until the release is working, forbid catalog commits and secret or key retirements that this combination cannot satisfy. Staging proves the N-to-N-1 generation. Get agreement from the EventStore AD-25/AD-33 owner.

### V-15: No handshake between EventStore publication authority and Platform release records

**High**, disposition **discuss**.

- Sources: SEAM-3, ADV-4 (lenses: seams, adversarial).
- Targets: AD-2; Release & Recovery: Deployment ownership; Release & Recovery: Automatic recovery; Deferred: Release attempt state, checks and notifications.
- Verification: confirmed. EventStore architecture L170: the lifecycle requires separate authenticated release-owner and deployment-owner records; spine L58 binds a release record but names no issuer or required state.

**Defect.** Platform is EventStore's deployment owner, but the spine never says:

- that the release workflow issues the AD-11/AD-26 production-promoted record;
- which lifecycle state (evidence-validated or release-available) staging and production require;
- that a rollback target must hold a valid record for the active profile digest.

The Platform release record also has no shape owner, although Builds already owns ReleaseEvidenceCodec and the readiness-evidence validators.

**Failure scenario.** EventStore waits for staging evidence before issuing release-available while Platform staging waits for release-available, and the two deadlock. Or Platform promotes without writing production-promoted, and AD-22 legacy retirement stays blocked for every module.

**Recommendation.** Add a per-environment table of required module lifecycle states: staging accepts evidence-validated candidates, and production requires release-available. The trusted release workflow is the registered deployment-owner issuer and emits production-promoted, bound to the profile digest, before traffic. A release counts as previous working only while its record is valid for the active profile digest. Builds validators are the encoding authority for the release record.

### V-16: Module-owned deployment manifests coexist with the Platform package in production

**High**, disposition **autofix**.

- Sources: ADV-9, BRN-8 (lenses: adversarial, brownfield).
- Targets: AD-1; AD-2; Conventions: Migration; Conventions: Production profile authority.
- Verification: confirmed. Memories overlays/production/kustomization.yaml uses namespace hexalith-memories; Folders sidecar-config-bindings.yaml patches hexalith-eventstore and hexalith-tenants in hexalith-production.

**Defect.** Several modules ship their own production deployment assets:

- Memories: a production Kustomize overlay (namespace hexalith-memories) and OpenBao values;
- Folders: deploy/dapr/production patches for the hexalith-eventstore and hexalith-tenants Deployments;
- Tenants and EventStore: deploy/dapr assets.

The spine makes the retained Platform package the deployment but never says these are only qualification inputs, so two definitions can target the same workloads and Dapr Configurations.

**Failure scenario.** Operators apply the Memories overlay alongside the Platform release. Two Memories writers then operate on one tenant population and register lineage, and AD-2 rollback no longer covers what is running.

**Recommendation.** Add to AD-1: in staging and production, the retained Platform package is the only deployment definition for enrolled modules' workloads, Dapr resources and data services. Module deploy assets are declarations or conformance inputs. Legacy deployments stay in their own namespaces and are never a second writer; cutover is a planned attempt that fences the legacy writer. Consolidate the per-app Dapr Configuration for shared app IDs.

### V-17: Startup tasks lack lifecycle class; recovery can re-run Memories genesis

**High**, disposition **autofix**.

- Sources: ADV-12, SEAM-19 (lenses: adversarial, seams).
- Targets: Conventions: Module declaration authority; AD-10; AD-12.
- Verification: confirmed. Memories spine L215: a named platform-bootstrap step alone creates the genesis marker, and ordinary startup may only verify it. Spine L124 lists startup tasks with no class.

**Defect.** Declarations list readiness and startup tasks without separating per-start verification from once-per-environment or population-creating bootstrap. Memories AD-21's platform-bootstrap step alone creates the register genesis marker and populationId, and Memories states that starting a new population is not recovery.

**Failure scenario.** A DR restore onto replacement capacity runs the declared startup tasks; the genesis task finds no live lineage and mints a new population, permanently stranding every tenant's restores.

**Recommendation.** Each startup task declares a lifecycle scope (per-start idempotent verify, once-per-environment creation, or operator-only) and an authority class. Platform runs creation tasks only when creating a new environment, never on upgrade, rollback, restore or replacement-capacity recovery. Tasks that create a population or authority are operator-only in hosted environments.

### V-18: McpCli discovery endpoint, compatibility key and release order unowned

**High**, disposition **discuss**.

- Sources: SEAM-6, ADV-15 (lenses: seams, adversarial).
- Targets: AD-11; Deferred: Connected McpCli metadata and contract parity.
- Verification: confirmed. apphost.cs L102 registers contract Version v1; spine Deferred L235 gives the metadata capability three co-owners and no compatibility predicate.

**Defect.** Connected discovery needs four things, and none is fixed:

- a gateway metadata endpoint, currently co-assigned to three owners;
- a per-operation compatibility predicate (McpCli uses Contracts package versions, while the catalog records a coarse contract version such as v1);
- one declaration site for surface eligibility that fits McpCli's equal-heads rule;
- a release order across EventStore.Contracts, module Contracts, McpCli and Platform.

**Failure scenario.** Either every operation is unknown and nothing is executable, so FR-12 fails, or v1 is read as matching any package and incompatible operations are advertised as executable. A Parties Contracts bump breaks McpCli's locked dependency closure.

**Recommendation.** Make EventStore the owner of a versioned gateway metadata endpoint derived from the committed catalog. Define the compatibility key once in the catalog schema, for example a contract-schema digest from the shared codec rather than package versions or vN strings. Choose one declaration site for surface eligibility and amend McpCli AD-5/AD-12. Fix the release order and the skew behaviour: an older McpCli shows affected operations as non-executable.

### V-19: Key-bearing backups defeat crypto-shredding; restore lacks quarantine and admission

**High**, disposition **discuss**.

- Sources: SEC-4, OPS-10 (lenses: security, operability).
- Targets: Release & Recovery: Backup coverage and cadence; Release & Recovery: Memories erasure continuity; Release & Recovery: Disaster recovery evidence; AD-12; Deferred: Memories recovery continuity and implementation gaps.
- Verification: confirmed. Memories spine L177-178 operational-backup and no-longer-decrypts rules; memlog L129 daily OpenBao raft snapshot and L141 whole-state restore; spine L146/L149 guard only the restore path.

**Defect.** Memories AD-16 lets backups remain only while tenant payloads are protected solely by tenant keys that erasure destroys, and requires proof that captured ciphertext no longer decrypts. The spine requires 7- and 30-day backups of shared services (OpenBao holds the tenant keys) plus independent decryption access, and it guards only the restore path against key resurrection. Nothing binds key-bearing backups at rest, backup immutability, drill-restore lifetime or a restored-but-not-admitted stage, and EventStore payload protection is a no-op by default.

**Failure scenario.** Tenant T is erased on day 1. Anyone with backup-bucket and decryption access can pair the day-0 EventStore backup with the day-0 OpenBao snapshot and read T's payloads for up to 30 days, or a DR restore of a day-9 snapshot makes T readable before reconciliation runs.

**Recommendation.** Make tenant-key custody a separate backup class whose escrow entries are destroyed on erasure (or wrap keys under per-period keys that are themselves destroyed), never held under the same custody as ciphertext backups. Make data backups immutable, and drill restores ephemeral and egress-denied. Extend the AD-12 sequence: restore the key store, re-apply tombstone key destruction, restore data into quarantine, run module admission and purge, then rebuild. Require tenant payload protection for EventStore backups of Memories partitions.

### V-20: Data services and persistent objects have no version authority or rollback class

**High**, disposition **discuss**.

- Sources: ADV-7, OPS-7 (lenses: adversarial, operability).
- Targets: AD-3; AD-2; AD-8; Conventions: Production profile authority; Release & Recovery: Automatic recovery; Design Paradigm.
- Verification: confirmed. Memories spine L202 AD-19 makes Memories.Aspire the sole digest owner; spine L64 excludes persistent data and shared infrastructure but not per-environment data-service instances; L145 leaves the Helm mechanism open.

**Defect.** Per-environment data services (PostgreSQL, Redis Stack, FalkorDB, broker) have two image authorities, the Memories.Aspire digest set and the EventStore-ratified profile. AD-3 counts them as neither persistent data nor shared infrastructure, so their rollback class is undefined. The spine also leaves open:

- whether recovery is a helm upgrade to the recorded identities or a helm rollback;
- whether Helm's atomic auto-rollback is forbidden;
- what 'no workload changed' means for config-only changes;
- whether an upgrade to the previous package may delete chart-owned PVCs (currently it can).

**Failure scenario.** A release upgrades FalkorDB, and rollback redeploys the older image over the newer on-disk format; or a newly enrolled module's chart-owned PVC is deleted during the single rollback.

**Recommendation.** Add to AD-3: data services, brokers, PVCs and cluster-scoped objects are not application-release objects. They are deployed and upgraded forward-only outside the Helm application release, or carry keep policies, and each has exactly one version authority (a profile facet, with module digest sets as qualification inputs). Recovery is an upgrade to the recorded previous identities, Helm auto-rollback is forbidden, and 'changed' means any release-owned rendered object digest differs.

### V-21: No outage detection, response bound or hosted operational envelope for RTO

**High**, disposition **discuss**.

- Sources: OPS-11, RUB-16 (lenses: operability, rubric).
- Targets: Release & Recovery: Disaster recovery evidence; Release & Recovery: Automatic recovery; Conventions: Diagnostics and notification; AD-12; AD-3; AD-8.
- Verification: confirmed. Spine L131 notifies only on deployment, recovery or backup failures; L148 puts detection and response inside the RTO; L145 excludes later incidents; memlog L142 starts the clock at the outage.
- Settled decision: The accepted PRD four-hour outage-to-verified-service RTO is an inherited constraint (memlog L13, L131, L142); scoping it to staffed hours would reinterpret it.

**Defect.** The four-hour RTO runs from the outage and includes detection and operator response. Yet only deployment, recovery and backup failures notify, nothing probes production availability outside release windows, and the single Administrator's response has no hours or acknowledgement bound, so drills with a forewarned operator cannot measure it. The single-node envelope says nothing about a telemetry sink or retention, staging-versus-production resource isolation, shared-infrastructure change or cost.

**Failure scenario.** The node's disk fails at 19:00 on a Friday. The backup-failure notice arrives around 19:30 and is read on Monday, missing the RTO by days while every drill passed. Or a staging load test evicts production pods and blocks promotion.

**Recommendation.** Add an off-site availability probe of production ingress and readiness with a stated detection bound. Make response a measured parameter (a staffed-hours RTO scope or a maximum acknowledgement time), and require drills to include measured detection and documented worst-case response, or to be unannounced. Add a production PriorityClass with staging quotas and limits, a hosted telemetry sink with minimum retention, and the statement 'no cost target; capacity budget is seed'.

### V-22: Shared Dapr control plane lacks per-environment trust domain and caller rule

**High**, disposition **autofix**.

- Sources: SEC-2 (lenses: security).
- Targets: AD-8; AD-9; Conventions: Hosted interfaces; Conventions: Production profile authority.
- Verification: disputed. EventStore's hosted ACL template is defaultAction deny with namespace-qualified policies; the cited allow fall-through is local-dev config. Shared default trust domain and bare app-ID allowlists hold; downgraded to high.

**Defect.** Staging and production share one observed Dapr control plane (Sentry CA, placement, scheduler). App IDs are identical across environments, and the EventStore AllowedCallers and Memories system allowlists key on bare app ID. The spine binds no hosted default-deny ACL, per-environment trust domain, environment-qualified caller identity or cross-environment invocation negative test, and it does not classify the control plane.

**Failure scenario.** A compromised staging workload invokes eventstore in the production namespace; if the production policy is not namespace-qualified, the production app sees an allow-listed caller app ID and accepts internal operations.

**Recommendation.** Add a convention: hosted sidecar Configurations use a top-level defaultAction deny and allow-list callers by trust domain, namespace and app ID. Each environment has its own trust domain, application caller allowlists use environment-qualified identity, and cross-environment invocation is a mandatory NFR-3 negative test. Classify the Dapr control plane as production-critical shared infrastructure with no staging write access.

### V-23: OpenBao is neither classified nor restorable per environment

**High**, disposition **discuss**.

- Sources: SEC-3 (lenses: security).
- Targets: Conventions: Secrets; AD-8; AD-12; Release & Recovery: Memories erasure continuity.
- Verification: confirmed. Memlog L65/L129 record one OpenBao 2.6.2 instance with a daily raft snapshot, and L141 a whole-state snapshot restore; the spine Secrets row and AD-8 do not classify it.

**Defect.** OpenBao holds application secrets, app-channel tokens and tenant key material, but the spine never says whether it is AD-8 per-environment application state or reusable supporting infrastructure. Only one instance, with a daily raft snapshot, was observed, and the secret contract has no environment dimension. A raft snapshot restore returns the whole store to the snapshot state, and a shared instance shares root and unseal custody and the policy plane.

**Failure scenario.** An operator restores yesterday's OpenBao snapshot to fix a staging secret, reviving production tenant keys erased today and credentials rotated today.

**Recommendation.** Classify key and secret authority as per-environment application state with a separate OpenBao instance per environment, consistent with AD-8. If the instance stays shared, require per-environment auth, secret and transit mounts, no staging principal with sys, policy or auth write, and a restore granularity that cannot rewind the other environment. Add the environment dimension to the secret contract and name the unseal and recovery-key custodians.

### V-24: AD-8 isolation lacks pod security and default-deny network policy

**High**, disposition **autofix**.

- Sources: SEC-7 (lenses: security).
- Targets: AD-8; Conventions: Hosted interfaces; Deferred: Secrets, identity, network and transport setup.
- Verification: confirmed. Spine AD-8 L94 names no mechanism; memlog L64 records one Ready node with OpenEBS local hostpath storage.

**Defect.** AD-8 says namespace separation alone is insufficient but names no enforcement mechanism. On the observed single node with local storage, a principal who can create staging pods can mount hostPath and read production volumes unless Pod Security Admission blocks it. Without enforced default-deny NetworkPolicy, staging pods reach production data services, sidecars and admin endpoints, and the NFR-3 tests cover staging users, not staging pods.

**Failure scenario.** A staging workload with a remote-code-execution bug creates a pod with a hostPath mount and copies the production EventStore PostgreSQL data directory.

**Recommendation.** Add to AD-8: both environment namespaces enforce Pod Security restricted, with default-deny ingress and egress NetworkPolicies on a CNI verified to enforce them. NFR-3 negative tests run from a staging pod against production data services, app ports, sidecars and volumes. Record the shared-kernel single-node residual risk as explicitly accepted.

### V-25: Shared ingress and certificates have no environment-bound host ownership

**High**, disposition **autofix**.

- Sources: SEC-6 (lenses: security).
- Targets: Conventions: Hosted interfaces; AD-6.
- Verification: plausible. Spine L130 confirms shared existing ingress and certificate infrastructure with no host-admission rule; controller merge behaviour was not checked.

**Defect.** One ingress controller and one certificate infrastructure serve both hexalith.com and tache.ai, and the same Helm package templates hostnames from environment configuration. Nothing stops a staging release from declaring a tache.ai or Keycloak host, or issuing a tache.ai certificate through a shared ClusterIssuer, and the production issuer hostname is undecided.

**Failure scenario.** A staging values file copies a tache.ai host for a new path, the controller merges it, and production users' API calls carrying production tokens reach a staging pod.

**Recommendation.** Add a convention: hostnames are owned per environment and enforced at admission (a policy engine or a per-environment ingress class). Only the production namespace may declare tache.ai and production issuer hosts, and certificates use namespaced Issuers or policy-enforced ClusterIssuer selectors. The production issuer lives under a production-controlled hostname, and a staging release declaring a production host is a required negative test.

### V-26: Keycloak administration credentials can bridge staging and production realms

**High**, disposition **autofix**.

- Sources: SEC-8 (lenses: security).
- Targets: AD-6; AD-3; Deferred: Secrets, identity, network and transport setup.
- Verification: plausible. Spine AD-6 L82 and Deferred L238 say nothing about realm administration credentials; the master-realm path is a likely default, not an observed configuration.

**Defect.** AD-6 separates application realm credentials but says nothing about who administers realms, clients, mappers and E2E test users, or with what credential. The simplest implementation is a master-realm admin credential stored as a runner or E2E secret, which also controls the production realm.

**Failure scenario.** The staging E2E fixture provisions synthetic users with a master-realm admin credential that then leaks from the staging path, and the attacker adds themselves to the production admission group or adds a redirect URI to a production client.

**Recommendation.** Add to AD-6: realm administration is realm-scoped, and no automation, runner or fixture holds master-realm or cross-realm admin. Staging provisioning uses a staging-only management client. Production realm user, admission, client, IdP and mapper changes are Administrator-only, with admin events exported off-cluster. The master realm and admin consoles stay off the public ingress.

### V-27: McpCli credentials are not bound to their environment's gateway

**High**, disposition **autofix**.

- Sources: SEC-9 (lenses: security).
- Targets: AD-11; Deferred: Connected McpCli metadata and contract parity.
- Verification: confirmed. McpCli spine L134 resolves each setting independently from flag, environment, profile and default, including EVENTSTORE_URL and EVENTSTORE_TOKEN; static bearer handler at L243.

**Defect.** The issuer check stops staging tokens reaching production but not production tokens reaching staging. McpCli resolves each setting independently (flag, environment, profile, default), so a production EVENTSTORE_TOKEN can combine with a staging profile URL, and tokens are long-lived static bearers stored in plaintext profile files.

**Failure scenario.** An agent session exports a production token and runs with --profile staging; the staging gateway, its logs or a compromised staging pod captures a production bearer that stays valid for its whole lifetime.

**Recommendation.** Add to AD-11: a credential is sent only to the gateway of its issuing environment. Profiles record the expected issuer and audience, and the client refuses mismatched tokens and never mixes URL and token from different sources. Hosted access uses short-lived per-environment OIDC tokens with refresh material in an OS credential store, and gateways never log or persist bearer tokens. The mechanism stays with McpCli and EventStore.

### V-28: Disaster restore can resurrect revoked access and compromised credentials

**High**, disposition **autofix**.

- Sources: SEC-5 (lenses: security).
- Targets: AD-12; Release & Recovery: Disaster recovery evidence.
- Verification: confirmed. Spine AD-12 L118 establishes identity and secrets but has no revocation continuity; DR evidence L148 checks only production-user success and staging denial; AD-3's no-rewind rule covers rollback only.

**Defect.** Restoring Keycloak and OpenBao from a recovery point revives admissions revoked after that point, rotated client secrets, realm signing keys, app-channel tokens and data-service passwords. A compromise-driven restore also revives every credential the attacker holds. DR checks cover only production-user success and staging-user denial, and deletion authority has a continuity protocol that revocation lacks.

**Failure scenario.** A contractor's production admission is removed at 10:05, storage fails at 10:50, and the newest usable point is from 10:00, so the restored realm re-admits the contractor.

**Recommendation.** Add to AD-12 and the DR evidence row: rotate every restored credential and signing key before reopening. Re-apply an off-site, append-only journal of admission revocations, user disables and secret revocations made after the recovery point, and require a negative check that they are denied. For a compromise-driven restore, also rotate the Dapr trust root and realm keys.

### V-29: Interrupted release attempts: timer anchoring, resumption and late rollback undefined

**High**, disposition **discuss**.

- Sources: OPS-2 (lenses: operability).
- Targets: Release & Recovery: Deployment ownership; Release & Recovery: Rollout; Release & Recovery: Verification; Release & Recovery: Automatic recovery; AD-7.
- Verification: confirmed. Spine L141 text confirmed with no ambiguity definition or recovery horizon; the PRD has no interruption rule. Downgraded from critical: single Platform-owned workflow, and a conservative stop reading exists.

**Defect.** The spine says a retried or interrupted job reconciles state before mutating, and that ambiguity stops promotion. It does not say:

- whether t0 and t1 are durable;
- whether a resumed job may open a new window;
- whether an observation gap invalidates the window;
- what counts as ambiguity;
- how late an automatic rollback may start.

**Failure scenario.** The runner host reboots at verification minute 3 and returns 40 minutes later with the intended release ready. One compliant implementation rolls back over 40 minutes of live writes, a second opens a new window and promotes, and a third holds.

**Recommendation.** Timers anchor to the recorded t0 and t1 and never restart, and any observation gap invalidates the window. An interruption detected within t1 + 5 min + grace fails the attempt, which uses its single recovery; a later detection stops for intervention with no automatic mutation. List the ambiguity conditions: record and cluster disagree, actual release unknown, lock held by an unknown owner, record unreadable.

### V-30: Promotion stop, its clearing and working-release re-baselining are undefined

**High**, disposition **discuss**.

- Sources: OPS-3 (lenses: operability).
- Targets: Release & Recovery: Automatic recovery; Release & Recovery: Before production update; Release & Recovery: Deployment ownership; AD-12.
- Verification: confirmed. Spine L142 and L145 confirmed: the stop exists without durability, scope, clearing record or re-baseline path.

**Defect.** The promotion stop after a deployment failure is left open in several ways:

- it is not declared durable;
- its scope is unstated (does a staging failure stop production?);
- no actor or record clears it;
- 'existing unhealthy production' has no measure;
- nothing re-establishes the previous working release after a manual repair, manual deployment or DR restore;
- queued candidates have no promotion order.

**Failure scenario.** Recovery fails, the Administrator deploys N-2 by hand and clears the stop by editing a variable. One pre-check then treats the failed N-1 as the rollback target, while another sees record and cluster disagree and stops forever.

**Recommendation.** The promotion stop is a field of the durable per-environment record. Every non-working terminal outcome and every DR entry sets it, and only an authenticated Administrator record clears it, binding the reason and a verified current working release. Every manual or DR change must pass the verification predicate to write a new baseline; until then automatic promotion stays blocked. Define pre-update health as 'recorded working release ready and its smoke suite passing now', and state staging behaviour explicitly.

### V-31: Attempt record location unfixed; runner death is silent and unwatched

**High**, disposition **autofix**.

- Sources: OPS-4 (lenses: operability).
- Targets: Release & Recovery: Deployment ownership; AD-7; Conventions: Diagnostics and notification.
- Verification: confirmed. Spine L141 'durably beyond the runner process' and AD-7 L88 'prefer outside the cluster' confirmed; no watchdog or record location is defined.

**Defect.** 'Durably beyond the runner process' still allows storage on the runner's disk or in the cluster, and both are lost with the site. The same single self-hosted runner executes attempts and sends notifications, so if it dies mid-attempt nobody hears about it. Nothing detects an attempt that outlives its maximum lifetime, staging jobs can queue ahead of recovery, and 'prefer outside the cluster' is not a requirement.

**Failure scenario.** The runner VM shares a host with the node. The host reboots during verification, an unverified release keeps serving, and nobody is notified for days.

**Recommendation.** Store the attempt record, promotion stop and working baseline outside both the target cluster and the runner host, for example in GitHub deployment records or the off-site store. Add an independent watchdog that notifies when a record stays non-terminal past its maximum lifetime. Run a production attempt from lock to terminal outcome in one job, or reserve runner capacity. Make 'outside the cluster' a rule for the production executor, or record the accepted risk.

### V-32: Usable recovery point, its age and cross-module set are undefined

**High**, disposition **autofix**.

- Sources: OPS-9 (lenses: operability).
- Targets: Release & Recovery: Backup coverage and cadence; Release & Recovery: Recovery freshness; Release & Recovery: Disaster recovery evidence; AD-12.
- Verification: confirmed. Spine L146-148 confirmed; memlog L129 records a daily OpenBao snapshot and no CNPG backup for keycloak-postgres.

**Defect.** The spine says what is not evidence of a usable recovery point but never what is. It does not fix whether age runs from the data cut or job completion, or whether RPO and freshness apply per job, per store or to the jointly restorable set. Observed stores differ widely (PostgreSQL WAL is possible, OpenBao is snapshotted daily, the Keycloak database is not backed up), and the addendum's per-point release and configuration record is dropped.

**Failure scenario.** EventStore WAL is 2 minutes old and the OpenBao snapshot 23 hours old. A per-job monitor reports green while a per-set monitor alerts continuously, and a drill proves a 1-hour RPO while a real restore yields mismatched keys and events.

**Recommendation.** Define a recovery point as the set of per-inventory artifacts that share a declared cut, together with:

- the compatible release and configuration identity;
- the Memories register populationId and sequence;
- verified integrity (checksums, an unbroken chain, decryptability with independent keys).

Age is failure time minus cut. The monitor reports the newest complete set and gives an earlier warning, and pruning skips points referenced by an open recovery.

### V-33: Fence old writers is undefined in scope and proof

**High**, disposition **autofix**.

- Sources: OPS-12 (lenses: operability).
- Targets: AD-12; Release & Recovery: External effects.
- Verification: confirmed. Spine AD-12 L118 'Fence old writers' has no scope or proof; the External effects row L150 covers only provider reconciliation.

**Defect.** Fencing is the first recovery step, but the spine lists no writers and no evidence that a fence holds. A returning node restarts application pods, actors and reminders, workflow workers, backup CronJobs writing to the same repository paths, the deployment runner and Folders provider mutations.

**Failure scenario.** After cutover, the old node returns and its backup CronJob uploads a new base and WAL into the shared prefix, silently breaking the restored environment's backup chain.

**Recommendation.** Define the fence as revoking credentials and authority, not network reachability. Revoke the old environment's database, broker, OpenBao, backup-write and deployment credentials, and give each backup repository a per-environment-instance prefix. Set the promotion stop and keep external-effect workers disabled until module reconciliation. Record a fence check (old credentials fail) before the restore proceeds.

### V-34: Production entry gate is ambiguous; Memories mirror silently blocks every module

**High**, disposition **discuss**.

- Sources: OPS-15 (lenses: operability).
- Targets: AD-12; Release & Recovery: Memories erasure continuity; Deferred: Memories recovery continuity and implementation gaps; Deferred: Recovery capacity and coverage; Structural Seed.
- Verification: confirmed. Spine L118/L149/L207 and Deferred L239-240 confirmed; Memories spine L216 makes EventStore restore fail closed while the register is unavailable.

**Defect.** 'Before production' could mean first deployment into the production namespace, admitting users, or enabling automatic promotion, and whether the required drill covers site loss is phrased conditionally. On the observed single node with local storage, any storage failure loses the live register lineage. The deferred Memories tombstone mirror is therefore a prerequisite for any passing DR drill, and so for production of every domain module, but the wording hides this.

**Failure scenario.** The Parties team plans production after its own gates, unaware that user access is blocked on an unfunded, deferred Memories/EventStore feature without which the drill cannot pass.

**Recommendation.** Name the gates in order:

- G1: deployment into the production namespace allowed;
- G2: users admitted and ingress opened only after the AD-12 drill passes, including Memories continuity;
- G3: automatic promotion enabled after the SM-5 rehearsals.

State that on the observed topology the mirror is a G2 prerequisite for every composition that includes Memories, and decide whether site loss is in the required drill scope.

### V-35: State with conflicting recovery classes can share one backup unit

**High**, disposition **autofix**.

- Sources: ADV-8 (lenses: adversarial).
- Targets: AD-8; AD-12; Release & Recovery: Backup coverage and cadence; Release & Recovery: Memories erasure continuity.
- Verification: confirmed. HexalithMemoriesServerExtensions.cs remarks: the server reuses the consumer stateStore, typically the shared EventStore state store; spine AD-8 L94 allows compatible sharing without a recovery class.

**Defect.** Memories.Aspire reuses the consumer's state store, typically the shared EventStore PostgreSQL statestore. Memories checkpoints, leases, fences and mappings, which have no operational backup-restore path, therefore sit in the instance Platform PITR-restores as EventStore's authoritative store. 'Compatible modules may share an instance' ignores recovery class.

**Failure scenario.** A disaster restore rewinds Memories checkpoints to Indexed while Redis and FalkorDB rebuild empty through replay, so queries return incomplete results labelled complete, which violates the Memories AD-3 reprojectionRequired rule.

**Recommendation.** Each declared state binding carries a recovery class: authoritative-restore, rebuild-only or live-authority-only. Platform never puts different classes in one backup unit; where co-location is unavoidable, the restore sequence invalidates rebuild-only state as its owner prescribes before reopening.

### V-36: Profile ratification cadence conflicts with per-release catalog and digest changes

**High**, disposition **discuss**.

- Sources: SEAM-2 (lenses: seams).
- Targets: Conventions: Production profile authority; Conventions: Shared host and catalogs; Stack.
- Verification: confirmed. EventStore architecture L287: the profile digest binds route/idempotency catalog digests, and changing it needs an approved architecture change; spine L128 requires an AD-26 owner-ratified change.

**Defect.** The canonical profile binds catalog and secret-contract digests and exact release authority, and replacing it requires an EventStore AD-26 owner-ratified change. But the routing catalog changes whenever any module adds a route, and Memories.Aspire digests change independently. Every module release therefore needs EventStore ratification or ships under a profile that no longer describes production, and the spine never says where Keycloak and data-service pins live.

**Failure scenario.** Projects adds a command and the catalog root digest changes; Platform must then either get a new EventStore architecture approval or bind production-promoted records to a stale profile digest.

**Recommendation.** Split the ratified profile template (runtime, component types and versions, sidecar mode, ACL policy shape; changed only through AD-26) from the per-release bindings (catalog generation, secret-contract digest, app-ID set, module and Memories.Aspire image digests). The per-release bindings live in the Platform release record under a Platform-owned rule. Name the file that holds the Keycloak and data-service pins, and get EventStore to ratify the split.

### V-37: Folders override leaves recovery dependency and non-Memories erasure resurrection unowned

**High**, disposition **discuss**.

- Sources: SEAM-11 (lenses: seams).
- Targets: Source Precedence; Release & Recovery: Memories erasure continuity; Deferred: Folders source alignment; AD-12.
- Verification: confirmed. Folders architecture L226 EXT-ES-RECOVERY requires PostgreSQL v2 and a recovery-safety export; EventStore architecture L362 says v2 has no v1 migration path; spine L242 calls realignment documentation.
- Settled decision: The user settled the Folders operational override and said not to block Folders enrollment (memlog L153-154); any re-plan must respect that.

**Defect.** The override replaces Folders' RPO, retention and topology envelope, but the Folders stories that produce its recovery inventory and drill evidence still depend on EXT-ES-RECOVERY (PostgreSQL v2, a recovery-safety export, restored-backup admission). EventStore does not plan that work, and v2 has no v1 migration path. The spine calls the realignment documentation work, and it guarantees non-resurrection after restore only for Memories tombstones, not for Folders deletions and legal holds or Parties erasures inside the one-hour RPO window.

**Failure scenario.** A drill restores a point 50 minutes old, and a Parties erasure or Folders deletion acknowledged in the last hour reappears; meanwhile Folders recovery evidence waits on an EventStore deliverable that never arrives.

**Recommendation.** Make the Folders re-plan an owned prerequisite of Folders' recovery evidence (not an enrollment block), and decide whether the recovery-safety export stays a data-safety rule owned by EventStore or is dropped. Add a general rule that acknowledged erasures, deletions and legal holds are re-applied from an off-site journal after restore, or record owner-accepted RPO exceptions for Parties and Folders.

### V-38: Aspire-to-Helm primary path is preview-only and exports no Dapr sidecars

**High**, disposition **discuss**.

- Sources: REAL-1 (lenses: reality).
- Targets: AD-1; AD-2; Deferred: Aspire-to-Helm qualification; Stack; Conventions: Secrets.
- Verification: plausible. EventStore.Aspire HexalithEventStoreExtensions.cs L40-47 and L240-277 skip Dapr and resiliency wiring in publish mode, leaving it to kustomize or Helm values; toolkit behaviour and package status are web claims.
- Settled decision: The user adopted AD-1 Option 1 (an Aspire model exporting Helm) with qualification and fallback criteria (memlog L29, L33); bringing the fallback forward changes that decision.

**Defect.** Aspire.Hosting.Kubernetes has never shipped a stable release, and the CommunityToolkit Dapr integration skips sidecars in publish mode. Every Dapr annotation and every Component, Configuration, Resiliency and Subscription resource would therefore have to be modelled by hand in C#. The generated chart also maps connection strings to Kubernetes Secrets, which the Secrets convention limits to bootstrap exceptions, and the memlog never records the missing Dapr export.

**Failure scenario.** The team builds the primary path, pods deploy without daprd, and a large custom annotation and CRD layer grows on a preview API before the AD-1 fallback decision arrives, after the cost is sunk.

**Recommendation.** Record the preview status and the missing Dapr publish support in the memlog. State in AD-1 that Dapr annotations, CRDs and secret-reference handling are known required customizations, and whether they count toward the duplicate-topology fallback trigger. Then either pin the preview package in the profile inventory or bring the fallback decision forward.

## Medium and low clusters

| ID | Severity | Title | Targets | Disposition |
| --- | --- | --- | --- | --- |
| V-39 | medium | Site-surviving freshness monitor and GitHub delivery have no host or heartbeat | Release & Recovery: Recovery freshness; Conventions: Diagnostics and notification; Deferred: Release attempt state, checks and notifications | autofix |
| V-40 | medium | Existing AppHosts, hosting projects and non-MVP consumer lanes unclassified during coexistence | AD-1; Conventions: Migration; Design Paradigm; Structural Seed; Source Precedence; Deferred: Source/package adoption and declarations | discuss |
| V-41 | medium | Source/package mode selector and Builds CI tier unreconciled with AD-4/AD-5 | AD-4; AD-5; Deferred: Source/package adoption and declarations; Deferred: Enrollment schema/validator, pinned runner and resource ownership records | autofix |
| V-42 | medium | Release records and artifacts not retained off-site for the recovery window | AD-2; AD-12; Release & Recovery: Deployment ownership; Release & Recovery: Backup coverage and cadence; Conventions: Diagnostics and notification | autofix |
| V-43 | medium | Smoke and E2E execution identity and synthetic tenant are unowned | Release & Recovery: Before production update; Release & Recovery: Disaster recovery evidence; AD-6 | autofix |
| V-44 | medium | Critical-flow and smoke suites have no artifact identity or result contract | AD-2; Release & Recovery: Staging gate; Release & Recovery: Automatic recovery | autofix |
| V-45 | medium | Root EventStore.Aspire pin is stale and bypasses AD-4 source mode | Stack; AD-4; AD-5; Structural Seed; Deferred: Source/package adoption and declarations | autofix |
| V-46 | medium | Stack evidence overstated and rests on uncommitted, untracked files | Stack; Structural Seed | autofix |
| V-47 | medium | Prerelease hosting dependencies and Dapr patch skew are unrecorded | Stack; AD-1; AD-5; AD-10; Conventions: Production profile authority | autofix |
| V-48 | medium | Status of module and FrontComposer MCP hosts beside McpCli undecided | AD-11; Design Paradigm; Deferred: Hosted HTTP MCP, generic mocks and traffic-metric rollback triggers | discuss |
| V-49 | medium | Module interface names versus environment FQDN mapping is undefined | Conventions: Hosted interfaces; Conventions: Module declaration authority | autofix |
| V-50 | medium | Projects and Parties persist domain records through raw Dapr state | AD-9; Conventions: Domain truth and delivery; AD-12 | discuss |
| V-51 | medium | Shared health, telemetry and Aspire Dapr helpers have three competing owners | Conventions: Diagnostics and notification; Deferred: new row Shared ServiceDefaults and Aspire Dapr helper ownership | defer |
| V-52 | medium | Static secret contract conflicts with Memories runtime per-tenant credentials | Conventions: Secrets; Deferred: Memories recovery continuity and implementation gaps | discuss |
| V-53 | medium | Only the enrollment schema has a first-shared-version ordering rule | Deferred: Enrollment schema/validator, pinned runner and resource ownership records; Conventions: Module declaration authority | autofix |
| V-54 | medium | Dapr reaches OpenBao only via token-auth Vault component | Conventions: Secrets; AD-9 | autofix |
| V-55 | medium | Reused shared infrastructure includes EOL and unpatched components | AD-8; AD-3; Deferred: Shared runtime/profile evidence; Deferred: Secrets, identity, network and transport setup | discuss (settled) |
| V-56 | medium | Promotion artifacts and evidence lack provenance and authenticity checks | AD-2; Release & Recovery: Deployment ownership | autofix |
| V-57 | medium | Diagnostics and notifications flow through a public GitHub channel | Conventions: Diagnostics and notification; Release & Recovery: Automatic recovery | autofix |
| V-58 | medium | External-effect provider tenancy is not separated per environment | AD-8; AD-5; Release & Recovery: External effects | autofix |
| V-59 | medium | Intra-environment data-service sharing does not require per-module principals | AD-8 | autofix |
| V-60 | medium | Handling of a failed first deployment or first module enrollment unspecified | Release & Recovery: Automatic recovery; AD-3 | autofix |
| V-61 | low | Diagram, Aspire CLI pin and Source Precedence prose polish | Design Paradigm; Structural Seed; Stack; Source Precedence | autofix |
| V-62 | low | OCI chart digest and Helm major version imprecise | AD-2; Stack | autofix |
| V-63 | low | Local Dapr mTLS and ACL convention exists in code but unratified | Conventions: Secrets; Deferred: Secrets, identity, network and transport setup | defer |
| V-64 | low | Platform deploy/dapr instances absent; McpCli nested Platform reference unaddressed | Conventions: Shared host and catalogs; Conventions: Secrets; AD-4; Deferred: Source/package adoption and declarations | autofix |
| V-65 | low | Explicit test attach could target hosted staging or production | AD-10 | autofix |
| V-66 | low | Retained local failures accumulate; outcome and override precedence undefined | AD-10; Conventions: Local tool and readiness | autofix |
| V-67 | low | Staging evidence never expires and E2E is outside the staging lock | Release & Recovery: Staging gate; Release & Recovery: Deployment ownership | autofix |

## AD heat

| Target | Count | Clusters |
| --- | --: | --- |
| AD-1 | 4 | V-16, V-38, V-40, V-47 |
| AD-2 | 12 | V-01, V-08, V-11, V-14, V-15, V-16, V-20, V-38, V-42, V-44, V-56, V-62 |
| AD-3 | 9 | V-03, V-09, V-13, V-14, V-20, V-21, V-26, V-55, V-60 |
| AD-4 | 4 | V-08, V-41, V-45, V-64 |
| AD-5 | 4 | V-41, V-45, V-47, V-58 |
| AD-6 | 5 | V-03, V-10, V-25, V-26, V-43 |
| AD-7 | 3 | V-02, V-29, V-31 |
| AD-8 | 11 | V-06, V-13, V-20, V-21, V-22, V-23, V-24, V-35, V-55, V-58, V-59 |
| AD-9 | 5 | V-06, V-07, V-22, V-50, V-54 |
| AD-10 | 5 | V-04, V-17, V-47, V-65, V-66 |
| AD-11 | 5 | V-06, V-10, V-18, V-27, V-48 |
| AD-12 | 15 | V-05, V-12, V-17, V-19, V-21, V-23, V-28, V-30, V-32, V-33, V-34, V-35, V-37, V-42, V-50 |
| Conventions | 34 | V-01, V-03, V-04, V-06, V-08, V-09, V-11, V-13, V-14, V-16, V-17, V-20, V-21, V-22, V-23, V-24, V-25, V-31, V-36, V-38, V-39, V-40, V-42, V-47, V-49, V-50, V-51, V-52, V-53, V-54, V-57, V-63, V-64, V-66 |
| Release & Recovery | 30 | V-02, V-07, V-09, V-11, V-12, V-13, V-14, V-15, V-19, V-20, V-21, V-23, V-28, V-29, V-30, V-31, V-32, V-33, V-34, V-35, V-37, V-39, V-42, V-43, V-44, V-56, V-57, V-58, V-60, V-67 |
| Source Precedence | 5 | V-05, V-07, V-37, V-40, V-61 |
| Stack | 7 | V-36, V-38, V-45, V-46, V-47, V-61, V-62 |
| Deferred | 29 | V-01, V-02, V-03, V-04, V-05, V-07, V-08, V-09, V-10, V-15, V-18, V-19, V-24, V-26, V-27, V-34, V-37, V-38, V-39, V-40, V-41, V-45, V-48, V-51, V-52, V-53, V-55, V-63, V-64 |

## Prior gate vs this validation

- **Prior gate.** The Finalize gate ran three configured lenses (rubric, reality and adversarial) at spine-consistency depth, after per-input reconciliation, and applied the clear fixes they found.
- **This validation.** It added security, operability, seams and brownfield lenses. It also re-ran the original three against module code, module spines and live package and GitHub sources. That exposed three kinds of gap the earlier lenses were not configured to probe: cross-owner artifact ownership, trust boundaries on the shared installation, and failure-path state machines.
- **No contradiction.** The findings mostly extend adopted ADs rather than contradict them. No cluster argues that an earlier Finalize fix was wrong, and only 7 clusters touch a settled decision.

## Suggested next step

Roll these results into a bmad-architecture Update: settle the discuss items first, then apply the 41 autofixes and 2 defer rows, and re-run all seven lenses.

Decide first, because these touch settled decisions (user authority required):

- V-05 Projects 99.9%, RPO-0 and durable-task envelope owned by neither side: Resolution needs a Folders-style override (user authority) or HA beyond the adopted AD-12 backup-restore design on the single designated installation.
- V-07 AD-9 accepts only FalkorDB; Memories direct Redis usage unresolved: The user amended AD-9 to accept only the Memories FalkorDB adapter and authorized no further exception (memlog L101-105).
- V-08 Module workspace has two Platform version authorities and no schema-skew rule: PRD FR-2's submodule requirement is an inherited, settled product constraint (memlog L13-14); making the tool the sole identity may change it.
- V-21 No outage detection, response bound or hosted operational envelope for RTO: The accepted PRD four-hour outage-to-verified-service RTO is an inherited constraint (memlog L13, L131, L142); scoping it to staffed hours would reinterpret it.
- V-37 Folders override leaves recovery dependency and non-Memories erasure resurrection unowned: The user settled the Folders operational override and said not to block Folders enrollment (memlog L153-154); any re-plan must respect that.
- V-38 Aspire-to-Helm primary path is preview-only and exports no Dapr sidecars: The user adopted AD-1 Option 1 (an Aspire model exporting Helm) with qualification and fallback criteria (memlog L29, L33); bringing the fallback forward changes that decision.
- V-55 Reused shared infrastructure includes EOL and unpatched components: Memlog L164 and the Stack section introduce no unasked provider upgrade and reuse existing Keycloak and infrastructure; mandating upgrades needs user agreement.

Decide next (owner or trade-off decisions, critical and high first):

- V-01 (critical) Shared EventStore host embedding module adapters has no builder or release identity
- V-03 (high) Keycloak client, audience, claim and admission contract has no owner
- V-10 (high) MCP surface and delegation restrictions rely on client-asserted identity
- V-14 (high) Rollback target catalog, key generations and authority are not composed
- V-15 (high) No handshake between EventStore publication authority and Platform release records
- V-18 (high) McpCli discovery endpoint, compatibility key and release order unowned
- V-19 (high) Key-bearing backups defeat crypto-shredding; restore lacks quarantine and admission
- V-20 (high) Data services and persistent objects have no version authority or rollback class
- V-23 (high) OpenBao is neither classified nor restorable per environment
- V-29 (high) Interrupted release attempts: timer anchoring, resumption and late rollback undefined
- V-30 (high) Promotion stop, its clearing and working-release re-baselining are undefined
- V-34 (high) Production entry gate is ambiguous; Memories mirror silently blocks every module
- V-36 (high) Profile ratification cadence conflicts with per-release catalog and digest changes
- V-40 (medium) Existing AppHosts, hosting projects and non-MVP consumer lanes unclassified during coexistence
- V-48 (medium) Status of module and FrontComposer MCP hosts beside McpCli undecided
- V-50 (medium) Projects and Parties persist domain records through raw Dapr state
- V-52 (medium) Static secret contract conflicts with Memories runtime per-tenant credentials
