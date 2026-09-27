# Review — Security & isolation
Verdict: FAIL — AD-7 and the AD-8/AD-9 composition permit two staging-to-production authority paths: a shared persistent deployment runner, and cross-namespace Dapr invocation authorized by bare app ID. Several shared components (OpenBao, ingress/cert-manager, Keycloak administration, backups) also have no isolation invariant. Every gap can be closed by tightening existing ADs or conventions; no adopted decision has to be reversed.

Reviewed: ARCHITECTURE-SPINE.md (final, 2026-09-27), .memlog.md, PRD FR-9–FR-12/NFR-3, EventStore AD-10/22/24/26/28, Memories AD-5/15/16/21, McpCli AD-13/14, Projects AD-20/29, Parties I7, Platform `DaprComponents/`, `apphost.cs` and the Builds `domain-release.yml`. I read files only. I ran no infrastructure or GitHub API commands.

## Trust boundaries as the spine defines them

| Component | Spine position | Evidence | Security status |
| --- | --- | --- | --- |
| Keycloak server (process, DB, master realm, hostname) | **Shared**; separate staging/production application realms | AD-6, AD-8, memlog 65 (one CNPG `keycloak-postgres`) | Realm split is decided. Admin-credential path, issuer hostname, admission record and admin custody are undecided (SEC-8, SEC-11) |
| Kubernetes cluster, API server, the single node, local storage | **Shared** | Structural Seed; memlog 129 | Deploy-credential scope, Pod Security and NetworkPolicy are unnamed (SEC-1, SEC-7) |
| Ingress controller, cert-manager/issuers, DNS wiring | **Shared** ("existing suitable") | Hosted interfaces row | Nothing binds host ownership to an environment (SEC-6) |
| Deployment runner | **Shared**: one "dedicated" runner, with "separate credentials" | AD-7 | Execution isolation, workflow trust and credential scope are unbound (SEC-1) |
| Dapr control plane (Sentry root CA, placement, scheduler state) | **Undecided**: one observed 1.18.1 control plane | memlog 65; AD-8 is silent | Trust domain, app-ID and ACL scoping by environment are unbound (SEC-2) |
| OpenBao | **Undecided**: one observed 2.6.2 instance, with a daily raft snapshot | memlog 65, 129, 141 | Not classified as either application state or supporting infrastructure (SEC-3) |
| Backup store and backup/decryption custody | **Undecided**: one observed off-site Velero bucket | memlog 66, 129; Backup coverage row | Erasure custody and immutability are unbound (SEC-4, SEC-5) |
| GitHub org, repos, Actions logs and artifacts, notifications | **Shared**, and inferred public (MIT, nuget.org publication) | Diagnostics row; Builds `domain-release.yml:486-493` | Visibility of the evidence channel is unbound (SEC-13) |
| Namespaces, data-service instances (DB, broker, Redis/FalkorDB), volumes, app credentials, Dapr components, realms/clients/service accounts, hostnames | **Per environment** | AD-6, AD-8, Hosted interfaces | Decided. Sharing a data service between modules inside one environment needs a principal rule (SEC-16) |
| External-effect targets (GitHub/Forgejo orgs, in-cluster Forgejo, LLM/embedding accounts) | **Undecided** | Not in the spine | SEC-15 |
| McpCli tokens on user and agent hosts | Deferred ("token provisioning/renewal") | AD-11; Deferred table | Environment binding and surface binding are unbound (SEC-9, SEC-10) |

## Findings

