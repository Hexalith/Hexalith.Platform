# Review — Reality & version check (update run)

- Target: `ARCHITECTURE-SPINE.md` (updated 2026-09-27), `.memlog.md` `(version)` entries, working tree (`apphost.cs`, `global.json`, `README.md`, `DaprSelfHostedMtls.cs`, `references/Hexalith.Builds/Props/Directory.Packages.props`, Builds workflows, Memories deploy assets).
- Reviewer lens: every committed decision must be web-researched or reality-checked, not asserted from training data. I used official sources only: vendor docs, GitHub release APIs, NuGet APIs, upstream source, and package inspection.
- Date: 2026-09-27. The review was read-only apart from this file. No runtime, cluster, or Git state was changed.

## Verdict

**FAIL for handoff, blocked by RV-1.** Every version pin in the Stack is current or has its gap correctly labelled, and most memlog `(version)` evidence still holds. Four load-bearing mechanisms, though, rest on capabilities that do not exist as assumed:

- **RV-1:** the GitHub plan the organization is actually on.
- **RV-2:** where artifact attestations can be minted.
- **RV-3:** `azp` for public clients.
- **RV-4:** Dapr sidecars under Pod Security `restricted`.

Counts: 1 critical, 3 high, 4 medium, 6 low (14 total). Disposition: 7 autofix, 5 discuss (RV-5 and RV-8 are mixed autofix and discuss), 2 defer.

## Verification ledger

| Claim in spine / memlog | Result | Source |
| --- | --- | --- |
| .NET SDK 10.0.401, latestPatch | Current. It is the latest SDK, from the 10.0.12 security release on 2026-09-08 | https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json |
| Aspire AppHost SDK / Docker / Redis 13.5.4 is the current stable | Confirmed; no 13.6 has been published | https://api.nuget.org/v3-flatcontainer/aspire.appHost.sdk/index.json |
| Aspire.Hosting.Kubernetes is preview-only | Confirmed; the latest is `13.5.4-preview.1.26464.4`, with no stable version in the NuGet registration | https://api.nuget.org/v3-flatcontainer/aspire.hosting.kubernetes/index.json |
| PublishAsKubernetesService can add annotations and CRDs | Confirmed by inspecting the pinned package. `PublishAsKubernetesService(Action<KubernetesResource>)` exposes `Workload` and `AdditionalResources`, and the package ships the `BaseKubernetesResource`, `PodTemplateSpecV1`, `ObjectMetaV1`, `SecurityContextV1`, `CapabilitiesV1` and `SeccompProfileV1` types | nupkg `aspire.hosting.kubernetes.13.5.4-preview.1.26464.4` XML docs; https://aspire.dev/integrations/compute/kubernetes/ |
| CommunityToolkit Dapr: last stable 13.0.0, preview since 13.1.0 | Confirmed; the latest is `13.5.1-beta.770` | https://api.nuget.org/v3-flatcontainer/communitytoolkit.aspire.hosting.dapr/index.json |
| Hexalith.EventStore.Aspire latest is 3.109.0, which is also the Builds catalog version | Confirmed | https://api.nuget.org/v3-flatcontainer/hexalith.eventstore.aspire/index.json; Builds props L9 |
| Dapr runtime latest is 1.18.4 | Confirmed (published 2026-09-09). The Dapr CLI latest is 1.18.2. Dapr .NET SDK `1.19.0-preview.2` exists, so a 1.19 line is coming | https://api.github.com/repos/dapr/dapr/releases; https://api.nuget.org/v3-flatcontainer/dapr.client/index.json |
| Dapr Configuration accessControl matches callers by trustDomain, namespace and appId | Confirmed for **service invocation only**; see RV-5 | https://docs.dapr.io/operations/configuration/invoke-allowlist/ (v1.18) |
| Dapr `secretstores.hashicorp.vault` against OpenBao, token auth | Confirmed. The component is token-only (`vaultToken` / `vaultTokenMountPath`), and Dapr documents it as tested with OpenBao. The brownfield Memories component already uses it | https://docs.dapr.io/reference/components-reference/supported-secret-stores/hashicorp-vault/; https://docs.dapr.io/reference/components-reference/supported-secret-stores/openbao/; `references/Hexalith.Memories/deploy/kubernetes/base/dapr/secretstore.yaml` |
| Helm OCI manifest digest; Helm 4.x | Confirmed. Helm 4 installs by `oci://…@sha256:`, and `helm push` reports the manifest digest. The latest release is 4.3.0 (2026-09-09); Aspire requires at least 4.2.0. See RV-9 | https://helm.sh/docs/topics/registries/; https://helm.sh/docs/overview/; https://api.github.com/repos/helm/helm/releases |
| Kubernetes 1.34 EOL 2026-10-27; supported minors 1.35–1.37 | Confirmed | https://kubernetes.io/releases/ |
| Pod Security `restricted` works with Dapr sidecar injection | **Not true with Dapr defaults**; see RV-4 | https://kubernetes.io/docs/concepts/security/pod-security-standards/; https://raw.githubusercontent.com/dapr/dapr/master/charts/dapr/README.md |
| Keycloak `azp` identifies the calling surface | Keycloak always sets `azp` to the requesting client_id. For public clients that value is not authenticated; see RV-3 | Keycloak `TokenManager.java` L1117 (`token.issuedFor(client.getClientId())`); https://www.rfc-editor.org/rfc/rfc8252#section-8.6 |
| Keycloak admin events exported off-cluster | The mechanism exists (DB store + Admin REST, or an event listener) but is off by default; see RV-6 | Keycloak `server_admin/topics/events/admin.adoc`; `JBossLoggingEventListenerProviderFactory.java` |
| Runner groups restrict to named workflows on protected refs; per-job credentials from protected environments; hosted workflows in a private ops repo | **Not available on the org's GitHub Free plan**; see RV-1 | `gh api orgs/Hexalith` → `plan.name: "free"`; GitHub docs cited in RV-1 |
| Deploy verifies provenance against the protected Builds workflow and ref | `gh attestation verify --signer-workflow/--source-ref` exists. Attestations in private repos need GHEC; see RV-2 | https://cli.github.com/manual/gh_attestation_verify; https://docs.github.com/actions/security-for-github-actions/using-artifact-attestations/using-artifact-attestations-to-establish-provenance-for-builds |
| GitHub-hosted off-site monitor and GitHub as the single notification path | The platform limits are real; see RV-7 | https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows |
| Hostname ownership enforced at admission | The mechanism exists: ValidatingAdmissionPolicy has been stable since 1.30 | https://kubernetes.io/docs/reference/access-authn-authz/validating-admission-policy/ |

