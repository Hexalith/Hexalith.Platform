# Review — Reality & version check
Verdict: PASS WITH FINDINGS — every named technology exists, and the root pins match the working tree. But the primary AD-1 export path depends on preview packages and gets no Dapr support from the toolkit, and several pins and observed installations are stale. The memlog also doesn't record verification for most Stack rows.

Scope: ARCHITECTURE-SPINE.md (status final) and .memlog.md `(version)` entries, checked against the working tree, the Builds/EventStore submodules, and live sources on 2026-09-27. I made no edits outside this file and ran no mutations.

## Verification ledger
| Claim (spine location) | Checked against | Result | Current? |
| --- | --- | --- | --- |
| .NET SDK 10.0.401, latestPatch (Stack) | `global.json` (working tree; HEAD = 10.0.302); dotnet.microsoft.com/en-us/download/dotnet/10.0 | Matches the working tree. It is the latest SDK (2026-09-08, runtime 10.0.12 security). `#:include` needs SDK ≥ 10.0.300, which is satisfied | Yes, but the pin is uncommitted |
| Aspire.AppHost.Sdk 13.5.4 (Stack) | `apphost.cs` (HEAD = 13.4.6); nuget.org/packages/Aspire.AppHost.Sdk | Matches. Latest stable (2026-09-15). This is the only Stack row with a memlog `(version)` entry (memlog L17) | Yes |
| Aspire.Hosting.Docker / Redis 13.5.4 (Stack) | `apphost.cs`; NuGet flat-container index | Match. Latest stable. Neither package is used directly in `apphost.cs` (Redis comes in transitively via EventStore.Aspire) | Yes (no memlog record) |
| CommunityToolkit.Aspire.Hosting.Dapr 13.5.1-beta.757 (Stack) | `apphost.cs`; nuget.org; `references/Hexalith.Builds/Props/Directory.Packages.props:151` | Matches the root file. It is prerelease: the only stable 13.x is 13.0.0 (2025-11-25). Newer betas exist (beta.770, 2026-09-25), and Builds already pins beta.770 | No (stale and prerelease) |
| Hexalith.EventStore.Aspire 3.106.0 (Stack) | `apphost.cs`; nuget.org; Builds props `HexalithEventStoreVersion=3.109.0`; submodule at `v3.109.0-10` | Matches the root file. Latest is 3.109.0 (2026-09-26). 3.106.0 depends on CT Dapr beta.752, Aspire.Hosting 13.5.3 and Keycloak 13.5.3-preview | No (3 releases behind; mismatches the repo catalog) |
| "checked 2026-09-27 against root files and recorded official sources" (Stack preamble) | memlog `(version)` grep | Only the AppHost SDK is recorded. The memlog has no entry for the .NET SDK, Docker/Redis, CT Dapr or EventStore.Aspire | Overstated |
| AD-1: Aspire exports a Helm chart from the app model | aspire.dev/integrations/compute/kubernetes, /deployment/kubernetes/clusters; nuget Aspire.Hosting.Kubernetes | Exists (`aspire publish`/`deploy`, `WithHelm`, `PublishAsKubernetesService`, `AdditionalResources`). Package is **preview-only** (13.5.4-preview.1.26464.4; never stable). Registry APIs are preview (ASPIRECOMPUTE003). Needs Helm ≥ 4.2.0 | Exists, preview |
| AD-1: Dapr sidecars exported through CT integration | CT `DaprDistributedApplicationLifecycleHook.cs`; aspire.dev Dapr AppHost page; microsoft/aspire#9887 | **No.** Publish mode skips the sidecar and writes a legacy `dapr.v0` manifest entry. No `dapr.io/*` annotations or Component/Configuration CRDs are produced. Publish translation exists only for Azure Container Apps | Gap |
| AD-2: immutable Helm package by content digest | helm.sh/docs/topics/registries; helm/helm releases | `helm push` reports a digest, and install/pull by `oci://…@sha256:` works ("digests are immutable"). Helm 4.3.0 is current. Helm 3.22 was the final v3 feature release | Yes (see REAL-9) |
| AD-6: Keycloak realms, audience checks | memlog L47; keycloak.org/downloads; keycloak/keycloak#52656 | Realms and audience validation are documented. Latest is 26.7.4. The installed version is unknown (digest only). Community Keycloak supports only the latest release | Installed version unverified |
| AD-7: self-hosted runner in private network | docs.github.com self-hosted-runners, secure-use; GitHub changelog 2026-06-12; `gh repo view` | Outbound-443 model is confirmed. Not recorded: runners must update within 30 days (minimum-version enforcement resumed, GHEC full enforcement 2026-09-25). Also GitHub says self-hosted runners should "almost never" be used on public repos, and Platform and Builds are **PUBLIC** | Partially verified |
| Conventions/AD-9: Dapr runtime | dapr/dapr releases; support policy; `DaprSelfHostedMtls.cs`; Builds `domain-ci.yml`; local `dapr --version`; memlog L65 | Latest is 1.18.4 (2026-09-09); 1.16–1.18 are supported. Observed 1.18.1 on the cluster, 1.18.2 as the CI default, 1.18.3 hard-coded in a root file, and 1.18.4 in local `dapr init` | Supported line, patch skew |
| Secrets: "Dapr/OpenBao" (memlog L77 "Dapr openbao component") | docs.dapr.io supported-secret-stores/openbao and /hashicorp-vault | There is no dedicated OpenBao component. Dapr documents `secretstores.hashicorp.vault` v1 as "tested and confirmed" with OpenBao. Token auth only (`vaultToken`/`vaultTokenMountPath`), with TLS options (caCert/caPem/tlsServerName) | Works, with constraints |
| OpenBao 2.6.2 installed (memlog L65/L141) | openbao/openbao releases | 2.7.0 and 2.6.3 were released 2026-09-23. 2.6.3 fixes 9 GHSAs, including a denied-ACL-policy-template evaluation being silently dropped | Unpatched |
| FalkorDB (AD-9 exception), installed 4.12.0 | FalkorDB/FalkorDB releases, repo | Actively maintained. Latest is 4.20.7 (2026-09-24). License is SSPLv1 | Installed version stale |
| Redis Stack 7.4.0-v8 installed (memlog L65) | hub.docker.com/r/redis/redis-stack | Maintenance for 6.2/7.2/7.4 stopped Dec 2025. "Redis Stack users should upgrade to Redis" (Open Source 8) | EOL |
| Kubernetes v1.34.9 single node (memlog L64) | kubernetes.io/releases | 1.34 latest patch is 1.34.11. **EOL 2026-10-27** | Nearly EOL |
| MCP stdio (AD-11) / ModelContextProtocol 2.2.0 (memlog L119) | modelcontextprotocol.io versioning + 2026-07-28 transports; nuget | 2026-07-28 is current. stdio remains a standard binding. 2.2.0 is the latest stable (2026-08-13) | Yes |
| GitHub notifications for freshness/site-loss reporting | docs.github.com events-that-trigger-workflows (schedule) | Mechanism unspecified. See REAL-10 for constraints on public-repo schedules | n/a |
| Dapr self-hosted mTLS (memlog L22) | `DaprSelfHostedMtls.cs` | Accurate: localhost Sentry/placement/scheduler, bind-mounted creds, `ExcludeFromManifest`, fixed ports 50001/51005/51006, named scheduler volume | Accurate |