### SEC-1 — A persistent deployment runner runs staging test code and holds production deployment authority
- Severity: critical
- Where: AD-7; Structural Seed (Runner → Stage → Evidence → Runner → Prod); Staging gate and Before-production-update rows.
- Finding: AD-7 permits one persistent self-hosted runner. That runner executes the staging E2E suites, which are module-owned test code from seven repositories plus their NuGet dependency trees. It then runs production rollout and smoke checks with production credentials. "Separate staging/production credentials" can be met by two credentials on one host, or by two cluster-admin kubeconfigs. The spine does not require ephemeral or per-environment execution. It does not restrict which workflows, refs or repositories may target the runner, and does not require GitHub environment protection. It does not limit a credential's scope to its own namespace or keep cluster-scoped resources out of the application Helm package. It also allows the runner inside the application cluster ("prefer … outside").
- Evidence: spine line 88 (AD-7); memlog lines 57 and 61 speak only of "environment-specific scoped deployment credentials"; the observed read-only access used context `jpiquot@local` (memlog 129). The repositories are MIT-licensed and publish to nuget.org, so they are very likely public. GitHub advises against self-hosted runners on public repositories.
- Failure scenario: (a) A compromised NuGet dependency of a module's E2E project runs during the staging gate. It plants persistence on the runner, for example in `~/.kube`, a helm plugin, a cached tool binary or a shell profile. The next production job hands it production kube and smoke credentials. (b) A workflow in any repository that can use the runner group (a PR or fork job, or a new workflow file on a branch) targets the runner label and reads whatever is cached there. (c) The staging "namespace" credential can create cluster-scoped objects (ClusterRoleBinding, webhook, IngressClass) or pods in any namespace, so it is production authority in practice.
- Recommendation: Add to AD-7 a production-authority execution invariant:
  - Production credentials are short-lived and issued per job (OIDC federation, or GitHub environment secrets behind required-reviewer and protected-branch rules).
  - They are presented only on an ephemeral runner, or on a production-only runner that never executes staging, test or PR code.
  - Staging E2E test code never runs where production credentials are or were present.
  - The runner group is limited to named repositories and to pinned release workflows on protected refs. It is never available to `pull_request` or fork-triggered jobs.
  - Each environment's deploy identity holds a namespace-scoped Role with no `bind`, `escalate` or `impersonate` and no access to the other namespace.
  - The retained application Helm package contains only namespaced resources. Cluster-scoped prerequisites are Administrator-owned and sit outside release ownership (AD-3).
- Suggested disposition: autofix (the invariant text; the concrete mechanism stays implementation seed)

### SEC-2 — The shared Dapr control plane allows cross-namespace invocation, and callers are authorized by bare app ID
- Severity: critical
- Where: AD-8, AD-9, Hosted interfaces ("Internal service-to-service invocation uses Dapr"), Production profile authority.
- Finding: Staging and production share one Dapr control plane: one Sentry root CA, one placement service and one scheduler. The scheduler stores production reminders and jobs. Dapr on Kubernetes allows cross-namespace service invocation (`appid.namespace`) unless ACLs deny it. AD-2 promotes the same package with environment configuration, so app IDs are almost certainly identical in both namespaces.
  - Application-level caller authorization is keyed on app ID alone: EventStore `DaprInternal:AllowedCallers` and the Memories AD-5 operator artifact ("app-ID-to-`system:*` allowlist").
  - The AD-28 app-channel token is added by the *callee's own* sidecar, so it does not discriminate between callers.
  - The spine binds none of the following: namespace-qualified ACLs, a distinct trust domain per environment, a top-level `defaultAction: deny`, or a staging-to-production invocation negative test.
- Evidence:
  - `DaprComponents/accesscontrol.yaml` and `accesscontrol.eventstore-admin.yaml` set a top-level `defaultAction: allow`, `trustDomain: "public"` and `namespace: "default"`.
  - EventStore `deploy/dapr/accesscontrol.yaml` defaults both environments to the same trust domain, `{env:DAPR_TRUST_DOMAIN|hexalith.io}`.
  - `apphost.cs:105` sets `Authentication__DaprInternal__AllowedCallers__0=works`.
  - Memories spine AD-5 (line 110); EventStore AD-28 (line 299); memlog 65 (one control plane).
- Failure scenario: A staging workload is compromised, or a staging configuration value names `eventstore.<prod-ns>` or `memories.<prod-ns>`. The staging sidecar gets a valid SVID from the shared Sentry. If the policy namespace does not match, the production EventStore sidecar falls through to its top-level `allow`. The production app sees caller app ID `works` or `eventstore`, which is on its allowlist, and accepts internal or `system:*` operations against production tenants.
- Recommendation: Add a convention: "Every hosted sidecar Configuration uses a top-level `defaultAction: deny` and allow-lists callers by (trustDomain, namespace, appId). Each environment has its own trust domain. Application-level caller allowlists key on environment-qualified identity, never on bare app ID. Invocation from one environment into the other is a mandatory NFR-3 negative test. Components are namespaced and explicitly scoped." Classify the Dapr control plane as production-critical shared infrastructure. Staging deploy identities get no write access to `dapr-system`, Sentry material or control-plane configuration. Control-plane upgrades are Administrator changes that affect both environments under profile authority.
- Suggested disposition: autofix

