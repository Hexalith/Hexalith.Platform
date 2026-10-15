---
title: '1.7 Ratify local Dapr conventions and bind component names'
type: 'feature'
epic: 1
story: 7
created: '2026-10-15'
status: 'draft'
baseline_commit: 'c3af1f2c9e7b0d1b003a1bd5978545f664e38167'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Declaration composition uses fixed component names, allow-by-default ACL templates and no local mTLS or declarative subscriptions. Existing topic and interface declarations cannot express subscriber routes or precise HTTP invocation permissions.

**Approach:** Ratify local Dapr conventions through `bmad-architecture` and apply them to the shared composition and owner contracts.

## Boundaries & Constraints

**Always:** Render security and bindings before adding resources; failures identify source, field and reason. Give every receiver a deny-by-default policy with explicit caller identity, path and verb grants. Match sidecar, Sentry and control-plane trust settings. Assign deterministic component names, inject them through configuration keys actually read by owners, and restrict scopes to intended selected servers. Preserve shared-state readers and Memories telemetry-store isolation. Render subscriptions only from explicit subscriber bindings, preserving dead-letter policy and disabling duplicate programmatic discovery. Publisher inventory alone creates no subscription.

Record `Hexalith.EventStore.Aspire`, `Hexalith.EventStore.ServiceDefaults` and the toolkit as canonical, consulting Commons. Builds owns versions, including local Dapr images; reconcile file-entry drift. Refuse new hosting helpers until recorded. Cover projects, containers and signed Builds runs. Preserve isolated defaults, Works, readiness/cleanup and snapshot provenance.

**Never:** Guess routes or HTTP methods, broaden grants to pass startup, migrate hosted environments, introduce provider exceptions, or initialize nested references. Hosted policy and concurrent-run isolation remain later stories. Separate release/image availability from fixture evidence.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Local invocation | Selected receivers and explicit caller grants | mTLS; allowed invocation succeeds; unlisted caller is denied | No permissive fallback |
| Capability | Logical role, recovery class, consuming servers and configuration keys | YAML name, owner options and sidecar references agree; exact scopes | Conflicting/unsupported bindings fail before resources |
| Topic | Publisher inventory or explicit subscriber binding | Only subscribers get scoped Subscription objects using bound pubsub name and delivery route | Missing receiver/route or invalid dead-letter target refuses |

</frozen-after-approval>

## Open Questions

1. Where should missing server scopes, subscription bindings and exact invocation rules live? **Extend v2 declarations (recommended):** optional explicit fields in Builds' closed schema, then migrate owner manifests; one validated contract. **Owner-authored companion contract:** retain v2 shape and add a separately validated binding file per module, including package snapshot provenance. Both require explicit owner data; neither infers routes or subscriptions.

## Code Map

- `src/Hexalith.Platform.Composition/PlatformAppModel.cs` — atomic preparation, resources and template rendering; `PlatformLocalProfile.cs` assigns literals; `PlatformDaprEndpointSubscriber.cs` configures actual generated CLI processes.
- `DaprSelfHostedMtls.cs` — Works foundation: project-only, fixed ports, runtime 1.18.3 versus catalog 1.18.2.
- `references/Hexalith.Builds/schemas/hexalith.module-manifest.v2.json` — topics lack direction/server/route; operations lack paths/verbs; capabilities lack server scope. Story 1.1 permits optional additions within v2.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` hardcodes resource names. Generic `Dapr:*Component` declaration keys do not configure actual publisher/projection/state options.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md`, `docs/platform-apphost-form.md` — use the architecture workflow to record convention, canonical package ownership, binding rules and helper gate.
- [ ] `references/Hexalith.Builds/{schemas/hexalith.module-manifest.v2.json,src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs}`, `references/{Hexalith.EventStore,Hexalith.Memories,Hexalith.Tenants}/module/hexalith.module-manifest.v2.json` — implement the chosen metadata contract and accurate owner bindings; keep v1 compatibility and atomic diagnostics.
- [ ] `src/Hexalith.Platform.Composition/{PlatformDaprBindings.cs,PlatformAppModel.cs,PlatformLocalProfile.cs,PlatformServerBindings.cs}`, `DaprComponents/reference/` — resolve capabilities once; render names, scopes, per-receiver ACLs and subscriptions; bind actual owner configuration keys.
- [ ] `DaprSelfHostedMtls.cs`, `src/Hexalith.Platform.Composition/PlatformDaprEndpointSubscriber.cs`, `apphost.cs` — integrate authenticated local control plane for projects/containers and signed plans, using catalog pins and existing lifecycle ownership.
- [ ] `references/Hexalith.EventStore/src/{Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs,Hexalith.EventStore.DomainService/EventStoreDomainEventsEndpointExtensions.cs,Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs,Hexalith.EventStore/Controllers/ProjectionNotificationController.cs,Hexalith.EventStore.Server/Configuration/ProjectionChangeNotifierOptions.cs}`, `references/Hexalith.Memories/src/{Hexalith.Memories.EventStore/EventIngestionController.cs,Hexalith.Memories.Server/Program.cs}`, `references/Hexalith.Tenants/src/Hexalith.Tenants/Program.cs` — accept assigned names and declarative mode while retaining delivery endpoints and isolated defaults.
- [ ] `tests/Hexalith.Platform.Composition.Tests/{PlatformDaprBindingsTests.cs,PlatformAppModelTests.cs,BuildsSourceStartupTests.cs}`, `references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/PlatformManifestValidationTests.cs` — test matrix, effective CLI configuration, subscription delivery/dead-letter handling and real allowed/denied mTLS invocation; run affected owner regressions.

**Acceptance Criteria:**
- Given the canonical set is absent/present in the spine, When a new hosting helper is evaluated, Then it is refused/accepted against that recorded set.
- Given reference-composition checks, When results are retained, Then evidence distinguishes live enforcement/delivery from model assertions.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

- Build `Hexalith.Platform.slnx` and the Platform test project in Debug; run its direct xUnit executable and affected Builds/owner tests.
- Run `python3 scripts/test-reference-module-declarations.py` and `eng/verify-agents-host.sh --mode LocalScaffold`.
- Extend the existing gated Docker/Dapr source smokes to prove permitted invocation, denied caller, declarative delivery and teardown. Inspect package graph/security with verified fixtures; do not claim live package qualification while release/image prerequisites are absent.
