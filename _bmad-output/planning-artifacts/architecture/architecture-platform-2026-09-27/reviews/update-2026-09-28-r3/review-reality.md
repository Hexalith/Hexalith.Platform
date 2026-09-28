# Reality / version review — update run 3 (2026-09-28)

Lens: every committed decision web-researched or reality-checked (current versions, named technologies exist and fit, live defaults). Scope: `ARCHITECTURE-SPINE.md` (485 lines, read in full), `reviews/update-2026-09-28-r3/spine.diff`, `.memlog.md` from "Update run 3 started" plus prior `(version)` entries. Settled decisions and accepted risks are not reopened. Read-only: web (WebFetch/WebSearch), NuGet/GitHub APIs, the local repo, and `kubectl get` on the Dapr CRDs and Configurations (15 s request timeout, no Secrets, no mutation). All checks were run on 2026-09-28.

**Verdict: PASS with minor findings.** Every technology behavior the r3 edits rely on exists and works as the spine states. There is one medium finding: the takeover-fencing invariant fails closed, but on the AD-7 GitHub-OIDC credential path it can never take its non-intervention branch. The remaining findings are low-severity precision and wording fixes.

Counts: critical 0, high 0, medium 1, low 6.

## Verified claims (pass)

| # | Spine claim (line) | Result | Source (checked 2026-09-28) |
| --- | --- | --- | --- |
| P1 | Dapr 1.18: with HotReload off, a change to a Component, Configuration, Resiliency, Subscription, WorkflowAccessPolicy, HTTPEndpoint or MCPServer needs a sidecar restart (L142, L249) | **Confirmed.** In 1.18 all seven kinds are hot-reloaded by default. The opt-out is `spec.features: [{name: HotReload, enabled: false}]` in the Configuration, which restores the "restart required" behavior. On Kubernetes that means a rollout restart. With HotReload on, Configuration, Resiliency, WorkflowAccessPolicy and HTTPEndpoint changes already force a graceful sidecar restart. Components and Subscriptions reload in place. | https://docs.dapr.io/operations/components/component-updates/ ; https://blog.dapr.io/posts/2026/06/10/dapr-v1.18-is-now-available/ |
| P2 | MCPServer is a real Dapr resource kind (L138, L215, L243, L247, L249) | **Confirmed.** `dapr.io/v1alpha1` `MCPServer` is new in 1.18 and GA, with its feature gate removed before GA. It is namespaced, supports `scopes`, carries `secretKeyRef`/OAuth2 auth and is hot-reloadable by default. The live cluster (control plane 1.18.1) serves `mcpservers.dapr.io` and `workflowaccesspolicies.dapr.io`. The component-updates page does not list MCPServer, but the release post and the MCPServer page both do. | https://docs.dapr.io/developing-ai/mcp/mcp-server-resource/ ; blog above; `kubectl get crd` |
| P3 | Hosted Configurations disable HotReload (L142, L465, L467) | **Feasible, with live precedent.** `hexalith-memories/memories-config` and `memories-access-telemetry-config` already set `HotReload enabled:false`. `dapr-system/daprsystem`, `memories-mcp-config` and `memories-access-telemetry-clock-config` do not. | `kubectl get configurations.dapr.io -A` |
| P4 | WorkflowAccessPolicy semantics (L142) | **Confirmed.** With no policy loaded for a target app, calls are open. Once any policy is loaded, the target defaults to deny. Rules match callers by app ID, and self-calls are always allowed. | https://docs.dapr.io/developing-applications/building-blocks/workflow/workflow-multi-app/ ; blog above |
| P5 | Secret-bound TokenRequest tokens can be revoked by deleting the Secret (memlog VAL-02 seed) | **Confirmed**, with timing caveats (RV3-2). Pod, Secret, Node and webhook-configuration bindings exist. A token fails once the bound object is gone, or from 60 s after `deletionTimestamp` while deletion is pending. `expirationSeconds` must be at least 600 s. `--service-account-lookup` defaults to true. | https://kubernetes.io/docs/reference/access-authn-authz/service-accounts-admin/ ; https://kubernetes.io/docs/reference/kubernetes-api/authentication-resources/token-request-v1/ ; https://kubernetes.io/docs/reference/command-line-tools-reference/kube-apiserver/ |
| P6 | Keycloak standard token exchange: surface from `azp`, target in `aud` (L202, L205, L442) | **Confirmed** for 26.7.4, the latest release (2026-09-16). The exchanged token's `azp` is the requester client. Its `aud` comes from the requester's client scopes and role mappings, and the `audience` parameter can only filter it down, never add. The subject token must carry the requester in `aud`, and the requester must be a confidential client. Enablement is a per-client "Standard token exchange" switch. Refresh tokens are gated by "Allow refresh token in Standard Token Exchange". V2 does not use fine-grained admin permissions (FGAP). The `downscope-assertion-grant-enforcer` executor exists. V1 is deprecated. So a chained token's `azp` is the calling module's `service` client, which matches AD-14's "most restrictive" rule. | https://www.keycloak.org/securing-apps/token-exchange ; https://github.com/keycloak/keycloak/blob/main/docs/guides/securing-apps/token-exchange.adoc ; https://www.keycloak.org/2026/09/keycloak-2674-released |
| P7 | A CAS-capable record store for the lock, epoch and promotion-stop revision (L225, L287, L448) | **Feasible on the existing off-site provider.** Scaleway Object Storage supports `If-Match` and `If-None-Match` conditional PutObject. | https://www.scaleway.com/en/docs/object-storage/api-cli/using-conditional-writes/ |
| P8 | Helm 4 `--rollback-on-failure` is forbidden (L89) | **Confirmed.** The flag exists in Helm 4.3.0. | https://helm.sh/docs/helm/helm_upgrade/ |
| P9 | Stack currency (L344–L352) | .NET SDK 10.0.401 is the latest (runtime 10.0.12, 2026-09-08). Aspire AppHost SDK, CLI, Docker and Redis 13.5.4 are the latest stable. Aspire.Hosting.Kubernetes and Keycloak latest are 13.5.4-preview.1.26464.4. The CommunityToolkit Dapr latest is 13.5.1-beta.806 (seed beta.757, catalog beta.770). EventStore.Aspire 3.109.0 is the latest and depends on toolkit beta.767, which matches the row. Helm latest is 4.3.0 (2026-09-09), so the 4.2.0 floor holds. Dapr latest is 1.18.4 (2026-09-09), with no 1.19. Kubernetes 1.34 reaches EOL on 2026-10-27; its latest patch is 1.34.11 and the cluster runs 1.34.9. Supported minors are 1.35–1.37. | NuGet flatcontainer; https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json ; GitHub releases API (dapr/dapr, helm/helm); https://kubernetes.io/releases/ |