### SEC-3 — OpenBao is neither classified per environment nor restorable per environment
- Severity: high
- Where: Secrets convention, AD-8 ("suitable supporting infrastructure may be reused"), AD-12, Memories erasure-continuity row.
- Finding: OpenBao holds application secrets, APP_API_TOKENs and tenant/payload key material. The spine never says whether it is an AD-8 application data service (separate per environment) or reusable supporting infrastructure. Only one instance was observed. The canonical `openbao-secret-contract.yaml` and EventStore AD-24's "singleton component" have no environment dimension.
  - A raft snapshot restore returns the *whole* cluster to the snapshot state (memlog 141). If staging and production share the instance, restoring for a staging incident rewinds production key generations and rotations and revives production tenant keys that were erased. AD-3 and the Memories row forbid exactly that, but nothing in the spine prevents it.
  - A shared instance also shares the root and unseal custody, audit device and policy-administration plane.
- Evidence: memlog 65, 129 (daily raft-snapshot CronJob), 140–141; spine lines 94, 127, 149; EventStore AD-24 (line 254).
- Failure scenario: An operator restores yesterday's OpenBao snapshot to fix a broken staging secret. Production tenant keys that were crypto-shredded today become usable again, and today's rotated production credentials are un-revoked. Separately, a staging automation token with `sys/policy` or `auth/*` write can widen its own policy to production paths.
- Recommendation: Classify key and secret authority as environment application state. Use a separate OpenBao instance per environment, which is recommended and consistent with AD-8. If the instance stays shared, require separate auth mounts bound to each namespace's service accounts, separate secret and transit mounts, separate operator policies, and no staging principal with `sys/*`, policy or auth write. Require a restore granularity that cannot rewind the other environment; raft snapshots cannot provide this. In both cases, revoke root tokens and name the unseal/recovery-key custodians. Add the environment dimension to the secret contract. Existing resources reused for production (hexalith-memories) are re-credentialed.
- Suggested disposition: discuss

### SEC-4 — Key-bearing backups defeat crypto-shredding, and backup custody is unbound
- Severity: high
- Where: Backup coverage and cadence row; AD-12 ("independently available operator/decryption access"); Disaster recovery evidence (drills); Memories erasure continuity row.
- Finding: The spine requires backups of shared services with 7-day and 30-day retention and independently available decryption material. It binds only the *restore* path against key resurrection ("secret-store recovery must preserve key destruction"). It says nothing about backups *at rest*.
  - Memories AD-16 allows retained EventStore backups only where tenant payloads are protected "exclusively by tenant key material that AD-16 destroys". Its verification requires that captured ciphertext "no longer decrypts" for every retained backup class.
  - OpenBao snapshots, or any key export, taken before an erasure keep the tenant key for up to 30 days. If they sit in the same off-site store as the ciphertext backups, the erased tenant can be decrypted by anyone with backup and decryption access. The AD-16 completion claim is then false.
  - The spine also does not require data backups to be immutable, or deletable only by credentials the primary site does not hold. On a one-node shared cluster, a compromise that reaches the backup writer can delete the recovery points.
  - Monthly drills restore production data *and keys* into an "isolated" environment. That environment has no defined lifetime, egress isolation, destruction, or erasure-target status.
- Evidence: spine lines 118, 146, 148–149; Memories AD-16 (lines 176–178, operational-backup rule and verification); memlog 132, 140 ("an old secret-store snapshot must never make an erased tenant key usable again").
- Failure scenario: Tenant T is erased on day 1 and completion is recorded. On day 10, someone with backup-bucket and backup-decryption access (a leaked Scaleway credential plus Administrator material, or an insider) pairs the day-0 EventStore base backup with the day-0 OpenBao snapshot and reads T's payloads. Alternatively, a drill environment created on day 0 still holds T readable on day 5.
- Recommendation: Add a cross-owner invariant:
  - Any backup containing tenant key material is either an AD-16 purge target, or tenant data keys are excluded from whole-store snapshots.
  - In the second case, keys are held in a key-escrow store whose entries are destroyed on erasure, with off-site durability through the qualified synchronous mirror.
  - Key-bearing backups and ciphertext backups never share custody: separate bucket, write credential and encryption key.
  - Data backups are immutable, or deletable only outside the primary failure domain.
  - Drill restores are ephemeral, egress-denied (no production realm, broker or provider writes), destroyed with verification within a stated bound, and listed as erasure targets while they exist.
  - Owners: Memories/EventStore (key hierarchy), Platform/Administrator (custody).
