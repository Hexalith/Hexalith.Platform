# Review — Reality & version check (validate r2)

**Verdict: CONDITIONAL PASS.** The Stack pins, Helm 4, Kubernetes, Keycloak, OpenBao and GitHub attestation facts check out against the web today and against the repository. Three premises of accepted risks or currency rules are contradicted by the live environment: the GitHub Free framing, the retired shared ingress controller, and a privileged CI runner on the production node. Two Dapr 1.18 facts are also stated wrongly or left out: `WorkflowAccessPolicy` matching, and HotReload being on by default. Clearing the conditions takes RV-1, RV-2 and RV-4 dispositioned, plus the medium autofixes.

- **Target:** `ARCHITECTURE-SPINE.md` (status final, updated 2026-09-27) and the `.memlog.md` `(version)` entries.
- **Brownfield:** root `apphost.cs`, `global.json`, `README.md`, `DaprSelfHostedMtls.cs`, the Builds catalog, module submodules, and the live cluster (read-only `kubectl get`).
- **Scope:** read-only review. Only this file was written. No cluster, Git or registry state was changed.
- **Date:** 2026-09-27.
- **Evidence classes used below:**
  - **web**: an official or upstream source fetched today, such as a NuGet or GitHub release API, vendor docs, or a package binary.
  - **repo**: repository files at the current checkout.
  - **live**: read-only `kubectl get`, `gh api` or an anonymous registry GET, run today.
  - **prior**: carried from an earlier review and not re-fetched today.

Counts: 0 critical, 3 high, 6 medium, 8 low (17 findings).

## Verification table

