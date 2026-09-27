---
title: "Hexalith Platform: Landscape Research"
researched: 2026-09-27
status: discovery-input
source_policy: official-primary-sources-only
---

# Hexalith Platform: Landscape Research

This research supports coaching questions for an internal platform. It does not change the confirmed scope: EventStore, Tenants, Parties, Folders, Projects, McpCli, and Memories; local Aspire development; local and CI tests; Kubernetes staging and production; and use of Platform as a direct Git submodule. Sources were accessed on 2026-09-27. Findings describe documented capabilities, not verified Hexalith implementation.

## 1. Shared composition can feed distinct deployment contexts

**Finding.** Aspire documents an AppHost-driven deployment pipeline. Resources contribute target-specific steps; environment context and compute target are separate concepts. Publishing emits artifacts for another system to apply, whereas direct deployment generates and applies its own output rather than consuming a prior publish result. [Aspire deployment model](https://aspire.dev/deployment/deploy-with-aspire/)

**Product implication — inference.** A common composition fits the brief, but it does not settle who owns release orchestration or what evidence makes an environment ready.

**Undecided policy.** What result must Platform expose for a successful environment provision, and where does its responsibility end relative to Hexalith.Builds and the CI system? Implementation remains architecture work.

## 2. Existing-cluster deployment is a documented Aspire target

**Finding.** Aspire documents generating Helm charts from the AppHost for an existing Kubernetes cluster. Its direct Kubernetes deployment path uses the current kubectl context. Project/container resources map to workload resources, endpoints to Services, and configuration to ConfigMaps/Secrets. [Aspire Kubernetes deployment](https://aspire.dev/deployment/kubernetes/)

**Product implication — inference.** The chosen local Kubernetes installation is compatible with the documented target category; this does not verify its readiness, capacity, storage, ingress, certificates, or compatibility with the installed Aspire version. Explicit target identity matters when staging and production share infrastructure.

**Undecided policy.** What must a caller see to distinguish staging, production, and a CI test environment, and what operational evidence is required before either hosted environment counts as usable?

## 3. Minimum composition and readiness are explicit concerns

**Finding.** Aspire project references pass dependency connection and service-discovery information into applications. That documentation describes explicit references, not automatic discovery of Hexalith's minimum module compositions. [Aspire project resources](https://aspire.dev/integrations/dotnet/project-resources/)

**Comparable context.** Tilt explicitly models resource dependencies, starts transitive dependencies when selecting a resource, and distinguishes startup readiness from continuing version compatibility. This is a useful comparison for the experience a developer expects, not a recommendation to change tooling. [Tilt resource dependencies](https://docs.tilt.dev/resource_dependencies.html)

**Product implication — inference.** “Minimum components” needs an owner and an observable meaning: required dependencies, optional dependencies, readiness, and failure reporting.

**Undecided policy.** Who supplies the supported dependency set for each module, and what should a developer experience if a required component is unavailable? The brief already leaves real dependencies versus test doubles open.

## 4. Automated AppHost tests have lifecycle support, but CI policy remains open

**Finding.** Aspire's testing support launches the application and resources as separate processes, randomizes proxied ports by default, and cleans up application resources when disposed. These are distributed integration tests; the separate-process boundary prevents direct in-process dependency-injection replacement from test code. [Aspire testing overview](https://aspire.dev/testing/overview/)

**Comparable context.** Skaffold profiles separately vary build, test, and deployment configuration by context, with explicit or automatic activation. [Skaffold profiles](https://skaffold.dev/docs/environment/profiles/)

**Product implication — inference.** Environment variation is a normal platform concern. Port randomization alone does not establish isolation of shared external data or cleanup following an interrupted CI runner.

**Undecided policy.** Where do CI environments run, may runs overlap, what data starts each run, and what must happen to resources and diagnostic evidence after success, failure, or cancellation?

## 5. Promotion needs an identifiable tested release

**Finding.** Kubernetes image tags can be reassigned, while content digests identify immutable image content. NuGet resolves a transitive dependency graph; its locked restore mode can restore the recorded graph or fail when the inputs no longer match. [Kubernetes images](https://kubernetes.io/docs/concepts/containers/images/), [NuGet dependency locking](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies)

**Product implication — inference.** The established local Debug/source and CI Release/NuGet distinction makes release identity and package availability relevant. “The release passed staging” requires a way to identify what was tested and subsequently deployed; selecting a technical representation belongs to architecture.

**Undecided policy.** What identifies a complete seven-module release, can only a subset change, and what should happen if an expected package version is unavailable? No new build-mode or source-fallback policy is established here.

## 6. Automatic rollback is broader than a Deployment revision

**Finding.** A Kubernetes Deployment reports a stalled rollout; the controller does not itself roll it back. Rolling back a Deployment restores its Pod template, and retained revision history governs which revisions are available. [Kubernetes Deployment failure and rollback behavior](https://kubernetes.io/docs/concepts/workloads/controllers/deployment/)

**Product implication — inference.** The brief's automatic rollback requires an explicit failure signal and an actor that performs recovery. Reverting a workload specification alone does not establish restoration of application data, compatibility with changed schemas/events, or recovery of a coherent multi-module release.

**Undecided policy.** Which production failures trigger automatic rollback, over what observation window, what constitutes the previous working release, and what happens when rollback cannot restore service? Acceptable downtime and data loss remain user decisions.

## 7. Shared infrastructure requires more than distinct names

**Finding.** Kubernetes multi-tenancy guidance separates access control, resource quotas, network isolation, and storage isolation. Pods can communicate across namespaces by default; network policies need a supporting network implementation. Quotas do not remove every shared-resource effect. [Kubernetes multi-tenancy](https://kubernetes.io/docs/concepts/security/multi-tenancy/)

**Product implication — inference.** The confirmed separation of staging/production application data and credentials needs observable outcomes. Separate domains or namespaces alone do not demonstrate that separation, and shared capacity can let staging activity affect production.

**Undecided policy.** Which supporting services may be shared, what cross-environment access is permitted, and what production impact from staging is acceptable? The supplied server IP does not establish node count or availability.

## Version and evidence limits

The repository's [AppHost](../../../../apphost.cs) currently pins Aspire.AppHost.Sdk, Aspire.Hosting.Docker, and Aspire.Hosting.Redis to **13.5.4**, and its Dapr integration to **13.5.1-beta.757**. This read-only observation is dated 2026-09-27. The official websites above are rolling documentation rather than a snapshot certified for those versions. No source-to-repository publication-date discrepancy was established; feature/version compatibility was not tested. In particular, this research does not certify Kubernetes integration availability or deployment behavior for the pinned dependencies.

The authoritative product inputs remain the [brief](../../briefs/brief-platform-2026-09-27/brief.md) and [brief addendum](../../briefs/brief-platform-2026-09-27/addendum.md). Their root-only submodule policy and local-versus-CI reference rules remain constraints. No clusters were contacted, workloads deployed, dependency versions changed, or product decisions authored during this research.
