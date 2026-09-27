# Review — Adversarial divergence attack (update 2026-09-27)

Verdict: **FAIL (blocks handoff)**. There is no critical hole, but 10 high-severity pairs remain. In each pair, units that obey every AD to the letter still build a promotion, rollback or authorization path that cannot pass readiness or that has two writers. Most of these pairs come from new material: the template/binding split, AD-15, AD-13 and AD-14. Every finding can be closed by tightening an existing AD or convention row. None of them reopens a decision the user adopted.

Scope: I read the full spine, the memlog from "Update run started" onward, and `reviews/validate-2026-09-27/findings.json`. I also read the cited sections of the EventStore `architecture.md` (AD-11, AD-24–AD-29, AD-33), the Memories spine (AD-16), the Projects spine (AD-3, AD-8, AD-13, AD-20, AD-30) and the McpCli spine (executor, settings, profiles). Code evidence comes from `Hexalith.Agents.EventStore`, `Hexalith.Folders.EventStore`, `Hexalith.Memories.EventStore` and the EventStore `IdempotencyIntentAdapterRegistry`. Spine line numbers refer to the 309-line spine dated 2026-09-27.

Counts: critical 0 · high 10 · medium 17 · low 3 (30 findings).

A common fix covers ADV-U1, U3, U4 and U6. **Add one binding-class table** to the Production profile row. The table assigns every rendered input to exactly one of three classes:

- **Release-invariant:** bound in the release record and identical in staging and production.
- **Environment-current:** taken from the target environment at deploy and rollback.
- **Attempt-bound:** recorded per environment attempt.

Most of the high findings are this missing table seen from different sides.

## Findings

### ADV-U1 — One release record is promoted unchanged, but it binds values that necessarily differ per environment
- **Severity:** high
- **Where:** AD-2 (L60: the record binds the "catalog generation … configuration"), the Production profile row (L149: per-release bindings include "catalog generation, secret-contract digest"), AD-13 (L126), the Secrets row (L148: "environment dimension"), AD-8 (L96: per-environment OpenBao)
- **Unit A:** The release workflow writes the release record while the release is being staged. The record binds staging's catalog generation, configuration and secret-contract digest, as AD-2 and L149 require.
- **Unit B:** The production executor promotes "the same package … with separately versioned environment configuration" and checks that production serves what the record binds.
- **Divergence:** EventStore AD-25 places "active and reader digest-key generations, OpenBao logical map" inside every idempotency catalog entry. Key generations rotate independently in each environment's OpenBao, and AD-15's forward rollback generations exist only in production. The catalog root digest and generation therefore always differ between the two environments. The secret-contract digest also changes with every rotation in either environment. Unit B then either fails every promotion on a digest mismatch, or it ignores the binding, and the staged evidence no longer describes what production runs.
- **Evidence:** EventStore `architecture.md` L279 (entries bind key generations) and L287 (the profile binds "route/idempotency catalog digests"); spine L60, L149.
- **Minimal fix:** The release record binds only release-invariant identities: package, images, composed host, the catalog *route-content* digest (routes plus adapter descriptors, without key generations), check suites, realm-contract version and template digest. Each environment's attempt record binds its catalog generation and root digest, secret-contract digest, configuration version and environment-layer pins. Promotion compares route-content and template digests; host readiness compares the attempt-record digests.
- **Disposition:** autofix

### ADV-U2 — AD-15's forward rollback generation keeps entries that the N-1 composed host cannot serve
- **Severity:** high
- **Where:** AD-15 (L138), AD-13 (L126), Automatic recovery (L168); EventStore AD-25/AD-33
- **Unit A:** The release workflow prepares the AD-15 combination: the N-1 package, whose composed host contains the N-1 extension packages, plus a forward generation holding "every still-referenced idempotency … entry". That includes command C, which N introduced and which N's smoke or users exercised. It can also include an entry whose canonical descriptor N bumped (`DescriptorVersion` 2).
- **Unit B:** The N-1 composed host runs EventStore AD-25. Each entry "binds the trusted adapter … canonical descriptor schema and digest", and "Readiness fails on missing entries … unsupported generations". The registry resolves exactly one registered adapter per command type and throws when none is registered.
- **Divergence:** The rollback host has no adapter for C, or only descriptor v1 for the bumped entry. Readiness therefore fails, or equivalent retries compare different descriptors. The one allowed recovery is spent on a combination that cannot become ready. The forward generation is also encoded with N's catalog codec, and nothing requires N-1 to parse that encoding.
- **Evidence:** `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Commands/IdempotencyIntentAdapterRegistry.cs` (one adapter per command type, `DescriptorVersion`, "No trusted idempotency adapter is registered"); EventStore `architecture.md` L279, L331.
- **Minimal fix:** Add to AD-15: "A retained entry whose adapter or descriptor version is absent from the restored host is carried as a non-executable retention entry, which that host must tolerate. The forward generation is encoded at the restored release's catalog-codec version. A new adapter or descriptor change counts as 'additive' only when N-1 tolerates it; otherwise it is 'breaking'. The staging rehearsal exercises at least one idempotent command new in N." EventStore must confirm that the catalog schema supports retention-only entries.
- **Disposition:** discuss