- Suggested disposition: discuss

### SEC-5 — Disaster restore can resurrect revoked access and compromised credentials
- Severity: high
- Where: AD-12 sequence ("establish trustworthy identity/secrets/deletion authority"); Disaster recovery evidence row.
- Finding: Restoring Keycloak (production realm) and OpenBao from a recovery point revives the state as of that point. That includes users whose production admission was revoked afterwards, disabled accounts, rotated client secrets, realm signing keys, APP_API_TOKENs and database or broker passwords. If the incident is itself a compromise, it also revives every credential the attacker already holds. The DR checks cover only "explicit production-user success and staging-user/credential denial". They do not deny principals revoked after the recovery point, and they do not require forward rotation. Deletion authority gets a continuity protocol (the tombstone mirror); revocation of access gets none.
- Evidence: spine lines 118, 148; AD-3 ("never rewind key generations or durable authority") applies to application rollback, not to DR; PRD FR-9 lists Keycloak and access configuration as recovery inventory.
- Failure scenario: A contractor's production admission is removed at 10:05. The storage fails at 10:50, and the newest usable point is 10:00. The restored realm re-admits the contractor. Or: a ransomware-driven restore brings back the pre-incident realm keys and client secrets that the attacker exfiltrated.
- Recommendation: Add to AD-12 and the DR evidence row:
  - Before reopening, rotate every credential and all signing material restored from backup.
  - Re-apply an off-site, append-only journal of admission revocations, user disables and secret revocations recorded after the recovery point.
  - Add a required negative check that principals and credentials revoked after the recovery point are denied.
  - For a restore driven by a compromise, rotate the Dapr trust root and Keycloak realm keys as well.
- Suggested disposition: autofix

### SEC-6 — Shared ingress, certificate and DNS wiring has no environment-bound host ownership
- Severity: high
- Where: Hosted interfaces row ("Platform owns ingress/DNS/TLS wiring using the existing suitable ingress/certificate infrastructure; modules declare supported routes").
- Finding: One ingress controller and cert-manager serve both `hexalith.com` and `tache.ai`, and the same Helm package templates hosts from environment configuration.
  - Nothing prevents the staging release, or a staging configuration error, from declaring an Ingress for a `tache.ai` host or path, or for the Keycloak host. With common controllers, the conflicting rule can capture production requests, including bearer tokens and credentials.
  - Nothing prevents the staging namespace from issuing a `Certificate` for `tache.ai` through a shared ClusterIssuer.
  - The production realm issuer hostname is undecided. If Keycloak is published under the staging domain, production JWKS discovery depends on staging-domain DNS, ingress and certificate control.
- Evidence: spine line 130; AD-6 is silent on the issuer host; nothing in the spine or memlog addresses ingress-class or host-admission policy.
- Failure scenario: A staging values file copies `host: app.tache.ai/api` for a new module path. The ingress controller merges it, and production users' API calls carrying production tokens reach the staging pod. Or: staging issues a valid certificate for `tache.ai` and serves a lookalike login page.
- Recommendation: Add a convention:
  - Hostnames are owned per environment and enforced at admission (ValidatingAdmissionPolicy or an equivalent policy engine, or a separate ingress class/controller per environment).
  - Only the production namespace may declare `tache.ai` hosts or production issuer hosts.
  - Certificate issuance uses namespaced Issuers, or ClusterIssuer selectors enforced by policy.
  - The production realm issuer lives under a production-controlled hostname.
  - A staging release that declares a production host is a required negative test.
- Suggested disposition: autofix