## Findings

### REAL-1 — Aspire→Helm primary path rests on a preview package and emits no Dapr sidecars
- Severity: high
- Where: AD-1, AD-2; Deferred "Aspire-to-Helm qualification"
- Finding: The spine treats Aspire export as the primary hosted path and makes the Helm chart the fallback. It doesn't say that (a) `Aspire.Hosting.Kubernetes` has never shipped a stable release, or (b) the CommunityToolkit Dapr integration produces nothing Kubernetes-native in publish mode. Every Hexalith workload depends on a Dapr sidecar (AD-9). Each one would therefore need hand-written `dapr.io/*` annotations through `PublishAsKubernetesService`. The Component, Configuration (ACL), Resiliency and Subscription CRDs would all have to be modeled as custom `BaseKubernetesResource` subclasses in `AdditionalResources`. EventStore.Aspire's own helpers say the same: publish-mode Dapr and resiliency wiring is left to "kustomize overlay / Helm values". The generated chart also maps connection strings and env vars to Kubernetes Secrets. Under the Secrets convention, Kubernetes Secrets are allowed only as documented bootstrap exceptions. This is essentially a C# re-description of the whole hosted Dapr topology, which is close to AD-1's own fallback trigger ("duplicate full topology"). The memlog records the preview status (L17/L21) and that the mapping must be deliberate (L22). It never records the missing Dapr export.
- Evidence: https://www.nuget.org/packages/Aspire.Hosting.Kubernetes (all versions `-preview`, latest 13.5.4-preview.1.26464.4). https://aspire.dev/deployment/kubernetes/ (mapping table: "Connection strings → ConfigMaps and Secrets"; Helm ≥ 4.2.0). https://github.com/CommunityToolkit/Aspire/blob/main/src/CommunityToolkit.Aspire.Hosting.Dapr/DaprDistributedApplicationLifecycleHook.cs (`if (context.ExecutionContext.IsPublishMode) return;` and a `dapr.v0` manifest entry). The aspire.dev "Set up Dapr resources" page documents publish translation only for Azure Container Apps. https://github.com/microsoft/aspire/issues/9887 (publish omits Dapr sidecars; closed not planned). In this repo: `references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs:40-47,240-277`, and `DaprSelfHostedMtls.cs` (`ExcludeFromManifest`).
- Failure scenario: The team builds the primary path. The exported chart deploys pods with no daprd, so every service call, state call and pub/sub call fails. Engineers then grow a large annotation/CRD layer in C# on a preview API that can break between patch releases. The AD-1 fallback decision arrives late, after sunk cost. Alternatively, the generated Kubernetes Secrets silently become the application-secret backend.
- Recommendation: Add a `(version)` memlog observation for the toolkit's missing Kubernetes publish support and the preview-only package. In AD-1, state that Dapr annotations/CRDs and secret-reference handling are known required customizations, and say whether they count toward the fallback trigger. Pin and name `Aspire.Hosting.Kubernetes` (preview) in the Stack or profile inventory if the primary path is kept. Alternatively, bring the fallback decision forward.
- Suggested disposition: discuss