### ADV-U3 — The rollback set does not say which rendered inputs revert, and Configurations in the package carry the current secret scopes
- **Severity:** high
- **Where:** AD-1 (L54: "Configurations, Subscriptions and Resiliency ship in the application package"), AD-15 (L138: "previous application package and configuration, current secret references"), the Secrets row (L148); EventStore AD-24
- **Unit A:** A rotation attempt follows the Secrets row and EventStore AD-24. `defaultAccess: deny` plus `allowedSecrets` "derive from" the secret contract, and they live in each app's Dapr `Configuration`. Generation 2 is published and acknowledged.
- **Unit B:** Automatic recovery follows AD-15 and AD-3 and runs a Helm upgrade to the N-1 package. That package re-renders N-1's Configurations, whose secret scopes and ACL caller lists were derived at N-1 time.
- **Divergence:** The N-1 pods cannot read the current generation, so "required generations gate readiness" fails and the recovery fails. The same fork applies to other inputs: the sidecar-image annotation (release-pinned) against the forward-only control plane, ACL callers added for app IDs N introduced, and Subscriptions for topics that remain live.
- **Evidence:** EventStore `architecture.md` L258 ("Component scopes, `defaultAccess: deny` plus `allowedSecrets` … derive from it"); spine L54, L138.
- **Minimal fix:** Apply the common binding-class table. Secret scopes, secret references, key generations, catalog generation, realm-contract version and environment-layer versions are environment-current, and the chart takes them as values. Workloads, images, resiliency and ACL shape are release-owned. Rollback renders the previous chart with the environment-current values, so the package is never regenerated.
- **Disposition:** autofix

### ADV-U4 — After a rollback, the working baseline matches no release record, and AD-15's "N-1" is not production's baseline
- **Severity:** high
- **Where:** AD-15 (L138: "Staging rehearses N to N-1"), Before production update (L165: "production's recorded working release"), Automatic recovery (L168), the Staging gate (L162: "A staging failure blocks only that candidate")
- **Unit A:** Automatic recovery commits the forward generation G_rb, restores N-1 with current secret references and records N-1 as working.
- **Unit B:** The next attempt, and the staging executor's rehearsal, build "previous package and configuration" from N-1's *release record*, which binds G_{N-1}. The staging executor also rehearses against *staging's* previous release, which may be an unpromoted candidate rather than production's baseline.
- **Divergence:** The rehearsal passes on a combination production is not running: it lacks G_rb's retained entries and uses the old secret references. Alternatively it tests compatibility against a staging-only release while staging data already contains state written by unpromoted candidates. The "wrong-baseline evidence" rule has no defined baseline identity to compare against.
- **Minimal fix:** Define the working baseline as a combination record: the release record plus the environment attempt record (catalog generation, secret-contract digest, configuration version, profile digest). "N-1" in AD-15 means production's working baseline. The serialized staging attempt deploys that baseline release before the candidate, and the evidence binds the baseline combination identity.
- **Disposition:** autofix

### ADV-U5 — The profile digest has no defined preimage, and an environment-layer change silently invalidates every rollback target
- **Severity:** high
- **Where:** The Production profile row (L149: template versus bindings versus inventory; "promotion requires equal profile digests"), AD-3 (L66), Rollout (L166: "bound to the active profile digest"), Infrastructure currency (L303); memlog V-15 ("valid previous working target only while its record is valid for the active profile digest")
- **Unit A:** The environment-layer owner applies a forward-only change, first in staging and then in production, under AD-3 and Infrastructure currency. Examples are an OpenBao security patch, Redis 8 or a PostgreSQL minor.
- **Unit B:** The release workflow requires equal staging and production profile digests and a rollback target whose production-promoted record matches the active digest.
- **Divergence:** The outcome depends on whether the digest covers the environment layer:
  - **If the digest covers the environment layer:** the staging-first change blocks every promotion until production matches. After production changes, the working release's record no longer matches, so no valid rollback target exists, AD-15 cannot be prepared, and automatic promotion stops with no renewal path.
  - **If it does not cover the environment layer:** staging and production can run different databases, brokers or control planes under "equal" digests, which contradicts EventStore AD-26 (the profile binds "PostgreSQL and broker components … exact DAPR runtime image").