## Findings

### RV-1 — AD-7 release-execution controls depend on GitHub features unavailable on the organization's Free plan
- **Severity:** critical
- **Location:** AD-7 (runner groups, protected refs, protected environments); Consistency Conventions → Diagnostics ("Hosted … workflows … private"); AD-7 last sentences (private operations repository).
- **Claim:** "Runner groups accept only named release workflows on protected refs, never `pull_request` or fork events, with per-job credentials from protected environments." It adds that hosted release, recovery and backup workflows live in a **private** operations repository.
- **Reality:**
  - `gh api orgs/Hexalith` returns `plan.name = "free"` (observed 2026-09-27). `Hexalith/Hexalith.Platform` is PUBLIC.
  - On GitHub Free, a private repository cannot create environments: "Creation of an environment in a private repository is available to organizations with GitHub Team and users with GitHub Pro." Environment secrets in private repositories also need Pro, Team or Enterprise.
  - Required reviewers, wait timers and custom protection rules in private repositories need Enterprise; on Free, Pro and Team they are available for public repositories only.
  - Protected branches and rulesets in private repositories need Pro or Team, so "protected refs" do not exist in a private repository on Free.
  - Only the Team plan can create additional organization runner groups; "All organizations have a single default runner group." The "Selected workflows" restriction (workflow pinned to a ref) is documented only in the Enterprise Cloud edition of the docs, not the Free/Pro/Team edition.
  - Runner groups cannot filter by event type at all. The rule "never `pull_request` or fork events" has to come from workflow triggers and branch policies.
  - "Only jobs directly defined within the selected workflows" get access. Jobs defined inside Builds reusable workflows therefore need the reusable workflow itself pinned by SHA or tag.
