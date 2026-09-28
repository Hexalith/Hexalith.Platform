# Reality and version review: update run 2 (2026-09-28)

**Verdict: CONDITIONAL PASS.** Every version and technology named in the spine exists, is current or tracked, and fits. However, two high findings rest on live facts the spine contradicts: the GitHub organization's owner and base-permission settings, and cert-manager's HTTP-01 solver-route namespace conflicting with the Gateway `allowedRoutes` rule. Seven medium findings are gaps in how the spine's mechanisms work in the tools as actually installed. None requires reversing an adopted decision. RV-1 and RV-2 need a user choice.

| Severity | Count |
| --- | --- |
| Critical | 0 |
| High | 2 |
| Medium | 7 |
| Low | 6 |
| **Total** | **15** |

Scope: the claims this update introduced, plus the eight areas the parent listed. Memlog `(version)` entries were not re-done except where noted. The live cluster was checked read-only (`kubectl --context jpiquot@local --request-timeout=15s`: metadata, specs, args and ConfigMaps only; no Secret values were read and nothing was mutated). GitHub state came from read-only `gh api` calls. Public endpoints were checked with anonymous GET or HEAD requests.

---

## Findings

### RV-1: A second organization owner and a `write` base permission break "writable only by Administrator" and "Administrator-only bypass"

- **Severity:** high
- **Location:** AD-7 *Triggers*; Workflows and provenance (ruleset sentence); Accepted risks; Owned work "Trigger | GitHub Team controls".
- **Claims:**
  - AD-7: "Deployment workflows live in a private operations repository writable only by Administrator … no PAT, App or deploy key outside Administrator's devices can write it."
  - Workflows and provenance: "Administrator-only bypass … and Builds bypass is Administrator-only."
  - Owned work: "once a second person gains write access to the operations repository."