- **Minimal fix:** Define the profile digest as the template plus the environment-layer facet pins, with per-environment values and per-release bindings excluded. An environment-layer change is its own attempt, staging first. The production attempt ends by re-verifying the current working release (readiness plus a passing smoke suite) and issuing a renewed production-promoted record for it at the new digest. Promotion stays stopped until that record exists.
- **Disposition:** discuss (EventStore AD-11/AD-26 record semantics)

### ADV-U6 — Memories.Aspire data-service digests and the Dapr runtime each have more than one version authority
- **Severity:** high
- **Where:** The Production profile row (L149: "module and Memories.Aspire image digests — live in the release record"; "One Dapr runtime patch per release record"; template "runtime"; inventory "Dapr control plane"), AD-3 (L66: "exactly one version authority each: a profile facet"), the Design Paradigm (L26)
- **Unit A:** The release workflow binds Memories.Aspire digests (Redis and FalkorDB) and the Dapr sidecar patch in each release record, and rollback redeploys N-1's values.
- **Unit B:** The environment-layer owner upgrades Redis, FalkorDB and the shared control plane forward-only through the profile facet. EventStore AD-26 treats the Dapr runtime image as part of the ratified profile.
- **Divergence:** Two writers control the same data-service image, so a rollback either downgrades on-disk formats or the release record misstates what runs. A Dapr patch bump either needs EventStore ratification for every patch or bypasses it. Rollback can also pair an N-1 sidecar with a newer control plane.
- **Minimal fix:** The release record carries Memories.Aspire and Dapr digests only as the *qualified set*. The deployed data-service and control-plane versions belong to the environment-layer facet alone. A release is deployable, and a valid rollback target, only when the current facet versions are in its qualified set; otherwise the change is classified as breaking. The template fixes the Dapr minor; the per-release sidecar patch must fall within the control plane's supported skew.
- **Disposition:** discuss (the Dapr ratification granularity involves EventStore)

### ADV-U7 — The EventStore lifecycle subject for the composed image is undefined
- **Severity:** high
- **Where:** AD-13 (L126: "Platform release workflow issues the composed image's identity"; "EventStore ratifies the composition rule once"), the Staging gate (L162: "EventStore evidence-validated candidates"), Before production update (L165: "EventStore release-available"), the EventStore input row (L185)
- **Unit A:** Platform reads "EventStore release-available" as the state of the server *package* and deploys the composed image, whose digest has no lifecycle record.
- **Unit B:** The Builds-owned EventStore validator applies AD-11/AD-26. "Any additional image first receives an explicit release identity and the same validation contract." `release-available` needs a release-owner record for the *exact subject*. "Any subject change restarts at `built`", and production-promoted binds "the same immutable subject's" predecessor records.
- **Divergence:** Unit B rejects every production-promoted record for the composed digest. The alternative is to run the full EventStore evidence and release-owner chain for every composed image, which makes EventStore a per-release signer and contradicts "ratifies … once, not per release".
- **Evidence:** EventStore `architecture.md` L168, L170, L287.
- **Minimal fix:** Name the two subjects. The server package carries EventStore-issued `evidence-validated` and `release-available`. The composed image is a Platform subject, built by the SHA-pinned Builds publisher as a multi-arch index. Its release-available and production-promoted records are Platform-issued and bind the input server-package subject (which must be release-available) and the extension digests. EventStore's one-time ratification registers Platform in the role registry for composed subjects.
- **Disposition:** discuss

### ADV-U8 — AD-14 leaves no compliant way for a cross-module server call to carry the original actor
- **Severity:** high
- **Where:** AD-14 (L132: "actor is the token subject; the workload is the authenticated client; the MVP has no separate delegation issuer"), the Projects input row (L190: "actor plus workload authorization satisfied by AD-14"); EventStore AD-10/AD-28
- **Unit A:** Projects follows its own AD-3, AD-8 and AD-20. Project creation is a durable task that provisions a Folder through Folders. Projects' "workload identity holds no Folder authorization". The context must carry "original actor, authenticated caller/workload service, delegation identifier/scopes/audience". AD-20 prevents "raw-token forwarding" and "service-only authority", and `task.reconcile` re-evaluates the original actor later.
- **Unit B:** The gateway follows AD-14 and EventStore AD-28, under which the caller app ID is attribution only.
- **Divergence:** Every available path breaks a rule:
  - **Projects calls with its own client token:** the actor becomes the Projects service account, so Folders denies the call, or someone grants the service account blanket authority, which Projects AD-30 forbids.
  - **Projects forwards the user's token:** this is forbidden forwarding. The surface becomes the originating client (for example `mcpcli`), so agent restrictions apply to internal steps, and the call fails once a durable task outlives the token.
  - **Projects calls through Dapr invocation:** that path carries no actor authority.

  The Projects row's claim is therefore false.