### REAL-2 — Stack pins are stale and disagree with the repo's own package catalog
- Severity: high
- Where: Stack table; AD-4/AD-5 (local vs CI parity)
- Finding: Root `apphost.cs` pins Hexalith.EventStore.Aspire 3.106.0 and CT Dapr 13.5.1-beta.757. The shared Builds catalog was updated today (commit aada815, 2026-09-27) and pins EventStore 3.109.0 and CT Dapr beta.770. Platform's own EventStore submodule is at v3.109.0+10. NuGet's latest EventStore.Aspire is 3.109.0 (2026-09-26), which depends on CT Dapr beta.767 and Aspire 13.5.4. The Platform composition therefore hosts modules built and CI-tested against a different EventStore hosting and helper line. The Stack table presents these pins as the observed seed without noting the skew.
- Evidence: `apphost.cs:6-7`. `references/Hexalith.Builds/Props/Directory.Packages.props:9` (`3.109.0`) and `:151` (`13.5.1-beta.770`). `git submodule status` shows `references/Hexalith.EventStore (v3.109.0-10-g3942057d)`. https://www.nuget.org/packages/Hexalith.EventStore.Aspire. The nuspec shows 3.106.0 deps as Aspire.Hosting 13.5.3, Keycloak 13.5.3-preview and CT Dapr beta.752.
- Failure scenario: Local and Platform composition use 3.106.0 helpers, while modules' Release/NuGet CI (AD-5) resolves 3.109.0. Registration, env-var or ACL conventions that changed across 3.107–3.109 then pass in one lane and fail in the other. The release record in AD-2 would bind non-equivalent artifact identities.
- Recommendation: Either align the root pins with the Builds catalog or record the skew as a deliberate exception. Add a rule that says which of the two is authoritative for Platform composition.
- Suggested disposition: discuss

### REAL-3 — Stack evidence is overstated and rests on uncommitted and untracked files
- Severity: medium
- Where: Stack preamble ("checked 2026-09-27 against root files and recorded official sources")
- Finding: Only Aspire.AppHost.Sdk has a recorded official-source check (memlog L17). The .NET SDK, Docker/Redis, CT Dapr and EventStore.Aspire rows have none. Every Stack value also exists only in the uncommitted working tree. HEAD has `Aspire.AppHost.Sdk@13.4.6`, no `#:package` lines and `global.json` 10.0.302. `DaprSelfHostedMtls.cs` (pulled in by `#:include`) and `DaprComponents/` are untracked.
- Evidence: `git diff global.json apphost.cs`; `git status` (`?? DaprComponents/`, `?? DaprSelfHostedMtls.cs`); memlog grep finds no `CommunityToolkit`, `3.106` or `10.0.401`.
- Failure scenario: The working-tree edits are discarded or partially committed (for example, `apphost.cs` without the untracked include). The spine's "observed" Stack then describes a state that no commit contains, and the composition fails to build.
- Recommendation: Add memlog `(version)` entries for each Stack row with the NuGet URL and date. Qualify the Stack evidence as "working tree, uncommitted" or cite a commit once one exists.
- Suggested disposition: autofix

