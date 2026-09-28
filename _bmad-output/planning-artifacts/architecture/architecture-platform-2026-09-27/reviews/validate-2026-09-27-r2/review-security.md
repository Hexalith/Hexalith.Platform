# Review — Security & identity (validate r2)

**Verdict: CONDITIONAL PASS.** No critical findings remain. The 2026-09-27 update closed the prior FAIL (shared runner, cross-namespace Dapr), and most of the 17 earlier findings are now spine rules. Seven high findings remain, each closable with rule wording and none requiring an adopted decision to be reversed:
- today's AD-11 amendment reopened hosted legacy MCP/CLI surfaces (SEC-1);
- AD-14 surface and revocation checks are lost across delegation hops (SEC-2);
- tokens an agent can use may carry Administrator authority (SEC-3);
- module code runs next to production credentials on executors (SEC-4);
- "authenticated Administrator record" has no defined authentication (SEC-5);
- the PRD-required deputy authority model is missing and conflicts with AD-6/AD-7 (SEC-6);
- shared-infrastructure hostnames sit in the staging DNS zone (SEC-7).

**Conditions:**
- SEC-1, SEC-2, SEC-4 and SEC-5 land before any hosted-environment story is finalized.
- SEC-3, SEC-6 and SEC-7 are decided before G1.