- **Evidence:** Projects spine L103, L155, L242, L302.
- **Minimal fix:** Add a server-to-server clause to AD-14. The workload is the calling module's client or sidecar-attested app ID. For synchronous steps, the actor comes from Keycloak standard token exchange of the incoming user token, so Keycloak remains the only issuer. Asynchronous task steps use the original actor attested at EventStore admission (EventStore AD-29 bounded form), and owners authorize them only for task-bound operations. The other option is to declare Projects' asynchronous Folder steps out of MVP scope.
- **Disposition:** discuss

### ADV-U9 — The surface class of synthetic E2E and smoke clients is undefined
- **Severity:** high
- **Where:** AD-14 (L132), Synthetic identities (L152: "enrolled synthetic actor and workload identities"), the Staging gate (L162: complete flow-to-test mappings)
- **Unit A:** A module's critical-flow suite must cover every declared flow, including UI-only or confirmation-required operations. It authenticates with its synthetic workload client, which counts as the "workload is the authenticated client".
- **Unit B:** The gateway derives the surface from `azp` and refuses operations for which that surface is not eligible.
- **Divergence:** The synthetic client is one of two things:
  - **Its own surface:** UI-only flows are refused, the mappings cannot be completed, and promotion is blocked permanently.
  - **Granted several surfaces:** this breaks "a distinct client per surface", and a leaked synthetic credential can then execute UI-only operations headlessly in production.

  Nobody owns the `azp`-to-surface map either. The EventStore gateway could hardcode client IDs, while the Platform realm-contract instance generates them.
- **Minimal fix:** The realm contract maps each client ID to exactly one surface class, and the gateway reads that map from the contract instance. Each suite declares the surface it exercises and uses that environment's synthetic client of that class, flagged as synthetic in the token. Flows that need human confirmation are tested only through their UI surface.
- **Disposition:** autofix

### ADV-U10 — Per-job synthetic credentials in production require realm administration that AD-6 forbids to automation
- **Severity:** medium
- **Where:** Synthetic identities (L152: "per-job credentials"), AD-6 (L84: "no automation, runner or fixture holds master or cross-realm admin … production realm changes are Administrator-only")
- **Unit A:** The production executor needs a new credential for each smoke job.
- **Unit B:** The production realm allows only the Administrator to make changes.
- **Divergence:** Issuing or resetting a user or client credential is a realm change. The executor either receives a production management client, which violates AD-6, or uses long-lived secrets, which violates "per-job".
- **Minimal fix:** "Per-job" means per-job tokens. The Administrator provisions production synthetic client credentials, which are held only in the production protected environment and rotated by an Administrator attempt. Each job mints short-lived tokens from them.
- **Disposition:** autofix

### ADV-U11 — McpCli's build identity is not tied to the composition it is tested against
- **Severity:** medium
- **Where:** AD-11 (L114: "Release order: EventStore.Contracts, module Contracts, Platform release, McpCli"), the Staging gate (L162: "Every included module …"), the Design Paradigm (L26: McpCli is an MVP module), AD-4 (L72)
- **Unit A:** McpCli publishes after each Platform release, as the release order requires.
- **Unit B:** The staging gate requires every included module's critical flows to pass for the exact release, and AD-4 source mode applies to local work.
- **Divergence:** Staging E2E for Platform N must run McpCli N-1, whose static Contracts lack N's schema digests, so every new or changed operation is non-executable. FR-12 for N is never E2E-proven, and McpCli N ships without passing a staging gate. Locally, a packaged McpCli cannot execute any operation changed in the source-mode active checkout, which blocks the SM-1 and SM-2 demonstrations.
- **Minimal fix:** A McpCli candidate built from the release's intake Contracts is bound in the release record as a client artifact that is not deployed. It runs the staging flows and is published only after staging validation. In local mode, the Platform tool builds and launches McpCli from the AD-4 mapping.
- **Disposition:** discuss

