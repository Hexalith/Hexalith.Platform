# Sprint change proposal for Dapr 1.19.2 adoption

23 November 2026 · Owner approved and planning edits applied · Batch review · Reviewed Platform revision `2e5b8ae7`

## 1. Issue summary

The sprint request is to use the latest stable Dapr release. Dapr published runtime **1.19.2** on November 19 with fixes for actor and workflow readiness, Scheduler behavior, state-store concurrency and security. The official Helm archive identifies both its chart and app version as **1.19.2**. The runtime embeds `components-contrib` **1.19.2**. The CLI and the .NET SDK packages used by Builds are independently released and are both **1.19.1**. [Runtime release](https://github.com/dapr/dapr/releases/tag/v1.19.2) · [Helm chart](https://dapr.github.io/helm-charts/dapr-1.19.2.tgz) · [Runtime dependencies](https://github.com/dapr/dapr/blob/v1.19.2/go.mod) · [CLI release](https://github.com/dapr/cli/releases/tag/v1.19.1) · [NuGet SDK](https://www.nuget.org/packages/Dapr.Client/)

Platform already selects that local toolchain through released Builds **6.6.2**, pinned to `2d9ef8b454ff897daca0149f3549844f584b634a`. The Works tool resolver returned runtime 1.19.2 and CLI 1.19.1 from that catalog. The official Linux amd64 runtime archive checksum and the extracted `daprd` hash matched the upstream checksum and the Platform Works record, respectively. Story 3.3 also records a 46-case local Parties package pass and a signed EventStore extension readiness pass on the reviewed Builds 6.6.2 candidate. Each result applies to its recorded source and scope. [Builds binding](../../eng/builds-tuple.json) · [Builds catalog](../../references/Hexalith.Builds/Props/Directory.Packages.props) · [Works toolchain record](../../config/works-proof-toolchains/1.19.2.json) · [Story 3.3 evidence](../implementation-artifacts/spec-3-3-generate-the-run-scoped-routing-catalog.md)

The gap is hosted qualification. The retained Story 4.5 Helm probe pins 1.18.7, and EventStore's latest retained broker candidate profiles pin 1.19.1 with embedded components 1.19.0. The latest retained hosted broker attempt observed 20 passes and four failures across 24 vectors. The five tracked broker guarantees remain open. The upstream RabbitMQ publish-failure PR remained open at the release check. [Helm qualification runbook](../../eng/helm-qualification/README.md) · [EventStore profiles](../../references/Hexalith.EventStore/deploy/dapr/qualification/README.md) · [Broker intake and dispositions](../../docs/implementation/story-4-17-next-candidate-intake.md) · [Upstream PR](https://github.com/dapr/components-contrib/pull/4767)

## 2. Impact analysis

| Area | Delivery impact |
| --- | --- |
| Epics 1–3 and Works | The Builds catalog and published tool already select the target. Story 2.10 owns runner lifecycle qualification, Story 2.14 owns released tuple adoption, and Epic 3 uses that tuple for the full local composition. Reuse candidate-scoped proof where the identities match. Works needs its existing parity gate for behavioral acceptance. The 1.19.2 executable hash establishes tool identity. |
| Epic 4 | Stories 4.5, 4.9 and 4.16 own maintained-chart qualification, the hosted inventory and the isolated staging policy envelope. Story 4.17 and EventStore 3.21 own the durable broker evidence. Story 4.22 consumes the accepted profile for the staging deployment. |
| Epics 5–6 and 8–10 | Use the exact hosted candidate for staging E2E, compatibility and any applicable restore proof. An upgrade to shared infrastructure follows Story 6.22. Stories 9.1 and 9.2 govern currency and promotion using the approved inventory. Epic 10 consumes that evidence for the seven-module release. |
| Epics 7, 11 and 12 | Production access, legacy retirement and Agents enrollment consume the applicable environment or catalog through their existing gates. Their scope and sequencing need no new work items. |
| PRD, architecture, spec and UX | FR-4, FR-6, FR-8 and FR-10 already define the integration, staging, recovery and isolation outcomes. MVP scope is preserved. The Platform architecture and sequencing spec need a current Dapr selection reference and an explicit hosted evidence dependency. The change has no user-facing screen, flow or accessibility impact. |
| Historical and specialized profiles | Works 1.18.7 behavior, the Memories 1.18.1 retention profile, custody qualification, the old Helm probe and all sealed broker packets retain their recorded identities. A newer claim for any of these lanes needs its own owner-scoped evidence. |

[Platform PRD](prds/prd-platform-2026-09-27/prd.md) · [Epic breakdown](epics.md) · [Sprint status](../implementation-artifacts/sprint-status.yaml) · [Platform spec](../specs/spec-platform/SPEC.md) · [Custody architecture](architecture/architecture-ext-protection-1-custody-2026-10-18/ARCHITECTURE-SPINE.md) · [Memories retention profile](../../src/Hexalith.Platform.Composition/PlatformRetentionProfile.cs)

## 3. Recommended approach

Use **direct adjustment** within the existing stories. Continue local and CI use of the released Builds selection. Prepare hosted 1.19.2 as a versioned candidate, checking official release identities and owner prerequisites before executing qualification. Freeze exact inputs when the candidate starts. Review later upstream releases at the next candidate intake under the existing currency policy.

1. Reconcile the current documentation with the released catalog and check the existing local and CI evidence against the exact intended candidate. Fill missing coverage through the ordinary lifecycle, tuple and Works parity lanes.
2. Prepare the maintained-chart candidate and hosted runtime inventory with EventStore. Capture chart and rendered CRD identities, injector and control-plane images, sidecar digests, embedded components and installed tools. Verify 1.19 Kubernetes CRD and Scheduler RBAC requirements against the starting version. Run the existing isolated ownership and staging envelope checks. [Dapr 1.19 upgrade notes](https://blog.dapr.io/posts/2026/10/22/dapr-v1.19-is-now-available/) · [Kubernetes upgrade documentation](https://docs.dapr.io/operations/hosting/kubernetes/kubernetes-upgrade/)
3. Review whether the official component sources satisfy the outstanding broker prerequisites. When EventStore admits an exact successor and the attempt is authorized, run the existing producer and independent verifier across both brokers and all 24 vectors. Carry the accepted result, profile and configuration identities into exact-release staging and existing production admission.

Estimate **1–2 engineering days** for planning, current-tuple evidence reconciliation and local follow-up, plus **2–4 engineering days** for hosted candidate preparation and disposable rehearsal once inputs are ready. This excludes the wait for upstream remediation, owner decisions and staging capacity. Risk is low for the version selection already covered by the catalog and moderate for hosted qualification. The main risks are reusing evidence for different inputs, misaligned chart and control-plane identities, and broker guarantees that still need verified remediation. Hosted delivery remains constrained by the existing broker and admission dependencies.

A rollback of completed sprint work would discard the released tuple and leave hosted qualification open. The MVP already contains the needed release and recovery gates. Use the existing infrastructure forward-revert procedure if an approved shared rollout fails, with recovery compatibility rehearsed against the actual baseline.

## 4. Detailed change proposals

### Architecture and spec

**Before.** The architecture Stack lists Dapr 1.18 in the historical September seed. The spec sequencing table requires shared runtime and broker evidence before the first staging deployment without identifying which inventory owns current Dapr selection. [Architecture Stack](architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#stack) · [Spec sequencing](../specs/spec-platform/sequencing.md#owned-work-by-gate)

**Proposed.** Add this text to Stack above the historical table.

> The released Builds catalog supplies the exact local and CI Dapr runtime, CLI and .NET SDK versions. The hosted profile inventory binds the exact Dapr chart, CRDs, control-plane and sidecar images, embedded component revision and qualified configuration. Select the latest stable runtime at candidate intake and freeze the selected identities through qualification and release.

Add this sentence to the spec's “First staging deployment” row.

> Use the Story 4.9 Dapr inventory that passed the Story 4.5 chart checks, Story 4.16 staging envelope and Story 4.17 broker qualification for the same runtime candidate.

These edits establish version ownership and bind evidence to its recorded inputs. The Dapr boundary and existing topology diagrams still apply.

### Epics 2–3 and Works

**Before.** Story 2.10 scenario evidence records source revision, environment, outcome, timings and diagnostics. Story 2.14 records expected and loaded artifact identities. The Works runbook binds the executable, while the source-release documentation calls out 1.19.1 as current. [Story 2.10](epics.md#story-210-qualify-the-runner-lifecycle-on-the-reference-composition) · [Story 2.14](epics.md#story-214-pin-builds-and-owner-modules-by-released-version) · [Works parity](../../docs/works-host-parity.md) · [Source release notes](../../docs/source-releases.md)

**Proposed.** Extend Story 2.10's scenario record to identify the actual Dapr CLI and `daprd` runtime with the selected released Builds catalog and SDK package lock. Use the existing Story 2.14 identity checks for the tested revision. Update the source-release runbook to point to the catalog-backed Works record and keep its 1.18.7 behavioral evidence dated. Qualify missing current behavior through the existing lanes, including workload readiness, cleanup and the complete Works parity suite. This makes a resolver or binary identity check distinguishable from observed runtime behavior.

### Epic 4 hosted qualification

**Before.** Story 4.5 requires fresh maintained-chart render and ownership evidence. Story 4.9 pins the Dapr control plane and deploy toolchain. Story 4.16 tests staging trust and sidecar restarts. Story 4.17 owns broker selection and its hosted guarantees, with the retained local/CI profiles still identified by their 1.19.1 inputs. [Story 4.5](epics.md#story-45-qualify-the-aspire-to-helm-export-with-the-reference-composition) · [Story 4.9](epics.md#story-49-ratify-the-hosted-runtime-and-identity-event-contracts) · [Story 4.16](epics.md#story-416-apply-the-staging-dapr-trust-domain-and-runtime-policy) · [Story 4.17](epics.md#story-417-select-and-qualify-the-durable-production-broker)

**Proposed.** Extend Story 4.9's inventory criterion with the exact identities listed in the architecture change. In 4.5, use a versioned 1.19.2 qualifier input with a verified chart, canonical CRDs and aligned images, and repeat the three render modes and isolated install, upgrade, recovery and ownership checks. In 4.16, bind the envelope evidence to that same inventory, including mTLS caller identity, deny-by-default invocation, component scope, WorkflowAccessPolicy, readiness, restricted sidecar privileges and the two required restart cases. Update the qualifier runbook's current catalog reference.

In Story 4.17, add a successor-candidate intake criterion. EventStore 3.21 owns the explicit 1.19.2 lock and independent verification, including source provenance for the broker fixes the candidate claims. Platform owns the bounded candidate configuration and qualification request. Evaluate the existing failure predicates before allocating an attempt. A missing prerequisite or failed vector keeps the related guarantee open. Broker selection still requires the original staging and AD-26 acceptance. This preserves the provenance of the 1.19.1 profiles and prevents a version label from substituting for observed guarantees. [EventStore broker qualification](../../references/Hexalith.EventStore/deploy/dapr/qualification/README.md) · [AD-26 preparation](../../docs/contracts/ad26-preparation/README.md)

### Epic 5 release evidence

**Before.** Stories 5.6 and 5.8 bind the staging result to the exact release, profile, configuration and production baseline. [Story 5.6](epics.md#story-56-run-exact-release-staging-e2e-in-a-serialized-staging-attempt) · [Story 5.8](epics.md#story-58-rehearse-the-rollback-set-and-record-compatibility-evidence)

**Proposed.** In 5.6, record observed Dapr runtime and profile constituent identities under the existing evidence binding. In 5.8, exercise baseline readback of candidate-written state and events when the Dapr runtime changes. Use the approved recovery path if compatibility fails. The profile comparison can then detect a runtime or component mismatch before production admission.

## 5. Implementation handoff

**Scope is moderate.** The Product Owner and Developer handoff uses the existing stories and their current sprint states. Chart qualification (4.5) supplies results for the inventory (4.9). The staging envelope (4.16) and broker evidence (4.17 and EventStore 3.21) qualify that same inventory before the reference release (4.22). Stories 5.6 and 5.8 consume it for release evidence and baseline readback. Local lifecycle (2.10) and Works parity can proceed independently when their normal prerequisites are available. [Sprint tracking](../implementation-artifacts/sprint-status.yaml)

| Owner | Responsibility and acceptance |
| --- | --- |
| Platform and Builds developers | Confirm the released Dapr selection through ordinary source and locked package paths. Retain exact version and artifact readback, required tuple results and Story 2.10 outcomes. Works and Platform maintainers publish the complete current-runtime parity result before crediting 1.19.2 behavior for Works. |
| Platform hosted maintainer and EventStore owner | Deliver the versioned 1.19.2 hosted inventory, aligned chart checks and envelope evidence. Bind broker results to exact tools and configuration. Require independently verified completion of all 24 vectors with attributable outcomes before crediting a broker guarantee or selection under its existing criteria. |
| Platform operations and Administrator | Require the exact staged suite and applicable baseline or named recovery evidence before promotion. For an existing shared installation, use the Story 6.22 locks, recovery point, rehearsal rules and postchecks. Record observed Dapr identities in the attempt and use them for the currency check. |

### Workflow execution log

On 23 November 2026 the owner selected **Continue and approve**. The Developer applied the approved wording in the architecture stack, Platform sequencing spec and Epic 2, 4 and 5 acceptance criteria. The staging sequencing entry refers to the Story 4.22 reference release so staging qualification workloads can run before it. The source-release and Helm qualification runbooks now identify the catalog as the current authority. The sprint record links the approved dependencies with the existing story IDs and statuses. [Architecture stack](architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#stack) · [Sequencing](../specs/spec-platform/sequencing.md#owned-work-by-gate) · [Epics](epics.md) · [Source releases](../../docs/source-releases.md) · [Helm qualification](../../eng/helm-qualification/README.md) · [Sprint tracking](../implementation-artifacts/sprint-status.yaml)

Checklist 1.2–1.3, 2.1–2.5, 3.1–3.4, 4.1–4.4, 5.1–5.5 and 6.1–6.5 are complete. Item 1.1 is N/A because the stakeholder request triggered the change. Release metadata, catalog resolution and the Works executable identity were verified during planning. The delivery owners have the approved acceptance criteria and tracking links. Current Works parity, hosted qualification and release acceptance remain delivery work. An exact admitted broker successor, including verified remediation, is required before seeking authorization for a hosted broker attempt.

Handoff verification passed for all eight Works toolchain tests, the added local links, the sprint status mappings and Markdown whitespace.