**Reviewed:** ARCHITECTURE-SPINE.md (working tree, including today's AD-11 and legacy-row diff), memlog decisions V-03/V-22–V-33/RV-1/ADV-U8/RV-3 and the gate reality evidence, PRD FR-9–FR-12, NFR-3, the addendum's McpCli context, the recovery-deputy section and G1 qualification note, EventStore AD-10/AD-28/AD-29 and the McpCli course correction, the McpCli spine (course correction, AD-13/AD-14, the HTTP-release actor), and code under `references/` (legacy MCP/CLI hosts, Builds workflow pins). Method: read-only; no infrastructure or GitHub API calls.

## Threat table

| Asset | Threat | Governing spine rule | Gap? |
| --- | --- | --- | --- |
| UI-only / confirmation-required operations (Parties erasure, Projects confirmations) | Agent executes them via a legacy MCP/CLI host or a non-gateway endpoint | AD-11, AD-14 (surface derived only at "gateways"), Hosted interfaces | **Yes: SEC-1** |
| Same | Surface is laundered through token exchange (azp becomes the module client), async attestation or a copied UI-client token | AD-14, EventStore AD-29 | **Yes: SEC-2** |
| Production admission and realm administration | Agent-held McpCli token for Administrator carries realm-management roles; no MFA invariant | AD-6, AD-14, Deferred "Secrets, identity…" | **Yes: SEC-3** |
| Same | "Synthetic" token flag used as an admission bypass at G1 | Synthetic identities, G1 | **Yes: SEC-8** |
| Production namespace secrets and deploy authority | Module smoke-suite or recovery-hook code reads the attempt job's deploy, environment-layer or cluster-scoped identity | AD-7, AD-8, Secrets | **Yes: SEC-4** |
| Promotion stop, approved-mode releases, deployment workflows | Record or workflow forged by any credential with ops-repo write (job token, monitor PAT) | Release modes, Automatic recovery, AD-7 | **Yes: SEC-5** |
| Recovery capability vs "Administrator alone" controls | Deputy cannot run DR without realm writes or ops-repo write, or is over-granted | Roles, AD-6, AD-7, DR sequence | **Yes: SEC-6** |
| Identity-issuer and registry trust (JWKS, login pages, pulls) | Staging ACME/DNS credential for `hexalith.com` mints certificates or DNS records for shared hostnames; Ingress snippet injection | Hosted interfaces | **Yes: SEC-7** |
| Revocations across DR | Old issuer still alive; module-owned revocations in the RPO window resurrected | DR sequence, Lost window | **Yes: SEC-9** |
| Tenant keys and ciphertext custody | Convergence on a persistent recovery executor; drill copies with no destruction bound | AD-7, AD-12, Backup coverage | **Yes: SEC-10** |
| Production realm state | Whole-server Keycloak restore rewinds production; shared server not classified | AD-6, Backup coverage, Accepted risks | **Yes: SEC-11** |
| NFR-3 isolation proof | Required negative cases not bound: OpenBao, identity administration, automation credentials, restored copies | AD-8 | **Yes: SEC-12** |
| Artifact integrity | Tag-pinned transitive actions; module-image signer identity not recorded; push credential can delete retained artifacts | AD-2, Workflows and provenance | **Yes: SEC-13** |
| Cross-environment Dapr invocation | Staging sidecar calls production | AD-8 (deny-default ACL, Sentry-validated namespace, separate control-plane authority) | Closed (SEC-14 low residual) |
| Production data services, volumes, pods | Staging pod, hostPath or network access | AD-8 (PSS restricted, default-deny NetworkPolicy, CNI proof) | Closed (PV nuance in SEC-12) |
| Staging evidence gating production | Compromised staging executor forges E2E or compatibility evidence | AD-2 (evidence only from the executor identity) | Accepted by design. Code still has to come from the protected publication workflow. Not flagged. |

## Prior findings (validate-2026-09-27) — verification

| Prior | Status in the current spine | Residual |
| --- | --- | --- |
| SEC-1 shared runner | Resolved: separate executors; production executor outside the cluster; per-job credentials; no bind/escalate/impersonate; allowlists; private ops repo | Smoke suites still run inside production jobs → SEC-4 |
| SEC-2 Dapr cross-namespace | Resolved: deny-default by trust domain, namespace and app ID; Sentry-validated namespace; control plane without staging write access; negative tests | App-level allowlists → SEC-14 |
| SEC-3 OpenBao classification | Resolved: per-environment instance in the data namespace; custodians named | Existing instance and shared bootstrap token remain owned Deferred work |
| SEC-4 key-bearing backups | Resolved in principle: tenant-key store is a separate custody class; erasure invalidates earlier key backups | Custody converges on the recovery executor; drill copies unbounded → SEC-10 |
| SEC-5 DR resurrects revoked access | Mostly resolved: DR step 4 rotates credentials and replays revocations | Old issuer and module-owned revocations → SEC-9 |
| SEC-6 ingress, certificates, DNS | Partial: hostname and issuer admission bound | Production issuer is no longer bound to a production-controlled name; shared hostnames sit in the staging zone → SEC-7 |
| SEC-7 PSS and NetworkPolicy | Resolved | PV scope → SEC-12 |
| SEC-8 Keycloak admin path | Resolved: realm-scoped administration | Shared-server restore → SEC-11; admin API reach → SEC-3 |
| SEC-9 McpCli environment binding | Resolved: profile binds gateway, issuer and audience; short-lived per-environment tokens | Refresh-token storage and lifetime → SEC-3 |
| SEC-10 client-asserted surface | Resolved at the gateway (azp, confidential UI clients, per-public-client negative test) | Delegation → SEC-2; non-gateway endpoints → SEC-1 |
| SEC-11 admission record and custody | Partial: named admission group | MFA, break-glass and deputy exist only as Deferred items → SEC-3, SEC-6 |
| SEC-12 provenance | Resolved for Platform artifacts | Administrator records → SEC-5; transitive pins and module identity → SEC-13 |
| SEC-13 public diagnostics | Resolved for deployment logs and artifacts | Notification repository and credentials unspecified → SEC-5 |
| SEC-14 smoke identity | Resolved (Synthetic identities convention) | Admission at G1 → SEC-8 |
| SEC-15, SEC-16, SEC-17 | Resolved (AD-8, AD-10) | — |

## Findings

### SEC-1 — The AD-11 amendment lets legacy MCP/CLI hosts run in hosted environments, and AD-14 is enforced only at the gateway
- **Severity:** high
- **Location:** AD-11 (amended today); AD-14; Hosted interfaces; Deferred row "Legacy MCP/CLI retirement…"
- **Finding:** The previous AD-11 said no module, FrontComposer or technical-module MCP host, including EventStore.Admin.Mcp, "is deployed or routed in Platform compositions until an AD admits it under AD-14". The new text calls these hosts "obsolete migration sources" and admits "no **new** alternate" surface. Existing hosts are kept until owner-approved retirement. EventStore's course correction says that retirement waits for a generic McpCli administration contract, so infrastructure administration keeps running through the legacy tools with no end date. Nothing now stops:
  - a module declaration from enrolling a legacy HTTP MCP host as a workload behind ingress;
  - legacy CLI/MCP clients from calling hosted non-gateway endpoints with static bearers.

  AD-14 has three gaps here:
  - It derives the surface only at "gateways", so the EventStore Admin Server and module-owned REST endpoints are outside it.
  - Its negative test covers only "each public client".
  - Nothing forces a legacy client into the agent-capable surface class.

  The addendum still says Platform "does not deploy or route them as new supported surfaces". The spine has lost even that.
- **Evidence:**
  - `Hexalith.Parties.Mcp` and `Hexalith.Memories.Mcp` are HTTP MCP hosts (`WithHttpTransport`, `MapMcp`).
  - `Parties.Mcp/McpContextForwardingHandler.cs` forwards the raw `Authorization` header and sets `X-User-Id`/`X-Tenant-Id`. That is raw token forwarding plus caller-set actor headers, both forbidden by AD-14.
  - `EventStore.Admin.Mcp` (stdio) and `Admin.Cli` take a static bearer from `EVENTSTORE_ADMIN_TOKEN` and call the Admin Server directly.
  - The McpCli spine's planned HTTP release "replaces Actor from the forwarded user header". The old AD-14 admission gate would have caught it; the new text does not.
- **Scenario:** An agent is configured with `Hexalith.EventStore.Admin.Mcp`, the production Admin Server URL and a copied Administrator bearer. Through the Admin Server it runs destructive stream, subscription or cluster operations. The Admin Server is not the gateway, so no azp-to-surface check applies, and the token is long-lived rather than an AD-11 short-lived per-environment token. Alternatively, `Parties.Mcp` is enrolled for migration parity and routed. It forwards whatever token and `X-User-Id` the agent supplies.
- **Suggested fix (rule wording):**
  - Add to AD-11: "Until retired, no legacy MCP host is deployed or routed in a hosted Platform composition. Legacy CLI and stdio MCP clients reach a hosted environment only through a realm-contract client mapped to the agent-capable class, with short-lived per-environment tokens, or not at all in production. Any new MCP/CLI surface, including a McpCli HTTP transport, needs an AD admitting it under AD-14."
  - Change AD-14 "gateways derive the surface" to "every externally reachable host that accepts bearer tokens derives the surface…, or accepts only tokens audienced to its own confidential client".
  - Extend the negative test to every client in the surface map.
- **Disposition:** autofix (restore the prohibition and widen AD-14); discuss the interim hosted path for infrastructure administration (for example Administrator-only through a confidential UI or executor workflow).

### SEC-2 — AD-14 loses the originating surface and current revocation across delegation hops
- **Severity:** high
- **Location:** AD-14 (server-to-server clause); EventStore AD-29 dependency
- **Finding:**
  - Keycloak standard token exchange sets `azp` to the requester, which is the calling module's confidential client. After one synchronous hop the derived surface is therefore the module's, not McpCli's.
  - The EventStore AD-29 attested delegation binds subject, service principal, tenant, operation, IDs, issuer and expiry. It does not bind the surface.
  - Nothing requires asynchronous steps to re-check the original actor's current admission. The PRD requires "denial after revocation" (NFR-3).
  - AD-14 makes UI-eligible clients "confidential server-side" but never says their tokens stay server-side. A bearer token copied from a UI session carries the UI surface to any host.
- **Scenario:**
  - An agent invokes an agent-eligible Projects operation through McpCli. Projects exchanges the token, or runs an attested async task step, and calls a Folders or Parties operation that is confirmation-required. The callee sees `azp = projects` (confidential) and allows it. FR-12's "refused through McpCli" is bypassed in one hop.
  - Separately, a user removed from the production group keeps executing through a long-running task under the attestation.
- **Suggested fix:** Add to AD-14: "Every exchanged token and EventStore-attested delegation carries the originating surface class. The effective surface of a call is the most restrictive class in its chain. A chain that originated on an agent-capable surface is never eligible for UI-only or confirmation-required operations. Each exchange is permitted per (requester, target audience) pair and downscopes to the target's declared operations. Asynchronous steps re-check the original actor's current admission and permissions and fail closed after revocation. Tokens minted for UI-eligible clients never leave their server (never delivered to a browser, CLI, log or environment variable)." Record the attested-surface field as an EventStore AD-29 owner confirmation in Deferred.
- **Disposition:** autofix (Platform rule); EventStore owner confirmation

### SEC-3 — Tokens an agent can use may carry Administrator authority, and Administrator authentication has no invariant
- **Severity:** high
- **Location:** AD-6; AD-14 ("public clients … least privilege"); Deferred "Secrets, identity, network and transport" (Administrator MFA, break-glass)
- **Finding:**
  - The Administrator is also the product owner and a McpCli user, and agents act with that user's token. If the production principal Administrator uses with McpCli also holds realm-management roles, Keycloak's default full-scope client setting puts those roles and the `realm-management` audience into McpCli tokens. The admin REST API accepts any realm token that carries those roles.
  - AD-6 keeps "admin consoles" off *public* ingress only. The installation sits on a LAN address, and the admin REST API is not named.
  - MFA and break-glass appear only as Deferred work items, not as invariants.
  - The McpCli spine stores bearers in plaintext `~/.eventstore/mcpcli.json`, and no rule binds refresh-token storage or lifetime, or whether `offline_access` is allowed, for hosted access.
- **Scenario:** A prompt-injected agent that holds the Administrator's McpCli token (or its refresh token, read from disk) calls `/admin/realms/<prod>/groups/<admission>/members` and admits an attacker. This bypasses "production realm changes are Administrator-only" without touching any executor.
- **Suggested fix:** Add to AD-6/AD-14:
  - "Identity-administration authority is held by principals distinct from every application principal used with McpCli, UIs or agents."
  - "Realm-contract clients disable full scope and map audiences and roles explicitly. Tokens from agent-capable clients never carry realm-management roles or the admin-API audience."
  - "The Keycloak admin API is reachable only from a declared Administrator path."
  - "Every Administrator and deputy authority (GitHub, Keycloak administration, OpenBao, registry, DNS, backup store, custody) requires phishing-resistant MFA. Break-glass credentials are sealed and alert on use."
  - "Public clients never receive `offline_access`. Hosted refresh material has bounded idle and maximum lifetime and is kept in an OS credential store, never in a profile file."
- **Disposition:** autofix

### SEC-4 — Module-supplied code runs inside jobs that hold production deploy, environment-layer or recovery credentials
- **Severity:** high
- **Location:** AD-7; Attempt ownership ("A production attempt runs from lock to terminal outcome in one job"); Verification; Production profile (environment-layer attempts end by re-verifying); DR sequence (module recovery hooks, restored-release smokes); AD-8 and Secrets convention
- **Finding:**
  - AD-7 says test code "never runs on an executor … that holds or held production credentials". It then has the production executor run "digest-identified smoke suites that passed staging". Those suites are module-authored code with NuGet closures. They run at a fixed cadence inside the attempt job that holds the per-job deploy identity. Environment-layer and shared-infrastructure attempts also re-verify, so the suites can run alongside the data-namespace identity or the cluster-scoped identity.
  - Passing staging is no trust property: malicious code can behave correctly in staging.
  - The recovery executor likewise runs module recovery hooks and smokes.
  - AD-8 says bootstrap Secrets sit "outside the application deploy identity's scope". However, Dapr resolves a Component's `secretKeyRef` in its own (application) namespace, and any identity that can create pods there can mount every Secret and service-account token in that namespace. The deploy identity is therefore equivalent to every app's OpenBao bootstrap token, and the spine's claim is not achievable as written.
- **Scenario:** A compromised test dependency in a module's smoke suite activates only when the target hostname ends in `tache.ai`. During the production verification window it reads the job's kubeconfig or OIDC token (and a job token or credentials persisted by checkout). It then creates a pod in the production namespace that mounts the OpenBao token Secrets, and exfiltrates production application secrets.
- **Suggested fix:** Add to AD-7: "Module-supplied code — smoke suites, E2E, recovery hooks — runs in an isolated sandbox (separate OS user or container) with no access to the job's credential material, runner workspace, job token or container runtime. The sandbox receives only short-lived tokens minted from the synthetic clients and the declared verification endpoint. Deploy, environment-layer, cluster-scoped and custody credentials never enter it." Reword AD-8 to say the application deploy identity is secret-equivalent for its namespace, which is why it is per-job and never co-resident with module code.
- **Disposition:** autofix

### SEC-5 — "Authenticated Administrator record" has no defined authentication, and ops-repo write paths are not bounded
- **Severity:** high
- **Location:** Release modes; Attempt ownership; Automatic recovery (promotion-stop clearance); AD-7 (single-writer ops repo on GitHub Free); Diagnostics and notification
- **Finding:** Administrator records gate approved-mode releases, stop clearance and takeover. The spine never says what authenticates them. On GitHub Free the private ops repo has no branch protection, environments or required reviewers (memlog RV-1). "Writable only by Administrator" therefore depends on every other credential that can write the repo:
  - workflow job tokens with `contents: write`, which executors may need if records live in the repo;
  - PATs or GitHub Apps held by the off-site monitor to raise issues;
  - deploy keys.

  The commit author field is free text. The spine also never names the repository that receives notification issues or runs the dead-man workflow. The Platform repository is public, where issue content is public and scheduled workflows are disabled after 60 days without activity.
- **Scenario:**
  - The monitor's classic PAT (repo scope, needed to create issues) leaks from the internet-facing off-site host. The attacker commits a "cleared" promotion stop authored as Administrator, or edits a deployment workflow that the production executor then runs with production credentials.
  - A job token read by a smoke suite (SEC-4) does the same.
- **Suggested fix:**
  - "Every Administrator record is signed by an Administrator-held identity that no executor, workflow token, monitor or deputy holds, and executors verify the signature before acting. Write access to storage never authenticates a record."
  - "Ops-repo workflows run with read-only repository permissions; no PAT, App or deploy key outside Administrator's devices can write the ops repo."
  - "Notifications and the dead-man check use a private repository through an issues-only credential."
- **Disposition:** autofix (invariant; the signing mechanism is an implementation seed)

### SEC-6 — The deputy's authority model is missing and conflicts with AD-6 and AD-7
- **Severity:** high
- **Location:** Roles; AD-6 ("production realm changes are Administrator-only"); AD-7 (single-writer ops repo; recovery executor); DR sequence steps 3–4; Secrets (custodians); Diagnostics ("assigned to Administrator")
- **Finding:** The addendum (Recovery deputy authority, Option 1) says explicitly: "Architecture follow-up is required … do not yet implement deputy recovery authority". The spine has not done it.
  - The DR sequence needs production-realm writes: restore Keycloak, replay revocations, rotate realm keys and client secrets. AD-6 makes all of these Administrator-only.
  - The deputy can only start recovery workflows by triggering jobs in the ops repo, which needs write access. That breaks single-writer and triggers the GitHub Team review, and no alternative trigger path is defined.
  - Notifications reach only Administrator. PRD FR-8/FR-9 and the addendum require delivery to the deputy as well.
  - If one custodian's recovery shares can unseal or generate an OpenBao root, the deputy alone holds full production secret authority. That is broader than "recovery only", and no audit or revocation rule applies.
- **Scenario:** Administrator is unreachable during an outage. Either the deputy cannot complete step 4 within the RTO, or the runbook hands the deputy realm-admin and ops-repo write "for recovery". That standing authority then covers production-user administration and stop clearance, which the PRD reserves to Administrator.
- **Suggested fix:** Add a deputy clause:
  - "The deputy acts through a named personal identity and may trigger only allowlisted recovery workflows through a declared path that grants no ops-repo write."
  - "Recovery workflows may restore identity state and apply only subtractive or rotational changes (journaled revocations, credential and key rotation); they never grant admission or roles and never clear the promotion stop."
  - "Each custodian's use of unseal or root material is alerted, and any root token is revoked at the end of recovery."
  - "Notifications are assigned to Administrator and the deputy."
- **Disposition:** discuss (choosing the trigger path under GitHub Free is a user decision)

### SEC-7 — Shared-infrastructure hostnames sit in the staging environment's DNS zone, and the ingress boundary covers only hostnames
- **Severity:** high (medium if no identity-issuer host is under `hexalith.com`)
- **Location:** Hosted interfaces
- **Finding:** The spine says the staging namespace can neither serve nor obtain certificates for shared-infrastructure hostnames ("registry, identity issuers"). It also gives each environment its own ACME credential. However:
  - The registry is `registry.hexalith.com`, inside the staging zone.
  - The production-realm issuer host is unbound; the earlier decision V-25 ("only production may declare … production issuer hosts") was widened into "shared".
  - A namespaced Issuer has to hold its DNS-01 credential as a Secret in the staging namespace. A zone-wide `hexalith.com` token therefore lets any staging deploy identity or pod mint certificates and change DNS for every shared name, outside cert-manager policy.
  - Hostname admission does not stop controller-level configuration injection (snippet or raw-config annotations) on the shared ingress controller. That controller holds both environments' TLS Secrets.
- **Scenario:** A staging compromise uses the zone token to issue a certificate and repoint DNS for the Keycloak issuer host. Production login is phished (the Administrator included), or JWKS is served to gateways that resolve through public DNS, which forges production tokens.
- **Suggested fix:**
  - "Shared-infrastructure and production-trust hostnames live in a zone or delegated subzone whose DNS and ACME credentials no staging identity, namespace or executor holds. Staging ACME credentials cover only staging names."
  - "The production realm issuer uses a production-controlled name."
  - "Application-namespace Ingress objects may set only host, path, TLS and backend fields; controller configuration injection is rejected at admission."
- **Disposition:** discuss

### SEC-8 — The G1 empty admission group conflicts with synthetic smoke actors
- **Severity:** medium
- **Location:** Production entry gates (G1); Synthetic identities; AD-6
- **Finding:** G1 requires "the production admission group is empty", yet smokes with synthetic actors run from the first production deployment. The spine never says how synthetic actors pass admission. The addendum requires the architecture to carry the restricted step in which Administrator temporarily admits a synthetic test identity. The natural shortcut, "flagged synthetic token ⇒ admitted", is a second admission path controlled by a protocol mapper.
- **Scenario:** A developer implements an admission bypass keyed on the synthetic flag. A later realm change adds the flag mapper to another client, and that client's users skip admission.
- **Suggested fix:** "Synthetic actors are admitted only through an Administrator-granted named group limited to the synthetic tenant. G1's empty group means no non-synthetic members. The gateway never treats the synthetic flag as admission. The SM-4 test grant is recorded and revoked after verification."
- **Disposition:** autofix

### SEC-9 — The DR fence and revocation replay are incomplete
- **Severity:** medium
- **Location:** DR sequence steps 1 and 4; Lost window and external effects
- **Finding:**
  - The fence revokes database, broker, OpenBao, backup-write and deployment credentials. It does not cover:
    - the old identity issuer: realm signing keys rotate only in a compromise-driven restore, so a partly alive old Keycloak keeps minting tokens the restored gateways accept;
    - external-provider credentials: only workers are disabled;
    - access to the tenant-key store.
  - Replay covers Keycloak admin and user events only. Module-owned authorization revocations acknowledged in the RPO window are lost, for example Tenants membership or role removal. They are neither re-applied nor listed as an accepted exception, and FR-9 requires "denial of revoked principals".
- **Scenario:** A tenant admin removes a user from a tenant 40 minutes before a storage failure. The restored Tenants state re-grants that access, and the DR report says nothing about it.
- **Suggested fix:**
  - "The fence also revokes the old environment's provider credentials and tenant-key access, and rotates realm signing keys unless the old issuer is proven unavailable."
  - "Module-owned authorization revocations after the cut are re-applied from an off-site journal, or are listed as an accepted RPO exception that Administrator reviews before reopening."
- **Disposition:** autofix (the exception wording); discuss (whether a journal is needed)

### SEC-10 — Custody classes converge on the persistent recovery executor
- **Severity:** medium
- **Location:** AD-7 (recovery executor with pre-provisioned credentials in off-site custody); AD-12 (tenant-key store as a separate custody class); Backup coverage; DR evidence (drill restores ephemeral)
- **Finding:** Restoring requires read access to ciphertext backups, decryption material, the tenant-key backup and prepared-capacity credentials, and all of them meet on one long-lived, off-site host that runs drills every month. The spine does not bind any of the following:
  - custody material released per job, rather than held standing;
  - rotation after drills or DR;
  - no write or delete on recovery points;
  - provenance verification of artifacts pulled from the off-site replica;
  - a destruction bound for drill and quarantine copies, or their status as erasure targets.

  The result undoes the separation AD-12 establishes.
- **Scenario:** An attacker persists on the recovery executor between drills and captures decrypted production data and tenant keys during the next monthly drill.
- **Suggested fix:** "The recovery executor holds standing credentials only for the prepared capacity plus read-only access to recovery points. Custodians release key and decryption material per job, and it is destroyed at job end. Credentials rotate after every drill and DR. Restored artifacts are provenance-verified, with attestations replicated. Drill and quarantine copies are erasure targets and are destroyed with verification within a stated bound."
- **Disposition:** autofix

### SEC-11 — The shared Keycloak server is not classified as production-critical, and its restore granularity is unbound
- **Severity:** medium
- **Location:** AD-6; AD-8 (only the Dapr control plane is classified); Backup coverage; Accepted risks
- **Finding:** Both realms share one Keycloak process, database, hostname and admin API. A whole-server restore, for example to repair the staging realm, rewinds production admission removals and revocations. Outside DR, nothing forbids this and nothing triggers replay. Staging pods and the staging management client reach the same admin listener. The accepted-risk list names the shared kernel but not the shared Keycloak server, Dapr control plane or ingress controller.
- **Scenario:** A staging realm corruption is repaired with yesterday's Keycloak database backup, and a user revoked from production today is admitted again.
- **Suggested fix:** "The Keycloak server, its database and its backups are production-critical shared infrastructure with no staging write, backup or restore access. Staging realm recovery regenerates from the realm contract. Any whole-server restore is a production DR-class change under both locks, with post-cut revocation replay. The shared Keycloak server, Dapr control plane and ingress controller are listed as accepted residual risks."
- **Disposition:** autofix

### SEC-12 — The AD-8 negative-test list does not bind the NFR-3 matrix
- **Severity:** medium
- **Location:** AD-8 ("Negative tests from staging users, credentials and pods…"); PRD NFR-3 and SM-C2
- **Finding:** AD-8 names data services, app ports, sidecars, workflows, actors, volumes and hostnames. NFR-3 also requires:
  - secret access: the production OpenBao;
  - identity administration: the staging management client and staging realm admins against the production realm, and McpCli tokens against the admin API;
  - automation credentials: staging executor identities against production namespaces, environment-layer objects, registry writes, production records and evidence, production backup prefixes, and the production internal verification endpoint, including from the executor's off-cluster network position;
  - restored copies: quarantine and drill restores unreachable from staging;
  - denial after revocation, repeated after every admission change and not only in DR.

  Separately, PersistentVolumes are cluster-scoped, so "volumes in the data namespace" is not an enforcement boundary; a staging PVC must not be able to bind a production PV.
- **Scenario:** A qualification suite passes AD-8's list while a staging executor credential can still read production backup prefixes.
- **Suggested fix:** Replace the AD-8 list with NFR-3's categories, adding secrets, identity administration, automation credentials, restored copies, PV binding and post-revocation denial. "PVs are created only by the production shared-infrastructure identity and are pre-bound to their environment's claims."
- **Disposition:** autofix

### SEC-13 — Pinning and provenance stop at the Builds reusable-workflow boundary
- **Severity:** medium
- **Location:** Workflows and provenance; AD-2 (deploy provenance check)
- **Finding:**
  - Only "SHA-pinned Builds reusable workflows" is bound. The actions they call transitively can still move.
  - AD-2 accepts only digests whose provenance names *the publication workflow*. Module images are built by module workflows, and the expected signer identity (repository, workflow, protected ref) for each module is recorded nowhere. Deploy therefore either rejects module images or has to loosen the check.
  - Nothing prevents the publication push credential from deleting or overwriting retained artifacts, nor makes the off-site replica's writer distinct.
- **Evidence:**
  - Builds composite actions pin by tag: `publish-container-to-registry` uses `docker/login-action@v4.6.0` (it handles registry credentials), `initialize-dotnet` and `package-release` use `setup-dotnet@v6.0.0`, and `build-release.yml` uses `NuGet/login@v1.2.0`.
  - Builds already has a governed uses-closure check (`Github/governed-provenance`), but the spine does not require it.
- **Scenario:** A retagged third-party action inside the Builds release path exfiltrates the registry push credential or tampers with the build before attestation. SHA-pinning the Builds workflow itself does not catch either.
- **Suggested fix:**
  - "The full uses closure of the publication and deployment workflows is SHA- or digest-pinned and checked by the Builds governed closure."
  - "The intake manifest records each module image's expected provenance identity, and deploy verifies against it."
  - "Publication credentials cannot delete or overwrite retained artifacts; the replica has a separate writer."
- **Disposition:** autofix

### SEC-14 — Application-level caller allowlists are still keyed on bare app IDs
- **Severity:** low
- **Location:** AD-8 (only the Dapr-level ACL is bound)
- **Finding:** Memlog V-22 accepted "environment-qualified caller allowlists", but the spine keeps only the Dapr Configuration rule. EventStore `DaprInternal:AllowedCallers` and the Memories operator artifact still key on the app ID. The sidecar ACL now enforces namespace, so this is lost defense in depth rather than an open path.
- **Scenario:** The Configuration's deny-default is regressed by a profile or template change. Production apps then accept a staging `eventstore` or `works` caller.
- **Suggested fix:** Add to AD-8: "application-level caller allowlists key on (namespace, app ID)".
- **Disposition:** autofix

## Checked and clean
- **Actor spoofing:** the gateway rejects a Contracts-declared actor property that differs from the token subject or the attested actor; `--actor` is refused in hosted environments; raw forwarding is forbidden (apart from the legacy hosts in SEC-1).
- **Environment binding of McpCli tokens:** profile-bound gateway, issuer and audience; no fallback.
- **Public vs confidential clients:** UI-only surfaces are confidential, and UI clients register no loopback or wildcard redirects.
- **Dapr isolation:** separate trust domains. The spine correctly treats the Sentry-validated namespace, not the trust domain, as the discriminator.
- **Pod Security restricted, default-deny NetworkPolicy and the CNI proof** are bound.
- **OpenBao per environment; tenant-key store excluded from snapshots; rotated generations retained.** Erasure invalidates earlier key backups while a fresh backup keeps other tenants recoverable. This is consistent with immutable copies, provided wrapping keys are destroyed rather than objects.
- **Private deployment logs; opaque notification references; the gateway never logs bearers.**
- **CI** holds no hosted or provider credentials. Hosted attach fails closed.
- **Executor event, ref and workflow allowlists and OIDC claim checks** are sound compensations for GitHub Free, given SEC-4 and SEC-5.
- **Staging evidence gating production** is an accepted design dependency, because code provenance still binds to the protected publication workflow.