### ADV-U12 — McpCli fills payload actor fields from user settings, but AD-14 says the actor is the token subject
- **Severity:** medium
- **Where:** AD-14 (L132), the McpCli input row (L187)
- **Unit A:** McpCli follows its own executor rule. "An Operation naming `actorProperty` requires `EnvelopeContext.Actor`", which resolves from `--actor`, `EVENTSTORE_ACTOR` or the profile, and the executor overwrites the payload's actor property with that value.
- **Unit B:** The gateway authorizes the token subject, while module handlers and audits read the Contracts-declared actor property.
- **Divergence:** A hosted user sets `--actor` to someone else, and attribution in domain events or audits is spoofed. If the gateway instead rejects mismatches, the Keycloak `sub` UUID versus configured-name format breaks every operation that carries an actor.
- **Evidence:** McpCli spine L110, L134.
- **Minimal fix:** In hosted environments, the gateway overwrites the declared actor property with the token subject, or rejects any difference. Hosted McpCli profiles derive the actor from the token and refuse `--actor`.
- **Disposition:** autofix

### ADV-U13 — AD-13 "built from the released EventStore server package" contradicts AD-4 source mode
- **Severity:** medium
- **Where:** AD-13 (L126), AD-4 (L72: root-declared dependencies build from source; "a source and a package copy of one identity fail the build")
- **Unit A:** A local workspace root-declares EventStore, for example the EventStore or Folders workspace. The Folders extension then references `Hexalith.EventStore.Gateway` as source (`HexalithEventStoreFromSource`).
- **Unit B:** The composed-host project takes the "released server package" literally.
- **Divergence:** A duplicate identity fails the build. Alternatively, EventStore developers cannot debug the server inside the composed host.
- **Evidence:** `references/Hexalith.Folders/src/Hexalith.Folders.EventStore/Hexalith.Folders.EventStore.csproj` (conditional project or package reference).
- **Minimal fix:** "The composed host is built through the AD-4 mapping: released packages in CI, staging and production, and root-declared source locally. The composed-host project is Platform-owned and mode-agnostic."
- **Disposition:** autofix

### ADV-U14 — A single committed catalog instance cannot serve compositions that contain different sets of modules
- **Severity:** high
- **Where:** The Catalogs row (L146: "Platform owns the content-bound `deploy/dapr/eventstore-routing-catalog.json` instance … No second hand-maintained operation catalog"), the Design Paradigm (L26: minimum composition), AD-10 (L108), AD-11 (discovery from the committed catalog)
- **Unit A:** A Parties developer runs the minimum composition (EventStore, Tenants, Memories, Parties), or CI runs a compatible batch.
- **Unit B:** EventStore AD-33 and AD-25 load the one committed instance, which contains Folders and Projects routes and idempotency entries.
- **Divergence:** Required hosts and adapters are absent, so readiness fails (missing, unbound entries). Discovery also advertises operations of modules that are not enabled, which FR-12 forbids. A per-composition hand edit would be the forbidden second catalog.
- **Minimal fix:** One deterministic Platform generator produces the catalog for each composition from the enrolled declarations. The committed path holds the hosted release instance. Local and CI instances are run-scoped and identified in the environment descriptor's composition identity.
- **Disposition:** autofix

### ADV-U15 — The extension-package contract is underspecified: runtime needs, configuration and API version
- **Severity:** medium
- **Where:** AD-13 (L126), Module declaration (L144: logical secrets and Dapr roles per module app), AD-8 (L96: per-module least-privilege principals), AD-9 (L102)
- **Unit A:** Module teams write extensions:
  - **Agents:** `AddAgentsEventStore(services, agentsAppId)` needs a trusted caller app ID as input.
  - **Memories:** a package named in the `*.EventStore` style references `StackExchange.Redis` and `Dapr.Workflow`.
  - **Folders:** its extension targets extension API v1.
- **Unit B:** Platform composes all extensions into one process under the `eventstore` app ID and principal, against whichever extension API version the server release ships.
- **Divergence:** Any extension that needs a secret, state or a provider SDK runs under eventstore's grants. That breaks per-module least privilege and moves the AD-9 SET NX exception out of the Memories adapter boundary. No declaration field supplies extension configuration such as the trusted Agents app ID. An extension built against v1 on a v2 server has no acceptance rule.
- **Evidence:** `references/Hexalith.Agents/src/Hexalith.Agents.EventStore/AgentsEventStoreServiceCollectionExtensions.cs`; `references/Hexalith.Memories/src/Hexalith.Memories.EventStore/Hexalith.Memories.EventStore.csproj`.
- **Minimal fix:** Extension packages implement only the extension API. They reference no provider SDK or Dapr client and declare no secrets or Dapr roles. Their configuration inputs are declaration fields that Platform binds. The server declares the extension-API majors it supports (current and previous), and the composed-host build fails naming the module that falls outside them.
- **Disposition:** autofix