### REAL-4 — Prerelease hosting dependencies not flagged; toolkit's tested-Dapr claim is outside Dapr support
- Severity: medium
- Where: Stack; AD-1; AD-10
- Finding: The local and CI composition depends on prerelease packages. CT Dapr 13.5.1-beta.* are frequent prerelease builds; the only stable 13.x is 13.0.0 from Nov 2025. EventStore.Aspire pulls `Aspire.Hosting.Keycloak` 13.5.x-preview, and the K8s path needs `Aspire.Hosting.Kubernetes` preview. The toolkit README says Aspire is "only tested for compatibility with … Dapr 1.15.3 runtime / 1.15.0 CLI". 1.15 is outside Dapr's supported window (1.16–1.18), while this estate runs 1.18.x. Nothing in the spine or memlog records this support posture.
- Evidence: https://www.nuget.org/packages/CommunityToolkit.Aspire.Hosting.Dapr. https://raw.githubusercontent.com/CommunityToolkit/Aspire/main/src/CommunityToolkit.Aspire.Hosting.Dapr/README.md ("Notes"). https://docs.dapr.io/operations/support/support-release-policy/. `Hexalith.EventStore.Aspire.csproj:13` (Keycloak).
- Failure scenario: A beta bump changes sidecar or CLI argument behavior; `DaprSelfHostedMtls.cs` already works around one CLI trust-domain behavior. Readiness then breaks with no stable fallback, and upstream offers no tested-compatibility claim for 1.18.
- Recommendation: Mark prerelease status in the Stack table. Record the accepted prerelease risk and its owner in the memlog. Require the profile inventory to name the exact beta and preview builds it was tested with.
- Suggested disposition: autofix

### REAL-5 — Dapr runtime patch skew across local, CI and cluster, with an unlisted pin in a root file
- Severity: medium
- Where: Stack ("Dapr runtime … pins belong to the tested environment/profile inventory"); AD-5/AD-10; production-profile convention
- Finding: Four different 1.18 patches are in use:
  - `DaprSelfHostedMtls.cs` hard-codes `daprio/sentry` and `daprio/dapr` **1.18.3** for the local control plane.
  - Local `dapr init` gives daprd **1.18.4** (CLI 1.18.2), so local sidecars are newer than their control plane.
  - The Builds `domain-ci.yml` default runtime is **1.18.2** (CLI 1.18.0).
  - The cluster control plane is **1.18.1** (memlog L65).
  - Upstream latest is 1.18.4 (2026-09-09, scheduler/workflow fixes).

  The Stack section says runtime pins aren't part of the root seed, yet a root file already pins one.
- Evidence: `DaprSelfHostedMtls.cs:42,82,104`. `references/Hexalith.Builds/.github/workflows/domain-ci.yml:24-33`. `dapr --version` shows CLI 1.18.2 and runtime 1.18.4. https://github.com/dapr/dapr/releases/tag/v1.18.4.
- Failure scenario: Workflow and scheduler fixes present in 1.18.4 (for example, workflows stuck PENDING across scheduler restarts) pass local tests and fail in staging on 1.18.1. Staging E2E evidence (AD-2) is then produced on a runtime that local and CI never ran.
- Recommendation: List the root `DaprSelfHostedMtls.cs` image pin in the Stack table or move it into the profile inventory. Require one Dapr runtime patch across local, CI and staging per release record.
- Suggested disposition: autofix