| # | Claim | Spine location | Evidence | Status |
| --- | --- | --- | --- | --- |
| 1 | .NET SDK 10.0.401, latestPatch | Stack L205 | `global.json:2-4`, committed at b9410d3. Web: https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json lists latest-sdk 10.0.401 (10.0.12, 2026-09-08, security); .NET 11 is RC1 only. Local `dotnet --version` 10.0.401 | confirmed (web + repo) |
| 2 | Aspire AppHost SDK, Docker and Redis hosting 13.5.4, current stable | Stack L206 | `apphost.cs:2,4,5`. Web: NuGet flat-container indexes for `aspire.appHost.sdk`, `aspire.hosting.docker` and `aspire.hosting.redis` show latest 13.5.4; GitHub microsoft/aspire v13.5.4 released 2026-09-15; no 13.6 published | confirmed (web + repo) |
| 3 | Aspire CLI must equal the SDK (13.5.4); README floor 13.4.6; installed 13.5.3 | Stack L207 | `README.md:26` (13.4.6). Local `aspire --version` 13.5.3+b5f1433. Web: NuGet `aspire.cli` latest 13.5.4 | confirmed (web + repo + local) |
| 4 | CommunityToolkit Dapr 13.5.1-beta.757, catalog beta.770, prerelease | Stack L208 | `apphost.cs:6`; `references/Hexalith.Builds/Props/Directory.Packages.props:151` (beta.770). Web: NuGet index shows latest beta.770 and last stable 13.0.0. The toolkit shipped GitHub release v13.5.0 on 2026-08-25, but the Dapr package has no 13.5 stable | confirmed (web + repo) |
| 5 | Hexalith.EventStore.Aspire 3.106.0 must move to catalog 3.109.0 | Stack L209 | `apphost.cs:7`; `Directory.Packages.props:9`. Web: NuGet latest 3.109.0. The 3.109.0 nuspec requires CT Dapr ≥ beta.767 and Keycloak 13.5.4-preview (RV-12) | confirmed (web + repo) |
| 6 | Aspire.Hosting.Kubernetes 13.5.4-preview.1.26464.4 | Stack L210 | `Directory.Packages.props:133`. Web: every NuGet version is `-preview`. Package inspection today found Helm 4 invocations (`--server-side=true --force-conflicts`), a `HelmVersionValidator` with a Helm 4 minimum, Ingress and Gateway emitters, and cert-manager install via `oci://quay.io/jetstack/charts/cert-manager` | confirmed (web + package) |
| 7 | Aspire.Hosting.Keycloak 13.5.4-preview, "where local realms use it" | Stack L211 | `Directory.Packages.props:132`. Web: the nuspecs of EventStore.Aspire 3.106.0 and 3.109.0 list `Aspire.Hosting.Keycloak` as an unconditional dependency | version confirmed; "where used" framing **wrong** (RV-12) |
| 8 | Helm 4.2.0+ required, 4.3.0 current | Stack L212 | Web: GitHub helm/helm v4.3.0 released 2026-09-09 (v3.22.0 on 2026-09-10); https://aspire.dev/integrations/compute/kubernetes/ says "Helm v4.2.0 or later" and that Aspire validates the version. Helm is not installed locally | confirmed (web) |
| 9 | Dapr 1.18; observed 1.18.1–1.18.4 skew | Stack L213 | Repo: `DaprSelfHostedMtls.cs:43,86,107` (1.18.3); `references/Hexalith.Builds/.github/workflows/domain-ci.yml:28,33` (CLI 1.18.0, runtime 1.18.2). Live: dapr-system images 1.18.1, injector `SIDECAR_IMAGE=ghcr.io/dapr/daprd:1.18.1`. Local: runtime 1.18.4, CLI 1.18.2. Web: dapr/dapr v1.18.4 released 2026-09-09; 1.16–1.18 supported (https://docs.dapr.io/operations/support/support-release-policy/) | confirmed (web + repo + live) |
| 10 | Helm 4 `--rollback-on-failure` (formerly `--atomic`) | AD-3 L69 | Web: https://helm.sh/docs/overview/ (Helm 4.3.0) says "--atomic → --rollback-on-failure", and the old flag remains as a deprecated alias that emits a warning | confirmed (web); a guard must reject both spellings |
| 11 | `helm upgrade` an OCI chart by manifest digest | AD-2 L63 | Web: https://helm.sh/docs/overview/ ("Install charts by digest… non-matching digests are not installed"); https://helm.sh/docs/topics/registries/ (digest URI; `helm push` prints `Digest:`; page flagged "not yet updated for Helm 4") | confirmed (web) |
| 12 | Helm server-side apply ownership needs qualifying | Owned work L312 | Web: Helm 4 overview says new releases default to server-side apply and upgrades follow the release's previous apply method. Package: Aspire `deploy` uses `--server-side=true --force-conflicts` | confirmed (web + package) |
| 13 | Executors never regenerate with `aspire publish`/`aspire deploy` | AD-2 L63 | Memlog L21, with https://aspire.dev/deployment/deploy-with-aspire/ (deploy regenerates and does not consume published assets). Today, aspire.dev confirms that `aspire deploy` shells out to `helm upgrade --install` | confirmed (web) |
| 14 | Hosted publish: data services become external connection parameters | AD-1 L57 | Web: https://aspire.dev/deployment/kubernetes/ maps "Connection strings → ConfigMaps and Secrets" and "Environment variables → ConfigMaps and Secrets" | mechanism confirmed; conflicts with the Secrets convention (RV-7) |
| 15 | Dapr `secretstores.hashicorp.vault` against OpenBao | Secrets L153; EventStore AD-24 | Web: https://docs.dapr.io/reference/components-reference/supported-secret-stores/openbao/ ("tested and confirmed"; v1.18, modified 2026-09-26). The Vault component is token-only (`vaultToken`/`vaultTokenMountPath`), with no renewal. Repo: `references/Hexalith.EventStore/_bmad-output/planning-artifacts/architecture.md:255` | confirmed (web + repo) |
| 16 | `WorkflowAccessPolicies` deny by default and allow callers by trust domain, namespace and app ID | AD-8 L99; AD-1 L57 | Web: https://pkg.go.dev/github.com/dapr/dapr/pkg/apis/workflowaccesspolicy/v1alpha1 (v1.18.4: `WorkflowCaller{AppID}` only, no defaultAction). https://blog.dapr.io/posts/2026/06/10/dapr-v1.18-is-now-available/ (`dapr.io/v1alpha1`, **Alpha**, open until a policy is loaded). https://docs.dapr.io/developing-applications/building-blocks/workflow/workflow-multi-app/ ("Cross-namespace workflows are not supported"). Live: CRD `workflowaccesspolicies.dapr.io` installed, 0 policies | **wrong** (RV-5) |
| 17 | Configuration ACL by trust domain, namespace and app ID; Sentry-validated namespace | AD-8 L99 | Web: https://docs.dapr.io/operations/configuration/invoke-allowlist/ (v1.18: SPIFFE `trustDomain/ns/appId` matching; default trust domain `public`; covers service invocation only). Live: existing Memories Configurations use `trustDomain: public`, `defaultAction: deny` | confirmed (web + live) |
| 18 | Sidecar drop-all-capabilities setting | Production profile L154 | Web: https://github.com/dapr/dapr/blob/master/charts/dapr/README.md (`dapr_sidecar_injector.sidecarDropALLCapabilities`, default false). Seccomp annotation `dapr.io/sidecar-seccomp-profile-type` per https://docs.dapr.io/reference/arguments-annotations-overview/. Live: injector `SIDECAR_DROP_ALL_CAPABILITIES=false`, `SIDECAR_RUN_AS_NON_ROOT=true`, `SIDECAR_READ_ONLY_ROOT_FILESYSTEM=true` | confirmed (web + live); not yet enabled |
| 19 | "within the control plane's supported skew" | Production profile L154 | Web: the Dapr support policy defines minor support and upgrade paths only. https://docs.dapr.io/operations/hosting/kubernetes/kubernetes-upgrade/ says only to restart deployments to update sidecars; no skew table exists | **unverifiable**, as no upstream term exists (RV-13) |
| 20 | Redis pub/sub excluded per EventStore AD-26; AD-26 unratified | Owned work L315; Production profile L154 | Repo: EventStore `architecture.md:284` (AD-26 is `[ASSUMPTION]`; "Redis is Development/test only") | confirmed (repo) |
| 21 | Dapr 1.18 resource change semantics (implicit in AD-3, Catalogs and Verification) | AD-3 L69; Catalogs L151 | Web: https://docs.dapr.io/operations/components/component-updates/ (HotReload is GA and on by default; Configuration, Resiliency, WorkflowAccessPolicy and HTTPEndpoint changes trigger a graceful sidecar restart; actor-state-store reload behavior changed in 1.18.3) | **not recorded** (RV-6) |
| 22 | Keycloak standard token exchange (V2) by a confidential client; subject preserved | AD-14 L135 | Web: https://www.keycloak.org/securing-apps/token-exchange (26.7.4: fully supported, enabled by default; requester must be confidential; `azp` = requester; subject_token `aud` must include the requester; per-client switch; no refresh token by default). Officially supported since 26.2 (https://www.keycloak.org/2025/05/standard-token-exchange-kc-26-2) | confirmed (web); preconditions unrecorded (RV-14) |
| 23 | `azp` authenticates only confidential clients | AD-14 L135 | Web: the token-exchange doc example shows `azp` set to the requester client. Keycloak `TokenManager` sets `issuedFor(client_id)`; RFC 8252 §8.5–8.6 (prior, source-code check) | confirmed (web + prior) |
| 24 | Keycloak admin and user events exported off-cluster | AD-6 L87 | Web: Keycloak Server Admin Guide 26.7.4 (event listener SPI, `/admin-events` REST, include representation). The off-by-default detail comes from prior source inspection | confirmed (web + prior) |
| 25 | Installed Keycloak version unknown (memlog L65) | Owned work L316/L318 | Live: image digest `sha256:82a77884…` resolves on quay.io to tag **26.7.4** (latest, released 2026-09-16) | memlog **stale**; the server is current (RV-14) |
| 26 | Kubernetes 1.34 EOL 2026-10-27 | Owned work L318; Accepted risks L328 | Web: https://kubernetes.io/releases/ (1.34 EOL 2026-10-27; 1.35–1.37 maintained). GitHub kubernetes v1.34.12 released 2026-09-23. Live: node1 v1.34.9 (kubeadm static pods) | confirmed (web + live); patch is 3 behind |
| 27 | Pod Security `restricted` | AD-8 L99 | Web: https://kubernetes.io/docs/concepts/security/pod-security-standards/ (v1.37; applies to init and ephemeral containers). Live: only `keycloak` and `openbao` namespaces enforce `restricted`; `hexalith-memories` is unlabelled | confirmed (web + live) |
| 28 | Built-in ValidatingAdmissionPolicy | Hosted interfaces L157 | Web: https://kubernetes.io/docs/reference/access-authn-authz/validating-admission-policy/ ("v1.30 [stable]"). Live: no policies deployed yet | confirmed (web) |
| 29 | Default-deny ingress and egress NetworkPolicy on an enforcing CNI | AD-8 L99 | Live: CNI is Calico v3.31.3 (calico-system), which enforces egress policy; AdminNetworkPolicy CRDs are present. Web: Calico latest v3.31.7 / v3.32.2 | confirmed (live); CNI version stale (RV-2) |
| 30 | Namespaced or policy-restricted certificate issuers | Hosted interfaces L157 | Live: cert-manager v1.21.2 installed. Web: v1.21.2 released 2026-09-11 is the latest | confirmed (web + live) |
| 31 | Artifact attestations from the public Platform repo on GitHub Free | Workflows L155; AD-2 L63 | Web: GitHub docs ("If you are on a GitHub Free, GitHub Pro, or GitHub Team plan, artifact attestations are only available for public repositories"); public repos use the Sigstore Public Good transparency log. Live: `gh api orgs/Hexalith` gives plan `free`; Platform, Builds and EventStore are PUBLIC. Repo: Builds `domain-release.yml:1000` uses `actions/attest-build-provenance@4d10147…` (v4.2.2) | confirmed (web + live + repo) |
| 32 | Provenance names caller repo, workflow, protected ref and pinned Builds workflow ref | Workflows L155; AD-2 L63 | Web: https://cli.github.com/manual/gh_attestation_verify (`--signer-workflow`, `--signer-digest`, `--source-ref`, `--source-digest`, `--repo`; a reusable workflow is the signer). No flag checks the caller workflow file. Live: **Platform `main` is unprotected and has no rulesets** | mechanism confirmed; "protected ref" **false today** (RV-1, RV-17) |
| 33 | GitHub OIDC claim checks for self-hosted executors | AD-7 L93 | Web: https://docs.github.com/en/actions/reference/security/oidc (`job_workflow_ref`, `workflow_ref`, `ref`, `event_name`, `repository`, `environment`, `runner_environment`=`self-hosted`); no plan restriction stated | confirmed (web) |
| 34 | Executors refuse jobs not on their allowlist | AD-7 L93 | Web: https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/run-scripts (a non-zero exit from `ACTIONS_RUNNER_HOOK_JOB_STARTED` fails the job before it runs) | confirmed (web) |
| 35 | SHA-pinned Builds reusable workflows | Workflows L155 | Web: https://docs.github.com/en/actions/how-tos/reuse-automations/reuse-workflows ("Using the commit SHA is the safest option") | confirmed (web) |
| 36 | GitHub Free: private repos lack environments and protection; accepted with a single-writer ops repo | AD-7 L93; Accepted risks L328 | Web: manage-environments doc ("Organizations with GitHub Team and users with GitHub Pro can configure environments for private repositories"). Live: org rulesets return HTTP 403 "Upgrade to GitHub Team". Platform/Builds have 3 writers (admins `jpiquot`, `tinouit`; writer `QuentinDV`); the Builds `Protect main` ruleset lets all three bypass | plan facts confirmed; accepted-risk framing **incomplete** (RV-1) |
| 37 | Hourly GitHub-hosted dead-man check | Diagnostics L159 | Web: https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows (5-minute minimum; top-of-hour delays and drops; "In a public repository, scheduled workflows are automatically disabled when no repository activity has occurred in 60 days") | confirmed with caveats (RV-15) |
| 38 | Builds registry `registry.hexalith.com`; per-environment pull credentials | Workflows L155 | Live: Zot v2.1.20 in namespace `registry-distribution`; anonymous `GET /v2/_catalog` and `/v2/eventstore/tags/list` succeed. A second Zot, `registry.tache.ai` (`registry-private`), also exists. Web: Zot latest v2.1.21 | host confirmed; pull-credential control **contradicted** (RV-8) |
| 39 | OpenBao current and patched; Raft snapshot semantics | Owned work L316/L318; AD-12 L123 | Web: GitHub openbao v2.7.0 and v2.6.3 released 2026-09-23; https://openbao.org/docs/commands/operator/raft/ (2.7.x: restore "return[s] the cluster to the state defined in it", with no partial restore). Live: `quay.io/openbao/openbao:2.6.2` | confirmed (web + live); 2.6.2 unpatched, as the spine says |
| 40 | Redis Stack 7.4 to Redis 8 | Owned work L318 | Web: https://github.com/redis-stack/redis-stack ("stop releasing maintainance releases of Redis Stack (6.2, 7.2, and 7.4) on December 2025"; "All Redis Stack modules are included in Redis Open Source"); redis 8.10.2 released 2026-09-17. Repo: Memories `redis-statefulset.yaml:37` pins `redis-stack-server:7.4.0-v8` | confirmed (web + repo) |
| 41 | FalkorDB, CloudNativePG and PostgreSQL currency | Owned work L318 | Web: FalkorDB v4.20.7 (2026-09-24) against 4.12.0 in Memories `falkordb-statefulset.yaml:37`; CNPG v1.30.1 (2026-09-23) against live 1.30.0; PostgreSQL 15.19 and 18.6 (https://www.postgresql.org/versions.json) against live 15.15 and repo 18.4 | confirmed (web + repo + live) |
| 42 | EventStore AD-10, AD-22, AD-24, AD-26 and AD-28 as cited | AD-6, AD-13, Secrets, Migration, Profile | Repo: EventStore `architecture.md:151` (AD-10 JWT and fingerprint conformance), `:243` (AD-22 exact-SHA removal authority), `:255` (AD-24 OpenBao), `:284` (AD-26 `[ASSUMPTION]`), `:298` (AD-28 app channel) | confirmed (repo) |
| 43 | EventStore AD-29 "attested original actor" for asynchronous task steps | AD-14 L135; Source table L191 | Repo: EventStore `architecture.md:304`. AD-29 is "Admin Mutations Preserve Human And Service Attribution", scoped to Admin mutations. EventStore has no "extension API" text (grep) | AD exists; cited scope **wrong** (RV-9) |
| 44 | Cross-module references (Projects AD-28/AD-30/G-1, `hexalith.module.v1`; Folders I-10/I-11/EXT-ES-RECOVERY; Works AD-20; Agents EXT-HOST-1) | Source precedence L187; Migration L160 | Repo: Projects spine L289, L301, L486, L441; Folders `architecture.md` L229, L835, L836; Works `architecture.md:326`; Agents readiness report L391. All eight spine source links resolve | confirmed (repo) |
| 45 | Stack seed is "the uncommitted working tree" | Stack L201 | Repo: `git log` shows `apphost.cs`, `global.json` and `DaprSelfHostedMtls.cs` committed at b9410d3 (2026-09-27 22:11 +0200), with no working-tree diff | **stale** (RV-10) |
| 46 | Staging and production on the designated `192.168.1.30` installation | Hosted interfaces L157 | Live: single node `node1`, InternalIP 192.168.1.30. Public ingress status address is 192.168.1.31 | confirmed (live) |
| 47 | Environment ingress (implicit: shared, patched infrastructure) | Hosted interfaces L157; Profile L154 | Live: IngressClass `nginx-public` names controller `k8s.io/ingress-nginx` and serves `auth.tache.ai`, `registry.hexalith.com`, `registry.tache.ai` and `kube.hexalith.com`. Web: https://kubernetes.io/blog/2025/11/11/ingress-nginx-retirement/ (no releases or security fixes after March 2026); `gh api repos/kubernetes/ingress-nginx` gives `archived: true`, last chart 2026-03-19 | **stale / out of support** (RV-2) |

## Findings

### RV-1 — GitHub Free accepted risk omits the publication-side trust root, whose "protected ref" does not exist today
- **Severity:** high
- **Location:** Accepted risks L328 ("GitHub Free with a single-writer operations repository"); AD-2 L63 ("provenance names the protected publication workflow and ref"); Workflows and provenance L155; AD-7 L93 ("Adopt GitHub Team controls once a second person gains write access to the operations repository").
- **Finding:** Deploy trust rests on attestations minted by the publication workflow in the **public** Platform repository, and the accepted risk reasons only about the private operations repository. Checked today:
  - `Hexalith/Hexalith.Platform` has three people with write or admin access (admins `jpiquot`, `tinouit`; writer `QuentinDV`).
  - Platform `main` has no branch protection and no rulesets (`GET …/branches/main/protection` returns 404; `GET …/rulesets` returns `[]`).
  - The Builds `Protect main` ruleset gives "always" bypass to OrganizationAdmin and to `QuentinDV`, which covers everyone.
  - Organization rulesets are refused on Free (HTTP 403, "Upgrade to GitHub Team").
  - Repository rulesets and branch protection **are** available for public repositories on Free, so the target is reachable without Team.

  An attestation proves only that the named workflow ran at the named ref. It does not prove the ref was reviewed.
- **Consequence:** Any of the three accounts, or a stolen token for one, can push a modified publication workflow to Platform `main` and mint attestations that pass `--signer-workflow`/`--source-ref` verification. The production executor would then deploy that output. The single-writer control on the operations repository does not cover this path.
- **Suggested fix:**
  - Add a before-first-publication owned-work row: a repository ruleset on Platform `main` and release tags (PR required, no force-push or deletion, `.github/workflows/**` owned by Administrator, bypass only by Administrator), plus equivalent tightening of Builds bypass actors.
  - Reframe the accepted risk to "GitHub Free: repository-level rulesets only; publication repositories have N writers and named bypass actors; operations repository single-writer".
  - Record the observed state in the memlog.
- **Suggested disposition:** discuss

### RV-2 — The shared ingress controller is the retired ingress-nginx, and it is absent from the currency rules
- **Severity:** high
- **Location:** Owned work "Infrastructure currency" L318; Production profile L154 ("Inventory pins stay within upstream support and current on security patches"); Hosted interfaces L157; Structural seed L232 ("ingress and certificates").
- **Finding:**
  - The live IngressClass `nginx-public` names controller `k8s.io/ingress-nginx`. Its ingresses publish status 192.168.1.31, and no controller pod runs in the cluster, so the controller version could not be read.
  - Kubernetes SIG Network retired ingress-nginx in March 2026: "no further releases, no bugfixes, and no updates to resolve any security vulnerabilities". The GitHub repository is archived, and its last charts shipped on 2026-03-19.
  - This controller fronts `auth.tache.ai` (the only identity issuer for both environments), both registries and `kube.hexalith.com`.
  - Traefik v3.7.13 (current) is also installed.
  - Other shared-layer components are behind and also missing from the currency row: Calico v3.31.3 (latest v3.31.7 / v3.32.2; Calico is the NetworkPolicy enforcement point AD-8 relies on), Zot v2.1.20 (v2.1.21), Velero v1.18.2 (v1.18.3) and Kubernetes patch 1.34.9 (1.34.12).
- **Consequence:** The ingress that terminates TLS for the production issuer and gateway can receive no security fixes, which breaks the spine's own "within upstream support" rule. A Kubernetes minor upgrade before 2026-10-27 may also require CNI and controller changes that nobody has scheduled.
- **Suggested fix:**
  - Add the ingress controller, CNI, registry and backup tooling to the Infrastructure currency row with a G1 gate.
  - Record the replacement decision (Traefik is already present; Gateway API is the upstream recommendation) as an Administrator-owned environment-layer change.
  - Record these observations in the memlog.
- **Suggested disposition:** autofix (list); discuss (controller choice)

### RV-3 — Keycloak admin and master-realm endpoints, and a cluster console, are on public ingress today, unrecorded
- **Severity:** medium
- **Location:** AD-6 L87 ("master realm and admin consoles stay off public ingress"); Owned work L316 ("Keycloak inventory") and L317 (exposure).
- **Finding:** Live ingresses on `auth.tache.ai` route `/admin` (`keycloak-admin-rate-limit`), `/realms/master/protocol/openid-connect/token` (`keycloak-master-token-rate-limit`) and `/` through the public class. The KubeSphere console (v4.2.1, `registry.cn-beijing.aliyuncs.com/kse/*` images) is public at `kube.hexalith.com`. The memlog records none of this, and no owned-work row names the removal.
- **Consequence:** AD-6's rule is written as a target, but nothing assigns closing the existing gap before G1. A reader could take the rule as already true.
- **Suggested fix:** Add "remove `/admin` and master-realm routes and cluster consoles from public ingress; verify with an external negative probe" to the Exposure row (before G1), and record the observation in the memlog.
- **Suggested disposition:** autofix

### RV-4 — A privileged CI runner shares the single production node, which breaks the "shared kernel" residual-risk premise
- **Severity:** high
- **Location:** AD-8 L99 ("The single-node shared kernel is an accepted residual risk"); AD-7 L93 ("Staging, test or PR code never runs on an executor or host process that holds or held production credentials; co-scheduling on the shared node is the AD-8 residual risk"); Accepted risks L328.
- **Finding:** Live namespace `forgejo-runner` is labelled `privileged` and runs `forgejo/runner` with Docker-in-Docker containers set to `securityContext.privileged: true` on `node1`. That is the only node, and it also hosts Keycloak, OpenBao, the Dapr control plane and Memories. The Forgejo instance is `repository.tache.ai`. A privileged container is root on the node. It is not merely a shared-kernel exposure.
- **Consequence:** Anyone who can run a Forgejo Actions job gets node root, and with it every Secret, OpenBao volume and sidecar identity of both environments. The accepted residual risk assumed `restricted` pods. Its real severity is unrecorded.
- **Suggested fix:** Record the observation. Before G1, either move CI runners off the production node, or run them rootless and unprivileged in a `restricted` namespace with no host mounts. Otherwise name "privileged CI runner on the production node" as an explicit, owner-signed accepted risk.
- **Suggested disposition:** discuss

### RV-5 — `WorkflowAccessPolicy` is alpha, matches callers by app ID only, and is open until a policy is loaded
- **Severity:** medium
- **Location:** AD-8 L99 ("Hosted Dapr Configurations and WorkflowAccessPolicies deny by default and allow callers by trust domain, namespace and app ID"); AD-1 L57; Accepted risks L328 (prerelease pins).
- **Finding:**
  - The Dapr 1.18.4 API is `dapr.io/v1alpha1`. `WorkflowCaller` has only `AppID`, with no `namespace` or `trustDomain` field, and there is no `defaultAction`.
  - Behavior: with no policy loaded for a target, every call is allowed. With one or more loaded, the target denies anything not matched, and self-calls are always allowed.
  - Namespace isolation for workflows comes from elsewhere: "Cross-namespace workflows are not supported" upstream.
  - The CRD is installed live, with 0 policies.
- **Consequence:** Implementers cannot express the triple match the spine mandates. Deny-by-default holds only if every workflow-hosting app has a scoped policy loaded. The alpha API can change between minors and is not listed as an accepted prerelease risk.
- **Suggested fix:**
  - Reword: Configurations match by trust domain, namespace and app ID. WorkflowAccessPolicies match by app ID within the single-namespace workflow scope, and every workflow-hosting app must have at least one scoped policy.
  - Add "Dapr WorkflowAccessPolicy v1alpha1" to the accepted prerelease risks.
  - Add a negative test that a staging app ID equal to a production app ID cannot schedule production workflows.
- **Suggested disposition:** autofix

### RV-6 — Dapr 1.18 HotReload is on by default, which the rollout, rollback and verification model does not account for
- **Severity:** medium
- **Location:** AD-1 L57 (Configurations, WorkflowAccessPolicies, Subscriptions and Resiliency go with the application package); AD-3 L69; Catalogs activation L151; Verification L174; Secrets L153 (EventStore AD-24, "app-channel token is startup-loaded").
- **Finding:**
  - In Dapr 1.18, HotReload is GA and on by default.
  - Component and Subscription changes reload in place.
  - Configuration, Resiliency, WorkflowAccessPolicy and HTTPEndpoint changes trigger an automatic graceful restart of every sidecar that loads them.
  - `secretKeyRef` changes to Kubernetes Secrets are picked up within 60 seconds.
  - Actor-state-store reload behavior changed between 1.18.2 and 1.18.3, a patch-level behavior difference inside the observed 1.18.1–1.18.4 skew.
  - Neither the spine nor the memlog records any of this.
- **Consequence:** A Helm upgrade that changes an application-package Dapr resource alters or restarts **baseline** sidecars before candidate readiness. That breaks the assumption that the baseline keeps serving during the catalog-activation window. A shared Configuration change can also cause simultaneous sidecar restarts that trip the 60-second unavailability rule. Environment-layer Component changes propagate into running workloads the moment they are applied.
- **Suggested fix:** Decide the HotReload posture in the profile template: either disable `HotReload` in hosted Configurations so changes follow pod rollout, or model its effects. Add it to Aspire-to-Helm qualification and rollback rehearsal, and pin the exact Dapr patch, not only the minor, in the template.
- **Suggested disposition:** discuss

### RV-7 — The Aspire Kubernetes publisher renders connection strings into Kubernetes Secrets, which conflicts with the Secrets convention and AD-8
- **Severity:** medium
- **Location:** AD-1 L57 ("In hosted publish, data services become external connection parameters"); Secrets L153 ("any Kubernetes Secrets are documented bootstrap exceptions"); AD-8 L99 (bootstrap Secrets live in the data namespace, outside the application deploy identity's scope); Owned work L312.
- **Finding:** aspire.dev maps "Connection strings → ConfigMaps and Secrets" and "Environment variables → ConfigMaps and Secrets". Parameters become Helm values, and Helm keeps release values in an in-cluster release Secret. V-38 raised secret-reference handling earlier. The Aspire-to-Helm qualification row still omits it.
- **Consequence:** Database and broker credentials passed as connection parameters end up in application-namespace Secrets and in Helm release history, managed by the application deploy identity. This contradicts both rules.
- **Suggested fix:** State that hosted connection parameters carry only non-secret endpoints, and that credentials resolve through Dapr secret stores or component `secretKeyRef`. Add "rendered chart contains no credential-bearing Secret or values" to the Aspire-to-Helm qualification.
- **Suggested disposition:** autofix

### RV-8 — The registry is anonymously readable, a second registry exists, and an `eventstore` repository already exists
- **Severity:** medium
- **Location:** Workflows and provenance L155 ("Builds registry `registry.hexalith.com` … pull credentials are per-environment secrets"); AD-13 L129 (composed `eventstore` image).
- **Finding:**
  - Anonymous `GET https://registry.hexalith.com/v2/_catalog` lists `eventstore`, `memories`, `parties` and other repositories, and tag lists are anonymous too. It is Zot v2.1.20 in namespace `registry-distribution`, labelled `baseline`.
  - A separate private Zot serves `registry.tache.ai`.
  - The `eventstore` repository holds EventStore-published tags 3.100.0 through 3.104.0.
- **Consequence:** Per-environment pull credentials add no read control. The spine does not say which registry holds retained artifacts, or whether public read is intended. The composed host image name collides with the existing EventStore-published `eventstore` repository, which blurs AD-13's subject identity.
- **Suggested fix:**
  - Name the retained-artifact registry and its read policy (public read accepted, or authenticated).
  - Name the composed-image repository distinctly, for example `platform/eventstore`, or state that EventStore's `eventstore` repository is not a deployment subject.
  - Add Zot to the currency row.
- **Suggested disposition:** discuss

### RV-9 — EventStore AD-29 does not provide the "original actor attested at admission" that AD-14 relies on
- **Severity:** medium
- **Location:** AD-14 L135 ("asynchronous task steps use the original actor attested by EventStore at admission"); Source table L191 ("AD-29 attested original actor"); First shared versions L286–301.
- **Finding:** EventStore AD-29 (`architecture.md:304`) is "Admin Mutations Preserve Human And Service Attribution" and is scoped to Admin mutations, as a bounded delegation. No EventStore decision defines admission-time actor attestation for general domain or task commands. The same Source row presents the "versioned extension API" as a retained contract, but EventStore's architecture has no such text; it is correctly listed as pending in First shared versions.
- **Consequence:** AD-14's asynchronous path (Projects tasks) rests on a capability no owner has adopted. FR-12 and Projects enrollment could stall on a missing EventStore contract with no row tracking it.
- **Suggested fix:** Add a First shared versions row: "EventStore attested-original-actor contract for asynchronous task steps (extends AD-29)". Owner EventStore; consumers Projects and McpCli; must precede Projects cross-module steps and FR-12 acceptance. Reword the Source row to "requested".
- **Suggested disposition:** autofix

### RV-10 — The Stack seed says "uncommitted working tree", but the files are committed
- **Severity:** low
- **Location:** Stack L201.
- **Finding:** `apphost.cs`, `global.json` and `DaprSelfHostedMtls.cs` were committed at b9410d3 (2026-09-27 22:11) and have no working-tree diff. The spine was edited later (22:35) and still says uncommitted. Memlog L196 is correct only as of its own time.
- **Consequence:** A reader cannot reproduce the seed from a stable reference.
- **Suggested fix:** "Seed from commit b9410d3 and the Builds catalog at 0610f78 on 2026-09-27".
- **Suggested disposition:** autofix

### RV-11 — The Aspire CLI rule is correct, but nothing records who resolves the drift today
- **Severity:** low
- **Location:** Stack L207.
- **Finding:** Confirmed: README floor 13.4.6, installed 13.5.3, SDK 13.5.4. The row already says "to align". Only the owner and the trigger (the Platform tool check) are missing from the Source/package adoption row L308.
- **Consequence:** The drift persists until the first tool check.
- **Suggested fix:** Mention the README floor update in Owned work L308.
- **Suggested disposition:** autofix

### RV-12 — `Aspire.Hosting.Keycloak` preview is mandatory, not "where local realms use it", and the pin moves are coupled
- **Severity:** low
- **Location:** Stack L209, L211.
- **Finding:**
  - The nuspecs of Hexalith.EventStore.Aspire 3.106.0 and 3.109.0 both list `Aspire.Hosting.Keycloak` as an unconditional dependency (13.5.3-preview.1.26425.3 and 13.5.4-preview.1.26464.4). Every composition carries the preview package.
  - The root AppHost currently resolves the 13.5.3 preview against the 13.5.4 SDK.
  - EventStore.Aspire 3.109.0 requires CommunityToolkit Dapr ≥ beta.767. Moving that row alone while the root keeps beta.757 yields a NuGet downgrade error.
- **Consequence:** The accepted-risk scope is understated, and a partial pin move breaks the build.
- **Suggested fix:** Change the row status to "transitive via EventStore.Aspire; prerelease accepted risk". Note that the EventStore.Aspire and toolkit rows move together.
- **Suggested disposition:** autofix

### RV-13 — "The control plane's supported skew" is not an upstream-defined term
- **Severity:** low
- **Location:** Production profile L154.
- **Finding:** Dapr documents minor support (N-2) and upgrade paths, not a control-plane-to-sidecar skew. On Kubernetes the injector sets the sidecar image from the control plane (live `SIDECAR_IMAGE=…daprd:1.18.1`) unless `dapr.io/sidecar-image` overrides it. Local and CI run their own control planes: 1.18.3 in `DaprSelfHostedMtls.cs`, 1.18.2 as the CI default.
- **Consequence:** The rule cannot be checked. Patch-level behavior differences (RV-6) make the gap matter.
- **Suggested fix:** Define it: hosted sidecar patch equals the control-plane patch; local and CI control-plane and sidecar images equal the release's pinned patch.
- **Suggested disposition:** autofix

### RV-14 — Token-exchange preconditions are missing from the realm contract, and the memlog Keycloak version is stale
- **Severity:** low
- **Location:** AD-14 L135; AD-6 L87; memlog L65; Owned work L316.
- **Finding:**
  - Keycloak 26.7.4 standard token exchange requires the requester client in the subject token's `aud`, a per-client "Standard token exchange" switch, and it issues no refresh token by default.
  - The installed Keycloak digest resolves to **26.7.4**, which is current and supports V2. The memlog still says the version is unknown.
- **Consequence:** Without audience mappers, the gateway-to-module-to-module exchange chain fails at runtime. The currency row carries a needless open item.
- **Suggested fix:** Add the audience, switch and refresh-token settings to the realm contract. Record the 26.7.4 observation.
- **Suggested disposition:** autofix

### RV-15 — The dead-man check's repository and schedule are unspecified, and both have GitHub caveats
- **Severity:** low
- **Location:** Diagnostics and notification L159.
- **Finding:** In a public repository, scheduled workflows auto-disable after 60 days without activity. In a private Free repository, an hourly job uses about 720 of the 2,000 included minutes. Top-of-hour schedules are the documented high-load window where runs can be dropped.
- **Consequence:** The monitor-of-the-monitor can silently stop.
- **Suggested fix:** Name the repository, use an off-hour cron minute, and make the off-site monitor alert when the dead-man check's own last run is stale.
- **Suggested disposition:** autofix

### RV-16 — The per-app OpenBao token mechanism is unrecorded under AD-24's singleton `openbao` component
- **Severity:** low
- **Location:** Secrets L153 ("Its per-app tokens … documented bootstrap exceptions").
- **Finding:** The Dapr Vault component takes one `vaultToken` (`secretKeyRef`) or one `vaultTokenMountPath`. With a single component named `openbao` per namespace (EventStore AD-24), per-app tokens need a per-pod token file mounted into daprd through `dapr.io/volume-mounts`, which are secret or projected volumes and allowed under `restricted`. The alternative is distinct per-app components, which contradicts the singleton. No token renewal is documented.
- **Consequence:** Implementers may fall back to the shared token observed today.
- **Suggested fix:** Record `vaultTokenMountPath` plus `dapr.io/volume-mounts` as the seed mechanism, and qualify it in the Secrets owned-work row.
- **Suggested disposition:** defer

### RV-17 — Verifying the caller workflow needs a custom certificate check
- **Severity:** low
- **Location:** Workflows and provenance L155 ("Provenance … names its caller repository, workflow, protected ref and pinned Builds workflow ref").
- **Finding:** `gh attestation verify` checks the signer (the reusable Builds workflow, via `--signer-workflow` and `--signer-digest`), plus source repository, ref and digest. It has no flag for the caller workflow file. That lives in the Fulcio Build Config URI extension and needs a JSON-output policy check. Helm-chart OCI attestation verification is already a qualification item.
- **Consequence:** A naive verifier accepts artifacts built by any workflow in the Platform repository that calls the pinned Builds workflow.
- **Suggested fix:** Specify the verifier policy fields: signer workflow and digest, source repository and ref, plus the Build Config URI equal to the publication workflow path.
- **Suggested disposition:** autofix

## Prior reality findings, re-verified today

| Prior | Status now |
| --- | --- |
| update RV-1 (GitHub plan) | Decision recorded (memlog; AD-7 OIDC/executor allowlist). Plan still `free`. Framing gap, see RV-1 |
| update RV-2 (attestations) | Resolved. Publication moved to the public Platform repo; plan limits confirmed today |
| update RV-3 (`azp`) | Resolved in AD-14. Confirmed |
| update RV-4 (PSS vs Dapr) | Resolved in the profile and AD-1. The live injector still has drop-all `false`, as expected before G1 |
| update RV-5 (workflow ACL) | Partially resolved. The triple-match wording is wrong, see RV-5 |
| update RV-6 (Keycloak events) | Resolved in AD-6 and the Deferred rows |
| update RV-7 (monitor) | Resolved (off-site monitor plus dead-man). Caveats in RV-15 |
| update RV-8 (currency) | Resolved for the listed items. Newly missing items in RV-2 |
| update RV-9 (Helm 4) | Resolved. Confirmed |
| update RV-10/11/12 | Resolved. Residuals in RV-10 and RV-12 |
| update RV-13/14 | Carried as owned work (runner updates; `openbao-runtime-bootstrap` split before 2027-07-19) |