### ADV-U16 — The attempt fixes no point for catalog activation, and "no workload changed" can leave N's generation live
- **Severity:** medium
- **Where:** Rollout (L166), Automatic recovery (L168: "If no workload changed, retain and report"), AD-3 (L66: changed means any release-owned rendered object digest differs), AD-15; EventStore AD-33 (prepare, ready, commit)
- **Unit A:** The release workflow commits N's generation early. The Helm upgrade then fails before any rendered object changes, so the attempt "retains" N-1.
- **Unit B:** Another implementer commits only after readiness, while EventStore readiness rejects a partial or uncommitted generation.
- **Divergence:** In the first case, N-1 workloads run against N's committed routes to methods that do not exist, and the report says nothing changed. In the second, readiness deadlocks.
- **Minimal fix:** Fix the order:
  1. Prepare N's generation and the rollback generation.
  2. Ready-validate the rollback generation on the running N-1 hosts.
  3. Run the Helm upgrade; N hosts validate N's prepared generation.
  4. Commit at readiness.
  5. Verify.

  A committed generation counts as a change, and recovery commits the rollback generation.
- **Disposition:** autofix (confirm with EventStore)

### ADV-U17 — An interrupted attempt cannot both finish in "one job" and use its single recovery, and the lock has no fencing
- **Severity:** medium
- **Where:** Interruption (L164: "fails the attempt, which uses its single recovery"), Attempt ownership (L163: "runs from lock to terminal outcome in one job"; the watchdog only notifies), the DR sequence (L173; the lock covers DR)
- **Unit A:** The executor job dies before the verification deadline plus grace.
- **Unit B:** The one-job rule and the rule to stop on an unknown lock owner both apply.
- **Divergence:** Only a second job can perform the required recovery, which the one-job rule forbids, so otherwise no one performs it. DR cannot lawfully take a lock held by a dead job. A zombie job on a surviving executor can still mutate after an Administrator or DR takeover.
- **Minimal fix:** The lock carries a monotonic epoch that is checked before every mutation. Only two parties may take it over: an Administrator record, or a replacement job of the same workflow that resumes the same attempt and performs only its remaining single recovery within the grace. DR entry always takes a new epoch.
- **Disposition:** autofix

### ADV-U18 — Shared-cluster and environment-layer changes have no executor identity and no atomic lock rule
- **Severity:** medium
- **Where:** Attempt ownership (L163: "change only while no attempt is open in either environment"), AD-7 (L90: namespace-scoped deploy identities; the production executor runs only production jobs), AD-8 (L96: control plane "without staging write access")
- **Unit A:** The shared-service change owner upgrades the Dapr control plane, CRDs or cert-manager, which needs cluster-scoped rights.
- **Unit B:** AD-7 gives the executors only namespace-scoped identities and keeps staging away from production.
- **Divergence:** The change is made from an Administrator workstation that also runs module development, which breaks AD-7. The check-then-act rule also races: staging opens an attempt just after the check, and a staging deploy runs during the control-plane upgrade.
- **Minimal fix:** Shared and cluster-scoped changes run as a named workflow on the production executor with a separate per-job cluster-scoped identity. They hold both environment locks, acquired staging first, for the whole change. Namespaced environment-layer changes run from that environment's executor under its lock.
- **Disposition:** autofix

### ADV-U19 — The environment layer has no deployment definition, while AD-1 recognizes only one model
- **Severity:** medium
- **Where:** AD-1 (L54: "one Aspire application model"; the package is "the only deployment definition"; fallback on a "duplicate full topology"), AD-3 (L66)
- **Unit A:** The Aspire model contains Redis, PostgreSQL and FalkorDB because local runs need them. The Kubernetes publisher emits them into the application chart, where they roll back with the application.
- **Unit B:** AD-3 places them outside the package.
- **Divergence:** Data services either ship and roll back in the application chart, or a second, unowned, hand-maintained definition appears, which AD-1's fallback trigger can read as a duplicate topology.
- **Minimal fix:** In hosted publish, data-service resources become external connection parameters. The environment layer is a separate, versioned, per-environment definition owned by Platform and generated from the profile facets. It is explicitly not a duplicate topology.
- **Disposition:** autofix

### ADV-U20 — G1 "ingress closed" blocks production verification and the off-site probe
- **Severity:** medium
- **Where:** Production entry gates (L169: G1), Verification (L167), Detection and response (L172: the probe "checks production ingress"), AD-7 (the production executor sits outside the cluster), Hosted interfaces (L151: "No direct-pod bypass")
- **Unit A:** G1 deploys with ingress closed.
- **Unit B:** Smoke checks from the off-cluster executor, and the off-site probe, reach the gateway through the production hostnames.
- **Divergence:** The first deployment fails verification and "stops". The executor could instead bypass ingress, which conflicts with the direct-pod rule. The probe either floods notifications or is disabled, and then the G2 drill cannot "measure real detection".
- **Minimal fix:** Before G2, "ingress closed" means that production hostnames admit only the executor's and the probe's sources and the production admission group is empty. The probe runs from G1 onward, with notifications labelled pre-G2.
- **Disposition:** autofix