## Findings

### RV3-1 — Kubernetes/Helm has no authority-side epoch check, and GitHub-OIDC Kubernetes credentials cannot be revoked
- **Severity:** medium. **Action:** discuss. **Confidence:** high.
- **Evidence:** L282: the new owner "relies on an authority-side epoch or generation check, or revokes the previous job's per-job credentials at their issuer … otherwise it stops for intervention". L128: per-job credentials come "through GitHub OIDC claim checks or executor-held credentials". L472 lists "takeover fencing or revoke-and-drain at every mutation authority" as acceptance.
- **Reality:**
  - Helm 4.3.0 `upgrade` has no flag that conditions an upgrade on an expected revision, resourceVersion or precondition.
  - Kubernetes offers optimistic concurrency only per object, through a PUT carrying `resourceVersion` (409 Conflict). Patch and server-side apply writes are unconditional by default.
  - So for the Kubernetes authority, which covers the application, environment-layer and shared-infrastructure targets, the only branch available without custom plumbing is revoke-and-drain. Memlog VAL-02 rejected the ValidatingAdmissionPolicy epoch check.
  - Kubernetes' own authentication docs say an OIDC `id_token` "can't be revoked, it's like a certificate". A GitHub Actions OIDC token presented to the API server through a JWT authenticator therefore cannot be revoked at its issuer.