### REAL-6 — "Dapr/OpenBao" secrets: no dedicated component; Vault component is token-only and read-only
- Severity: medium
- Where: Consistency Conventions → Secrets; AD-9; memlog L77
- Finding: Dapr has no OpenBao component type. Its OpenBao page directs users to `secretstores.hashicorp.vault` v1, which it documents as tested with OpenBao. That component authenticates only with a static token (`vaultToken` or `vaultTokenMountPath`); it has no Kubernetes or AppRole auth. The Dapr secrets API is also read-only. The convention asks for "least privilege, default-deny, rotation acknowledgments" and says Kubernetes Secrets are bootstrap-only. Meeting that requires one scoped component and token per app. Each token has to be delivered as bootstrap material (typically a mounted file or Kubernetes Secret), then renewed and rotated outside Dapr. Neither the spine nor the memlog records these constraints.
- Evidence: https://docs.dapr.io/reference/components-reference/supported-secret-stores/openbao/ ("there is no dedicated OpenBao Secrets Store… utilize `secretstores.hashicorp.vault`"). https://docs.dapr.io/reference/components-reference/supported-secret-stores/hashicorp-vault/ (metadata fields; token only).
- Failure scenario: A shared long-lived OpenBao token is mounted for all apps to satisfy "Dapr/OpenBao". That breaks per-app least privilege, and an expired token later fails readiness across the estate.
- Recommendation: Correct the memlog wording to "Dapr `secretstores.hashicorp.vault` against OpenBao". Record the token-auth constraint and name the bootstrap exception for per-app Vault tokens and their renewal owner in `openbao-secret-contract.yaml`.
- Suggested disposition: autofix

### REAL-7 — Observed installed infrastructure includes EOL and unpatched components that "suitable reuse" doesn't flag
- Severity: medium
- Where: AD-3/AD-8 ("existing common Keycloak and suitable supporting infrastructure may be reused"); Deferred "Shared runtime/profile evidence", "Secrets, identity…"
- Finding: The memlog records installed versions as neutral observations, and none was checked for currency:
  - Kubernetes v1.34.9 reaches **EOL on 2026-10-27**; the latest patch is 1.34.11.
  - OpenBao 2.6.2 is behind 2.6.3 and 2.7.0 (both 2026-09-23). 2.6.3 fixes 9 GHSAs, one of which silently dropped a denied ACL policy template. That bears directly on the default-deny secret convention.
  - Redis Stack 7.4 stopped receiving maintenance in Dec 2025, replaced by Redis Open Source 8.
  - FalkorDB 4.12.0 is behind 4.20.7.
  - Keycloak's installed version is unknown (digest only), while community Keycloak patches only its latest release (26.7.4).
- Evidence: https://kubernetes.io/releases/. https://github.com/openbao/openbao/releases/tag/v2.6.3. https://hub.docker.com/r/redis/redis-stack. https://github.com/FalkorDB/FalkorDB/releases. https://www.keycloak.org/downloads and https://github.com/keycloak/keycloak/issues/52656. Memlog L64/L65.
- Failure scenario: Production qualification completes on a single-node cluster whose Kubernetes minor goes out of support about a month later. The forced cluster upgrade is shared infrastructure outside application rollback (AD-3), with no HA and both environments on one node, so it becomes an unplanned outage. The unpatched OpenBao policy bug also undermines the default-deny evidence.
- Recommendation: Make "suitable" in AD-8 include "within upstream support and current on security patches". Add the Kubernetes minor upgrade, OpenBao 2.6.3+, a Redis Stack → Redis 8 decision (Memories owner) and resolution of the installed Keycloak version to the deferred qualification rows.
- Suggested disposition: discuss

### REAL-8 — AD-7 self-hosted deployment runner on public repositories; runner update enforcement not recorded
- Severity: high
- Where: AD-7
- Finding: `Hexalith/Hexalith.Platform` and `Hexalith/Hexalith.Builds` are public. GitHub's current guidance is that self-hosted runners should almost never be used with public repositories, because "any user can open pull requests against the repository and compromise the environment". AD-7 gives this runner production Kubernetes access but binds none of the usual mitigations:
  - a runner group restricted to selected repositories and workflows,
  - environment protection with required reviewers,
  - no `pull_request`/fork-triggered jobs,
  - ephemeral or JIT runners.

  Separately, a runner that isn't updated within 30 days stops receiving jobs. Minimum-version enforcement resumed in 2026 (registration minimum 2.329.0; GHEC full enforcement 2026-09-25). Automatic rollback and recovery (Release and Recovery Acceptance) depend on this runner. Memlog L56 checked only its outbound-443 model.
- Evidence: `gh repo view` shows `"visibility":"PUBLIC"` for Platform and Builds. https://docs.github.com/en/actions/reference/security/secure-use. https://docs.github.com/en/actions/reference/runners/self-hosted-runners ("If you do not perform a software update within 30 days… will not queue jobs"). https://github.blog/changelog/2026-06-12-github-actions-minimum-version-enforcement-timeline-for-self-hosted-runners/.
- Failure scenario: A fork PR changes a workflow to target the deployment runner's labels and exfiltrates cluster credentials. Or the long-lived runner with auto-update blocked stops receiving jobs, and the "one automatic recovery attempt" never executes after a failed production rollout.
- Recommendation: Add these to AD-7 or its binding convention: the runner accepts only protected-environment release workflows from protected refs of named repositories, never PR events, and uses a restricted runner group plus JIT/ephemeral runners where feasible. Also require auto-update reachability and version monitoring. Record the GitHub sources in the memlog.
- Suggested disposition: discuss