### SEC-7 — Namespace RBAC is insufficient on a one-node local-storage cluster, and pod and network controls are unnamed
- Severity: high
- Where: AD-8 ("Enforce network/RBAC … namespace separation alone is insufficient"); Hosted interfaces ("No direct-pod or alternate transport bypass").
- Finding: The binding exists, but the spine never names the mechanisms that make it enforceable.
  - On the observed single node with local storage, any principal allowed to create pods in the staging namespace can mount `hostPath` and read production persistent volumes and node credentials, unless Pod Security Admission blocks it. Namespace-scoped RBAC does not prevent this.
  - Without default-deny NetworkPolicy on a CNI that enforces it, staging pods reach production PostgreSQL, Redis/FalkorDB and RabbitMQ directly. Only a password stands in the way. They also reach app ports, sidecar internal gRPC, OpenBao, Keycloak admin endpoints and the API server.
  - The PRD NFR-3 negative tests cover staging *users and credentials*, not staging *pods*.
  - The residual risk of sharing one kernel and node is not recorded as accepted.
- Evidence: spine lines 94, 130, 207; memlog 75 and 162 (one Ready node, local storage).
- Failure scenario: A staging workload with a remote-code-execution bug, or a staging deploy credential, creates a pod with `hostPath: /var/lib/.../storage` and copies the production EventStore PostgreSQL data directory.
- Recommendation: Add to AD-8:
  - Both environment namespaces enforce Pod Security `restricted`: no hostPath, privileged, hostNetwork/PID or added capabilities.
  - Default-deny ingress and egress NetworkPolicies with explicit allows, on a CNI verified to enforce them.
  - NFR-3 negative tests from a staging pod to production data services, app ports, sidecars and volumes.
  - The shared-kernel residual risk recorded as an explicit accepted risk.
- Suggested disposition: autofix

### SEC-8 — The Keycloak administration credential path can bridge realms
- Severity: high
- Where: AD-6 ("Separate clients, service accounts and credentials"), AD-3 (Keycloak is outside release ownership), Staging gate (E2E needs staging users), Deferred table row "Secrets, identity…".
- Finding: AD-6 separates *application* realm credentials. It says nothing about who administers the realms, and with what credential: realm, client, role and audience configuration, and staging E2E test-user provisioning. The simplest implementation is a master-realm `admin-cli` credential stored as a runner or E2E secret, and that credential controls the production realm. Neither the spine nor the memlog forbids master-realm credentials in automation, or requires admin-event audit or a private-only admin console.
- Evidence: spine lines 82, 238; memlog 52–53.
- Failure scenario: The staging E2E fixture provisions synthetic users with a master-realm admin credential. The credential leaks from the staging path (see SEC-1). The attacker adds themselves to the production admission group, or adds a redirect URI or protocol mapper to a production client.
- Recommendation: Add to AD-6:
  - Realm administration is realm-scoped. No automation, runner or test fixture holds master-realm or cross-realm admin.
  - Staging provisioning uses a staging-realm-only management client.
  - Changes to production-realm users, admission, clients, IdPs and mappers are made by the Administrator only, with admin events enabled and exported off-cluster.
  - The master realm and admin consoles are not published on the public ingress.
- Suggested disposition: autofix

### SEC-9 — A McpCli credential is not bound to its environment, so production tokens can reach staging
- Severity: high
- Where: AD-11 ("Profiles bind an environment explicitly and confer no authority"); Deferred row "Connected McpCli … token provisioning/renewal".
- Finding: The spine protects the staging-token-to-production direction through the issuer check. It does not protect the reverse. McpCli resolves *each setting independently*: flag, then environment variable, then profile, then default. So `EVENTSTORE_TOKEN` (production) can combine with a profile URL (staging). Tokens are static long-lived bearers stored in plaintext `~/.eventstore/mcpcli.json` on user and agent hosts. A production token sent to the staging gateway lands on infrastructure with weaker controls, where it can be replayed against production until it expires.
- Evidence: McpCli spine AD-13 (line 134: "Each setting resolves flag, environment, selected Profile, default"), AD-14 (line 140: token field in the profile file), line 261 (static bearer); memlog 120 (a profile selects the gateway URL and a static bearer token).
- Failure scenario: An agent session exports a production `EVENTSTORE_TOKEN` and then runs `--profile staging` for a test. The staging gateway, or its logs or a compromised staging pod, captures a production bearer valid for the rest of its lifetime.
- Recommendation: Add to AD-11:
  - A credential is used only with the gateway of its issuing environment. The profile records the expected issuer and audience.
  - The client refuses to send a token whose issuer or audience does not match, and never mixes URL and token from different sources.
  - Hosted access uses short-lived OIDC tokens from a per-environment public McpCli client, obtained through device or authorization-code flow with PKCE. Refresh material is kept in an OS credential store.
  - Gateways never log or persist bearer tokens.
  - The mechanism stays with McpCli/EventStore.