- **Consequence:** the invariant still fails closed. On the OIDC path, though, every takeover stops for intervention. That silently disables "a replacement job of the same workflow resuming the same attempt within the grace to perform its remaining single recovery" (L282), and the one automatic recovery after an executor or job crash depends on that path.
- **Minimal correction:** in AD-7 *Credentials*, require every per-job credential that can mutate a lock-covered authority to be revocable at its issuer, such as a Secret-bound TokenRequest token or an OpenBao token, unless that authority offers a compare-and-set or generation check. GitHub OIDC may be used only to obtain such a credential, never as the Kubernetes bearer. The alternative is to record that the OIDC path always stops for intervention on takeover.

### RV3-2 — Revoke-and-drain timing on Kubernetes is longer than "Secret deleted, request ends"
- **Severity:** low. **Action:** defer, as a mechanism note for the First production attempt acceptance (L472). **Confidence:** high.
- **Evidence:** L282 says "waits the declared maximum in-flight request duration". Memlog VAL-02 evidence says "once the bound object no longer exists the request is not authenticated".
- **Reality:** revocation does not take effect the moment the Secret is deleted:
  - kube-apiserver caches successful token authentications for 10 s by default (`TokenSuccessCacheTTL: 10 * time.Second`, `pkg/kubeapiserver/options/authentication.go`).
  - A bound object pending deletion keeps authenticating tokens until 60 s after `deletionTimestamp`.
  - Non-long-running requests are bounded by `--request-timeout`, which defaults to 1m0s. Watches use `--min-request-timeout` of 1800 s or more, but they are read-only.
- **Consequence:** a drain declared as request duration alone leaves a window of about 10 s, or up to 60 s when finalizers are present, in which a stale job can start a new mutating request after "revocation".
- **Minimal correction:** declare the Kubernetes drain as three parts:
  1. confirm the bound Secret is absent, not merely that a delete was requested;
  2. then wait for the token success-cache TTL;
  3. then wait for the API server `--request-timeout`.

  Read both flag values from the actual cluster and record them in the profile inventory.

### RV3-3 — The catalog authority's "generation check" is not a confirmed EventStore capability
- **Severity:** low. **Action:** discuss (EventStore confirmation). **Confidence:** medium.
- **Evidence:** memlog VAL-02 names "catalog commit conditioned on the expected generation" as an authority-side fence. L282 relies on such checks. EventStore's architecture (`references/Hexalith.EventStore/_bmad-output/planning-artifacts/architecture.md`, Activation paragraph) specifies prepare/ready/commit by the deployment owner and says nothing about an expected-generation compare-and-set. First shared versions (L438) and the G3 EventStore confirmations (L472) do not list one either.
- **Consequence:** the catalog would fall back to revoke-and-drain of whatever credential commits it (see RV3-1). Otherwise takeover stops at the catalog step.
- **Minimal correction:** add "catalog commit conditioned on the expected generation" to the Routing catalog row of First shared versions or to the EventStore G3 confirmations. The alternative is to state that catalog commits go through the Kubernetes authority and inherit its drain.

