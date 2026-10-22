---
title: 'Qualify the Aspire-to-Helm export with the reference composition'
type: 'feature'
epic: 4
story: 5
created: '2026-10-22'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
story_key: '4-5-qualify-the-aspire-to-helm-export-with-the-reference-composi'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Platform has no qualified reference-composition Helm package. Its Development composition creates local infrastructure and literal secrets; the Timesheets chart is only an enrollment candidate.

**Approach:** Share declaration-derived workload metadata between local composition and hosted publishing. Probe native Aspire publishing; if an AD-1 trigger is demonstrated, record it and select one small maintained chart with parity checks. Qualify the selected package independently of future publication and staging deployment.

## Boundaries & Constraints

**Always:** Include Parties, EventStore, Tenants and Memories and every enabled server. Use one shared Dapr/security helper. Bind hosted images by digest through values, data services by non-secret endpoints, and credentials by existing references. Record actual tooling identities: current Aspire is 13.6.1, rather than the story's historical 13.5.4. Preserve unrelated work and local behavior.

**Never:** Duplicate topology, hand-model module Dapr plumbing, patch generated files repeatedly, or build a custom exporter. Create no Gateway, Secret, data-service workload or persistent infrastructure in the application chart. Do not publish, mutate existing clusters, upgrade dependencies, alter module declarations, or claim OCI-attestation/off-site-restoration acceptance.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Modes | Same inputs; normal/recovery/remove-workloads | Normal serves; recovery closes public routes and quiesces workloads; removal omits workloads/routes and preserves environment resources | Unknown modes fail |
| Bindings | Image digests, endpoint parameters, existing credential references | Deterministic output; HTTPRoutes name Gateway and namespace | Missing bindings, tags, credential literals and unsafe endpoints fail |
| Export boundary | Native export requires an AD-1 forbidden approach | Record evidence; maintained chart replaces exporter for downstream stories | Incomplete probes never count as qualification |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Composition/PlatformDeclarationSet.cs` — canonical dependency selection. `PartiesAdoptionTests.Options` selects the actual Parties declaration and seven-server closure.
- `src/Hexalith.Platform.Composition/PlatformServerBindings.cs` — server identities/readiness, currently coupled to `PlatformLocalProfile.Bind`; extract neutral metadata before local secrets are generated.
- `src/Hexalith.Platform.Composition/PlatformDaprBindings.cs` — reuse capability names, consumers, ACLs and subscriptions; its current renderer is self-hosted.
- `src/Hexalith.Platform.Composition/PlatformAppModel.cs` and `apphost.cs` — retain one composition entry; current Compose adds local providers and credentials.
- `eng/timesheets-deployment/chart/` and `test_hosted_chart.py` — reuse chart/security/testing patterns; preserve their candidate status. `eng/cluster-management/rehearse.py` demonstrates owned kind targeting/cleanup.

## Tasks & Acceptance

**Execution:**
- [ ] `src/Hexalith.Platform.Composition/PlatformApplicationGraph.cs`, `PlatformServerBindings.cs`, `PlatformDaprBindings.cs` — expose shared neutral workload/Dapr metadata; local binding consumes the same graph without behavior changes.
- [ ] `src/Hexalith.Platform.Composition/PlatformHostedPublishing.cs`, `Hexalith.Platform.Composition.csproj`, `apphost.cs` — add hosted selection and a native publishing probe using catalog-pinned APIs; avoid local secret/provider setup. Record the export decision and trigger evidence in `eng/helm-qualification/README.md`.
- [ ] `eng/helm-qualification/chart/` — when fallback is triggered, supply the single generic maintained chart. Otherwise use native generated output. Centralize Dapr/security, namespaced Gateway references, digest/endpoint/reference values and all three modes; separate application-owned Dapr objects from environment prerequisites.
- [ ] `tests/Hexalith.Platform.Composition.Tests/PlatformHostedPublishingTests.cs` — test graph parity, selection and local regression. `eng/helm-qualification/test_chart.py` — exercise the matrix, secret/provider exclusion, two identical renders per mode, archive parity and changed-digest values without regeneration.
- [ ] `eng/helm-qualification/qualify.py` and `toolchain.json` — pin verified Helm 4.2.0+, node image, Gateway CRDs and catalog Dapr patch. Create only an invocation-owned kind cluster/kubeconfig; enforce restricted policy. Server-dry-run actual Pod templates through Dapr injection, inspect sidecar/init security, and require an unsafe Pod rejection. Install/upgrade the archive with Helm SSA; prove Apply ownership, unforced conflict refusal and removal boundaries. Bound waits and clean up only owned resources on failure/success.
- [ ] `_bmad-output/implementation-artifacts/evidence/4-5/qualification.json` and `eng/helm-qualification/README.md` — retain commands, exact source/input/tool/package hashes, results, exclusions and cleanup evidence; synchronize the existing sprint key only to demonstrated progress.

**Acceptance Criteria:**
- Given the canonical reference graph, when either selected publishing path renders, then every enabled server has matching identity, readiness and shared Dapr/security configuration, with no independent module topology.
- Given the packaged chart, when qualified, then every mode renders identically twice and matches source-chart output; changing image digests requires values only, and Dapr HotReload stays disabled.
- Given restricted admission with Dapr installed, when each workload's Pod is dry-run, then the response includes a compliant injected sidecar; the unsafe control is rejected. A Deployment-only dry-run is insufficient.
- Given Helm SSA and separately owned environment fixtures, when install, conflicting upgrade and removal run, then ownership/conflict behavior is proven and environment fixtures survive.
- Given missing tools or failed checks, when reporting, then qualification fails explicitly; skipped checks cannot close this story.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Hosted publishing must bypass local secret generation, not redact its output. Native export remains preferred until measured against AD-1. The existing Timesheets fallback is precedent, not proof that the complete reference chart is qualified. Recovery rendering quiesces workloads; it does not perform data restoration.

## Verification

- `dotnet test --project tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj` — run focused hosted and existing declaration/composition regressions using the repository runner.
- `python3 -m unittest discover -s eng/helm-qualification -p 'test_*.py' -v` — real Helm checks pass without skips.
- `python3 eng/helm-qualification/qualify.py` — owned-cluster admission/SSA evidence passes and cleanup completes; never use the ambient kubeconfig.