- Suggested disposition: autofix (the invariant); defer the mechanism to the McpCli owner

### SEC-10 — MCP surface and confirmation restrictions cannot be enforced while the surface is client-asserted
- Severity: high
- Where: AD-11 ("the gateway … enforces the module's permitted operation/surface contract … Module-specific confirmation and surface restrictions remain binding … Parties exposes no MCP erasure operation"); AD-6 ("actor plus workload/delegation checks").
- Finding: The CLI and MCP heads are one binary with one profile token, and it runs on the user's or agent's host. The gateway can learn the surface only from client-supplied metadata. An agent with shell access can call the same operation through the CLI head, or with raw HTTPS, using the same bearer.
  - Parties restricts erasure to exactly two doors (Admin UI with typed-name confirmation, and consumer self-scope). McpCli is neither.
  - Projects AD-20 requires both an actor credential and a workload/delegation credential. It forbids CLI adapters from self-confirming, and AD-29 forbids MCP confirmation.
  - A static user bearer carries no workload or delegation identity. A client-asserted surface flag is not an authorization boundary.
- Evidence: spine line 112; Parties spine I7 (lines 186–196); Projects AD-20 (line 238) and AD-29 (line 292); McpCli AD-13/14.
- Failure scenario: A prompt-injected agent that holds a Parties administrator's production token calls `EraseParty` (or a Projects confirmation) through `hexalith` CLI or curl. The gateway sees a valid admin bearer and a "CLI" or absent surface, and executes an irreversible erasure outside its only permitted doors.
- Recommendation: Bind surface eligibility to *authenticated client identity*:
  - Each surface has its own Keycloak client per environment, and the gateway maps `azp`/`client_id` to the surface.
  - Agent-host sessions use a delegated or token-exchanged credential that carries a workload or agent marker.
  - Operations limited to a UI door, or needing human confirmation, require a token from the owning UI client, or a fresh interactive step-up claim that an agent host cannot mint.
  - Until this exists, such operations are not executable through the gateway with McpCli-client tokens.
  - Owners: EventStore gateway, McpCli, Platform realm configuration.
- Suggested disposition: discuss

### SEC-11 — The production admission record and Administrator custody are undefined
- Severity: medium
- Where: AD-6 ("explicit environment admission … Administrator explicitly enrolls production users"); Deferred table.
- Finding: The spine says admission is checked but does not say what the admission record is (realm membership, or a named group or role claim), where it is enforced, or who owns it. If admission equals membership of the production realm, then enabling any identity-provider brokering or first-broker-login flow, or a default role, admits users automatically, and "default grants" only partly covers this. AD-10's shared JWT contract validates roles, but the spine does not require the admission claim to be part of every host's fingerprinted configuration. Only the gateway is implied. A single person (the "Administrator", who is also the product owner) holds production admission, Keycloak admin, GitHub approval, OpenBao and backup decryption, and DR execution. The spine sets no MFA, break-glass, deputy or escrow requirement, although AD-12 needs "independently available operator access".
- Evidence: spine lines 82, 118; PRD glossary (line 312); EventStore AD-10 (line 154).
- Failure scenario: Someone enables GitHub login in the production realm for convenience. First login auto-creates users, and the Tenants UI host accepts any valid production-realm token. Or: the Administrator is unavailable or loses their device during an outage, and nobody can decrypt the backups within the four-hour RTO.
- Recommendation: Define admission as a named production-realm group or role that is never a default role and never mapped from an identity provider or first-login flow. Validate it through the AD-10 contract fingerprint on every externally reachable host. Require phishing-resistant MFA on every Administrator authority. Seal break-glass credentials with custody in two locations and alerting on use. Name a deputy or escrow for the AD-12 recovery access.
- Suggested disposition: autofix