- **Evidence:**
  - `gh api orgs/Hexalith`
  - https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments
  - https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments
  - https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches
  - https://docs.github.com/en/actions/concepts/runners/runner-groups
  - https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/manage-access
  - https://docs.github.com/en/enterprise-cloud@latest/actions/how-tos/manage-runners/self-hosted-runners/manage-access
  - The memlog and the validation reviews contain no record of the GitHub plan being checked.
- **Failure scenario:** The implementers fall back to repository-level secrets in the private operations repository. Any workflow on any branch of that repository can then read production deployment credentials. This is the exact exposure V-02 set out to close.
- **Fix:** Record the GitHub plan (and the operations-repository owner) as a profile-inventory prerequisite, then pick one option:
  - **(a) Plan upgrade:** move the org to GitHub Team at minimum, which provides environments, deployment-branch policies, protected branches, rulesets and runner groups. Add GHEC if pinned runner-group workflows, required reviewers in private repos, or private-repo attestations are required.
  - **(b) Enforce at the target instead:** keep the plan and authenticate deploy jobs to the Kubernetes API through GitHub Actions OIDC federation. Validate claims `repository`, `job_workflow_ref`/`workflow_ref`, `ref`, `environment`, `event_name` and `runner_environment`; these claims are available on all plans (https://docs.github.com/en/actions/reference/security/oidc). Add an executor-side allowlist of workflow refs.

  Either way, reword AD-7 so it names the enforcing mechanism rather than "runner groups".
- **Disposition:** discuss

### RV-2 — Provenance verification assumes attestations wherever the release workflow runs; private repositories cannot mint them on this plan
- **Severity:** high
- **Location:** AD-2 ("Deploy verifies every digest's provenance against the protected Builds workflow and ref"); AD-13 ("The Platform release workflow issues the composed image's identity"); AD-7 (private operations repository).
- **Claim:** Every package and image digest carries verifiable provenance from the protected Builds workflow.
- **Reality:**
  - On Free, Pro and Team, artifact attestations are available only in public repositories. Private or internal repositories need GitHub Enterprise Cloud.
  - Public-repo attestations go to the public Sigstore transparency log.
  - Builds already mints attestations for NuGet candidates (`references/Hexalith.Builds/.github/workflows/domain-release.yml`, `attestations: write`), so the mechanism works while Platform and Builds stay public.
  - The spine does not say which repository builds and attests the composed `eventstore` image and the Helm package. If "Platform release workflow" is read as the hosted release workflow in the private operations repository, no attestation can be produced.
  - `gh attestation verify` supports `--signer-workflow`, `--signer-repo`, `--source-ref` and `oci://` subjects. Container images are documented; verifying Helm-chart OCI artifacts is not explicitly documented.
- **Evidence:**
  - https://docs.github.com/actions/security-for-github-actions/using-artifact-attestations/using-artifact-attestations-to-establish-provenance-for-builds ("If you are on a GitHub Free, GitHub Pro, or GitHub Team plan, artifact attestations are only available for public repositories")
  - https://cli.github.com/manual/gh_attestation_verify
  - `gh repo view` → PUBLIC
- **Fix:** Amend AD-2 and AD-13: the application package, composed host image and module images are built and attested in the public Platform and Builds repositories via Builds reusable workflows, pinned by SHA or tag. The private operations repository only verifies and deploys. Add "Helm-chart OCI attestation verification" to the Aspire-to-Helm or release qualification row.
- **Disposition:** autofix

### RV-3 — `azp` is an authenticated surface only for confidential clients; McpCli and any public UI client can be impersonated
- **Severity:** high
- **Location:** AD-14; AD-11 (hosted McpCli tokens with refresh material in the OS credential store).
- **Claim:** "Gateways derive the surface from the authenticated client (`azp`)", which prevents "agent-held tokens executing UI-only or confirmation-required operations."
- **Reality:**
  - Keycloak sets `azp` to the client_id that requested the token (`TokenManager`: `token.issuedFor(client.getClientId())`).
  - McpCli is a native app with refresh tokens on the user host, so it is necessarily a public client. RFC 8252 says a native client's identity cannot be assured: statically shipped secrets "should not be treated as confidential secrets", and servers should not auto-approve "except when the identity of the client can be assured".
  - Any process on the user host can therefore complete a PKCE flow with *another* public client's client_id whose redirect URIs it can satisfy, such as a loopback redirect or a public SPA or WASM UI client. It receives a token whose `azp` is that surface.
  - The restriction holds only when every surface that may execute agent-ineligible operations is a confidential (server-side) client.
  - No memlog `(version)` entry checked `azp` semantics.
- **Evidence:**
  - https://github.com/keycloak/keycloak/blob/main/services/src/main/java/org/keycloak/protocol/oidc/TokenManager.java (L1117)
  - https://github.com/keycloak/keycloak/blob/main/core/src/main/java/org/keycloak/representations/JsonWebToken.java (`AZP`)
  - https://www.rfc-editor.org/rfc/rfc8252#section-8.5 and #section-8.6
- **Fix:** Amend AD-14 and the realm contract:
  - Surfaces permitted to run agent-ineligible, UI-only or confirmation-required operations must be confidential clients (BFF, client authentication required).
  - A public client's `azp` is treated as agent-capable, least-privilege.
  - UI clients may not register loopback or wildcard redirect URIs.
  - Add an NFR-3 negative test: a token minted by an agent through each public client is denied the UI-only operations.
- **Disposition:** discuss

### RV-4 — Pod Security `restricted` rejects Dapr-injected pods under default Dapr settings
- **Severity:** high
- **Location:** AD-8 ("Both namespaces enforce Pod Security `restricted`"); AD-1 (one shared helper adds the Dapr sidecar annotations); Production profile (the Dapr control plane pin); Owned work, "Secrets, identity, network and transport".
- **Claim:** Hosted namespaces enforce `restricted`, and workloads receive Dapr sidecars.
- **Reality:**
  - `restricted` requires every container, including init and native-sidecar containers, to have `allowPrivilegeEscalation: false`, `runAsNonRoot: true`, `capabilities.drop: [ALL]` and a `RuntimeDefault` or `Localhost` seccomp profile at pod or container level.
  - The Dapr injector sets `allowPrivilegeEscalation: false`, `runAsNonRoot` and `readOnlyRootFilesystem`. It adds `drop: [ALL]` only when the Helm value `dapr_sidecar_injector.sidecarDropALLCapabilities` is true, and that value defaults to **false**. It sets seccomp only with the per-pod annotation `dapr.io/sidecar-seccomp-profile-type`; by default the injector adds no seccompProfile.
  - Because `sidecarDropALLCapabilities` is a **control-plane-wide** setting on the shared Dapr control plane, it is a shared-infrastructure change under the attempt lock and change owner.
  - Aspire-generated workloads carry no container security context by default. The pinned package does expose the `SecurityContextV1`, `CapabilitiesV1` and `SeccompProfileV1` types, so the shared helper can set them.
  - Dapr 1.18 can inject native sidecars, which are init containers; these are also checked.
  - Memories manifests already set pod seccomp and non-root settings, and its OpenBao namespace enforces `restricted`. So data services can be made compliant; the sidecar is the gap.
- **Evidence:**
  - https://kubernetes.io/docs/concepts/security/pod-security-standards/
  - https://raw.githubusercontent.com/dapr/dapr/master/charts/dapr/README.md (`sidecarDropALLCapabilities` default `false`)
  - https://docs.dapr.io/operations/hosting/kubernetes/kubernetes-production/
  - https://docs.dapr.io/reference/arguments-annotations-overview/
  - https://github.com/dapr/dapr/blob/release-1.18/pkg/injector/patcher/sidecar_container.go (L238–251)
  - https://blog.dapr.io/posts/2026/06/10/dapr-v1.18-is-now-available/
  - `references/Hexalith.Memories/deploy/kubernetes/base/server-deployment.yaml`; `references/Hexalith.Memories/deploy/openbao/namespace.yaml`
- **Failure scenario:** The first staging deploy with `enforce=restricted` is rejected at admission for every Dapr-enabled workload. Relaxing the namespace label to unblock it silently drops an NFR-3 control.
- **Fix:**
  - Record `dapr_sidecar_injector.sidecarDropALLCapabilities=true` (plus the default `sidecarRunAsNonRoot` and `sidecarReadOnlyRootFilesystem`) in the profile inventory's Dapr control-plane facet.
  - The shared Platform helper emits `dapr.io/sidecar-seccomp-profile-type: RuntimeDefault` or a pod-level seccompProfile, and sets a compliant container securityContext on every generated workload.
  - Add a server-side admission dry-run of the rendered chart against `restricted` to Aspire-to-Helm qualification.
- **Disposition:** autofix

### RV-5 — The Dapr invocation ACL does not cover workflows or actors; the trust domain is self-assigned
- **Severity:** medium
- **Location:** AD-8 ("Hosted Dapr Configurations deny by default and allow callers by trust domain, namespace and app ID"); AD-9 (workflows use Dapr).
- **Claim:** Deny-by-default Configurations isolate callers.
- **Reality:**
  - The Dapr 1.18 docs state that access control lists apply to **service invocation**, and that "Service invocation access control does not cover cross-app workflow scheduling." Workflows use the new `WorkflowAccessPolicy` CRD, which is **open by default** until a policy is loaded.
  - The invoke ACL is not documented as covering actor invocation. EventStore (`AggregateActor`) uses Dapr Actors, and Memories uses `Dapr.Workflow` (`Hexalith.Memories.Server.csproj`, `Hexalith.Memories.EventStore.csproj`).
  - Each app's trust domain is set in that app's own Configuration (default `public`) and becomes its SPIFFE ID. A staging Configuration could claim production's trust-domain string. Only the Sentry-validated namespace and app ID are hard identity, so the spine's triple match (trust domain, namespace, app ID) is necessary and namespace must never be omitted.
- **Evidence:** https://docs.dapr.io/operations/configuration/invoke-allowlist/; https://blog.dapr.io/posts/2026/06/10/dapr-v1.18-is-now-available/; the csproj files listed above.
- **Fix:** Add deny-default `WorkflowAccessPolicy` resources to the application-package Dapr set for every workflow-hosting app. Add NFR-3 negative tests for cross-app and cross-namespace workflow scheduling and for direct actor calls to EventStore actor types from non-`eventstore` apps. Ask EventStore to state its actor-exposure assumption. State that the namespace is the enforced discriminator.
- **Disposition:** autofix (resources and tests); discuss with EventStore (actor exposure)

### RV-6 — The Keycloak admin-event export is assumed but disabled by default, and does not capture every revocation
- **Severity:** medium
- **Location:** AD-6 ("admin events exported off-cluster"); DR sequence step 4 ("re-apply identity revocations from the off-cluster admin-event export").
- **Claim:** An off-cluster admin-event export exists and holds the revocations needed after restore.
- **Reality:**
  - Keycloak stores admin events in its own database only when **Save events** is ON, and needs **Include representation** to replay changes.
  - The built-in `jboss-logging` listener logs admin events, but success events log at `debug` by default.
  - There is no built-in off-cluster export. It must be an Admin REST poll (`/admin/realms/{realm}/admin-events`), log shipping at `info`, or a custom `EventListenerProvider`.
  - The in-DB store shares the database's failure domain and restore point.
  - Revocations initiated by users or the system (user logout or credential change, brute-force lockout) are **user events**, not admin events.
- **Evidence:** https://github.com/keycloak/keycloak/blob/main/docs/documentation/server_admin/topics/events/admin.adoc; https://github.com/keycloak/keycloak/blob/main/services/src/main/java/org/keycloak/events/log/JBossLoggingEventListenerProviderFactory.java (`success-level` default `debug`); https://www.keycloak.org/docs-api/latest/javadocs/org/keycloak/events/EventListenerProvider.html
- **Fix:**
  - The realm contract enables admin events with representation, plus the required user-event types, per hosted realm.
  - Name one export channel to an off-cluster store, with retention at least the backup retention.
  - The DR step consumes both admin and user revocation events.
  - Add the export's freshness to recovery-point metadata.
- **Disposition:** autofix

### RV-7 — GitHub scheduled workflows cannot guarantee the at-most-5-minute probe, and the private-repo minute budget cannot sustain it
- **Severity:** medium
- **Location:** Release and Recovery Acceptance → Recovery point and freshness, and Detection and response; Diagnostics and notification (GitHub is the single notification path).
- **Claim:** An off-site GitHub-hosted monitor, plus a probe at an interval of 5 minutes or less, notifies Administrator through GitHub.
- **Reality:**
  - The `schedule` event is at most every 5 minutes. It "can be delayed during periods of high loads… If the load is sufficiently high enough, some queued jobs may be dropped."
  - Notifications for scheduled runs go to the user who last modified the cron. Other runs notify only the triggering actor, subject to that user's notification settings.
  - A dropped run produces no failure notification.
  - The operations repository is private and the org is on Free: 2,000 included GitHub-hosted minutes per month, with each job rounded up to a whole minute. A 5-minute probe alone is at least 8,640 jobs a month, and the default $0 spending limit then stops private-repo jobs.
  - The earlier REAL-10 defer predates the move to a private repository.
- **Evidence:** https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows; https://docs.github.com/en/actions/concepts/workflows-and-actions/notifications-for-workflow-runs; https://docs.github.com/en/billing/concepts/product-billing/github-actions; https://docs.github.com/en/billing/reference/actions-runner-pricing
- **Fix:**
  - State that the 5-minute probe is not a GitHub `schedule` job; use an off-site monitor host or service that raises GitHub issues, or record the gap as accepted risk.
  - Run the freshness monitor several times an hour at off-hour minutes, with a dead-man check such as the probe asserting the monitor's last-success age.
  - Require notifications through an explicit GitHub issue or mention assigned to Administrator, rather than default run notifications.
  - Budget minutes, or run monitors from a public repository that holds no secrets.
- **Disposition:** discuss

### RV-8 — The infrastructure-currency list is incomplete, and the Kubernetes EOL lands in 30 days
- **Severity:** medium
- **Location:** AD-8 ("Reused infrastructure is suitable only while within upstream support and current on security patches"); Owned work, "Infrastructure currency".
- **Claim:** The currency row covers Kubernetes, OpenBao, Redis Stack and Keycloak "before production qualification."
- **Reality:**
  - Kubernetes 1.34 reaches EOL on **2026-10-27**. By AD-8's own rule the shared cluster, including staging, becomes unsuitable then, not just before production qualification. The upgrade is a shared-infrastructure change under the attempt lock.
  - Observed components missing from the row:

    | Component | Observed | Latest |
    | --- | --- | --- |
    | CloudNativePG operator | 1.30.0 | 1.30.1 (2026-09-23) |
    | PostgreSQL 15 for Keycloak | 15.15 | 15.19 (2026-08-13) |
    | PostgreSQL 18 for Memories telemetry | 18.4 | 18.6 |
    | FalkorDB | 4.12.0 | 4.20.7 (2026-09-24) |
    | Dapr control plane | 1.18.1 | 1.18.4 |

  - OpenBao 2.6.3 and 2.7.0 (2026-09-23), Keycloak 26.7.4 (2026-09-16) and Redis 8.10.2 (2026-09-17) all check out as recorded.
- **Evidence:** https://kubernetes.io/releases/; https://www.postgresql.org/versions.json; https://api.github.com/repos/cloudnative-pg/cloudnative-pg/releases; https://api.github.com/repos/FalkorDB/FalkorDB/releases; https://api.github.com/repos/openbao/openbao/releases; https://api.github.com/repos/keycloak/keycloak/releases; https://api.github.com/repos/redis/redis/releases; memlog L65.
- **Fix:**
  - Add CNPG, both PostgreSQL instances, FalkorDB and the Dapr control-plane patch to the currency row. The row can also point at a profile-inventory currency check instead of listing items.
  - Give the Kubernetes upgrade a date-driven trigger (before 2026-10-27), or record the post-EOL window as an explicit accepted risk for staging.
- **Disposition:** autofix (list); discuss (Kubernetes date)

### RV-9 — Helm 4 version floor and renamed rollback flag
- **Severity:** low
- **Location:** Stack row "Helm 4.x"; AD-3 ("Helm automatic or atomic rollback is forbidden").
- **Reality:**
  - Aspire's Kubernetes pipeline requires Helm 4.2.0 or later; the current release is 4.3.0.
  - Helm 4 renamed `--atomic` to `--rollback-on-failure` and `--force` to `--force-replace`, so a guard that looks for `--atomic` misses the Helm 4 spelling.
  - Helm 4 uses server-side apply for new releases, which affects field ownership of Dapr CRs and keep-policy objects.
- **Evidence:** https://aspire.dev/integrations/compute/kubernetes/; https://helm.sh/docs/overview/; https://api.github.com/repos/helm/helm/releases
- **Fix:**
  - Change the Stack entry to "Helm 4.x, at least 4.2.0 (4.3.0 current)".
  - Change the AD-3 wording to "no `--atomic`/`--rollback-on-failure` or automatic rollback."
  - Add server-side-apply ownership to the Aspire-to-Helm qualification.
- **Disposition:** autofix

### RV-10 — Stack rows overstate alignment and mix sources
- **Severity:** low
- **Location:** Stack.
- **Reality:**
  - **Hexalith.EventStore.Aspire:** the row "3.106.0 — Package-mode pin aligns to the Builds catalog (3.109.0)" reads as satisfied. The working tree pins 3.106.0, below the catalog and in breach of AD-4 until moved.
  - **Aspire.Hosting.Kubernetes:** the row "13.5.4 preview" comes from the Builds catalog (`13.5.4-preview.1.26464.4`), not the working tree the Stack intro cites. The same catalog also pins preview `Aspire.Hosting.Keycloak 13.5.4-preview.1.26464.4`, which any Aspire-hosted local realm would use.
  - **Aspire CLI:** the floor of "at least 13.4.6" is below the 13.5.4 AppHost SDK. Aspire documents CLI/SDK skew warnings and failures, and the installed CLI is 13.5.3.
- **Evidence:** `apphost.cs` L2–7; `references/Hexalith.Builds/Props/Directory.Packages.props` L9, L132–133, L151; `README.md` L26; Aspire docs page "Aspire troubleshooting guide" (aspire.dev slug `aspire-troubleshooting-guide`, section "CLI and SDK version mismatch").
- **Fix:**
  - EventStore.Aspire: "3.106.0 → must move to catalog 3.109.0".
  - Aspire.Hosting.Kubernetes: give the exact preview version and its source, "Builds catalog".
  - Add Aspire.Hosting.Keycloak to the preview/accepted-risk list if it is used.
  - Set the Aspire CLI to "equal to the AppHost SDK (13.5.4)", checked by the Platform tool.
- **Disposition:** autofix

### RV-11 — The memlog cites the wrong source for "Kubernetes publisher emits no Dapr sidecars"
- **Severity:** low
- **Location:** memlog update-run `(version)` entry (Aspire / CommunityToolkit); AD-1 amendment rationale.
- **Reality:** The cited CommunityToolkit v13.1.0 release notes say only that the Dapr integrations were marked preview because of unexplained CI test failures. They say nothing about compute resources, annotations or CRDs. The conclusion is still correct. The pinned `13.5.1-beta.757` and `beta.770` packages contain no Kubernetes or publish code: no `Kubernetes` string, and the only dependency is `Aspire.Hosting 13.5.0`.
- **Evidence:** https://github.com/CommunityToolkit/Aspire/releases/tag/v13.1.0; https://api.nuget.org/v3-flatcontainer/communitytoolkit.aspire.hosting.dapr/13.5.1-beta.770/communitytoolkit.aspire.hosting.dapr.13.5.1-beta.770.nupkg (inspected).
- **Fix:** Replace the citation with the package-inspection evidence.
- **Disposition:** autofix

### RV-12 — aspire.dev documents unreleased 13.6 behavior; exporter capabilities must be pinned to package evidence
- **Severity:** low
- **Location:** AD-1; memlog Aspire `(version)` entries; Owned work, "Aspire-to-Helm qualification".
- **Reality:**
  - aspire.dev already describes features that "require Aspire 13.6 or later" (for example, the automatic pod security context on persistent volumes), while NuGet's latest is 13.5.4. The docs are therefore not version-pinned evidence.
  - Package inspection confirms that the pinned preview has the callback, `AdditionalResources` and the typed pod and securityContext model AD-1 relies on.
  - Not verified:
    - that generated `values.yaml` can carry digest-pinned image references;
    - that 13.5.4 already enforces the Helm 4.2.0 floor.
- **Evidence:** https://aspire.dev/deployment/kubernetes/persistent-volumes/ (section "Default pod security context"); nupkg `aspire.hosting.kubernetes.13.5.4-preview.1.26464.4` (`lib/net8.0/Aspire.Hosting.Kubernetes.xml`).
- **Fix:** Record the package-API evidence in the memlog. Add "digest-pinned image references via values, without chart regeneration" to the Aspire-to-Helm qualification row.
- **Disposition:** autofix

### RV-13 — Executor runner update and version monitoring is absent from the spine
- **Severity:** low
- **Location:** AD-7; Owned work, "Release state, checks and notifications".
- **Reality:** A self-hosted runner that has not installed a release within 30 days stops receiving jobs, and minimum-version enforcement has resumed in 2026. The memlog keeps "runner update monitoring" as seed only. Automatic recovery depends on the production executor being current.
- **Evidence:** https://docs.github.com/en/actions/reference/runners/self-hosted-runners; https://github.blog/changelog/2026-06-12-github-actions-minimum-version-enforcement-timeline-for-self-hosted-runners/
- **Fix:** Add "executor runner update and version monitoring" to the owned-work row before automated promotion.
- **Disposition:** defer

### RV-14 — The brownfield OpenBao token contradicts the per-app token rule and has a hard expiry
- **Severity:** low
- **Location:** Consistency Conventions → Secrets ("per-app tokens … with a named renewal owner").
- **Reality:**
  - The mechanism is verified (Dapr Vault component against OpenBao).
  - The existing Memories component uses one token from `openbao-runtime-bootstrap` shared by the scopes `eventstore` and `memories`.
  - Memories operations docs record the operator and Dapr token expiry as `2027-07-19 13:41:25 UTC`.
  - Dapr's component documents no token renewal.
- **Evidence:** `references/Hexalith.Memories/deploy/kubernetes/base/dapr/secretstore.yaml`; `references/Hexalith.Memories/docs/operations/openbao.md` L280; https://docs.dapr.io/reference/components-reference/supported-secret-stores/hashicorp-vault/
- **Fix:** Add both facts to the existing-instance classification item: the shared token must be split per app, and the renewal owner must act before 2027-07-19.
- **Disposition:** defer

## Stack rows against the working tree

| Row | Spine | Working tree / catalog | Upstream latest (2026-09-27) | Verdict |
| --- | --- | --- | --- | --- |
| .NET SDK | 10.0.401, latestPatch | `global.json` 10.0.401 / latestPatch | 10.0.401 | OK |
| Aspire AppHost SDK, Docker, Redis | 13.5.4 | `apphost.cs` 13.5.4 | 13.5.4 | OK |
| Aspire CLI | at least 13.4.6 | README at least 13.4.6; installed 13.5.3 | 13.5.4 | RV-10 |
| CommunityToolkit Aspire Dapr | 13.5.1-beta.757 | `apphost.cs` beta.757; catalog beta.770 | beta.770 (no stable after 13.0.0) | OK (gap labelled) |
| Hexalith.EventStore.Aspire | 3.106.0 | `apphost.cs` 3.106.0; catalog 3.109.0 | 3.109.0 | RV-10 |
| Aspire.Hosting.Kubernetes | 13.5.4 preview | catalog only, `13.5.4-preview.1.26464.4` | same | RV-10 (source) |
| Helm | 4.x | not in tree | 4.3.0 | RV-9 |
| Dapr runtime | 1.18, skew 1.18.1–1.18.4 | `DaprSelfHostedMtls.cs` 1.18.3; Builds CI runtime 1.18.2, CLI 1.18.0; catalog SDK 1.18.10 | 1.18.4; CLI 1.18.2 | OK (skew labelled) |

## Checked and clean

- Dapr's `secretstores.hashicorp.vault` is the documented, OpenBao-tested path. It is token-only, which matches the spine's bootstrap-exception wording.
- Helm 4 installs by OCI manifest digest and rejects non-matching digests, so AD-2's identity model is sound.
- The Aspire Kubernetes pinned package can emit Dapr CRs (`BaseKubernetesResource` via `AdditionalResources`) and pod annotations through `PublishAsKubernetesService`. The AD-1 helper approach is feasible, subject to RV-4 and RV-12.
- Dapr accessControl fields and the SPIFFE ID format match the spine for service invocation.
- ValidatingAdmissionPolicy (stable since 1.30) supports the hostname-admission rule.
- GitHub Actions OIDC exposes `job_workflow_ref`, `ref`, `environment`, `event_name`, `repository_visibility` and `runner_environment` on all plans. This makes it a plan-independent way to enforce AD-7 (RV-1 option b).
- Kubernetes 1.34 EOL, OpenBao 2.6.3/2.7.0, Redis Stack end of maintenance and Keycloak 26.7.4 all match the memlog's update-run entries.