### RV3-4 — Stack row for Aspire.Hosting.Keycloak does not match the seed's actual transitive version
- **Severity:** low. **Action:** autofix. **Confidence:** high.
- **Evidence:** L350: "Aspire.Hosting.Keycloak | 13.5.4-preview.1.26464.4 | Prerelease, transitive via EventStore.Aspire". The seed pins `Hexalith.EventStore.Aspire@3.106.0` (L348, `apphost.cs`). Its nuspec depends on `Aspire.Hosting.Keycloak 13.5.3-preview.1.26425.3`, `Aspire.Hosting 13.5.3` and toolkit `beta.752`. The root apphost restore (`~/.local/share/dotnet/runfile/apphost-…/obj/project.assets.json`, 2026-09-27 12:41) resolves Keycloak hosting to **13.5.3-preview.1.26425.3**. The 13.5.4 preview is the Builds catalog pin (`Props/Directory.Packages.props` L132) and the dependency of EventStore.Aspire 3.109.0.
- **Consequence:** the table misstates an existing pin, which matters for its "prerelease pins as accepted risk" traceability.
- **Minimal correction:** change the row to "13.5.3-preview.1.26425.3 (transitive via EventStore.Aspire 3.106.0); catalog 13.5.4-preview.1.26464.4". A related minor point: the installed Aspire CLI is 13.5.3. The L346 row describes the target rule, and L459 already owns the README floor.

### RV3-5 — "Prerelease … Dapr WorkflowAccessPolicy (v1alpha1)" misreads the apiVersion
- **Severity:** low. **Action:** autofix (wording only; the risk stays accepted). **Confidence:** high.
- **Evidence:** L485 accepted risks. The live cluster serves every Dapr CRD at `v1alpha1` only, including components, configurations, resiliencies, httpendpoints, mcpservers and workflowaccesspolicies. Subscriptions are served at v1alpha1 and v2alpha1. The Dapr 1.18 release post calls WorkflowAccessPolicy and MCPServer GA.
- **Consequence:** readers infer an instability that upstream does not claim. The same logic would label Components and Configurations prerelease.
- **Minimal correction:** reword to "Dapr WorkflowAccessPolicy and MCPServer, both new in 1.18", or drop "(v1alpha1)".

### RV3-6 — Dapr activation should name referenced Kubernetes Secret values
- **Severity:** low. **Action:** autofix. **Confidence:** medium.
- **Evidence:** L249 names resource kinds only. The component-updates page, in its hot-reload section, says a Component that references a Kubernetes Secret through `secretKeyRef` re-initializes within 60 s without a pod restart. The spine turns HotReload off.
- **Consequence:** a bootstrap Secret changed in the environment layer (L243) may leave consuming sidecars on stale values until restarted. Dapr activation as worded would not trigger that restart. The Secrets row (L228) covers only per-app OpenBao tokens.
- **Minimal correction:** in Dapr activation, change "effective content or binding" to "effective content, including referenced Kubernetes Secret values, or binding".

### RV3-7 — The downscope enforcer constrains how cross-module audiences can be modelled
- **Severity:** low. **Action:** defer, as a realm-contract qualification note for L442. **Confidence:** medium.
- **Evidence:** L442 requires the `downscope-assertion-grant-enforcer` on every requester. L205 says exchange is "permitted per requester and target-audience pair".
- **Reality:** the enforcer allows only scopes already in the subject token's `scope` claim. The one exception is default scopes configured with *include in token scope* = false, such as `roles`, which carries the audience-resolve mapper. V2 has no explicit requester-to-audience permission object: a pair is permitted only through the requester's client scopes and role scope mappings (full scope off).
- **Consequence:** suppose the contract models a target audience as an optional client scope on the requester. The exchange would then fail unless the originating UI or McpCli token already carried that scope, which widens the origin token and defeats least privilege.
- **Minimal correction:** in the realm-contract row, state that cross-module target audiences come from the requester's role scope mappings (audience resolve), not from optional scopes. Add one staging exchange test with the enforcer on.

## Not re-checked (settled or unchanged since prior dated evidence)

These carry forward from memlog `(version)` entries dated 2026-09-27 and 2026-09-28: the Aspire.Hosting.Kubernetes `AddGateway` and parent-reference behavior, Traefik 3.7 Gateway API v1.6.1, cert-manager v1.21.2 Gateway API, the Zot GC and anonymous-read settings, the Dapr vault token read-once behavior, GitHub Free plan limits and the ingress-nginx retirement. The r3 edits do not depend on any of them.