### ADV-U21 — Production changes before G3, and "separately planned" releases, have no defined path
- **Severity:** medium
- **Where:** G3 (L169), Before production update (L165: evidence problems route "to the separately planned procedure"), AD-3 (L66), AD-7 (named workflows only); PRD SM-5 and SM-C1
- **Unit A:** One team treats a manual release as an Administrator Helm upgrade, with no record, lock or evidence.
- **Unit B:** Another team routes manual releases through the release workflow, which the spine defines only as the automatic path that G3 gates.
- **Divergence:** Between G1 and G3, every release takes one of these incompatible paths. SM-5 also needs deliberately failing deployments in production, which cannot pass the staging gate.
- **Minimal fix:** Use one production release workflow with two modes, automatic and Administrator-approved. The approved mode replaces only the G3 and automatic-compatibility trigger with an Administrator record; the staging gate, lock, provenance, verification, rollback and records all still apply. SM-5 fault rehearsals use Platform-owned fault-injection configuration on a staged release before G2.
- **Disposition:** discuss

### ADV-U22 — A literal drill would mutate live shared authority, or skip the steps G2 depends on
- **Severity:** medium
- **Where:** DR evidence (L174: an isolated drill before G2, monthly), AD-12 (L120: "drill restores are ephemeral and egress-denied"), DR sequence steps 1, 4 and 5 (L173)
- **Unit A:** The drill runner executes the sequence as written: revoke the old environment's credentials, rotate, re-apply Keycloak revocations, and verify production users and staging denial.
- **Unit B:** Production is still live, Keycloak is the shared existing server, and the drill cannot reach it because egress is denied.
- **Divergence:** Following the sequence either causes a production outage or cannot run. Skipping those steps still produces a "passing" drill that G2 relies on.
- **Minimal fix:** Define drill substitutions. The fence is proven against drill-scoped copies. Keycloak is restored into the drill, or a drill realm is generated from the realm contract with the admin-event export applied. The drill report lists each substitution, and real fence steps are rehearsed on staging before G2.
- **Disposition:** autofix

### ADV-U23 — Restoring the issuers rewinds the step-1 fence
- **Severity:** medium
- **Where:** The DR sequence (L173): step 1 revokes and proves old credentials fail; steps 2–3 restore the key store and data; step 4 rotates
- **Unit A:** The fence revokes credentials at the live issuers.
- **Unit B:** Steps 2–3 restore OpenBao and the databases from backups taken before the revocation.
- **Divergence:** Until step 4, the restored issuers accept the old credentials again. A surviving old workload, after a storage-only failure or a compromise, can write into the restored environment during quarantine or replay.
- **Minimal fix:** Restored issuers and data services stay network-reachable only from the recovery executor until the step-4 rotation completes. The fence proof is repeated against the restored instances before step 5.
- **Disposition:** autofix

### ADV-U24 — The recovery executor is unassigned and collides with AD-7
- **Severity:** medium
- **Where:** AD-7 (L90: "Executors do not establish site recovery"; production credentials never where test code runs), AD-12 (L120: "independently usable … access")
- **Unit A:** DR onto prepared capacity runs from the Administrator's workstation, which also runs local module development.
- **Unit B:** Alternatively it runs from the production executor, which may be lost and whose namespace-scoped credentials target the old API and were revoked in step 1.
- **Divergence:** The first option breaks AD-7. The second has no reachable, valid credentials for the replacement cluster.
- **Minimal fix:** Name a recovery executor class. It is off-site, runs only recovery workflows from the private operations repository, and uses pre-provisioned credentials for the prepared capacity held in off-site custody. It never runs staging or test work, and drills use it.
- **Disposition:** autofix

### ADV-U25 — The tenant-key store is unnamed, and OpenBao is backed up as an ordinary unit
- **Severity:** medium
- **Where:** Backup coverage (L170: "each environment's OpenBao" as an inventory item; "different classes never share a backup unit"), AD-12 (L120: key custody is a separate class, and erasure makes earlier key backups unusable), DR step 2 ("Restore the key store"); EventStore AD-23/AD-24
- **Unit A:** The backup owner snapshots each environment's OpenBao every 30 minutes, immutable and kept for 30 days.
- **Unit B:** EventStore or Memories key custody stores the tenant keys or KEKs in that same OpenBao, which is where "key-generation checks gate readiness".
- **Divergence:** The snapshots become key backups held beside the ciphertext backups, and they cannot be made unusable for a single erased tenant. If the keys live elsewhere instead, step 2 restores a store the spine never names.
- **Minimal fix:** Name the tenant-key store as a separate instance or mount excluded from ordinary OpenBao snapshots. State that the environment OpenBao backup unit contains no tenant-key material.
- **Disposition:** autofix