### REAL-9 — "Content digest" and Helm major version are imprecise
- Severity: low
- Where: AD-2; memlog L25/L27/L159
- Finding: Helm's verifiable immutable identity is the OCI manifest digest used in `oci://…@sha256:`, which is not the same as a `.tgz` file hash. The spine doesn't say which one binds the release record. Helm 4 (4.3.0) is current and Helm 3.22 was its final feature release. Aspire's Kubernetes pipeline requires Helm ≥ 4.2.0. The memlog cites helm.sh docs with no major version, and the registries page notes it "has not yet been updated for Helm 4".
- Evidence: https://helm.sh/docs/topics/registries/; https://github.com/helm/helm/releases; https://aspire.dev/integrations/compute/kubernetes/ ("Helm v4.2.0 or later").
- Failure scenario: Release records store a `.tgz` sha256 while deployment pulls by OCI tag or manifest digest, so the "same package" check compares incompatible identities. Separately, the deployment runner ships Helm 3 and the Aspire path fails the version check.
- Recommendation: Specify "OCI manifest digest (sha256) of the pushed chart" and a Helm 4.x toolchain pin in the profile inventory.
- Suggested disposition: autofix

### REAL-10 — Site-surviving freshness and failure reporting through GitHub has platform limits
- Severity: low
- Where: Release and Recovery Acceptance → Recovery freshness; Diagnostics and notification
- Finding: A site-surviving monitor would most naturally be a GitHub scheduled workflow. On public repositories, scheduled workflows are auto-disabled after 60 days without repository activity. Scheduled events can also be delayed, or queued jobs dropped, under load, especially at the top of the hour. Either would silently break the "newest usable point older than one hour" alert.
- Evidence: https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows (schedule notes).
- Failure scenario: After a quiet period the freshness check is disabled, backups stall, and no alert is raised during the window when the RPO is being violated.
- Recommendation: In deferred "Release attempt state, checks and notifications", require a heartbeat or dead-man check for the freshness monitor. Avoid relying solely on public-repo `schedule` triggers, or schedule them off the top of the hour and alert on missed runs.
- Suggested disposition: defer

## Checked and clean
- .NET SDK 10.0.401 is the current latest (2026-09-08), and `#:include` in `apphost.cs` is supported from SDK 10.0.300.
- Aspire.AppHost.Sdk, Aspire.Hosting.Docker and Aspire.Hosting.Redis 13.5.4 are the latest stable releases (2026-09-15). The Aspire CLI installed locally is 13.5.3, a minor difference.
- Transitive consistency holds: root pins (Aspire 13.5.4, CT beta.757) are at or above the minimums EventStore.Aspire 3.106.0 needs (13.5.3, beta.752), so there's no downgrade.
- Helm OCI install by immutable digest exists as AD-2 assumes, and Aspire `WithChartVersion` supports a versioned chart.
- Dapr 1.18 is the current supported line. Stable state (PostgreSQL v1), pub/sub (RabbitMQ/Kafka v1), workflows, component scopes and namespaced resources are as the memlog records.
- MCP stdio is still a standard binding in the current 2026-07-28 revision, and ModelContextProtocol 2.2.0 is the latest stable. AD-11's "local stdio MCP → HTTPS gateway" fits.
- Keycloak realm isolation and audience validation are documented as the memlog cites, and 26.7.4 is the current release.
- GitHub-hosted standard Linux runners are fresh VMs, and a self-hosted runner needs only outbound 443 (memlog L38/L56).
- The file-based AppHost limits for `DistributedApplicationTestingBuilder`, and the `aspire start`/`stop` isolation caveats, are recorded with official sources (memlog L38/L108).
- Memlog L22's description of `DaprSelfHostedMtls.cs` matches the code: localhost control plane, bind-mounted credentials, `ExcludeFromManifest`, fixed ports and a named volume.
- FalkorDB exists and is actively maintained (SSPLv1; used only as an internal backend, not offered as a service).