- **Evidence:**
  - `gh api orgs/Hexalith/members?role=admin` returns `jpiquot` and `tinouit`, so the organization has two owners.
  - `gh api orgs/Hexalith` returns `default_repository_permission: "write"`.
  - On the existing private repository `Hexalith.GitDocumentStorage`, `tinouit` has `admin` and `QuentinDV` has `write` (neither is a direct collaborator; the access comes from ownership and the base permission).
  - On `Hexalith.Platform` and `Hexalith.Builds`, the admin collaborators are `jpiquot` and `tinouit`.
  - The Builds ruleset `Protect main` has bypass actors `OrganizationAdmin` (always) and User 148322258, which is `QuentinDV` (always).
  - GitHub docs: "People with admin access to a repository … can create, edit, and delete rulesets" (https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/about-rulesets). Owners have "complete administrative access to your organization" (https://docs.github.com/en/organizations/managing-peoples-access-to-your-organization-with-roles/roles-in-an-organization).
  - Consequence: a private operations or notification repository created in the Hexalith organization would be writable by every member and administrable by the second owner from day one. That owner can also edit or delete the Platform and Builds rulesets, so "Administrator-only bypass" is nominal.
  - The Platform-repository side of this was raised in r2 (RV-1 there). The operations-repository side was not.
- **Proposed fix:** Add to AD-7 *Triggers*: "The operations and notification repositories live in an account or organization whose only owner is Administrator, with no base repository permission." Replace the accepted-risk clause with: "GitHub Free with repository-level rulesets only; every other owner or admin of the publication repositories can edit their rulesets and is a named writer; the operations repository is single-writer." Change the Owned work trigger to "once anyone other than Administrator can write or administer the operations repository."
- **Disposition:** discuss. This refines the settled RV-1 (stay on GitHub Free, single-writer operations repository) and C-22 decisions. Options: (a) host the operations and notification repositories outside the Hexalith organization (recommended; cheapest); (b) demote `tinouit` to member and set the base permission to `none`/`read`; (c) accept the second owner and record them as a named writer.

### RV-2: The HTTP-01 solver route lands in a namespace the Gateway rule rejects

- **Severity:** high
- **Location:** Hosted interfaces ("admits routes only from its application namespace"; "Staging … obtains certificates only through HTTP-01"); AD-8 *Namespaces* (the environment-layer identity writes only `components.dapr.io` objects and named bootstrap Secrets in the application namespace); Release tiers (the environment's Gateway is environment layer).
- **Claim:** "Each environment's Gateway, Gateway API on Traefik, lists only that environment's hostnames and admits routes only from its application namespace."
- **Evidence:**
  - cert-manager documentation (https://cert-manager.io/docs/configuration/acme/http01/, raw source `content/docs/configuration/acme/http01/README.md`): the `gatewayHTTPRoute` solver "creates a temporary HTTPRoute". Its example shows the HTTPRoute in the Certificate's namespace, and it says an issuer "may reference a Gateway that is on a separate namespace, as long as the Gateway's port 80 listener is configured with `from: All`". It also says "cert-manager does not edit Gateway resources".
  - Why the conflict arises:
    - The Gateway's TLS Secret must sit in the Gateway's namespace (or be reached through a ReferenceGrant).
    - Under AD-8 the Gateway cannot live in the application namespace, so it sits in the environment-layer (data) namespace.
    - Its Certificate, and therefore the solver HTTPRoute, then sit in a namespace that the "application namespace only" rule rejects.
  - As written, staging's only certificate path (HTTP-01) cannot complete.
- **Proposed fix:** Replace the sentence in Hosted interfaces with: "Each environment's Gateway, Gateway API on Traefik, lives in its data namespace and lists only that environment's hostnames as exact FQDNs. Its HTTPS listener admits routes only from the application namespace; its HTTP listener also admits cert-manager HTTP-01 solver routes from the Gateway's own namespace."
- **Disposition:** discuss. This adds a narrow exception to the C-23 wording the user accepted. The alternative is to put Certificates in the application namespace with a ReferenceGrant, which widens the environment-layer identity's scope and exposes the TLS key to the application deploy identity. The recommended fix is the one above.

### RV-3: cert-manager Gateway API support is off on the live cluster, and no owned work enables it

- **Severity:** medium
- **Location:** Owned work, First staging deployment, "Secrets, identity, network and transport" ("Gateway API CRDs and the Traefik Gateway provider; hostname admission and staging HTTP-01").
- **Claim:** The listed work suffices for staging HTTP-01 through Gateway API.
- **Evidence:**
  - Live cert-manager is `v1.21.2`, the current 1.21 patch (https://github.com/cert-manager/cert-manager/releases). The controller args carry no Gateway API setting, and there is no config ConfigMap in `cert-manager`.
  - Both ACME ClusterIssuers (`letsencrypt-prod-http01`, `letsencrypt-staging-http01`) solve only through `http01.ingress.ingressClassName: nginx-public`.
  - cert-manager docs: "you still need to enable the Gateway API support … `config.gatewayAPI.enabled: true` … The Gateway API CRDs should either be installed before cert-manager starts or the cert-manager Deployment should be restarted after installing the Gateway API CRDs." There are no `gateway.networking.k8s.io` CRDs on the cluster today.
- **Proposed fix:** Change that row's clause to: "Gateway API CRDs, the Traefik Gateway provider and cert-manager Gateway API support (`config.gatewayAPI.enabled`, restarted after the CRDs), with per-environment `gatewayHTTPRoute` issuers replacing the `nginx-public` Ingress solvers; hostname admission and staging HTTP-01".
- **Disposition:** autofix.

### RV-4: Traefik also serves Ingress and IngressRoute from every namespace, so `allowedRoutes` alone does not bound an application namespace's hostnames

- **Severity:** medium
- **Location:** Hosted interfaces ("Application routes may set only host, path, TLS and backend fields"); AD-8 *Negative tests* (hostnames).
- **Claim:** The Gateway's hostname list and `allowedRoutes` confine each environment's hostnames.
- **Evidence:**
  - The live Traefik DaemonSet (image digest `24841fe2…`, the same digest as the `traefik:v3.7.13` relay) runs `--providers.kubernetescrd` with no namespace restriction.
  - It also runs `--providers.kubernetesingressnginx` with `ingressClass=nginx-public` and `watchIngressWithoutClass=false`.
  - Current Ingresses on that class include `registry.hexalith.com`, `kube.hexalith.com` and `auth.tache.ai`. The shared `hexalith.com` zone also carries `mail.hexalith.com` through an IngressRoute.
  - Traefik docs: "routes are sorted, by default, in descending order using rules length … the longest length has the highest priority"; ties across providers go to `providers.precedence` (https://doc.traefik.io/traefik/reference/routing-configuration/http/routing/rules-and-priority/).
  - So an Ingress, IngressRoute or HTTPRoute in a staging namespace with a longer rule for a shared host (for example `Host(registry.hexalith.com) && PathPrefix(/v2/)`) would take traffic from the shared Ingress. A wildcard `*.hexalith.com` listener would admit such an HTTPRoute.
- **Proposed fix:** Append to Hosted interfaces: "Application namespaces hold only Gateway API routes: the application deploy identity cannot create Ingress, `traefik.io` or `hub.traefik.io` objects, because the shared Traefik also serves Ingress and IngressRoute from every namespace. Listener hostnames are exact FQDNs, never wildcards over a zone that carries shared names."
- **Disposition:** autofix.

### RV-5: Aspire 13.5.4's Gateway model cannot emit routes that attach to an environment-layer Gateway

- **Severity:** medium
- **Location:** AD-1 *Chart* ("the generated application chart, which carries workloads, services and Gateway API routes"); Release tiers (the Gateway is environment layer); Owned work "Aspire-to-Helm qualification".
- **Claim:** The generated chart carries Gateway API routes, while the Gateway stays in the environment layer.
- **Evidence:** Package inspection of `~/.nuget/packages/aspire.hosting.kubernetes/13.5.4-preview.1.26464.4/lib/net8.0/Aspire.Hosting.Kubernetes.xml` shows:
  - `AddGateway` "generates a gateway.networking.k8s.io/v1 Gateway resource and one or more HTTPRoute resources in the Helm chart output".
  - `KubernetesGatewayResource.ShouldMaterialize`: "A gateway with no routes is skipped".
  - `HttpRouteParentRefV1` exposes only `Name` (no `Namespace`), so the typed route can only attach to a Gateway in its own namespace.
  - `GatewayRouteNamespacesV1` exposes only `From`, with no selector.
  - The typed model therefore always puts a Gateway in the application chart and cannot reference the environment's Gateway in another namespace.
  - `aspire deploy` also patches Gateway listeners and creates bootstrap TLS Secrets (`DiscoverFqdnAndBootstrapTlsAsync`, `EnsureBootstrapTlsSecretAsync`). That path is unused, because promotion never runs `aspire deploy`.
- **Proposed fix:** In the Aspire-to-Helm qualification row, replace "Gateway API routes" with "Gateway API HTTPRoutes emitted by the shared helper with a namespaced parentRef to the environment's Gateway, and no Gateway in the chart (13.5.4's `AddGateway` materializes its own Gateway and names parents without a namespace)".
- **Disposition:** autofix. Emitting the routes as `BaseKubernetesResource` additional resources is ordinary helper work, not an AD-1 fallback trigger.

### RV-6: Aspire connection-string parameters are always secret and render into a chart Secret

- **Severity:** medium
- **Location:** AD-1 *Chart* ("Hosted data services become external connection parameters carrying only non-secret endpoints … the rendered chart holds no credential-bearing Secret or value"); Owned work "Aspire-to-Helm qualification".
- **Claim:** Modelling hosted data services as external connection parameters keeps secrets out of the chart.
- **Evidence:**
  - Aspire source, `release/13.5` branch, `src/Aspire.Hosting/ConnectionStringParameterResource.cs`: `: base(name, callback, secret: true)`. `AddConnectionString` wraps this resource (`ParameterResourceBuilderExtensions.cs` L322-334).
  - The Kubernetes publisher DLL strings include `.Values.secrets.` and `-secrets`, and the XML docs describe `KubernetesResource.Secret` and `AllocateBranchParameters` ("allocated in the appropriate dictionary (EnvironmentVariables or Secrets) so their values flow to values.yaml").
  - Result: the idiomatic `AddConnectionString` renders a Kubernetes Secret plus a `secrets:` values key, even when the string holds only an endpoint.
- **Proposed fix:**
  - AD-1 *Chart*: "Hosted data services become non-secret parameters (not Aspire connection-string resources, which are always secret) carrying only endpoints; … and the rendered chart contains no Secret object."
  - Qualification row: replace "no credential-bearing Secret or value in the rendered chart" with "no Secret object or secret value in the rendered chart".
- **Disposition:** autofix.

### RV-7: Keycloak standard token exchange allows upscoping by default, so the preconditions list misses the downscope enforcer

- **Severity:** medium
- **Location:** AD-14 *Chains* ("permitted per requester and target-audience pair and downscoped to the target's declared operations"); Deferred "Realm contract … token-exchange permissions and preconditions (requester in the subject token audience, per-client standard-exchange switch, refresh-token setting)".
- **Claim:** The three listed preconditions plus pair permission give downscoped exchange.
- **Evidence:** Keycloak 26.7.4 token-exchange guide (https://www.keycloak.org/securing-apps/token-exchange) confirms the three listed items:
  - "you also need to enable the Standard token exchange switch for the client";
  - "requester-client must be a confidential client";
  - "Verification that the requester client must be in the audience of the subject_token";
  - refresh tokens are issued only with "Allow refresh token in Standard Token Exchange".

  It also says:
  - "By default, token exchange can be used to request extra scopes and audiences that are not present in the initial subject_token. If … you want to ensure that scopes are limited to the ones already granted to the subject_token, the downscope-assertion-grant-enforcer policy executor can be applied to the client."
  - The `audience` parameter "will not add more audiences". It filters the audiences available from the requester's client scopes.
  - "Fine-grained admin permissions (FGAP) are not needed for the standard token exchange." So a per-pair permission is expressed through the requester's client scopes and audiences, which depends on Full scope allowed being off.
- **Proposed fix:** Change the Deferred row parenthetical to: "(requester in the subject token audience, per-client standard-exchange switch, refresh-token setting, the `downscope-assertion-grant-enforcer` client policy on every requester, and requester audiences limited by client scopes with full scope off)".
- **Disposition:** autofix. This adds to C-58 without reversing it.

### RV-8: `hexalith-module` and `hexalith-evidence` are already published; the Builds README and the "first publication" gate are stale

- **Severity:** medium
- **Location:** Owned work "First tool publication | Platform tool ratification"; First shared versions "Declaration schema … Must precede: First `hexalith-module` publication or pin"; Source row "Builds README"; memlog C-02 ("Verified 2026-09-28: Builds README states hexalith-module/hexalith-evidence are unpublished").
- **Claim:** The tools are unpublished, and ratification precedes their first publication.
- **Evidence:**
  - NuGet registration `api.nuget.org/v3/registration5-gz-semver2/hexalith.builds.module.cli/index.json`: 11 listed versions, from 4.20.0 (2026-07-17) to 4.27.4 (2026-09-19). The package type is `DotnetTool`, built from `Hexalith.Builds` main at commit `410bd595`.
  - `hexalith.builds.evidence.cli`: 20 versions, the latest 4.27.4 (2026-09-19).
  - `410bd595` is an ancestor of the pinned `0610f78` (51 commits earlier). The README at `0610f78` (L226-229) still says "consumers must not invent a `4.20.0` pin".
  - The hard-coded consts remain: `src/libraries/Hexalith.Builds.Tooling/Manifest/SupportedPlatformPins.cs` has `EventStoreVersion = "3.109.0"`, `DaprRuntimeVersion = "1.18.2"` and `DaprSdkVersion = "1.18.10"`.
  - No `references/*/.config/dotnet-tools.json` pins either tool yet.
  - The rest of the README claims hold: the `run`/`down`/`test` commands, `hexalith.module-manifest.v1`, and `Tools/runtime-toolchain-baseline.json`.
- **Proposed fix:**
  - Rename the Owned work gate to "First Platform-accepted tool version" and add: "Versions through 4.27.4, already on NuGet.org, predate ratification and are not Platform-accepted; no workspace pins them."
  - Change the First shared versions "Must precede" cell to "First Platform-accepted `hexalith-module` version or any workspace pin".
  - Add "correct the README's unpublished statement" to the tool-ratification acceptance.
- **Disposition:** autofix. This corrects C-02's premise; the intent (ratify before adoption) is unchanged. The parent may escalate it if it treats the gate rename as reopening C-02.

### RV-9: Registry garbage collection deletes untagged manifests by default, and the spine has no registry-side retention mechanism

- **Severity:** medium
- **Location:** AD-2 *Retention*; Workflows and provenance ("publication credentials cannot delete or overwrite retained artifacts").
- **Claim:** Packages and digests are retained for the backup retention period or their life as a rollback target.
- **Evidence:**
  - Live `registry-distribution/distribution-registry-config` (Zot, serving `registry.hexalith.com`): `"gc": true, "gcDelay": "24h"`, with no `retention` block.
  - Zot docs (https://zotregistry.dev/v2.1.21/articles/retention/): "By default, if no retention policies are defined, all untagged manifests are deleted, unless they are referenced by indexes or artifacts"; `deleteUntagged` "Default is true".
  - A retained digest therefore survives only while some tag points at it. The live `adminPolicy` and `**` policy grant `update` and `delete` to the admin user.
  - Zot separates `create` from `update`, so a create-only writer cannot move a tag. The `latest` exemption was fixed in 2.1.15 (CVE-2026-31801, GHSA-85jx-fm8m-x8c6), and live Zot is 2.1.20, so the fix applies.
  - Current tags are unique versions (`eventstore` has 189 tags, none `latest`), so the immediate risk is low but not prevented.
  - validate-2026-09-27 operability raised this scenario without live evidence. The spine text did not absorb a mechanism.
- **Proposed fix:** Append to the Workflows and provenance registry sentence: "Every writer to a retained repository holds create without update or delete, and registry garbage collection and retention never remove a retained digest."
- **Disposition:** autofix.

### RV-10: The live registry authenticates command-line clients only through API keys of users in the `tache` realm

- **Severity:** low
- **Location:** Workflows and provenance ("anonymous read disabled and per-environment pull credentials"); Owned work G1 ("the registry's failure domain, authenticated read and off-site replica").
- **Claim:** Per-environment pull credentials exist, or can simply be issued.
- **Evidence:**
  - The live `registry.hexalith.com` config has `auth.apikey: true` plus `openid` against `https://auth.tache.ai/realms/tache`, with no htpasswd or LDAP.
  - `accessControl."**".anonymousPolicy: ["read"]`, and an anonymous `GET /v2/_catalog` returns 200 with 9 repositories. For contrast, `registry.tache.ai` has `anonymousPolicy: []` and returns 401.
  - A pull credential therefore needs either an API key minted by a Keycloak user in the `tache` realm, htpasswd, or Zot's OIDC workload identity. Zot 2.1.21 docs: "validate Bearer tokens as OIDC ID tokens (e.g. Kubernetes ServiceAccount, GitHub Actions)"; not verified on the observed 2.1.20.
  - Observed Zot version is 2.1.20, resolved from the digest's OCI annotation `io.stackeroci.stacker.git_version`. The latest is 2.1.21 (2026-09-06).
- **Proposed fix:** Change the G1 clause to "authenticated read with a declared per-environment credential mechanism (htpasswd, workload OIDC, or per-environment registry identities outside the production realm) and off-site replica".
- **Disposition:** autofix.

### RV-11: "`.github/workflows/**` owned by Administrator" is enforceable on public Free repositories only through CODEOWNERS

- **Severity:** low
- **Location:** Workflows and provenance (ruleset sentence).
- **Claim:** "Platform `main` and release tags carry a repository ruleset — pull request required, no force-push or deletion, `.github/workflows/**` owned by Administrator, Administrator-only bypass".
- **Evidence:**
  - About rulesets (docs, fetched today): "Rulesets are available in public repositories with GitHub Free and GitHub Free for organizations … Push rulesets are available for the GitHub Team plan in internal and private repositories". "Restrict file paths" is a push rule.
  - The pull-request rule's `require_code_owner_review` parameter is available: it appears in the live Builds ruleset JSON.
  - Individual `User` bypass actors are accepted; the live Builds ruleset shows `actor_type: "User"`.
  - Tag rulesets restrict creation, update and deletion; "pull request required" does not apply to tags.
- **Proposed fix:** "Platform `main` carries a repository ruleset — pull request required with code-owner review, no force-push or deletion, Administrator-only bypass — and CODEOWNERS assigns `.github/workflows/**` and `.github/CODEOWNERS` to Administrator; release tags carry a tag ruleset forbidding update and deletion, with creation only by the publication workflow or Administrator."
- **Disposition:** autofix.

### RV-12: Dapr's vault store reads its token once and never renews it

- **Severity:** low
- **Location:** Consistency conventions, Secrets ("Per-app tokens are mounted per pod (seed: `vaultTokenMountPath` with `dapr.io/volume-mounts`) … each with a named renewal owner").
- **Claim:** A per-pod mounted token is the seed mechanism.
- **Evidence:**
  - The mechanism exists: Dapr 1.18 documents `vaultTokenMountPath` ("Path to file containing token") and `dapr.io/volume-mounts` ("List of pod volumes to be mounted to the sidecar container in read-only mode").
  - The component documents no Kubernetes-auth method.
  - Source `dapr/components-contrib` `release-1.18`, `secretstores/hashicorp/vault/vault.go` L400-419: `initVaultToken` does `os.ReadFile(v.vaultTokenMountPath)` once during `Init` and contains no renewal. A rotated Secret file is not picked up until the sidecar restarts.
- **Proposed fix:** Append to the Secrets row: "Dapr reads the token once at sidecar start and never renews it, so tokens are renewed outside Dapr and a rotation restarts the pod."
- **Disposition:** autofix.

### RV-13: The dead-man staleness rule does not tolerate GitHub's delayed or dropped scheduled runs

- **Severity:** low
- **Location:** Diagnostics and notification ("the monitor alerts when the dead-man run is stale").
- **Claim:** An hourly schedule on an off-hour minute gives a dependable heartbeat.
- **Evidence:** GitHub docs, "Events that trigger workflows" (schedule):
  - "The schedule event can be delayed during periods of high loads … High load times include the start of every hour. If the load is sufficiently high enough, some queued jobs may be dropped."
  - The minimum interval is 5 minutes, and scheduled runs use only the default branch.
  - The 60-day inactivity auto-disable applies to public repositories only, so the private repository is unaffected.

  Budget check: Free organizations get 2,000 private-repository minutes a month; self-hosted runner use is free, and the announced $0.002/min self-hosted charge was postponed. An hourly job uses about 720 minutes a month.
- **Proposed fix:** "…and the monitor alerts when the dead-man run is stale beyond two scheduled intervals."
- **Disposition:** autofix.

### RV-14: Live shared components are missing from the currency and inventory lists; Dapr drop-all-capabilities is off

- **Severity:** low
- **Location:** Owned work G1 "Infrastructure currency"; Stack pin sentence ("Keycloak …, OpenBao, data-service, broker, Traefik …, Calico, Zot and Velero pins belong to the profile inventory"); Production profile ("Dapr control plane with sidecar drop-all-capabilities enabled").
- **Claim:** The listed components cover the shared infrastructure that the currency check must track.
- **Evidence:**
  - Live components the lists omit:
    - cert-manager `v1.21.2` (current);
    - OpenEBS `openebs.io/local` hostpath provisioner (the default StorageClass is `openebs-hostpath-retain`);
    - KubeSphere Enterprise `v4.2.1` (a cluster-admin management plane; images from `registry.cn-beijing.aliyuncs.com/kse`);
    - `tigera-operator`, whose `GatewayAPI` CRD defaults to creating Gateway API CRDs if they are absent (`crdManagement` "PreferExisting"; no GatewayAPI resource exists today).
  - Gateway API CRDs: Traefik 3.7 docs state support for "v1.6.1" (bumped in 3.7.10), and upstream latest is v1.6.2 (2026-09-03).
  - The live Dapr injector has `SIDECAR_DROP_ALL_CAPABILITIES=false` (`SIDECAR_RUN_AS_NON_ROOT=true`, `SIDECAR_READ_ONLY_ROOT_FILESYSTEM=true`).
  - Components in the list that are behind: Calico `v3.31.3` (latest 3.31.7 / 3.32.2) and CloudNativePG 1.30.0 (1.30.1 released 2026-09-23). Velero's latest is 1.18.4 (released 2026-09-28).
- **Proposed fix:**
  - G1 currency: add "cert-manager, the Gateway API CRDs (standard channel at a version the Traefik pin supports), the storage provisioner, and KubeSphere or its removal".
  - Stack pin sentence: add "cert-manager, Gateway API CRDs".
  - First-staging "Secrets, identity, network and transport" row: add "enable Dapr sidecar drop-all-capabilities (live: off)".
- **Disposition:** autofix.

### RV-15: Removing the `/admin` Ingress alone does not close Keycloak admin exposure

- **Severity:** low
- **Location:** Owned work G1 ("remove Keycloak `/admin` and master-realm routes and the public cluster console, verified by an external negative probe"); AD-6 *Administration*.
- **Claim:** Removing the named routes closes the exposure.
- **Evidence:**
  - `keycloak/keycloak-admin-rate-limit` (`/admin`) is a rate-limit overlay. The catch-all `keycloak/keycloak-ingress` on `auth.tache.ai:/` also serves `/admin` and `/realms/master`.
  - The Keycloak Deployment sets `KC_HOSTNAME=https://auth.tache.ai` with no admin hostname.
  - Through the public A record `82.67.127.189` (checked by DNS-over-HTTPS, then a hairpin request from the LAN; no external vantage point was tested): `auth.tache.ai/admin/` returns 302, `kube.hexalith.com/` returns 302, and `registry.hexalith.com/v2/_catalog` returns 200.
- **Proposed fix:** None needed in the spine. The external negative probe already catches this. Implementation note: restrict the public paths to the application realms and `/resources/`, and serve the admin console on a separate admin hostname.
- **Disposition:** defer (implementation detail under the existing G1 row).

---

## Verified OK (for the memlog)

| Claim | Source and observed result |
| --- | --- |
| Traefik observed 3.7.13, the current release | The live DaemonSet digest `24841fe2…` equals `traefik:v3.7.13` (the `hexalith-tache-ai-relay` Deployment). GitHub releases: v3.7.13 is latest (2026-09-04); no v3.8. |
| Traefik 3.7 Gateway API provider: standard channel, v1.6.1 | Traefik 3.7 provider docs (https://doc.traefik.io/traefik/reference/install-configuration/providers/kubernetes/kubernetes-gateway/): "v1.6.1", HTTPRoute core fully supported, backward compatible with v1.5.x CRDs. GatewayClass controller is `traefik.io/gateway-controller`. Listener ports must equal entryPoint ports; live entryPoints are `web :8000` (hostPort 80) and `websecure :8443` (hostPort 443), so listeners use 8000/8443. Listener hostnames and `allowedRoutes` (Same/All/Selector) are Gateway API core features. |
| Gateway API CRDs are not installed | `kubectl api-resources --api-group=gateway.networking.k8s.io` is empty. The only gateway-named CRDs are `gatewayapis.operator.tigera.io` and `ingressclassscopes.gateway.kubesphere.io`. |
| Traefik serves the `nginx-public` class through `kubernetesingressnginx` | DaemonSet args `--providers.kubernetesingressnginx.controllerClass=k8s.io/ingress-nginx`, `ingressClass=nginx-public`, `publishStatusAddress=192.168.1.31`, alongside `--providers.kubernetescrd`. IngressClass `nginx-public` names controller `k8s.io/ingress-nginx`. |
| Privileged Forgejo runner | Namespace label `pod-security.kubernetes.io/enforce=privileged`; Deployment `forgejo-runner` has a `dind` container with `privileged: true`. |
| Anonymously readable registry | `registry.hexalith.com` Zot config `anonymousPolicy: ["read"]`; anonymous `GET /v2/_catalog` returns 200. `registry.tache.ai` returns 401. |
| Public Keycloak admin routes and KubeSphere console | Ingresses `auth.tache.ai:/admin`, `/realms/master/protocol/openid-connect/token` and `kube.hexalith.com:/` exist; public A records point to 82.67.127.189 (see RV-15). |
| One node, local storage, one shared OpenBao, designated IP | Node `node1` at 192.168.1.30, v1.34.9, Ubuntu 24.04.3, containerd 2.3.3. StorageClasses are OpenEBS hostpath. `openbao/hexalith-keys` runs OpenBao 2.6.2 with 3 replicas. |
| Kubernetes 1.34 end of life 2026-10-27 | https://kubernetes.io/releases/: 1.34 latest 1.34.11, "End of Life: 2026-10-27". Supported minors are 1.35 to 1.37 (1.37.0 released 2026-08-26). |
| Dapr 1.18: HotReload opt-out, per resource type | https://docs.dapr.io/operations/components/component-updates/ lists Components, Subscriptions, Configurations, Resiliency, WorkflowAccessPolicies and HTTPEndpoints as hot-reloaded, with opt-out via the `HotReload` feature. The live Configuration CRD `spec.features[]` has `{name, enabled}`. Live control plane 1.18.1; latest 1.18.4 (2026-09-09). |
| Configuration accessControl matches trust domain, namespace and app ID | Live `configurations.dapr.io` CRD: `accessControl.{defaultAction, trustDomain, policies[].{appId, namespace, trustDomain, defaultAction, operations}}`. |
| WorkflowAccessPolicy v1alpha1 semantics | Live CRD: callers identified by `appID` only; operations enum schedule, terminate, raise, pause, resume, purge, get, rerun; namespaced, with `scopes`. Docs (https://docs.dapr.io/operations/security/workflow-access-policy/): with no policy scoped to an app, "All workflow and activity requests are allowed"; cross-namespace calls are "always denied, regardless of policy contents"; mTLS is required for cross-app enforcement; self-calls are allowed; only `schedule` currently takes effect. Consistent with AD-8. |
| `vaultTokenMountPath` and `dapr.io/volume-mounts` exist in 1.18 | Dapr 1.18 component reference and annotations reference (see RV-12 for the renewal caveat). |
| Keycloak 26.7.4 installed and current | GitHub releases: 26.7.4 (2026-09-16) is latest (memlog digest resolution). Live Deployment `start --hostname-strict=true`, `KC_HOSTNAME=https://auth.tache.ai`, no `KC_FEATURES` override, so token exchange V2 is on by default. |
| Token-exchange preconditions and `azp` | Keycloak token-exchange guide: requester must be confidential and in the subject token `aud`; a per-client "Standard token exchange" switch; refresh token only with "Allow refresh token in Standard Token Exchange"; the exchanged token's `azp` is the requester client (example `"azp": "requester-client"`); public clients cannot exchange. See RV-7 for the upscoping caveat. |
| "Full scope allowed" and `offline_access` | Server Administration Guide: the Full scope allowed switch is on the dedicated client scope's Scope tab. "To issue an offline token, users must have the role mapping for the realm-level offline_access role … Clients must add an offline_access client scope as an Optional client scope … which is done by default." So the realm contract must remove that optional scope from public clients. Feasible. |
| Admin and user event export options | Server Administration Guide: admin events are saved only when toggled on (with optional "Include representation"); the jboss-logging listener logs success events at debug unless `--spi-events-listener--jboss-logging--success-level=info`. There is no built-in off-cluster sink: use stored events polled over the Admin REST API, the logging listener plus a log shipper, or a custom listener SPI. This fits the deferred "event export channel". |
| GitHub rulesets on Free public repositories | About rulesets (quoted in RV-11). The `Hexalith.Platform` ruleset is not yet applied, as Owned work expects. The org plan is `free` and 2FA is required (not specifically phishing-resistant; that remains a personal practice). |
| GitHub scheduled workflows in a private repository | Events-that-trigger-workflows docs (quoted in RV-13); Actions billing docs: Free org 2,000 minutes, and self-hosted runners are free. |
| Helm 4.2 floor and 4.3 current | The `Aspire.Hosting.Kubernetes` 13.5.4 package README requires "Helm v4.2.0 or later". GitHub: v4.3.0 (2026-09-09); latest 4.2 patch v4.2.4. |
| Stack rows | .NET `latest-sdk` 10.0.401 (release 10.0.12, 2026-09-08). NuGet latest: Aspire.AppHost.Sdk and Aspire.Cli 13.5.4; Aspire.Hosting.Kubernetes and Aspire.Hosting.Keycloak 13.5.4-preview.1.26464.4; Hexalith.EventStore.Aspire 3.109.0 (equals the catalog); CommunityToolkit.Aspire.Hosting.Dapr latest 13.5.1-beta.806 (the spine's beta.757 is an existing pin, as stated). |
| OpenBao and Redis 8 | GitHub: OpenBao v2.6.3 and v2.7.0 (2026-09-23), live 2.6.2. Redis 8.10.2 (2026-09-17) is the latest Redis 8. |
| Aspire Kubernetes emits Gateway API types and cert-manager integration | Package XML: `GatewayV1`, `HttpRouteV1`, `AddGateway`, `WithGatewayClass`, `WithHostname`, `WithTls`, `CertManagerExtensions.WithTls`. See RV-5 for the attachment limitation. |
| Builds README runner claims | `references/Hexalith.Builds/README.md` L204-224: `run`/`down`/`test` commands, `hexalith.module-manifest.v1`, `hexalith-evidence validate`. Consts are in `SupportedPlatformPins.cs`. See RV-8 for the publication status. |