### ADV-U26 — Modules classify changes against their predecessor, but promotion needs the classification against production's baseline
- **Severity:** medium
- **Where:** Module declaration (L144: "change classification (none, additive, breaking)"), Before production update (L165)
- **Unit A:** A module team classifies release N against its own N-1.
- **Unit B:** Production runs N-3, because N-2 was breaking and was never promoted.
- **Divergence:** The candidate is labelled "additive" while the cumulative change is breaking. The rehearsal may catch it, but the stated classification evidence is wrong.
- **Minimal fix:** The effective classification is the maximum over every module release between the baseline and the candidate. The intake manifest keeps that chain.
- **Disposition:** autofix

### ADV-U27 — An automatic attempt never checks that its realm-contract version has been applied
- **Severity:** medium
- **Where:** AD-6 (L84: the version is "applied forward-only before the attempt that needs it"; production realm changes are Administrator-only), Before production update (L165)
- **Unit A:** The release workflow starts an automatic attempt.
- **Unit B:** The Administrator has not yet applied the new clients and roles.
- **Divergence:** Verification fails, rollback follows and the promotion stop is set. The executor applying the contract itself would violate AD-6.
- **Minimal fix:** Add a pre-update check: the production realm must report the required contract version or a compatible later one, read through a realm-scoped read-only view. Otherwise the attempt stops without mutation and notifies.
- **Disposition:** autofix

### ADV-U28 — AD-10 and the Migration row disagree about EventStore's own AppHost tests
- **Severity:** low
- **Where:** AD-10 (L108: "EventStore.Testing(.Integration) … never start their own AppHost"), Migration and coexistence (L154: "technical-module AppHosts stay valid isolated-test roots")
- **Divergence:** EventStore's integration tests use the same fixture library that AD-10 forbids from starting an AppHost.
- **Minimal fix:** AD-10 governs multi-module integration environments. A technical module's own-repository tests may start its AppHost through a fixture path that is not exported to consumers.
- **Disposition:** autofix

### ADV-U29 — Recovery-phase module tasks have no lifecycle scope and no cross-module order
- **Severity:** low
- **Where:** Module declaration (L144: lifecycle scopes are per-start verify, once-per-environment creation and operator-only), DR step 3 (L173)
- **Divergence:** Teams declare admission and purge as operator-only, which makes them manual and unmeasured in the RTO, or as per-start verify, which runs them on every rollout. Projects can also rebuild its reverse index from Conversations before Conversations has been admitted and purged.
- **Minimal fix:** Add a "recovery" lifecycle scope that runs in declared-dependency order, completing each module's admission and purge before its dependents replay.
- **Disposition:** autofix

### ADV-U30 — Provenance for Platform-built artifacts names only the Builds workflow
- **Severity:** low
- **Where:** AD-2 (L60: "verifies every digest's provenance against the protected Builds workflow and ref"), AD-13 (the Platform release workflow builds the composed image), AD-7 (release workflows live in the private operations repository)
- **Divergence:** The deploy verifier rejects the composed image, the application package and the catalog, or accepts any caller.
- **Minimal fix:** For each artifact class, name the caller repository, workflow, protected ref and the pinned Builds reusable-workflow ref that must appear in its provenance.
- **Disposition:** autofix

## Withstood

These earlier divergence attacks are closed by the update, subject to the refinements above:

- **Who builds the shared host (prior ADV-1):** AD-13. Refined by U7, U13 and U15.
- **Dapr component naming and namespace (prior ADV-2):** the declaration row and AD-9.
- **Staging run on a cheaper profile (prior ADV-3):** the Production profile row. Refined by U5.
- **Unserialized rotations, catalog commits and DR (prior ADV-6):** Attempt ownership. Refined by U17 and U18.
- **Other closed attacks:** a restore of one environment's OpenBao rewinding the other (per-environment OpenBao); attaching tests to hosted environments; module MCP hosts beside McpCli; staging declaring `tache.ai`; a failed first enrollment deleting data; genesis tasks re-running on recovery; public workflow logs.

The Folders and Projects envelope overrides were not attacked, because they are user-adopted.