### SEC-12 — Promotion evidence and artifacts lack authenticity and provenance checks
- Severity: medium
- Where: AD-2; Deployment ownership row.
- Finding: Digest pinning prevents a swap between staging and production. It does not prove the artifact came from the protected build path, or that the stored "passing E2E evidence" and release record were written by the trusted workflow. Builds already offers governed provenance attestations, but only as an opt-in (`if: inputs.governed-release`). AD-2 does not require that Platform-deployed images and Helm packages carry them, or that deployment verify them.
- Evidence: spine line 58; Builds `domain-release.yml:496-506` (the only job with `id-token`/attestation permissions is optional).
- Failure scenario: A compromised hosted build job, or any workflow with `contents: write`, publishes a digest and writes a release record or evidence file that the production workflow treats as authoritative.
- Recommendation: Require provenance verification at deploy for each image and Helm-package digest, against the protected Builds release workflow and ref. Make the governed mode mandatory for Platform-deployed artifacts. Only the environment-protected deployment identity may write release records and gate evidence, and production accepts only evidence from that identity.
- Suggested disposition: autofix

### SEC-13 — Diagnostics and notifications go through a likely public GitHub channel
- Severity: medium
- Where: Diagnostics and notification row; Automatic recovery row.
- Finding: The spine binds "classified, redacted" diagnostics but does not bind the channel's visibility. For public repositories, Actions logs (including self-hosted production deployment output), uploaded artifacts (release evidence is retained 30 days) and issues are readable by anyone. The spine requires GitHub notification to include environment and release identity plus evidence references.
- Evidence: spine line 131, 145; Builds `domain-release.yml:486-493`; the repositories are MIT-licensed and publish to nuget.org (visibility inferred, not queried).
- Failure scenario: A failed production rollout log or `helm`/`kubectl` output shows internal hostnames, IPs, namespace and app topology, tenant IDs in error text or secret references. Once published, anyone on GitHub can read it.
- Recommendation: Require hosted deployment, recovery and backup workflows, and their logs, artifacts and notifications, to live in a private repository or restricted environment. Notifications should carry only opaque references to an access-controlled evidence store. Apply EventStore AD-10's data prohibition to that evidence, and test the redaction.
- Suggested disposition: autofix

### SEC-14 — Smoke and DR check identity and the synthetic tenant are unspecified
- Severity: medium
- Where: Before production update ("Smoke writes use synthetic data without real-user changes"); DR evidence ("explicit production-user success").
- Finding: The spine does not say which production principal performs smoke writes and DR access checks, which tenant receives the synthetic data, or how that data is kept out of real tenants' projections, audits, exports, Memories indexes and aggregates. A convenient implementation is an admin-scoped service account held on the runner, which is standing production write authority (compare SEC-1).
- Evidence: spine lines 142, 148.
- Failure scenario: Smoke runs as a global admin. A smoke defect writes into a real tenant, or the credential leaks with cross-tenant rights.
- Recommendation: Use a dedicated production service principal admitted only to a dedicated, persistent synthetic tenant that is flagged and excluded from real-tenant views, exports and aggregates. Give it no admin or cross-tenant grants and issue its credentials per job. Define the data's retention and cleanup without disturbing tombstone or non-reuse rules.
- Suggested disposition: autofix

### SEC-15 — External-effect targets are not environment-scoped
- Severity: medium
- Where: AD-8 (separates credentials, not provider tenancy); AD-5 (CI "real-service" integration); External effects row.
- Finding: Folders writes to GitHub or Forgejo repositories, and an in-cluster Forgejo instance was observed as shared infrastructure. Memories uses external embedding and LLM providers. "Separate credentials" to the *same* provider org, account or instance does not isolate environments. Staging E2E critical flows, CI real-service tests and DR drills could then mutate production provider resources or consume production quotas.
- Evidence: spine lines 76, 94, 150; memlog 66 (forgejo namespace/schedule); Folders architecture (GitHub/Forgejo adapters).
- Failure scenario: A staging E2E Folders flow, using a staging GitHub App installed on the same organization as production, force-pushes to or deletes a production tenant's repository.
- Recommendation: Extend AD-8: each environment and CI uses distinct provider tenancy (App installation or org, Forgejo org or instance, provider account, webhook or mail target). Staging and CI credentials cannot write production provider resources. Shared in-cluster providers are classified per environment. CI never receives staging or production provider credentials.
- Suggested disposition: autofix

### SEC-16 — Sharing a data service between modules does not require per-module principals
- Severity: medium
- Where: AD-8 ("Compatible modules may share an instance within their environment … Retain module-required tenant isolation").
- Finding: "Compatible" is not defined in security terms. On a shared Redis or PostgreSQL, one module's Dapr component using a broad default credential can read another module's data. For example, Memories' unencrypted tenant projections, which require per-tenant backend principals (Memories AD-15), would be exposed. That silently defeats module isolation.
- Evidence: spine line 94; Memories AD-15 (line 170, production blocked until per-tenant backend principals exist).
- Failure scenario: Parties and Memories share an environment's Redis, and Parties' component uses the `default` user. A Parties defect or compromise then gives access to all of Memories' tenant data.
- Recommendation: Sharing within an environment requires distinct least-privilege backend principals per module, and per tenant where the module requires it. No module credential has instance-admin or cross-module grants. Instance-admin credentials belong to operators only. Components are scoped to the owning app IDs.
- Suggested disposition: autofix

### SEC-17 — AD-10 "explicit attach" can target hosted environments
- Severity: low
- Where: AD-10 ("Explicit attach checks composition … data isolation; externally owned services remain running").
- Finding: Local tests could attach to a hosted staging or production endpoint, which counts as an "externally owned service", using a developer's own production-admitted credentials. The PRD says tests must not affect staging or production data. AD-10's "checks data isolation" is not defined tightly enough to exclude this.
- Evidence: spine line 106; PRD line 99.
- Failure scenario: A developer points a local suite at the production gateway to debug an issue, and the fixtures write test data into a real tenant.
- Recommendation: Allow attach only to local or CI run-owned environments, or explicitly designated test environments. Attaching to hosted staging or production fails closed; hosted verification runs only through the AD-7 workflows.
- Suggested disposition: autofix

## Checked and clean
- **AD-6 realm split:** separate realms with per-environment issuer and audience validation, reusing the EventStore AD-10 contract, reject staging tokens in production. The spine explicitly forbids self-registration, copied membership and promotion granting production access. Remaining gaps are in SEC-8, SEC-9 and SEC-11.
- **AD-8 data layer:** separate data-service instances, brokers, volumes and Dapr components per environment remove the cross-environment backend-ACL problem. The Dapr runtime plane (SEC-2) and OpenBao (SEC-3) are not covered.
- **AD-3 and NFR-1:** application rollback never rewinds credentials, key generations, Keycloak or durable authority.
- **AD-5 CI:** disposable hosted runners with run-owned state and no hosted data or deployment credentials. Provider credentials are the exception (SEC-15).
- **AD-2 digest-bound promotion:** prevents a tag or rebuild swap between staging evidence and production. Provenance is covered in SEC-12.
- **Secrets policy:** Dapr/OpenBao only, Kubernetes Secrets for bootstrap only, and readiness gated on secret and key generations. This is sound at the policy level. Environment classification is covered in SEC-3.
- **AD-11 attack surface:** there is no hosted HTTP MCP server, no remote plugin loader and no second registry. The gateway reauthorizes every call and profiles confer no authority.
- **Memories erasure continuity on the restore path:** unknown lineage fails closed, shared Redis/FalkorDB projections have no operational backup, a copied register is not authority, and no operator override exists. Backups at rest are covered in SEC-4.
- **EventStore AD-28 app channel:** kept distinct from JWT, and neither grants tenant authority. It remains necessary but does not discriminate between callers (SEC-2).
- **Site-loss failure reporting:** must survive the primary site.
- **Shared EventStore host with module intent adapters:** this concentrates in-process trust, but the EventStore AD-25/AD-33 catalog binds each trusted adapter per route entry. I do not flag it at Platform altitude.
- **Local `DaprSelfHostedMtls.cs`:** it is Development-only and excluded from the manifest, and `apphost.cs` rejects non-Development execution. It does not leak into hosted profiles, though its `defaultAction: allow` pattern must not be exported (SEC-2).
